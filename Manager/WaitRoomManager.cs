using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public class WaitRoomManager
    {
        private static readonly WaitRoomManager _instance = new();
        public static WaitRoomManager Instance() => _instance;
        public CreateNewWaitRoomResult CreateNewWaitRoom(string roomName) => CreateWaitRoom(roomName);

        private readonly ConcurrentDictionary<string, WaitRoom> _rooms = new();
        private readonly object _lockObj = new();

        public int RoomLimit { get; private set; } = 32;

        private WaitRoomManager()
        {
        }

        public void SetRoomLimit(int limit)
        {
            RoomLimit = Math.Max(1, limit);
        }

        public CreateNewWaitRoomResult CreateWaitRoom(string roomName, int capacity = 8, EGameMode mode = EGameMode.DeathMatch)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                roomName = Template.RandomRoomName();
            }

            if (_rooms.Count >= RoomLimit)
            {
                return new CreateNewWaitRoomResult("Server RoomLimit Over", null);
            }

            var room = new WaitRoom(roomName, capacity);
            room.ChangeGameMode(mode);

            if (_rooms.TryAdd(room.RoomId, room))
            {
                ConsoleWrite.WriteMessage($"[WAITROOM] Created room: {room.RoomName} ({room.RoomId}) Mode: {mode}", ConsoleColor.Green);
                return new CreateNewWaitRoomResult("Successful", room);
            }

            return new CreateNewWaitRoomResult("Fail (Duplicate ID)", null);
        }

        public WaitRoom? FindWaitRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) return null;
            return _rooms.TryGetValue(roomId, out var room) ? room : null;
        }

        public List<WaitRoom> GetAllRooms()
        {
            return _rooms.Values.ToList();
        }

        public List<WaitRoom> FindWaitRoomsByGameMode(EGameMode mode)
        {
            return _rooms.Values.Where(r => r.setting != null && r.setting.Mode == mode).ToList();
        }

        public bool CloseRoom(string roomId)
        {
            if (_rooms.TryRemove(roomId, out var room))
            {
                ConsoleWrite.WriteMessage($"[WAITROOM] Closed room: {room.RoomName} ({roomId})", ConsoleColor.Yellow);
                return true;
            }
            return false;
        }

        public JObject RoomInfo()
        {
            var result = new JObject
            {
                ["WaitRoomCount"] = _rooms.Count,
                ["RoomCapacity"] = $"{_rooms.Count}/{RoomLimit}"
            };

            var array = new JArray();
            foreach (var room in _rooms.Values)
            {
                var roomJson = new JObject
                {
                    ["RoomID"] = room.RoomId,
                    ["RoomName"] = room.RoomName,
                    ["Capacity"] = room.Capacity,
                    ["PlayerCount"] = room.Players.Count,
                    ["NowPlaying"] = room.NowPlaying
                };

                if (room.setting != null)
                {
                    var gameModeJson = new JObject
                    {
                        ["Mode"] = room.setting.Mode.ToString(),
                        ["MaxPlayers"] = room.setting.MaxPlayerCount
                    };
                    roomJson["GameMode"] = gameModeJson;
                }

                array.Add(roomJson);
            }

            result["WaitRooms"] = array;
            return result;
        }
    }
}
