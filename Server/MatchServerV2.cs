using NetCoreServer;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Timers;
using OpenGSCore; // OpenGSCoreのMatchRoom使用

namespace OpenGSServer
{
    /// <summary>
    /// MatchサーバーのTCP実装
    /// NetCoreServerを使用した高性能実装
    /// </summary>
    public class MatchTcpServer : TcpServer
    {
        private int _serverFrameCount;

        public int ServerFrameCount => _serverFrameCount;

        public MatchTcpServer(IPAddress address, int port) : base(address, port)
        {
            ConsoleWrite.WriteMessage($"[Match] TCP Server initialized on port {port}", ConsoleColor.Green);
        }

        protected override TcpSession CreateSession()
        {
            return new ClientSession(this);
        }

        protected override void OnError(SocketError error)
        {
            ConsoleWrite.WriteMessage($"[Match] TCP Server error: {error}", ConsoleColor.Red);
        }

        /// <summary>
        /// フレームカウントをインクリメント
        /// </summary>
        public void IncrementFrame()
        {
            _serverFrameCount++;
        }

        /// <summary>
        /// 全クライアントにブロードキャスト
        /// </summary>
        public void BroadcastToAll(byte[] data)
        {
            Multicast(data);
        }
    }

    /// <summary>
    /// MatchServerV2 - ゲームマッチサーバー
    /// C# 14.0: OpenGSCore.MatchRoomを使用
    /// </summary>
    public sealed class MatchServerV2 : IDisposable
    {
        private static readonly MatchServerV2 _instance = new();
        public static MatchServerV2 Instance => _instance;

        private MatchTcpServer? _tcpServer;
        private MatchUDPServer? _udpServer;
        private TickTimer? _gameLoopTimer;
        private readonly Stopwatch _performanceTimer = new();
        private bool _disposed;

        // パフォーマンス統計
        private int _totalFrames;
        private double _averageFrameTime;
        private readonly object _statsLock = new();

        public bool IsRunning { get; private set; }
        public int? TcpPort => _tcpServer?.Endpoint.Port;
        public double AverageFrameTime => _averageFrameTime;

        private MatchServerV2()
        {
            ConsoleWrite.WriteMessage("[Match] Server initializing...", ConsoleColor.Cyan);
            InitializeGameLoop();
        }

        /// <summary>
        /// ゲームループを初期化（40ms = 25Hz）
        /// </summary>
        private void InitializeGameLoop()
        {
            _gameLoopTimer = new TickTimer(GameLoopCallback, 40);
            ConsoleWrite.WriteMessage("[Match] Game loop initialized (25Hz)", ConsoleColor.Green);
        }

        /// <summary>
        /// TCP/UDPサーバーを起動
        /// </summary>
        public void Listen(int tcpPort, int udpPort)
        {
            if (IsRunning)
            {
                ConsoleWrite.WriteMessage("[Match] Server already running", ConsoleColor.Yellow);
                return;
            }

            try
            {
                // TCPサーバー起動
                _tcpServer = new MatchTcpServer(IPAddress.Any, tcpPort);
                _tcpServer.Start();
                ConsoleWrite.WriteMessage($"[Match] TCP Server started on port {tcpPort}", ConsoleColor.Green);

                // UDPサーバー起動
                _udpServer = new MatchUDPServer();
                _udpServer.Listen(udpPort);
                ConsoleWrite.WriteMessage($"[Match] UDP Server started on port {udpPort}", ConsoleColor.Green);

                // ゲームループ開始
                _gameLoopTimer?.Start();
                IsRunning = true;

                ConsoleWrite.WriteMessage("[Match] Server fully operational", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Match] Failed to start server: {ex.Message}", ConsoleColor.Red);
                throw;
            }
        }

        /// <summary>
        /// メインゲームループコールバック（25Hz）
        /// </summary>
        private void GameLoopCallback(object? state)
        {
            _performanceTimer.Restart();

            try
            {
                // MatchRoomManagerから全ルームを取得
                var matchRoomManager = MatchRoomManager.Instance;
                var allRooms = matchRoomManager.AllRooms();

                // 各ルームのゲーム更新
                foreach (var room in allRooms)
                {
                    if (room is MatchRoom matchRoom)
                    {
                        UpdateMatchRoom(matchRoom);
                    }
                }

                // UDPイベントをポーリング
                _udpServer?.PollingEvent();

                // TCPフレームカウント更新
                _tcpServer?.IncrementFrame();
                _totalFrames++;
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Match] Game loop error: {ex.Message}", ConsoleColor.Red);
            }

            _performanceTimer.Stop();
            UpdatePerformanceStats(_performanceTimer.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// MatchRoomの更新処理
        /// </summary>
        private void UpdateMatchRoom(MatchRoom room)
        {
            if (room.Playing && !room.Finished)
            {
                // ゲームシーン更新（OpenGSCore.MatchRoom使用）
                room.GameUpdate();

                // ゲーム状態をクライアントに送信（必要に応じて）
                // BroadcastRoomState(room);
            }
        }

        /// <summary>
        /// マルチコア対応ゲームループ（オプション）
        /// </summary>
        private void MultiCoreGameLoopCallback(object? state)
        {
            _performanceTimer.Restart();

            try
            {
                var matchRoomManager = MatchRoomManager.Instance;
                var allRooms = matchRoomManager.AllRooms();

                // 並列処理でルーム更新
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                Parallel.ForEach(allRooms, options, room =>
                {
                    if (room is MatchRoom matchRoom)
                    {
                        UpdateMatchRoom(matchRoom);
                    }
                });

                _udpServer?.PollingEvent();
                _tcpServer?.IncrementFrame();
                _totalFrames++;
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Match] Multi-core loop error: {ex.Message}", ConsoleColor.Red);
            }

            _performanceTimer.Stop();
            UpdatePerformanceStats(_performanceTimer.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// パフォーマンス統計を更新
        /// </summary>
        private void UpdatePerformanceStats(double frameTimeMs)
        {
            lock (_statsLock)
            {
                // 移動平均を計算
                _averageFrameTime = (_averageFrameTime * 0.95) + (frameTimeMs * 0.05);

                // 警告：フレーム時間が40msを超えた場合
                if (frameTimeMs > 40)
                {
                    ConsoleWrite.WriteMessage(
                        $"[Match] Frame time exceeded: {frameTimeMs:F2}ms (target: 40ms)", 
                        ConsoleColor.Yellow);
                }
            }
        }

        /// <summary>
        /// マルチコアモードに切り替え
        /// </summary>
        public void EnableMultiCore()
        {
            if (!IsRunning)
            {
                ConsoleWrite.WriteMessage("[Match] Server not running", ConsoleColor.Yellow);
                return;
            }

            _gameLoopTimer?.Stop();
            _gameLoopTimer = new TickTimer(MultiCoreGameLoopCallback, 40);
            _gameLoopTimer.Start();

            ConsoleWrite.WriteMessage("[Match] Multi-core mode enabled", ConsoleColor.Cyan);
        }

        /// <summary>
        /// サーバー統計を取得
        /// </summary>
        public ServerStats GetStats()
        {
            lock (_statsLock)
            {
                var matchRoomManager = MatchRoomManager.Instance;
                var allRooms = matchRoomManager.AllRooms();

                return new ServerStats
                {
                    TotalFrames = _totalFrames,
                    AverageFrameTime = _averageFrameTime,
                    ActiveRooms = allRooms.Count,
                    PlayingRooms = allRooms.Count(r => r is MatchRoom m && m.Playing),
                    TotalPlayers = allRooms.Sum(r => r.Players.Count),
                    IsRunning = IsRunning
                };
            }
        }

        /// <summary>
        /// サーバーをシャットダウン
        /// </summary>
        public void Shutdown()
        {
            if (!IsRunning)
            {
                ConsoleWrite.WriteMessage("[Match] Server not running", ConsoleColor.Yellow);
                return;
            }

            ConsoleWrite.WriteMessage("[Match] Server shutting down...", ConsoleColor.Yellow);

            _gameLoopTimer?.Stop();
            _udpServer?.Shutdown();
            _tcpServer?.Stop();

            IsRunning = false;

            ConsoleWrite.WriteMessage("[Match] Server shutdown complete", ConsoleColor.Green);
        }

        public void Dispose()
        {
            if (_disposed) return;

            Shutdown();

            _gameLoopTimer?.Stop();
            // TickTimerにDisposeがない場合はStopのみ
            _tcpServer?.Dispose();
            _udpServer?.Dispose();
            _performanceTimer?.Stop();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// サーバー統計情報
    /// </summary>
    public readonly record struct ServerStats
    {
        public required int TotalFrames { get; init; }
        public required double AverageFrameTime { get; init; }
        public required int ActiveRooms { get; init; }
        public required int PlayingRooms { get; init; }
        public required int TotalPlayers { get; init; }
        public required bool IsRunning { get; init; }

        public override string ToString() =>
            $"Frames: {TotalFrames}, AvgTime: {AverageFrameTime:F2}ms, " +
            $"Rooms: {PlayingRooms}/{ActiveRooms}, Players: {TotalPlayers}";
    }
}
