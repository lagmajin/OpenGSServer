using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class ServerEventDelegate
    {
        public static void ServerInfoRequest(ClientSession session, IDictionary<string, JToken> dic)
        {
            var timezone = TimeZoneInfo.Local;

            var json = new JObject();




        }

        public static void PingRequest(ClientSession session, Dictionary<string, JToken> dic)
        {
            var pingResult = new PingResult(0);

            if (dic.TryGetValue("ClientTimeUTC", out var time))
            {


            }

            session.SendPing();
        }
    }
}
