using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenGSCore;
using OpenGSServer.Utility; // Ping管理を追加

namespace OpenGSServer
{
    /// <summary>
    /// ロビーサーバーマネージャー - ロビー機能の全体管理
    /// C# 14.0: スレッドセーフ + イベントシステム + レート制限
    /// </summary>
    public sealed class LobbyServerManager : IDisposable
    {
        private static readonly Lazy<LobbyServerManager> _instance = 
            new(() => new LobbyServerManager(), LazyThreadSafetyMode.ExecutionAndPublication);
        
        private readonly Lobby _lobby = new();
        private readonly ConcurrentDictionary<string, LobbyPlayerInfo> _connectedPlayers = new();
        private readonly ConcurrentDictionary<string, PlayerRateLimiter> _rateLimiters = new();
        private readonly ConcurrentDictionary<PlayerID, string> _playerIdMapping = new(); // PlayerID <-> string ID のマッピング
        private readonly PingManager _pingManager = new(); // Ping管理（PlayerID型使用）
        private readonly ReaderWriterLockSlim _lobbyLock = new();
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        public static LobbyServerManager Instance => _instance.Value;

        // イベント
        public event Action<string, LobbyPlayerInfo>? OnPlayerJoined;
        public event Action<string>? OnPlayerLeft;
        public event Action<string, LobbyRoomInfo>? OnRoomCreated;
        public event Action<string, string>? OnPlayerJoinedRoom;
        public event Action<string>? OnRoomClosed;
        public event Action<string, string>? OnChatMessage;

        private LobbyServerManager()
        {
            InitializeLobby();
            
            // 定期的にクリーンアップ（5分ごと）
            _cleanupTimer = new Timer(
                callback: _ => CleanupInactivePlayers(),
                state: null,
                dueTime: TimeSpan.FromMinutes(5),
                period: TimeSpan.FromMinutes(5)
            );
        }

        private void InitializeLobby()
        {
            ConsoleWrite.WriteMessage("[LOBBY] Lobby server initialized", ConsoleColor.Cyan);
        }

        private void CleanupInactivePlayers()
        {
            try
            {
                var inactivePlayers = _connectedPlayers
                    .Where(kvp => (DateTime.UtcNow - kvp.Value.JoinedAt).TotalMinutes > 30)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var playerId in inactivePlayers)
                {
                    ConsoleWrite.WriteMessage($"[LOBBY] Removing inactive player: {playerId}", ConsoleColor.Yellow);
                    PlayerLeaveLobby(playerId);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Cleanup error: {ex.Message}", ConsoleColor.Red);
            }
        }

        #region プレイヤー管理

        public LobbyResult<LobbyPlayerInfo> PlayerJoinLobby(string playerId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<LobbyPlayerInfo>.Error("Player ID cannot be empty");
            
            if (string.IsNullOrWhiteSpace(playerName))
                return LobbyResult<LobbyPlayerInfo>.Error("Player name cannot be empty");
            
            if (playerName.Length > 20)
                return LobbyResult<LobbyPlayerInfo>.Error("Player name too long (max 20 characters)");

            var rateLimiter = _rateLimiters.GetOrAdd(playerId, _ => new PlayerRateLimiter());
            if (!rateLimiter.AllowJoin())
                return LobbyResult<LobbyPlayerInfo>.Error("Too many join attempts. Please wait.");

            _lobbyLock.EnterWriteLock();
            try
            {
                if (_connectedPlayers.ContainsKey(playerId))
                    return LobbyResult<LobbyPlayerInfo>.Error("Player already in lobby");

                var success = _lobby.AddPlayer(playerId, playerName);
                if (!success)
                    return LobbyResult<LobbyPlayerInfo>.Error("Failed to add player to lobby");

                var playerInfo = _lobby.GetPlayers().FirstOrDefault(p => p.PlayerId == playerId);
                if (playerInfo == null)
                    return LobbyResult<LobbyPlayerInfo>.Error("Player info not found after join");

                _connectedPlayers[playerId] = playerInfo;
                ConsoleWrite.WriteMessage($"[LOBBY] Player {playerId} ({playerName}) joined lobby", ConsoleColor.Green);
                
                // Ping追跡を開始（PlayerID型で）
                var pid = PlayerID.FromString(playerId);
                _playerIdMapping[pid] = playerId;
                _pingManager.AddPlayer(pid);
                
                OnPlayerJoined?.Invoke(playerId, playerInfo);
                
                return LobbyResult<LobbyPlayerInfo>.Success(playerInfo);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error joining lobby: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<LobbyPlayerInfo>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitWriteLock();
            }
        }

        public LobbyResult<bool> PlayerLeaveLobby(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<bool>.Error("Player ID cannot be empty");

            _lobbyLock.EnterWriteLock();
            try
            {
                if (!_connectedPlayers.ContainsKey(playerId))
                    return LobbyResult<bool>.Error("Player not in lobby");

                _lobby.LeaveRoom(playerId);
                var success = _lobby.RemovePlayer(playerId);
                if (!success)
                    return LobbyResult<bool>.Error("Failed to remove player from lobby");

                _connectedPlayers.TryRemove(playerId, out _);
                _rateLimiters.TryRemove(playerId, out _);
                
                // Ping追跡を停止
                var pid = PlayerID.FromString(playerId);
                _pingManager.RemovePlayer(pid);
                _playerIdMapping.TryRemove(pid, out _);
                
                ConsoleWrite.WriteMessage($"[LOBBY] Player {playerId} left lobby", ConsoleColor.Yellow);
                OnPlayerLeft?.Invoke(playerId);
                
                return LobbyResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error leaving lobby: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<bool>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitWriteLock();
            }
        }

        public LobbyResult<LobbyPlayerInfo> GetPlayerInfo(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<LobbyPlayerInfo>.Error("Player ID cannot be empty");

            _lobbyLock.EnterReadLock();
            try
            {
                if (!_connectedPlayers.TryGetValue(playerId, out var playerInfo))
                    return LobbyResult<LobbyPlayerInfo>.Error("Player not found");

                return LobbyResult<LobbyPlayerInfo>.Success(playerInfo);
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        public LobbyResult<List<LobbyPlayerInfo>> GetAllPlayers()
        {
            _lobbyLock.EnterReadLock();
            try
            {
                var players = _lobby.GetPlayers();
                return LobbyResult<List<LobbyPlayerInfo>>.Success(players);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error getting players: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<List<LobbyPlayerInfo>>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        #endregion

        #region ルーム管理

        public LobbyResult<LobbyRoomInfo> CreateRoom(string roomName, string ownerId, EGameMode gameMode)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return LobbyResult<LobbyRoomInfo>.Error("Room name cannot be empty");
            
            if (roomName.Length > 30)
                return LobbyResult<LobbyRoomInfo>.Error("Room name too long (max 30 characters)");
            
            if (string.IsNullOrWhiteSpace(ownerId))
                return LobbyResult<LobbyRoomInfo>.Error("Owner ID cannot be empty");

            var rateLimiter = _rateLimiters.GetOrAdd(ownerId, _ => new PlayerRateLimiter());
            if (!rateLimiter.AllowRoomAction())
                return LobbyResult<LobbyRoomInfo>.Error("Too many room actions. Please wait.");

            _lobbyLock.EnterWriteLock();
            try
            {
                if (!_connectedPlayers.ContainsKey(ownerId))
                    return LobbyResult<LobbyRoomInfo>.Error("Owner not in lobby");

                var room = _lobby.CreateRoom(roomName, ownerId, gameMode);
                if (room == null)
                    return LobbyResult<LobbyRoomInfo>.Error("Failed to create room");

                ConsoleWrite.WriteMessage($"[LOBBY] Room '{roomName}' created by {ownerId}", ConsoleColor.Green);
                OnRoomCreated?.Invoke(ownerId, room);
                
                return LobbyResult<LobbyRoomInfo>.Success(room);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error creating room: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<LobbyRoomInfo>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitWriteLock();
            }
        }

        public LobbyResult<bool> JoinRoom(string roomId, string playerId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return LobbyResult<bool>.Error("Room ID cannot be empty");
            
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<bool>.Error("Player ID cannot be empty");

            var rateLimiter = _rateLimiters.GetOrAdd(playerId, _ => new PlayerRateLimiter());
            if (!rateLimiter.AllowRoomAction())
                return LobbyResult<bool>.Error("Too many room actions. Please wait.");

            _lobbyLock.EnterWriteLock();
            try
            {
                if (!_connectedPlayers.ContainsKey(playerId))
                    return LobbyResult<bool>.Error("Player not in lobby");

                var success = _lobby.JoinRoom(roomId, playerId);
                if (!success)
                    return LobbyResult<bool>.Error("Failed to join room (room full or not found)");

                ConsoleWrite.WriteMessage($"[LOBBY] Player {playerId} joined room {roomId}", ConsoleColor.Green);
                OnPlayerJoinedRoom?.Invoke(playerId, roomId);
                
                return LobbyResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error joining room: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<bool>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitWriteLock();
            }
        }

        public LobbyResult<bool> LeaveRoom(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<bool>.Error("Player ID cannot be empty");

            _lobbyLock.EnterWriteLock();
            try
            {
                var success = _lobby.LeaveRoom(playerId);
                if (!success)
                    return LobbyResult<bool>.Error("Player not in any room");

                ConsoleWrite.WriteMessage($"[LOBBY] Player {playerId} left room", ConsoleColor.Yellow);
                return LobbyResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error leaving room: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<bool>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitWriteLock();
            }
        }

        #endregion

        #region チャット機能

        public LobbyResult<bool> AddLobbyChat(string playerId, string message)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<bool>.Error("Player ID cannot be empty");
            
            if (string.IsNullOrWhiteSpace(message))
                return LobbyResult<bool>.Error("Message cannot be empty");
            
            if (message.Length > 200)
                return LobbyResult<bool>.Error("Message too long (max 200 characters)");

            var rateLimiter = _rateLimiters.GetOrAdd(playerId, _ => new PlayerRateLimiter());
            if (!rateLimiter.AllowChat())
                return LobbyResult<bool>.Error("Too many chat messages. Please slow down.");

            _lobbyLock.EnterReadLock();
            try
            {
                if (!_connectedPlayers.ContainsKey(playerId))
                    return LobbyResult<bool>.Error("Player not in lobby");

                _lobby.AddChat(playerId, message);
                ConsoleWrite.WriteMessage($"[LOBBY] Chat from {playerId}: {message}", ConsoleColor.Gray);
                
                OnChatMessage?.Invoke(playerId, message);
                
                return LobbyResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error adding chat: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<bool>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        public LobbyResult<bool> AddRoomChat(string roomId, string playerId, string message)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return LobbyResult<bool>.Error("Room ID cannot be empty");
            
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<bool>.Error("Player ID cannot be empty");
            
            if (string.IsNullOrWhiteSpace(message))
                return LobbyResult<bool>.Error("Message cannot be empty");
            
            if (message.Length > 200)
                return LobbyResult<bool>.Error("Message too long (max 200 characters)");

            var rateLimiter = _rateLimiters.GetOrAdd(playerId, _ => new PlayerRateLimiter());
            if (!rateLimiter.AllowChat())
                return LobbyResult<bool>.Error("Too many chat messages. Please slow down.");

            _lobbyLock.EnterReadLock();
            try
            {
                var room = _lobby.GetRoom(roomId);
                if (room == null)
                    return LobbyResult<bool>.Error("Room not found");

                if (!room.Players.Contains(playerId))
                    return LobbyResult<bool>.Error("Player not in this room");

                ConsoleWrite.WriteMessage($"[LOBBY] Room chat in {roomId} from {playerId}: {message}", ConsoleColor.Gray);
                
                return LobbyResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error adding room chat: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<bool>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        #endregion

        #region マッチメイキング

        public LobbyResult<string> QuickMatch(string playerId, EGameMode preferredMode)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<string>.Error("Player ID cannot be empty");

            _lobbyLock.EnterUpgradeableReadLock();
            try
            {
                if (!_connectedPlayers.ContainsKey(playerId))
                    return LobbyResult<string>.Error("Player not in lobby");

                var availableRoom = _lobby.GetAvailableRooms()
                    .FirstOrDefault(r => r.GameMode == preferredMode);

                if (availableRoom != null)
                {
                    _lobbyLock.EnterWriteLock();
                    try
                    {
                        if (_lobby.JoinRoom(availableRoom.RoomId, playerId))
                        {
                            ConsoleWrite.WriteMessage($"[LOBBY] Quick match: {playerId} joined existing room", ConsoleColor.Cyan);
                            return LobbyResult<string>.Success(availableRoom.RoomId);
                        }
                    }
                    finally
                    {
                        _lobbyLock.ExitWriteLock();
                    }
                }
                
                _lobbyLock.EnterWriteLock();
                try
                {
                    var roomName = $"{preferredMode} Quick Match";
                    var newRoom = _lobby.CreateRoom(roomName, playerId, preferredMode);
                    
                    if (newRoom != null)
                    {
                        ConsoleWrite.WriteMessage($"[LOBBY] Quick match: {playerId} created new room", ConsoleColor.Cyan);
                        return LobbyResult<string>.Success(newRoom.RoomId);
                    }
                }
                finally
                {
                    _lobbyLock.ExitWriteLock();
                }

                return LobbyResult<string>.Error("Failed to create or join room");
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error in quick match: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<string>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitUpgradeableReadLock();
            }
        }

        #endregion

        #region Ping管理

        /// <summary>
        /// プレイヤーのPing測定を記録
        /// </summary>
        public void RecordPlayerPing(string playerId, double pingMs, bool packetLost = false)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return;

            _lobbyLock.EnterReadLock();
            try
            {
                if (_connectedPlayers.ContainsKey(playerId))
                {
                    var pid = PlayerID.FromString(playerId);
                    _pingManager.RecordPing(pid, pingMs, packetLost);
                }
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        /// <summary>
        /// プレイヤーのPing測定を記録（PlayerID型）
        /// </summary>
        public void RecordPlayerPing(PlayerID playerId, double pingMs, bool packetLost = false)
        {
            if (playerId == null || playerId.IsNull) return;

            _lobbyLock.EnterReadLock();
            try
            {
                var stringId = _playerIdMapping.GetValueOrDefault(playerId);
                if (!string.IsNullOrEmpty(stringId) && _connectedPlayers.ContainsKey(stringId))
                {
                    _pingManager.RecordPing(playerId, pingMs, packetLost);
                }
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        /// <summary>
        /// プレイヤーのPing統計を取得
        /// </summary>
        public LobbyResult<PingStats> GetPlayerPingStats(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return LobbyResult<PingStats>.Error("Player ID cannot be empty");

            _lobbyLock.EnterReadLock();
            try
            {
                var pid = PlayerID.FromString(playerId);
                var tracker = _pingManager.GetTracker(pid);
                if (tracker == null)
                    return LobbyResult<PingStats>.Error("Player not found");

                var stats = tracker.GetStats();
                return LobbyResult<PingStats>.Success(stats);
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        /// <summary>
        /// プレイヤーのPing統計を取得（PlayerID型）
        /// </summary>
        public LobbyResult<PingStats> GetPlayerPingStats(PlayerID playerId)
        {
            if (playerId == null || playerId.IsNull)
                return LobbyResult<PingStats>.Error("Player ID cannot be null");

            _lobbyLock.EnterReadLock();
            try
            {
                var tracker = _pingManager.GetTracker(playerId);
                if (tracker == null)
                    return LobbyResult<PingStats>.Error("Player not found");

                var stats = tracker.GetStats();
                return LobbyResult<PingStats>.Success(stats);
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 全プレイヤーのPing統計を取得
        /// </summary>
        public LobbyResult<Dictionary<string, PingStats>> GetAllPlayerPingStats()
        {
            _lobbyLock.EnterReadLock();
            try
            {
                var stats = _pingManager.GetAllStats();
                
                // PlayerID → string に変換
                var stringStats = new Dictionary<string, PingStats>();
                foreach (var kvp in stats)
                {
                    var stringId = _playerIdMapping.GetValueOrDefault(kvp.Key);
                    if (!string.IsNullOrEmpty(stringId))
                    {
                        stringStats[stringId] = kvp.Value;
                    }
                }
                
                return LobbyResult<Dictionary<string, PingStats>>.Success(stringStats);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error getting ping stats: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<Dictionary<string, PingStats>>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 接続品質の悪いプレイヤーを取得
        /// </summary>
        public LobbyResult<List<(string PlayerId, PingStats Stats)>> GetPoorConnectionPlayers()
        {
            _lobbyLock.EnterReadLock();
            try
            {
                var poorPlayers = _pingManager.GetPoorQualityPlayers(NetworkQuality.Poor);
                
                // PlayerID → string に変換
                var result = new List<(string PlayerId, PingStats Stats)>();
                foreach (var (pid, stats) in poorPlayers)
                {
                    var stringId = _playerIdMapping.GetValueOrDefault(pid);
                    if (!string.IsNullOrEmpty(stringId))
                    {
                        result.Add((stringId, stats));
                    }
                }
                
                return LobbyResult<List<(string PlayerId, PingStats Stats)>>.Success(result);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error getting poor connection players: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<List<(string PlayerId, PingStats Stats)>>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        #endregion

        #region 統計情報とルーム取得

        public LobbyResult<List<LobbyRoomInfo>> GetAvailableRooms()
        {
            _lobbyLock.EnterReadLock();
            try
            {
                var rooms = _lobby.GetAvailableRooms();
                return LobbyResult<List<LobbyRoomInfo>>.Success(rooms);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error getting rooms: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<List<LobbyRoomInfo>>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        public LobbyResult<LobbyRoomInfo> GetRoomInfo(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return LobbyResult<LobbyRoomInfo>.Error("Room ID cannot be empty");

            _lobbyLock.EnterReadLock();
            try
            {
                var room = _lobby.GetRoom(roomId);
                if (room == null)
                    return LobbyResult<LobbyRoomInfo>.Error("Room not found");

                return LobbyResult<LobbyRoomInfo>.Success(room);
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        public LobbyResult<LobbyStats> GetLobbyStats()
        {
            _lobbyLock.EnterReadLock();
            try
            {
                var rooms = _lobby.GetAvailableRooms();
                var players = _lobby.GetPlayers();

                var stats = new LobbyStats
                {
                    TotalPlayers = players.Count,
                    ActiveRooms = rooms.Count,
                    RoomsByGameMode = rooms.GroupBy(r => r.GameMode)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    AveragePing = _pingManager.GetAllStats().Values.Any() 
                        ? _pingManager.GetAllStats().Values.Average(s => s.AveragePing) 
                        : 0
                };

                return LobbyResult<LobbyStats>.Success(stats);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error getting stats: {ex.Message}", ConsoleColor.Red);
                return LobbyResult<LobbyStats>.Error($"Internal error: {ex.Message}");
            }
            finally
            {
                _lobbyLock.ExitReadLock();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            ConsoleWrite.WriteMessage("[LOBBY] Disposing lobby server manager", ConsoleColor.Yellow);

            _cleanupTimer?.Dispose();
            _lobbyLock?.Dispose();

            foreach (var playerId in _connectedPlayers.Keys.ToList())
            {
                PlayerLeaveLobby(playerId);
            }

            _connectedPlayers.Clear();
            _rateLimiters.Clear();

            _disposed = true;
        }

        #endregion
    }

    /// <summary>
    /// ロビー統計情報
    /// </summary>
    public sealed class LobbyStats
    {
        public int TotalPlayers { get; set; }
        public int ActiveRooms { get; set; }
        public Dictionary<EGameMode, int> RoomsByGameMode { get; set; } = new();
        public double AveragePing { get; set; }
    }
}
