using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace OpenGSServer
{


    public partial class MatchRUdpServer
    {
        private List<MatchRUdpSession> sessions=new();
        private Dictionary<string, NetPeer> players = new();
        void OnConnectionRequested(ConnectionRequest request)
        {
            request.Accept();
        }

        void OnConnect(NetPeer peer)
        {
            ConsoleWrite.WriteMessage("Peer connected",ConsoleColor.Green);
            //sessions.Add(new MatchRUdpSession(peer));

            // playersに追加
            string playerId = peer.Id.ToString();
            players[playerId] = peer;

            // ハートビート初期化
            lastHeartbeat[playerId] = DateTime.UtcNow;
            
            var json = new JObject();

            json["MessageType"] = "MatchServerConnectSuccessful";
            json["Test"] = peer.Ping;

            var str = json.ToString();

            var writer = new NetDataWriter();                 // Create writer class
            writer.Put(str);    
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
        private void OnDisConnected(NetPeer peer, DisconnectInfo info)
        {
            ConsoleWrite.WriteMessage("Peer disconnected", ConsoleColor.Red);

            List<string> removedKeys = new List<string>();
            foreach (var kvp in players)
            {
                if (kvp.Value == peer)
                {
                    removedKeys.Add(kvp.Key);
                }
            }
            foreach (var key in removedKeys)
            {
                players.Remove(key);
                lastHeartbeat.Remove(key); // ハートビートからも削除
                ConsoleWrite.WriteMessage($"[INFO]Removed player {key} from session list.");
            }

        }
 

        void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                var seq = reader.GetInt();
                var data = reader.GetRemainingBytes();

                if (data.Length == 0) // ACKパケット
                {
                    HandleAck(peer, seq);
                }
                else
                {
                    // データパケット
                    HandleData(peer, seq, data);

                    // ACK送信
                    SendAck(peer, seq);
                }
            }
            catch
            {
                // 旧処理
                if (reader.TryGetByte(out var data))
                {
                    ConsoleWrite.WriteMessage(data.ToString());

                    try
                    {
                        var jsonString = System.Text.Encoding.UTF8.GetString(new byte[] { data });
                        var json = JObject.Parse(jsonString);

                        // ハートビート応答チェック
                        if (json["MessageType"]?.ToString() == "HeartbeatResponse")
                        {
                            string playerId = GetPlayerIdByPeer(peer);
                            if (playerId != null)
                            {
                                lastHeartbeat[playerId] = DateTime.UtcNow;
                                ConsoleWrite.WriteMessage($"[RUDP] Heartbeat response from {playerId}");
                            }
                        }
                        else
                        {
                            InGameMatchEventHandler.HandleTcpSystemEvent(json);
                        }
                    }
                    catch
                    {
                        InGameMatchEventHandler.HandleUdpGameEvent(new byte[] { data }, "unknown");
                    }
                }
            }
        }

        private string GetPlayerIdByPeer(NetPeer peer)
        {
            foreach (var kvp in players)
            {
                if (kvp.Value == peer)
                {
                    return kvp.Key;
                }
            }
            return null;
        }



    }
}
