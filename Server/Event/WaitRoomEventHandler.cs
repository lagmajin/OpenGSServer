using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    internal interface IWaitRoomEventHandler
    {
    }

    internal class WaitRoomEventHandler
    {
        private static JObject CreateRoomError(string messageType, string roomId)
        {
            return new JObject
            {
                ["MessageType"] = messageType,
                ["RoomId"] = roomId ?? string.Empty
            };
        }

        private static void BroadcastRoomUpdate(WaitRoom waitRoom, string messageType)
        {
            var updateJson = new JObject
            {
                ["MessageType"] = messageType,
                ["RoomInfo"] = waitRoom.ToJson()
            };

            foreach (var player in waitRoom.AllPlayers())
            {
                var targetSession = LobbyServerManager.Instance.FindSessionByPlayerId(player.Id);
                targetSession?.SendAsyncJsonWithTimeStamp(updateJson);
            }
        }

        public static void ChangePlayerSettting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("PlayerId");
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (dic.TryGetValue("PlayerCharacter", out var playerCharacterToken))
            {
                _ = playerCharacterToken?.ToString();
            }

            if (dic.TryGetValue("EquipInstantItems", out var instantItemToken) &&
                instantItemToken.Type == JTokenType.Array)
            {
                // TODO: core 側の PlayerInfo / WaitRoomPlayerInfo に揃える
            }
        }

        public static void ChangeRoomSetting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = JsonHelper.GetStringOrNull(dic, "RoomId");
            if (string.IsNullOrWhiteSpace(roomId))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.InvalidRoomId, string.Empty));
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            if (dic.TryGetValue("RoomName", out var roomNameToken))
            {
                waitRoom.RoomName = roomNameToken?.ToString() ?? waitRoom.RoomName;
            }

            if (dic.TryGetValue("Capacity", out var capacityToken) &&
                int.TryParse(capacityToken?.ToString(), out var capacity))
            {
                waitRoom.Capacity = Math.Max(1, capacity);
            }

            if (dic.TryGetValue("GameMode", out var gameModeToken))
            {
                var rawMode = gameModeToken?.ToString() ?? string.Empty;
                if (Enum.TryParse<EGameMode>(rawMode, true, out var gameMode))
                {
                    waitRoom.ChangeGameMode(gameMode);
                }
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.RoomSettingChanged,
                ["RoomInfo"] = waitRoom.ToJson()
            });

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void SendUpdateWaitRoom(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            if (string.IsNullOrWhiteSpace(roomId))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.InvalidRoomId, string.Empty));
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.WaitRoomUpdateNotification,
                ["RoomInfo"] = waitRoom.ToJson()
            });
        }

        public static void CloseRoomRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            if (string.IsNullOrWhiteSpace(roomId))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.InvalidRoomId, string.Empty));
                return;
            }

            var removed = WaitRoomManager.Instance().CloseRoom(roomId);
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = removed ? MessageType.RoomDeleted : MessageType.RoomNotFound,
                ["RoomId"] = roomId
            });
        }

        public static void ExitRoomRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            var playerId = dic.GetStringOrNull("PlayerId") ?? dic.GetStringOrNull("PlayerID");

            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.InvalidRoomId, roomId ?? string.Empty));
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            waitRoom.RemovePlayer(playerId);
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.WaitRoomLeave,
                ["RoomId"] = roomId,
                ["PlayerId"] = playerId
            });

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void ReadyRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            var playerId = dic.GetStringOrNull("PlayerId") ?? dic.GetStringOrNull("PlayerID");
            var type = dic.GetStringOrNull("MessageType");

            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
            {
                return;
            }

            lock (waitRoom)
            {
                if (waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    player.IsReady = string.Equals(type, MessageType.WaitRoomPlayerReady, StringComparison.OrdinalIgnoreCase);
                    BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
                }
            }
        }
    }
}
