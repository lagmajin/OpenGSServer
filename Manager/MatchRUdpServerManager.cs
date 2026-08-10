using System;
using System.Collections.Generic;
using System.Numerics;
using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;

namespace OpenGSServer
{
    internal static class MatchMessageTypes
    {
        public const string PlayerShot = "PlayerShot";
        public const string GrenadeThrow = "GrenadeThrow";
        public const string ObjectSpawned = "ObjectSpawned";
        public const string ObjectDestroyed = "ObjectDestroyed";
    }

    /// <summary>
    /// UDPベースのリアルタイムゲーム通信マネージャー
    /// </summary>
    public class MatchRUdpServerManager
    {
        private NetManager? netManager;
        private EventBasedNetListener? listener;
        private Dictionary<string, NetPeer> connectedPlayers =
            new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> playerRoomMapping =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ServerPlayerStateManager playerStateManager = new();
        private readonly Dictionary<string, ServerProjectileState> projectiles = new();
        private readonly object projectileLock = new();
        private int projectileSequence = 0;
        private const float ProjectileTickSeconds = 0.05f;
        private const float BulletLifetimeSeconds = 3.0f;
        private const float GrenadeLifetimeSeconds = 4.0f;
        private const float BulletSpeed = 22f;
        private const float GrenadeSpeed = 12f;
        private const float BulletRadius = 0.15f;
        private const float GrenadeRadius = 0.35f;
        private const float PlayerHitRadius = 0.6f;

        public MatchRUdpServerManager()
        {
            InitializeUdpServer();
        }

        private void InitializeUdpServer()
        {
            listener = new EventBasedNetListener();
            netManager = new NetManager(listener);

            // イベントハンドラーの設定
            listener.ConnectionRequestEvent += OnConnectionRequest;
            listener.PeerConnectedEvent += OnPeerConnected;
            listener.PeerDisconnectedEvent += OnPeerDisconnected;
            listener.NetworkReceiveEvent += OnNetworkReceive;

            // UDPサーバー設定
            netManager.Start(63000); // マッチ用UDPポート
            netManager.BroadcastReceiveEnabled = true;
            netManager.UpdateTime = 15; // 15ms更新

            ConsoleWrite.WriteMessage("UDP Game Server started on port 63000", ConsoleColor.Cyan);
        }

        #region UDPイベントハンドラー

        private void OnConnectionRequest(ConnectionRequest request)
        {
            // 接続要求を常に受け入れる（実際の運用では認証が必要）
            request.Accept();
        }

        private void OnPeerConnected(NetPeer peer)
        {
            ConsoleWrite.WriteMessage($"UDP Peer connected: {peer.EndPoint}", ConsoleColor.Green);

            // 接続成功メッセージを送信
            var connectMessage = new JObject
            {
                ["MessageType"] = "UdpConnectionEstablished",
                ["PlayerID"] = peer.Id.ToString(),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            SendToPeer(peer, connectMessage);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            ConsoleWrite.WriteMessage($"UDP Peer disconnected: {peer.EndPoint}", ConsoleColor.Red);

            // プレイヤーマッピングから削除
            RemovePlayerMapping(peer);
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                // UDPパケットを処理
                var data = reader.GetRemainingBytes();
                var jsonString = System.Text.Encoding.UTF8.GetString(data);

                // JSONとしてパースを試行
                if (TryParseJson(jsonString, out JObject json))
                {
                    // JSONメッセージとして処理
                    ProcessUdpMessage(peer, json);
                }
                else
                {
                    // バイナリデータとして処理（将来的な拡張用）
                    ProcessBinaryData(peer, data);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"UDP receive error: {ex.Message}", ConsoleColor.Red);
            }
        }

        #endregion

        #region メッセージ処理

        private void ProcessUdpMessage(NetPeer peer, JObject message)
        {
            var messageType = message.GetStringOrNull("MessageType");
            var playerId = message.GetStringOrNull("PlayerID");
            var roomId = message.GetStringOrNull("RoomID");

            if (string.IsNullOrEmpty(messageType) || string.IsNullOrEmpty(playerId))
                return;

            // プレイヤールームマッピングを更新
            if (!string.IsNullOrEmpty(roomId))
            {
                playerRoomMapping[playerId] = roomId;
            }

            // メッセージタイプに応じて処理
            switch (messageType)
            {
                case "PlayerShotEvent":
                case "ShootRequest":
                case MatchMessageTypes.PlayerShot:
                    HandlePlayerShot(peer, message, playerId);
                    break;

                case "GrenadeThrowEvent":
                case MatchMessageTypes.GrenadeThrow:
                    HandlePlayerGrenade(peer, message, playerId);
                    break;

                case NetworkingConstants.MessageType.PlayerPosition:
                    HandlePlayerPosition(peer, message, playerId);
                    break;

                case NetworkingConstants.MessageType.PlayerAction:
                    HandlePlayerAction(peer, message, playerId);
                    break;

                case NetworkingConstants.MessageType.GameEvent:
                    HandleGameEvent(peer, message, playerId);
                    break;

                case GameMessageTypes.PlayerPose:
                    HandlePlayerPose(peer, message, playerId);
                    break;

                case NetworkingConstants.MessageType.Heartbeat:
                    HandleHeartbeat(peer, playerId);
                    break;

                default:
                    ConsoleWrite.WriteMessage($"Unknown UDP message type: {messageType}", ConsoleColor.Yellow);
                    break;
            }
        }

        private void ProcessBinaryData(NetPeer peer, byte[] data)
        {
            // 将来的なバイナリプロトコル拡張用
            ConsoleWrite.WriteMessage($"Binary data received: {data.Length} bytes", ConsoleColor.Gray);
        }

        #endregion

        #region ゲームイベント処理

        private void HandlePlayerPosition(NetPeer peer, JObject message, string playerId)
        {
            var position = message.GetValue("Position") as JObject;
            if (position != null)
            {
                var x = position.GetValue("X")?.ToObject<float>() ?? position.GetValue("x")?.ToObject<float>() ?? 0f;
                var y = position.GetValue("Y")?.ToObject<float>() ?? position.GetValue("y")?.ToObject<float>() ?? 0f;
                playerStateManager.SetPlayerPosition(playerId, x, y, 0f);
                // 位置情報をルーム内の全プレイヤーにブロードキャスト
                BroadcastToRoomExceptSender(playerId, message);
            }
        }

        private void HandlePlayerAction(NetPeer peer, JObject message, string playerId)
        {
            var actionType = message.GetStringOrNull("ActionType");

            switch (actionType)
            {
                case "Shoot":
                    HandlePlayerShot(peer, message, playerId);
                    break;

                case "Grenade":
                    HandlePlayerGrenade(peer, message, playerId);
                    break;

                case "Reload":
                    HandlePlayerReload(peer, message, playerId);
                    break;

                default:
                    // 不明なアクションはブロードキャスト
                    BroadcastToRoomExceptSender(playerId, message);
                    break;
            }
        }

        private void HandleGameEvent(NetPeer peer, JObject message, string playerId)
        {
            // ゲームイベントをMatchRoomHandlerに委譲
            InGameMatchEventHandler.HandleUdpGameEvent(
                System.Text.Encoding.UTF8.GetBytes(message.ToString()),
                playerId
            );
        }

        private void HandleHeartbeat(NetPeer peer, string playerId)
        {
            // ハートビート応答
            var response = new JObject
            {
                ["MessageType"] = "HeartbeatResponse",
                ["PlayerID"] = playerId,
                ["ServerTime"] = DateTime.UtcNow.ToString("o")
            };

            SendToPeer(peer, response);
        }

        private void HandlePlayerShot(NetPeer peer, JObject message, string playerId)
        {
            var roomId = GetRoomId(playerId);
            var weaponType = message.GetStringOrNull("WeaponType") ?? "Pistol";
            var position = ReadVector2(message, "Position", "Origin");
            var direction = ReadVector2(message, "Direction", "AimDirection");
            if (direction.LengthSquared() < 0.0001f)
            {
                direction = new Vector2(1f, 0f);
            }

            direction = Vector2.Normalize(direction);
            var projectileId = CreateProjectileId("bullet");
            var projectile = new ServerProjectileState
            {
                ProjectileId = projectileId,
                OwnerId = playerId,
                RoomId = roomId,
                Kind = ServerProjectileKind.Bullet,
                Position = position,
                Velocity = direction * BulletSpeed,
                Radius = BulletRadius,
                RemainingLifetime = BulletLifetimeSeconds,
                WeaponType = weaponType,
                Damage = CalculateWeaponDamage(weaponType)
            };

            lock (projectileLock)
            {
                projectiles[projectileId] = projectile;
            }

            BroadcastSpawn(projectile, "Bullet");
            BroadcastShotEvent(roomId, playerId, message, projectileId);
        }

        private void HandlePlayerGrenade(NetPeer peer, JObject message, string playerId)
        {
            var roomId = GetRoomId(playerId);
            var grenadeType = message.GetStringOrNull("GrenadeType") ?? message.GetStringOrNull("WeaponType") ?? "Normal";
            var position = ReadVector2(message, "Position", "Origin");
            var direction = ReadVector2(message, "Direction", "AimDirection");
            if (direction.LengthSquared() < 0.0001f)
            {
                direction = new Vector2(1f, 0f);
            }

            direction = Vector2.Normalize(direction);
            var projectileId = CreateProjectileId("grenade");
            var projectile = new ServerProjectileState
            {
                ProjectileId = projectileId,
                OwnerId = playerId,
                RoomId = roomId,
                Kind = ServerProjectileKind.Grenade,
                Position = position,
                Velocity = direction * GrenadeSpeed,
                Radius = GrenadeRadius,
                RemainingLifetime = GrenadeLifetimeSeconds,
                GrenadeType = grenadeType,
                ExplosionDamage = CalculateGrenadeDamage(grenadeType),
                ExplosionRadius = CalculateGrenadeRadius(grenadeType),
                FuseTime = CalculateGrenadeFuse(grenadeType)
            };

            lock (projectileLock)
            {
                projectiles[projectileId] = projectile;
            }

            BroadcastSpawn(projectile, $"{NormalizeGrenadeObjectType(grenadeType)}");
            BroadcastGrenadeEvent(roomId, playerId, message, projectileId);
        }

        private void HandlePlayerReload(NetPeer peer, JObject message, string playerId)
        {
            // リロードイベントをブロードキャスト
            BroadcastToRoomExceptSender(playerId, message);
        }

        private void HandlePlayerPose(NetPeer peer, JObject message, string playerId)
        {
            BroadcastToRoom(GetRoomId(playerId), message);
        }

        #endregion

        #region ユーティリティメソッド

        private bool TryParseJson(string jsonString, out JObject json)
        {
            try
            {
                json = JObject.Parse(jsonString);
                return true;
            }
            catch
            {
                json = null;
                return false;
            }
        }

        private int CalculateWeaponDamage(string? weaponType)
        {
            var gunType = EGunTypeExtensions.FromString(weaponType ?? string.Empty);
            return gunType.GetDamage();
        }

        private static int CalculateGrenadeDamage(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "power" => 160,
                "cluster" => 90,
                "fire" => 120,
                "magnet" => 80,
                _ => 110
            };
        }

        private static float CalculateGrenadeRadius(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "power" => 4.5f,
                "cluster" => 3.0f,
                "fire" => 3.5f,
                "magnet" => 2.5f,
                _ => 3.0f
            };
        }

        private static float CalculateGrenadeFuse(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "power" => 2.5f,
                "cluster" => 2.0f,
                "fire" => 2.2f,
                "magnet" => 2.0f,
                _ => 3.0f
            };
        }

        #endregion

        #region 送信メソッド

        public void SendToPlayer(string playerId, JObject message)
        {
            if (connectedPlayers.TryGetValue(playerId, out NetPeer peer))
            {
                SendToPeer(peer, message);
            }
        }

        public void BroadcastToRoom(string roomId, JObject message)
        {
            foreach (var kvp in playerRoomMapping)
            {
                if (kvp.Value == roomId && connectedPlayers.TryGetValue(kvp.Key, out NetPeer peer))
                {
                    SendToPeer(peer, message);
                }
            }
        }

        public void BroadcastToRoomExceptSender(string senderPlayerId, JObject message)
        {
            if (playerRoomMapping.TryGetValue(senderPlayerId, out string roomId))
            {
                foreach (var kvp in playerRoomMapping)
                {
                    if (kvp.Value == roomId && kvp.Key != senderPlayerId &&
                        connectedPlayers.TryGetValue(kvp.Key, out NetPeer peer))
                    {
                        SendToPeer(peer, message);
                    }
                }
            }
        }

        private void SendToPeer(NetPeer peer, JObject message)
        {
            try
            {
                var jsonString = message.ToString();
                var writer = new NetDataWriter();
                writer.Put(jsonString);
                peer.Send(writer, DeliveryMethod.Unreliable); // UDPなのでUnreliable
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"UDP send error: {ex.Message}", ConsoleColor.Red);
            }
        }

        #endregion

        #region プレイヤー管理

        public void RegisterPlayer(string playerId, NetPeer peer)
        {
            connectedPlayers[playerId] = peer;
            playerStateManager.RegisterPlayer(playerId);
        }

        public void UnregisterPlayer(string playerId)
        {
            connectedPlayers.Remove(playerId);
            playerRoomMapping.Remove(playerId);
            playerStateManager.UnregisterPlayer(playerId);
        }

        private void RemovePlayerMapping(NetPeer peer)
        {
            List<string> playersToRemove = new();
            foreach (var kvp in connectedPlayers)
            {
                if (kvp.Value == peer)
                {
                    playersToRemove.Add(kvp.Key);
                }
            }

            foreach (var playerId in playersToRemove)
            {
                UnregisterPlayer(playerId);
            }
        }

        #endregion

        #region ライフサイクル

        public void Update()
        {
            netManager?.PollEvents();
            UpdateProjectiles(ProjectileTickSeconds);
        }

        public void Shutdown()
        {
            netManager?.Stop();
            connectedPlayers.Clear();
            playerRoomMapping.Clear();
            lock (projectileLock)
            {
                projectiles.Clear();
            }
        }

        private void UpdateProjectiles(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            List<ServerProjectileState> toRemove = new();
            List<ServerProjectileState> toDestroy = new();

            lock (projectileLock)
            {
                foreach (var projectile in projectiles.Values)
                {
                    if (!projectile.IsActive)
                    {
                        continue;
                    }

                    projectile.RemainingLifetime -= deltaSeconds;
                    projectile.Position += projectile.Velocity * deltaSeconds;

                    if (projectile.Kind == ServerProjectileKind.Bullet)
                    {
                        if (TryResolveBulletHit(projectile))
                        {
                            projectile.IsActive = false;
                            toRemove.Add(projectile);
                            toDestroy.Add(projectile);
                            continue;
                        }
                    }
                    else
                    {
                        projectile.ElapsedFuse += deltaSeconds;
                        if (projectile.ElapsedFuse >= projectile.FuseTime || projectile.RemainingLifetime <= 0f)
                        {
                            projectile.IsActive = false;
                            SpawnClusterChildrenIfNeeded(projectile);
                            ApplyGrenadeDamage(projectile);
                            toRemove.Add(projectile);
                            toDestroy.Add(projectile);
                            continue;
                        }
                    }

                    if (projectile.RemainingLifetime <= 0f)
                    {
                        projectile.IsActive = false;
                        toRemove.Add(projectile);
                        toDestroy.Add(projectile);
                    }
                }

                foreach (var projectile in toRemove)
                {
                    projectiles.Remove(projectile.ProjectileId);
                }
            }

            foreach (var projectile in toDestroy)
            {
                BroadcastDestroy(projectile);
            }
        }

        private bool TryResolveBulletHit(ServerProjectileState projectile)
        {
            foreach (var kvp in connectedPlayers)
            {
                var targetId = kvp.Key;
                if (string.Equals(targetId, projectile.OwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var state = playerStateManager.GetPlayerState(targetId);
                var targetPos = new Vector2(state.PositionX, state.PositionY);
                if (Vector2.Distance(projectile.Position, targetPos) <= projectile.Radius + PlayerHitRadius)
                {
                    ApplyDamage(targetId, projectile.OwnerId, projectile.Damage, projectile.Position);
                    return true;
                }
            }

            return false;
        }

        private void ApplyGrenadeDamage(ServerProjectileState grenade)
        {
            foreach (var kvp in connectedPlayers)
            {
                var targetId = kvp.Key;
                var state = playerStateManager.GetPlayerState(targetId);
                var targetPos = new Vector2(state.PositionX, state.PositionY);
                var distance = Vector2.Distance(grenade.Position, targetPos);
                if (distance <= grenade.ExplosionRadius)
                {
                    var falloff = 1f - Math.Clamp(distance / Math.Max(0.001f, grenade.ExplosionRadius), 0f, 1f);
                    var damage = Math.Max(1, (int)MathF.Round(grenade.ExplosionDamage * falloff));
                    ApplyDamage(targetId, grenade.OwnerId, damage, grenade.Position);
                }
            }
        }

        private void SpawnClusterChildrenIfNeeded(ServerProjectileState grenade)
        {
            if (!string.Equals(grenade.GrenadeType, "Cluster", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var childDirections = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(-0.35f, 0.94f),
                new Vector2(-0.35f, -0.94f)
            };

            foreach (var dir in childDirections)
            {
                var childId = CreateProjectileId("cluster-child");
                var normalized = Vector2.Normalize(dir);
                var child = new ServerProjectileState
                {
                    ProjectileId = childId,
                    OwnerId = grenade.OwnerId,
                    RoomId = grenade.RoomId,
                    Kind = ServerProjectileKind.Grenade,
                    Position = grenade.Position,
                    Velocity = normalized * (GrenadeSpeed * 1.15f),
                    Radius = 0.25f,
                    RemainingLifetime = 1.25f,
                    GrenadeType = "ClusterChild",
                    ExplosionDamage = 60,
                    ExplosionRadius = 2.0f,
                    FuseTime = 1.0f
                };

                lock (projectileLock)
                {
                    projectiles[childId] = child;
                }

                BroadcastSpawn(child, "ChildClusterGrenade");
            }
        }

        private void ApplyDamage(string targetId, string attackerId, int damage, Vector2 hitPosition)
        {
            var roomId = GetRoomId(targetId);
            var poseMultiplier = GetPoseDamageMultiplier(roomId, targetId);
            var adjustedDamage = Math.Max(1, (int)MathF.Round(damage * poseMultiplier));

            var damageMessage = new JObject
            {
                ["MessageType"] = "PlayerDamaged",
                ["DamagedPlayerID"] = targetId,
                ["Damage"] = adjustedDamage,
                ["AttackerID"] = attackerId,
                ["PoseMultiplier"] = poseMultiplier,
                ["HitPosition"] = new JObject
                {
                    ["X"] = hitPosition.X,
                    ["Y"] = hitPosition.Y
                },
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            SendToPlayer(targetId, damageMessage);
            if (playerRoomMapping.TryGetValue(attackerId, out var attackerRoomId))
            {
                BroadcastToRoom(attackerRoomId, damageMessage);
            }
        }

        private float GetPoseDamageMultiplier(string roomId, string playerId)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
            {
                return 1f;
            }

            var room = MatchRoomManager.Instance.SearchRoomByMemberID(playerId);
            if (room == null)
            {
                return 1f;
            }

            var poseState = room.GetPlayerPoseState(playerId);
            return poseState switch
            {
                EPlayerPoseState.Sit => 0.88f,
                EPlayerPoseState.LieDown => 0.72f,
                _ => 1f
            };
        }

        private void BroadcastSpawn(ServerProjectileState projectile, string spawnType)
        {
            var message = new JObject
            {
                ["MessageType"] = MatchMessageTypes.ObjectSpawned,
                ["ObjectType"] = spawnType,
                ["ObjectId"] = projectile.ProjectileId,
                ["PlayerID"] = projectile.OwnerId,
                ["RoomID"] = projectile.RoomId,
                ["PosX"] = projectile.Position.X,
                ["PosY"] = projectile.Position.Y,
                ["DirX"] = projectile.Velocity.X,
                ["DirY"] = projectile.Velocity.Y,
                ["ProjectileType"] = projectile.Kind.ToString(),
                ["WeaponType"] = projectile.WeaponType,
                ["GrenadeType"] = projectile.GrenadeType,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(projectile.RoomId, message);
        }

        private void BroadcastDestroy(ServerProjectileState projectile)
        {
            var message = new JObject
            {
                ["MessageType"] = MatchMessageTypes.ObjectDestroyed,
                ["ObjectType"] = projectile.Kind == ServerProjectileKind.Bullet ? "Bullet" : NormalizeGrenadeObjectType(projectile.GrenadeType),
                ["ObjectId"] = projectile.ProjectileId,
                ["PlayerID"] = projectile.OwnerId,
                ["RoomID"] = projectile.RoomId,
                ["PosX"] = projectile.Position.X,
                ["PosY"] = projectile.Position.Y,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(projectile.RoomId, message);
        }

        private void BroadcastShotEvent(string roomId, string playerId, JObject original, string projectileId)
        {
            var message = new JObject
            {
                ["MessageType"] = MatchMessageTypes.PlayerShot,
                ["PlayerID"] = playerId,
                ["RoomID"] = roomId,
                ["ObjectId"] = projectileId,
                ["ShotData"] = original,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        private void BroadcastGrenadeEvent(string roomId, string playerId, JObject original, string projectileId)
        {
            var message = new JObject
            {
                ["MessageType"] = MatchMessageTypes.GrenadeThrow,
                ["PlayerID"] = playerId,
                ["RoomID"] = roomId,
                ["ObjectId"] = projectileId,
                ["GrenadeData"] = original,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        private static Vector2 ReadVector2(JObject json, params string[] keys)
        {
            foreach (var key in keys)
            {
                var token = json.GetValue(key) as JObject;
                if (token != null)
                {
                    var x = token.GetValue("X")?.ToObject<float>() ?? token.GetValue("x")?.ToObject<float>() ?? 0f;
                    var y = token.GetValue("Y")?.ToObject<float>() ?? token.GetValue("y")?.ToObject<float>() ?? 0f;
                    return new Vector2(x, y);
                }
            }

            var px = json.GetValue("PosX")?.ToObject<float>() ?? json.GetValue("PositionX")?.ToObject<float>() ?? 0f;
            var py = json.GetValue("PosY")?.ToObject<float>() ?? json.GetValue("PositionY")?.ToObject<float>() ?? 0f;
            return new Vector2(px, py);
        }

        private string GetRoomId(string playerId)
        {
            return playerRoomMapping.TryGetValue(playerId, out var roomId) ? roomId : string.Empty;
        }

        private string CreateProjectileId(string prefix)
        {
            return $"{prefix}-{DateTime.UtcNow.Ticks}-{System.Threading.Interlocked.Increment(ref projectileSequence)}";
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

        #endregion
    }

    internal enum ServerProjectileKind
    {
        Bullet,
        Grenade
    }

    internal sealed class ServerProjectileState
    {
        public string ProjectileId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public ServerProjectileKind Kind { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Radius { get; set; }
        public float RemainingLifetime { get; set; }
        public bool IsActive { get; set; } = true;
        public string WeaponType { get; set; } = string.Empty;
        public string GrenadeType { get; set; } = string.Empty;
        public int Damage { get; set; }
        public int ExplosionDamage { get; set; }
        public float ExplosionRadius { get; set; }
        public float FuseTime { get; set; }
        public float ElapsedFuse { get; set; }
    }
}
