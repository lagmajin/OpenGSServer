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

        static public JObject MatchServerInfo()
        {
            JObject result=new JObject();

            result["MessageType"] = "";

            return result;
        }

        static public JObject MakeChatUpdateCommand()
        {
            JObject result = new JObject();

            result["MessageType"] = "";

            return result;

        }


    }
}
