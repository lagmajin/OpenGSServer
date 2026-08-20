using System;
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using OpenGSCore;

#nullable enable

namespace OpenGSServer
{
    internal interface IWaitRoomEventHandler
    {
    }

    internal class WaitRoomEventHandler
    {
        private static readonly object LoadingStateLock = new();
        private static readonly Dictionary<string, HashSet<string>> LoadingCompletedPlayers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoadingRooms = new(StringComparer.OrdinalIgnoreCase);

        private static JObject CreateRoomError(string messageType, string roomId)
        {
            return new JObject
            {
                ["MessageType"] = messageType,
                ["RoomId"] = roomId ?? string.Empty
            };
        }

        internal static void BroadcastRoomUpdate(WaitRoom waitRoom, string messageType)
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

        private static void BroadcastLoadingNotification(WaitRoom waitRoom, JObject notification)
        {
            foreach (var player in waitRoom.AllPlayers())
            {
                LobbyServerManager.Instance.FindSessionByPlayerId(player.Id)?.SendAsyncJsonWithTimeStamp(notification);
            }
        }

        private static void BeginLoading(WaitRoom waitRoom)
        {
            lock (LoadingStateLock)
            {
                if (LoadingRooms.Add(waitRoom.RoomId))
                {
                    LoadingCompletedPlayers[waitRoom.RoomId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        private static bool MarkLoadingCompleted(WaitRoom waitRoom, string playerId)
        {
            lock (LoadingStateLock)
            {
                if (!LoadingRooms.Contains(waitRoom.RoomId))
                {
                    LoadingRooms.Add(waitRoom.RoomId);
                    LoadingCompletedPlayers[waitRoom.RoomId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                var completedPlayers = LoadingCompletedPlayers[waitRoom.RoomId];
                completedPlayers.Add(playerId);
                var players = waitRoom.AllPlayers();
                if (players.Count == 0)
                {
                    return false;
                }

                foreach (var player in players)
                {
                    if (!completedPlayers.Contains(player.Id))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static void ClearLoadingState(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            lock (LoadingStateLock)
            {
                LoadingRooms.Remove(roomId);
                LoadingCompletedPlayers.Remove(roomId);
            }
        }

        private static void BroadcastAllowEnterMap(WaitRoom waitRoom)
        {
            var notification = new JObject
            {
                ["MessageType"] = MessageType.AllowEnterMap,
                ["RoomID"] = waitRoom.RoomId,
                ["RoomId"] = waitRoom.RoomId,
                ["Approved"] = true
            };

            BroadcastLoadingNotification(waitRoom, notification);
        }

        private static float ReadLoadingProgress(IDictionary<string, JToken> dic)
        {
            var token = dic.GetStringOrNull("Progress");
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var progress)
                ? Math.Clamp(progress, 0f, 1f)
                : 0f;
        }

        private static JObject BuildRoomInfoJson(WaitRoom waitRoom)
        {
            var snapshot = waitRoom.ToSnapshot().ToJson();
            snapshot["OwnerId"] = waitRoom.GetFirstPlayerId();
            snapshot["OwnerID"] = snapshot["OwnerId"];
            snapshot["HasPassword"] = !string.IsNullOrWhiteSpace(waitRoom.Password);
            return snapshot;
        }

        private static JObject BuildPublicRoomSettingsJson(JObject roomInfoJson)
        {
            var settings = new JObject
            {
                ["RoomName"] = roomInfoJson["RoomName"],
                ["Capacity"] = roomInfoJson["Capacity"],
                ["GameMode"] = roomInfoJson["GameMode"],
                ["TeamBalance"] = roomInfoJson["TeamBalance"],
                ["Map"] = roomInfoJson["Map"],
                ["HasPassword"] = roomInfoJson["HasPassword"],
                ["RoomId"] = roomInfoJson["RoomId"],
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = roomInfoJson["PlayerCount"],
                ["NowPlaying"] = roomInfoJson["NowPlaying"]
            };

            return settings;
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

        private static WaitRoom? FindMissionRoomByPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            foreach (var room in MissionWaitRoomManager.Instance.GetAllMissionRooms())
            {
                if (room.Players.ContainsKey(playerId))
                {
                    return room;
                }
            }

            return null;
        }

        private static bool TryGetSessionPlayerId(ClientSession? session, out string playerId)
        {
            playerId = session?.PlayerID ?? string.Empty;
            return !string.IsNullOrWhiteSpace(playerId);
        }

        public static void RemoveDisconnectedPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            foreach (var waitRoom in WaitRoomManager.Instance().GetAllRooms())
            {
                var previousOwnerId = waitRoom.GetFirstPlayerId();
                if (!waitRoom.TryRemovePlayer(playerId, out _))
                {
                    continue;
                }

                var newOwnerId = waitRoom.GetFirstPlayerId();
                if (waitRoom.AllPlayers().Count == 0)
                {
                    ClearLoadingState(waitRoom.RoomId);
                    WaitRoomManager.Instance().CloseRoom(waitRoom.RoomId);
                    LobbyEventHandler.BroadcastRoomListUpdate();
                    continue;
                }

                if (!string.Equals(previousOwnerId, newOwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    var ownerChange = new JObject
                    {
                        ["MessageType"] = MessageType.WaitRoomOwnerChange,
                        ["RoomID"] = waitRoom.RoomId,
                        ["RoomId"] = waitRoom.RoomId,
                        ["PreviousOwnerId"] = previousOwnerId,
                        ["NewOwnerId"] = newOwnerId,
                        ["OwnerId"] = newOwnerId,
                        ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
                    };

                    foreach (var player in waitRoom.AllPlayers())
                    {
                        LobbyServerManager.Instance.FindSessionByPlayerId(player.Id)?.SendAsyncJsonWithTimeStamp(ownerChange);
                    }
                }

                BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
                LobbyEventHandler.BroadcastRoomListUpdate();
            }

            foreach (var missionRoom in MissionWaitRoomManager.Instance.GetAllMissionRooms())
            {
                if (!missionRoom.TryRemovePlayer(playerId, out _))
                {
                    continue;
                }

                if (missionRoom.AllPlayers().Count == 0)
                {
                    ClearLoadingState(missionRoom.RoomId);
                    MissionWaitRoomManager.Instance.CloseMissionRoom(missionRoom.RoomId);
                }
                else
                {
                    BroadcastRoomUpdate(missionRoom, MessageType.WaitRoomUpdateNotification);
                }

                LobbyEventHandler.BroadcastRoomListUpdate();
            }
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
            if (!TryGetSessionPlayerId(session, out var playerId))
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

            foreach (var missionRoom in MissionWaitRoomManager.Instance.GetAllMissionRooms())
            {
                if (!missionRoom.TryRemovePlayer(playerId, out _))
                {
                    continue;
                }

                if (missionRoom.AllPlayers().Count == 0)
                {
                    MissionWaitRoomManager.Instance.CloseMissionRoom(missionRoom.RoomId);
                }
                else
                {
                    BroadcastRoomUpdate(missionRoom, MessageType.WaitRoomUpdateNotification);
                }

                LobbyEventHandler.BroadcastRoomListUpdate();
            }
        }

        public static void LoadingStartedRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId))
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

            BeginLoading(waitRoom);

            BroadcastLoadingNotification(waitRoom, new JObject
            {
                ["MessageType"] = MessageType.LoadingStartedNotification,
                ["PlayerID"] = playerId,
                ["Progress"] = 0f,
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
            });

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void LoadingCompletedRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId))
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

            var allPlayersCompletedLoading = MarkLoadingCompleted(waitRoom, playerId);

            BroadcastLoadingNotification(waitRoom, new JObject
            {
                ["MessageType"] = MessageType.LoadingCompletedNotification,
                ["PlayerID"] = playerId,
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
            });

            if (allPlayersCompletedLoading)
            {
                BroadcastAllowEnterMap(waitRoom);
            }

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
            LobbyEventHandler.BroadcastRoomListUpdate();
        }

        public static void LoadingProgressRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId) ||
                !TryResolveWaitRoom(dic, playerId, out var waitRoom) || waitRoom == null)
            {
                return;
            }

            BroadcastLoadingNotification(waitRoom, new JObject
            {
                ["MessageType"] = MessageType.LoadingProgressNotification,
                ["PlayerID"] = playerId,
                ["Progress"] = ReadLoadingProgress(dic)
            });
        }

        public static void ChangeRoomSetting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (session is null)
            {
                return;
            }

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
            waitRoom ??= MissionWaitRoomManager.Instance.FindMissionRoom(roomId);
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            if (!string.Equals(waitRoom.GetFirstPlayerId(), session.PlayerID, StringComparison.OrdinalIgnoreCase))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.RoomSettingChanged,
                    ["RoomId"] = roomId,
                    ["RoomID"] = roomId,
                    ["Success"] = false,
                    ["Error"] = "Only the room owner can change settings",
                    ["ErrorMessage"] = "Only the room owner can change settings"
                });
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
                ["Settings"] = BuildPublicRoomSettingsJson(roomInfoJson),
                ["RoomName"] = roomInfoJson["RoomName"],
                ["Capacity"] = roomInfoJson["Capacity"],
                ["GameMode"] = roomInfoJson["GameMode"],
                ["Map"] = roomInfoJson["Map"],
                ["TeamBalance"] = roomInfoJson["TeamBalance"],
                ["HasPassword"] = roomInfoJson["HasPassword"],
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
            if (removed)
            {
                ClearLoadingState(roomId);
                LobbyEventHandler.BroadcastRoomListUpdate();
            }
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = removed ? MessageType.RoomDeleted : MessageType.RoomNotFound,
                ["RoomId"] = roomId
            });
        }

        public static void ExitRoomRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId))
            {
                return;
            }

            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");

            var waitRoom = string.IsNullOrWhiteSpace(roomId)
                ? FindWaitRoomByPlayerId(playerId)
                : WaitRoomManager.Instance().FindWaitRoom(roomId);
            var isMissionRoom = false;
            if (waitRoom == null)
            {
                waitRoom = string.IsNullOrWhiteSpace(roomId)
                    ? FindMissionRoomByPlayerId(playerId)
                    : MissionWaitRoomManager.Instance.FindMissionRoom(roomId);
                isMissionRoom = waitRoom != null;
            }
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            roomId = waitRoom.RoomId;
            var previousOwnerId = waitRoom.GetFirstPlayerId();
            if (!waitRoom.TryRemovePlayer(playerId, out _))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.LeaveRoomResponse,
                ["RoomId"] = roomId,
                ["RoomID"] = roomId,
                ["PlayerId"] = playerId,
                ["PlayerID"] = playerId,
                ["Success"] = true
            });

            if (waitRoom.AllPlayers().Count == 0)
            {
                ClearLoadingState(roomId);
                if (isMissionRoom)
                {
                    MissionWaitRoomManager.Instance.CloseMissionRoom(roomId);
                }
                else
                {
                    WaitRoomManager.Instance().CloseRoom(roomId);
                }
                LobbyEventHandler.BroadcastRoomListUpdate();
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.RoomDeleted,
                    ["RoomId"] = roomId,
                    ["RoomID"] = roomId
                });
                return;
            }

            var newOwnerId = waitRoom.GetFirstPlayerId();
            if (string.Equals(previousOwnerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(previousOwnerId, newOwnerId, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(newOwnerId))
            {
                var ownerChange = new JObject
                {
                    ["MessageType"] = MessageType.WaitRoomOwnerChange,
                    ["RoomId"] = roomId,
                    ["RoomID"] = roomId,
                    ["PreviousOwnerId"] = previousOwnerId,
                    ["NewOwnerId"] = newOwnerId,
                    ["OwnerId"] = newOwnerId,
                    ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
                };

                foreach (var player in waitRoom.AllPlayers())
                {
                    LobbyServerManager.Instance.FindSessionByPlayerId(player.Id)?.SendAsyncJsonWithTimeStamp(ownerChange);
                }
            }

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void ReadyRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            if (!TryGetSessionPlayerId(session, out var playerId))
            {
                return;
            }
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

        public static void KickPlayerRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            var targetPlayerId = dic.GetStringOrNull("PlayerId") ?? dic.GetStringOrNull("PlayerID");
            var reason = dic.GetStringOrNull("Reason") ?? "Kicked by room owner";
            var room = WaitRoomManager.Instance().FindWaitRoom(roomId ?? string.Empty);

            if (room == null || string.IsNullOrWhiteSpace(targetPlayerId))
            {
                return;
            }

            if (!string.Equals(room.GetFirstPlayerId(), session?.PlayerID, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(room.GetFirstPlayerId(), targetPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!room.TryRemovePlayer(targetPlayerId, out _))
            {
                return;
            }

            var kickNotification = new JObject
            {
                ["MessageType"] = MessageType.WaitRoomKickPlayer,
                ["Success"] = true,
                ["PlayerID"] = targetPlayerId,
                ["RoomID"] = room.RoomId,
                ["Reason"] = reason
            };
            LobbyServerManager.Instance.FindSessionByPlayerId(targetPlayerId)?.SendAsyncJsonWithTimeStamp(kickNotification);
            BroadcastRoomUpdate(room, MessageType.WaitRoomUpdateNotification);
            LobbyEventHandler.BroadcastRoomListUpdate();
        }
    }
}
