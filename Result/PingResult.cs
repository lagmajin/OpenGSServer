using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;




namespace OpenGSServer
{
    class PingResult : AbstractResult
    {
        private readonly int ping = 0;
        public PingResult(int ping)
        {
            this.ping = ping;
        }

        public JObject ToJson()
        {
            var result = new JObject();

            result["Ping"] = ping.ToString();


            return result;
        }


    }
}
