using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using OpenGSCore;


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
        PlayerRespawn
    }

    public class IInGameMatchRoomHandler
    {

    }
    internal class InGameMatchEventHandler:IInGameMatchRoomHandler
    {
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

                    if (room != null)
                    {
                        ProcessSystemEvent(room, type, json, playerId);
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

                    if (room != null)
                    {
                        ProcessRealtimeGameEvent(room, type, json, playerId, remoteEndPoint);
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
                    HandlePlayerRespawn(room, playerId);
                    break;

                case GameMessageTypes.ObjectSpawned:
                    HandleObjectSpawned(room, playerId, json);
                    break;

                case GameMessageTypes.ObjectDestroyed:
                    HandleObjectDestroyed(room, playerId, json);
                    break;

                default:
                    Console.WriteLine($"[Match] Unknown system event type: {eventType}");
                    break;
            }
        }

        private static void ProcessRealtimeGameEvent(MatchRoom room, string eventType, JObject json, string playerId, string remoteEndPoint)
        {
            switch (eventType)
            {
                case GameMessageTypes.PlayerKilled:
                    var killedPlayerId = json.GetStringOrNull("KilledPlayerID");
                    var killerId = json.GetStringOrNull("KillerID");
                    if (killedPlayerId != null && killerId != null)
                    {
                        HandlePlayerKilled(room, killerId, killedPlayerId);
                    }
                    break;

                case "PlayerDamaged":
                    var damagedPlayerId = json.GetValue("DamagedPlayerID")?.ToString();
                    var damageToken = json.GetValue("Damage");
                    int damage = damageToken != null ? (int)damageToken : 0;
                    if (damagedPlayerId != null)
                    {
                        HandlePlayerDamaged(room, damagedPlayerId, damage);
                    }
                    break;

                case GameMessageTypes.FlagCaptured:
                    var capturingTeam = ReadString(json, "CapturingTeam", "Team");
                    if (capturingTeam != null)
                    {
                        HandleFlagCaptured(room, capturingTeam);
                    }
                    break;

                case GameMessageTypes.FlagLost:
                    HandleFlagLost(room, ReadString(json, "Team", "CapturingTeam"), playerId);
                    break;

                case GameMessageTypes.FlagPickup:
                    HandleFlagPickup(room, ReadString(json, "Team", "CapturingTeam"), playerId);
                    break;

                case GameMessageTypes.FlagReturn:
                    HandleFlagReturn(room, ReadString(json, "Team", "CapturingTeam"), ReadString(json, "ReturnedByPlayerId", "ReturnedByPlayerID", "PlayerID", "PlayerId"));
                    break;

                case GameMessageTypes.FlagScoreUpdate:
                    HandleFlagScoreUpdate(room, json);
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

                default:
                    Console.WriteLine($"Unknown realtime game event type: {eventType}");
                    break;
            }
        }

        private static void HandlePlayerKilled(MatchRoom room, string killerId, string killedPlayerId)
        {
            // キラーにスコア加算（基本実装）
            Console.WriteLine($"Player {killedPlayerId} was killed by {killerId}");

            // 全プレイヤーに通知（基本実装）
            Console.WriteLine($"Kill event: {killerId} killed {killedPlayerId}");
        }

        private static void HandlePlayerDamaged(MatchRoom room, string damagedPlayerId, int damage)
        {
            Console.WriteLine($"Player {damagedPlayerId} took {damage} damage");
        }

        private static void HandleFlagCaptured(MatchRoom room, string capturingTeam)
        {
            Console.WriteLine($"Team {capturingTeam} captured the flag");
            if (string.IsNullOrWhiteSpace(capturingTeam))
            {
                return;
            }

            if (Enum.TryParse<ETeam>(capturingTeam, true, out var team))
            {
                room.AddFlagCapture(team);
            }

            GameMessageDispatcher.SendFlagCaptured(room.Id.ToString(), capturingTeam);
            GameMessageDispatcher.SendFlagScoreUpdate(
                room.Id.ToString(),
                room.GetFlagScore(ETeam.Red),
                room.GetFlagScore(ETeam.Blue));
        }

        private static void HandleFlagLost(MatchRoom room, string team, string playerId)
        {
            Console.WriteLine($"Team {team} lost the flag");
            if (string.IsNullOrWhiteSpace(team))
            {
                return;
            }

            GameMessageDispatcher.SendFlagLost(room.Id.ToString(), team, playerId);
        }

        private static void HandleFlagPickup(MatchRoom room, string team, string playerId)
        {
            Console.WriteLine($"Team {team} picked up the flag");
            if (string.IsNullOrWhiteSpace(team))
            {
                return;
            }

            GameMessageDispatcher.SendFlagPickup(room.Id.ToString(), team, playerId);
        }

        private static void HandleFlagReturn(MatchRoom room, string team, string playerId)
        {
            Console.WriteLine($"Team {team} returned the flag");
            if (string.IsNullOrWhiteSpace(team))
            {
                return;
            }

            GameMessageDispatcher.SendFlagReturn(room.Id.ToString(), team, playerId);
        }

        private static void HandleFlagScoreUpdate(MatchRoom room, JObject json)
        {
            var red = ReadInt(json, "RedTeamScore", "RedTeamFlagScore");
            var blue = ReadInt(json, "BlueTeamScore", "BlueTeamFlagScore");
            var currentRed = room.GetFlagScore(ETeam.Red);
            var currentBlue = room.GetFlagScore(ETeam.Blue);

            if (red == currentRed && blue == currentBlue)
            {
                Console.WriteLine($"FlagScoreUpdate ignored because it matches room state ({red}:{blue})");
                return;
            }

            GameMessageDispatcher.SendFlagScoreUpdate(room.Id.ToString(), red, blue);
        }

        private static void HandlePlayerEliminated(MatchRoom room, string playerId)
        {
            Console.WriteLine($"Player {playerId} was eliminated");
        }

        private static void SendMatchStatus(MatchRoom room, string requestingPlayerId)
        {
            var status = $"Match Active - Players: {room.Players.Count}";
            Console.WriteLine($"Sending status to {requestingPlayerId}: {status}");
        }

        private static void HandlePositionUpdate(MatchRoom room, string playerId, JObject position)
        {
            Console.WriteLine($"Player {playerId} position updated");
        }

        private static void HandlePlayerRespawn(MatchRoom room, string playerId)
        {
            Console.WriteLine($"Player {playerId} respawned");
        }

        private static void HandlePlayerShot(MatchRoom room, string playerId, JObject shotData)
        {
            // 射撃処理 - ヒット判定、ダメージ計算など
            var targetId = shotData.GetStringOrNull("TargetID");
            var weaponType = shotData.GetStringOrNull("WeaponType");
            var hitPosition = shotData.GetValue("HitPosition") as JObject;

            Console.WriteLine($"Player {playerId} shot with {weaponType}");

            if (targetId != null)
            {
                // ヒット判定とダメージ処理
                HandleShotHit(room, playerId, targetId, weaponType, hitPosition);
            }

            // 全プレイヤーに射撃イベントをブロードキャスト（UDP）
            BroadcastShotEvent(room, playerId, shotData);
        }

        private static void HandleGrenadeThrow(MatchRoom room, string playerId, JObject grenadeData)
        {
            var objectType = NormalizeGrenadeObjectType(grenadeData.GetStringOrNull("GrenadeType"));
            var objectId = grenadeData.GetStringOrNull("ObjectId") ?? Guid.NewGuid().ToString("N");
            Console.WriteLine($"Player {playerId} threw {objectType} ({objectId})");
            BroadcastGrenadeEvent(room, playerId, grenadeData);
        }

        private static void HandleObjectSpawned(MatchRoom room, string playerId, JObject objectData)
        {
            var objectType = objectData.GetStringOrNull("ObjectType") ?? "Unknown";
            var objectId = objectData.GetStringOrNull("ObjectId") ?? Guid.NewGuid().ToString("N");
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} spawned {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectSpawned,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["PlayerID"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = objectData.GetValue("PosX")?.ToObject<float>() ?? 0f,
                ["PosY"] = objectData.GetValue("PosY")?.ToObject<float>() ?? 0f,
                ["Rotation"] = objectData.GetValue("Rotation")?.ToObject<float>() ?? 0f,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void HandleObjectDestroyed(MatchRoom room, string playerId, JObject objectData)
        {
            var objectId = objectData.GetStringOrNull("ObjectId") ?? Guid.NewGuid().ToString("N");
            var objectType = objectData.GetStringOrNull("ObjectType") ?? "Unknown";
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} destroyed {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectDestroyed,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["DestroyedBy"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = objectData.GetValue("PosX")?.ToObject<float>() ?? 0f,
                ["PosY"] = objectData.GetValue("PosY")?.ToObject<float>() ?? 0f,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void HandleShotHit(MatchRoom room, string shooterId, string targetId, string? weaponType, JObject? hitPosition)
        {
            // ダメージ計算（武器タイプによる）
            int damage = CalculateWeaponDamage(weaponType);

            // ターゲットにダメージを与える
            HandlePlayerDamaged(room, targetId, damage);

            // 射撃ヒットイベントをブロードキャスト
            BroadcastShotHitEvent(room, shooterId, targetId, damage, hitPosition);
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

        // UDPブロードキャストメソッド群
        private static void BroadcastShotEvent(MatchRoom room, string playerId, JObject shotData)
        {
            var objectId = shotData.GetStringOrNull("ObjectId") ?? "unknown";
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} fired shot ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerShot,
                ["PlayerID"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["ShotData"] = shotData,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            // UDPブロードキャスト（ルーム内の全プレイヤー）
            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void BroadcastGrenadeEvent(MatchRoom room, string playerId, JObject grenadeData)
        {
            var objectType = NormalizeGrenadeObjectType(grenadeData.GetStringOrNull("GrenadeType"));
            var objectId = grenadeData.GetStringOrNull("ObjectId") ?? Guid.NewGuid().ToString("N");
            var direction = grenadeData.GetValue("Direction") as JObject;
            var dirX = direction?.GetValue("X")?.ToObject<float>() ?? direction?.GetValue("x")?.ToObject<float>() ?? 1f;
            var dirY = direction?.GetValue("Y")?.ToObject<float>() ?? direction?.GetValue("y")?.ToObject<float>() ?? 0f;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} threw {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.GrenadeThrow,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["PlayerID"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = grenadeData.GetValue("PosX")?.ToObject<float>() ?? 0f,
                ["PosY"] = grenadeData.GetValue("PosY")?.ToObject<float>() ?? 0f,
                ["Direction"] = new JObject
                {
                    ["X"] = dirX,
                    ["Y"] = dirY
                },
                ["Rotation"] = grenadeData.GetValue("Rotation")?.ToObject<float>() ?? 0f,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
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

        private static void BroadcastShotHitEvent(MatchRoom room, string shooterId, string targetId, int damage, JObject? hitPosition)
        {
            var message = new JObject
            {
                ["MessageType"] = "ShotHit",
                ["ShooterID"] = shooterId,
                ["TargetID"] = targetId,
                ["Damage"] = damage,
                ["HitPosition"] = hitPosition,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void UdpBroadcastToRoom(string roomId, JObject message)
        {
            // UDPブロードキャストの実装
            // MatchRUdpServerManagerなどを通じてルーム内の全プレイヤーに送信
            Console.WriteLine($"UDP Broadcast to room {roomId}: {message["MessageType"]}");
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

        private static int ReadInt(JObject json, params string[] keys)
        {
            var text = ReadString(json, keys);
            if (int.TryParse(text, out var value))
            {
                return value;
            }

            if (json == null || keys == null)
            {
                return 0;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var token = json.GetValue(key);
                if (token == null)
                {
                    continue;
                }

                try
                {
                    return token.ToObject<int>();
                }
                catch
                {
                    // ignore and continue
                }
            }

            return 0;
        }
    }
}
