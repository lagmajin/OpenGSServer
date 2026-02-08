using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
            ConsoleWrite.WriteMessage($"[INFO] Management Server initialized... Port:{port}", ConsoleColor.Green);
        }

        /// <summary>
        /// セッションを作成
        /// </summary>
        protected override TcpSession CreateSession()
        {
            return new ManagementServerSession(this);
        }

        /// <summary>
        /// クライアント接続時の処理
        /// </summary>
        protected override void OnConnected(TcpSession session)
        {
            if (session is ManagementServerSession mgmtSession)
            {
                _connectedSessions.TryAdd(session.Id, mgmtSession);
                ConsoleWrite.WriteMessage(
                    $"[Management] クライアント接続: {mgmtSession.ClientIpAddress()} (ID: {session.Id})", 
                    ConsoleColor.Green);
            }
        }

        /// <summary>
        /// クライアント切断時の処理
        /// </summary>
        protected override void OnDisconnected(TcpSession session)
        {
            _connectedSessions.TryRemove(session.Id, out _);
            ConsoleWrite.WriteMessage($"[Management] クライアント切断: {session.Id}", ConsoleColor.Yellow);
        }

        /// <summary>
        /// エラー処理
        /// </summary>
        protected override void OnError(SocketError error)
        {
            ConsoleWrite.WriteMessage($"[Management] エラー発生: {error}", ConsoleColor.Red);
        }

        /// <summary>
        /// すべてのクライアントにメッセージを送信
        /// </summary>
        public void SendToAllClients(string message)
        {
            foreach (var session in _connectedSessions.Values)
            {
                session?.SendAsync(message);
            }
        }

        /// <summary>
        /// すべてのクライアントにJSONメッセージを送信
        /// </summary>
        public void BroadcastJson(JObject json)
        {
            if (json == null)
                return;

            string message = json.ToString() + "\n";
            SendToAllClients(message);
        }

        /// <summary>
        /// 特定のクライアントにメッセージを送信
        /// </summary>
        public bool SendToClient(Guid sessionId, string message)
        {
            if (_connectedSessions.TryGetValue(sessionId, out var session))
            {
                return session.SendAsync(message);
            }
            return false;
        }

        /// <summary>
        /// 接続中のセッション数を取得
        /// </summary>
        public int GetConnectedSessionCount()
        {
            return _connectedSessions.Count;
        }

        /// <summary>
        /// 特定のセッションを切断
        /// </summary>
        public bool DisconnectSession(Guid sessionId)
        {
            if (_connectedSessions.TryRemove(sessionId, out var session))
            {
                session?.Disconnect();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 管理サーバーの主要クラス - TCP管理機能を提供
    /// </summary>
    public sealed class ManagementServer : AbstractServer, IManagementServer, IDisposable
    {
        private ManagementTcpServer? _tcpServer;
        private CancellationTokenSource? _updateCts;
        private Task? _updateTask;

        public static ManagementServer Instance { get; } = new();

        public ManagementServer() : base(false)
        {
        }

        /// <summary>
        /// 管理サーバーの起動（ポート指定）
        /// </summary>
        public void Listen(int port)
        {
            if (_tcpServer != null)
            {
                ConsoleWrite.WriteMessage("[Management] サーバーは既に実行中です", ConsoleColor.Yellow);
                return;
            }

            try
            {
                _tcpServer = new ManagementTcpServer(IPAddress.Any, port, this);
                _tcpServer.Start();

                ConsoleWrite.WriteMessage($"[Management] サーバー起動成功 (ポート: {port})", ConsoleColor.Green);
                ConsoleWrite.WriteMessage("[Management] クライアント接続待機中...", ConsoleColor.Green);

                // 更新タスクの開始
                StartUpdateTask();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Management] サーバー起動失敗: {ex.Message}", ConsoleColor.Red);
                _tcpServer?.Dispose();
                _tcpServer = null;
            }
        }

        /// <summary>
        /// 管理サーバーの起動（デフォルトポート）
        /// </summary>
        public override void Listen()
        {
            Listen(45455); // デフォルトポート
        }

        /// <summary>
        /// 更新タスクの開始
        /// </summary>
        private void StartUpdateTask()
        {
            _updateCts = new CancellationTokenSource();
            _updateTask = Task.Run(() => UpdateLoop(_updateCts.Token));
        }

        /// <summary>
        /// 定期更新ループ
        /// </summary>
        private async Task UpdateLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Update();
                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ConsoleWrite.WriteMessage($"[Management] 更新ループエラー: {ex.Message}", ConsoleColor.Red);
                }
            }
        }

        /// <summary>
        /// 定期的な状態更新（例：接続中のクライアント情報）
        /// </summary>
        protected override void Update()
        {
            if (_tcpServer == null)
                return;

            // 定期的に接続情報をログ出力
            var sessionCount = _tcpServer.GetConnectedSessionCount();
            if (sessionCount > 0)
            {
                // ここでサーバー統計情報を更新可能
            }
        }

        /// <summary>
        /// 管理サーバーの停止
        /// </summary>
        public void Stop()
        {
            StopUpdateTask();

            _tcpServer?.Stop();
            _tcpServer?.Dispose();
            _tcpServer = null;

            ConsoleWrite.WriteMessage("[Management] サーバー停止完了", ConsoleColor.Yellow);
        }

        /// <summary>
        /// 更新タスクの停止
        /// </summary>
        private void StopUpdateTask()
        {
            _updateCts?.Cancel();
            try
            {
                _updateTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // キャンセル例外を無視
            }
            _updateTask?.Dispose();
            _updateCts?.Dispose();
        }

        /// <summary>
        /// 管理サーバーのシャットダウン
        /// </summary>
        public override void Shutdown()
        {
            Stop();
        }

        /// <summary>
        /// すべてのクライアントにサーバー状態をブロードキャスト
        /// </summary>
        public void BroadcastStatus(JObject status)
        {
            if (_tcpServer != null)
            {
                _tcpServer.BroadcastJson(status);
            }
        }

        /// <summary>
        /// サーバー統計情報をブロードキャスト
        /// </summary>
        public void BroadcastServerStats()
        {
            var stats = new JObject
            {
                ["MessageType"] = "ServerStats",
                ["Timestamp"] = DateTime.UtcNow.ToString("O"),
                ["ConnectedSessions"] = _tcpServer?.GetConnectedSessionCount() ?? 0,
                ["LoggedInUsers"] = AccountManager.GetInstance().GetLoggedInUserCount()
            };

            BroadcastStatus(stats);
        }

        /// <summary>
        /// 接続中のクライアント数を取得
        /// </summary>
        public int GetConnectedClientCount()
        {
            return _tcpServer?.GetConnectedSessionCount() ?? 0;
        }

        /// <summary>
        /// 特定のクライアントにメッセージを送信
        /// </summary>
        public bool SendToClient(Guid sessionId, string message)
        {
            return _tcpServer?.SendToClient(sessionId, message) ?? false;
        }

        /// <summary>
        /// すべてのクライアントにメッセージをブロードキャスト
        /// </summary>
        public void BroadcastMessage(string message)
        {
            _tcpServer?.SendToAllClients(message);
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            Stop();
            _tcpServer?.Dispose();
            _updateCts?.Dispose();
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~ManagementServer()
        {
            Dispose();
        }
    }

    /// <summary>
    /// 管理サーバー用インターフェース
    /// </summary>
    public interface IManagementServer
    {
        void Listen(int port);
        void Stop();
        void Shutdown();
        void BroadcastStatus(JObject status);
        int GetConnectedClientCount();
        bool SendToClient(Guid sessionId, string message);
        void BroadcastMessage(string message);
    }
}
