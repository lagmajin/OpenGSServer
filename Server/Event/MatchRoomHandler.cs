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
        GrenadeThrown,
        ItemUsed,
        FlagCaptured,
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

                var playerId = json.GetStringOrNull("PlayerID");
                var roomId = json.GetStringOrNull("RoomID");

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
                var playerId = json.GetStringOrNull("PlayerID");
                var roomId = json.GetStringOrNull("RoomID");

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
                case "LoadingFinished":
                    room.SetPlayerReady(playerId);
                    break;

                case "MatchStatusRequest":
                    SendMatchStatus(room, playerId);
                    break;

                case "PlayerRespawn":
                    HandlePlayerRespawn(room, playerId);
                    break;

                default:
                    Console.WriteLine($"Unknown system event type: {eventType}");
                    break;
            }
        }

        private static void ProcessRealtimeGameEvent(MatchRoom room, string eventType, JObject json, string playerId, string remoteEndPoint)
        {
            switch (eventType)
            {
                case "PlayerKilled":
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

                case "FlagCaptured":
                    var capturingTeam = json.GetValue("CapturingTeam")?.ToString();
                    if (capturingTeam != null)
                    {
                        HandleFlagCaptured(room, capturingTeam);
                    }
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

                case "PlayerShot":
                    HandlePlayerShot(room, playerId, json);
                    break;

                case "GrenadeThrown":
                    HandleGrenadeThrown(room, playerId, json);
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

        private static void HandleGrenadeThrown(MatchRoom room, string playerId, JObject grenadeData)
        {
            // グレネード投擲処理
            var position = grenadeData.GetValue("Position") as JObject;
            var velocity = grenadeData.GetValue("Velocity") as JObject;

            Console.WriteLine($"Player {playerId} threw grenade");

            // Relay grenade message as-is to room players (clients are authoritative)
            var message = new JObject
            {
                ["MessageType"] = grenadeData.GetStringOrNull("MessageType") ?? "GrenadeSpawn",
                ["PlayerID"] = playerId,
                ["GrenadeData"] = grenadeData,
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
            var message = new JObject
            {
                ["MessageType"] = "PlayerShot",
                ["PlayerID"] = playerId,
                ["ShotData"] = shotData,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            // UDPブロードキャスト（ルーム内の全プレイヤー）
            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void BroadcastGrenadeEvent(MatchRoom room, string playerId, JObject grenadeData)
        {
            var message = grenadeData.DeepClone() as JObject ?? new JObject();
            // Ensure basic routing fields
            message["MessageType"] = message.GetStringOrNull("MessageType") ?? "GrenadeSpawn";
            message["PlayerID"] = playerId;
            message["RoomID"] = room.Id.ToString();
            message["Timestamp"] = DateTime.UtcNow.ToString("o");

            // Broadcast via UDP manager
            var udpManager = new MatchRUdpServerManager();
            udpManager.BroadcastToRoom(room.Id.ToString(), message);
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
    }
}
