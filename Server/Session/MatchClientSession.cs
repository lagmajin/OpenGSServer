using NetCoreServer;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenGSServer
{

    public class ClientMatchSession : TcpSession
    {
        public ClientMatchSession(TcpServer server) : base(server) { }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";
            SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

            var matches = new Regex(@"\{(.+?)\}").Matches(message);

            ConsoleWrite.WriteMessage(message.Length.ToString());


            for (int i = 0; i < matches.Count; i++)
            {
                ConsoleWrite.WriteMessage(matches[i].ToString());

                var json = JObject.Parse(matches[i].ToString());

                if (!(json == null))
                {
                    Parse(json);
                }


            }



            //var json = System.Text.Json.JsonDocument.Parse(message);

            //ConsoleWrite.WriteMessage(json.ToString());

            // var id = json["id"].ToString();
            // var pass = json["pass"].ToString();



            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP session caught an error with code {error}");
        }

        private void Parse(in JObject json)
        {


            /*
            if(json.TryGetValue("MessageType",out var value))
            {
                messageType=value.ToString();


            }
            else
            {
                return;
            }
            */
            string messageType;

            IDictionary<string, JToken> dic = json;


            if (dic.ContainsKey("MessageType"))
            {
                messageType = dic["MessageType"].ToString();
            }
            else
            {

                return;
            }


            //JsonUtility.TakeValueToString(json,"MessageType")

            //var messageType = json["MessageType"].ToString();


            if (messageType == "Shot")
            {

            }

            if (messageType == "PlayerWeaponChanged")
            {

            }

            if (messageType == "TakeFieldItem")
            {


                json["PlayerID"].ToString();
                json["FieldItemType"].ToString();



            }

            if (messageType == "PlayerBurst")
            {

            }

            if (messageType == "PlayerDead")
            {

            }

            if (messageType == "PlayerUpdate")
            {

            }

            // Grenade messages (client-authoritative): forward to room handler which will broadcast to room players
            if (messageType == "GrenadeSpawn" || messageType == "GrenadeUpdate" || messageType == "GrenadeExplode")
            {
                try
                {
                    // Forward to match room handler (expects PlayerID and RoomID in JSON)
                    InGameMatchEventHandler.ParseTcpEvent(json);
                }
                catch (Exception ex)
                {
                    ConsoleWrite.WriteMessage($"[ERROR] Grenade message handling failed: {ex.Message}", ConsoleWrite.eMessageType.Error);
                }

                return;
            }

            if (messageType == "FlagReturnRequest")
            {

            }

            if (messageType == "FlagRecovery")
            {

            }

        }

    }




}
