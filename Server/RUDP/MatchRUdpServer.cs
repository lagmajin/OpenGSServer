using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;
using OpenGSServer;

namespace OpenGSServer
{
    public partial class MatchRUdpServer
    {
        private EventBasedNetListener listener = new();

        private NetManager server = null;

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        // RUDP再送処理用
        private int sequenceNumber = 0;
        private Dictionary<int, RUDPPacket> sendBuffer = new();
        private Dictionary<string, int> ackBuffer = new(); // playerId -> lastAckedSeq
        private Timer retransmitTimer;
        private const int RETRANSMIT_TIMEOUT = 1000; // ms
        private const int MAX_RETRIES = 5;

        // ハートビート用
        private Dictionary<string, DateTime> lastHeartbeat = new();
        private Timer heartbeatTimer;
        private const int HEARTBEAT_INTERVAL = 5000; // 5秒ごとにハートビート送信
        private const int HEARTBEAT_TIMEOUT = 15000; // 15秒でタイムアウト

        public MatchRUdpServer()
        {

        }

        public void Listen(int port)
        {
            listener.NtpResponseEvent += packet =>
            {

            };

            listener.ConnectionRequestEvent += OnConnectionRequested;

            listener.PeerConnectedEvent += OnConnect;

            listener.PeerDisconnectedEvent += OnDisConnected;
            listener.NetworkReceiveEvent += OnNetworkReceive;

            server = new NetManager(listener);

            server.Start(port);

            // 再送タイマーを開始
            retransmitTimer = new Timer(CheckRetransmit, null, RETRANSMIT_TIMEOUT, RETRANSMIT_TIMEOUT);

            // ハートビートタイマーを開始
            heartbeatTimer = new Timer(SendHeartbeat, null, HEARTBEAT_INTERVAL, HEARTBEAT_INTERVAL);

            Task.Run(() => ServerUpdateTask(tokenSource.Token));

            ConsoleWrite.WriteMessage("[INFO]Match Server(RUDP) Started on Port:" + port.ToString());
        }

        private async Task ServerUpdateTask(CancellationToken token)
        {
            Thread.CurrentThread.Name = "ServerUpdateThread";
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100);
            }
        }

        public void Stop()
        {
            tokenSource.Cancel();
            retransmitTimer?.Dispose();
            heartbeatTimer?.Dispose();
        }

        // 再送チェック
        private void CheckRetransmit(object state)
        {
            var now = DateTime.UtcNow;
            var toRetransmit = new List<int>();

            foreach (var kvp in sendBuffer)
            {
                var packet = kvp.Value;
                if ((now - packet.SentTime).TotalMilliseconds > RETRANSMIT_TIMEOUT)
                {
                    packet.RetryCount++;
                    if (packet.RetryCount < MAX_RETRIES)
                    {
                        toRetransmit.Add(kvp.Key);
                    }
                    else
                    {
                        // 最大リトライを超えたら削除
                        sendBuffer.Remove(kvp.Key);
                        ConsoleWrite.WriteMessage($"[RUDP] Packet {kvp.Key} dropped after max retries");
                    }
                }
            }

            foreach (var seq in toRetransmit)
            {
                if (sendBuffer.TryGetValue(seq, out var packet))
                {
                    RetransmitPacket(packet);
                }
            }
        }

        private void RetransmitPacket(RUDPPacket packet)
        {
            var writer = new NetDataWriter();
            writer.Put(packet.SequenceNumber);
            writer.Put(packet.Data);

            packet.Player.Send(writer, DeliveryMethod.Unreliable); // 信頼性を独自に制御
            packet.SentTime = DateTime.UtcNow;

            ConsoleWrite.WriteMessage($"[RUDP] Retransmitting packet {packet.SequenceNumber} to {packet.Player.EndPoint}");
        }

        public void SendMessageToPlayer(in string id, JObject json)
        {
            if (players.TryGetValue(id, out var player))
            {
                var seq = sequenceNumber++;
                var data = Encoding.UTF8.GetBytes(json.ToString());

                var rudpPacket = new RUDPPacket
                {
                    SequenceNumber = seq,
                    Data = data,
                    Player = player,
                    SentTime = DateTime.UtcNow,
                    RetryCount = 0
                };

                sendBuffer[seq] = rudpPacket;

                var writer = new NetDataWriter();
                writer.Put(seq);
                writer.Put(data);

                player.Send(writer, DeliveryMethod.Unreliable);
            }
        }

        public async Task SendMessageToPlayerAsync(string id)
        {

        }

        public void PolingEvent()
        {
            if (server != null)
            {
                server.PollEvents();
            }
        }

        // ハートビート送信
        private void SendHeartbeat(object state)
        {
            var now = DateTime.UtcNow;
            var toDisconnect = new List<string>();

            foreach (var kvp in lastHeartbeat)
            {
                var playerId = kvp.Key;
                var lastTime = kvp.Value;

                if ((now - lastTime).TotalMilliseconds > HEARTBEAT_TIMEOUT)
                {
                    toDisconnect.Add(playerId);
                }
                else
                {
                    // ハートビート送信
                    if (players.TryGetValue(playerId, out var peer))
                    {
                        var heartbeatJson = new JObject
                        {
                            ["MessageType"] = "Heartbeat",
                            ["Timestamp"] = now.ToString("o")
                        };
                        SendMessageToPlayer(playerId, heartbeatJson);
                    }
                }
            }

            // タイムアウトしたプレイヤーを切断
            foreach (var playerId in toDisconnect)
            {
                if (players.TryGetValue(playerId, out var peer))
                {
                    peer.Disconnect();
                    lastHeartbeat.Remove(playerId);
                    ConsoleWrite.WriteMessage($"[RUDP] Player {playerId} disconnected due to heartbeat timeout");
                }
            }
        }

        // HandleAck, HandleData, SendAckメソッド
        private void HandleAck(NetPeer peer, int seq)
        {
            // ACKを受信したらバッファから削除
            if (sendBuffer.ContainsKey(seq))
            {
                sendBuffer.Remove(seq);
                ConsoleWrite.WriteMessage($"[RUDP] ACK received for packet {seq}");
            }
        }

        private void HandleData(NetPeer peer, int seq, byte[] data)
        {
            // データ処理（既存のロジック）
            try
            {
                var jsonString = Encoding.UTF8.GetString(data);
                var json = JObject.Parse(jsonString);
                InGameMatchEventHandler.HandleTcpSystemEvent(json);
            }
            catch
            {
                InGameMatchEventHandler.HandleUdpGameEvent(data, "unknown");
            }
        }

        private void SendAck(NetPeer peer, int seq)
        {
            var writer = new NetDataWriter();
            writer.Put(seq); // ACKはシーケンス番号のみ
            // データ長0でACKを示す

            peer.Send(writer, DeliveryMethod.Unreliable);
        }

    }

    // RUDPパケットクラス
    public class RUDPPacket
    {
        public int SequenceNumber { get; set; }
        public byte[] Data { get; set; }
        public NetPeer Player { get; set; }
        public DateTime SentTime { get; set; }
        public int RetryCount { get; set; }
    }




}
