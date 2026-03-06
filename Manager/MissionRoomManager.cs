using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenGSCore;

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

            if (_missionRooms.Count >= RoomLimit)
            {
                return new CreateNewWaitRoomResult("Mission Server RoomLimit Over", null);
            }

            // ミッション用の待機室を作成
            var room = new WaitRoom(roomName, capacity);
            
            // TODO: EGameMode に PVE 系のモードを追加するか、既存のものを流用
            // ここでは Practice や Survival をミッションのプレースホルダとして使用可能
            room.ChangeGameMode(EGameMode.Survival);

            if (_missionRooms.TryAdd(room.RoomId, room))
            {
                ConsoleWrite.WriteMessage($"[MISSION] Created mission room: {room.RoomName} ({room.RoomId}) Mission: {missionId}", ConsoleColor.Magenta);
                return new CreateNewWaitRoomResult("Successful", room);
            }

            return new CreateNewWaitRoomResult("Fail (Duplicate ID)", null);
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
            if (_missionRooms.TryRemove(roomId, out var room))
            {
                ConsoleWrite.WriteMessage($"[MISSION] Closed mission room: {room.RoomName} ({roomId})", ConsoleColor.DarkMagenta);
                return true;
            }
            return false;
        }
    }
}
