using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetCoreServer;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    /// <summary>
    /// 管理サーバーのTCPサーバー実装
    /// </summary>
    public sealed class ManagementTcpServer : TcpServer
    {
        private readonly ManagementServer _manager;
        private readonly ConcurrentDictionary<Guid, ManagementServerSession> _connectedSessions = new();

        public ManagementTcpServer(IPAddress address, int port, ManagementServer manager) 
            : base(address, port)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        protected override TcpSession CreateSession() => new ManagementServerSession(this);

        protected override void OnConnected(TcpSession session)
        {
            if (session is ManagementServerSession mgmtSession)
            {
                _connectedSessions.TryAdd(session.Id, mgmtSession);
                ConsoleWrite.WriteMessage($"[Management] Master connection: {mgmtSession.ClientIpAddress()} (ID: {session.Id})", ConsoleColor.Green);
            }
        }

        protected override void OnDisconnected(TcpSession session)
        {
            _connectedSessions.TryRemove(session.Id, out _);
            ConsoleWrite.WriteMessage($"[Management] Master disconnected: {session.Id}", ConsoleColor.Yellow);
        }

        protected override void OnError(SocketError error)
        {
            ConsoleWrite.WriteMessage($"[Management] Socket Error: {error}", ConsoleColor.Red);
        }

        public void SendToAllClients(string message)
        {
            foreach (var session in _connectedSessions.Values)
            {
                session?.SendAsync(message);
            }
        }

        public void BroadcastJson(JObject json)
        {
            if (json == null) return;
            string message = json.ToString(Newtonsoft.Json.Formatting.None) + "\n";
            SendToAllClients(message);
        }

        public bool SendToClient(Guid sessionId, string message)
        {
            if (_connectedSessions.TryGetValue(sessionId, out var session))
            {
                return session.SendAsync(message);
            }
            return false;
        }

        public int GetConnectedSessionCount() => _connectedSessions.Count;
    }

    /// <summary>
    /// 管理サーバーの主要クラス - 監視とリモートコマンドを提供
    /// </summary>
    public sealed class ManagementServer : AbstractServer, IManagementServer, IDisposable
    {
        private ManagementTcpServer? _tcpServer;
        private CancellationTokenSource? _updateCts;
        private Task? _updateTask;
        private DateTime _lastStatsBroadcast = DateTime.MinValue;
        private readonly Process _currentProcess = Process.GetCurrentProcess();

        public static ManagementServer Instance { get; } = new();

        public ManagementServer() : base(false)
        {
        }

        public void Listen(int port)
        {
            if (_tcpServer != null) return;

            try
            {
                _tcpServer = new ManagementTcpServer(IPAddress.Any, port, this);
                _tcpServer.Start();
                ConsoleWrite.WriteMessage($"[Management] Monitoring Server active on port {port}", ConsoleColor.Cyan);
                StartUpdateTask();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Management] Failed to start management server: {ex.Message}", ConsoleColor.Red);
            }
        }

        public override void Listen() => Listen(45455);

        private void StartUpdateTask()
        {
            _updateCts = new CancellationTokenSource();
            _updateTask = Task.Run(() => UpdateLoop(_updateCts.Token));
        }

        private async Task UpdateLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Update();
                    await Task.Delay(1000, cancellationToken); // 1秒おきにチェック
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { ConsoleWrite.WriteMessage($"[Management] loop error: {ex.Message}", ConsoleColor.Red); }
            }
        }

        protected override void Update()
        {
            if (_tcpServer == null || _tcpServer.GetConnectedSessionCount() == 0) return;

            // 5秒おきに統計をブロードキャスト
            if ((DateTime.UtcNow - _lastStatsBroadcast).TotalSeconds >= 5)
            {
                BroadcastServerStats();
                _lastStatsBroadcast = DateTime.UtcNow;
            }
        }

        public void Stop()
        {
            _updateCts?.Cancel();
            _tcpServer?.Stop();
            _tcpServer?.Dispose();
            _tcpServer = null;
        }

        public override void Shutdown() => Stop();

        public void BroadcastStatus(JObject status) => _tcpServer?.BroadcastJson(status);

        public void BroadcastServerStats()
        {
            if (_tcpServer == null) return;

            var stats = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.ServerStats,
                ["Timestamp"] = DateTime.UtcNow.ToString("O"),
                ["ServerUptime"] = (DateTime.Now - _currentProcess.StartTime).ToString(@"dd\.hh\:mm\:ss"),
                ["MemoryUsageMB"] = _currentProcess.WorkingSet64 / (1024 * 1024),
                ["CpuThreads"] = _currentProcess.Threads.Count,
                ["ConnectedMasters"] = _tcpServer.GetConnectedSessionCount(),
                ["LoggedInUsers"] = AccountManager.GetInstance().GetLoggedInUserCount(),
                ["ActiveRooms"] = WaitRoomManager.Instance().GetAllRooms().Count,
                ["OS"] = RuntimeInformation.OSDescription
            };

            BroadcastStatus(stats);
        }

        public int GetConnectedClientCount() => _tcpServer?.GetConnectedSessionCount() ?? 0;

        public bool SendToClient(Guid sessionId, string message) => _tcpServer?.SendToClient(sessionId, message) ?? false;

        public void BroadcastMessage(string message) => _tcpServer?.SendToAllClients(message);

        /// <summary>
        /// 全プレイヤーへの緊急放送
        /// </summary>
        public void SystemAnnouncement(string message)
        {
            var announcement = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.Chat,
                ["ChatType"] = "System",
                ["Message"] = $"[SYSTEM ANNOUNCEMENT] {message}",
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            LobbyServerManager.Instance.BroadcastToAllInLobby(announcement);
            ConsoleWrite.WriteMessage($"[SYSTEM] Announcement: {message}", ConsoleColor.Yellow);
        }

        public void Dispose()
        {
            Stop();
            _currentProcess.Dispose();
            _updateCts?.Dispose();
        }

        ~ManagementServer() => Dispose();
    }

    public interface IManagementServer
    {
        void Listen(int port);
        void Stop();
        void Shutdown();
        void BroadcastStatus(JObject status);
        int GetConnectedClientCount();
        bool SendToClient(Guid sessionId, string message);
        void BroadcastMessage(string message);
        void SystemAnnouncement(string message);
    }
}
