using System.Collections.Generic;
using System.Linq;
using OpenGSCore;

namespace OpenGSServer
{


    public interface ILobby
    {
        void AddChat(string playerName, string chat);
        bool AddPlayer(string playerId, string playerName);
        bool RemovePlayer(string playerId);
        List<LobbyPlayerInfo> GetPlayers();
        LobbyRoomInfo? CreateRoom(string roomName, string ownerId, EGameMode gameMode);
        bool JoinRoom(string roomId, string playerId);
        List<LobbyRoomInfo> GetAvailableRooms();
    }

    public class Lobby : ILobby
    {
        private readonly ChatManager chatManager = new();
        private readonly Dictionary<string, LobbyPlayerInfo> players = new();
        private readonly Dictionary<string, LobbyRoomInfo> rooms = new();
        private int nextRoomId = 1;

        internal ChatManager ChatManager => chatManager;

        public int UserCount() => players.Count;

        public void AddPlayer()
        {
            // 基本実装
        }

        public void AddChat(string playerName, string chat)
        {
            chatManager.NewChat("", playerName, chat, ChatType.All);
        }

        public bool AddPlayer(string playerId, string playerName)
        {
            if (players.ContainsKey(playerId))
                return false;

            var lobbyPlayer = new LobbyPlayerInfo
            {
                PlayerId = playerId,
                PlayerName = playerName,
                Status = LobbyPlayerStatus.Idle,
                JoinedAt = System.DateTime.UtcNow
            };

            players[playerId] = lobbyPlayer;
            return true;
        }

        public bool RemovePlayer(string playerId)
        {
            return players.Remove(playerId);
        }

        public List<LobbyPlayerInfo> GetPlayers()
        {
            return players.Values.ToList();
        }

        public LobbyRoomInfo? CreateRoom(string roomName, string ownerId, EGameMode gameMode)
        {
            if (!players.ContainsKey(ownerId))
                return null;

            var roomId = $"room_{nextRoomId++}";
            var room = new LobbyRoomInfo
            {
                RoomId = roomId,
                RoomName = roomName,
                OwnerId = ownerId,
                GameMode = gameMode,
                Players = new List<string> { ownerId },
                MaxPlayers = 8,
                IsPrivate = false,
                CreatedAt = System.DateTime.UtcNow
            };

            rooms[roomId] = room;
            players[ownerId].CurrentRoomId = roomId;
            players[ownerId].Status = LobbyPlayerStatus.InRoom;

            return room;
        }

        public bool JoinRoom(string roomId, string playerId)
        {
            if (!players.ContainsKey(playerId) || !rooms.ContainsKey(roomId))
                return false;

            var room = rooms[roomId];
            if (room.Players.Count >= room.MaxPlayers)
                return false;

            room.Players.Add(playerId);
            players[playerId].CurrentRoomId = roomId;
            players[playerId].Status = LobbyPlayerStatus.InRoom;

            return true;
        }

        public List<LobbyRoomInfo> GetAvailableRooms()
        {
            return rooms.Values.Where(r => !r.IsPrivate && r.Players.Count < r.MaxPlayers).ToList();
        }

        public LobbyRoomInfo? GetRoom(string roomId)
        {
            return rooms.ContainsKey(roomId) ? rooms[roomId] : null;
        }

        public bool LeaveRoom(string playerId)
        {
            if (!players.ContainsKey(playerId))
                return false;

            var player = players[playerId];
            if (string.IsNullOrEmpty(player.CurrentRoomId))
                return false;

            var roomId = player.CurrentRoomId;
            if (rooms.ContainsKey(roomId))
            {
                var room = rooms[roomId];
                room.Players.Remove(playerId);

                // ルームが空になったら削除
                if (room.Players.Count == 0)
                {
                    rooms.Remove(roomId);
                }
                else if (room.OwnerId == playerId)
                {
                    // オーナーが退出したら新しいオーナーを設定
                    room.OwnerId = room.Players.First();
                }
            }

            player.CurrentRoomId = null;
            player.Status = LobbyPlayerStatus.Idle;

            return true;
        }
    }

    // ロビープレイヤー情報
    public class LobbyPlayerInfo
    {
        public string PlayerId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public LobbyPlayerStatus Status { get; set; } = LobbyPlayerStatus.Idle;
        public string? CurrentRoomId { get; set; }
        public System.DateTime JoinedAt { get; set; }
    }

    // ロビールーム情報
    public class LobbyRoomInfo
    {
        public string RoomId { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public EGameMode GameMode { get; set; } = EGameMode.DeathMatch;
        public List<string> Players { get; set; } = new();
        public int MaxPlayers { get; set; } = 8;
        public bool IsPrivate { get; set; } = false;
        public System.DateTime CreatedAt { get; set; }
    }

    // ロビープレイヤーステータス
    public enum LobbyPlayerStatus
    {
        Idle,
        InRoom,
        InGame,
        Away
    }
}

