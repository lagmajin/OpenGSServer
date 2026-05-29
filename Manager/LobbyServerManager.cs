using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using NetCoreServer; // NetCoreServer追加
using OpenGSCore;
using OpenGSServer.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        private readonly ConcurrentDictionary<PlayerID, string> _playerIdMapping = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _pendingFriendRequests = new(StringComparer.OrdinalIgnoreCase);
        private readonly PingManager _pingManager = new();
        private readonly ReaderWriterLockSlim _lobbyLock = new();
        private readonly Timer _cleanupTimer;
        
        // NetCoreServer TCP機能
        private LobbyTcpServer? _tcpServer;
        
        // アイドルチェック用
        private readonly Timer _idleCheckTimer;

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

            // アイドルチェックタイマー（1分ごと）
            _idleCheckTimer = new Timer(
                callback: _ => CheckIdlePlayers(),
                state: null,
                dueTime: TimeSpan.FromMinutes(1),
                period: TimeSpan.FromMinutes(1)
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

        private void CheckIdlePlayers()
        {
            try
            {
                var now = DateTime.UtcNow;
                var idlePlayers = new List<string>();

                // 読み取りロックでスナップショットを作成
                _lobbyLock.EnterReadLock();
                try
                {
                    foreach (var kvp in _connectedPlayers)
                    {
                        var player = kvp.Value;
                        if ((now - player.LastActivity).TotalMinutes >= 15)
                        {
                            idlePlayers.Add(kvp.Key);
                        }
                    }
                }
                finally
                {
                    _lobbyLock.ExitReadLock();
                }

                // スナップショットに基づいて書き込み操作を行う（PlayerLeaveLobbyは内部でロックを取得する）
                foreach (var playerId in idlePlayers)
                {
                    ConsoleWrite.WriteMessage($"[LOBBY] Kicking idle player: {playerId}", ConsoleColor.Yellow);
                    PlayerLeaveLobby(playerId);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error in idle check: {ex.Message}", ConsoleColor.Red);
            }
        }

        private void UpdatePlayerActivity(string playerId)
        {
            if (_connectedPlayers.TryGetValue(playerId, out var player))
            {
                player.LastActivity = DateTime.UtcNow;
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
                    return LobbyResult<LobbyPlayerInfo>.Error("Failed to retrieve player info");

                playerInfo.LastActivity = DateTime.UtcNow; // 初期化
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
                UpdatePlayerActivity(ownerId); // アクティビティ更新
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
                UpdatePlayerActivity(playerId); // アクティビティ更新
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
                
                UpdatePlayerActivity(playerId); // アクティビティ更新
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

        #region TCP サーバー管理

        /// <summary>
        /// NetCoreServerを使用したTCPサーバーを起動
        /// </summary>
        public void StartTcpServer(int port)
        {
            if (_tcpServer != null)
            {
                ConsoleWrite.WriteMessage("[LOBBY] TCP server already running", ConsoleColor.Yellow);
                return;
            }

            try
            {
                _tcpServer = new LobbyTcpServer(IPAddress.Any, port, this);
                _tcpServer.Start();
                
                ConsoleWrite.WriteMessage($"[LOBBY] TCP server started on port {port}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Failed to start TCP server: {ex.Message}", ConsoleColor.Red);
                throw;
            }
        }

        /// <summary>
        /// TCPサーバーを停止
        /// </summary>
        public void StopTcpServer()
        {
            if (_tcpServer == null)
            {
                ConsoleWrite.WriteMessage("[LOBBY] TCP server not running", ConsoleColor.Yellow);
                return;
            }

            try
            {
                _tcpServer.Stop();
                _tcpServer.Dispose();
                _tcpServer = null;
                
                ConsoleWrite.WriteMessage("[LOBBY] TCP server stopped", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error stopping TCP server: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// TCPサーバーが起動しているか
        /// </summary>
        public bool IsTcpServerRunning => _tcpServer?.IsStarted ?? false;

        /// <summary>
        /// TCPポート番号を取得
        /// </summary>
        public int? TcpPort => _tcpServer?.Endpoint.Port;
        
        public void BroadcastToAllClients(string message)
        {
            _tcpServer?.Multicast(message);
        }

        /// <summary>
        /// ロビー内の全プレイヤーに通知を送信
        /// </summary>
        public void BroadcastToAllInLobby(JObject json)
        {
            if (_tcpServer == null || json == null) return;
            
            // ClientSession.SendAsyncJsonWithTimeStamp 形式に合わせて JS プレフィックスとデリミタを追加
            // ただし Multicast を使う場合はバイト列で一括送信したほうが効率的
            string message = "JS" + json.ToString(Formatting.None) + '\u001F';
            _tcpServer.Multicast(message);
        }

        /// <summary>
        /// 特定のプレイヤーにメッセージを送信
        /// </summary>
        public bool SendToPlayer(string playerId, JObject json)
        {
            if (_tcpServer == null || string.IsNullOrEmpty(playerId) || json == null) return false;

            // セッションを検索
            var session = _tcpServer.FindSessionByPlayerId(playerId);
            if (session != null)
            {
                return session.SendAsyncJsonWithTimeStamp(json);
            }

            return false;
        }

        public ClientSession? FindSessionByPlayerId(string playerId)
        {
            return _tcpServer?.FindSessionByPlayerId(playerId);
        }

        public ClientSession? GetSession(string playerId)
        {
            return FindSessionByPlayerId(playerId);
        }

        /// <summary>
        /// クライアントからのメッセージを処理（TCP専用）
        /// </summary>
        public void HandleMessage(string messageType, JObject data, ClientSession? session = null)
        {
            var clientSession = session as ClientSession; // キャスト
            switch (messageType)
            {
                case MessageType.LoginRequest:
                    // ログイン処理（既存の OldAccountEventHandler を移行）
                    var playerId = OldAccountEventHandler.Login(clientSession, data);
                    if (clientSession != null && playerId != null) clientSession.SetPlayerID(playerId);
                    break;
                case MessageType.LogoutRequest:
                    OldAccountEventHandler.Logout(clientSession);
                    break;
                case MessageType.CreateRoomRequest:
                    LobbyEventHandler.CreateNewWaitRoom(clientSession, data);
                    break;
                case MessageType.JoinRoomRequest:
                    LobbyEventHandler.EnterRoomRequest(clientSession, data);
                    break;
                case MessageType.MatchServerInfoRequest:
                    HandleMatchServerInfoRequest(clientSession, data);
                    break;
                case MessageType.RoomListUpdateRequest:
                    LobbyEventHandler.UpdateRoom(clientSession, data);
                    break;
                case MessageType.LeaveRoomRequest:
                    WaitRoomEventHandler.ExitRoomRequest(clientSession, data);
                    break;
                case MessageType.WaitRoomSettingsChange:
                    if (data.ContainsKey("RoomName") || data.ContainsKey("Capacity") || data.ContainsKey("GameMode"))
                    {
                        WaitRoomEventHandler.ChangeRoomSetting(clientSession, data);
                    }

                    if (data.ContainsKey("PlayerCharacter") || data.ContainsKey("EquipInstantItems"))
                    {
                        WaitRoomEventHandler.ChangePlayerSettting(clientSession, data);
                    }
                    break;
                case MessageType.WaitRoomPlayerReady:
                case MessageType.WaitRoomPlayerUnready:
                    WaitRoomEventHandler.ReadyRequest(clientSession, data);
                    break;
                case MessageType.LoadingStarted:
                case MessageType.ClientLoadingSceneEntered:
                    WaitRoomEventHandler.LoadingStartedRequest(clientSession, data);
                    break;
                case MessageType.LoadingProgress:
                    break;
                case MessageType.LoadingCompleted:
                    WaitRoomEventHandler.LoadingCompletedRequest(clientSession, data);
                    break;
                case MessageType.LobbyChatRequest:
                    // チャット処理
                    var playerIdChat = data["PlayerId"]?.ToString() ?? data["PlayerID"]?.ToString();
                    var message = data["Message"]?.ToString();
                    if (!string.IsNullOrEmpty(playerIdChat) && !string.IsNullOrEmpty(message))
                    {
                        AddLobbyChat(playerIdChat, message);
                    }
                    break;
                case MessageType.ShopStateRequest:
                    HandleShopStateRequest(clientSession, data);
                    break;
                case MessageType.ShopPurchaseRequest:
                    HandleShopPurchaseRequest(clientSession, data);
                    break;
                case MessageType.ShopEquipRequest:
                    HandleShopEquipRequest(clientSession, data);
                    break;
                case MessageType.ShopUnequipRequest:
                    HandleShopUnequipRequest(clientSession, data);
                    break;
                case MessageType.FriendRequest:
                    HandleFriendRequest(clientSession, data);
                    break;
                case MessageType.FriendApproveRequest:
                    HandleFriendApproveRequest(clientSession, data);
                    break;
                case MessageType.FriendListRequest:
                    HandleFriendListRequest(clientSession, data);
                    break;
                case MessageType.GuildListRequest:
                    HandleGuildListRequest(clientSession);
                    break;
                case MessageType.GuildInfoRequest:
                    HandleGuildInfoRequest(clientSession, data);
                    break;
                case MessageType.GuildCreateRequest:
                    HandleGuildCreateRequest(clientSession, data);
                    break;
                case MessageType.GuildJoinRequest:
                    HandleGuildJoinRequest(clientSession, data);
                    break;
                case MessageType.GuildLeaveRequest:
                    HandleGuildLeaveRequest(clientSession, data);
                    break;
                case MessageType.GuildInviteRequest:
                    HandleGuildInviteRequest(clientSession, data);
                    break;
                case MessageType.GuildKickRequest:
                    HandleGuildKickRequest(clientSession, data);
                    break;
                case MessageType.GuildChatRequest:
                    HandleGuildChatRequest(clientSession, data);
                    break;
                default:
                    ConsoleWrite.WriteMessage($"[LOBBY] Unknown message type: {messageType}", ConsoleColor.Yellow);
                    break;
            }
        }

        private void HandleMatchServerInfoRequest(ClientSession? session, JObject data)
        {
            if (session == null) return;

            var infoJson = new JObject();
            var matchServer = MatchServerV2.Instance; // Instanceプロパティを使用

            infoJson["MessageType"] = "MatchServerInformationNotification";
            infoJson["Port"] = matchServer.TcpPort; // TcpPortプロパティを使用
            infoJson["SubPort"] = 2000;

            var str = infoJson.ToString(Formatting.None);
            ConsoleWrite.WriteMessage(str);
            session.SendAsyncJsonWithTimeStamp(infoJson);
        }

        private void HandleShopStateRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var accountId = ResolveGuildPlayerId(session, data, "PlayerID", "PlayerId", "AccountID", "AccountId");
            if (string.IsNullOrWhiteSpace(accountId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ShopStateResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = "PlayerID is required"
                });
                return;
            }

            var db = AccountDatabaseManager.GetInstance();
            var account = db.GetAccount(accountId);
            if (account == null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ShopStateResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = $"Account '{accountId}' was not found"
                });
                return;
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.ShopStateResponse,
                ["Success"] = true,
                ["Credits"] = account.Credits,
                ["PurchasedItems"] = new JArray(db.GetPurchasedItems(accountId)),
                ["EquippedItems"] = new JArray((account.EquippedItems ?? new List<DBEquippedItem>()).Select(item => new JObject
                {
                    ["Category"] = item.Category,
                    ["ItemId"] = item.ItemId
                })),
                ["EquippedInstantItems"] = new JArray((account.EquippedInstantItems ?? new List<DBInstantEquippedItem>()).Select(item => new JObject
                {
                    ["Slot"] = item.Slot,
                    ["ItemId"] = item.ItemId
                }))
            });
        }

        private void HandleShopPurchaseRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var accountId = ResolveGuildPlayerId(session, data, "PlayerID", "PlayerId", "AccountID", "AccountId");
            var itemId = data?["ItemId"]?.ToString() ?? string.Empty;
            var price = data?["Price"]?.ToObject<long>() ?? 0;
            var db = AccountDatabaseManager.GetInstance();
            var account = db.GetAccount(accountId);

            if (account == null || string.IsNullOrWhiteSpace(itemId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ShopPurchaseResponse,
                    ["Success"] = false,
                    ["ItemId"] = itemId,
                    ["Credits"] = account?.Credits ?? 0,
                    ["ErrorMessage"] = "Account or item is invalid"
                });
                return;
            }

            if (account.Credits < price)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ShopPurchaseResponse,
                    ["Success"] = false,
                    ["ItemId"] = itemId,
                    ["Credits"] = account.Credits,
                    ["ErrorMessage"] = "Insufficient credits"
                });
                return;
            }

            account.Credits -= price;
            db.SetPurchasedItem(accountId, itemId, true);
            db.UpdateAccountData(account);

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.ShopPurchaseResponse,
                ["Success"] = true,
                ["ItemId"] = itemId,
                ["Credits"] = account.Credits
            });
        }

        private void HandleShopEquipRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var accountId = ResolveGuildPlayerId(session, data, "PlayerID", "PlayerId", "AccountID", "AccountId");
            var itemId = data?["ItemId"]?.ToString() ?? string.Empty;
            var category = data?["Category"]?.ToString() ?? "Weapon";
            var slot = data?["Slot"]?.ToObject<int>() ?? 0;
            var db = AccountDatabaseManager.GetInstance();

            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(itemId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ShopEquipResponse,
                    ["Success"] = false,
                    ["ItemId"] = itemId,
                    ["Category"] = category,
                    ["Slot"] = slot,
                    ["ErrorMessage"] = "AccountID and ItemId are required"
                });
                return;
            }

            if (!db.IsPurchased(accountId, itemId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ShopEquipResponse,
                    ["Success"] = false,
                    ["ItemId"] = itemId,
                    ["Category"] = category,
                    ["Slot"] = slot,
                    ["ErrorMessage"] = "Item has not been purchased"
                });
                return;
            }

            var success = category.Equals("InstantItem", StringComparison.OrdinalIgnoreCase)
                ? db.SetEquippedInstantItem(accountId, slot, itemId)
                : db.SetEquippedItem(accountId, category, itemId);

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.ShopEquipResponse,
                ["Success"] = success,
                ["ItemId"] = itemId,
                ["Category"] = category,
                ["Slot"] = slot,
                ["Credits"] = db.GetCredits(accountId)
            });
        }

        private void HandleShopUnequipRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var accountId = ResolveGuildPlayerId(session, data, "PlayerID", "PlayerId", "AccountID", "AccountId");
            var category = data?["Category"]?.ToString() ?? "Weapon";
            var slot = data?["Slot"]?.ToObject<int>() ?? 0;
            var db = AccountDatabaseManager.GetInstance();

            if (string.IsNullOrWhiteSpace(accountId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ShopEquipResponse,
                    ["Success"] = false,
                    ["Category"] = category,
                    ["Slot"] = slot,
                    ["ErrorMessage"] = "AccountID is required"
                });
                return;
            }

            var success = category.Equals("InstantItem", StringComparison.OrdinalIgnoreCase)
                ? db.SetEquippedInstantItem(accountId, slot, string.Empty)
                : db.SetEquippedItem(accountId, category, string.Empty);

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.ShopEquipResponse,
                ["Success"] = success,
                ["Category"] = category,
                ["Slot"] = slot
            });
        }

        private void HandleFriendRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var requesterId = ResolveGuildPlayerId(session, data, "PlayerID", "PlayerId", "RequesterId", "RequesterID");
            var targetPlayerId = ResolveGuildPlayerId(null, data, "TargetPlayerID", "TargetPlayerId", "FriendId", "FriendID");
            var db = AccountDatabaseManager.GetInstance();

            if (string.IsNullOrWhiteSpace(requesterId) || string.IsNullOrWhiteSpace(targetPlayerId) || string.Equals(requesterId, targetPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.FriendRequestResponse,
                    ["Success"] = false,
                    ["Error"] = "Invalid friend request"
                });
                return;
            }

            if (db.GetFriendIds(requesterId).Contains(targetPlayerId, StringComparer.OrdinalIgnoreCase))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.FriendRequestResponse,
                    ["Success"] = false,
                    ["Error"] = "Already friends",
                    ["TargetPlayerID"] = targetPlayerId
                });
                return;
            }

            var pending = _pendingFriendRequests.GetOrAdd(targetPlayerId, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
            var added = pending.TryAdd(requesterId, 0);
            var delivered = false;

            if (added)
            {
                delivered = SendToPlayer(targetPlayerId, new JObject
                {
                    ["MessageType"] = MessageType.FriendRequestNotification,
                    ["Success"] = true,
                    ["FromPlayerID"] = requesterId,
                    ["FromPlayerName"] = ResolvePlayerDisplayName(requesterId)
                });
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.FriendRequestResponse,
                ["Success"] = added,
                ["TargetPlayerID"] = targetPlayerId,
                ["Delivered"] = delivered,
                ["Error"] = added ? string.Empty : "Friend request already sent"
            });
        }

        private void HandleFriendApproveRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var approverId = ResolveGuildPlayerId(session, data, "PlayerID", "PlayerId", "ApproverId", "ApproverID");
            var requestPlayerId = ResolveGuildPlayerId(null, data, "RequestPlayerID", "RequestPlayerId", "FromPlayerID", "FromPlayerId");
            var approve = data?["Approve"]?.ToObject<bool>() ?? true;
            var db = AccountDatabaseManager.GetInstance();

            if (string.IsNullOrWhiteSpace(approverId) || string.IsNullOrWhiteSpace(requestPlayerId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.FriendApproveResponse,
                    ["Success"] = false,
                    ["Error"] = "Invalid approve request"
                });
                return;
            }

            if (!_pendingFriendRequests.TryGetValue(approverId, out var pending) || !pending.TryRemove(requestPlayerId, out _))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.FriendApproveResponse,
                    ["Success"] = false,
                    ["Error"] = "No pending friend request"
                });
                return;
            }

            if (approve)
            {
                db.AddFriend(approverId, requestPlayerId);
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.FriendApproveResponse,
                ["Success"] = true,
                ["Approved"] = approve,
                ["RequestPlayerID"] = requestPlayerId
            });
        }

        private void HandleFriendListRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var playerId = ResolveGuildPlayerId(session, data, "PlayerID", "PlayerId");
            var db = AccountDatabaseManager.GetInstance();
            var friendDetails = db.GetFriendDetails(playerId);
            var friends = new JArray(friendDetails.Select(friend => new JObject
            {
                ["PlayerID"] = friend.AccountId,
                ["PlayerName"] = friend.DisplayName,
                ["IsOnline"] = FindSessionByPlayerId(friend.AccountId) != null
            }));

            var pendingRequests = new JArray();
            if (_pendingFriendRequests.TryGetValue(playerId, out var pending))
            {
                foreach (var requesterId in pending.Keys.OrderBy(key => key))
                {
                    pendingRequests.Add(new JObject
                    {
                        ["FromPlayerID"] = requesterId,
                        ["FromPlayerName"] = ResolvePlayerDisplayName(requesterId)
                    });
                }
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.FriendListResponse,
                ["Success"] = true,
                ["PlayerID"] = playerId,
                ["Friends"] = friends,
                ["PendingRequests"] = pendingRequests
            });
        }

        private string ResolvePlayerDisplayName(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return string.Empty;
            }

            var account = AccountDatabaseManager.GetInstance().GetAccount(playerId);
            if (account != null && !string.IsNullOrWhiteSpace(account.DisplayName))
            {
                return account.DisplayName;
            }

            var lobbyPlayer = _connectedPlayers.TryGetValue(playerId, out var info) ? info : null;
            return lobbyPlayer?.PlayerName ?? playerId;
        }

        private void HandleGuildListRequest(ClientSession? session)
        {
            if (session == null)
            {
                return;
            }

            var manager = GuildManager.Instance;
            var guilds = manager.GetAllGuilds();
            var guildArray = new JArray();

            foreach (var guild in guilds.OrderBy(g => g.GuildName))
            {
                guildArray.Add(BuildGuildSummaryJson(guild, manager.GetGuildMembers(guild.GuildName)));
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.GuildListResponse,
                ["Success"] = true,
                ["Guilds"] = guildArray
            });
        }

        private void HandleGuildInfoRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var guildName = data?["GuildName"]?.ToString() ?? string.Empty;
            var manager = GuildManager.Instance;
            var guild = manager.FindGuild(guildName);
            if (guild == null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.GuildInfoResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = $"Guild '{guildName}' was not found",
                    ["GuildName"] = guildName
                });
                return;
            }

            var members = manager.GetGuildMembers(guild.GuildName);
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.GuildInfoResponse,
                ["Success"] = true,
                ["Guild"] = BuildGuildDetailJson(guild, members)
            });
        }

        private void HandleGuildCreateRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var guildName = data?["GuildName"]?.ToString() ?? string.Empty;
            var shortName = data?["GuildShortName"]?.ToString() ?? string.Empty;
            var leaderId = ResolveGuildPlayerId(session, data, "LeaderId", "LeaderID", "PlayerID", "PlayerId");

            var manager = GuildManager.Instance;
            var success = manager.CreateNewGuild(guildName, leaderId, shortName);
            if (!success)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.GuildCreateResponse,
                    ["Success"] = false,
                    ["ErrorMessage"] = $"Guild '{guildName}' could not be created"
                });
                return;
            }

            var guild = manager.FindGuild(guildName);
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.GuildCreateResponse,
                ["Success"] = true,
                ["Guild"] = BuildGuildDetailJson(guild, manager.GetGuildMembers(guildName))
            });
        }

        private void HandleGuildJoinRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var guildName = data?["GuildName"]?.ToString() ?? string.Empty;
            var memberId = ResolveGuildPlayerId(session, data, "MemberId", "MemberID", "PlayerID", "PlayerId");
            var role = data?["Role"]?.ToString() ?? "Member";

            var manager = GuildManager.Instance;
            var success = manager.JoinGuild(guildName, memberId, role);
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.GuildJoinResponse,
                ["Success"] = success,
                ["GuildName"] = guildName,
                ["MemberId"] = memberId,
                ["Role"] = role,
                ["Guild"] = success ? BuildGuildDetailJson(manager.FindGuild(guildName), manager.GetGuildMembers(guildName)) : null,
                ["ErrorMessage"] = success ? string.Empty : $"Failed to join guild '{guildName}'"
            });
        }

        private void HandleGuildLeaveRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var guildName = data?["GuildName"]?.ToString() ?? string.Empty;
            var memberId = ResolveGuildPlayerId(session, data, "MemberId", "MemberID", "PlayerID", "PlayerId");

            var manager = GuildManager.Instance;
            var success = manager.LeaveGuild(guildName, memberId);
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.GuildLeaveResponse,
                ["Success"] = success,
                ["GuildName"] = guildName,
                ["MemberId"] = memberId,
                ["ErrorMessage"] = success ? string.Empty : $"Failed to leave guild '{guildName}'"
            });
        }

        private void HandleGuildInviteRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var guildName = data?["GuildName"]?.ToString() ?? string.Empty;
            var inviterId = ResolveGuildPlayerId(session, data, "InviterId", "InviterID", "PlayerID", "PlayerId");
            var targetPlayerId = ResolveGuildPlayerId(null, data, "TargetPlayerId", "TargetPlayerID", "MemberId", "MemberID");
            var message = data?["Message"]?.ToString() ?? string.Empty;

            var manager = GuildManager.Instance;
            var guild = manager.FindGuild(guildName);
            var canInvite = guild != null && manager.CanInviteGuildMember(guildName, targetPlayerId);
            var delivered = false;

            if (canInvite && !string.IsNullOrWhiteSpace(targetPlayerId))
            {
                var notification = new JObject
                {
                    ["MessageType"] = MessageType.GuildInviteNotification,
                    ["Success"] = true,
                    ["GuildName"] = guildName,
                    ["InviterId"] = inviterId,
                    ["TargetPlayerId"] = targetPlayerId,
                    ["Message"] = message,
                    ["Timestamp"] = DateTime.UtcNow.ToString("o")
                };

                delivered = SendToPlayer(targetPlayerId, notification);
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.GuildInviteResponse,
                ["Success"] = canInvite,
                ["GuildName"] = guildName,
                ["TargetPlayerId"] = targetPlayerId,
                ["InviterId"] = inviterId,
                ["Delivered"] = delivered,
                ["ErrorMessage"] = canInvite ? string.Empty : $"Failed to invite '{targetPlayerId}' to guild '{guildName}'"
            });
        }

        private void HandleGuildKickRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var guildName = data?["GuildName"]?.ToString() ?? string.Empty;
            var kickerId = ResolveGuildPlayerId(session, data, "KickerId", "KickerID", "PlayerID", "PlayerId");
            var memberId = ResolveGuildPlayerId(null, data, "MemberId", "MemberID", "TargetPlayerId", "TargetPlayerID");

            var manager = GuildManager.Instance;
            var success = manager.KickGuildMember(guildName, memberId);
            if (success && !string.IsNullOrWhiteSpace(memberId))
            {
                SendToPlayer(memberId, new JObject
                {
                    ["MessageType"] = MessageType.GuildKickNotification,
                    ["Success"] = true,
                    ["GuildName"] = guildName,
                    ["KickerId"] = kickerId,
                    ["MemberId"] = memberId,
                    ["Timestamp"] = DateTime.UtcNow.ToString("o")
                });
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.GuildKickResponse,
                ["Success"] = success,
                ["GuildName"] = guildName,
                ["MemberId"] = memberId,
                ["KickerId"] = kickerId,
                ["ErrorMessage"] = success ? string.Empty : $"Failed to kick '{memberId}' from guild '{guildName}'"
            });
        }

        private void HandleGuildChatRequest(ClientSession? session, JObject data)
        {
            if (session == null)
            {
                return;
            }

            var guildName = data?["GuildName"]?.ToString() ?? string.Empty;
            var senderId = ResolveGuildPlayerId(session, data, "SenderId", "SenderID", "PlayerID", "PlayerId");
            var message = data?["Message"]?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(guildName) || string.IsNullOrWhiteSpace(message))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ErrorNotification,
                    ["Success"] = false,
                    ["ErrorMessage"] = "Guild name and message are required"
                });
                return;
            }

            var manager = GuildManager.Instance;
            if (!manager.Exist(guildName))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ErrorNotification,
                    ["Success"] = false,
                    ["ErrorMessage"] = $"Guild '{guildName}' was not found"
                });
                return;
            }

            var members = manager.GetGuildMembers(guildName);
            if (!members.Any(member => string.Equals(member.Id, senderId, StringComparison.OrdinalIgnoreCase)))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.ErrorNotification,
                    ["Success"] = false,
                    ["ErrorMessage"] = $"Player '{senderId}' is not a member of guild '{guildName}'"
                });
                return;
            }

            manager.BroadcastGuildChat(guildName, senderId, message);
        }

        private static string ResolveGuildPlayerId(ClientSession? session, JObject? data, params string[] candidateKeys)
        {
            if (data != null)
            {
                foreach (var key in candidateKeys)
                {
                    var value = data[key]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            if (session != null && !string.IsNullOrWhiteSpace(session.PlayerID))
            {
                return session.PlayerID;
            }

            return string.Empty;
        }

        private static JObject BuildGuildSummaryJson(DBGuild guild, IEnumerable<DBGuildMember> members)
        {
            var memberList = members?.ToList() ?? new List<DBGuildMember>();
            return new JObject
            {
                ["Id"] = guild.id,
                ["GuildName"] = guild.GuildName,
                ["GuildShortName"] = guild.GuildShortName,
                ["LeaderId"] = guild.LeaderId,
                ["Level"] = guild.Level,
                ["Experience"] = guild.Experience,
                ["CreationTime"] = guild.CreationTime,
                ["MemberCount"] = memberList.Count
            };
        }

        private static JObject BuildGuildDetailJson(DBGuild guild, IEnumerable<DBGuildMember> members)
        {
            var memberList = members?.ToList() ?? new List<DBGuildMember>();
            var memberArray = new JArray();

            foreach (var member in memberList.OrderBy(m => string.Equals(m.Role, "Leader", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                                             .ThenBy(m => m.Id))
            {
                memberArray.Add(new JObject
                {
                    ["MemberId"] = member.Id,
                    ["Role"] = member.Role,
                    ["TimeStamp"] = member.TimeStamp
                });
            }

            return new JObject
            {
                ["Id"] = guild.id,
                ["GuildName"] = guild.GuildName,
                ["GuildShortName"] = guild.GuildShortName,
                ["LeaderId"] = guild.LeaderId,
                ["Level"] = guild.Level,
                ["Experience"] = guild.Experience,
                ["CreationTime"] = guild.CreationTime,
                ["MemberCount"] = memberList.Count,
                ["Members"] = memberArray
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            ConsoleWrite.WriteMessage("[LOBBY] Disposing lobby server manager", ConsoleColor.Yellow);

            // TCPサーバー停止
            StopTcpServer();

            _cleanupTimer?.Dispose();
            _idleCheckTimer?.Dispose();
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

    /// <summary>
    /// ロビーTCPサーバー（NetCoreServer実装）
    /// LobbyServerManagerと統合
    /// </summary>
    public sealed class LobbyTcpServer : TcpServer
    {
        private readonly LobbyServerManager _manager;

        public LobbyTcpServer(IPAddress address, int port, LobbyServerManager manager) 
            : base(address, port)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            ConsoleWrite.WriteMessage($"[LOBBY] TCP Server initialized on port {port}", ConsoleColor.Green);
        }

        protected override TcpSession CreateSession()
        {
            return new ClientSession(this);
        }

        protected override void OnConnected(TcpSession session)
        {
            if (session is ClientSession clientSession)
            {
                var ip = clientSession.Socket.RemoteEndPoint;
                ConsoleWrite.WriteMessage($"[LOBBY] Client connected from {ip}", ConsoleColor.Cyan);
            }
        }

        protected override void OnDisconnected(TcpSession session)
        {
            if (session is ClientSession clientSession)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Client disconnected", ConsoleColor.Yellow);
            }
        }

        protected override void OnError(SocketError error)
        {
            ConsoleWrite.WriteMessage($"[LOBBY] TCP Server error: {error}", ConsoleColor.Red);
        }

        /// <summary>
        /// LobbyServerManagerへのアクセス
        /// </summary>
        public LobbyServerManager Manager => _manager;

        /// <summary>
        /// プレイヤーIDでセッションを検索
        /// </summary>
        public ClientSession? FindSessionByPlayerId(string playerId)
        {
            foreach (var session in Sessions.Values)
            {
                if (session is ClientSession clientSession && clientSession.PlayerID == playerId)
                {
                    return clientSession;
                }
            }
            return null;
        }
    }
}
