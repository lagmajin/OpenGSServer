using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;

#nullable enable

namespace OpenGSServer
{
    /// <summary>
    /// A LiteNetLib client used to exercise the match RUDP path without Unity.
    /// <para>
    /// S2 asks for the match progression to be server authoritative, and the
    /// playthrough smoke covers the lobby side of that. This covers the other
    /// half: the UDP listener accepts a connection, the token handshake
    /// registers the peer, and position input reaches the authoritative state
    /// manager. It speaks the same wire format as ClientNetworkManager, using
    /// the same LiteNetLib build, so no reimplementation of the protocol is
    /// needed.
    /// </para>
    /// </summary>
    /// <summary>Exposed so the integration test can attach a receive listener.</summary>
    public sealed class MatchRudpProbe : IDisposable
    {
        private const string ConnectionKey = "OpenGS";

        private readonly EventBasedNetListener listener = new();
        private readonly NetManager client;

        /// <summary>The underlying listener, so a test can subscribe to packets.</summary>
        public EventBasedNetListener Listener => listener;
        private readonly NetDataWriter writer = new();
        private readonly BlockingCollection<JObject> received = new();
        private readonly ConcurrentDictionary<string, JObject> positionsByPlayer = new();

        private NetPeer? peer;
        private int disposed;

        public MatchRudpProbe()
        {
            // LiteNetLib 0.9: the listener owns the packet events, and Start()
            // takes no arguments. The caller supplies the endpoint to Connect.
            client = new NetManager(listener);
        }

        /// <summary>
        /// Sends a json payload as the raw utf-8 bytes a real client sends.
        /// <para>
        /// The server sniffs the first byte of a packet to tell json from its own
        /// binary format, so a payload written with Put(string) arrives behind a
        /// length prefix and is read as binary instead. That is not a detail of the
        /// probe: the peer then never registers, the handshake fails silently and
        /// every later message is refused, so the probe has to frame its packets
        /// exactly the way the client does.
        /// </para>
        /// </summary>
        private void Send(JObject payload, DeliveryMethod method)
        {
            if (peer == null)
            {
                throw new InvalidOperationException("The probe is not connected.");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(payload.ToString());
            peer.Send(bytes, 0, method);
        }

        /// <summary>Connects and completes the ClientConnect handshake.</summary>
        public bool Connect(
            string host,
            int port,
            string playerId,
            string token,
            string roomId,
            int timeoutMs,
            Action? pumpServer = null)
        {
            if (!client.Start())
            {
                return false;
            }

            var peerConnection = client.Connect(host, port, ConnectionKey);
            if (peerConnection == null)
            {
                client.Stop();
                return false;
            }

            var deadline = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                // In production the listener is polled from the match loop. A
                // caller that drives the server itself has to pump it here,
                // otherwise the connection request is never even seen.
                pumpServer?.Invoke();
                client.PollEvents();
                if (peerConnection.ConnectionState == ConnectionState.Connected)
                {
                    peer = peerConnection;
                    break;
                }

                Thread.Sleep(10);
            }

            if (peer == null)
            {
                return false;
            }

            peer.Tag = playerId;

            Send(new JObject
            {
                ["MessageType"] = "ClientConnect",
                ["PlayerID"] = playerId,
                ["RoomID"] = roomId,
                ["UdpToken"] = token
            }, DeliveryMethod.ReliableOrdered);

            // The server only registers the peer once it has processed the
            // handshake, so wait for it here with the server being polled. Polling
            // only the probe would let the handshake sit unsent and every later
            // message would be refused as coming from an unregistered peer.
            Poll(500, pumpServer);

            // The server answers a rejected handshake by dropping the peer, so a
            // peer still connected afterwards is the evidence the token was
            // accepted. Reporting success without checking would let a test pass on
            // a connection the server never registered.
            return peer.ConnectionState == ConnectionState.Connected;
        }

        /// <summary>Sends an arbitrary json message on the realtime channel.</summary>
        public void Send(string playerId, string roomId, JObject payload)
        {
            payload["PlayerID"] = playerId;
            payload["RoomID"] = roomId;
            Send(payload, DeliveryMethod.ReliableOrdered);
        }

        /// <summary>Sends one authoritative position input.</summary>
        public void SendPosition(
            string playerId,
            string roomId,
            float x,
            float y,
            float z,
            float rotation,
            float deltaTime,
            byte sequence)
        {
            if (peer == null)
            {
                throw new InvalidOperationException("The probe is not connected.");
            }

            Send(new JObject
            {
                ["MessageType"] = "PlayerPositionUpdate",
                ["PlayerID"] = playerId,
                ["RoomID"] = roomId,
                ["PosX"] = x,
                ["PosY"] = y,
                ["PosZ"] = z,
                ["Rotation"] = rotation,
                ["DeltaTime"] = deltaTime,
                ["SequenceNumber"] = sequence,
                ["Timestamp"] = Environment.TickCount64 / 1000.0
            }, DeliveryMethod.Unreliable);
        }

        /// <summary>Sends a movement input packet.</summary>
        public void SendMove(
            string playerId,
            string roomId,
            float moveX,
            float moveY,
            bool fire,
            bool jump,
            float deltaTime,
            byte sequence)
        {
            if (peer == null)
            {
                throw new InvalidOperationException("The probe is not connected.");
            }

            Send(new JObject
            {
                ["MessageType"] = "PlayerMove",
                ["PlayerID"] = playerId,
                ["RoomID"] = roomId,
                ["VelX"] = moveX,
                ["VelY"] = moveY,
                ["MoveX"] = moveX,
                ["MoveY"] = moveY,
                ["Fire"] = fire,
                ["Jump"] = jump,
                ["DeltaTime"] = deltaTime,
                ["SequenceNumber"] = sequence,
                ["Timestamp"] = Environment.TickCount64 / 1000.0
            }, DeliveryMethod.Unreliable);
        }

        /// <summary>Polls for <paramref name="durationMs"/> and queues what arrives.</summary>
        public void Poll(int durationMs)
        {
            Poll(durationMs, null);
        }

        /// <summary>
        /// Polls the probe and, when supplied, the server.
        /// <para>
        /// The probe only transmits what it sends while it is polled, and the
        /// server only receives a packet while it is polled. A caller that owns
        /// the server therefore has to pass its pump here, or the two never
        /// actually talk: the probe believes it sent a message and the server
        /// never receives one.
        /// </para>
        /// </summary>
        public void Poll(int durationMs, Action? pumpServer)
        {
            var deadline = Environment.TickCount64 + durationMs;
            while (Environment.TickCount64 < deadline)
            {
                client.PollEvents();
                pumpServer?.Invoke();
                Thread.Sleep(5);
            }
        }

        /// <summary>Takes a queued message that satisfies <paramref name="predicate"/>.</summary>
        public bool TryTake(Func<JObject, bool> predicate, out JObject message)
        {
            while (received.TryTake(out var candidate))
            {
                if (predicate(candidate))
                {
                    message = candidate;
                    return true;
                }
            }

            message = null!;
            return false;
        }

        /// <summary>Queues a received JSON payload.</summary>
        public void Offer(JObject message)
        {
            if (message == null)
            {
                return;
            }

            if (message["PosX"] != null || message["Position"] != null)
            {
                var playerId = message["PlayerID"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(playerId))
                {
                    positionsByPlayer[playerId] = message;
                }
            }

            received.TryAdd(message);
        }

        /// <summary>The last position the server echoed back for a player.</summary>
        public bool TryGetEchoedPosition(string playerId, out JObject message)
        {
            if (positionsByPlayer.TryGetValue(playerId, out var found))
            {
                message = found;
                return true;
            }

            message = null!;
            return false;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                received.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                peer?.Disconnect();
                client.PollEvents();
                client.Stop();
            }
            catch (Exception)
            {
                // The probe is test only; a teardown failure is not interesting.
            }

            writer.Reset();
        }
    }
}
