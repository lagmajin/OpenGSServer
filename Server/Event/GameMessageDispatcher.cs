using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;

#nullable enable

namespace OpenGSServer
{
    public static class GameMessageTypes
    {
        public const string PlayerKilled = "PlayerKilled";
        public const string PlayerShot = "PlayerShot";
        public const string GrenadeThrow = "GrenadeThrow";
        public const string ObjectSpawned = "ObjectSpawned";
        public const string ObjectDestroyed = "ObjectDestroyed";
        public const string FlagCaptured = "FlagCaptured";
        public const string FlagLost = "FlagLost";
        public const string FlagPickup = "FlagPickup";
        public const string FlagReturn = "FlagReturn";
        public const string FlagScoreUpdate = "FlagScoreUpdate";
        public const string MatchStatus = "MatchStatus";
        public const string MatchStatusRequest = "MatchStatusRequest";
        public const string PlayerRespawn = "PlayerRespawn";
        public const string PlayerPose = "PlayerPose";
        public const string LoadingFinished = "LoadingFinished";
        public const string LoadingStarted = "LoadingStarted";
        public const string LoadingProgress = "LoadingProgress";
        public const string LoadingCompleted = "LoadingCompleted";
        public const string MatchEnd = "MatchEnd";
    }

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

        // Shared helpers for broadcasting match events.
        public static void SendPlayerKilled(string roomId, string killerId, string killedPlayerId)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerKilled,
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
                ["MessageType"] = GameMessageTypes.FlagCaptured,
                ["RoomID"] = roomId,
                ["RoomId"] = roomId,
                ["CapturingTeam"] = capturingTeam,
                ["Team"] = capturingTeam,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendFlagLost(string roomId, string team, string? playerId = null)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.FlagLost,
                ["RoomID"] = roomId,
                ["RoomId"] = roomId,
                ["Team"] = team,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendFlagPickup(string roomId, string team, string? playerId = null)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.FlagPickup,
                ["RoomID"] = roomId,
                ["RoomId"] = roomId,
                ["Team"] = team,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendFlagReturn(string roomId, string team, string? playerId = null)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.FlagReturn,
                ["RoomID"] = roomId,
                ["RoomId"] = roomId,
                ["Team"] = team,
                ["ReturnedByPlayerId"] = playerId,
                ["ReturnedByPlayerID"] = playerId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendFlagScoreUpdate(string roomId, int redTeamScore, int blueTeamScore)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.FlagScoreUpdate,
                ["RoomID"] = roomId,
                ["RoomId"] = roomId,
                ["RedTeamScore"] = redTeamScore,
                ["BlueTeamScore"] = blueTeamScore,
                ["RedTeamFlagScore"] = redTeamScore,
                ["BlueTeamFlagScore"] = blueTeamScore,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendMatchStatus(string roomId, string status)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.MatchStatus,
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
                ["MessageType"] = GameMessageTypes.PlayerRespawn,
                ["RoomID"] = roomId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["SpawnPosition"] = spawnPosition,
                ["PosX"] = spawnPosition.GetValue("X") ?? spawnPosition.GetValue("x") ?? 0f,
                ["PosY"] = spawnPosition.GetValue("Y") ?? spawnPosition.GetValue("y") ?? 0f,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendPlayerPose(string roomId, string playerId, string poseState)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerPose,
                ["RoomID"] = roomId,
                ["RoomId"] = roomId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["PoseState"] = poseState,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        public static void SendMatchEnd(string roomId, List<string> winners)
        {
            var winnerArray = new JArray();
            foreach (var winner in winners)
            {
                winnerArray.Add(winner);
            }

            // Consumers use the standard match-result notification and can render a
            // single winner directly. Keep the full list for draw/tie-capable modes.
            var primaryWinner = winners.Count > 0 ? winners[0] : "Draw";

            var message = new JObject
            {
                ["MessageType"] = MessageType.MatchEndNotification,
                ["RoomID"] = roomId,
                ["Winner"] = primaryWinner,
                ["Winners"] = winnerArray,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }
    }
}
