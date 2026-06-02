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
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
            };

            foreach (var player in waitRoom.AllPlayers())
            {
                var targetSession = LobbyServerManager.Instance.FindSessionByPlayerId(player.Id);
                targetSession?.SendAsyncJsonWithTimeStamp(updateJson);
            }
        }

        private static JObject BuildRoomInfoJson(WaitRoom waitRoom)
        {
            var snapshot = waitRoom.ToSnapshot().ToJson();
            snapshot["OwnerId"] = waitRoom.GetFirstPlayerId();
            snapshot["HasPassword"] = !string.IsNullOrWhiteSpace(waitRoom.Password);
            return snapshot;
        }

        private static WaitRoom? FindWaitRoomByPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            foreach (var room in WaitRoomManager.Instance().GetAllRooms())
            {
                if (room.Players.ContainsKey(playerId))
                {
                    return room;
                }
            }

            return null;
        }

        private static bool TryResolveWaitRoom(IDictionary<string, JToken> dic, string playerId, out WaitRoom? waitRoom)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            waitRoom = null;

            if (!string.IsNullOrWhiteSpace(roomId))
            {
                waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            }

            if (waitRoom != null && !string.IsNullOrWhiteSpace(playerId) && !waitRoom.Players.ContainsKey(playerId))
            {
                waitRoom = null;
            }

            if (waitRoom == null)
            {
                waitRoom = FindWaitRoomByPlayerId(playerId);
            }

            return waitRoom != null;
        }

        private static bool TryParsePlayerCharacter(JToken token, out EPlayerCharacter playerCharacter)
        {
            playerCharacter = default;
            if (token == null)
            {
                return false;
            }

            var raw = token.ToString();
            if (Enum.TryParse(raw, true, out EPlayerCharacter parsed))
            {
                playerCharacter = parsed;
                return true;
            }

            if (int.TryParse(raw, out var numeric) && Enum.IsDefined(typeof(EPlayerCharacter), numeric))
            {
                playerCharacter = (EPlayerCharacter)numeric;
                return true;
            }

            return false;
        }

        private static List<EInstantItemType> ParseInstantItems(JToken token)
        {
            var instantItems = new List<EInstantItemType>();

            if (token?.Type != JTokenType.Array)
            {
                return instantItems;
            }

            foreach (var itemToken in token.Children())
            {
                if (itemToken == null)
                {
                    continue;
                }

                var raw = itemToken.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                if (Enum.TryParse(raw, true, out EInstantItemType parsed))
                {
                    instantItems.Add(parsed);
                    continue;
                }

                if (int.TryParse(raw, out var numeric) &&
                    Enum.IsDefined(typeof(EInstantItemType), numeric))
                {
                    instantItems.Add((EInstantItemType)numeric);
                }
            }

            return instantItems;
        }

        public static void ChangePlayerSettting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("PlayerId");
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (!TryResolveWaitRoom(dic, playerId, out var waitRoom) || waitRoom == null)
            {
                return;
            }

            bool changed = false;
            lock (waitRoom)
            {
                if (!waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    return;
                }

                if (dic.TryGetValue("PlayerCharacter", out var playerCharacterToken) &&
                    TryParsePlayerCharacter(playerCharacterToken, out var playerCharacter))
                {
                    player.playerCharacter = playerCharacter;
                    changed = true;
                }

                if (dic.TryGetValue("EquipInstantItems", out var instantItemToken))
                {
                    player.EquipInstantItems = ParseInstantItems(instantItemToken);
                    changed = true;
                }
            }

            if (changed)
            {
                BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
            }
        }

        public static void LoadingStartedRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("PlayerId") ?? dic.GetStringOrNull("PlayerID");
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (!TryResolveWaitRoom(dic, playerId, out var waitRoom) || waitRoom == null)
            {
                return;
            }

            bool changed = false;
            lock (waitRoom)
            {
                if (waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    player.IsReady = false;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.LoadingStartedNotification,
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
            });

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void LoadingCompletedRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("PlayerId") ?? dic.GetStringOrNull("PlayerID");
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (!TryResolveWaitRoom(dic, playerId, out var waitRoom) || waitRoom == null)
            {
                return;
            }

            bool changed = false;
            lock (waitRoom)
            {
                if (waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    player.IsReady = true;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.LoadingCompletedNotification,
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
            });

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void ChangeRoomSetting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomSetting = WaitRoomSetting.FromDictionary(dic);
            var roomId = !string.IsNullOrWhiteSpace(roomSetting.RoomId)
                ? roomSetting.RoomId
                : JsonHelper.GetStringOrNull(dic, "RoomId");

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

            roomSetting.ApplyTo(waitRoom);

            var roomInfoJson = BuildRoomInfoJson(waitRoom);
            var responseJson = new JObject
            {
                ["MessageType"] = MessageType.RoomSettingChanged,
                ["RoomId"] = roomId,
                ["RoomID"] = roomId,
                ["RoomInfo"] = roomInfoJson,
                ["Settings"] = roomSetting.ToJson(),
                ["RoomName"] = roomInfoJson["RoomName"],
                ["Capacity"] = roomInfoJson["Capacity"],
                ["GameMode"] = roomInfoJson["GameMode"],
                ["TeamBalance"] = roomInfoJson["TeamBalance"],
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = roomInfoJson["PlayerCount"],
                ["NowPlaying"] = roomInfoJson["NowPlaying"]
            };

            session.SendAsyncJsonWithTimeStamp(responseJson);

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
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
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
