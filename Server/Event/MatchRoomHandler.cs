using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.ComponentModel.Design;
using System.Globalization;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;


#nullable enable

namespace OpenGSServer
{

    public enum GameEventType
    {
        LoadingFinished,
        PlayerShot,
        PlayerKilled,
        PlayerDamaged,
        GrenadeThrow,
        ItemUsed,
        FlagCaptured,
        FlagLost,
        FlagPickup,
        FlagReturn,
        FlagScoreUpdate,
        PlayerEliminated,
        MatchStatusRequest,
        PlayerPositionUpdate,
        PlayerRespawn,
        PlayerPose
    }

    public class IInGameMatchRoomHandler
    {

    }
    internal class InGameMatchEventHandler:IInGameMatchRoomHandler
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastFlagEvents = new();
        private static readonly ConcurrentDictionary<string, byte> FlagCarriers = new();
        private static readonly ConcurrentDictionary<string, DateTime> LastShots = new();

        public InGameMatchEventHandler() { }

        /// <summary>
        /// TCPベースのシステムイベント処理
        /// </summary>
        public static void ParseTcpEvent(JObject json)
        {
            var type = json.GetStringOrNull("MessageType");

            if (type != null)
            {
                MatchRoomManager manager = MatchRoomManager.Instance;

                var playerId = ReadString(json, "PlayerID", "PlayerId");
                var roomId = ReadString(json, "RoomID", "RoomId");

                if (playerId != null && roomId != null)
                {
                    var room = manager.SearchRoomByMemberID(playerId);

                    if (room != null && IsRoomMatch(room, roomId))
                    {
                        ProcessSystemEvent(room, type, json, playerId);
                    }
                    else if (room != null)
                    {
                        Console.WriteLine($"[Match] Rejected TCP event for mismatched room '{roomId}' from '{playerId}'");
                    }
                }
            }
        }

        /// <summary>
        /// UDPベースのリアルタイムゲームイベント処理
        /// </summary>
        public static void ParseUdpEvent(byte[] udpData, string remoteEndPoint)
        {
            try
            {
                // UDPデータをJSONに変換（実際の実装では適切なデシリアライズ）
                var jsonString = System.Text.Encoding.UTF8.GetString(udpData);
                var json = JObject.Parse(jsonString);

                var type = json.GetStringOrNull("MessageType");
                var playerId = ReadString(json, "PlayerID", "PlayerId");
                var roomId = ReadString(json, "RoomID", "RoomId");

                if (type != null && playerId != null && roomId != null)
                {
                    MatchRoomManager manager = MatchRoomManager.Instance;
                    var room = manager.SearchRoomByMemberID(playerId);

                    if (room != null && IsRoomMatch(room, roomId))
                    {
                        ProcessRealtimeGameEvent(room, type, json, playerId, remoteEndPoint);
                    }
                    else if (room != null)
                    {
                        Console.WriteLine($"[Match] Rejected UDP event for mismatched room '{roomId}' from '{playerId}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UDP event parsing error: {ex.Message}");
            }
        }

        private static void ProcessSystemEvent(MatchRoom room, string eventType, JObject json, string playerId)
        {
            switch (eventType)
            {
                case GameMessageTypes.LoadingStarted:
                    Console.WriteLine($"[Match] Player {playerId} started loading");
                    break;

                case GameMessageTypes.LoadingProgress:
                    var progress = json.GetValue("Progress")?.ToString() ?? json.GetValue("LoadingProgress")?.ToString() ?? "0";
                    Console.WriteLine($"[Match] Player {playerId} loading progress: {progress}");
                    break;

                case GameMessageTypes.LoadingCompleted:
                case GameMessageTypes.LoadingFinished:
                    room.SetPlayerReady(playerId);
                    break;

                case GameMessageTypes.MatchStatusRequest:
                    SendMatchStatus(room, playerId);
                    break;

                case GameMessageTypes.PlayerRespawn:
                    HandlePlayerRespawn(room, playerId, json);
                    break;

                case GameMessageTypes.PlayerPose:
                    HandlePlayerPose(room, playerId, json);
                    break;

                case GameMessageTypes.ObjectSpawned:
                    Console.WriteLine($"[Match] Ignored client-supplied object spawn from '{playerId}'; objects are server-authoritative");
                    break;

                case GameMessageTypes.ObjectDestroyed:
                    Console.WriteLine($"[Match] Ignored client-supplied object destroy from '{playerId}'; objects are server-authoritative");
                    break;

                default:
                    Console.WriteLine($"[Match] Unknown system event type: {eventType}");
                    break;
            }
        }

        private static void ProcessRealtimeGameEvent(MatchRoom room, string eventType, JObject json, string playerId, string remoteEndPoint)
        {
            if (room == null || string.IsNullOrWhiteSpace(playerId) ||
                !room.Players.Any(player => string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"[Match] Ignored realtime event from non-member '{playerId}' in room '{room?.Id}'");
                return;
            }

            switch (eventType)
            {
                case GameMessageTypes.PlayerKilled:
                    Console.WriteLine($"[Match] Ignored client-supplied kill event from '{playerId}'; kills are server-authoritative");
                    break;

                case "PlayerDamaged":
                    Console.WriteLine($"[Match] Ignored client-supplied damage event from '{playerId}'; damage is server-authoritative");
                    break;

                case GameMessageTypes.FlagCaptured:
                    HandleFlagCaptured(room, playerId);
                    break;

                case GameMessageTypes.FlagLost:
                    HandleFlagLost(room, playerId);
                    break;

                case GameMessageTypes.FlagPickup:
                    HandleFlagPickup(room, playerId);
                    break;

                case GameMessageTypes.FlagReturn:
                    var returnedByPlayerId = ReadString(json, "ReturnedByPlayerId", "ReturnedByPlayerID", "PlayerID", "PlayerId");
                    if (!string.IsNullOrWhiteSpace(returnedByPlayerId) &&
                        !string.Equals(returnedByPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[Match] Ignored flag return with forged player '{returnedByPlayerId}' from '{playerId}'");
                        break;
                    }

                    HandleFlagReturn(room, playerId);
                    break;

                case GameMessageTypes.FlagScoreUpdate:
                    HandleFlagScoreUpdate(room);
                    break;

                case "PlayerEliminated":
                    HandlePlayerEliminated(room, playerId);
                    break;

                case "PlayerPositionUpdate":
                    var position = json.GetValue("Position") as JObject;
                    if (position != null)
                    {
                        HandlePositionUpdate(room, playerId, position);
                    }
                    break;

                case GameMessageTypes.PlayerShot:
                    HandlePlayerShot(room, playerId, json);
                    break;

                case GameMessageTypes.GrenadeThrow:
                    HandleGrenadeThrow(room, playerId, json);
                    break;
                case GameMessageTypes.FieldItemPickup:
                    HandleFieldItemPickup(room, playerId, json);
                    break;

                default:
                    Console.WriteLine($"Unknown realtime game event type: {eventType}");
                    break;
            }
        }

        /// <summary>
        /// Grants a field item to a player, or refuses.
        /// <para>
        /// The client sends the item id and the server used to have no route for
        /// this message at all, so the item never became claimed and the client
        /// only saw its own optimistic state. Nothing about the pickup was
        /// authoritative: the client decided it had touched an item and applied
        /// the effect locally.
        /// </para>
        /// <para>
        /// The claim is checked against the position the server already tracks
        /// for the player, so an id cannot be redeemed from across the map. The
        /// player id in the message is ignored in favour of the connection's
        /// player, so one player cannot claim on behalf of another.
        /// </para>
        /// </summary>
        /// <summary>
        /// The pickup budget, shared by the reliable and the realtime route so a
        /// client cannot get twice the allowance by alternating between them.
        /// </summary>
        private static readonly FieldItemPickupRateLimiter ItemPickupBudget = new();


        private static void HandleFieldItemPickup(MatchRoom room, string playerId, JObject json)
        {
            // Both routes draw on one budget, so alternating between them does
            // not buy a second allowance.
            if (!ItemPickupBudget.TryConsume(playerId))
            {
                return;
            }

            var itemId = ReadString(json, "ItemId", "itemId", "ItemID") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Console.WriteLine($"[Match] Ignored field item pickup with no item id from '{playerId}'");
                return;
            }

            // A client supplied player id is not evidence of who is asking.
            var claimed = ReadString(json, "PlayerID", "PlayerId");
            if (!string.IsNullOrWhiteSpace(claimed) &&
                !string.Equals(claimed, playerId, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Match] Ignored field item pickup for '{claimed}' sent by '{playerId}'");
                return;
            }

            var itemManager = MatchRoomManager.Instance.GetFieldItemManager(room.Id);
            if (itemManager == null)
            {
                Console.WriteLine($"[Match] Field item pickup for '{playerId}' with no item manager in room '{room.Id}'");
                return;
            }

            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (string.IsNullOrEmpty(state.PlayerId))
            {
                // No authoritative position recorded yet, so the radius cannot be
                // enforced. Refuse rather than hand out an unverified claim.
                Console.WriteLine($"[Match] Refused field item pickup for '{playerId}': no authoritative position");
                return;
            }

            var granted = itemManager.PickupItem(itemId, playerId, state.PositionX, state.PositionY, state.PositionZ);
            if (!granted)
            {
                Console.WriteLine($"[Match] Refused field item pickup of '{itemId}' by '{playerId}'");
                return;
            }

            if (!itemManager.TryGetItem(itemId, out var item))
            {
                return;
            }

            var type = item!.ItemType;
            var duration = FieldItemTypeNames.IsTimedBuff(type)
                ? FieldItemDefaults.DurationSeconds
                : 0f;

            // The duration is decided here rather than taken from the client.
            // A timed item used to carry its own thirty second lifetime that
            // only the client knew about, so the server could neither shorten
            // it nor answer a question about it.
            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.FieldItemPickup,
                ["RoomID"] = room.Id.ToString(),
                ["ItemId"] = itemId,
                ["PlayerID"] = playerId,
                ["ItemType"] = FieldItemTypeNames.ToWireName(type),
                ["Duration"] = duration,
                ["IsTimedBuff"] = FieldItemTypeNames.IsTimedBuff(type),
                ["ExpiresAtSeconds"] = duration > 0f
                    ? NowSeconds() + duration
                    : 0.0d,
                ["Success"] = true,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }
        /// <summary>
        /// Seconds on a clock the client cannot influence, used for effect
        /// expiry stamps in the messages sent to clients.
        /// </summary>
        private static double NowSeconds() => (double)DateTime.UtcNow.TimeOfDay.TotalSeconds;

        private static void HandlePlayerKilled(MatchRoom room, string killerId, string killedPlayerId)
        {
            Console.WriteLine($"Player {killedPlayerId} was killed by {killerId}");
            GameMessageDispatcher.SendPlayerKilled(room.Id.ToString(), killerId, killedPlayerId);
        }

        private static void HandlePlayerDamaged(
            MatchRoom room,
            string damagedPlayerId,
            int damage,
            string attackerId = "",
            JObject? hitPosition = null,
            int? remainingHealth = null)
        {
            Console.WriteLine($"Player {damagedPlayerId} took {damage} damage");

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = "PlayerDamaged",
                ["RoomID"] = room.Id.ToString(),
                ["DamagedPlayerID"] = damagedPlayerId,
                ["TargetId"] = damagedPlayerId,
                ["AttackerID"] = attackerId,
                ["AttackerId"] = attackerId,
                ["Damage"] = damage,
                ["RemainingHealth"] = remainingHealth,
                ["HitPosition"] = hitPosition,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        private static void HandleFlagCaptured(MatchRoom room, string playerId)
        {
            var team = ResolvePlayerTeam(room, playerId);
            if (team == ETeam.NoTeam || !AcceptFlagEvent(room, playerId, GameMessageTypes.FlagCaptured, TimeSpan.FromSeconds(2)) ||
                !FlagCarriers.TryRemove(GetFlagCarrierKey(room, playerId), out _))
            {
                Console.WriteLine($"[Match] Ignored flag capture without a server-tracked carrier '{playerId}'");
                return;
            }

            Console.WriteLine($"Team {team} captured the flag");
            room.AddFlagCapture(team);

            GameMessageDispatcher.SendFlagCaptured(room.Id.ToString(), team.ToString());
            GameMessageDispatcher.SendFlagScoreUpdate(
                room.Id.ToString(),
                room.GetFlagScore(ETeam.Red),
                room.GetFlagScore(ETeam.Blue));
        }

        private static void HandleFlagLost(MatchRoom room, string playerId)
        {
            var team = ResolvePlayerTeam(room, playerId);
            if (team == ETeam.NoTeam || !AcceptFlagEvent(room, playerId, GameMessageTypes.FlagLost, TimeSpan.FromMilliseconds(500)))
            {
                return;
            }

            Console.WriteLine($"Team {team} lost the flag");
            FlagCarriers.TryRemove(GetFlagCarrierKey(room, playerId), out _);
            GameMessageDispatcher.SendFlagLost(room.Id.ToString(), team.ToString(), playerId);
        }

        private static void HandleFlagPickup(MatchRoom room, string playerId)
        {
            var team = ResolvePlayerTeam(room, playerId);
            if (team == ETeam.NoTeam || !AcceptFlagEvent(room, playerId, GameMessageTypes.FlagPickup, TimeSpan.FromMilliseconds(500)))
            {
                return;
            }

            Console.WriteLine($"Team {team} picked up the flag");
            FlagCarriers[GetFlagCarrierKey(room, playerId)] = 0;
            GameMessageDispatcher.SendFlagPickup(room.Id.ToString(), team.ToString(), playerId);
        }

        private static void HandleFlagReturn(MatchRoom room, string playerId)
        {
            var team = ResolvePlayerTeam(room, playerId);
            if (team == ETeam.NoTeam || !AcceptFlagEvent(room, playerId, GameMessageTypes.FlagReturn, TimeSpan.FromMilliseconds(500)))
            {
                return;
            }

            Console.WriteLine($"Team {team} returned the flag");
            FlagCarriers.TryRemove(GetFlagCarrierKey(room, playerId), out _);
            GameMessageDispatcher.SendFlagReturn(room.Id.ToString(), team.ToString(), playerId);
        }

        private static ETeam ResolvePlayerTeam(MatchRoom room, string playerId)
        {
            return room.Players.FirstOrDefault(player =>
                string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase))?.Team ?? ETeam.NoTeam;
        }

        private static bool AcceptFlagEvent(MatchRoom room, string playerId, string eventType, TimeSpan cooldown)
        {
            var key = $"{room.Id}:{playerId}:{eventType}";
            var now = DateTime.UtcNow;
            if (LastFlagEvents.TryGetValue(key, out var lastEvent) && now - lastEvent < cooldown)
            {
                Console.WriteLine($"[Match] Ignored repeated {eventType} from '{playerId}' in room '{room.Id}'");
                return false;
            }

            LastFlagEvents[key] = now;
            return true;
        }

        private static string GetFlagCarrierKey(MatchRoom room, string playerId)
        {
            return $"{room.Id}:{playerId}";
        }

        public static void ClearPlayerState(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            foreach (var entry in FlagCarriers.Keys.Where(key => key.EndsWith($":{playerId}", StringComparison.OrdinalIgnoreCase)))
            {
                FlagCarriers.TryRemove(entry, out _);
            }

            foreach (var entry in LastFlagEvents.Keys.Where(key => key.Contains($":{playerId}:", StringComparison.OrdinalIgnoreCase)))
            {
                LastFlagEvents.TryRemove(entry, out _);
            }

            LastShots.TryRemove(playerId, out _);
        }

        private static void HandleFlagScoreUpdate(MatchRoom room)
        {
            // Score is derived from server-side flag events. Never relay the
            // client-provided score fields as authoritative state.
            var currentRed = room.GetFlagScore(ETeam.Red);
            var currentBlue = room.GetFlagScore(ETeam.Blue);
            GameMessageDispatcher.SendFlagScoreUpdate(room.Id.ToString(), currentRed, currentBlue);
        }

        private static void HandlePlayerEliminated(MatchRoom room, string playerId)
        {
            Console.WriteLine($"Player {playerId} was eliminated");
        }

        private static void SendMatchStatus(MatchRoom room, string requestingPlayerId)
        {
            var status = $"Match Active - Players: {room.Players.Count}";
            Console.WriteLine($"Sending status to {requestingPlayerId}: {status}");
            GameMessageDispatcher.SendMatchStatus(room.Id.ToString(), status);
        }

        private static void HandlePositionUpdate(MatchRoom room, string playerId, JObject position)
        {
            Console.WriteLine($"Player {playerId} position updated");
        }

        private static void HandlePlayerRespawn(MatchRoom room, string playerId, JObject json)
        {
            // The client may request a respawn, but it never chooses the
            // position. Until map-specific spawn tables are configured, keep
            // the authoritative server position and ignore client coordinates.
            var serverState = MatchServerV2.Instance?.ServerLagCompensationManager.GetPlayerState(playerId) ?? default;
            if (!string.Equals(serverState.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Match] Ignored respawn for unregistered player '{playerId}'");
                return;
            }

            var spawnX = serverState.PositionX;
            var spawnY = serverState.PositionY;
            var spawnZ = serverState.PositionZ;
            var respawnedPlayer = room.Players.FirstOrDefault(player =>
                string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase));
            if (respawnedPlayer == null || respawnedPlayer.Health > 0)
            {
                Console.WriteLine($"[Match] Ignored respawn request from living or unknown player '{playerId}'");
                return;
            }

            if (ServerManager.Instance.Settings.TryGetRespawnPoint(
                    respawnedPlayer.Team,
                    room.Players.IndexOf(respawnedPlayer),
                    out var configuredSpawn))
            {
                spawnX = configuredSpawn.X;
                spawnY = configuredSpawn.Y;
                spawnZ = configuredSpawn.Z;
            }

            var spawnPosition = new JObject
            {
                ["X"] = spawnX,
                ["Y"] = spawnY,
                ["Z"] = spawnZ
            };
            MatchServerV2.Instance?.ServerLagCompensationManager.SetPlayerPosition(
                playerId, spawnX, spawnY, spawnZ);

            respawnedPlayer.Health = Math.Max(1, respawnedPlayer.MaxHealth);

            GameMessageDispatcher.SendPlayerRespawn(room.Id.ToString(), playerId, spawnPosition);
        }

        private static void HandlePlayerPose(MatchRoom room, string playerId, JObject json)
        {
            var poseState = ReadString(json, "PoseState", "Pose", "Posture") ?? "Stand";
            if (!Enum.TryParse<EPlayerPoseState>(poseState, true, out var pose) ||
                !Enum.IsDefined(typeof(EPlayerPoseState), pose))
            {
                Console.WriteLine($"[Match] Ignored invalid pose '{poseState}' from '{playerId}'");
                return;
            }

            var canonicalPose = pose.ToString();
            Console.WriteLine($"Player {playerId} pose changed to {canonicalPose}");
            room.SetPlayerPoseState(playerId, pose);
            GameMessageDispatcher.SendPlayerPose(room.Id.ToString(), playerId, canonicalPose);
        }

        private static void HandlePlayerShot(MatchRoom room, string playerId, JObject shotData)
        {
            // 射撃処理 - ヒット判定、ダメージ計算など
            var targetId = shotData.GetStringOrNull("TargetID");
            var weaponType = NormalizeWeaponType(shotData.GetStringOrNull("WeaponType"));

            if (weaponType == null)
            {
                Console.WriteLine($"[Match] Ignored shot with unknown weapon from '{playerId}'");
                return;
            }

            if (!AcceptShot(playerId, weaponType))
            {
                return;
            }

            Console.WriteLine($"Player {playerId} shot with {weaponType}");

            if (!string.IsNullOrWhiteSpace(targetId) &&
                string.Equals(targetId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Match] Ignored self-targeted shot from '{playerId}'");
                targetId = null;
            }

            if (!string.IsNullOrWhiteSpace(targetId))
            {
                // ヒット判定とダメージ処理
                HandleShotHit(room, playerId, targetId, weaponType);
            }

            // 全プレイヤーに射撃イベントをブロードキャスト（UDP）
            BroadcastShotEvent(room, playerId, shotData);
        }

        /// <summary>
        /// Server side flight of bullets and grenades. The live match path drives
        /// it, so a grenade is simulated rather than only echoed back.
        /// </summary>
        private static readonly OpenGSServer.Network.ServerProjectileSimulator Projectiles =
            new();

        /// <summary>
        /// Applies a simulation tick to every live projectile.
        /// </summary>
        public static void UpdateProjectiles(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || !float.IsFinite(deltaSeconds))
            {
                return;
            }

            Projectiles.PlayerPositionLookup = playerId =>
            {
                var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
                return new Vector2(state.PositionX, state.PositionY);
            };
            Projectiles.PlayerIds = () => GetActiveMatchPlayerIds();
            Projectiles.OnSpawn = (projectile, spawnType) => BroadcastProjectileSpawn(projectile, spawnType);
            Projectiles.OnExpire = projectile => BroadcastProjectileExpire(projectile);
            Projectiles.OnDamage = (targetId, attackerId, damage, hit) =>
                HandleProjectileDamage(targetId, attackerId, damage, hit);

            Projectiles.Update(deltaSeconds);
        }
        /// <summary>
        /// Every player currently in a live match, used to resolve projectile
        /// hits. The simulator asks for this each tick rather than caching it,
        /// so a player who joins or leaves is reflected immediately.
        /// </summary>
        private static List<string> GetActiveMatchPlayerIds()
        {
            var ids = new List<string>();
            foreach (var room in MatchRoomManager.Instance.AllRooms().OfType<MatchRoom>())
            {
                if (!room.Playing)
                {
                    continue;
                }

                foreach (var player in room.Players)
                {
                    if (!string.IsNullOrWhiteSpace(player.Id))
                    {
                        ids.Add(player.Id);
                    }
                }
            }

            return ids;
        }

        private static void BroadcastProjectileSpawn(
            OpenGSServer.Network.ServerProjectileState projectile,
            string spawnType)
        {
            var room = MatchRoomManager.Instance.GetRoomById(projectile.RoomId);
            if (room == null)
            {
                return;
            }

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectSpawned,
                ["ObjectId"] = projectile.ProjectileId,
                ["ObjectType"] = spawnType,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = projectile.Position.X,
                ["PosY"] = projectile.Position.Y,
                ["ProjectileId"] = projectile.ProjectileId
            });
        }

        private static void BroadcastProjectileExpire(OpenGSServer.Network.ServerProjectileState projectile)
        {
            var room = MatchRoomManager.Instance.GetRoomById(projectile.RoomId);
            if (room == null)
            {
                return;
            }

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectDestroyed,
                ["ObjectId"] = projectile.ProjectileId,
                ["RoomID"] = room.Id.ToString()
            });
        }

        private static void HandleProjectileDamage(
            string targetId,
            string attackerId,
            int damage,
            Vector2 hitPosition)
        {
            var room = MatchRoomManager.Instance.SearchRoomByMemberID(targetId);
            if (room == null)
            {
                return;
            }

            var poseMultiplier = GetPoseDamageMultiplier(room.Id.ToString(), targetId);
            var adjusted = Math.Max(1, (int)MathF.Round(damage * poseMultiplier));

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = "PlayerDamaged",
                ["RoomID"] = room.Id.ToString(),
                ["DamagedPlayerID"] = targetId,
                ["AttackerID"] = attackerId,
                ["Damage"] = adjusted,
                ["PoseMultiplier"] = poseMultiplier,
                ["HitPosition"] = new JObject { ["X"] = hitPosition.X, ["Y"] = hitPosition.Y },
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }
        /// <summary>
        /// Reads a two component position out of a message, accepting both the
        /// nested and the flat shape clients have used.
        /// </summary>
        private static Vector2 ReadVector2(JObject json, params string[] keys)
        {
            if (json == null || keys == null)
            {
                return Vector2.Zero;
            }

            foreach (var key in keys)
            {
                var nested = json[key] as JObject;
                if (nested != null)
                {
                    return new Vector2(
                        GetFloat(nested["X"] ?? nested["x"], 0f),
                        GetFloat(nested["Y"] ?? nested["y"], 0f));
                }
            }

            foreach (var key in keys)
            {
                if (json[key] == null)
                {
                    continue;
                }

                var x = GetFloat(json[key + "X"] ?? json[key + "x"], float.NaN);
                var y = GetFloat(json[key + "Y"] ?? json[key + "y"], float.NaN);
                if (!float.IsNaN(x) || !float.IsNaN(y))
                {
                    return new Vector2(
                        float.IsNaN(x) ? 0f : x,
                        float.IsNaN(y) ? 0f : y);
                }
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// Pose state scaling applied to damage, so a crouching or rolling player
        /// takes less. Unknown pose falls back to full damage.
        /// </summary>
        private static float GetPoseDamageMultiplier(string roomId, string playerId)
        {
            var room = MatchRoomManager.Instance.GetRoomById(roomId);
            if (room == null)
            {
                return 1f;
            }

            var pose = room.GetPlayerPoseState(playerId);
            return pose switch
            {
                EPlayerPoseState.Sit => 0.5f,
                EPlayerPoseState.LieDown => 0.75f,
                _ => 1f
            };
        }
        private static void HandleGrenadeThrow(MatchRoom room, string playerId, JObject grenadeData)
        {
            var grenadeType = grenadeData.GetStringOrNull("GrenadeType")
                ?? grenadeData.GetStringOrNull("WeaponType")
                ?? "Normal";
            var origin = ReadVector2(grenadeData, "Position", "Origin");
            var direction = ReadVector2(grenadeData, "Direction", "AimDirection");

            // The server now owns the flight, the fuse, and the blast. The throw
            // used to be logged and echoed to the room, so the grenade existed
            // only as a message and nothing was ever simulated.
            var projectileId = Projectiles.CreateGrenade(
                room.Id.ToString(), playerId, origin, direction, grenadeType);

            Console.WriteLine($"Player {playerId} threw {grenadeType} ({projectileId})");
            BroadcastGrenadeEvent(room, playerId, grenadeData, projectileId);
        }
        private static void HandleObjectSpawned(MatchRoom room, string playerId, JObject objectData)
        {
            var objectType = objectData.GetStringOrNull("ObjectType") ?? "Unknown";
            var objectId = objectData.GetStringOrNull("ObjectId") ?? Guid.NewGuid().ToString("N");
            var position = objectData.GetValue("Position") as JObject;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} spawned {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectSpawned,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["PlayerID"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = GetFloat(objectData.GetValue("PosX") ?? position?.GetValue("X") ?? position?.GetValue("x"), 0f),
                ["PosY"] = GetFloat(objectData.GetValue("PosY") ?? position?.GetValue("Y") ?? position?.GetValue("y"), 0f),
                ["Rotation"] = GetFloat(objectData.GetValue("Rotation"), 0f),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void HandleObjectDestroyed(MatchRoom room, string playerId, JObject objectData)
        {
            var objectId = objectData.GetStringOrNull("ObjectId") ?? Guid.NewGuid().ToString("N");
            var objectType = objectData.GetStringOrNull("ObjectType") ?? "Unknown";
            var position = objectData.GetValue("Position") as JObject;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} destroyed {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectDestroyed,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["DestroyedBy"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = GetFloat(objectData.GetValue("PosX") ?? position?.GetValue("X") ?? position?.GetValue("x"), 0f),
                ["PosY"] = GetFloat(objectData.GetValue("PosY") ?? position?.GetValue("Y") ?? position?.GetValue("y"), 0f),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void HandleShotHit(MatchRoom room, string shooterId, string targetId, string? weaponType)
        {
            var shooter = room.Players.FirstOrDefault(player =>
                string.Equals(player.Id, shooterId, StringComparison.OrdinalIgnoreCase));
            var target = room.Players.FirstOrDefault(player =>
                string.Equals(player.Id, targetId, StringComparison.OrdinalIgnoreCase));

            if (shooter == null || target == null)
            {
                Console.WriteLine($"[Match] Ignored shot against non-member '{targetId}' in room '{room.Id}'");
                return;
            }

            if (shooter.Health <= 0 || target.Health <= 0 ||
                !IsServerValidatedShot(shooterId, targetId, weaponType))
            {
                Console.WriteLine($"[Match] Ignored invalid shot {shooterId}->{targetId} in room '{room.Id}'");
                return;
            }

            // ダメージ計算（武器タイプによる）
            int damage = CalculateWeaponDamage(weaponType);
            LobbyServerManager.Instance.RecordDamageDailyProgress(shooterId, damage);

            target.Health = Math.Max(0, target.Health - damage);
            var targetState = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(targetId);
            var authoritativeHitPosition = new JObject
            {
                ["X"] = targetState.PositionX,
                ["Y"] = targetState.PositionY,
                ["Z"] = targetState.PositionZ
            };
            HandlePlayerDamaged(room, targetId, damage, shooterId, authoritativeHitPosition, target.Health);

            if (target.Health <= 0)
            {
                target.Deaths++;
                shooter.Kills++;
                shooter.Score += 100;
                HandlePlayerKilled(room, shooterId, targetId);
            }
        }

        private static bool IsServerValidatedShot(string shooterId, string targetId, string? weaponType)
        {
            var stateManager = MatchServerV2.Instance.ServerLagCompensationManager;
            var shooterState = stateManager.GetPlayerState(shooterId);
            var targetState = stateManager.GetPlayerState(targetId);
            if (!string.Equals(shooterState.PlayerId, shooterId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(targetState.PlayerId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var dx = shooterState.PositionX - targetState.PositionX;
            var dy = shooterState.PositionY - targetState.PositionY;
            var dz = shooterState.PositionZ - targetState.PositionZ;
            var distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);
            var range = weaponType switch
            {
                "Pistol" => 45f,
                "SMG" => 55f,
                "Shotgun" => 25f,
                "Rifle" => 90f,
                "Sniper" => 180f,
                _ => 60f
            };

            return !float.IsNaN(distanceSquared) &&
                   !float.IsInfinity(distanceSquared) &&
                   distanceSquared <= range * range;
        }

        private static int CalculateWeaponDamage(string? weaponType)
        {
            return weaponType switch
            {
                "Pistol" => 25,
                "Rifle" => 35,
                "Sniper" => 80,
                "Shotgun" => 20,
                "SMG" => 15,
                _ => 30 // デフォルトダメージ
            };
        }

        private static bool AcceptShot(string playerId, string weaponType)
        {
            var cooldown = weaponType switch
            {
                "Pistol" => TimeSpan.FromMilliseconds(200),
                "SMG" => TimeSpan.FromMilliseconds(80),
                "Shotgun" => TimeSpan.FromMilliseconds(800),
                "Rifle" => TimeSpan.FromMilliseconds(150),
                "Sniper" => TimeSpan.FromSeconds(1),
                _ => TimeSpan.FromMilliseconds(200)
            };
            var now = DateTime.UtcNow;
            if (LastShots.TryGetValue(playerId, out var lastShot) && now - lastShot < cooldown)
            {
                Console.WriteLine($"[Match] Ignored rapid {weaponType} shot from '{playerId}'");
                return false;
            }

            LastShots[playerId] = now;
            return true;
        }

        // UDPブロードキャストメソッド群
        private static void BroadcastShotEvent(MatchRoom room, string playerId, JObject shotData)
        {
            var serverState = MatchServerV2.Instance?.ServerLagCompensationManager.GetPlayerState(playerId) ?? default;
            if (!string.Equals(serverState.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) ||
                !IsFinite(serverState.PositionX) || !IsFinite(serverState.PositionY) || !IsFinite(serverState.PositionZ))
            {
                Console.WriteLine($"[Match] Ignored shot broadcast from unregistered player '{playerId}'");
                return;
            }

            var objectId = Guid.NewGuid().ToString("N");
            var weaponType = NormalizeWeaponType(shotData.GetStringOrNull("WeaponType")) ?? "Unknown";
            var direction = shotData.GetValue("Direction") as JObject;
            var dirX = GetFloat(shotData.GetValue("DirX") ?? direction?.GetValue("X") ?? direction?.GetValue("x"), 1f);
            var dirY = GetFloat(shotData.GetValue("DirY") ?? direction?.GetValue("Y") ?? direction?.GetValue("y"), 0f);
            var directionLength = MathF.Sqrt((dirX * dirX) + (dirY * dirY));
            if (!IsFinite(dirX) || !IsFinite(dirY) || !IsFinite(directionLength) || directionLength < 0.001f)
            {
                Console.WriteLine($"[Match] Ignored shot with invalid direction from '{playerId}'");
                return;
            }

            dirX /= directionLength;
            dirY /= directionLength;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} fired shot ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerShot,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["ObjectId"] = objectId,
                ["PosX"] = serverState.PositionX,
                ["PosY"] = serverState.PositionY,
                ["PosZ"] = serverState.PositionZ,
                ["DirX"] = dirX,
                ["DirY"] = dirY,
                ["WeaponType"] = weaponType,
                ["ShotData"] = new JObject
                {
                    ["ObjectId"] = objectId,
                    ["WeaponType"] = weaponType,
                    ["PosX"] = serverState.PositionX,
                    ["PosY"] = serverState.PositionY,
                    ["PosZ"] = serverState.PositionZ,
                    ["DirX"] = dirX,
                    ["DirY"] = dirY
                },
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            // UDPブロードキャスト（ルーム内の全プレイヤー）
            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void BroadcastGrenadeEvent(MatchRoom room, string playerId, JObject grenadeData, string objectId)
        {
            var objectType = NormalizeGrenadeObjectType(grenadeData.GetStringOrNull("GrenadeType"));
            var serverState = MatchServerV2.Instance?.ServerLagCompensationManager.GetPlayerState(playerId) ?? default;
            if (!string.Equals(serverState.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) ||
                !IsFinite(serverState.PositionX) || !IsFinite(serverState.PositionY) || !IsFinite(serverState.PositionZ))
            {
                Console.WriteLine($"[Match] Ignored grenade from unregistered player '{playerId}' in room '{room.Id}'");
                return;
            }

            var direction = grenadeData.GetValue("Direction") as JObject;
            var dirX = GetFloat(grenadeData.GetValue("DirX") ?? direction?.GetValue("X") ?? direction?.GetValue("x"), float.NaN);
            var dirY = GetFloat(grenadeData.GetValue("DirY") ?? direction?.GetValue("Y") ?? direction?.GetValue("y"), float.NaN);
            var directionLength = MathF.Sqrt((dirX * dirX) + (dirY * dirY));
            if (!IsFinite(dirX) || !IsFinite(dirY) || !IsFinite(directionLength) || directionLength < 0.001f)
            {
                Console.WriteLine($"[Match] Ignored grenade with invalid direction from '{playerId}'");
                return;
            }

            dirX /= directionLength;
            dirY /= directionLength;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} threw {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.GrenadeThrow,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = serverState.PositionX,
                ["PosY"] = serverState.PositionY,
                ["PosZ"] = serverState.PositionZ,
                ["DirX"] = dirX,
                ["DirY"] = dirY,
                ["GrenadeType"] = objectType,
                ["Direction"] = new JObject
                {
                    ["X"] = dirX,
                    ["Y"] = dirY
                },
                ["Rotation"] = MathF.Atan2(dirY, dirX),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static float GetFloat(JToken? token, float fallback)
        {
            return token != null && float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string NormalizeGrenadeObjectType(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "power" => "PowerGrenade",
                "magnetic" => "MagneticGrenade",
                "magnet" => "MagneticGrenade",
                "mine" => "MineGrenade",
                "cluster" => "ClusterGrenade",
                "clusterchild" => "ChildClusterGrenade",
                "fire" => "FireGrenade",
                "smoke" => "SmokeGrenade",
                _ => "NormalGrenade"
            };
        }

        private static string? NormalizeWeaponType(string? weaponType)
        {
            var normalized = weaponType?.Trim().ToLowerInvariant()
                .Replace("(clone)", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            return normalized switch
            {
                "pistol" or "handgun" or "glock" or "deserteagle" => "Pistol",
                "smg" or "submachinegun" or "scorpion" or "fnp90" or "uzi" or "mp5" => "SMG",
                "shotgun" => "Shotgun",
                "rifle" or "assaultrifle" or "ak47" or "m16" or "famas" or "f2000" or
                    "steyraug" or "mg42" or "m60" or "fnminimisaw" => "Rifle",
                "sniper" or "sniperrifle" or "scout" or "dragunov" or "psg1" or "awp" => "Sniper",
                _ => null
            };
        }

        private static void UdpBroadcastToRoom(string roomId, JObject message)
        {
            GameMessageDispatcher.BroadcastToRoom(roomId, message);
        }

        /// <summary>
        /// 後方互換性のためのメソッド - TCPイベントとして処理
        /// </summary>
        public static void ParseEvent(JObject json)
        {
            ParseTcpEvent(json);
        }

        /// <summary>
        /// UDPサーバーからゲームイベントを受信した際に呼び出す
        /// </summary>
        public static void HandleUdpGameEvent(byte[] data, string remoteEndPoint)
        {
            ParseUdpEvent(data, remoteEndPoint);
        }

        /// <summary>
        /// TCPサーバーからシステムイベントを受信した際に呼び出す
        /// </summary>
        public static void HandleTcpSystemEvent(JObject json)
        {
            ParseTcpEvent(json);
        }

        private static string ReadString(JObject json, params string[] keys)
        {
            if (json == null || keys == null)
            {
                return null;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var value = json.GetValue(key)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool IsRoomMatch(MatchRoom room, string roomId)
        {
            return string.Equals(room.Id.ToString(), roomId, StringComparison.OrdinalIgnoreCase);
        }

    }
}
