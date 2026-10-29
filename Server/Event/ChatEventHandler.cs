using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;

#nullable enable

namespace OpenGSServer
{
    internal class ChatEventHandler
    {
        public static void ProcessOneOnOneChat(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var senderId = dic.GetStringOrNull("AccountID") ?? dic.GetStringOrNull("PlayerID") ?? dic.GetStringOrNull("PlayerId");
            var targetId = dic.GetStringOrNull("TargetAccountID") ?? dic.GetStringOrNull("TargetPlayerID");
            var message = dic.GetStringOrNull("Message");

            if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(message))
                return;

            var targetSession = LobbyServerManager.Instance.FindSessionByPlayerId(targetId);
            if (targetSession == null)
                return;

            targetSession.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = "OneOnOneChat",
                ["FromPlayerID"] = senderId,
                ["Message"] = message,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        public static void ProcessLobbyChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("AccountID") ?? dic.GetStringOrNull("PlayerID") ?? dic.GetStringOrNull("PlayerId");
            var playerName = dic.GetStringOrNull("PlayerName") ?? "Player";
            var message = dic.GetStringOrNull("Message");

            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(message))
                return;

            LobbyServerManager.Instance.AddLobbyChat(playerId, message);

            var notifyJson = new JObject
            {
                ["MessageType"] = MessageType.LobbyChatNotification,
                ["PlayerID"] = playerId,
                ["PlayerName"] = playerName,
                ["Message"] = message
            };

            foreach (var kvp in LobbyServerManager.Instance.GetAllConnectedPlayers())
            {
                var ps = LobbyServerManager.Instance.FindSessionByPlayerId(kvp.Key);
                ps?.SendAsyncJsonWithTimeStamp(notifyJson);
            }
        }

        public static void ProcessWaitRoomChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("AccountID") ?? dic.GetStringOrNull("PlayerID") ?? dic.GetStringOrNull("PlayerId");
            var playerName = dic.GetStringOrNull("PlayerName") ?? "Player";
            var roomId = dic.GetStringOrNull("RoomID") ?? dic.GetStringOrNull("RoomId");
            var message = dic.GetStringOrNull("Message");

            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(message))
                return;

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
                return;

            var notifyJson = new JObject
            {
                ["MessageType"] = "WaitRoomChatNotification",
                ["PlayerID"] = playerId,
                ["PlayerName"] = playerName,
                ["RoomID"] = roomId,
                ["Message"] = message
            };

            foreach (var player in waitRoom.AllPlayers())
            {
                var targetSession = LobbyServerManager.Instance.FindSessionByPlayerId(player.Id);
                targetSession?.SendAsyncJsonWithTimeStamp(notifyJson);
            }
        }

        public static void ProcessMatchRoomChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("AccountID") ?? dic.GetStringOrNull("PlayerID") ?? dic.GetStringOrNull("PlayerId");
            var playerName = dic.GetStringOrNull("PlayerName") ?? "Player";
            var message = dic.GetStringOrNull("Message");

            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(message))
                return;

            var notifyJson = new JObject
            {
                ["MessageType"] = "MatchRoomChatNotification",
                ["PlayerID"] = playerId,
                ["PlayerName"] = playerName,
                ["Message"] = message
            };

            // Broadcast to all players in the same match room
            foreach (var kvp in LobbyServerManager.Instance.GetAllConnectedPlayers())
            {
                var ps = LobbyServerManager.Instance.FindSessionByPlayerId(kvp.Key);
                ps?.SendAsyncJsonWithTimeStamp(notifyJson);
            }
        }
    }
}
