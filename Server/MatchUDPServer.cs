
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;

namespace OpenGSServer
{
    /// <summary>
    /// Match UDP server — LiteNetLib transport with JSON payloads.
    /// Payload format matches OpenGSR client (UTF-8 JSON bytes).
    /// </summary>
    public sealed class MatchUDPServer : IDisposable
    {
        private const string ConnectionKey = "OpenGS";
        private static readonly Encoding Utf8 = Encoding.UTF8;

        private NetManager? _server;
        private readonly EventBasedNetListener _listener = new();
        private readonly ConcurrentDictionary<string, PlayerConnectionInfo> _connectedPlayers = new();
        private readonly ConcurrentDictionary<string, UdpTokenEntry> _udpTokens =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _lastShotTicks = new();
        private readonly ConcurrentDictionary<string, WeaponAmmoState> _weaponAmmo = new();
        private readonly ConcurrentDictionary<string, PlayerWeaponLoadout> _weaponLoadouts = new();
        private readonly ConcurrentDictionary<string, PlayerGrenadeLoadout> _grenadeLoadouts = new();
        private readonly ConcurrentDictionary<string, byte> _configuredGrenadeLoadouts = new();
        private readonly ConcurrentDictionary<string, long> _lastGrenadeThrowTicks = new();
        private readonly ConcurrentDictionary<string, long> _attackBuffUntil = new();
        private readonly ConcurrentDictionary<string, long> _defenseBuffUntil = new();
        private int _udpTokenCleanupCounter;
        private bool _disposed;

        public bool IsRunning => _server?.IsRunning ?? false;
        public int ConnectedPlayerCount => _connectedPlayers.Count;

        public string GetOrCreateUdpToken(string roomId, string playerId)
        {
            if (string.IsNullOrWhiteSpace(roomId) || roomId.Length > 128 ||
                string.IsNullOrWhiteSpace(playerId) || playerId.Length > 128)
                return string.Empty;

            var now = DateTime.UtcNow;
            var key = TokenKey(roomId, playerId);
            var entry = _udpTokens.AddOrUpdate(key,
                _ => CreateUdpTokenEntry(now),
                (_, current) => current.ExpiresAtUtc > now ? current : CreateUdpTokenEntry(now));
            if ((Interlocked.Increment(ref _udpTokenCleanupCounter) & 63) == 0)
            {
                foreach (var candidate in _udpTokens)
                    if (candidate.Value.ExpiresAtUtc <= now)
                        ((ICollection<KeyValuePair<string, UdpTokenEntry>>)_udpTokens).Remove(candidate);
            }
            return entry.Token;
        }

        private static UdpTokenEntry CreateUdpTokenEntry(DateTime now)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return new UdpTokenEntry(token, now.AddHours(12));
        }

        private static string TokenKey(string roomId, string playerId) =>
            $"{roomId.Length}:{roomId}{playerId}";

        private bool ValidateUdpToken(string roomId, string playerId, string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 128 ||
                !_udpTokens.TryGetValue(TokenKey(roomId, playerId), out var entry))
                return false;
            if (entry.ExpiresAtUtc <= DateTime.UtcNow)
            {
                ((ICollection<KeyValuePair<string, UdpTokenEntry>>)_udpTokens)
                    .Remove(new KeyValuePair<string, UdpTokenEntry>(TokenKey(roomId, playerId), entry));
                return false;
            }

            var expected = Encoding.UTF8.GetBytes(entry.Token);
            var supplied = Encoding.UTF8.GetBytes(token);
            return expected.Length == supplied.Length &&
                CryptographicOperations.FixedTimeEquals(expected, supplied);
        }

        public void Listen(int port)
        {
            if (_server != null)
            {
                ConsoleWrite.WriteMessage("[UDP] Server already running", ConsoleColor.Yellow);
                return;
            }

            ConsoleWrite.WriteMessage($"[UDP] Starting server on port {port}...", ConsoleColor.Cyan);

            _server = new NetManager(_listener);
            _server.Start(port);

            StartSnapshotBroadcast(50);

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.NetworkErrorEvent += OnNetworkError;

            ConsoleWrite.WriteMessage($"[UDP] Server started on port {port}", ConsoleColor.Green);
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            // The connection data contains only the shared key. Player identity is sent
            // in the first ClientConnect JSON packet after LiteNetLib accepts the peer.
            if (request.AcceptIfKey(ConnectionKey) == null)
                ConsoleWrite.WriteMessage("[UDP] Connection rejected (invalid connection key)", ConsoleColor.Yellow);
        }

        private void OnPeerConnected(NetPeer peer)
        {
            ConsoleWrite.WriteMessage($"[UDP] Transport connected: {peer.Id} from {peer.EndPoint}; awaiting ClientConnect", ConsoleColor.Green);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            string? playerId = peer.Tag?.ToString();
            if (!string.IsNullOrEmpty(playerId) &&
                _connectedPlayers.TryGetValue(playerId, out var current) && current.PeerId == peer.Id &&
                RemoveConnectedPlayer(playerId, current))
            {
                MatchServerV2.Instance.ServerLagCompensationManager.RemovePlayer(playerId);
                ClearPlayerShotCooldowns(playerId);
                _lastGrenadeThrowTicks.TryRemove($"{current.MatchRoomId}|{playerId}", out _);
            }

            ConsoleWrite.WriteMessage(
                $"[UDP] Peer disconnected: {playerId ?? "unauthenticated"} ({peer.Id}) (Reason: {info.Reason})",
                ConsoleColor.Yellow);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);
        }

        private void HandleClientConnect(NetPeer peer, JObject json)
        {
            string playerId = json.Value<string>("PlayerID") ?? json.Value<string>("PlayerId") ?? string.Empty;
            string roomId = json.Value<string>("RoomID") ?? json.Value<string>("RoomId") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(roomId))
            {
                ConsoleWrite.WriteMessage("[UDP] ClientConnect rejected (PlayerID or RoomID missing)", ConsoleColor.Yellow);
                peer.Disconnect();
                return;
            }
            if (!string.IsNullOrEmpty(peer.Tag?.ToString()) &&
                !string.Equals(peer.Tag.ToString(), playerId, StringComparison.Ordinal))
            {
                ConsoleWrite.WriteMessage("[UDP] ClientConnect rejected (peer identity cannot change)", ConsoleColor.Yellow);
                peer.Disconnect();
                return;
            }

            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => !candidate.Finished && candidate.ContainsPlayer(playerId) &&
                    (string.Equals(candidate.Id, roomId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(candidate.WaitRoomLink?.RoomId, roomId, StringComparison.OrdinalIgnoreCase)));
            if (room == null)
            {
                ConsoleWrite.WriteMessage($"[UDP] ClientConnect rejected for {playerId}: room membership not found", ConsoleColor.Yellow);
                peer.Disconnect();
                return;
            }
            string udpToken = json.Value<string>("UdpToken") ?? string.Empty;
            if (!ValidateUdpToken(room.Id, playerId, udpToken))
            {
                ConsoleWrite.WriteMessage($"[UDP] ClientConnect rejected for {playerId}: invalid or expired UDP token", ConsoleColor.Yellow);
                peer.Disconnect();
                return;
            }

            if (_connectedPlayers.TryGetValue(playerId, out var previous))
            {
                RemoveConnectedPlayer(playerId, previous);
                var previousPeer = _server?.GetPeerById(previous.PeerId);
                if (previousPeer != null && previousPeer.Id != peer.Id)
                    previousPeer.Disconnect();
            }

            peer.Tag = playerId;
            var connectedInfo = new PlayerConnectionInfo
            {
                PeerId = peer.Id,
                EndPoint = peer.EndPoint.ToString(),
                ConnectedAt = DateTime.UtcNow,
                PlayerId = playerId,
                MatchRoomId = room.Id,
                ClientRoomId = room.WaitRoomLink?.RoomId ?? room.Id
            };
            _connectedPlayers[playerId] = connectedInfo;
            var loadout = _weaponLoadouts.GetOrAdd(
                $"{room.Id}|{playerId}", _ => new PlayerWeaponLoadout());
            _grenadeLoadouts.GetOrAdd($"{room.Id}|{playerId}", _ => new PlayerGrenadeLoadout());

            var lagCompensation = MatchServerV2.Instance.ServerLagCompensationManager;
            lagCompensation.AddPlayer(playerId);
            lagCompensation.RegisterClientCallback(playerId, state => SendTransformStateToClient(peer, state));
            var startingAmmo = GetWeaponAmmo(
                playerId, connectedInfo.MatchRoomId, "Pistol", "Pistol");
            SendAmmoUpdate(peer, connectedInfo, playerId,
                "Pistol", "Pistol", startingAmmo);
            SendJson(peer, new JObject
            {
                ["MessageType"] = "FieldItemStateSync",
                ["RoomID"] = connectedInfo.ClientRoomId,
                ["RoomId"] = connectedInfo.ClientRoomId,
                ["Items"] = MatchRoomManager.Instance.GetFieldItemSnapshot(connectedInfo.MatchRoomId)
            }, DeliveryMethod.ReliableOrdered);
            ConsoleWrite.WriteMessage($"[UDP] Player connected: {playerId} to room {room.Id} ({peer.Id})", ConsoleColor.Green);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);
        }

        private bool RemoveConnectedPlayer(string playerId, PlayerConnectionInfo expected)
        {
            return ((ICollection<KeyValuePair<string, PlayerConnectionInfo>>)_connectedPlayers)
                .Remove(new KeyValuePair<string, PlayerConnectionInfo>(playerId, expected));
        }

        public void ClearRoomState(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) return;

            string roomPrefix = $"{roomId}|";
            RemoveRoomEntries(_weaponAmmo, roomPrefix);
            RemoveRoomEntries(_weaponLoadouts, roomPrefix);
            RemoveRoomEntries(_grenadeLoadouts, roomPrefix);
            RemoveRoomEntries(_configuredGrenadeLoadouts, roomPrefix);
            RemoveRoomEntries(_lastGrenadeThrowTicks, roomPrefix);
            RemoveRoomEntries(_attackBuffUntil, roomPrefix);
            RemoveRoomEntries(_defenseBuffUntil, roomPrefix);

            string tokenPrefix = $"{roomId.Length}:{roomId}";
            foreach (var tokenKey in _udpTokens.Keys)
                if (tokenKey.StartsWith(tokenPrefix, StringComparison.Ordinal))
                    _udpTokens.TryRemove(tokenKey, out _);

            foreach (var pair in _connectedPlayers)
            {
                if (!string.Equals(pair.Value.MatchRoomId, roomId, StringComparison.OrdinalIgnoreCase))
                    continue;

                ClearPlayerShotCooldowns(pair.Key);
                MatchServerV2.Instance.ServerLagCompensationManager.RemovePlayer(pair.Key);
            }
        }

        private static void RemoveRoomEntries<T>(ConcurrentDictionary<string, T> entries, string roomPrefix)
        {
            foreach (var key in entries.Keys)
                if (key.StartsWith(roomPrefix, StringComparison.OrdinalIgnoreCase))
                    entries.TryRemove(key, out _);
        }

        private bool TryGetAuthenticatedConnection(NetPeer peer, out string playerId, out PlayerConnectionInfo connection)
        {
            playerId = peer.Tag?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(playerId) || !_connectedPlayers.TryGetValue(playerId, out var found) ||
                found.PeerId != peer.Id)
            {
                connection = null!;
                return false;
            }
            connection = found;
            return true;
        }

        private void HandlePlayerShot(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            float posX = json.Value<float?>("PosX") ?? 0f;
            float posY = json.Value<float?>("PosY") ?? 0f;
            float dirX = json.Value<float?>("DirX") ?? 0f;
            float dirY = json.Value<float?>("DirY") ?? 0f;
            string weaponType = json.Value<string>("WeaponType") ?? string.Empty;
            if (!float.IsFinite(posX) || !float.IsFinite(posY) || !float.IsFinite(dirX) || !float.IsFinite(dirY) ||
                Math.Abs(posX) > 100000f || Math.Abs(posY) > 100000f ||
                Math.Abs(dirX) > 1000f || Math.Abs(dirY) > 1000f ||
                Math.Sqrt(dirX * dirX + dirY * dirY) < 0.001 ||
                string.IsNullOrWhiteSpace(weaponType) || weaponType.Length > 64)
            {
                ConsoleWrite.WriteMessage($"[UDP] Invalid shot packet from {playerId}", ConsoleColor.Yellow);
                return;
            }

            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Id, connection.MatchRoomId, StringComparison.OrdinalIgnoreCase));
            if (room == null || !IsActiveLivingPlayer(room, playerId)) return;

            var lagCompensation = MatchServerV2.Instance.ServerLagCompensationManager;
            var shooterState = lagCompensation.GetPlayerState(playerId);
            if (string.IsNullOrEmpty(shooterState.PlayerId) ||
                !float.IsFinite(shooterState.PositionX) || !float.IsFinite(shooterState.PositionY))
                return;
            // Use the server's latest accepted transform as the shot origin. The client
            // still supplies its aim vector, but cannot move hitscan origin by packet.
            posX = shooterState.PositionX;
            posY = shooterState.PositionY;
            string weaponVariant = json.Value<string>("WeaponVariant") ?? weaponType;
            if (weaponVariant.Length > 64) return;
            if (!_weaponLoadouts.TryGetValue($"{connection.MatchRoomId}|{playerId}", out var loadout) ||
                !loadout.Contains(weaponVariant))
                return;
            // Special weapons use projectile and area-damage behavior in Unity.
            // This UDP path currently implements hitscan only; do not silently turn
            // their shots into generic 30-damage hitscan attacks.
            if (IsSpecialWeapon(weaponType) || IsSpecialWeapon(weaponVariant))
            {
                var ammoState = GetWeaponAmmo(playerId, connection.MatchRoomId, weaponType, weaponVariant);
                SendAmmoUpdate(peer, connection, playerId, weaponType, weaponVariant, ammoState);
                return;
            }
            if (!TryFireWeapon(playerId, connection.MatchRoomId, weaponType, weaponVariant,
                out var ammo, out var blockedByReload))
            {
                if (blockedByReload)
                    SendAmmoUpdate(peer, connection, playerId, weaponType, weaponVariant, ammo);
                return;
            }
            SendAmmoUpdate(peer, connection, playerId, weaponType, weaponVariant, ammo);

            float directionLength = MathF.Sqrt(dirX * dirX + dirY * dirY);
            float normalizedDirX = dirX / directionLength;
            float normalizedDirY = dirY / directionLength;
            string? targetPlayerId = null;
            float nearestAlongRay = GetWeaponRange(weaponType);
            const float playerHitRadius = 0.65f;
            if (!string.IsNullOrEmpty(shooterState.PlayerId))
            {
                foreach (var candidate in room.Players)
                {
                    if (candidate.Id == playerId || candidate.Health <= 0) continue;
                    var targetState = lagCompensation.GetPlayerState(candidate.Id);
                    if (string.IsNullOrEmpty(targetState.PlayerId) ||
                        !float.IsFinite(targetState.PositionX) || !float.IsFinite(targetState.PositionY))
                        continue;

                    float offsetX = targetState.PositionX - shooterState.PositionX;
                    float offsetY = targetState.PositionY - shooterState.PositionY;
                    float alongRay = offsetX * normalizedDirX + offsetY * normalizedDirY;
                    if (alongRay <= 0f || alongRay > nearestAlongRay) continue;
                    float perpendicularX = offsetX - alongRay * normalizedDirX;
                    float perpendicularY = offsetY - alongRay * normalizedDirY;
                    if (perpendicularX * perpendicularX + perpendicularY * perpendicularY >
                        playerHitRadius * playerHitRadius)
                        continue;

                    nearestAlongRay = alongRay;
                    targetPlayerId = candidate.Id;
                }
            }

            var shot = new JObject
            {
                ["MessageType"] = "PlayerShot",
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = connection.ClientRoomId,
                ["PosX"] = posX,
                ["PosY"] = posY,
                ["DirX"] = dirX,
                ["DirY"] = dirY,
                ["WeaponType"] = weaponType,
                ["ObjectId"] = Guid.NewGuid().ToString("N"),
                ["DamageType"] = "Bullet"
            };
            if (json.TryGetValue("WeaponVariant", out var variant) && variant.Type == Newtonsoft.Json.Linq.JTokenType.String)
                shot["WeaponVariant"] = variant;
            BroadcastJsonToRoom(connection.MatchRoomId, shot, DeliveryMethod.ReliableOrdered);

            if (targetPlayerId == null || !room.TryApplyPlayerDamage(
                    playerId, targetPlayerId,
                    GetAdjustedWeaponDamage(connection.MatchRoomId, playerId, targetPlayerId, weaponType),
                    out var damageApplied, out var remainingHealth, out var wasKilled) ||
                !room.TryGetPlayer(targetPlayerId, out var damagedPlayer) || damagedPlayer == null ||
                !room.TryGetPlayer(playerId, out var attacker) || attacker == null)
                return;

            var damageEvent = new JObject
            {
                ["MessageType"] = "PlayerDamaged",
                ["RoomID"] = connection.ClientRoomId,
                ["AttackerID"] = playerId,
                ["DamagedPlayerID"] = targetPlayerId,
                ["Damage"] = damageApplied,
                ["RemainingHealth"] = remainingHealth,
                ["MaxHealth"] = damagedPlayer.MaxHealth,
                ["IsDown"] = wasKilled
            };
            BroadcastJsonToRoom(connection.MatchRoomId, damageEvent, DeliveryMethod.ReliableOrdered);
            if (!wasKilled) return;

            bool isTeamMode = room.Setting.Mode == EGameMode.TeamDeathMatch ||
                room.Setting.Mode == EGameMode.TeamSurvival ||
                room.Setting.Mode == EGameMode.CaptureTheFlag;
            int redTeamKills = room.Players.Where(player => player.Team == ETeam.Red).Sum(player => player.Kills);
            int blueTeamKills = room.Players.Where(player => player.Team == ETeam.Blue).Sum(player => player.Kills);
            var killedEvent = new JObject
            {
                ["MessageType"] = "PlayerKilled",
                ["RoomID"] = connection.ClientRoomId,
                ["KillerID"] = playerId,
                ["KilledPlayerID"] = targetPlayerId,
                ["KillerKills"] = attacker.Kills,
                ["IsTeamMode"] = isTeamMode,
                ["RedTeamKills"] = redTeamKills,
                ["BlueTeamKills"] = blueTeamKills
            };
            BroadcastJsonToRoom(connection.MatchRoomId, killedEvent, DeliveryMethod.ReliableOrdered);
        }

        private static int GetWeaponDamage(string weaponType)
        {
            return weaponType.ToLowerInvariant() switch
            {
                "pistol" or "handgun" => 25,
                "rifle" or "assaultrifle" => 35,
                "sniper" or "sniperrifle" => 80,
                "shotgun" => 20,
                "smg" or "submachinegun" => 15,
                _ => 30
            };
        }

        private int GetAdjustedWeaponDamage(
            string roomId, string attackerId, string targetId, string weaponType)
        {
            return GetAdjustedDamage(roomId, attackerId, targetId, GetWeaponDamage(weaponType));
        }

        private static float GetWeaponRange(string weaponType)
        {
            return weaponType.ToLowerInvariant() switch
            {
                "sniper" or "sniperrifle" => 100f,
                "rifle" or "assaultrifle" => 45f,
                "smg" or "submachinegun" => 30f,
                "pistol" or "handgun" => 24f,
                "shotgun" => 12f,
                _ => 30f
            };
        }

        private bool TryConsumeShotCooldown(string playerId, string weaponType, string weaponVariant)
        {
            string variant = weaponVariant.Replace("_", string.Empty).Replace("-", string.Empty)
                .Replace(" ", string.Empty).ToLowerInvariant();
            double seconds = variant switch
            {
                "famas" => 0.08,
                "f2000" or "steyraug" => 0.09,
                "glock" or "deserteagle" or "fiveseven" => 0.25,
                "hk69a1" => 3.2,
                _ => weaponType.ToLowerInvariant() switch
                {
                    "pistol" or "handgun" => 0.25,
                    "shotgun" => 0.5,
                    "sniper" or "sniperrifle" => 0.8,
                    "grenadelauncher" or "rocketlauncher" => 1.0,
                    _ => 0.1
                }
            };
            string cooldownKey = $"{playerId}::{variant}";
            long now = DateTime.UtcNow.Ticks;
            long cooldownTicks = (long)(seconds * TimeSpan.TicksPerSecond);
            while (true)
            {
                if (_lastShotTicks.TryGetValue(cooldownKey, out var previous))
                {
                    if (now - previous < cooldownTicks) return false;
                    if (_lastShotTicks.TryUpdate(cooldownKey, now, previous)) return true;
                }
                else if (_lastShotTicks.TryAdd(cooldownKey, now))
                {
                    return true;
                }
            }
        }

        private void ClearPlayerShotCooldowns(string playerId)
        {
            string prefix = $"{playerId}::";
            foreach (var key in _lastShotTicks.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    _lastShotTicks.TryRemove(key, out _);
            }
        }

        private WeaponAmmoState GetWeaponAmmo(
            string playerId, string matchRoomId, string weaponType, string weaponVariant)
        {
            string normalizedVariant = new string(weaponVariant
                .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            string key = $"{matchRoomId}|{playerId}|{normalizedVariant}";
            return _weaponAmmo.GetOrAdd(key, _ =>
            {
                int capacity = GetMagazineCapacity(weaponType, normalizedVariant);
                int reserve = IsSpecialWeapon(weaponVariant) ? 0 : capacity * 3;
                return new WeaponAmmoState(weaponType, capacity, reserve);
            });
        }

        private static int GetMagazineCapacity(string weaponType, string normalizedVariant)
        {
            return normalizedVariant switch
            {
                "glock" or "uzi" => normalizedVariant == "uzi" ? 32 : 30,
                "deserteagle" => 7,
                "scout" or "psg1" or "dragunov" or "awp" => 10,
                "spas" or "spas12" => 8,
                "f2000" => 17,
                "hk69a1" => 1,
                "grenadelauncher" or "rocketlauncher" => 10,
                "flamethrower" => 100,
                _ => weaponType.ToLowerInvariant() switch
                {
                    "pistol" or "handgun" => 15,
                    "sniper" or "sniperrifle" => 10,
                    "shotgun" => 8,
                    "grenadelauncher" or "rocketlauncher" => 1,
                    _ => 30
                }
            };
        }

        private static float GetReloadDuration(string weaponVariant)
        {
            string variant = new string(weaponVariant
                .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            return variant switch
            {
                "glock" or "deserteagle" or "fiveseven" or "hk69a1" => 1.6f,
                "spas" or "spas12" => 2.8f,
                "f2000" => 2.1f,
                _ => 2f
            };
        }

        private bool TryFireWeapon(
            string playerId, string matchRoomId, string weaponType, string weaponVariant,
            out WeaponAmmoState ammo, out bool blockedByReload)
        {
            ammo = GetWeaponAmmo(playerId, matchRoomId, weaponType, weaponVariant);
            blockedByReload = false;
            lock (ammo.Sync)
            {
                if (ammo.ReloadUntilTicks > DateTime.UtcNow.Ticks || ammo.CurrentAmmo <= 0)
                {
                    blockedByReload = true;
                    return false;
                }
                if (!TryConsumeShotCooldown(playerId, weaponType, weaponVariant)) return false;
                ammo.CurrentAmmo--;
                return true;
            }
        }

        private static JObject BuildAmmoMessage(
            string messageType, PlayerConnectionInfo connection, string playerId,
            string weaponType, string weaponVariant, WeaponAmmoState ammo,
            bool? accepted = null, string? reason = null, float? reloadSeconds = null)
        {
            JObject message;
            lock (ammo.Sync)
            {
                message = new JObject
                {
                    ["MessageType"] = messageType,
                    ["RoomID"] = connection.ClientRoomId,
                    ["RoomId"] = connection.ClientRoomId,
                    ["PlayerID"] = playerId,
                    ["PlayerId"] = playerId,
                    ["WeaponType"] = weaponType,
                    ["WeaponVariant"] = weaponVariant,
                    ["CurrentAmmo"] = ammo.CurrentAmmo,
                    ["MaxAmmo"] = ammo.MaxAmmo,
                    ["ReserveAmmo"] = ammo.ReserveAmmo
                };
            }
            if (accepted.HasValue) message["Accepted"] = accepted.Value;
            if (!string.IsNullOrEmpty(reason)) message["Reason"] = reason;
            if (reloadSeconds.HasValue) message["ReloadSeconds"] = reloadSeconds.Value;
            return message;
        }

        private void SendAmmoUpdate(
            NetPeer peer, PlayerConnectionInfo connection, string playerId,
            string weaponType, string weaponVariant, WeaponAmmoState ammo)
        {
            SendJson(peer, BuildAmmoMessage("AmmoUpdate", connection, playerId,
                weaponType, weaponVariant, ammo),
                DeliveryMethod.ReliableOrdered);
        }

        private void HandleGrenadeThrow(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Id, connection.MatchRoomId, StringComparison.OrdinalIgnoreCase));
            if (room == null || !IsActiveLivingPlayer(room, playerId)) return;
            float posX = json.Value<float?>("PosX") ?? 0f;
            float posY = json.Value<float?>("PosY") ?? 0f;
            float dirX = json.Value<float?>("DirX") ?? 0f;
            float dirY = json.Value<float?>("DirY") ?? 0f;
            float power = json.Value<float?>("Power") ?? 0f;
            string grenadeType = json.Value<string>("GrenadeType") ?? string.Empty;
            if (!float.IsFinite(posX) || !float.IsFinite(posY) || !float.IsFinite(dirX) || !float.IsFinite(dirY) ||
                !float.IsFinite(power) || Math.Abs(posX) > 100000f || Math.Abs(posY) > 100000f ||
                Math.Abs(dirX) > 1000f || Math.Abs(dirY) > 1000f ||
                Math.Sqrt(dirX * dirX + dirY * dirY) < 0.001 || power < 0f || power > 1f || grenadeType.Length > 64)
            {
                ConsoleWrite.WriteMessage($"[UDP] Invalid grenade packet from {playerId}", ConsoleColor.Yellow);
                return;
            }

            var grenadeLoadout = _grenadeLoadouts.GetOrAdd(
                $"{connection.MatchRoomId}|{playerId}", _ => new PlayerGrenadeLoadout());
            if (!TryConsumeGrenadeCooldown(connection.MatchRoomId, playerId))
            {
                SendGrenadeLoadoutUpdate(peer, connection, playerId, grenadeLoadout);
                return;
            }
            if (!grenadeLoadout.TryConsume(grenadeType))
            {
                SendGrenadeLoadoutUpdate(peer, connection, playerId, grenadeLoadout);
                return;
            }
            var playerState = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (!float.IsFinite(playerState.PositionX) || !float.IsFinite(playerState.PositionY))
            {
                grenadeLoadout.Refill(grenadeType);
                SendGrenadeLoadoutUpdate(peer, connection, playerId, grenadeLoadout);
                return;
            }
            posX = playerState.PositionX;
            posY = playerState.PositionY;

            var grenade = new JObject
            {
                ["MessageType"] = "GrenadeThrow",
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = connection.ClientRoomId,
                ["PosX"] = posX,
                ["PosY"] = posY,
                ["DirX"] = dirX,
                ["DirY"] = dirY,
                ["Power"] = power,
                ["GrenadeType"] = grenadeType,
                ["ObjectId"] = Guid.NewGuid().ToString("N")
            };
            BroadcastJsonToRoom(connection.MatchRoomId, grenade, DeliveryMethod.ReliableOrdered);
            SendGrenadeLoadoutUpdate(peer, connection, playerId, grenadeLoadout);
            _ = ResolveGrenadeAfterFuseAsync(room, connection, playerId,
                grenade.Value<string>("ObjectId")!, posX, posY, dirX, dirY, power, grenadeType);
        }

        private async Task ResolveGrenadeAfterFuseAsync(
            MatchRoom room, PlayerConnectionInfo connection, string ownerId, string projectileId,
            float startX, float startY, float dirX, float dirY, float power, string grenadeType)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                var normalizedType = new string(grenadeType
                    .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                float speed = normalizedType == "childclustergrenade" ? 8f : 12f * Math.Clamp(power, 0.1f, 1f);
                float gravity = normalizedType == "childclustergrenade" ? 30f :
                    normalizedType is "smoke" or "smokegrenade" ? 14f : 18f;
                float impactX = startX;
                float impactY = startY;
                float velocityX = dirX * speed;
                float velocityY = dirY * speed;
                float remaining = 3f;
                while (remaining > 0f)
                {
                    float step = Math.Min(remaining, 0.1f);
                    velocityY -= gravity * step;
                    impactX += velocityX * step;
                    impactY += velocityY * step;
                    remaining -= step;
                }

                BroadcastJsonToRoom(connection.MatchRoomId, new JObject
                {
                    ["MessageType"] = "ObjectDestroyed",
                    ["RoomID"] = connection.ClientRoomId,
                    ["ObjectId"] = projectileId,
                    ["PosX"] = impactX,
                    ["PosY"] = impactY
                }, DeliveryMethod.ReliableOrdered);

                if (!room.TryGetPlayer(ownerId, out var owner) || owner == null) return;
                const float blastRadius = 2.5f;
                foreach (var target in room.Players.ToArray())
                {
                    if (target.Id == ownerId || target.Health <= 0 ||
                        (owner.Team != ETeam.NoTeam && target.Team == owner.Team))
                        continue;
                    var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(target.Id);
                    if (string.IsNullOrEmpty(state.PlayerId) ||
                        !float.IsFinite(state.PositionX) || !float.IsFinite(state.PositionY))
                        continue;
                    float distance = MathF.Sqrt(
                        MathF.Pow(state.PositionX - impactX, 2) + MathF.Pow(state.PositionY - impactY, 2));
                    if (distance > blastRadius) continue;
                    float falloff = 1f - 0.65f * Math.Clamp(distance / blastRadius, 0f, 1f);
                    int baseDamage = Math.Max(1, (int)MathF.Ceiling(120f * falloff));
                    if (!room.TryApplyPlayerDamage(ownerId, target.Id,
                        GetAdjustedDamage(connection.MatchRoomId, ownerId, target.Id, baseDamage),
                        true, out var damage, out var remainingHealth, out var wasKilled) ||
                        !room.TryGetPlayer(target.Id, out var damagedPlayer) || damagedPlayer == null)
                        continue;

                    BroadcastJsonToRoom(connection.MatchRoomId, new JObject
                    {
                        ["MessageType"] = "PlayerDamaged",
                        ["RoomID"] = connection.ClientRoomId,
                        ["AttackerID"] = ownerId,
                        ["DamagedPlayerID"] = target.Id,
                        ["Damage"] = damage,
                        ["RemainingHealth"] = remainingHealth,
                        ["MaxHealth"] = damagedPlayer.MaxHealth,
                        ["IsDown"] = wasKilled
                    }, DeliveryMethod.ReliableOrdered);
                    if (wasKilled && room.TryGetPlayer(ownerId, out var attacker) && attacker != null)
                    {
                        bool teamMode = room.Setting.Mode == EGameMode.TeamDeathMatch ||
                            room.Setting.Mode == EGameMode.TeamSurvival ||
                            room.Setting.Mode == EGameMode.CaptureTheFlag;
                        BroadcastJsonToRoom(connection.MatchRoomId, new JObject
                        {
                            ["MessageType"] = "PlayerKilled",
                            ["RoomID"] = connection.ClientRoomId,
                            ["KillerID"] = ownerId,
                            ["KilledPlayerID"] = target.Id,
                            ["KillerKills"] = attacker.Kills,
                            ["IsTeamMode"] = teamMode,
                            ["RedTeamKills"] = room.Players.Where(player => player.Team == ETeam.Red).Sum(player => player.Kills),
                            ["BlueTeamKills"] = room.Players.Where(player => player.Team == ETeam.Blue).Sum(player => player.Kills)
                        }, DeliveryMethod.ReliableOrdered);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[UDP] Grenade resolution failed: {ex.Message}", ConsoleColor.Red);
            }
        }

        private int GetAdjustedDamage(string roomId, string attackerId, string targetId, int baseDamage)
        {
            long now = DateTime.UtcNow.Ticks;
            double multiplier = 1.0;
            if (_attackBuffUntil.TryGetValue($"{roomId}|{attackerId}", out var attackUntil) && attackUntil > now)
                multiplier *= 2.0;
            if (_defenseBuffUntil.TryGetValue($"{roomId}|{targetId}", out var defenseUntil) && defenseUntil > now)
                multiplier *= 0.5;
            return Math.Max(1, (int)Math.Ceiling(baseDamage * multiplier));
        }

        private bool TryConsumeGrenadeCooldown(string roomId, string playerId)
        {
            string key = $"{roomId}|{playerId}";
            long now = DateTime.UtcNow.Ticks;
            long cooldownTicks = TimeSpan.TicksPerSecond / 2;
            while (true)
            {
                if (_lastGrenadeThrowTicks.TryGetValue(key, out var previous))
                {
                    if (now - previous < cooldownTicks) return false;
                    if (_lastGrenadeThrowTicks.TryUpdate(key, now, previous)) return true;
                }
                else if (_lastGrenadeThrowTicks.TryAdd(key, now)) return true;
            }
        }

        private void HandleGrenadeLoadout(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            if (json["GrenadeSlots"] is not JArray slots || slots.Count != 3) return;
            var requestedSlots = new string[3];
            for (int index = 0; index < requestedSlots.Length; index++)
            {
                if (slots[index]?.Type != Newtonsoft.Json.Linq.JTokenType.String) return;
                requestedSlots[index] = slots[index]!.Value<string>() ?? string.Empty;
                if (!PlayerGrenadeLoadout.IsSupported(requestedSlots[index])) return;
            }
            string loadoutKey = $"{connection.MatchRoomId}|{playerId}";
            if (!_configuredGrenadeLoadouts.TryAdd(loadoutKey, 0)) return;
            var loadout = _grenadeLoadouts.GetOrAdd(loadoutKey, _ => new PlayerGrenadeLoadout());
            if (loadout.Set(requestedSlots))
                SendGrenadeLoadoutUpdate(peer, connection, playerId, loadout);
            else
                _configuredGrenadeLoadouts.TryRemove(loadoutKey, out _);
        }

        private static bool IsActiveLivingPlayer(MatchRoom room, string playerId) =>
            room.Playing && !room.Finished && room.TryGetPlayer(playerId, out var player) &&
            player != null && player.Health > 0;

        private void SendGrenadeLoadoutUpdate(
            NetPeer peer, PlayerConnectionInfo connection, string playerId,
            PlayerGrenadeLoadout loadout)
        {
            SendJson(peer, new JObject
            {
                ["MessageType"] = "GrenadeLoadoutUpdate",
                ["RoomID"] = connection.ClientRoomId,
                ["RoomId"] = connection.ClientRoomId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["GrenadeSlots"] = new JArray(loadout.Slots())
            }, DeliveryMethod.ReliableOrdered);
        }

        private void HandlePlayerPose(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Id, connection.MatchRoomId, StringComparison.OrdinalIgnoreCase));
            if (room == null || !IsActiveLivingPlayer(room, playerId)) return;
            string pose = json.Value<string>("PoseState") ?? string.Empty;
            if (pose != "Stand" && pose != "Sit" && pose != "LieDown") return;

            var message = new JObject
            {
                ["MessageType"] = "PlayerPose",
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = connection.ClientRoomId,
                ["PoseState"] = pose
            };
            BroadcastJsonToRoom(connection.MatchRoomId, message, DeliveryMethod.ReliableOrdered);
        }

        private void HandlePlayerReload(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Id, connection.MatchRoomId, StringComparison.OrdinalIgnoreCase));
            if (room == null || !IsActiveLivingPlayer(room, playerId)) return;
            string weaponType = json.Value<string>("WeaponType") ?? string.Empty;
            string weaponVariant = json.Value<string>("WeaponVariant") ?? weaponType;
            if (string.IsNullOrWhiteSpace(weaponType) || weaponType.Length > 64 || weaponVariant.Length > 64)
                return;
            if (!_weaponLoadouts.TryGetValue($"{connection.MatchRoomId}|{playerId}", out var loadout) ||
                !loadout.Contains(weaponVariant))
                return;

            var ammo = GetWeaponAmmo(playerId, connection.MatchRoomId, weaponType, weaponVariant);
            bool accepted = false;
            string reason;
            float reloadSeconds = GetReloadDuration(weaponVariant);
            lock (ammo.Sync)
            {
                if (ammo.ReloadUntilTicks > DateTime.UtcNow.Ticks)
                    reason = "Reload already in progress";
                else if (ammo.CurrentAmmo >= ammo.MaxAmmo)
                    reason = "Magazine is full";
                else if (ammo.ReserveAmmo <= 0)
                    reason = "No reserve ammunition";
                else
                {
                    int transferred = Math.Min(ammo.MaxAmmo - ammo.CurrentAmmo, ammo.ReserveAmmo);
                    ammo.CurrentAmmo += transferred;
                    ammo.ReserveAmmo -= transferred;
                    ammo.ReloadUntilTicks = DateTime.UtcNow.Ticks +
                        (long)(reloadSeconds * TimeSpan.TicksPerSecond);
                    accepted = transferred > 0;
                    reason = accepted ? string.Empty : "No ammunition transferred";
                }
            }

            var response = BuildAmmoMessage(
                "PlayerReloaded", connection, playerId, weaponType, weaponVariant, ammo,
                accepted, reason, reloadSeconds);
            BroadcastJsonToRoom(connection.MatchRoomId, response, DeliveryMethod.ReliableOrdered);
        }

        private void HandleWeaponDrop(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Id, connection.MatchRoomId, StringComparison.OrdinalIgnoreCase));
            if (room == null || !IsActiveLivingPlayer(room, playerId)) return;
            string requestedRoomId = json.Value<string>("RoomID") ?? json.Value<string>("RoomId") ?? string.Empty;
            if (!string.IsNullOrEmpty(requestedRoomId) &&
                !string.Equals(requestedRoomId, connection.ClientRoomId, StringComparison.OrdinalIgnoreCase))
                return;
            string weaponType = json.Value<string>("WeaponType") ?? string.Empty;
            int slotIndex = json.Value<int?>("SlotIndex") ?? -1;
            if (string.IsNullOrWhiteSpace(weaponType) || weaponType.Length > 64 || slotIndex is < 0 or > 1)
                return;
            if (!_weaponLoadouts.TryGetValue($"{connection.MatchRoomId}|{playerId}", out var loadout) ||
                !loadout.TryRemove(slotIndex, weaponType))
                return;

            var ammo = GetWeaponAmmo(playerId, connection.MatchRoomId,
                WeaponFamily(weaponType), weaponType);
            int magazineAmmo;
            lock (ammo.Sync) magazineAmmo = ammo.CurrentAmmo;
            var playerState = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (!float.IsFinite(playerState.PositionX) || !float.IsFinite(playerState.PositionY))
            {
                loadout.Set(slotIndex, weaponType);
                return;
            }

            if (!MatchRoomManager.Instance.TryDropWeapon(connection.MatchRoomId, playerId,
                weaponType, magazineAmmo, slotIndex, playerState.PositionX, playerState.PositionY, out var itemId))
            {
                loadout.Set(slotIndex, weaponType);
                return;
            }

            var dropped = new JObject
            {
                ["MessageType"] = "FieldItemSpawn",
                ["RoomID"] = connection.ClientRoomId,
                ["RoomId"] = connection.ClientRoomId,
                ["ItemId"] = itemId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["SlotIndex"] = slotIndex,
                ["ItemType"] = "WeaponItem",
                ["WeaponType"] = weaponType,
                ["IsPlayerDroppedWeapon"] = true,
                ["MagazineAmmo"] = magazineAmmo,
                ["PositionX"] = playerState.PositionX,
                ["PositionY"] = playerState.PositionY,
                ["PositionZ"] = playerState.PositionZ
            };
            BroadcastJsonToRoom(connection.MatchRoomId, dropped, DeliveryMethod.ReliableOrdered);
        }

        private void HandleFieldItemPickup(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            var matchRoom = MatchRoomManager.Instance.AllRooms().OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(candidate.Id, connection.MatchRoomId,
                    StringComparison.OrdinalIgnoreCase));
            if (matchRoom == null || !IsActiveLivingPlayer(matchRoom, playerId)) return;
            string requestedRoomId = json.Value<string>("RoomID") ?? json.Value<string>("RoomId") ?? string.Empty;
            if (!string.IsNullOrEmpty(requestedRoomId) &&
                !string.Equals(requestedRoomId, connection.ClientRoomId, StringComparison.OrdinalIgnoreCase))
                return;
            string itemId = json.Value<string>("ItemId") ?? json.Value<string>("ItemID") ?? string.Empty;
            int slotIndex = json.Value<int?>("SlotIndex") ?? -1;
            if (string.IsNullOrWhiteSpace(itemId) || itemId.Length > 128 || slotIndex is < 0 or > 2)
                return;

            var manager = MatchRoomManager.Instance;
            bool success = manager.TryGetFieldItem(connection.MatchRoomId, itemId, out var item) &&
                item.IsActive && item.State == "Spawned";
            string weaponType = success
                ? (item.IsPlayerDroppedWeapon ? item.WeaponType : ResolveSpecialWeapon(item.ItemType))
                : string.Empty;
            if (success && IsHealItem(item.ItemType))
            {
                if (!matchRoom.TryGetPlayer(playerId, out var player) ||
                    player == null || player.Health <= 0 || player.Health >= player.MaxHealth)
                    success = false;
            }
            var loadout = _weaponLoadouts.GetOrAdd(
                $"{connection.MatchRoomId}|{playerId}", _ => new PlayerWeaponLoadout());
            bool reservesWeaponSlot = success && !string.IsNullOrEmpty(weaponType);
            if (reservesWeaponSlot)
            {
                int expectedSlot = IsSpecialWeapon(weaponType) ? 2 : slotIndex;
                success = expectedSlot == slotIndex && loadout.TrySetEmpty(slotIndex, weaponType);
            }

            if (success)
            {
                var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
                success = manager.TryPickupFieldItem(connection.MatchRoomId, itemId, playerId,
                    state.PositionX, state.PositionY, out item);
            }
            if (!success && reservesWeaponSlot)
                loadout.Remove(slotIndex, weaponType);

            WeaponAmmoState? ammo = null;
            if (success && reservesWeaponSlot)
            {
                ammo = GetWeaponAmmo(playerId, connection.MatchRoomId,
                    WeaponFamily(weaponType), weaponType);
                if (item.IsPlayerDroppedWeapon)
                {
                    lock (ammo.Sync)
                        ammo.CurrentAmmo = Math.Clamp(item.MagazineAmmo, 0, ammo.MaxAmmo);
                }
            }

            PlayerGrenadeLoadout? grenadeLoadout = null;
            if (success && (NormalizeIdentifier(item.ItemType) is
                "grenadepack" or "normalgrenadepack" or "normalgrenadepackitem"))
            {
                grenadeLoadout = _grenadeLoadouts.GetOrAdd(
                    $"{connection.MatchRoomId}|{playerId}", _ => new PlayerGrenadeLoadout());
                grenadeLoadout.RefillNormal();
            }

            var response = new JObject
            {
                ["MessageType"] = "FieldItemPickup",
                ["RoomID"] = connection.ClientRoomId,
                ["RoomId"] = connection.ClientRoomId,
                ["ItemId"] = itemId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["Success"] = success
            };
            if (success)
            {
                response["ItemType"] = item.ItemType;
                response["SlotIndex"] = slotIndex;
                var normalizedItemType = NormalizeIdentifier(item.ItemType);
                if (IsTimedBuffItem(normalizedItemType))
                {
                    const float duration = 30f;
                    long expiresAt = DateTime.UtcNow.Ticks + (long)(duration * TimeSpan.TicksPerSecond);
                    string buffKey = $"{connection.MatchRoomId}|{playerId}";
                    if (normalizedItemType is "powerup" or "powerupitem") _attackBuffUntil[buffKey] = expiresAt;
                    else if (normalizedItemType is "defenceup" or "defenceupitem" or "defenseup" or "defenseupitem")
                        _defenseBuffUntil[buffKey] = expiresAt;
                    response["IsTimedBuff"] = true;
                    response["Duration"] = duration;
                }
                if (IsHealItem(item.ItemType))
                {
                    if (matchRoom.TryHealPlayer(playerId, 25,
                        out var restored, out var health, out var maxHealth))
                    {
                        response["RestoredHealth"] = restored;
                        response["CurrentHealth"] = health;
                        response["MaxHealth"] = maxHealth;
                    }
                }
                if (!string.IsNullOrEmpty(weaponType))
                {
                    response["WeaponType"] = weaponType;
                    response["MagazineAmmo"] = ammo?.CurrentAmmo ?? item.MagazineAmmo;
                }
                BroadcastJsonToRoom(connection.MatchRoomId, response, DeliveryMethod.ReliableOrdered);
                if (ammo != null)
                    SendAmmoUpdate(peer, connection, playerId, WeaponFamily(weaponType!), weaponType!, ammo);
                if (grenadeLoadout != null)
                    SendGrenadeLoadoutUpdate(peer, connection, playerId, grenadeLoadout);
            }
            else
            {
                response["Error"] = "Item is unavailable, too far away, or the requested weapon slot is occupied";
                SendJson(peer, response, DeliveryMethod.ReliableOrdered);
            }
        }

        private static string WeaponFamily(string weaponType)
        {
            string normalized = new string(weaponType.Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
            return normalized switch
            {
                "grenadelauncher" or "rocketlauncher" or "flamethrower" => weaponType,
                "glock" or "deserteagle" or "fiveseven" or "pistol" => "Pistol",
                "scout" or "psg1" or "dragunov" or "awp" => "Sniper",
                "spas" or "spas12" => "Shotgun",
                "uzi" => "SMG",
                _ => "Rifle"
            };
        }

        private static string ResolveSpecialWeapon(string itemType)
        {
            string normalized = new string(itemType.Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
            return normalized switch
            {
                "grenadelauncher" or "grenadelauncheritem" or "grenadelauncherfielditem" => "GrenadeLauncher",
                "rocketlauncher" or "rocketlauncheritem" => "RocketLauncher",
                "flamethrower" or "flamethroweritem" => "FlameThrower",
                _ => string.Empty
            };
        }

        private static bool IsSpecialWeapon(string weaponType) => NormalizeIdentifier(weaponType) is
            "grenadelauncher" or "rocketlauncher" or "flamethrower";

        private static string NormalizeIdentifier(string value) => new(value
            .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        private static bool IsHealItem(string itemType) =>
            NormalizeIdentifier(itemType) is "heal" or "healitem";

        private static bool IsTimedBuffItem(string itemType) => itemType is
            "powerup" or "powerupitem" or "defenceup" or "defenceupitem" or
            "defenseup" or "defenseupitem" or "speedup" or "speedupitem" or
            "stealth" or "stealthitem";

        private void HandlePlayerRespawn(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            string requestedRoomId = json.Value<string>("RoomID") ?? json.Value<string>("RoomId") ?? string.Empty;
            if (!string.Equals(requestedRoomId, connection.ClientRoomId, StringComparison.OrdinalIgnoreCase))
                return;

            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Id, connection.MatchRoomId, StringComparison.OrdinalIgnoreCase));
            if (room == null || !room.TryRespawnPlayer(playerId, out var currentHealth, out var maxHealth))
                return;

            var spawnX = connection.InitialSpawnSet ? connection.InitialSpawnX : 0f;
            var spawnY = connection.InitialSpawnSet ? connection.InitialSpawnY : 0f;
            var spawnZ = connection.InitialSpawnSet ? connection.InitialSpawnZ : 0f;
            MatchServerV2.Instance.ServerLagCompensationManager.SetPlayerPosition(playerId, spawnX, spawnY, spawnZ);
            connection.PositionSynchronized = true;
            ClearPlayerShotCooldowns(playerId);

            var response = new JObject
            {
                ["MessageType"] = "PlayerRespawn",
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = connection.ClientRoomId,
                ["RoomId"] = connection.ClientRoomId,
                ["PosX"] = spawnX,
                ["PosY"] = spawnY,
                ["PosZ"] = spawnZ,
                ["CurrentHealth"] = currentHealth,
                ["MaxHealth"] = maxHealth
            };
            BroadcastJsonToRoom(connection.MatchRoomId, response, DeliveryMethod.ReliableOrdered);
        }

        // --- Outbound: JSON → UTF-8 bytes → NetDataWriter ---

        private void SendJson(NetPeer peer, JObject json, DeliveryMethod deliveryMethod)
        {
            if (peer == null || peer.ConnectionState != ConnectionState.Connected) return;
            var raw = Utf8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            var writer = new NetDataWriter();
            writer.Put(raw);
            peer.Send(writer, deliveryMethod);
        }

        private void SendTransformStateToClient(NetPeer peer, ServerTransformState state)
        {
            if (peer == null || peer.ConnectionState != ConnectionState.Connected) return;

            var json = new JObject
            {
                ["MessageType"] = "ServerTransformState",
                ["NetworkId"] = state.NetworkId,
                ["PlayerId"] = state.PlayerId,
                ["PositionX"] = state.PositionX,
                ["PositionY"] = state.PositionY,
                ["PositionZ"] = state.PositionZ,
                ["RotationX"] = state.RotationX,
                ["RotationY"] = state.RotationY,
                ["RotationZ"] = state.RotationZ,
                ["RotationW"] = state.RotationW,
                ["VelocityX"] = state.VelocityX,
                ["VelocityY"] = state.VelocityY,
                ["VelocityZ"] = state.VelocityZ,
                ["Timestamp"] = state.Timestamp,
                ["SequenceNumber"] = state.SequenceNumber
            };
            SendJson(peer, json, DeliveryMethod.Unreliable);
        }

        // --- Inbound: UTF-8 bytes → JSON string → JObject ---

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                var rawBytes = reader.GetRemainingBytes();
                if (rawBytes == null || rawBytes.Length == 0) return;

                var jsonString = Utf8.GetString(rawBytes);
                var json = JObject.Parse(jsonString);
                var messageType = json.Value<string>("MessageType") ?? string.Empty;
                if (string.IsNullOrEmpty(peer.Tag?.ToString()) && messageType != "ClientConnect")
                {
                    ConsoleWrite.WriteMessage("[UDP] Unauthenticated peer sent a game packet; disconnecting", ConsoleColor.Yellow);
                    peer.Disconnect();
                    return;
                }

                switch (messageType)
                {
                    case "PlayerMove":
                        HandlePlayerMove(peer, json);
                        break;
                    case "PlayerShoot":
                    case "ShootRequest":
                        HandlePlayerShoot(peer, json);
                        break;
                    case "PlayerShot":
                        HandlePlayerShot(peer, json);
                        break;
                    case "GrenadeThrow":
                        HandleGrenadeThrow(peer, json);
                        break;
                    case "GrenadeLoadout":
                        HandleGrenadeLoadout(peer, json);
                        break;
                    case "PlayerPose":
                        HandlePlayerPose(peer, json);
                        break;
                    case "PlayerReload":
                        HandlePlayerReload(peer, json);
                        break;
                    case "WeaponDrop":
                        HandleWeaponDrop(peer, json);
                        break;
                    case "FieldItemPickup":
                        HandleFieldItemPickup(peer, json);
                        break;
                    case "PlayerRespawn":
                        HandlePlayerRespawn(peer, json);
                        break;
                    case "PlayerAction":
                        HandlePlayerAction(peer, json);
                        break;
                    case "Ping":
                    case "PingRequest":
                        HandlePing(peer, json);
                        break;
                    case "ClientConnect":
                        HandleClientConnect(peer, json);
                        break;
                    default:
                        ConsoleWrite.WriteMessage($"[UDP] Unknown message type: {messageType}", ConsoleColor.Yellow);
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[UDP] Error processing message: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                reader.Recycle();
            }
        }

        private void HandlePlayerMove(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection)) return;
            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Id, connection.MatchRoomId, StringComparison.OrdinalIgnoreCase));
            if (room == null || !IsActiveLivingPlayer(room, playerId)) return;

            if (!connection.PositionSynchronized)
            {
                var posX = json.Value<float?>("PosX") ?? float.NaN;
                var posY = json.Value<float?>("PosY") ?? float.NaN;
                var posZ = json.Value<float?>("PosZ") ?? 0f;
                if (!float.IsFinite(posX) || !float.IsFinite(posY) || !float.IsFinite(posZ) ||
                    Math.Abs(posX) > 100000f || Math.Abs(posY) > 100000f || Math.Abs(posZ) > 100000f)
                {
                    ConsoleWrite.WriteMessage($"[UDP] Invalid initial position from {playerId}", ConsoleColor.Yellow);
                    return;
                }
                MatchServerV2.Instance.ServerLagCompensationManager.SetPlayerPosition(playerId, posX, posY, posZ);
                connection.InitialSpawnX = posX;
                connection.InitialSpawnY = posY;
                connection.InitialSpawnZ = posZ;
                connection.InitialSpawnSet = true;
            }

            var inputData = new ClientInputData
            {
                PlayerId = playerId,
                MoveX = json.Value<float?>("MoveX") ?? json.Value<float?>("PosX") ?? 0f,
                MoveY = json.Value<float?>("MoveY") ?? json.Value<float?>("PosY") ?? 0f,
                MoveZ = 0f,
                LookX = json.Value<float?>("LookX") ?? json.Value<float?>("DirX") ?? 0f,
                LookY = json.Value<float?>("LookY") ?? json.Value<float?>("DirY") ?? 0f,
                Jump = json.Value<bool?>("Jump") ?? false,
                Fire = json.Value<bool?>("Fire") ?? false,
                SequenceNumber = (byte)(json.Value<int?>("SequenceNumber") ?? 0),
                Timestamp = json.Value<float?>("Timestamp") ?? 0f,
                DeltaTime = json.Value<float?>("DeltaTime") ?? 0f
            };

            if (!MatchServerV2.Instance.ServerLagCompensationManager.ProcessClientInput(inputData, out var rejectionReason))
            {
                ConsoleWrite.WriteMessage($"[UDP] Rejected move input from {playerId}: {rejectionReason}", ConsoleColor.Yellow);
            }
            else
            {
                connection.PositionSynchronized = true;
            }
        }

        private void HandlePlayerShoot(NetPeer peer, JObject json)
        {
            if (!TryGetAuthenticatedConnection(peer, out var playerId, out var connection) ||
                !connection.PositionSynchronized)
                return;

            var room = MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Id, connection.MatchRoomId, StringComparison.OrdinalIgnoreCase));
            if (room == null || !IsActiveLivingPlayer(room, playerId)) return;

            float lookX = json.Value<float?>("DirX") ?? json.Value<float?>("Angle") ?? 0f;
            float lookY = json.Value<float?>("DirY") ?? 0f;
            if (!float.IsFinite(lookX) || !float.IsFinite(lookY) ||
                Math.Abs(lookX) > 1000f || Math.Abs(lookY) > 1000f)
                return;

            var inputData = new ClientInputData
            {
                PlayerId = playerId,
                MoveX = 0f,
                MoveY = 0f,
                MoveZ = 0f,
                LookX = lookX,
                LookY = lookY,
                Jump = false,
                Fire = true,
                SequenceNumber = (byte)(json.Value<int?>("SequenceNumber") ?? 0),
                Timestamp = json.Value<float?>("Timestamp") ?? 0f,
                DeltaTime = json.Value<float?>("DeltaTime") ?? 0f
            };

            if (!MatchServerV2.Instance.ServerLagCompensationManager.ProcessClientInput(inputData, out var rejectionReason))
            {
                ConsoleWrite.WriteMessage($"[UDP] Rejected shoot input from {playerId}: {rejectionReason}", ConsoleColor.Yellow);
            }
        }

        private void HandlePlayerAction(NetPeer peer, JObject json)
        {
            var actionType = json.Value<string>("ActionType") ?? "Unknown";
            ConsoleWrite.WriteMessage($"[UDP] Player {peer.Tag} action: {actionType}", ConsoleColor.Gray);
        }

        private void HandlePing(NetPeer peer, JObject json)
        {
            var clientTimestamp = json.Value<long?>("ClientTimestamp") ?? 0;

            var pong = new JObject
            {
                ["MessageType"] = "PingResponse",
                ["ClientTimestamp"] = clientTimestamp,
                ["ServerTimestamp"] = DateTime.UtcNow.Ticks
            };
            SendJson(peer, pong, DeliveryMethod.Unreliable);

            string playerId = peer.Tag?.ToString() ?? "Unknown";
            var rtt = (DateTime.UtcNow.Ticks - clientTimestamp) / TimeSpan.TicksPerMillisecond;
            ConsoleWrite.WriteMessage($"[UDP] Ping from {playerId}: {rtt}ms", ConsoleColor.Gray);
        }

        private void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            ConsoleWrite.WriteMessage($"[UDP] Network error from {endPoint}: {socketError}", ConsoleColor.Red);
        }

        // --- Broadcast ---

        public void BroadcastToRoom(int senderId, string messageType, Action<NetDataWriter> writeData)
        {
            var matchRoom = GetMatchRoomForPlayer(senderId);
            if (matchRoom == null) return;

            var writer = new NetDataWriter();
            writer.Put(messageType);
            writeData(writer);

            foreach (var player in matchRoom.Players)
            {
                if (_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                {
                    var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                    peerById?.Send(writer, DeliveryMethod.ReliableOrdered);
                }
            }
        }

        public void BroadcastJsonToRoom(string roomId, JObject json, DeliveryMethod method = DeliveryMethod.Unreliable)
        {
            var raw = Utf8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            var writer = new NetDataWriter();
            writer.Put(raw);

            var matchRoomManager = MatchRoomManager.Instance;
            var allRooms = matchRoomManager.AllRooms();

            foreach (var abstractRoom in allRooms)
            {
                if (abstractRoom is MatchRoom room && room.Id.ToString() == roomId)
                {
                    foreach (var player in room.Players)
                    {
                        if (_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                        {
                            var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                            peerById?.Send(writer, method);
                        }
                    }
                    break;
                }
            }
        }

        private MatchRoom? GetMatchRoomForPlayer(int peerId)
        {
            var playerId = _connectedPlayers.FirstOrDefault(x => x.Value.PeerId == peerId).Key;
            if (string.IsNullOrEmpty(playerId)) return null;

            var matchRoomManager = MatchRoomManager.Instance;
            var allRooms = matchRoomManager.AllRooms();

            return allRooms
                .OfType<MatchRoom>()
                .FirstOrDefault(room => room.Players.Any(p => p.Id == playerId));
        }

        private void NotifyPlayerJoined(string playerId) { }
        private void NotifyPlayerLeft(string playerId) { }

        public void PollingEvent()
        {
            _server?.PollEvents();
        }

        // --- Snapshot broadcast (20Hz default) ---

        private System.Timers.Timer? _snapshotTimer;

        public void StartSnapshotBroadcast(int intervalMs = 50)
        {
            if (_snapshotTimer != null) return;
            _snapshotTimer = new System.Timers.Timer(intervalMs);
            _snapshotTimer.Elapsed += (s, e) => BroadcastSnapshots();
            _snapshotTimer.Start();
        }

        public void StopSnapshotBroadcast()
        {
            _snapshotTimer?.Stop();
            _snapshotTimer?.Dispose();
            _snapshotTimer = null;
        }

        private void BroadcastSnapshots()
        {
            try
            {
                var matchRoomManager = MatchRoomManager.Instance;
                var rooms = matchRoomManager.AllRooms();
                var lagCompManager = MatchServerV2.Instance.ServerLagCompensationManager;

                foreach (var abstractRoom in rooms)
                {
                    if (abstractRoom is not MatchRoom room) continue;

                    foreach (var player in room.Players)
                    {
                        var playerState = lagCompManager.GetPlayerState(player.Id);
                        if (playerState.PlayerId == null) continue;
                        if (!_connectedPlayers.TryGetValue(player.Id, out var connectionInfo)) continue;

                        var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                        if (peerById == null) continue;

                        var json = new JObject
                        {
                            ["MessageType"] = "ServerTransformState",
                            ["NetworkId"] = playerState.NetworkId,
                            ["PlayerId"] = playerState.PlayerId,
                            ["PositionX"] = playerState.PositionX,
                            ["PositionY"] = playerState.PositionY,
                            ["PositionZ"] = playerState.PositionZ,
                            ["RotationX"] = playerState.RotationX,
                            ["RotationY"] = playerState.RotationY,
                            ["RotationZ"] = playerState.RotationZ,
                            ["RotationW"] = playerState.RotationW,
                            ["VelocityX"] = playerState.VelocityX,
                            ["VelocityY"] = playerState.VelocityY,
                            ["VelocityZ"] = playerState.VelocityZ,
                            ["Timestamp"] = playerState.Timestamp,
                            ["SequenceNumber"] = playerState.SequenceNumber
                        };
                        var raw = Utf8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
                        var writer = new NetDataWriter();
                        writer.Put(raw);
                        peerById.Send(writer, DeliveryMethod.Unreliable);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[UDP] Error broadcasting snapshots: {ex.Message}", ConsoleColor.Red);
            }
        }

        public void Shutdown()
        {
            if (_server == null || !_server.IsRunning) return;

            ConsoleWrite.WriteMessage("[UDP] Shutting down server...", ConsoleColor.Yellow);

            _listener.ConnectionRequestEvent -= OnConnectionRequest;
            _listener.PeerConnectedEvent -= OnPeerConnected;
            _listener.PeerDisconnectedEvent -= OnPeerDisconnected;
            _listener.NetworkReceiveEvent -= OnNetworkReceive;
            _listener.NetworkErrorEvent -= OnNetworkError;

            _server.Stop();
            _connectedPlayers.Clear();
            _lastShotTicks.Clear();
            _weaponAmmo.Clear();
            _weaponLoadouts.Clear();
            _grenadeLoadouts.Clear();
            _configuredGrenadeLoadouts.Clear();
            _lastGrenadeThrowTicks.Clear();
            _attackBuffUntil.Clear();
            _defenseBuffUntil.Clear();

            ConsoleWrite.WriteMessage("[UDP] Server shutdown complete", ConsoleColor.Green);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Shutdown();
            _disposed = true;
        }
    }

    internal sealed class UdpTokenEntry
    {
        public UdpTokenEntry(string token, DateTime expiresAtUtc)
        {
            Token = token;
            ExpiresAtUtc = expiresAtUtc;
        }

        public string Token { get; }
        public DateTime ExpiresAtUtc { get; }
    }

    internal sealed class PlayerConnectionInfo
    {
        public required int PeerId { get; init; }
        public required string EndPoint { get; init; }
        public required DateTime ConnectedAt { get; init; }
        public required string PlayerId { get; init; }
        public required string MatchRoomId { get; init; }
        public required string ClientRoomId { get; init; }
        public volatile bool PositionSynchronized;
        public bool InitialSpawnSet;
        public float InitialSpawnX;
        public float InitialSpawnY;
        public float InitialSpawnZ;
    }

    internal sealed class WeaponAmmoState
    {
        public WeaponAmmoState(string weaponType, int maxAmmo, int reserveAmmo)
        {
            WeaponType = weaponType;
            MaxAmmo = maxAmmo;
            CurrentAmmo = maxAmmo;
            ReserveAmmo = reserveAmmo;
        }

        public object Sync { get; } = new();
        public string WeaponType { get; }
        public int MaxAmmo { get; }
        public int CurrentAmmo { get; set; }
        public int ReserveAmmo { get; set; }
        public long ReloadUntilTicks { get; set; }
    }

    internal sealed class PlayerWeaponLoadout
    {
        private readonly object _sync = new();
        private readonly string[] _slots = { "Pistol", "", "" };

        public bool Contains(string weaponType)
        {
            string normalized = Normalize(weaponType);
            lock (_sync) return normalized.Length > 0 &&
                _slots.Any(slot => Normalize(slot) == normalized);
        }

        public bool TrySetEmpty(int slot, string weaponType)
        {
            if (slot is < 0 or > 2 || string.IsNullOrWhiteSpace(weaponType)) return false;
            lock (_sync)
            {
                if (!string.IsNullOrEmpty(_slots[slot])) return false;
                _slots[slot] = weaponType;
                return true;
            }
        }

        public void Set(int slot, string weaponType)
        {
            if (slot is < 0 or > 2) return;
            lock (_sync) _slots[slot] = weaponType;
        }

        public bool TryRemove(int slot, string weaponType)
        {
            if (slot is < 0 or > 1) return false;
            lock (_sync)
            {
                if (Normalize(_slots[slot]) != Normalize(weaponType)) return false;
                _slots[slot] = string.Empty;
                return true;
            }
        }

        public void Remove(int slot, string weaponType)
        {
            if (slot is < 0 or > 2) return;
            lock (_sync)
            {
                if (Normalize(_slots[slot]) == Normalize(weaponType))
                    _slots[slot] = string.Empty;
            }
        }

        private static string Normalize(string value) => new(value
            .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    internal sealed class PlayerGrenadeLoadout
    {
        private readonly object _sync = new();
        private string[] _slots = { "Normal", "Normal", "Normal" };

        public static bool IsSupported(string value) => Normalize(value) is
            "empty" or "normal" or "power" or "cluster" or "magnetic" or "magnet" or
            "mine" or "fire" or "smoke";

        public bool Set(string[] slots)
        {
            if (slots.Length != 3 || slots.Any(slot => !IsSupported(slot))) return false;
            lock (_sync)
            {
                _slots = slots.Select(slot => Canonical(slot)).ToArray();
                return true;
            }
        }

        public bool TryConsume(string grenadeType)
        {
            string requested = Normalize(grenadeType);
            if (requested.Length == 0 || requested == "empty") return false;
            lock (_sync)
            {
                int slot = Array.FindIndex(_slots, value => Normalize(value) == requested ||
                    (requested == "magnet" && Normalize(value) == "magnetic"));
                if (slot < 0) return false;
                _slots[slot] = "Empty";
                return true;
            }
        }

        public void Refill(string grenadeType)
        {
            string canonical = Canonical(grenadeType);
            if (canonical == "Empty") return;
            lock (_sync)
            {
                int slot = Array.FindIndex(_slots, value => Normalize(value) == "empty");
                if (slot >= 0) _slots[slot] = canonical;
            }
        }

        public void RefillNormal()
        {
            lock (_sync)
            {
                for (int index = 0; index < _slots.Length; index++)
                    if (Normalize(_slots[index]) == "empty") _slots[index] = "Normal";
            }
        }

        public string[] Slots()
        {
            lock (_sync) return (string[])_slots.Clone();
        }

        private static string Canonical(string value) => Normalize(value) switch
        {
            "empty" => "Empty",
            "normal" => "Normal",
            "power" => "Power",
            "cluster" => "Cluster",
            "magnetic" or "magnet" => "Magnetic",
            "mine" => "Mine",
            "fire" => "Fire",
            "smoke" => "Smoke",
            _ => "Empty"
        };

        private static string Normalize(string value) => new(value
            .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
