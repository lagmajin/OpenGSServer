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
                ConsoleWrite.WriteMessage($"[INFO]Removed player {key} from session list.");
            }

        }
 

        void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {


           if(reader.TryGetByte(out var data))
            {
                ConsoleWrite.WriteMessage(data.ToString());

                // UDPデータをJSONとして処理
                try
                {
                    var jsonString = System.Text.Encoding.UTF8.GetString(new byte[] { data });
                    var json = JObject.Parse(jsonString);
                    InGameMatchEventHandler.HandleTcpSystemEvent(json); // TCPとして処理
                }
                catch
                {
                    // JSONパース失敗時はUDPゲームイベントとして扱う
                    InGameMatchEventHandler.HandleUdpGameEvent(new byte[] { data }, "unknown");
                }
            }



        }



    }
}
