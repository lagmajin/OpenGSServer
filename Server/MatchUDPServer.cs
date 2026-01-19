
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// Match UDPサーバー
    /// LiteNetLibを使用した高速UDP通信
    /// OpenGSCore.MatchRoomと統合
    /// </summary>
    public sealed class MatchUDPServer : IDisposable
    {
        private const string ConnectionKey = "OpenGS";
        
        private NetManager? _server;
        private readonly EventBasedNetListener _listener = new();
        private readonly ConcurrentDictionary<int, PlayerConnectionInfo> _connectedPlayers = new();
        private bool _disposed;

        public bool IsRunning => _server?.IsRunning ?? false;
        public int ConnectedPlayerCount => _connectedPlayers.Count;

        /// <summary>
        /// UDPサーバーを起動
        /// </summary>
        public void Listen(int port)
        {
            if (_server != null)
            {
                ConsoleWrite.WriteMessage("[UDP] Server already running", ConsoleColor.Yellow);
                return;
            }

            ConsoleWrite.WriteMessage($"[UDP] Starting server on port {port}...", ConsoleColor.Cyan);

            _server = new NetManager(_listener);
            _server.Start(port);

            // イベントハンドラー登録
            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.NetworkErrorEvent += OnNetworkError;

            ConsoleWrite.WriteMessage($"[UDP] Server started on port {port}", ConsoleColor.Green);
        }

        /// <summary>
        /// 接続リクエスト処理
        /// </summary>
        private void OnConnectionRequest(ConnectionRequest request)
        {
            // 接続キー検証
            var peer = request.AcceptIfKey(ConnectionKey);
            
            if (peer != null)
            {
                ConsoleWrite.WriteMessage($"[UDP] Connection request accepted from {peer.EndPoint}", ConsoleColor.Green);
            }
            else
            {
                ConsoleWrite.WriteMessage($"[UDP] Connection request rejected (invalid key)", ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// プレイヤー接続時
        /// </summary>
        private void OnPeerConnected(NetPeer peer)
        {
            var playerInfo = new PlayerConnectionInfo
            {
                PeerId = peer.Id,
                EndPoint = peer.EndPoint.ToString(),
                ConnectedAt = DateTime.UtcNow
            };

            _connectedPlayers[peer.Id] = playerInfo;

            ConsoleWrite.WriteMessage($"[UDP] Player connected: {peer.Id} from {peer.EndPoint}", ConsoleColor.Green);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);

            // MatchRoomに通知（必要に応じて）
            NotifyPlayerJoined(peer.Id);
        }

        /// <summary>
        /// プレイヤー切断時
        /// </summary>
        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            _connectedPlayers.TryRemove(peer.Id, out _);

            ConsoleWrite.WriteMessage(
                $"[UDP] Player disconnected: {peer.Id} (Reason: {info.Reason})", 
                ConsoleColor.Yellow);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);

            // MatchRoomに通知
            NotifyPlayerLeft(peer.Id);
        }

        /// <summary>
        /// データ受信時
        /// </summary>
        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                // メッセージタイプを読み取り
                var messageType = reader.GetString();

                // メッセージタイプに応じて処理
                switch (messageType)
                {
                    case "PlayerMove":
                        HandlePlayerMove(peer, reader);
                        break;

                    case "PlayerShoot":
                        HandlePlayerShoot(peer, reader);
                        break;

                    case "PlayerAction":
                        HandlePlayerAction(peer, reader);
                        break;

                    case "Ping":
                        HandlePing(peer, reader);
                        break;

                    default:
                        ConsoleWrite.WriteMessage($"[UDP] Unknown message type: {messageType}", ConsoleColor.Yellow);
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[UDP] Error processing message: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                reader.Recycle();
            }
        }

        /// <summary>
        /// プレイヤー移動処理
        /// </summary>
        private void HandlePlayerMove(NetPeer peer, NetPacketReader reader)
        {
            var posX = reader.GetFloat();
            var posY = reader.GetFloat();
            var velocityX = reader.GetFloat();
            var velocityY = reader.GetFloat();

            // MatchRoomに転送
            var matchRoom = GetMatchRoomForPlayer(peer.Id);
            if (matchRoom != null)
            {
                // OpenGSCore.MatchRoomのGameSceneに移動情報を転送
                // matchRoom.GameScene.UpdatePlayerPosition(peer.Id, posX, posY);
            }

            // 他のプレイヤーにブロードキャスト
            BroadcastToRoom(peer.Id, "PlayerMoved", writer =>
            {
                writer.Put(peer.Id);
                writer.Put(posX);
                writer.Put(posY);
                writer.Put(velocityX);
                writer.Put(velocityY);
            });
        }

        /// <summary>
        /// プレイヤー射撃処理
        /// </summary>
        private void HandlePlayerShoot(NetPeer peer, NetPacketReader reader)
        {
            var weaponType = reader.GetInt();
            var posX = reader.GetFloat();
            var posY = reader.GetFloat();
            var angle = reader.GetFloat();

            // 他のプレイヤーにブロードキャスト
            BroadcastToRoom(peer.Id, "PlayerShot", writer =>
            {
                writer.Put(peer.Id);
                writer.Put(weaponType);
                writer.Put(posX);
                writer.Put(posY);
                writer.Put(angle);
            });
        }

        /// <summary>
        /// プレイヤーアクション処理
        /// </summary>
        private void HandlePlayerAction(NetPeer peer, NetPacketReader reader)
        {
            var actionType = reader.GetString();

            // アクションに応じた処理
            ConsoleWrite.WriteMessage($"[UDP] Player {peer.Id} action: {actionType}", ConsoleColor.Gray);
        }

        /// <summary>
        /// Ping処理
        /// </summary>
        private void HandlePing(NetPeer peer, NetPacketReader reader)
        {
            var timestamp = reader.GetLong();

            // Pong返信
            var writer = new NetDataWriter();
            writer.Put("Pong");
            writer.Put(timestamp);
            peer.Send(writer, DeliveryMethod.Unreliable);

            // Ping統計を記録
            var playerId = PlayerID.FromString(peer.Id.ToString());
            var rtt = (DateTime.UtcNow.Ticks - timestamp) / TimeSpan.TicksPerMillisecond;
            LobbyServerManager.Instance.RecordPlayerPing(playerId, rtt);
        }

        /// <summary>
        /// ネットワークエラー時
        /// </summary>
        private void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            ConsoleWrite.WriteMessage($"[UDP] Network error from {endPoint}: {socketError}", ConsoleColor.Red);
        }

        /// <summary>
        /// ルーム内のプレイヤーにブロードキャスト
        /// </summary>
        private void BroadcastToRoom(int senderId, string messageType, Action<NetDataWriter> writeData)
        {
            var matchRoom = GetMatchRoomForPlayer(senderId);
            if (matchRoom == null) return;

            var writer = new NetDataWriter();
            writer.Put(messageType);
            writeData(writer);

            // 同じルームのプレイヤーに送信
            foreach (var player in matchRoom.Players)
            {
                if (int.TryParse(player.Id, out var playerId))
                {
                    var peer = _server?.GetPeerById(playerId);
                    peer?.Send(writer, DeliveryMethod.ReliableOrdered);
                }
            }
        }

        /// <summary>
        /// プレイヤーの所属MatchRoomを取得
        /// </summary>
        private MatchRoom? GetMatchRoomForPlayer(int peerId)
        {
            var matchRoomManager = MatchRoomManager.Instance;
            var allRooms = matchRoomManager.AllRooms();

            return allRooms
                .OfType<MatchRoom>()
                .FirstOrDefault(room => room.Players.Any(p => p.Id == peerId.ToString()));
        }

        /// <summary>
        /// プレイヤー参加通知
        /// </summary>
        private void NotifyPlayerJoined(int peerId)
        {
            // 必要に応じてMatchRoomManagerに通知
        }

        /// <summary>
        /// プレイヤー退出通知
        /// </summary>
        private void NotifyPlayerLeft(int peerId)
        {
            // 必要に応じてMatchRoomManagerに通知
        }

        /// <summary>
        /// イベントをポーリング
        /// </summary>
        public void PollingEvent()
        {
            _server?.PollEvents();
        }

        /// <summary>
        /// サーバーをシャットダウン
        /// </summary>
        public void Shutdown()
        {
            if (_server == null || !_server.IsRunning)
            {
                return;
            }

            ConsoleWrite.WriteMessage("[UDP] Shutting down server...", ConsoleColor.Yellow);

            // イベントハンドラー解除
            _listener.ConnectionRequestEvent -= OnConnectionRequest;
            _listener.PeerConnectedEvent -= OnPeerConnected;
            _listener.PeerDisconnectedEvent -= OnPeerDisconnected;
            _listener.NetworkReceiveEvent -= OnNetworkReceive;
            _listener.NetworkErrorEvent -= OnNetworkError;

            _server.Stop();
            _connectedPlayers.Clear();

            ConsoleWrite.WriteMessage("[UDP] Server shutdown complete", ConsoleColor.Green);
        }

        public void Dispose()
        {
            if (_disposed) return;

            Shutdown();
            _disposed = true;
        }
    }

    /// <summary>
    /// プレイヤー接続情報
    /// </summary>
    internal sealed class PlayerConnectionInfo
    {
        public required int PeerId { get; init; }
        public required string EndPoint { get; init; }
        public required DateTime ConnectedAt { get; init; }
    }
}
