using System;
using System.Collections.Generic;
using System.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// ロビーサーバーマネージャー - ロビー機能の全体管理
    /// </summary>
    public class LobbyServerManager
    {
        private static LobbyServerManager? instance;
        private readonly Lobby lobby = new();
        private readonly Dictionary<string, LobbyPlayerInfo> connectedPlayers = new();

        public static LobbyServerManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new LobbyServerManager();
                }
                return instance;
            }
        }

        private LobbyServerManager()
        {
            InitializeLobby();
        }

        private void InitializeLobby()
        {
            ConsoleWrite.WriteMessage("[LOBBY] Lobby server initialized", ConsoleColor.Cyan);
        }

        #region プレイヤー管理

        /// <summary>
        /// プレイヤーをロビーに参加させる
        /// </summary>
        public bool PlayerJoinLobby(string playerId, string playerName)
        {
            if (connectedPlayers.ContainsKey(playerId))
                return false;

            var success = lobby.AddPlayer(playerId, playerName);
            if (success)
            {
                connectedPlayers[playerId] = lobby.GetPlayers().First(p => p.PlayerId == playerId);
                ConsoleWrite.WriteMessage($"[LOBBY] Player {playerId} joined lobby", ConsoleColor.Green);
            }

            return success;
        }

        /// <summary>
        /// プレイヤーをロビーから退出させる
        /// </summary>
        public bool PlayerLeaveLobby(string playerId)
        {
            if (!connectedPlayers.ContainsKey(playerId))
                return false;

            // ルームから退出
            lobby.LeaveRoom(playerId);

            var success = lobby.RemovePlayer(playerId);
            if (success)
            {
                connectedPlayers.Remove(playerId);
                ConsoleWrite.WriteMessage($"[LOBBY] Player {playerId} left lobby", ConsoleColor.Yellow);
            }

            return success;
        }

        /// <summary>
        /// 接続中のプレイヤー情報を取得
        /// </summary>
        public LobbyPlayerInfo? GetPlayerInfo(string playerId)
        {
            return connectedPlayers.ContainsKey(playerId) ? connectedPlayers[playerId] : null;
        }

        /// <summary>
        /// ロビー内の全プレイヤー情報を取得
        /// </summary>
        public List<LobbyPlayerInfo> GetAllPlayers()
        {
            return lobby.GetPlayers();
        }

        #endregion

        #region ルーム管理

        /// <summary>
        /// 新しいルームを作成
        /// </summary>
        public LobbyRoomInfo? CreateRoom(string roomName, string ownerId, EGameMode gameMode)
        {
            var room = lobby.CreateRoom(roomName, ownerId, gameMode);
            if (room != null)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Room '{roomName}' created by {ownerId}", ConsoleColor.Green);
            }
            return room;
        }

        /// <summary>
        /// ルームに参加
        /// </summary>
        public bool JoinRoom(string roomId, string playerId)
        {
            var success = lobby.JoinRoom(roomId, playerId);
            if (success)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Player {playerId} joined room {roomId}", ConsoleColor.Green);
            }
            return success;
        }

        /// <summary>
        /// ルームから退出
        /// </summary>
        public bool LeaveRoom(string playerId)
        {
            var success = lobby.LeaveRoom(playerId);
            if (success)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Player {playerId} left room", ConsoleColor.Yellow);
            }
            return success;
        }

        /// <summary>
        /// 利用可能なルーム一覧を取得
        /// </summary>
        public List<LobbyRoomInfo> GetAvailableRooms()
        {
            return lobby.GetAvailableRooms();
        }

        /// <summary>
        /// ルーム情報を取得
        /// </summary>
        public LobbyRoomInfo? GetRoomInfo(string roomId)
        {
            return lobby.GetRoom(roomId);
        }

        #endregion

        #region チャット機能

        /// <summary>
        /// ロビーチャットにメッセージを追加
        /// </summary>
        public void AddLobbyChat(string playerId, string message)
        {
            if (connectedPlayers.ContainsKey(playerId))
            {
                lobby.AddChat(playerId, message);
                ConsoleWrite.WriteMessage($"[LOBBY] Chat from {playerId}: {message}", ConsoleColor.Gray);
            }
        }

        /// <summary>
        /// ルームチャットにメッセージを追加
        /// </summary>
        public void AddRoomChat(string roomId, string playerId, string message)
        {
            var room = lobby.GetRoom(roomId);
            if (room != null && room.Players.Contains(playerId))
            {
                // ルームチャットの実装（将来的に拡張）
                ConsoleWrite.WriteMessage($"[LOBBY] Room chat in {roomId} from {playerId}: {message}", ConsoleColor.Gray);
            }
        }

        #endregion

        #region マッチメイキング

        /// <summary>
        /// クイックマッチ - 自動的に適切なルームを探すまたは作成
        /// </summary>
        public string? QuickMatch(string playerId, EGameMode preferredMode)
        {
            if (!connectedPlayers.ContainsKey(playerId))
                return null;

            // 同じゲームモードの空いているルームを探す
            var availableRoom = lobby.GetAvailableRooms()
                .FirstOrDefault(r => r.GameMode == preferredMode);

            if (availableRoom != null)
            {
                // 既存ルームに参加
                if (lobby.JoinRoom(availableRoom.RoomId, playerId))
                {
                    return availableRoom.RoomId;
                }
            }
            else
            {
                // 新しいルームを作成
                var roomName = $"{preferredMode} Quick Match";
                var newRoom = lobby.CreateRoom(roomName, playerId, preferredMode);
                return newRoom?.RoomId;
            }

            return null;
        }

        /// <summary>
        /// カスタムマッチ - 指定された条件でルームを探す
        /// </summary>
        public string? CustomMatch(string playerId, EGameMode gameMode, int maxPlayers, bool isPrivate = false)
        {
            if (!connectedPlayers.ContainsKey(playerId))
                return null;

            // 条件に合うルームを探す
            var suitableRoom = lobby.GetAvailableRooms()
                .FirstOrDefault(r => r.GameMode == gameMode &&
                                   r.MaxPlayers == maxPlayers &&
                                   r.IsPrivate == isPrivate);

            if (suitableRoom != null)
            {
                if (lobby.JoinRoom(suitableRoom.RoomId, playerId))
                {
                    return suitableRoom.RoomId;
                }
            }

            return null;
        }

        #endregion

        #region 統計情報

        /// <summary>
        /// ロビーの統計情報を取得
        /// </summary>
        public LobbyStats GetLobbyStats()
        {
            var rooms = lobby.GetAvailableRooms();
            var players = lobby.GetPlayers();

            return new LobbyStats
            {
                TotalPlayers = players.Count,
                ActiveRooms = rooms.Count,
                RoomsByGameMode = rooms.GroupBy(r => r.GameMode)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        #endregion
    }

    /// <summary>
    /// ロビー統計情報
    /// </summary>
    public class LobbyStats
    {
        public int TotalPlayers { get; set; }
        public int ActiveRooms { get; set; }
        public Dictionary<EGameMode, int> RoomsByGameMode { get; set; } = new();
    }
}