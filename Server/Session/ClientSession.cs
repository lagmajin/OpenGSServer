using NetCoreServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class ClientSession : TcpSession
    {
        private string id = "";


        readonly string rs = ((char)30).ToString();

        readonly char unitSeperatorChar = (char)Convert.ToInt32("0x1f", 16);



        public ClientSession(TcpServer server) : base(server) { }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} connected!");

            // Send invite message
            //string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!" ;

            //var e=Encoding.UTF8.GetBytes(message);

            //Send(e);



        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} disconnected!");

            //AccountManager.GetInstance().Logout()
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {


            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

            ConsoleWrite.WriteMessage(message);

            ConsoleWrite.WriteMessage(message.Length.ToString());


            //var matches=new Regex(@"\{(.+?)\}").Matches(message);


            var begin = message.IndexOf("{");

            var end = message.IndexOf("}");

            var k = message.Substring(begin, end + 1);

            JObject json;

            try
            {

                json = JObject.Parse(k);

            }
            catch (JsonReaderException e)
            {
                ConsoleWrite.WriteMessage("Json parse error!!");

                return;
            }


            Parse(json);


            //Send("aa");


            /*
            for(int i=0;i<matches.Count;i++)
            {
                ConsoleWrite.WriteMessage(matches[i].ToString());

                var json = JObject.Parse(matches[i].ToString());

                if(!(json==null))
                {
                    Parse(json);
                }


            }

            */



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


            if ("LoginRequest" == messageType)
            {

                AccountEventDelegate.Login(this, dic);
            }

            if("LogoutRequest"==messageType)
            {
                AccountEventDelegate.Logout(this, dic);
            }

            if("AddFriendRequest"==messageType)
            {
                //AccountEventDelegate.AddFriendRequest()

            }

            if("PlayerInfoRequest"==messageType)
            {

            }
            

            if ("MatchServerInfomationRequest" == messageType)
            {
                var infoJson = new JObject();

                var matchServer = MatchServerV2.GetInstance();



                infoJson["MessageType"] = "MatchServerInfomationNotification";

                infoJson["Port"] = matchServer.Port();

                infoJson["SubPort"] = 2000;

                var str = infoJson.ToString(Formatting.None);

                ConsoleWrite.WriteMessage(str);

                SendAsync(str);

            }

            if ("UpdateRoomRequest" == messageType)
            {

                LobbyEventDelegate.UpdateRoom(this, dic);

            }

            if ("CreateNewRoomRequest" == messageType)
            {





            }

            if ("EnterRoomRequest" == messageType)
            {

                LobbyEventDelegate.EnterRoom(this, dic);
            }


            if ("ExitRoomRequest" == messageType)
            {
                var playerID = json["PlayerID"].ToString();
                var roomID = json["RoomID"].ToString();
                if (string.IsNullOrEmpty(playerID))
                {

                }

            }

            if ("MatchStartRequest" == messageType)
            {
                var playerID = json["PlayerID"].ToString();
                var roomID = json["RoomID"].ToString();




            }

            if ("AddNewLobbyChat" == messageType)
            {
                var playerID = json["id"].ToString();

                var message = json["Message"].ToString();

                if (string.IsNullOrEmpty(playerID))
                {

                }

            }

            if ("AddNewRoomChatRequest" == messageType)
            {
                string idString;
                string roomIdString;

                if (json.TryGetValue("id", out var id))
                {


                }
                else
                {

                    return;
                }

                if (json.TryGetValue("roomid", out var roomID))
                {

                }

                //var id = json["id"].ToString();
                //var roomId = json["roomid"].ToString();
                //var chat = json["message"].ToString();



            }

        }

        protected override void OnSent(long sent, long pending)
        {
            ConsoleWrite.WriteMessage("adfdfetrererere");
        }
    }

}
