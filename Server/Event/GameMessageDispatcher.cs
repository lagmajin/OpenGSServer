using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public interface IGameMessageSender
    {
        void SendToPlayer(string playerId, JObject message);
        void BroadcastToRoom(string roomId, JObject message);
        void BroadcastToAll(JObject message);
    }

    public class GameMessageDispatcher
    {
        private static IGameMessageSender? messageSender;

        public static void Initialize(IGameMessageSender sender)
        {
            messageSender = sender;
        }

        public static void SendToPlayer(string playerId, JObject message)
        {
            messageSender?.SendToPlayer(playerId, message);
        }

        public static void BroadcastToRoom(string roomId, JObject message)
        {
            messageSender?.BroadcastToRoom(roomId, message);
        }

        public static void BroadcastToAll(JObject message)
        {
            messageSender?.BroadcastToAll(message);
        }

        // ゲームイベント用の便利メソッド
        public static void SendPlayerKilled(string roomId, string killerId, string killedPlayerId)
        {
            var message = new JObject
            {
                ["MessageType"] = "PlayerKilled",
                ["RoomID"] = roomId,
                ["KillerID"] = killerId,
                ["KilledPlayerID"] = killedPlayerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendFlagCaptured(string roomId, string capturingTeam)
        {
            var message = new JObject
            {
                ["MessageType"] = "FlagCaptured",
                ["RoomID"] = roomId,
                ["CapturingTeam"] = capturingTeam,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendMatchStatus(string roomId, string status)
        {
            var message = new JObject
            {
                ["MessageType"] = "MatchStatus",
                ["RoomID"] = roomId,
                ["Status"] = status,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendPlayerRespawn(string roomId, string playerId, JObject spawnPosition)
        {
            var message = new JObject
            {
                ["MessageType"] = "PlayerRespawn",
                ["RoomID"] = roomId,
                ["PlayerID"] = playerId,
                ["SpawnPosition"] = spawnPosition,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendMatchEnd(string roomId, List<string> winners)
        {
            var message = new JObject
            {
                ["MessageType"] = "MatchEnd",
                ["RoomID"] = roomId,
                ["Winners"] = JArray.FromObject(winners),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }
    }
}