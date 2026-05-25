using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class MatchRUdpSession
    {
        private readonly NetPeer peer_;
        public string IP { get; set; }
        public string PeerId { get; private set; } = string.Empty;
        public bool IsConnected => peer_ != null && peer_.ConnectionState == ConnectionState.Connected;
        public DateTime? LastSentUtc { get; private set; }
        public string LastErrorMessage { get; private set; } = string.Empty;

        public MatchRUdpSession(NetPeer peer)
        {
            peer_ = peer;
            IP = peer.EndPoint.Address.ToString();
            PeerId = peer.Id.ToString();

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
            LastSentUtc = DateTime.UtcNow;
        }

        public Task SendJsonAsync(JObject json)
        {
            SendJson(json);
            return Task.CompletedTask;
        }

        public void SendErrorMessage(string errorMsg)
        {
            LastErrorMessage = errorMsg ?? string.Empty;
            SendJson(new JObject
            {
                ["MessageType"] = ServerMessageTypes.Error,
                ["ErrorMessage"] = LastErrorMessage
            });
        }

        public Task SendErrorMessageAsync(string errorMsg)
        {
            SendErrorMessage(errorMsg);
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            peer_?.Disconnect();
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["IP"] = IP,
                ["PeerId"] = PeerId,
                ["IsConnected"] = IsConnected,
                ["LastSentUtc"] = LastSentUtc?.ToString("O") ?? string.Empty,
                ["LastErrorMessage"] = LastErrorMessage
            };
        }
    }
}
