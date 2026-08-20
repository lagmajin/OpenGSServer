using System;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;

#nullable enable

namespace OpenGSServer
{
    public static class LobbyEventHandler
    {
        private static WaitRoom? FindWaitRoomForPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            foreach (var room in WaitRoomManager.Instance().GetAllRooms())
            {
                if (room.ContainsPlayer(playerId))
                {
                    return room;
                }
            }

            return null;
        }

        private static WaitRoom? FindMissionRoomForPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            foreach (var room in MissionWaitRoomManager.Instance.GetAllMissionRooms())
            {
                if (room.ContainsPlayer(playerId))
                {
                    return room;
                }
            }

            return null;
        }

        public static void CreateNewWaitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            if (session is null)
            {
                return;
            }

            // The authenticated TCP session is authoritative; request IDs are
            // display/protocol data and must not select another account.
            var playerId = session.PlayerID;
            var playerName = dic.GetStringOrNull("PlayerName") ?? "Host";

            if (string.IsNullOrWhiteSpace(playerId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.CreateRoomResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = "PlayerID is required"
                });
                return;
            }

            var existingRoom = FindWaitRoomForPlayer(playerId) ?? FindMissionRoomForPlayer(playerId);
            if (existingRoom != null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.CreateRoomResponse,
                    ["Success"] = false,
                    ["RoomID"] = existingRoom.RoomId,
                    ["RoomId"] = existingRoom.RoomId,
                    ["ErrorMessage"] = "Player is already in a wait room"
                });
                return;
            }

            var roomSetting = WaitRoomSetting.FromDictionary(dic);
            if (string.IsNullOrWhiteSpace(roomSetting.RoomName))
            {
                roomSetting.RoomName = dic.GetStringOrNull("RoomName") ?? Template.RandomRoomName();
            }

            var roomResult = WaitRoomManager.Instance().CreateNewWaitRoom(roomSetting);
            var room = roomResult.Room;
            if (room == null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.CreateRoomResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = roomResult.Message ?? "Failed to create room"
                });
                return;
            }

            room.AddPlayer(playerId, playerName);

            var roomInfoJson = room.ToSnapshot().ToJson();
            roomInfoJson["OwnerId"] = room.GetFirstPlayerId();
            roomInfoJson["OwnerID"] = roomInfoJson["OwnerId"];
            roomInfoJson["HasPassword"] = !string.IsNullOrWhiteSpace(room.Password);

            var json = new JObject
            {
                ["MessageType"] = MessageType.CreateRoomResponse,
                ["Success"] = true,
                ["RoomInfo"] = roomInfoJson,
                ["RoomId"] = room.RoomId,
                ["RoomID"] = room.RoomId,
                ["RoomName"] = room.RoomName,
                ["Capacity"] = room.Capacity,
                ["GameMode"] = room.GameMode.ToString(),
                ["Map"] = room.Map.ToString(),
                ["TeamBalance"] = room.setting is AbstractTeamMatchSetting teamSetting && teamSetting.TeamBalance,
                ["HasPassword"] = roomInfoJson["HasPassword"],
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = room.PlayerCount,
                ["NowPlaying"] = room.NowPlaying
            };

            session.SendAsyncJsonWithTimeStamp(json);
            BroadcastRoomListUpdate();
        }

        public static void CreateNewMissionRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var playerId = session?.PlayerID;
            var playerName = dic.GetStringOrNull("PlayerName") ?? "Host";
            var roomName = dic.GetStringOrNull("RoomName") ?? string.Empty;
            var missionId = dic.GetStringOrNull("MissionId") ?? dic.GetStringOrNull("MissionID") ?? dic.GetStringOrNull("MissionType") ?? "Default";
            var capacity = dic.GetValueDefaultInt("Capacity", 4);

            if (string.IsNullOrWhiteSpace(playerId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.CreateRoomResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = "PlayerID is required"
                });
                return;
            }

            var existingRoom = FindWaitRoomForPlayer(playerId) ?? FindMissionRoomForPlayer(playerId);
            if (existingRoom != null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.CreateRoomResponse,
                    ["Success"] = false,
                    ["RoomID"] = existingRoom.RoomId,
                    ["RoomId"] = existingRoom.RoomId,
                    ["ErrorMessage"] = "Player is already in a wait room"
                });
                return;
            }

            var missionRoomManager = MissionWaitRoomManager.Instance;
            var result = missionRoomManager.CreateMissionRoom(roomName, missionId, capacity);
            var room = result.Room;
            if (room == null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.CreateRoomResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = result.Message ?? "Failed to create mission room"
                });
                return;
            }

            room.AddPlayer(playerId, playerName);

            var roomInfoJson = room.ToSnapshot().ToJson();
            roomInfoJson["OwnerId"] = room.GetFirstPlayerId();
            roomInfoJson["OwnerID"] = roomInfoJson["OwnerId"];
            roomInfoJson["HasPassword"] = !string.IsNullOrWhiteSpace(room.Password);

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.CreateRoomResponse,
                ["Success"] = true,
                ["IsMission"] = true,
                ["MissionId"] = missionId,
                ["RoomInfo"] = roomInfoJson,
                ["RoomId"] = room.RoomId,
                ["RoomID"] = room.RoomId,
                ["RoomName"] = room.RoomName,
                ["Capacity"] = room.Capacity,
                ["GameMode"] = room.GameMode.ToString(),
                ["Map"] = room.Map.ToString(),
                ["TeamBalance"] = room.setting is AbstractTeamMatchSetting teamSetting && teamSetting.TeamBalance,
                ["HasPassword"] = roomInfoJson["HasPassword"],
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = room.PlayerCount,
                ["NowPlaying"] = room.NowPlaying
            });
            BroadcastRoomListUpdate();
        }

        public static void QuickStartRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            _ = session;
            _ = dic;
        }

        public static void EnterRoomRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomID") ?? dic.GetStringOrNull("RoomId");
            var playerId = session?.PlayerID;
            var playerName = dic.GetStringOrNull("PlayerName") ?? "Player";
            var password = dic.GetStringOrNull("Password") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.JoinRoomResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = "RoomID and PlayerID are required"
                });
                return;
            }

            var existingRoom = FindWaitRoomForPlayer(playerId) ?? FindMissionRoomForPlayer(playerId);
            if (existingRoom != null && !string.Equals(existingRoom.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.JoinRoomResponse,
                    ["Success"] = false,
                    ["RoomID"] = roomId,
                    ["RoomId"] = roomId,
                    ["ErrorMessage"] = "Player is already in another wait room",
                    ["CurrentRoomID"] = existingRoom.RoomId,
                    ["CurrentRoomId"] = existingRoom.RoomId
                });
                return;
            }

            var roomManager = WaitRoomManager.Instance();
            var room = roomManager.FindWaitRoom(roomId);
            if (room == null)
            {
                room = MissionWaitRoomManager.Instance.FindMissionRoom(roomId);
                if (room == null)
                {
                    session.SendAsyncJsonWithTimeStamp(new JObject
                    {
                        ["MessageType"] = MessageType.JoinRoomResponse,
                        ["Success"] = false,
                        ["ErrorMessage"] = "Room not found",
                        ["RoomID"] = roomId
                    });
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(room.Password) && !string.Equals(room.Password, password, StringComparison.Ordinal))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.JoinRoomResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = "Incorrect password",
                    ["RoomID"] = roomId
                });
                return;
            }

            if (!room.HasSpace() && !room.ContainsPlayer(playerId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.JoinRoomResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = "Room is full",
                    ["RoomID"] = roomId
                });
                return;
            }

            room.AddPlayer(playerId, playerName);

            var roomInfoJson = room.ToSnapshot().ToJson();
            roomInfoJson["OwnerId"] = room.GetFirstPlayerId();
            roomInfoJson["OwnerID"] = roomInfoJson["OwnerId"];
            roomInfoJson["HasPassword"] = !string.IsNullOrWhiteSpace(room.Password);

            var response = new JObject
            {
                ["MessageType"] = MessageType.JoinRoomResponse,
                ["Success"] = true,
                ["IsMission"] = MissionWaitRoomManager.Instance.FindMissionRoom(room.RoomId) != null,
                ["RoomInfo"] = roomInfoJson,
                ["RoomId"] = room.RoomId,
                ["RoomID"] = room.RoomId,
                ["RoomName"] = room.RoomName,
                ["Capacity"] = room.Capacity,
                ["GameMode"] = room.GameMode.ToString(),
                ["Map"] = room.Map.ToString(),
                ["TeamBalance"] = room.setting is AbstractTeamMatchSetting teamSetting && teamSetting.TeamBalance,
                ["HasPassword"] = roomInfoJson["HasPassword"],
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = room.PlayerCount,
                ["NowPlaying"] = room.NowPlaying,
                ["PlayerID"] = playerId,
                ["PlayerName"] = playerName
            };

            session.SendAsyncJsonWithTimeStamp(response);
            WaitRoomEventHandler.BroadcastRoomUpdate(room, MessageType.WaitRoomUpdateNotification);
            BroadcastRoomListUpdate();
        }

        public static void ExitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            _ = session;
            _ = dic;
        }

        public static void RemoveRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            _ = session;
            _ = dic;
        }

        public static void UpdateRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            _ = dic;

            LobbyServerManager.Instance.PruneDisconnectedTcpSessions();

            var result = BuildRoomListUpdate();
            session.SendAsyncJsonWithTimeStamp(result);
        }

        public static void BroadcastRoomListUpdate()
        {
            LobbyServerManager.Instance.BroadcastToAllInLobby(BuildRoomListUpdate());
        }

        private static JObject BuildRoomListUpdate()
        {

            var result = new JObject
            {
                ["MessageType"] = MessageType.RoomListUpdateNotification
            };

            var roomArray = new JArray();
            foreach (var room in WaitRoomManager.Instance().GetAllRooms())
            {
                var json = new RoomListEntry
                {
                    RoomId = room.RoomId,
                    RoomName = room.RoomName,
                    OwnerId = room.GetFirstPlayerId(),
                    Capacity = room.Capacity,
                    GameMode = room.GameMode.ToString(),
                    TeamBalance = room.setting is AbstractTeamMatchSetting teamSetting && teamSetting.TeamBalance,
                    PlayerCount = room.Players.Count
                }.ToJson();
                json["Map"] = room.Map.ToString();
                json["NowPlaying"] = room.NowPlaying;
                json["HasPassword"] = !string.IsNullOrWhiteSpace(room.Password);
                roomArray.Add(json);
            }

            foreach (var room in MissionWaitRoomManager.Instance.GetAllMissionRooms())
            {
                var json = new JObject
                {
                    ["RoomId"] = room.RoomId,
                    ["RoomID"] = room.RoomId,
                    ["RoomName"] = room.RoomName,
                    ["OwnerId"] = room.GetFirstPlayerId(),
                    ["OwnerID"] = room.GetFirstPlayerId(),
                    ["Capacity"] = room.Capacity,
                    ["GameMode"] = room.GameMode.ToString(),
                    ["PlayerCount"] = room.PlayerCount,
                    ["NowPlaying"] = room.NowPlaying,
                    ["Map"] = room.Map.ToString(),
                    ["HasPassword"] = !string.IsNullOrWhiteSpace(room.Password),
                    ["IsMission"] = true
                };
                roomArray.Add(json);
            }

            result["Rooms"] = roomArray;
            result["RoomCount"] = roomArray.Count;
            return result;
        }

        public static void MatchStart(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomID") ?? dic.GetStringOrNull("RoomId");
            var playerId = session?.PlayerID;

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId ?? string.Empty)
                ?? MissionWaitRoomManager.Instance.FindMissionRoom(roomId ?? string.Empty);
            if (waitRoom == null)
            {
                SendMatchStartError(session, roomId, "RoomNotFound");
                return;
            }

            if (string.IsNullOrWhiteSpace(playerId) ||
                !string.Equals(waitRoom.GetFirstPlayerId(), playerId, StringComparison.OrdinalIgnoreCase))
            {
                SendMatchStartError(session, roomId, "Only the room owner can start the match");
                return;
            }

            var matchRoom = MatchRoomManager.Instance.StartMatchFromWaitRoom(waitRoom);
            if (matchRoom == null)
            {
                SendMatchStartError(session, roomId, "Match could not be started");
            }
        }

        private static void SendMatchStartError(ClientSession session, string? roomId, string error)
        {
            session?.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.ErrorNotification,
                ["Success"] = false,
                ["ErrorMessage"] = error,
                ["RoomID"] = roomId ?? string.Empty
            });
        }
    }
}
