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

        private bool ValidateGrenadeMessage(JObject json, out string reason)
        {
            reason = string.Empty;

            if (json == null)
            {
                reason = "json is null";
                return false;
            }

            if (!json.TryGetValue("MessageType", out var mt))
            {
                reason = "MessageType missing";
                return false;
            }

            // require player id
            if (!json.TryGetValue("PlayerID", out var pid) && !json.TryGetValue("PlayerId", out pid))
            {
                reason = "PlayerID missing";
                return false;
            }

            // require object id
            if (!json.TryGetValue("ObjectId", out var oid) && !json.TryGetValue("ID", out oid))
            {
                reason = "ObjectId missing";
                return false;
            }

            // require room id for routing via InGameMatchEventHandler
            if (!json.TryGetValue("RoomID", out var rid) && !json.TryGetValue("RoomId", out rid))
            {
                reason = "RoomID missing";
                return false;
            }

            return true;
        }

        // Per-session rate limiters
        private static readonly double GrenadeSpawnCapacity = 5; // tokens
        private static readonly double GrenadeSpawnRefillPerSecond = 5; // tokens/sec

        private TokenBucket _grenadeSpawnBucket = new TokenBucket(GrenadeSpawnCapacity, GrenadeSpawnRefillPerSecond);

        private bool CheckGrenadeRateLimit()
        {
            // consume 1 token per spawn/update/explode message
            if (!_grenadeSpawnBucket.TryConsume(1))
            {
                ConsoleWrite.WriteMessage("[RATE] Grenade message rate exceeded", ConsoleColor.Yellow);
                return false;
            }

            return true;
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
                    // Basic validation before forwarding
                    if (!ValidateGrenadeMessage(json, out var reason))
                    {
                        ConsoleWrite.WriteMessage($"[WARN] Invalid grenade message: {reason}", ConsoleWrite.eMessageType.Warning);
                        var err = new JObject
                        {
                            ["MessageType"] = "Error",
                            ["Detail"] = reason
                        };
                        // send error back to sender
                        try { SendAsync(err.ToString()); } catch { }
                        return;
                    }

                    // Rate limit check
                    if (!CheckGrenadeRateLimit())
                    {
                        var err = new JObject
                        {
                            ["MessageType"] = "Error",
                            ["Detail"] = "Rate limit exceeded"
                        };
                        try { SendAsync(err.ToString()); } catch { }
                        return;
                    }

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
