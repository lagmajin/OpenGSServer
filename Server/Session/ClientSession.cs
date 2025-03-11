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
using MessagePack;




namespace OpenGSServer
{
    public interface IClientSession
    {
        string ClientIpAddress();
        public string ID();


        public bool SendAsyncJsonWithTimeStamp(JObject json);
    }
    public class ClientSession : TcpSession,IClientSession
    {
        private string id = "";


        //readonly string rs = ((char)30).ToString();

        //readonly char unitSeperatorChar = (char)Convert.ToInt32("0x1f", 16);

        private string ip = "";

        private string _utf_format = "";

        byte separator = 0x1F;
        public string? PlayerID { get; private set; }

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
            var result = Guid.NewGuid().ToString("N");


            return id;
        }

        public bool SendPing()
        {
            string utcFormat = "hh:mm:ss:FFFF";

            var utcDate = DateTime.UtcNow;

            var json = new JObject();


            json["ServerTimeStampFormat"] = utcFormat;
            json["ServerTimeStampUTC"] = utcDate.ToString(utcFormat);


            SendAsync(json.ToString());

            return true;
        }

        public void SendMessagePackWithTimeStamp(object obj)
        {
            string utcFormat = "HH:mm:ss:ffff";

            var serverTimeStamp = DateTime.UtcNow;

            byte[] serializedData = MessagePackSerializer.Serialize(obj);

            var str=new StringBuilder();
            str.Append("MSG");
            str.Append(serializedData);     // メッセージ
            str.Append(separator);



            SendAsync(str.ToString());

        }
        public bool SendAsyncJsonWithTimeStamp2(JObject obj)
        {
            string utcFormat = "HH:mm:ss:ffff";

            var serverTimeStamp = DateTime.UtcNow;


            //obj["ServerTimeStampFormat"] = utcFormat;
            //obj["ServerTimeStampUTC"] = utcDate.ToString(utcFormat);

            var str = new StringBuilder();
            str.Append("JS");
            str.Append(obj.ToString());     // メッセージ
            str.Append(separator);

            var data=str.ToString();

            //var str = obj.ToString(Formatting.None) + "\n";




            ConsoleWrite.WriteMessage(data, ConsoleColor.Green);


            //Send(str);


            return SendAsync(data);
        }

        public bool SendAsyncJsonWithTimeStamp(JObject obj)
        {
            string utcFormat = "HH:mm:ss:ffff";

            var serverTimeStamp = DateTime.UtcNow;


            //obj["ServerTimeStampFormat"] = utcFormat;
            //obj["ServerTimeStampUTC"] = utcDate.ToString(utcFormat);




            string str = "JS" + obj.ToString(Formatting.None) + Encoding.UTF8.GetString(new byte[] { separator });

            ConsoleWrite.WriteMessage(str, ConsoleColor.Green);


            //Send(str);


            return SendAsync(str);
        }


        public void SendErrorMessage(string errorType,string msg)
        {
            var json = new JObject();

            json["MessageType"] = "";
            json["Message"] = msg;
            SendAsyncJsonWithTimeStamp(json);
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

            Socket.ReceiveTimeout = 6000;
            //Socket.SendTimeout = 1000;

            var jobject = new JObject();

            jobject["MessageType"] = "ConnectServerSuccessful";
            jobject["RSAPublicKey"] = EncryptManager.Instance.GetRSAPublicKey();



            SendAsyncJsonWithTimeStamp(jobject);

        }

        protected override void OnDisconnected()
        {
            //Console.WriteLine($"TCP session with Id {Id} disconnected!");
            ConsoleWrite.WriteMessage($"TCP session with Id {Id} disconnected!", ConsoleColor.Red);


            OldAccountEventHandler.Logout(this);


            Disconnect();

        
       　}
        private List<byte> receiveBuffer = new List<byte>();
        //private readonly byte delimiter = (byte)'\n'; // 制御文字

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // 受信データをバッファに追加
            receiveBuffer.AddRange(buffer.Skip((int)offset).Take((int)size));

            while (true)
            {
                // 制御文字（\n）の位置を探す
                int delimiterIndex = receiveBuffer.IndexOf(separator);
                if (delimiterIndex == -1)
                {
                    // 制御文字がないなら、まだ完全なデータが届いていない
                    return;
                }

                // 1つのメッセージを取り出す
                byte[] completeData = receiveBuffer.Take(delimiterIndex).ToArray();
                receiveBuffer.RemoveRange(0, delimiterIndex + 1); // データ + 制御文字を削除

                // 最低3バイト（識別子+1バイト以上のデータ）がないと無効
                if (completeData.Length < 3) continue;

                // 先頭の識別子を取得
                string identifier = Encoding.UTF8.GetString(completeData, 0, 2);
                byte[] payload = completeData.Skip(2).ToArray();

                if (identifier == "JS") // JSON処理
                {
                    try
                    {
                        string jsonString = Encoding.UTF8.GetString(payload);
                        JObject json = JObject.Parse(jsonString);
                        ParseMessageFromClient(json);
                    }
                    catch (JsonReaderException e)
                    {
                        ConsoleWrite.WriteMessage($"JSON parse error: {e.Message}", ConsoleColor.Red);
                    }
                }
                else if (identifier == "MSG") // MessagePack処理
                {
                    try
                    {
                        var obj = MessagePack.MessagePackSerializer.Deserialize<object>(payload);
                        //HandleMessagePackData(obj);
                    }
                    catch (Exception e)
                    {
                        ConsoleWrite.WriteMessage($"MessagePack parse error: {e.Message}", ConsoleColor.Red);
                    }
                }
                else
                {
                    ConsoleWrite.WriteMessage($"Unknown identifier: {identifier}", ConsoleColor.Yellow);
                }
            }
        }


        /*
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


            if (begin == -1 || end == -1 || end <= begin)
            {
                ConsoleWrite.WriteMessage("Invalid JSON format in received message!", ConsoleColor.Red);
                return;
            }

            // end - begin + 1 に修正して範囲を適切に
            string jsonString = message.Substring(begin, end - begin + 1);
            //var k = message.Substring(begin, end + 1);

            JObject json;

            try
            {

                json = JObject.Parse(jsonString);

            }
            catch (JsonReaderException e)
            {
                ConsoleWrite.WriteMessage($"JSON parse error: {e.Message}", ConsoleColor.Red);
                ConsoleWrite.WriteMessage($"Raw JSON: {jsonString}", ConsoleColor.Red);

                return;
            }


            ParseMessageFromClient(json);



            if (message == "!")
                Disconnect();
        }

        */


        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP session caught an error with code {error}");
        }

        //#networkcore
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

                PlayerID=OldAccountEventHandler.Login(this, dic);
                OldAccountEventHandler.SendUserInfo(this, dic);
            }

            if ("LogoutRequest" == messageType)
            {
                OldAccountEventHandler.Logout(this);
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

                LobbyEventHandler.UpdateRoom(this, dic);

            }

            if ("CreateNewWaitRoomRequest" == messageType)
            {
                ConsoleWrite.WriteMessage("Create");

                LobbyEventHandler.CreateNewWaitRoom(this, dic);



            }

            if ("QuickStartRequest" == messageType)
            {
                ConsoleWrite.WriteMessage("QuickStart", ConsoleColor.Yellow);

                LobbyEventHandler.QuickStartRequest(this, dic);

            }

            if ("EnterRoomRequest" == messageType)
            {

                LobbyEventHandler.EnterRoomRequest(this, dic);
            }

            if("CloseWaitRoomReqauest"==messageType)
            {

            }


            if ("ExitRoomRequest" == messageType)
            {

                WaitRoomEventHandler.ExitRoomRequest(this,dic);

            }

            if ("MatchStartRequest" == messageType)
            {
                var playerID = json["PlayerID"].ToString();
                var roomID = json["RoomID"].ToString();




            }

            if ("AddNewLobbyChat" == messageType)
            {
                //var playerID = json["id"].ToString();

                //var message = json["Message"].ToString();

                /*
                if (string.IsNullOrEmpty(playerID))
                {

                }

                */

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
