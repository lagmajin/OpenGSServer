using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OpenGSServer.Result
{
    internal class ChatResult : AbstractResult
    {

        public JObject ToJson()
        {
            var result = new JObject();
            result["MessageType"] = "";

            return result;
        }


    }

    internal class ChatLogResult : AbstractResult
    {

        public JObject ToJson()
        {
            var result = new JObject();


            return result;
        }


    }

}
