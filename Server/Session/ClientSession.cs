using NetCoreServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Buffer = NetCoreServer.Buffer;

namespace OpenGSServer
{
    public class ClientSession : TcpSession
    {
        private string id = "";


        readonly string rs = ((char)30).ToString();

        readonly char unitSeperatorChar = (char)Convert.ToInt32("0x1f", 16);

        private string ip = "";

        private string _utf_format = "";


        public ClientSession(TcpServer server) : base(server) { }

        private void setIPAddress()
        {
            var endpoint = (IPEndPoint)Socket.RemoteEndPoint;

            var v = IPAddress.Parse(endpoint.Address.ToString());

            ip = v.ToString();
        }
        public string ClientIpAddress()
        {
            return ip;
        }

        public string ID()
        {
            return id;
        }

        public bool SendPing()
        {
            string utcFormat = "hh:mm:ss:FFFF";

            var utcDate = DateTime.UtcNow;

            var json = new JObject();


            json["ServerTimeStampFormat"] = utcFormat;
            json["ServerTimeStampUTC"] = utcDate.ToString(utcFormat);


            return true;
        }

        public bool SendJsonAsyncWithTimeStamp(JObject obj)
        {
            string utcFormat = "hh:mm:ss:ffff";

            var utcDate = DateTime.UtcNow;


            //obj["ServerTimeStampFormat"] = utcFormat;
            //obj["ServerTimeStampUTC"] = utcDate.ToString(utcFormat);

            var str = obj.ToString();

            ConsoleWrite.WriteMessage(str, ConsoleColor.Green);


            //Send(str);


            return SendAsync(str);
        }




        protected override void OnConnected()
        {
            //this.Socket.RemoteEndPoint.
            if (ip == "")
            {
                setIPAddress();
            }


            ConsoleWrite.WriteMessage("IP Address:" + ip, ConsoleColor.Green);
            ConsoleWrite.WriteMessage($"TCP session with Id {Id} connected!", ConsoleColor.DarkMagenta);

            //Console.WriteLine(endpoint.ToString());

            Socket.ReceiveTimeout = 1000;
            Socket.SendTimeout = 1000;




        }

        protected override void OnDisconnected()
        {
            //Console.WriteLine($"TCP session with Id {Id} disconnected!");
            ConsoleWrite.WriteMessage($"TCP session with Id {Id} disconnected!", ConsoleColor.Red);

            Disconnect();

        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            if (string.IsNullOrEmpty(ip))
            {
                setIPAddress();
            }


            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

            ConsoleWrite.WriteMessage(message, ConsoleColor.Cyan);

            ConsoleWrite.WriteMessage(message.Length.ToString(), ConsoleColor.Cyan);


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
                ConsoleWrite.WriteMessage("Json parse error!!", ConsoleColor.Red);

                return;
            }


            ParseMessageFromClient(json);



            if (message == "!")
                Disconnect();
        }




        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP session caught an error with code {error}");
        }

        private void ParseMessageFromClient(in JObject json)
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

            if (dic.ContainsKey("ClientTimeStampUTC"))
            {
                var str = dic["ClientTimeStampUTC"].ToString();

                var time = DateTime.ParseExact(str, "hh:mm:ss:ffff", null);


                ConsoleWrite.WriteMessage("ParseTime" + time.ToString("ffff"));
            }


            if ("LoginRequest" == messageType)
            {

                AccountEventDelegate.Login(this, dic);
                AccountEventDelegate.SendUserInfo(this, dic);
            }

            if ("LogoutRequest" == messageType)
            {
                AccountEventDelegate.Logout(this, dic);
            }

            if ("AddFriendRequest" == messageType)
            {
                //AccountEventDelegate.AddFriendRequest()

            }

            if ("PlayerInfoRequest" == messageType)
            {

            }


            if ("MatchServerInformationRequest" == messageType)
            {
                var infoJson = new JObject();

                var matchServer = MatchServerV2.GetInstance();



                infoJson["MessageType"] = "MatchServerInformationNotification";

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

                LobbyEventDelegate.EnterRoomRequest(this, dic);
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
            ConsoleWrite.WriteMessage("OnSent");
        }
    }

}
