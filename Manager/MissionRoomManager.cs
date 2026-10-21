using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenGSCore;

#nullable enable

namespace OpenGSServer
{
    /// <summary>
    /// ミッション（PVE）待機室マネージャー
    /// </summary>
    public class MissionWaitRoomManager
    {
        private static readonly MissionWaitRoomManager _instance = new();
        public static MissionWaitRoomManager Instance => _instance;

        private readonly ConcurrentDictionary<string, WaitRoom> _missionRooms = new();
        private readonly object _lockObj = new();

        public int RoomLimit { get; private set; } = 16;

        private MissionWaitRoomManager()
        {
        }

        public void SetRoomLimit(int limit)
        {
            RoomLimit = Math.Max(1, limit);
        }

        /// <summary>
        /// 新しいミッションルームを作成
        /// </summary>
        public CreateNewWaitRoomResult CreateMissionRoom(string roomName, string missionId, int capacity = 4)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                roomName = $"Mission_{missionId}_{DateTime.Now:HHmm}";
            }

            WaitRoom room;
            lock (_lockObj)
            {
                if (_missionRooms.Count >= RoomLimit)
                {
                    return new CreateNewWaitRoomResult("Mission Server RoomLimit Over", null);
                }

room = new WaitRoom(roomName, capacity);

                // The mission's own mode. This used to be decided by looking for
                // the words "quest" or "team" in the mission's name, so the same
                // mission played as two different modes depending on what it had
                // been called, and neither of them was a mission: the setting it
                // built declared itself a survival, so a mission room was played
                // by the survival rule and settled as one.
                room.ChangeGameMode(EGameMode.Mission);
                room.setting = new MissionMatchSetting(
                    capacity,
                    string.IsNullOrWhiteSpace(missionId) ? "Default" : missionId);
                room.Map = EMap.DryDays;

                if (!_missionRooms.TryAdd(room.RoomId, room))
                {
                    return new CreateNewWaitRoomResult("Fail (Duplicate ID)", null);
                }
            }

ConsoleWrite.WriteMessage($"[MISSION] Created mission room: {room.RoomName} ({room.RoomId}) Mission: {missionId}", ConsoleColor.Magenta);
            PromoteToVisibleRoom(room);
            return new CreateNewWaitRoomResult("Successful", room);
        }

        public WaitRoom? FindMissionRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) return null;
            return _missionRooms.TryGetValue(roomId, out var room) ? room : null;
        }

        public List<WaitRoom> GetAllMissionRooms()
        {
            return _missionRooms.Values.ToList();
        }

        public bool CloseMissionRoom(string roomId)
        {
            // Both places. It was only ever removed from this dictionary, so a
            // mission room that had been put on the list stayed on it after being
            // closed, offering players a room that could no longer be joined.
            WaitRoomManager.Instance().CloseRoom(roomId);

            if (_missionRooms.TryRemove(roomId, out var room))
            {
                ConsoleWrite.WriteMessage($"[MISSION] Closed mission room: {room.RoomName} ({roomId})", ConsoleColor.DarkMagenta);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Puts a mission room on the room list so a client can find it.
        /// <para>
        /// A mission room used to live only here, in a dictionary nothing else read.
        /// The room list a client browses is built from the main wait room manager,
        /// so a mission could be created, joined by id, played and never once
        /// appeared in the list of rooms. A room nobody can find is not a room.
        /// </para>
        /// </summary>
        public bool PromoteToVisibleRoom(WaitRoom missionRoom)
        {
            if (missionRoom == null)
            {
                return false;
            }

            WaitRoomManager.Instance().RegisterExternalRoom(missionRoom);
            return true;
        }
    }
}
