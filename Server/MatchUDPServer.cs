
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using OpenGSCore;
using OpenGSServer.Network; // ServerLagCompensationManager, ClientInputDataを使用
using System.Linq;

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
        private readonly ConcurrentDictionary<string, PlayerConnectionInfo> _connectedPlayers = new(); // PlayerIDをstringに
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

            // start periodic snapshot broadcast (default 20Hz)
            StartSnapshotBroadcast(50);

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
            // 接続キー検証 (ClientNetworkManagerのClientPlayerIdをTagとして設定)
            var reader = request.Data;
            string playerId = reader.GetString(); // クライアントからのPlayerIDを読み取る
            reader.Recycle(); // readerをリサイクル

            if (!string.IsNullOrEmpty(playerId))
            {
                var peer = request.AcceptIfKey(ConnectionKey);
                if (peer != null)
                {
                    peer.Tag = playerId; // PeerにPlayerIDを紐付ける
                    ConsoleWrite.WriteMessage($"[UDP] Connection request accepted from {peer.EndPoint} for Player: {playerId}", ConsoleColor.Green);
                }
                else
                {
                    ConsoleWrite.WriteMessage($"[UDP] Connection request rejected (invalid key or PlayerID missing)", ConsoleColor.Yellow);
                }
            }
            else
            {
                 request.Reject();
                 ConsoleWrite.WriteMessage($"[UDP] Connection request rejected (PlayerID missing in data)", ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// プレイヤー接続時
        /// </summary>
        private void OnPeerConnected(NetPeer peer)
        {
            string playerId = peer.Tag?.ToString() ?? "Unknown";

            var playerInfo = new PlayerConnectionInfo
            {
                PeerId = peer.Id,
                EndPoint = peer.EndPoint.ToString(),
                ConnectedAt = DateTime.UtcNow,
                PlayerId = playerId // PlayerIdを保存
            };

            _connectedPlayers[playerId] = playerInfo; // Dictionaryのキーをstringに

            ConsoleWrite.WriteMessage($"[UDP] Player connected: {playerId} ({peer.Id}) from {peer.EndPoint}", ConsoleColor.Green);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);

            // ラグ補償システムにクライアントを登録
            MatchServerV2.Instance.ServerLagCompensationManager.AddPlayer(playerId);
            MatchServerV2.Instance.ServerLagCompensationManager.RegisterClientCallback(
                playerId,
                state => SendTransformStateToClient(peer, state)
            );

            // MatchRoomに通知（必要に応じて）
            // NotifyPlayerJoined(peer.Id); // IDがintなので要修正
        }

        /// <summary>
        /// プレイヤー切断時
        /// </summary>
        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            string playerId = peer.Tag?.ToString() ?? "Unknown";
            _connectedPlayers.TryRemove(playerId, out _); // Dictionaryのキーをstringに

            ConsoleWrite.WriteMessage(
                $"[UDP] Player disconnected: {playerId} ({peer.Id}) (Reason: {info.Reason})", 
                ConsoleColor.Yellow);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);

            // ラグ補償システムからクライアントを解除
            MatchServerV2.Instance.ServerLagCompensationManager.RemovePlayer(playerId);

            // MatchRoomに通知
            // NotifyPlayerLeft(peer.Id); // IDがintなので要修正
        }

        /// <summary>
        /// 状態ブロードキャストコールバックから呼ばれる
        /// </summary>
        private void SendTransformStateToClient(NetPeer peer, ServerTransformState state)
        {
            if (peer == null || peer.ConnectionState != ConnectionState.Connected) return;

            var writer = new NetDataWriter();
            writer.Put("ServerTransformState"); // メッセージタイプ
            writer.Put(state.NetworkId);
            writer.Put(state.PlayerId);
            writer.Put(state.PositionX);
            writer.Put(state.PositionY);
            writer.Put(state.PositionZ);
            writer.Put(state.RotationX);
            writer.Put(state.RotationY);
            writer.Put(state.RotationZ);
            writer.Put(state.RotationW);
            writer.Put(state.VelocityX);
            writer.Put(state.VelocityY);
            writer.Put(state.VelocityZ);
            writer.Put(state.Timestamp);
            writer.Put(state.SequenceNumber);
            peer.Send(writer, DeliveryMethod.Unreliable);
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
                    case "ClientConnect": // クライアントからPlayerIDを受け取るための初期接続メッセージ
                        // peer.Tagに既に設定済みなので何もしない
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
            var moveX = reader.GetFloat();
            var moveY = reader.GetFloat();
            var moveZ = reader.GetFloat(); 
            var lookX = reader.GetFloat();
            var lookY = reader.GetFloat();
            var jump = reader.GetBool();
            var fire = reader.GetBool();
            var sequence = reader.GetByte();
            var timestamp = reader.GetFloat();
            var deltaTime = reader.GetFloat();

            string playerId = peer.Tag?.ToString() ?? "Unknown";

            // ClientInputDataを構築し、ラグ補償システムにプッシュ
            var inputData = new ClientInputData
            {
                PlayerId = playerId,
                MoveX = moveX,
                MoveY = moveY,
                MoveZ = moveZ,
                LookX = lookX,
                LookY = lookY,
                Jump = jump,
                Fire = fire,
                SequenceNumber = sequence,
                Timestamp = timestamp,
                DeltaTime = deltaTime
            };

            MatchServerV2.Instance.ServerLagCompensationManager.ProcessClientInput(inputData);
        }

        /// <summary>
        /// プレイヤー射撃処理
        /// </summary>
        private void HandlePlayerShoot(NetPeer peer, NetPacketReader reader)
        {
            var weaponType = reader.GetInt(); // クライアントがweaponTypeを送ってくる場合
            var posX = reader.GetFloat(); // クライアントが射撃時の位置を送ってくる場合
            var posY = reader.GetFloat();
            var angle = reader.GetFloat(); // クライアントが射撃時の角度を送ってくる場合
            var sequence = reader.GetByte();
            var timestamp = reader.GetFloat();
            var deltaTime = reader.GetFloat();

            string playerId = peer.Tag?.ToString() ?? "Unknown";

            // ClientInputDataを構築し、ラグ補償システムにプッシュ
            var inputData = new ClientInputData
            {
                PlayerId = playerId,
                MoveX = 0, MoveY = 0, MoveZ = 0, // 射撃は移動ではない
                LookX = angle, LookY = 0, // 射撃方向をlookに含める (例)
                Jump = false, Fire = true,
                SequenceNumber = sequence,
                Timestamp = timestamp,
                DeltaTime = deltaTime
            };
            MatchServerV2.Instance.ServerLagCompensationManager.ProcessClientInput(inputData);
        }

        /// <summary>
        /// プレイヤーアクション処理
        /// </summary>
        private void HandlePlayerAction(NetPeer peer, NetPacketReader reader)
        {
            var actionType = reader.GetString();

            // アクションに応じた処理
            ConsoleWrite.WriteMessage($"[UDP] Player {peer.Tag} action: {actionType}", ConsoleColor.Gray);
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
            string playerId = peer.Tag?.ToString() ?? "Unknown";
            // var playerId = PlayerID.FromString(peer.Id.ToString()); // PlayerID.FromStringは未定義なのでコメントアウト
            var rtt = (DateTime.UtcNow.Ticks - timestamp) / TimeSpan.TicksPerMillisecond;
            // LobbyServerManager.Instance.RecordPlayerPing(playerId, rtt); // PlayerIDがint想定なのでコメントアウト
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
                // player.Idはstringなのでpeer.Tagと比較する必要がある
                // _connectedPlayers から PlayerID (string) を見つける必要がある
                if (_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                {
                    var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                    peerById?.Send(writer, DeliveryMethod.ReliableOrdered);
                }
            }
        }

        /// <summary>
        /// プレイヤーの所属MatchRoomを取得
        /// </summary>
        private MatchRoom? GetMatchRoomForPlayer(int peerId)
        {
            // peer.Tag が PlayerID (string) なので、int peerId は使えない
            // _connectedPlayers から PlayerID (string) を見つける必要がある
            var playerId = _connectedPlayers.FirstOrDefault(x => x.Value.PeerId == peerId).Key;
            if (string.IsNullOrEmpty(playerId)) return null;

            var matchRoomManager = MatchRoomManager.Instance;
            var allRooms = matchRoomManager.AllRooms();

            return allRooms
                .OfType<MatchRoom>()
                .FirstOrDefault(room => room.Players.Any(p => p.Id == playerId));
        }

        /// <summary>
        /// プレイヤー参加通知
        /// </summary>
        private void NotifyPlayerJoined(string playerId) // playerId を string に変更
        {
            // 必要に応じてMatchRoomManagerに通知
        }

        /// <summary>
        /// プレイヤー退出通知
        /// </summary>
        private void NotifyPlayerLeft(string playerId) // playerId を string に変更
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

        private System.Timers.Timer? _snapshotTimer;

        public void StartSnapshotBroadcast(int intervalMs = 50)
        {
            if (_snapshotTimer != null) return;
            _snapshotTimer = new System.Timers.Timer(intervalMs);
            _snapshotTimer.Elapsed += (s, e) => BroadcastSnapshots();
            _snapshotTimer.Start();
        }

        public void StopSnapshotBroadcast()
        {
            _snapshotTimer?.Stop();
            _snapshotTimer?.Dispose();
            _snapshotTimer = null;
        }

        private void BroadcastSnapshots()
        {
            try
            {
                var matchRoomManager = MatchRoomManager.Instance;
                var rooms = matchRoomManager.AllRooms();

                foreach (var abstractRoom in rooms)
                {
                    if (abstractRoom is MatchRoom room)
                    {
                        // ISyncable インターフェースを使用してルーム全体の同期状態を取得
                        // var syncState = room.ToJSon(); // Full MatchRoom snapshot

                        // ラグ補償システムからプレイヤー状態を取得
                        var lagCompManager = MatchServerV2.Instance.ServerLagCompensationManager;
                        
                        foreach (var player in room.Players)
                        {
                            var playerState = lagCompManager.GetPlayerState(player.Id);
                            if (playerState.PlayerId != null) // デフォルト値でないことを確認
                            {
                                // ServerTransformStateをシリアライズして送信
                                var writer = new NetDataWriter();
                                writer.Put("ServerTransformState"); // メッセージタイプ
                                writer.Put(playerState.NetworkId);
                                writer.Put(playerState.PlayerId);
                                writer.Put(playerState.PositionX);
                                writer.Put(playerState.PositionY);
                                writer.Put(playerState.PositionZ);
                                writer.Put(playerState.RotationX);
                                writer.Put(playerState.RotationY);
                                writer.Put(playerState.RotationZ);
                                writer.Put(playerState.RotationW);
                                writer.Put(playerState.VelocityX);
                                writer.Put(playerState.VelocityY);
                                writer.Put(playerState.VelocityZ);
                                writer.Put(playerState.Timestamp);
                                writer.Put(playerState.SequenceNumber);

                                if (_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                                {
                                    var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                                    peerById?.Send(writer, DeliveryMethod.Unreliable);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[UDP] Error broadcasting snapshots: {ex.Message}", ConsoleColor.Red);
            }
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
        public required string PlayerId { get; init; } // PlayerIDを追加
    }
}
