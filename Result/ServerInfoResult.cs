
using System;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class ServerInfoResult:AbstractResult
    {
        public ServerInfoResult()
        {

        }

        public JObject ToJson()
        {
            var result = new JObject();

            var timezoneInfo =TimeZoneInfo.Local;


            

            return result;
        }

    }
}
