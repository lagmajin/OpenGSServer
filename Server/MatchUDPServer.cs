
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network; // ServerLagCompensationManager, ClientInputDataを使用
using System.Linq;

#nullable enable

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
        private static readonly string[] MatchEventTypes =
        {
            "LoadingStarted", "LoadingProgress", "LoadingCompleted", "LoadingFinished",
            "MatchStatusRequest", "PlayerRespawn", "PlayerPose", "GrenadeThrow", "PlayerShot",
            "ShootRequest", "FlagCaptured", "FlagLost", "FlagPickup", "FlagReturn",
            "FlagScoreUpdate", "PlayerEliminated", "ObjectSpawned", "ObjectDestroyed"
        };
        
        private NetManager? _server;
        private readonly EventBasedNetListener _listener = new();
        private readonly ConcurrentDictionary<string, PlayerConnectionInfo> _connectedPlayers =
            new(StringComparer.OrdinalIgnoreCase); // PlayerIDをstringに
        private readonly ConcurrentDictionary<string, string> _connectionTokens =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ConnectionTokenInfo> _connectionTokenInfo =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<int, DateTime> _pendingPeers = new();
        private readonly ConcurrentDictionary<int, int> _unauthorizedPacketCounts = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ClientInputData>> _pendingInputs =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, TokenBucket> _matchEventRateLimiters =
            new(StringComparer.OrdinalIgnoreCase);
        private const double MatchEventBurstCapacity = 30;
        private const double MatchEventRatePerSecond = 20;
        private const int UnauthorizedPacketLimit = 8;
        private const int PendingPeerTimeoutSeconds = 10;
        private const int ConnectionTokenLifetimeMinutes = 10;
        private const int MaxInputsPerPlayerPerTick = 8;
        private bool _disposed;

        public bool IsRunning => _server?.IsRunning ?? false;
        public int ConnectedPlayerCount => _connectedPlayers.Count;
        public int? UdpPort { get; private set; }

        public string IssueConnectionToken(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return string.Empty;
            }

            var token = Guid.NewGuid().ToString("N");
            _connectionTokens[playerId] = token;
            _connectionTokenInfo[playerId] = new ConnectionTokenInfo(token, DateTime.UtcNow.AddMinutes(ConnectionTokenLifetimeMinutes));
            return token;
        }

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

            // イベントハンドラー登録
            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.NetworkErrorEvent += OnNetworkError;

            try
            {
                if (!_server.Start(port))
                {
                    throw new InvalidOperationException($"Unable to start UDP server on port {port}.");
                }

                UdpPort = port;

            }
            catch
            {
                _listener.ConnectionRequestEvent -= OnConnectionRequest;
                _listener.PeerConnectedEvent -= OnPeerConnected;
                _listener.PeerDisconnectedEvent -= OnPeerDisconnected;
                _listener.NetworkReceiveEvent -= OnNetworkReceive;
                _listener.NetworkErrorEvent -= OnNetworkError;
                _server = null;
                UdpPort = null;
                throw;
            }

            ConsoleWrite.WriteMessage($"[UDP] Server started on port {port}", ConsoleColor.Green);
        }

        /// <summary>
        /// 接続リクエスト処理
        /// </summary>
        private void OnConnectionRequest(ConnectionRequest request)
        {
            // 接続キーは認証用であり、PlayerID は接続後の ClientConnect で受け取る。
            var peer = request.AcceptIfKey(ConnectionKey);
            if (peer != null)
            {
                ConsoleWrite.WriteMessage($"[UDP] Connection request accepted from {peer.EndPoint}; waiting for ClientConnect", ConsoleColor.Green);
            }
            else
            {
                ConsoleWrite.WriteMessage("[UDP] Connection request rejected (invalid key)", ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// プレイヤー接続時
        /// </summary>
        private void OnPeerConnected(NetPeer peer)
        {
            string playerId = peer.Tag?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                _pendingPeers[peer.Id] = DateTime.UtcNow;
                ConsoleWrite.WriteMessage($"[UDP] Peer connected, waiting for ClientConnect: {peer.EndPoint}", ConsoleColor.Cyan);
                return;
            }

            _pendingPeers.TryRemove(peer.Id, out _);

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
            var matchRoom = MatchRoomManager.Instance.SearchRoomByMemberID(playerId);
            var roomPlayer = matchRoom?.Players.FirstOrDefault(player =>
                string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase));
            if (matchRoom != null && roomPlayer != null &&
                ServerManager.Instance.Settings.TryGetRespawnPoint(
                    roomPlayer.Team,
                    matchRoom.Players.IndexOf(roomPlayer),
                    out var initialSpawn))
            {
                MatchServerV2.Instance.ServerLagCompensationManager.SetPlayerPosition(
                    playerId, initialSpawn.X, initialSpawn.Y, initialSpawn.Z);
            }
            MatchServerV2.Instance.ServerLagCompensationManager.RegisterClientCallback(
                playerId,
                state => SendTransformStateToClient(peer, state)
            );

            NotifyPlayerJoined(playerId);
        }

        /// <summary>
        /// プレイヤー切断時
        /// </summary>
        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            string playerId = peer.Tag?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            // 同じ PlayerID が再接続している場合、古い peer の切断通知で
            // 現在の接続とラグ補償状態まで削除しない。
            if (!_connectedPlayers.TryGetValue(playerId, out var currentConnection) ||
                currentConnection.PeerId != peer.Id)
            {
                return;
            }

            _connectedPlayers.TryRemove(playerId, out _); // Dictionaryのキーをstringに
            _matchEventRateLimiters.TryRemove(playerId, out _);
            _pendingInputs.TryRemove(playerId, out _);
            _pendingPeers.TryRemove(peer.Id, out _);
            _unauthorizedPacketCounts.TryRemove(peer.Id, out _);
            InGameMatchEventHandler.ClearPlayerState(playerId);

            ConsoleWrite.WriteMessage(
                $"[UDP] Player disconnected: {playerId} ({peer.Id}) (Reason: {info.Reason})", 
                ConsoleColor.Yellow);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);

            // ラグ補償システムからクライアントを解除
            MatchServerV2.Instance.ServerLagCompensationManager.RemovePlayer(playerId);

            NotifyPlayerLeft(playerId);
        }

        /// <summary>
        /// 状態ブロードキャストコールバックから呼ばれる
        /// </summary>
        private void SendTransformStateToClient(NetPeer peer, ServerTransformState state)
        {
            var message = CreateTransformStateMessage(state);
            var room = GetMatchRoomForPlayer(state.PlayerId);
            if (room == null)
            {
                if (peer != null && peer.ConnectionState == ConnectionState.Connected)
                {
                    SendJsonToPeer(peer, message, DeliveryMethod.Unreliable);
                }

                return;
            }

            foreach (var player in room.Players)
            {
                if (!_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                {
                    continue;
                }

                var targetPeer = _server?.GetPeerById(connectionInfo.PeerId);
                if (targetPeer != null && targetPeer.ConnectionState == ConnectionState.Connected)
                {
                    SendJsonToPeer(targetPeer, message, DeliveryMethod.Unreliable);
                }
            }
        }

        private static JObject CreateTransformStateMessage(ServerTransformState state)
        {
            return new JObject
            {
                ["MessageType"] = "ServerTransformState",
                ["NetworkId"] = state.NetworkId,
                ["PlayerID"] = state.PlayerId,
                ["PlayerId"] = state.PlayerId,
                ["PosX"] = state.PositionX,
                ["PosY"] = state.PositionY,
                ["PosZ"] = state.PositionZ,
                ["PositionX"] = state.PositionX,
                ["PositionY"] = state.PositionY,
                ["PositionZ"] = state.PositionZ,
                ["RotationX"] = state.RotationX,
                ["RotationY"] = state.RotationY,
                ["RotationZ"] = state.RotationZ,
                ["RotationW"] = state.RotationW,
                ["VelX"] = state.VelocityX,
                ["VelY"] = state.VelocityY,
                ["VelZ"] = state.VelocityZ,
                ["VelocityX"] = state.VelocityX,
                ["VelocityY"] = state.VelocityY,
                ["VelocityZ"] = state.VelocityZ,
                ["Timestamp"] = state.Timestamp,
                ["SequenceNumber"] = state.SequenceNumber
            };
        }

        private static void SendJsonToPeer(NetPeer peer, JObject message, DeliveryMethod method)
        {
            var writer = new NetDataWriter();
            writer.Put(message.ToString(Newtonsoft.Json.Formatting.None));
            peer.Send(writer, method);
        }

        /// <summary>
        /// データ受信時
        /// </summary>
        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                var data = reader.GetRemainingBytes();
                if (LooksLikeJson(data))
                {
                    if (TryParseJson(data, out var json))
                    {
                        HandleJsonMessage(peer, json!);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage($"[UDP] Rejected malformed JSON packet from {peer.EndPoint}", ConsoleColor.Yellow);
                    }
                    return;
                }

                if (peer.Tag is not string playerId || string.IsNullOrWhiteSpace(playerId))
                {
                    RegisterUnauthorizedPacket(peer);
                    ConsoleWrite.WriteMessage($"[UDP] Ignoring binary packet from peer without PlayerID: {peer.EndPoint}", ConsoleColor.Yellow);
                    return;
                }

                var binaryReader = new NetDataReader(data);
                // メッセージタイプを読み取り
                var messageType = binaryReader.GetString();

                // メッセージタイプに応じて処理
                switch (messageType)
                {
                    case "PlayerMove":
                        HandlePlayerMove(peer, binaryReader);
                        break;

                    case "PlayerShoot":
                        HandlePlayerShoot(peer, binaryReader);
                        break;

                    case "PlayerAction":
                        HandlePlayerAction(peer, binaryReader);
                        break;

                    case "Ping":
                        HandlePing(peer, binaryReader);
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

        private static bool TryParseJson(byte[] data, out JObject? json)
        {
            json = null;
            var text = Encoding.UTF8.GetString(data).TrimStart();
            if (!text.StartsWith("{", StringComparison.Ordinal)) return false;

            try
            {
                json = JObject.Parse(text);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool LooksLikeJson(byte[] data)
        {
            if (data == null)
            {
                return false;
            }

            var index = 0;
            while (index < data.Length && char.IsWhiteSpace((char)data[index]))
            {
                index++;
            }

            return index < data.Length && data[index] == (byte)'{';
        }

        private void HandleJsonMessage(NetPeer peer, JObject message)
        {
            var messageType = MessageType.Normalize(message["MessageType"]?.ToString());
            var messagePlayerId = message["PlayerID"]?.ToString() ?? message["PlayerId"]?.ToString();

            if (string.Equals(messageType, "ClientConnect", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(messagePlayerId))
                {
                    peer.Disconnect();
                    return;
                }

                var token = message["UdpToken"]?.ToString() ?? message["Token"]?.ToString();
                if (!_connectionTokens.TryGetValue(messagePlayerId, out var expectedToken) ||
                    !_connectionTokenInfo.TryGetValue(messagePlayerId, out var tokenInfo) ||
                    tokenInfo.IsExpired ||
                    string.IsNullOrWhiteSpace(token) ||
                    !string.Equals(expectedToken, token, StringComparison.Ordinal) ||
                    !string.Equals(tokenInfo.Token, token, StringComparison.Ordinal))
                {
                    ConsoleWrite.WriteMessage($"[UDP] Rejected ClientConnect without a valid token for {messagePlayerId}", ConsoleColor.Yellow);
                    peer.Disconnect();
                    return;
                }

                RegisterJsonPeer(peer, messagePlayerId);
                return;
            }

            if (peer.Tag is not string playerId || string.IsNullOrWhiteSpace(playerId))
            {
                RegisterUnauthorizedPacket(peer);
                ConsoleWrite.WriteMessage($"[UDP] Ignoring JSON packet before ClientConnect: {peer.EndPoint}", ConsoleColor.Yellow);
                return;
            }

            if (string.Equals(messageType, "PingRequest", StringComparison.OrdinalIgnoreCase))
            {
                HandleJsonPing(peer, playerId, message);
                return;
            }

            if (string.Equals(messageType, "PingResponse", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(messageType, "PlayerPositionUpdate", StringComparison.OrdinalIgnoreCase))
            {
                HandleJsonPositionUpdate(playerId, message);
                return;
            }

            if (TryDispatchMatchEvent(peer, playerId, messageType, message))
            {
                return;
            }

            if (!IsJsonInputMessage(messageType))
            {
                ConsoleWrite.WriteMessage(
                    $"[UDP] Ignoring unsupported JSON message type '{messageType}' from {playerId}",
                    ConsoleColor.Yellow);
                return;
            }

            var direction = message["Direction"] as JObject;
            var input = new ClientInputData
            {
                PlayerId = playerId,
                MoveX = ReadJsonFloat(message, "VelX", "MoveX"),
                MoveY = ReadJsonFloat(message, "VelY", "MoveY"),
                MoveZ = ReadJsonFloat(message, "VelZ", "MoveZ"),
                LookX = ReadJsonFloat(direction, "X", "DirX"),
                LookY = ReadJsonFloat(direction, "Y", "DirY"),
                Jump = ReadJsonBool(message, "Jump"),
                Fire = ReadJsonBool(message, "Fire"),
                SequenceNumber = ReadJsonByte(message, "SequenceNumber", "Sequence"),
                Timestamp = ReadJsonFloat(message, "Timestamp"),
                DeltaTime = ReadJsonFloat(message, "DeltaTime"),
                HasClientPosition = message["PosX"] != null || message["PositionX"] != null,
                ClientPosX = ReadJsonFloat(message, "PosX", "PositionX"),
                ClientPosY = ReadJsonFloat(message, "PosY", "PositionY"),
                ClientPosZ = ReadJsonFloat(message, "PosZ", "PositionZ")
            };

            QueueInput(input);
        }

        private static bool IsJsonInputMessage(string messageType)
        {
            // PlayerMove is the current client wire format; PlayerInput remains
            // accepted for older clients and local integration harnesses.
            return string.Equals(messageType, "PlayerMove", StringComparison.OrdinalIgnoreCase)
                || string.Equals(messageType, "PlayerInput", StringComparison.OrdinalIgnoreCase);
        }

        private static void HandleJsonPing(NetPeer peer, string playerId, JObject message)
        {
            var response = new JObject
            {
                ["MessageType"] = "PingResponse",
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["ClientTimestamp"] = message["ClientTimestamp"] ?? JValue.CreateNull(),
                ["ServerTimestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            SendJsonToPeer(peer, response, DeliveryMethod.Unreliable);
        }

        private void HandleJsonPositionUpdate(string playerId, JObject message)
        {
            var position = message["Position"] as JObject;
            var x = message["PosX"] != null || message["PositionX"] != null
                ? ReadJsonFloat(message, "PosX", "PositionX")
                : ReadJsonFloat(position, "X", "x");
            var y = message["PosY"] != null || message["PositionY"] != null
                ? ReadJsonFloat(message, "PosY", "PositionY")
                : ReadJsonFloat(position, "Y", "y");
            var z = message["PosZ"] != null || message["PositionZ"] != null
                ? ReadJsonFloat(message, "PosZ", "PositionZ")
                : ReadJsonFloat(position, "Z", "z");
            var rotation = ReadJsonFloat(message, "Rotation", "RotationZ", "RotationY");
            var deltaTime = ReadJsonFloat(message, "DeltaTime");

            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z) || !IsFinite(rotation) || !IsFinite(deltaTime))
            {
                ConsoleWrite.WriteMessage($"[UDP] Rejected non-finite position update from {playerId}", ConsoleColor.Yellow);
                return;
            }

            if (!MatchServerV2.Instance.ServerLagCompensationManager.ValidatePlayerPosition(
                    playerId, x, y, z, deltaTime, out var rejectionReason))
            {
                ConsoleWrite.WriteMessage($"[UDP] Rejected position update from {playerId}: {rejectionReason}", ConsoleColor.Yellow);
                return;
            }

            MatchServerV2.Instance.ServerLagCompensationManager.SetPlayerPosition(playerId, x, y, z);

            var room = MatchRoomManager.Instance.SearchRoomByMemberID(playerId);
            if (room == null)
            {
                return;
            }

            var update = new JObject
            {
                ["MessageType"] = "PlayerPositionUpdate",
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["Position"] = new JObject
                {
                    ["X"] = x,
                    ["Y"] = y,
                    ["Z"] = z
                },
                ["PosX"] = x,
                ["PosY"] = y,
                ["PosZ"] = z,
                ["Rotation"] = rotation,
                ["SequenceNumber"] = message["SequenceNumber"] ?? message["Sequence"] ?? 0,
                ["Timestamp"] = message["Timestamp"] ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            foreach (var player in room.Players)
            {
                if (string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase) ||
                    !_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                {
                    continue;
                }

                var peer = _server?.GetPeerById(connectionInfo.PeerId);
                if (peer != null && peer.ConnectionState == ConnectionState.Connected)
                {
                    SendJsonToPeer(peer, update, DeliveryMethod.Unreliable);
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private bool TryDispatchMatchEvent(NetPeer peer, string playerId, string messageType, JObject message)
        {
            messageType = CanonicalizeMatchEventType(messageType);
            message["MessageType"] = messageType;

            if (string.Equals(messageType, "ShootRequest", StringComparison.OrdinalIgnoreCase))
            {
                messageType = "PlayerShot";
                message["MessageType"] = messageType;
            }

            // Loading and match-control messages are authoritative TCP
            // operations. Keeping them out of the UDP path prevents a stale
            // or forged datagram from changing room/loading state.
            if (messageType is
                "LoadingStarted" or
                "LoadingProgress" or
                "LoadingCompleted" or
                "LoadingFinished" or
                "MatchStatusRequest")
            {
                ConsoleWrite.WriteMessage(
                    $"[UDP] Ignoring TCP control message '{messageType}' from {playerId}; use the match TCP session.",
                    ConsoleColor.Yellow);
                return true;
            }

            var isSystemEvent = messageType is
                "PlayerRespawn" or
                "PlayerPose";

            var isRealtimeEvent = messageType is
                "GrenadeThrow" or
                "PlayerShot" or
                "FlagCaptured" or
                "FlagLost" or
                "FlagPickup" or
                "FlagReturn" or
                "FlagScoreUpdate" or
                "PlayerEliminated" or
                "ObjectSpawned" or
                "ObjectDestroyed";

            if (!isSystemEvent && !isRealtimeEvent)
            {
                return false;
            }

            var room = MatchRoomManager.Instance.SearchRoomByMemberID(playerId);
            if (room == null)
            {
                ConsoleWrite.WriteMessage($"[UDP] Ignoring {messageType} from player without a match room: {playerId}", ConsoleColor.Yellow);
                return true;
            }

            var rateLimiter = _matchEventRateLimiters.GetOrAdd(
                playerId,
                _ => new TokenBucket(MatchEventBurstCapacity, MatchEventRatePerSecond));
            if (!rateLimiter.TryConsume(1))
            {
                ConsoleWrite.WriteMessage($"[UDP] Rate limit exceeded for match events from {playerId}", ConsoleColor.Yellow);
                return true;
            }

            message["PlayerID"] = playerId;
            message["PlayerId"] = playerId;
            message["RoomID"] = room.Id.ToString();
            message["RoomId"] = room.Id.ToString();

            if (isSystemEvent)
            {
                InGameMatchEventHandler.HandleTcpSystemEvent(message);
            }
            else
            {
                InGameMatchEventHandler.HandleUdpGameEvent(
                    Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None)),
                    peer.EndPoint.ToString());
            }

            return true;
        }

        private static string CanonicalizeMatchEventType(string messageType)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                return messageType;
            }

            foreach (var knownType in MatchEventTypes)
            {
                if (string.Equals(messageType, knownType, StringComparison.OrdinalIgnoreCase))
                {
                    return knownType;
                }
            }

            return messageType;
        }

        private void RegisterJsonPeer(NetPeer peer, string playerId)
    {
            if (_connectedPlayers.TryGetValue(playerId, out var existing))
            {
                if (existing.PeerId == peer.Id)
                {
                    peer.Tag = playerId;
                    return;
                }

                _server?.GetPeerById(existing.PeerId)?.Disconnect();
            }

            peer.Tag = playerId;
            _pendingPeers.TryRemove(peer.Id, out _);
            _unauthorizedPacketCounts.TryRemove(peer.Id, out _);
            OnPeerConnected(peer);
        }

        private void RegisterUnauthorizedPacket(NetPeer peer)
        {
            var unauthorizedCount = _unauthorizedPacketCounts.AddOrUpdate(peer.Id, 1, (_, count) => count + 1);
            if (unauthorizedCount >= UnauthorizedPacketLimit)
            {
                ConsoleWrite.WriteMessage($"[UDP] Disconnecting unauthenticated peer after {unauthorizedCount} packets: {peer.EndPoint}", ConsoleColor.Yellow);
                peer.Disconnect();
            }
        }

        private static float ReadJsonFloat(JObject? json, params string[] names)
        {
            if (json == null) return 0f;
            foreach (var name in names)
            {
                if (float.TryParse(json[name]?.ToString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)) return value;
            }
            return 0f;
        }

        private static bool ReadJsonBool(JObject json, string name)
        {
            return bool.TryParse(json[name]?.ToString(), out var value) && value;
        }

        private static byte ReadJsonByte(JObject json, params string[] names)
        {
            foreach (var name in names)
            {
                if (byte.TryParse(json[name]?.ToString(), out var value)) return value;
            }
            return 0;
        }

        /// <summary>
        /// プレイヤー移動処理
        /// </summary>
        private void HandlePlayerMove(NetPeer peer, NetDataReader reader)
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

            QueueInput(inputData);
        }

        /// <summary>
        /// プレイヤー射撃処理
        /// </summary>
        private void HandlePlayerShoot(NetPeer peer, NetDataReader reader)
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
            QueueInput(inputData);
        }

        /// <summary>
        /// プレイヤーアクション処理
        /// </summary>
        private void HandlePlayerAction(NetPeer peer, NetDataReader reader)
        {
            var actionType = reader.GetString();

            // アクションに応じた処理
            ConsoleWrite.WriteMessage($"[UDP] Player {peer.Tag} action: {actionType}", ConsoleColor.Gray);
        }

        /// <summary>
        /// Ping処理
        /// </summary>
        private void HandlePing(NetPeer peer, NetDataReader reader)
        {
            var timestamp = reader.GetLong();

            // Pong返信
            SendJsonToPeer(peer, new JObject
            {
                ["MessageType"] = "Pong",
                ["Timestamp"] = timestamp
            }, DeliveryMethod.Unreliable);

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
        public void BroadcastToRoom(int senderId, string messageType, Action<NetDataWriter> writeData)
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

        public void BroadcastToRoom(string senderPlayerId, string messageType)
        {
            var matchRoom = GetMatchRoomForPlayer(senderPlayerId);
            if (matchRoom == null) return;

            var message = new JObject
            {
                ["MessageType"] = messageType,
                ["PlayerID"] = senderPlayerId,
                ["PlayerId"] = senderPlayerId,
                ["RoomID"] = matchRoom.Id.ToString(),
                ["RoomId"] = matchRoom.Id.ToString()
            };

            BroadcastJsonToRoom(matchRoom, message);
        }

        private void BroadcastJsonToRoom(MatchRoom matchRoom, JObject message)
        {
            if (matchRoom == null || message == null) return;

            foreach (var player in matchRoom.Players)
            {
                if (_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                {
                    var peer = _server?.GetPeerById(connectionInfo.PeerId);
                    if (peer != null && peer.ConnectionState == ConnectionState.Connected)
                    {
                        SendJsonToPeer(peer, message, DeliveryMethod.ReliableOrdered);
                    }
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
                .FirstOrDefault(room => room.Players.Any(p =>
                    string.Equals(p.Id, playerId, StringComparison.OrdinalIgnoreCase)));
        }

        private MatchRoom? GetMatchRoomForPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return null;

            return MatchRoomManager.Instance.AllRooms()
                .OfType<MatchRoom>()
                .FirstOrDefault(room => room.Players.Any(p =>
                    string.Equals(p.Id, playerId, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// プレイヤー参加通知
        /// </summary>
        private void NotifyPlayerJoined(string playerId)
        {
            var room = GetMatchRoomForPlayer(playerId);
            if (room == null) return;

            var player = room.Players.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, playerId, StringComparison.OrdinalIgnoreCase));
            var message = new JObject
            {
                ["MessageType"] = "PlayerJoined",
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["PlayerName"] = player?.Name ?? playerId,
                ["Team"] = player?.Team.ToString() ?? "NoTeam",
                ["RoomID"] = room.Id.ToString(),
                ["RoomId"] = room.Id.ToString()
            };

            BroadcastJsonToRoom(room, message);
        }

        /// <summary>
        /// プレイヤー退出通知
        /// </summary>
        private void NotifyPlayerLeft(string playerId)
        {
            var room = GetMatchRoomForPlayer(playerId);
            if (room == null) return;

            var message = new JObject
            {
                ["MessageType"] = "PlayerLeft",
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["Reason"] = "Disconnected",
                ["RoomID"] = room.Id.ToString(),
                ["RoomId"] = room.Id.ToString()
            };

            BroadcastJsonToRoom(room, message);
        }

        /// <summary>
        /// 固定サーバーTick。ネットワーク受信コールバックから入力を直接適用せず、
        /// MatchServerV2のゲームループ上でプレイヤー入力を適用する。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _ = deltaTime;
            ProcessPendingInputs();
            ExpireUnauthenticatedPeers();
            ExpireConnectionTokens();
        }

        private void QueueInput(ClientInputData input)
        {
            if (string.IsNullOrWhiteSpace(input.PlayerId)) return;

            var queue = _pendingInputs.GetOrAdd(
                input.PlayerId,
                _ => new ConcurrentQueue<ClientInputData>());

            // 古い入力を無限に溜めず、遅延が増えた場合は最新側を優先する。
            while (queue.Count >= MaxInputsPerPlayerPerTick * 4 && queue.TryDequeue(out _)) { }
            queue.Enqueue(input);
        }

        private void ProcessPendingInputs()
        {
            foreach (var entry in _pendingInputs)
            {
                if (!_connectedPlayers.ContainsKey(entry.Key))
                {
                    _pendingInputs.TryRemove(entry.Key, out _);
                    continue;
                }

                var processed = 0;
                while (processed++ < MaxInputsPerPlayerPerTick && entry.Value.TryDequeue(out var input))
                {
                    if (!MatchServerV2.Instance.ServerLagCompensationManager.ProcessClientInput(input, out var rejectionReason) &&
                        !string.IsNullOrWhiteSpace(rejectionReason))
                    {
                        ConsoleWrite.WriteMessage($"[UDP] Rejected queued input from {entry.Key}: {rejectionReason}", ConsoleColor.Yellow);
                    }
                }
            }
        }

        private void ExpireUnauthenticatedPeers()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-PendingPeerTimeoutSeconds);
            foreach (var pending in _pendingPeers)
            {
                if (pending.Value > cutoff) continue;

                var peer = _server?.GetPeerById(pending.Key);
                peer?.Disconnect();
                _pendingPeers.TryRemove(pending.Key, out _);
                _unauthorizedPacketCounts.TryRemove(pending.Key, out _);
                ConsoleWrite.WriteMessage($"[UDP] Disconnected peer that did not authenticate within {PendingPeerTimeoutSeconds}s: {pending.Key}", ConsoleColor.Yellow);
            }
        }

        private void ExpireConnectionTokens()
        {
            foreach (var entry in _connectionTokenInfo)
            {
                if (!entry.Value.IsExpired) continue;

                _connectionTokenInfo.TryRemove(entry.Key, out _);
                _connectionTokens.TryRemove(entry.Key, out _);
            }
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
                                if (_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                                {
                                    var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                                    if (peerById != null)
                                    {
                                        SendJsonToPeer(peerById, CreateTransformStateMessage(playerState), DeliveryMethod.Unreliable);
                                    }
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
            StopSnapshotBroadcast();

            if (_server == null)
            {
                _connectedPlayers.Clear();
                _connectionTokens.Clear();
                _connectionTokenInfo.Clear();
                _pendingPeers.Clear();
                _unauthorizedPacketCounts.Clear();
                _pendingInputs.Clear();
                return;
            }

            ConsoleWrite.WriteMessage("[UDP] Shutting down server...", ConsoleColor.Yellow);

            // イベントハンドラー解除
            _listener.ConnectionRequestEvent -= OnConnectionRequest;
            _listener.PeerConnectedEvent -= OnPeerConnected;
            _listener.PeerDisconnectedEvent -= OnPeerDisconnected;
            _listener.NetworkReceiveEvent -= OnNetworkReceive;
            _listener.NetworkErrorEvent -= OnNetworkError;

            if (_server.IsRunning)
            {
                _server.Stop();
            }

            _server = null;
            UdpPort = null;
            _connectedPlayers.Clear();
            _connectionTokens.Clear();
            _connectionTokenInfo.Clear();
            _pendingPeers.Clear();
            _unauthorizedPacketCounts.Clear();
            _pendingInputs.Clear();
            _matchEventRateLimiters.Clear();

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

    internal sealed class ConnectionTokenInfo
    {
        public ConnectionTokenInfo(string token, DateTime expiresAtUtc)
        {
            Token = token;
            ExpiresAtUtc = expiresAtUtc;
        }

        public string Token { get; }
        public DateTime ExpiresAtUtc { get; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    }
}
