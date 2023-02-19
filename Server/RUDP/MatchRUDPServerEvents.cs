
using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{


    public partial class MatchRUdpServer
    {
        private List<MatchRUdpSession> sessions=new();

        void OnConnectionRequested(ConnectionRequest request)
        {
            request.Accept();
        }

        void OnConnect(NetPeer peer)
        {ConsoleWrite.WriteMessage("Peer connected",ConsoleColor.Green);
            sessions.Add(new MatchRUdpSession(peer));
            
            
            var json = new JObject();

            json["MessageType"] = "MatchServerConnectSuccessful";
            json["Test"] = peer.Ping;

            var str = json.ToString();

            var writer = new NetDataWriter();                 // Create writer class
            writer.Put(str);    
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        void OnDisConnected()
        {

        }

        void OnDataRecieved()
        {

        }


        private void OnDisConnected(NetPeer peer, DisconnectInfo info)
        {
            ConsoleWrite.WriteMessage("Peer disconnected",ConsoleColor.Red);


        }
    }
}
