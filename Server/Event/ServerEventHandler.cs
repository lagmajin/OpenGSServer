using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class ServerEventHandler
    {
        public static void HandleServerInfoRequestFromClient(ClientSession session, IDictionary<string, JToken> dic)
        {
            var timezone = TimeZoneInfo.Local;

            var lobbyServerInfoJson = new JObject();
            lobbyServerInfoJson["Port"] = "";


            var matchServerInfoJson = new JObject();
            matchServerInfoJson["Port"] = "";
            matchServerInfoJson["Mode"] = "RUDP";

            var json = new JObject();

            json["MessageType"] = "ServerInfo";
            json["LobbyServerInfo"] = lobbyServerInfoJson;
            json["MatchServerInfo"] = matchServerInfoJson;


            session.SendAsyncJsonWithTimeStamp(json);
        }

        public static void HandlePingRequestFromClient(ClientSession session, Dictionary<string, JToken> dic)
        {
            var pingResult = new PingResult(0);

            if (dic.TryGetValue("ClientTimeStampUTC", out var timeStampToken))
            {





                //time.ToString();



            }

            session.SendPing();
        }
    }
}
