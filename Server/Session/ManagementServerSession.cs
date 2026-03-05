using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetCoreServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    /// <summary>
    /// 管理サーバーのセッション管理クラス
    /// </summary>
    public sealed class ManagementServerSession : TcpSession
    {
        private string _clientId = "";
        private string _clientIp = "";
        private string _adminId = "";
        private bool _isAuthenticated = false;
        private DateTime _sessionStartTime = DateTime.UtcNow;
        private readonly StringBuilder _receiveBuffer = new();

        private const string UTF_FORMAT = "yyyy-MM-dd HH:mm:ss.ffff";
        private readonly char _unitSeparatorChar = (char)0x1F;

        public ManagementServerSession(TcpServer server) : base(server) { }

        /// <summary>
        /// クライアントのIPアドレスを取得
        /// </summary>
        public string ClientIpAddress()
        {
            return _clientIp;
        }

        /// <summary>
        /// セッションIDを取得
        /// </summary>
        public string GetSessionId()
        {
            return Id.ToString();
        }

        /// <summary>
        /// 管理者IDを取得
        /// </summary>
        public string GetAdminId()
        {
            return _adminId;
        }

        /// <summary>
        /// 認証状態を確認
        /// </summary>
        public bool IsAuthenticated()
        {
            return _isAuthenticated;
        }

        /// <summary>
        /// セッション開始時刻を取得
        /// </summary>
        public DateTime GetSessionStartTime()
        {
            return _sessionStartTime;
        }

        /// <summary>
        /// タイムスタンプ付きでPingを送信
        /// </summary>
        public bool SendPing()
        {
            var json = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.Ping,
                ["ServerTimeStamp"] = DateTime.UtcNow.ToString(UTF_FORMAT)
            };

            return SendJsonAsyncWithTimeStamp(json);
        }

        /// <summary>
        /// JSONメッセージをタイムスタンプ付きで送信
        /// </summary>
        public bool SendJsonAsyncWithTimeStamp(JObject obj)
        {
            if (obj == null)
                return false;

            obj["ServerTimeStamp"] = DateTime.UtcNow.ToString(UTF_FORMAT);
            var str = obj.ToString() + "\n";

            ConsoleWrite.WriteMessage($"[Management] 送信: {str}", ConsoleColor.Green);
            return SendAsync(str);
        }

        /// <summary>
        /// 接続時の処理
        /// </summary>
        protected override void OnConnected()
        {
            // クライアントのIPアドレスを取得
            var remoteEndPoint = Socket.RemoteEndPoint as IPEndPoint;
            _clientIp = remoteEndPoint?.Address.ToString() ?? "Unknown";
            _clientId = Guid.NewGuid().ToString("N");
            _sessionStartTime = DateTime.UtcNow;

            ConsoleWrite.WriteMessage(
                $"[Management] クライアント接続: IP={_clientIp}, SessionID={Id}", 
                ConsoleColor.Cyan);

            // ソケット設定
            Socket.ReceiveTimeout = 30000; // 30秒
            Socket.SendTimeout = 10000;    // 10秒

            // 接続成功メッセージを送信
            var responseJson = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.ConnectManagementServerSucceeded,
                ["SessionID"] = Id.ToString(),
                ["ClientIP"] = _clientIp
            };

            SendJsonAsyncWithTimeStamp(responseJson);
        }

        /// <summary>
        /// 切断時の処理
        /// </summary>
        protected override void OnDisconnected()
        {
            ConsoleWrite.WriteMessage(
                $"[Management] クライアント切断: SessionID={Id}, AdminID={_adminId}", 
                ConsoleColor.Yellow);

            // ログアウト処理
            if (!string.IsNullOrEmpty(_adminId))
            {
                // 管理者ログアウト処理（必要に応じて実装）
            }

            Disconnect();
        }

        /// <summary>
        /// データ受信時の処理
        /// </summary>
        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            var chunk = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            ConsoleWrite.WriteMessage($"[Management] 受信チャンク: {chunk} (長さ: {chunk.Length})", ConsoleColor.Magenta);

            _receiveBuffer.Append(chunk);
            ProcessBufferedMessages();
        }

        /// <summary>
        /// エラー時の処理
        /// </summary>
        protected override void OnError(SocketError error)
        {
            ConsoleWrite.WriteMessage(
                $"[Management] ソケットエラー (SessionID={Id}): {error}", 
                ConsoleColor.Red);
        }

        /// <summary>
        /// データ送信完了時の処理
        /// </summary>
        protected override void OnSent(long sent, long pending)
        {
            // ここでは特に処理は不要だが、デバッグ用に残す
            // ConsoleWrite.WriteMessage($"[Management] 送信完了: {sent} bytes, 待機中: {pending} bytes", ConsoleColor.Gray);
        }

        /// <summary>
        /// クライアントからのメッセージを解析して処理
        /// </summary>
        private void ParseMessageFromClient(in JObject json)
        {
            if (!json.ContainsKey("MessageType"))
            {
                SendErrorResponse("MessageType is required");
                return;
            }

            string messageType = json["MessageType"]?.ToString() ?? "";

            // タイムスタンプの確認（存在する場合）
            if (json.ContainsKey("ClientTimeStamp"))
            {
                var clientTimeStr = json["ClientTimeStamp"]?.ToString() ?? "";
                if (DateTime.TryParseExact(clientTimeStr, UTF_FORMAT, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var clientTime))
                {
                    var latency = (DateTime.UtcNow - clientTime).TotalMilliseconds;
                    ConsoleWrite.WriteMessage($"[Management] レイテンシ: {latency:F2}ms", ConsoleColor.Gray);
                }
            }

            // メッセージタイプごとの処理
            switch (messageType)
            {
                case NetworkingConstants.MessageType.AdminLoginRequest:
                    HandleAdminLogin(json);
                    break;

                case NetworkingConstants.MessageType.AdminLogoutRequest:
                    HandleAdminLogout(json);
                    break;

                case NetworkingConstants.MessageType.ServerStatusRequest:
                    HandleServerStatusRequest(json);
                    break;

                case NetworkingConstants.MessageType.GetConnectedUsersRequest:
                    HandleGetConnectedUsers(json);
                    break;

                case NetworkingConstants.MessageType.KickPlayerRequest:
                    HandleKickPlayer(json);
                    break;

                case NetworkingConstants.MessageType.BroadcastMessageRequest:
                    HandleBroadcastMessage(json);
                    break;

                case NetworkingConstants.MessageType.ShutdownServerRequest:
                    HandleShutdownRequest(json);
                    break;

                default:
                    SendErrorResponse($"Unknown message type: {messageType}");
                    break;
            }
        }

        /// <summary>
        /// 受信バッファからメッセージ境界単位で取り出して処理する
        /// </summary>
        private void ProcessBufferedMessages()
        {
            while (true)
            {
                var nextDelimiterIndex = FindNextMessageDelimiter(_receiveBuffer);
                if (nextDelimiterIndex < 0)
                {
                    break;
                }

                var rawMessage = _receiveBuffer.ToString(0, nextDelimiterIndex).Trim();
                _receiveBuffer.Remove(0, nextDelimiterIndex + 1);

                if (string.IsNullOrWhiteSpace(rawMessage))
                {
                    continue;
                }

                // 特殊コマンド（切断要求）
                if (rawMessage == "!")
                {
                    Disconnect();
                    return;
                }

                try
                {
                    var json = JObject.Parse(rawMessage);
                    ParseMessageFromClient(json);
                }
                catch (JsonReaderException ex)
                {
                    ConsoleWrite.WriteMessage($"[Management] JSON解析エラー: {ex.Message}", ConsoleColor.Red);
                    SendErrorResponse($"JSON parse error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// メッセージ区切り（LF または Unit Separator）を検索
        /// </summary>
        private int FindNextMessageDelimiter(StringBuilder source)
        {
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] == '\n' || source[i] == _unitSeparatorChar)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 管理者ログイン処理
        /// </summary>
        private void HandleAdminLogin(JObject json)
        {
            if (!json.ContainsKey("AdminID") || !json.ContainsKey("AdminPassword"))
            {
                SendErrorResponse("AdminID and AdminPassword are required");
                return;
            }

            string adminId = json["AdminID"]?.ToString() ?? "";
            string adminPassword = json["AdminPassword"]?.ToString() ?? "";

            var serverManager = ServerManager.Instance;
            var hasAdminAccount = serverManager.HasAdminAccount();
            var loginSucceeded = hasAdminAccount && serverManager.VerifyAdminCredential(adminId, adminPassword);

            if (loginSucceeded)
            {
                _adminId = adminId;
                _isAuthenticated = true;

                var response = new JObject
                {
                    ["MessageType"] = NetworkingConstants.MessageType.AdminLoginResponse,
                    ["Success"] = true,
                    ["Message"] = "Admin login succeeded"
                };

                SendJsonAsyncWithTimeStamp(response);
                ConsoleWrite.WriteMessage($"[Management] 管理者ログイン成功: {adminId}", ConsoleColor.Green);
            }
            else
            {
                var response = new JObject
                {
                    ["MessageType"] = NetworkingConstants.MessageType.AdminLoginResponse,
                    ["Success"] = false,
                    ["Message"] = hasAdminAccount
                        ? "Invalid credentials"
                        : "No admin account configured on server"
                };

                SendJsonAsyncWithTimeStamp(response);
                ConsoleWrite.WriteMessage($"[Management] 管理者ログイン失敗: {adminId}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// 管理者ログアウト処理
        /// </summary>
        private void HandleAdminLogout(JObject json)
        {
            if (!_isAuthenticated)
            {
                SendErrorResponse("Not authenticated");
                return;
            }

            _isAuthenticated = false;
            var adminIdForLog = _adminId;
            _adminId = "";

            var response = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.AdminLogoutResponse,
                ["Success"] = true,
                ["Message"] = "Logout succeeded"
            };

            SendJsonAsyncWithTimeStamp(response);
            ConsoleWrite.WriteMessage($"[Management] 管理者ログアウト: {adminIdForLog}", ConsoleColor.Yellow);
        }

        /// <summary>
        /// サーバーステータス要求処理
        /// </summary>
        private void HandleServerStatusRequest(JObject json)
        {
            if (!_isAuthenticated)
            {
                SendErrorResponse("Authentication required");
                return;
            }

            var accountManager = AccountManager.GetInstance();
            var response = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.ServerStatusResponse,
                ["ServerUptime"] = (DateTime.UtcNow - _sessionStartTime).TotalSeconds,
                ["LoggedInUsers"] = accountManager.GetLoggedInUserCount(),
                ["ServerTime"] = DateTime.UtcNow.ToString(UTF_FORMAT)
            };

            SendJsonAsyncWithTimeStamp(response);
        }

        /// <summary>
        /// 接続中のユーザー一覧取得
        /// </summary>
        private void HandleGetConnectedUsers(JObject json)
        {
            if (!_isAuthenticated)
            {
                SendErrorResponse("Authentication required");
                return;
            }

            var accountManager = AccountManager.GetInstance();
            var userIds = accountManager.GetLoggedInUserIds().ToList();

            var response = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.ConnectedUsersResponse,
                ["UserCount"] = userIds.Count,
                ["Users"] = new JArray(userIds)
            };

            SendJsonAsyncWithTimeStamp(response);
        }

        /// <summary>
        /// プレイヤーをキック処理
        /// </summary>
        private void HandleKickPlayer(JObject json)
        {
            if (!_isAuthenticated)
            {
                SendErrorResponse("Authentication required");
                return;
            }

            if (!json.ContainsKey("PlayerID"))
            {
                SendErrorResponse("PlayerID is required");
                return;
            }

            string playerId = json["PlayerID"]?.ToString() ?? "";
            var accountManager = AccountManager.GetInstance();

            if (accountManager.ExistID(playerId))
            {
                accountManager.Logout(playerId, true);
                
                var response = new JObject
                {
                    ["MessageType"] = NetworkingConstants.MessageType.KickPlayerResponse,
                    ["Success"] = true,
                    ["Message"] = $"Player {playerId} kicked"
                };

                SendJsonAsyncWithTimeStamp(response);
                ConsoleWrite.WriteMessage($"[Management] プレイヤーキック: {playerId} (管理者: {_adminId})", ConsoleColor.Yellow);
            }
            else
            {
                var response = new JObject
                {
                    ["MessageType"] = NetworkingConstants.MessageType.KickPlayerResponse,
                    ["Success"] = false,
                    ["Message"] = $"Player {playerId} not found"
                };

                SendJsonAsyncWithTimeStamp(response);
            }
        }

        /// <summary>
        /// ブロードキャストメッセージ送信
        /// </summary>
        private void HandleBroadcastMessage(JObject json)
        {
            if (!_isAuthenticated)
            {
                SendErrorResponse("Authentication required");
                return;
            }

            if (!json.ContainsKey("Message"))
            {
                SendErrorResponse("Message is required");
                return;
            }

            string message = json["Message"]?.ToString() ?? "";
            
            // すべてのクライアントにブロードキャスト
            var broadcastJson = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.BroadcastMessage,
                ["AdminID"] = _adminId,
                ["Message"] = message,
                ["Timestamp"] = DateTime.UtcNow.ToString(UTF_FORMAT)
            };

            ManagementServer.Instance.BroadcastStatus(broadcastJson);
            
            var response = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.BroadcastMessageResponse,
                ["Success"] = true,
                ["Message"] = "Broadcast message sent"
            };

            SendJsonAsyncWithTimeStamp(response);
            ConsoleWrite.WriteMessage($"[Management] ブロードキャストメッセージ: {message} (送信者: {_adminId})", ConsoleColor.Cyan);
        }

        /// <summary>
        /// サーバーシャットダウン要求処理
        /// </summary>
        private void HandleShutdownRequest(JObject json)
        {
            if (!_isAuthenticated)
            {
                SendErrorResponse("Authentication required");
                return;
            }

            var response = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.ShutdownResponse,
                ["Success"] = true,
                ["Message"] = "Shutdown initiated"
            };

            SendJsonAsyncWithTimeStamp(response);
            ConsoleWrite.WriteMessage($"[Management] シャットダウン要求: 管理者 {_adminId}", ConsoleColor.Red);

            // シャットダウンプロセス開始（別タスク）
            Task.Delay(1000).ContinueWith(_ =>
            {
                // LobbyServerManager.Instance?.StopTcpServer();
                Environment.Exit(0);
            });
        }

        /// <summary>
        /// エラーレスポンスを送信
        /// </summary>
        private void SendErrorResponse(string errorMessage)
        {
            var response = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.ErrorResponse,
                ["Error"] = errorMessage
            };

            SendJsonAsyncWithTimeStamp(response);
            ConsoleWrite.WriteMessage($"[Management] エラーレスポンス: {errorMessage}", ConsoleColor.Red);
        }
    }
}



