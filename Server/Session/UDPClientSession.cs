
using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class MatchRUdpSession
    {
        private NetPeer peer_;
        public string IP { get; set; }

        public MatchRUdpSession(NetPeer peer)
        {
            peer_ = peer;
            IP = peer.EndPoint.Address.ToString();

            ConsoleWrite.WriteMessage(IP);
        }

        public void SendJson(JObject json)
        {
            var str=json.ToString();

            var writer = new NetDataWriter();
            
            writer.Put(str);
            peer_.Send(writer,DeliveryMethod.ReliableOrdered);

        }
    }
}
