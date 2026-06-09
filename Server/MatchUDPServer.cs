
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;

namespace OpenGSServer
{
    /// <summary>
    /// Match UDP server — LiteNetLib transport with JSON payloads.
    /// Payload format matches OpenGSR client (UTF-8 JSON bytes).
    /// </summary>
    public sealed class MatchUDPServer : IDisposable
    {
        private const string ConnectionKey = "OpenGS";
        private static readonly Encoding Utf8 = Encoding.UTF8;

        private NetManager? _server;
        private readonly EventBasedNetListener _listener = new();
        private readonly ConcurrentDictionary<string, PlayerConnectionInfo> _connectedPlayers = new();
        private bool _disposed;

        public bool IsRunning => _server?.IsRunning ?? false;
        public int ConnectedPlayerCount => _connectedPlayers.Count;

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

            StartSnapshotBroadcast(50);

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.NetworkErrorEvent += OnNetworkError;

            ConsoleWrite.WriteMessage($"[UDP] Server started on port {port}", ConsoleColor.Green);
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            var reader = request.Data;
            string playerId = reader.GetString();
            reader.Recycle();

            if (!string.IsNullOrEmpty(playerId))
            {
                var peer = request.AcceptIfKey(ConnectionKey);
                if (peer != null)
                {
                    peer.Tag = playerId;
                    ConsoleWrite.WriteMessage($"[UDP] Connection accepted from {peer.EndPoint} for Player: {playerId}", ConsoleColor.Green);
                }
                else
                {
                    ConsoleWrite.WriteMessage("[UDP] Connection rejected (invalid key or PlayerID missing)", ConsoleColor.Yellow);
                }
            }
            else
            {
                request.Reject();
                ConsoleWrite.WriteMessage("[UDP] Connection rejected (PlayerID missing)", ConsoleColor.Yellow);
            }
        }

        private void OnPeerConnected(NetPeer peer)
        {
            string playerId = peer.Tag?.ToString() ?? "Unknown";

            var playerInfo = new PlayerConnectionInfo
            {
                PeerId = peer.Id,
                EndPoint = peer.EndPoint.ToString(),
                ConnectedAt = DateTime.UtcNow,
                PlayerId = playerId
            };

            _connectedPlayers[playerId] = playerInfo;

            ConsoleWrite.WriteMessage($"[UDP] Player connected: {playerId} ({peer.Id}) from {peer.EndPoint}", ConsoleColor.Green);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);

            MatchServerV2.Instance.ServerLagCompensationManager.AddPlayer(playerId);
            MatchServerV2.Instance.ServerLagCompensationManager.RegisterClientCallback(
                playerId,
                state => SendTransformStateToClient(peer, state)
            );
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            string playerId = peer.Tag?.ToString() ?? "Unknown";
            _connectedPlayers.TryRemove(playerId, out _);

            ConsoleWrite.WriteMessage(
                $"[UDP] Player disconnected: {playerId} ({peer.Id}) (Reason: {info.Reason})",
                ConsoleColor.Yellow);
            ConsoleWrite.WriteMessage($"[UDP] Total players: {_connectedPlayers.Count}", ConsoleColor.Cyan);

            MatchServerV2.Instance.ServerLagCompensationManager.RemovePlayer(playerId);
        }

        // --- Outbound: JSON → UTF-8 bytes → NetDataWriter ---

        private void SendJson(NetPeer peer, JObject json, DeliveryMethod deliveryMethod)
        {
            if (peer == null || peer.ConnectionState != ConnectionState.Connected) return;
            var raw = Utf8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            var writer = new NetDataWriter();
            writer.Put(raw);
            peer.Send(writer, deliveryMethod);
        }

        private void SendTransformStateToClient(NetPeer peer, ServerTransformState state)
        {
            if (peer == null || peer.ConnectionState != ConnectionState.Connected) return;

            var json = new JObject
            {
                ["MessageType"] = "ServerTransformState",
                ["NetworkId"] = state.NetworkId,
                ["PlayerId"] = state.PlayerId,
                ["PositionX"] = state.PositionX,
                ["PositionY"] = state.PositionY,
                ["PositionZ"] = state.PositionZ,
                ["RotationX"] = state.RotationX,
                ["RotationY"] = state.RotationY,
                ["RotationZ"] = state.RotationZ,
                ["RotationW"] = state.RotationW,
                ["VelocityX"] = state.VelocityX,
                ["VelocityY"] = state.VelocityY,
                ["VelocityZ"] = state.VelocityZ,
                ["Timestamp"] = state.Timestamp,
                ["SequenceNumber"] = state.SequenceNumber
            };
            SendJson(peer, json, DeliveryMethod.Unreliable);
        }

        // --- Inbound: UTF-8 bytes → JSON string → JObject ---

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                var rawBytes = reader.GetRemainingBytes();
                if (rawBytes == null || rawBytes.Length == 0) return;

                var jsonString = Utf8.GetString(rawBytes);
                var json = JObject.Parse(jsonString);
                var messageType = json.Value<string>("MessageType") ?? string.Empty;

                switch (messageType)
                {
                    case "PlayerMove":
                        HandlePlayerMove(peer, json);
                        break;
                    case "PlayerShoot":
                    case "ShootRequest":
                        HandlePlayerShoot(peer, json);
                        break;
                    case "PlayerAction":
                        HandlePlayerAction(peer, json);
                        break;
                    case "Ping":
                    case "PingRequest":
                        HandlePing(peer, json);
                        break;
                    case "ClientConnect":
                        break; // peer.Tag already set during connection request
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

        private void HandlePlayerMove(NetPeer peer, JObject json)
        {
            string playerId = peer.Tag?.ToString() ?? "Unknown";

            var inputData = new ClientInputData
            {
                PlayerId = playerId,
                MoveX = json.Value<float?>("MoveX") ?? json.Value<float?>("PosX") ?? 0f,
                MoveY = json.Value<float?>("MoveY") ?? json.Value<float?>("PosY") ?? 0f,
                MoveZ = 0f,
                LookX = json.Value<float?>("LookX") ?? json.Value<float?>("DirX") ?? 0f,
                LookY = json.Value<float?>("LookY") ?? json.Value<float?>("DirY") ?? 0f,
                Jump = json.Value<bool?>("Jump") ?? false,
                Fire = json.Value<bool?>("Fire") ?? false,
                SequenceNumber = (byte)(json.Value<int?>("SequenceNumber") ?? 0),
                Timestamp = json.Value<float?>("Timestamp") ?? 0f,
                DeltaTime = json.Value<float?>("DeltaTime") ?? 0f
            };

            if (!MatchServerV2.Instance.ServerLagCompensationManager.ProcessClientInput(inputData, out var rejectionReason))
            {
                ConsoleWrite.WriteMessage($"[UDP] Rejected move input from {playerId}: {rejectionReason}", ConsoleColor.Yellow);
            }
        }

        private void HandlePlayerShoot(NetPeer peer, JObject json)
        {
            string playerId = peer.Tag?.ToString() ?? "Unknown";

            var inputData = new ClientInputData
            {
                PlayerId = playerId,
                MoveX = 0f,
                MoveY = 0f,
                MoveZ = 0f,
                LookX = json.Value<float?>("DirX") ?? json.Value<float?>("Angle") ?? 0f,
                LookY = json.Value<float?>("DirY") ?? 0f,
                Jump = false,
                Fire = true,
                SequenceNumber = (byte)(json.Value<int?>("SequenceNumber") ?? 0),
                Timestamp = json.Value<float?>("Timestamp") ?? 0f,
                DeltaTime = json.Value<float?>("DeltaTime") ?? 0f
            };

            if (!MatchServerV2.Instance.ServerLagCompensationManager.ProcessClientInput(inputData, out var rejectionReason))
            {
                ConsoleWrite.WriteMessage($"[UDP] Rejected shoot input from {playerId}: {rejectionReason}", ConsoleColor.Yellow);
            }
        }

        private void HandlePlayerAction(NetPeer peer, JObject json)
        {
            var actionType = json.Value<string>("ActionType") ?? "Unknown";
            ConsoleWrite.WriteMessage($"[UDP] Player {peer.Tag} action: {actionType}", ConsoleColor.Gray);
        }

        private void HandlePing(NetPeer peer, JObject json)
        {
            var clientTimestamp = json.Value<long?>("ClientTimestamp") ?? 0;

            var pong = new JObject
            {
                ["MessageType"] = "PingResponse",
                ["ClientTimestamp"] = clientTimestamp,
                ["ServerTimestamp"] = DateTime.UtcNow.Ticks
            };
            SendJson(peer, pong, DeliveryMethod.Unreliable);

            string playerId = peer.Tag?.ToString() ?? "Unknown";
            var rtt = (DateTime.UtcNow.Ticks - clientTimestamp) / TimeSpan.TicksPerMillisecond;
            ConsoleWrite.WriteMessage($"[UDP] Ping from {playerId}: {rtt}ms", ConsoleColor.Gray);
        }

        private void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            ConsoleWrite.WriteMessage($"[UDP] Network error from {endPoint}: {socketError}", ConsoleColor.Red);
        }

        // --- Broadcast ---

        public void BroadcastToRoom(int senderId, string messageType, Action<NetDataWriter> writeData)
        {
            var matchRoom = GetMatchRoomForPlayer(senderId);
            if (matchRoom == null) return;

            var writer = new NetDataWriter();
            writer.Put(messageType);
            writeData(writer);

            foreach (var player in matchRoom.Players)
            {
                if (_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                {
                    var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                    peerById?.Send(writer, DeliveryMethod.ReliableOrdered);
                }
            }
        }

        public void BroadcastJsonToRoom(string roomId, JObject json, DeliveryMethod method = DeliveryMethod.Unreliable)
        {
            var raw = Utf8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            var writer = new NetDataWriter();
            writer.Put(raw);

            var matchRoomManager = MatchRoomManager.Instance;
            var allRooms = matchRoomManager.AllRooms();

            foreach (var abstractRoom in allRooms)
            {
                if (abstractRoom is MatchRoom room && room.Id.ToString() == roomId)
                {
                    foreach (var player in room.Players)
                    {
                        if (_connectedPlayers.TryGetValue(player.Id, out var connectionInfo))
                        {
                            var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                            peerById?.Send(writer, method);
                        }
                    }
                    break;
                }
            }
        }

        private MatchRoom? GetMatchRoomForPlayer(int peerId)
        {
            var playerId = _connectedPlayers.FirstOrDefault(x => x.Value.PeerId == peerId).Key;
            if (string.IsNullOrEmpty(playerId)) return null;

            var matchRoomManager = MatchRoomManager.Instance;
            var allRooms = matchRoomManager.AllRooms();

            return allRooms
                .OfType<MatchRoom>()
                .FirstOrDefault(room => room.Players.Any(p => p.Id == playerId));
        }

        private void NotifyPlayerJoined(string playerId) { }
        private void NotifyPlayerLeft(string playerId) { }

        public void PollingEvent()
        {
            _server?.PollEvents();
        }

        // --- Snapshot broadcast (20Hz default) ---

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
                var lagCompManager = MatchServerV2.Instance.ServerLagCompensationManager;

                foreach (var abstractRoom in rooms)
                {
                    if (abstractRoom is not MatchRoom room) continue;

                    foreach (var player in room.Players)
                    {
                        var playerState = lagCompManager.GetPlayerState(player.Id);
                        if (playerState.PlayerId == null) continue;
                        if (!_connectedPlayers.TryGetValue(player.Id, out var connectionInfo)) continue;

                        var peerById = _server?.GetPeerById(connectionInfo.PeerId);
                        if (peerById == null) continue;

                        var json = new JObject
                        {
                            ["MessageType"] = "ServerTransformState",
                            ["NetworkId"] = playerState.NetworkId,
                            ["PlayerId"] = playerState.PlayerId,
                            ["PositionX"] = playerState.PositionX,
                            ["PositionY"] = playerState.PositionY,
                            ["PositionZ"] = playerState.PositionZ,
                            ["RotationX"] = playerState.RotationX,
                            ["RotationY"] = playerState.RotationY,
                            ["RotationZ"] = playerState.RotationZ,
                            ["RotationW"] = playerState.RotationW,
                            ["VelocityX"] = playerState.VelocityX,
                            ["VelocityY"] = playerState.VelocityY,
                            ["VelocityZ"] = playerState.VelocityZ,
                            ["Timestamp"] = playerState.Timestamp,
                            ["SequenceNumber"] = playerState.SequenceNumber
                        };
                        var raw = Utf8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
                        var writer = new NetDataWriter();
                        writer.Put(raw);
                        peerById.Send(writer, DeliveryMethod.Unreliable);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[UDP] Error broadcasting snapshots: {ex.Message}", ConsoleColor.Red);
            }
        }

        public void Shutdown()
        {
            if (_server == null || !_server.IsRunning) return;

            ConsoleWrite.WriteMessage("[UDP] Shutting down server...", ConsoleColor.Yellow);

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

    internal sealed class PlayerConnectionInfo
    {
        public required int PeerId { get; init; }
        public required string EndPoint { get; init; }
        public required DateTime ConnectedAt { get; init; }
        public required string PlayerId { get; init; }
    }
}
