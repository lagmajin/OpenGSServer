using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class MakeJson
    {
        //static 

        public static JObject MatchServerInfo()
        {
            var result=new JObject();

            result["MessageType"] = "MatchServerInfo";


            return result;
        }

        public static JObject MakeChatUpdateCommand()
        { 
            var result = new JObject();

            result["MessageType"] = "";

            return result;

        }




        public static JObject Make()
        {
            var result = new JObject();

            return result;

        }

    }
}
