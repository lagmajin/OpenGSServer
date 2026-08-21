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
//using Microsoft.VisualBasic;

using MessagePack;
using System.Diagnostics;

using Buffer = NetCoreServer.Buffer;
using Cysharp.Text;




#nullable enable

namespace OpenGSServer
{
    public interface IClientSession
    {
        string ClientIpAddress();
        public string ID();


        public bool SendAsyncJsonWithTimeStamp(JObject json);
    }

    //#
    public class ClientSession : TcpSession,IClientSession
    {
        //readonly string rs = ((char)30).ToString();

        //readonly char unitSeperatorChar = (char)Convert.ToInt32("0x1f", 16);

        private string ip = "";

        //private byte separator = 0x1F;

        private char separator = '\u001F';

        private Stopwatch forPing=new();

        public string? PlayerID { get; private set; }

        public void SetPlayerID(string id)
        {
            PlayerID = id;
        }

        public void ClearPlayerID()
        {
            PlayerID = null;
        }

        public ClientSession(TcpServer server) : base(server) { }

        private void setIPAddress()
        {
            if (Socket.RemoteEndPoint is IPEndPoint endpoint)
            {
                ip = endpoint.Address.ToString();
            }
        }
        public string ClientIpAddress()
        {
            return ip;
        }

        public string ID()
        {
            return Id.ToString();
        }

        public bool SendPing()
        {
            string utcFormat = "HH:mm:ss:FFFF";

            var utcDate = DateTime.UtcNow;

            var json = new JObject();


            json["ServerTimeStampFormat"] = utcFormat;
            json["ServerTimeStampUTC"] = utcDate.ToString(utcFormat);


            SendAsync(json.ToString());

            return true;
        }

        public bool SendPingRequestToClient()
        {

            return true;
        }

        private void HandlePongFromServer()
        {

        }

        public void SendMessagePackWithTimeStamp(object obj)
        {
            byte[] serializedData = MessagePackSerializer.Serialize(obj);
            var prefix = Encoding.UTF8.GetBytes("MP");
            var separatorBytes = new[] { (byte)separator };
            var frame = new byte[prefix.Length + serializedData.Length + separatorBytes.Length];

            System.Buffer.BlockCopy(prefix, 0, frame, 0, prefix.Length);
            System.Buffer.BlockCopy(serializedData, 0, frame, prefix.Length, serializedData.Length);
            System.Buffer.BlockCopy(separatorBytes, 0, frame, prefix.Length + serializedData.Length, separatorBytes.Length);

            SendAsync(frame);

        }
        public bool SendAsyncJsonWithTimeStamp2(JObject obj)
        {
            var str = new StringBuilder();
            str.Append("JS");
            str.Append(obj.ToString());     // メッセージ
            str.Append(separator);

            var data=str.ToString();




            ConsoleWrite.WriteMessage(data, ConsoleColor.Green);


            return SendAsync(data);
        }

        public bool SendAsyncJsonWithTimeStamp(JObject obj)
        {
            //obj["ServerTimeStampFormat"] = utcFormat;
            //obj["ServerTimeStampUTC"] = utcDate.ToString(utcFormat);




            string str = "JS" + obj.ToString(Formatting.None) + separator;

            ConsoleWrite.WriteMessage(str, ConsoleColor.Green);


            //Send(str);


            return SendAsync(str);
        }

        public bool SendAsyncMemoryPack(byte[] data)
        {
            var prefix = Encoding.UTF8.GetBytes("MP");
            var separatorBytes = new byte[] { (byte)separator };  // `char` → `byte`

            byte[] result = new byte[prefix.Length + data.Length + separatorBytes.Length];

            System.Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length);
            System.Buffer.BlockCopy(data, 0, result, prefix.Length, data.Length);
            System.Buffer.BlockCopy(separatorBytes, 0, result, prefix.Length + data.Length, separatorBytes.Length);

            return SendAsync(result);

        }


        public void SendErrorMessage(string errorType,string msg)
        {
            var json = new JObject();

            json[ServerMessageTypes.MessageType] = ServerMessageTypes.Error;
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

            if (BlackList.Instance.IsBlocked(ip))
            {
                ConsoleWrite.WriteMessage($"Connection rejected for banned IP {ip}", ConsoleColor.Red);
                Disconnect();
                return;
            }


            ConsoleWrite.WriteMessage("IP Address:" + ip, ConsoleColor.Green);
            ConsoleWrite.WriteMessage($"TCP session with Id {Id} connected!", ConsoleColor.DarkMagenta);

            //Console.WriteLine(endpoint.ToString());

            Socket.ReceiveTimeout = 6000;
            //Socket.SendTimeout = 1000;

            var jobject = new JObject();

            jobject[ServerMessageTypes.MessageType] = ServerMessageTypes.ConnectServerSuccessful;
            jobject["RSAPublicKey"] = EncryptManager.Instance.GetRSAPublicKey();



            SendAsyncJsonWithTimeStamp2(jobject);

        }

        protected override void OnDisconnected()
        {
            //Console.WriteLine($"TCP session with Id {Id} disconnected!");
            ConsoleWrite.WriteMessage($"TCP session with Id {Id} disconnected!", ConsoleColor.Red);


            AccountEventHandler.Logout(this);

            if (!string.IsNullOrWhiteSpace(PlayerID))
            {
                InGameMatchEventHandler.ClearPlayerState(PlayerID);
                LobbyServerManager.Instance.PlayerLeaveLobby(PlayerID);
                WaitRoomEventHandler.RemoveDisconnectedPlayer(PlayerID);
            }

            receiveBuffer.Clear();
        }
        private const int MaxReceiveBufferBytes = 1024 * 1024;
        private readonly List<byte> receiveBuffer = new();
        //private readonly byte delimiter = (byte)'\n'; // 制御文字

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // 受信データをバッファに追加
            receiveBuffer.AddRange(buffer.Skip((int)offset).Take((int)size));
            if (receiveBuffer.Count > MaxReceiveBufferBytes)
            {
                ConsoleWrite.WriteMessage("[WARN] Client receive buffer exceeded 1 MiB; dropping buffered data.", ConsoleColor.Yellow);
                receiveBuffer.Clear();
                return;
            }

            while (true)
            {
                // 制御文字（\n）の位置を探す
                int delimiterIndex = receiveBuffer.IndexOf((byte)separator);
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

                // 先頭の識別子を取得。MPが現行形式で、MSGも旧形式として受け付ける。
                int identifierLength = completeData.Length >= 3 &&
                    Encoding.UTF8.GetString(completeData, 0, 3) == "MSG" ? 3 : 2;
                string identifier = Encoding.UTF8.GetString(completeData, 0, identifierLength);
                byte[] payload = completeData.Skip(identifierLength).ToArray();

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
                else if (identifier == "MP" || identifier == "MSG") // MessagePack処理
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

        // #networkcore
        private void ParseMessageFromClient(in JObject json)
        {
            var rawMessageType = json["MessageType"]?.ToString();
            if (string.IsNullOrEmpty(rawMessageType)) return;

            if (string.Equals(rawMessageType, "RequestEnvelope", StringComparison.Ordinal))
            {
                HandleRequestEnvelope(json);
                return;
            }

            // Normalize legacy aliases once at the transport boundary so that
            // lobby and match handlers both dispatch on the canonical contract.
            string messageType = MessageType.Normalize(rawMessageType);

            // Delegate lobby traffic first, then match traffic.
            var lobbyManager = (Server as LobbyTcpServer)?.Manager;
            if (lobbyManager != null)
            {
                lobbyManager.HandleMessage(messageType, json, this);
            }
            else if (Server is MatchTcpServer)
            {
                switch (messageType)
                {
                    case MessageType.LoadingStarted:
                    case MessageType.ClientLoadingSceneEntered:
                    case MessageType.LoadingProgress:
                    case MessageType.LoadingCompleted:
                    case GameMessageTypes.LoadingFinished:
                    case GameMessageTypes.MatchStatusRequest:
                    case GameMessageTypes.PlayerRespawn:
                        if (string.IsNullOrWhiteSpace(PlayerID))
                        {
                            return;
                        }

                        // Match TCP events are authenticated by this session. Do
                        // not allow the request payload to impersonate another
                        // player before it reaches the game-event handler.
                        json["PlayerID"] = PlayerID;
                        json["PlayerId"] = PlayerID;
                        InGameMatchEventHandler.HandleTcpSystemEvent(json);
                        break;
                }
            }

            // ClientSession自身で処理するメッセージ
            switch (messageType)
            {
                case MessageType.PlayerInfoRequest:
                    HandlePlayerInfoRequest(json);
                    break;
                // Add more session-specific messages here if needed.
            }
        }

        private void HandleRequestEnvelope(JObject envelope)
        {
            var requestId = envelope["RequestId"]?.ToString();
            var route = envelope["Route"]?.ToString();
            var response = new JObject
            {
                ["MessageType"] = "ResponseEnvelope",
                ["RequestId"] = requestId ?? string.Empty,
                ["Route"] = route ?? string.Empty,
                ["Success"] = false
            };

            if (string.Equals(route, "foundation/ping", StringComparison.Ordinal))
            {
                var payload = envelope["Payload"] as JObject;
                response["Success"] = true;
                response["Payload"] = new JObject
                {
                    ["Nonce"] = payload?["Nonce"]?.ToString() ?? Guid.NewGuid().ToString("N"),
                    ["EchoClientSentAtUtc"] = payload?["ClientSentAtUtc"]?.ToString() ?? string.Empty,
                    ["ServerSentAtUtc"] = DateTime.UtcNow.ToString("O")
                };
            }
            else
            {
                response["ErrorCode"] = "UnknownRoute";
                response["ErrorMessage"] = $"Unknown request route: {route ?? string.Empty}";
            }

            SendAsyncJsonWithTimeStamp(response);
        }

        private void HandlePlayerInfoRequest(JObject requestJson)
        {
            string targetPlayerId = requestJson["TargetPlayerID"]?.ToString();
            if (string.IsNullOrEmpty(targetPlayerId))
            {
                SendErrorMessage("InvalidRequest", "TargetPlayerID is missing.");
                return;
            }

            var accountDbManager = AccountDatabaseManager.GetInstance();
            var account = accountDbManager.GetAccount(targetPlayerId); // Replace with the correct lookup if needed.

            if (account != null)
            {
                // プレイヤー情報が見つかった場合
                var responseJson = new JObject
                {
                    [ServerMessageTypes.MessageType] = MessageType.PlayerInfoResponse,
                    ["Success"] = true,
                    ["PlayerID"] = account.Id,
                    ["DisplayName"] = account.DisplayName,
                    ["Level"] = account.Level, // 仮のデータ
                    ["XP"] = account.Exp // 仮のデータ
                    // 他にも必要な情報を追加
                };
                SendAsyncJsonWithTimeStamp(responseJson);
            }
            else
            {
                // プレイヤー情報が見つからなかった場合
                var responseJson = new JObject
                {
                    [ServerMessageTypes.MessageType] = MessageType.PlayerInfoResponse,
                    ["Success"] = false,
                    ["Error"] = "PlayerNotFound",
                    ["TargetPlayerID"] = targetPlayerId
                };
                SendAsyncJsonWithTimeStamp(responseJson);
            }
        }

        protected override void OnSent(long sent, long pending)
        {
            ConsoleWrite.WriteMessage("OnSent");
        }
    }

}
