using System;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public static class LobbyEventHandler
    {
        public static void CreateNewWaitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("PlayerID") ?? dic.GetStringOrNull("PlayerId");
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
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = room.PlayerCount,
                ["NowPlaying"] = room.NowPlaying
            };

            session.SendAsyncJsonWithTimeStamp(json);
        }

        public static void CreateNewMissionRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var playerId = dic.GetStringOrNull("PlayerID") ?? dic.GetStringOrNull("PlayerId");
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
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = room.PlayerCount,
                ["NowPlaying"] = room.NowPlaying
            });
        }

        public static void QuickStartRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            _ = session;
            _ = dic;
        }

        public static void EnterRoomRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomID") ?? dic.GetStringOrNull("RoomId");
            var playerId = dic.GetStringOrNull("PlayerID") ?? dic.GetStringOrNull("PlayerId");
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

            var roomManager = WaitRoomManager.Instance();
            var room = roomManager.FindWaitRoom(roomId);
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
            roomInfoJson["HasPassword"] = !string.IsNullOrWhiteSpace(room.Password);

            var response = new JObject
            {
                ["MessageType"] = MessageType.JoinRoomResponse,
                ["Success"] = true,
                ["RoomInfo"] = roomInfoJson,
                ["RoomId"] = room.RoomId,
                ["RoomID"] = room.RoomId,
                ["RoomName"] = room.RoomName,
                ["Capacity"] = room.Capacity,
                ["GameMode"] = room.GameMode.ToString(),
                ["Map"] = room.Map.ToString(),
                ["TeamBalance"] = room.setting is AbstractTeamMatchSetting teamSetting && teamSetting.TeamBalance,
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = room.PlayerCount,
                ["NowPlaying"] = room.NowPlaying,
                ["PlayerID"] = playerId,
                ["PlayerName"] = playerName
            };

            session.SendAsyncJsonWithTimeStamp(response);
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

            var matchRoomManager = MatchRoomManager.Instance;
            var rooms = matchRoomManager.AllRooms();

            var result = new JObject
            {
                ["MessageType"] = MessageType.UpdateRoomResult,
                ["RoomCount"] = rooms.Count
            };

            var roomArray = new JArray();
            foreach (var item in rooms)
            {
                var json = new JObject
                {
                    ["RoomNumber"] = item.RoomNumber,
                    ["RoomID"] = item.Id,
                    ["GameMode"] = "",
                    ["MacCapacity"] = 10
                };

                roomArray.Add(json);
            }

            result["AllRoom"] = roomArray;
            session.SendAsync(result.ToString());
        }

        public static void MatchStart(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            _ = session;
            _ = dic;
        }
    }
}
