
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

            result["MessageType"] = "PingResult";
            result["Ping"] = ping.ToString();


            return result;
        }


    }
}
