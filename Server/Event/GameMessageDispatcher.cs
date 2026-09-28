using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;

#nullable enable

namespace OpenGSServer
{
    public static class GameMessageTypes
    {
        // A kill the client asserts about itself, and a kill the server ruled.
        // The ruling is named here next to the claim because the two used to
        // share a name, which meant a client could not tell the server's answer
        // from its own request.
        public const string PlayerKill = "PlayerKill";
        public const string PlayerKilled = MessageType.PlayerKilled;
        public const string PlayerShot = "PlayerShot";
        public const string GrenadeThrow = "GrenadeThrow";
        public const string ObjectSpawned = "ObjectSpawned";
        public const string ObjectDestroyed = "ObjectDestroyed";
        public const string FlagCaptured = "FlagCaptured";
        public const string FlagLost = "FlagLost";
        public const string FlagPickup = "FlagPickup";
        public const string FlagReturn = "FlagReturn";
        public const string FlagScoreUpdate = "FlagScoreUpdate";

        // A flag being destroyed. Only the server says this, because a flag going
        // is a rule outcome. A client that could assert it could destroy a flag
        // that was sitting safely on its stand.
        public const string FlagBurst = MessageType.FlagBurst;

        // A delivery that did not score. Only the server says this, and only to
        // the player whose delivery it was.
        public const string FlagCaptureRefused = MessageType.FlagCaptureRefused;
        public const string MatchStatus = MessageType.MatchStatus;
        public const string MatchStatusRequest = MessageType.MatchStatusRequest;
        public const string PlayerRespawn = "PlayerRespawn";
        public const string PlayerPose = "PlayerPose";
        public const string LoadingFinished = "LoadingFinished";
        public const string LoadingStarted = "LoadingStarted";
        public const string LoadingProgress = "LoadingProgress";
        public const string LoadingCompleted = "LoadingCompleted";
        public const string MatchEnd = "MatchEnd";
        // The pickup ruling, and a field item appearing and disappearing. All
        // three have a name in the shared contract so the server cannot answer
        // under a label the client does not read. Each used to carry its own copy
        // of the string here, and each copy was a name the client was missing.
        public const string FieldItemPickup = MessageType.FieldItemPickup;
        public const string FieldItemSpawn = MessageType.FieldItemSpawn;
        public const string FieldItemDespawn = MessageType.FieldItemDespawn;

        // A claim about a weapon on the ground, and the ruling on that claim.
        // The client is the only side that tells them apart, so it says one
        // thing and the server says the other: without the ruling a client that
        // was reaching for a weapon never heard whether it had it, and a client
        // standing next to one never heard that somebody else did.
        public const string WeaponReserved = MessageType.WeaponReserved;
        public const string WeaponReleased = MessageType.WeaponReleased;
        public const string WeaponPickup = "WeaponPickup";
        public const string WeaponDropped = MessageType.WeaponDropped;

        // Spending an instant item, and the server's answer. The answer is what
        // the client's own claim turns out to have been worth, so it carries
        // the health the server decided rather than the one the client asked
        // for.
        public const string ItemUsed = MessageType.ItemUsed;
        public const string ItemUseRefused = MessageType.ItemUseRefused;

        // The health ruling. The handler used to write this as a literal, and the
        // literal was not the name the client dispatched on, so a client was
        // never told what its health was.
        public const string PlayerDamaged = MessageType.PlayerDamaged;
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
            SendFlagReturned(roomId, team, playerId, EFlagReturnReason.FriendlyRecovered.ToString());
        }

        /// <summary>
        /// Says a flag is back on its own stand, and why.
        /// <para>
        /// The reason travels with it because a return is not one thing. A flag
        /// that timed out on the ground and a flag a team deliberately carried
        /// home are both home, but only one of them cost somebody the match, and
        /// without the reason a client cannot tell a flag that came back by
        /// itself from one a player brought back.
        /// </para>
        /// </summary>
        public static void SendFlagReturned(string roomId, string team, string? playerId, string reason)
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
                ["ReturnReason"] = reason,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        /// <summary>
        /// Tells a player their delivery did not score, and why.
        /// <para>
        /// A refused delivery used to be logged and dropped. The player had walked
        /// the flag to the enemy stand and got nothing back, which is
        /// indistinguishable from the message being lost. This is the one flag
        /// ruling that is about one player rather than the room, because it is that
        /// player's own delivery that did not stand.
        /// </para>
        /// </summary>
        public static void SendFlagCaptureRefused(
            string playerId,
            string roomId,
            string team,
            string reason)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.FlagCaptureRefused,
                ["RoomID"] = roomId,
                ["RoomId"] = roomId,
                ["Team"] = team,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["Reason"] = reason,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            SendToPlayer(playerId, message);
        }

        public static void SendFlagScoreUpdate(string roomId, int redTeamScore, int blueTeamScore)        {
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

        /// <summary>
        /// Says whose flag was destroyed.
        /// <para>
        /// A client used to send this about itself and to wait for the same name
        /// back, so a flag the server destroyed was a flag nobody heard about. It
        /// is a rule outcome rather than something a client can assert, so the
        /// server decides it and the whole room is told, because a flag going is
        /// something every player in the match can see.
        /// </para>
        /// </summary>
        public static void SendFlagBurst(string roomId, string team)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.FlagBurst,
                ["RoomID"] = roomId,
                ["RoomId"] = roomId,
                ["Team"] = team,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            BroadcastToRoom(roomId, message);
        }

        /// <summary>
        /// Answers a request for the match status to the player that asked.
        /// <para>
        /// This went to the whole room before, so one player's question was
        /// answered into every other player's screen, and the label it was sent
        /// under had no reader on the client at all. The room id, whether the
        /// match is running and the player count travel with it, so a client
        /// showing a status has the facts rather than a sentence.
        /// </para>
        /// </summary>
        public static void SendMatchStatus(
            string playerId,
            string roomId,
            string status,
            bool playing,
            int playerCount)
        {
            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.MatchStatus,
                ["RoomID"] = roomId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["Status"] = status,
                ["IsPlaying"] = playing,
                ["PlayerCount"] = playerCount,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            SendToPlayer(playerId, message);
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
