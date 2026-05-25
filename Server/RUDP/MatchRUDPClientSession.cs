using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class MatchRUdpSession
    {
        private readonly NetPeer peer_;
        public string IP { get; set; }

        public MatchRUdpSession(NetPeer peer)
        {
            peer_ = peer;
            IP = peer.EndPoint.Address.ToString();

            ConsoleWrite.WriteMessage(IP);
        }

        public void SendJson(JObject json)
        {
            if (peer_ == null || json == null)
            {
                return;
            }

            var writer = new NetDataWriter();
            writer.Put(json.ToString());
            peer_.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        public Task SendJsonAsync(JObject json)
        {
            SendJson(json);
            return Task.CompletedTask;
        }

        public void SendErrorMessage(string errorMsg)
        {
            SendJson(new JObject
            {
                ["MessageType"] = ServerMessageTypes.Error,
                ["ErrorMessage"] = errorMsg ?? string.Empty
            });
        }

        public Task SendErrorMessageAsync(string errorMsg)
        {
            SendErrorMessage(errorMsg);
            return Task.CompletedTask;
        }
    }
}
