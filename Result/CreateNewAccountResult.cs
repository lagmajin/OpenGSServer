using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{


    public enum eCreateAccountResult
    {
        Succeeful,
        AlreadySameName,
        Unknown,
    }

    public class CreateNewAccountResult:AbstractResult
    {
        readonly bool successed=false;

        eCreateAccountResult messageType = eCreateAccountResult.Unknown;

        public CreateNewAccountResult(eCreateAccountResult messageType = eCreateAccountResult.Unknown)
        {

        }

        private string MessageType()
        {

            switch ((messageType)
)
            {
                case eCreateAccountResult.Succeeful:
                    return "CreateNewAccountSucceeful";
                case eCreateAccountResult.AlreadySameName:
                    return "";
                case eCreateAccountResult.Unknown:
                    return "Unknown";
                default:
                    return "Unknown";
            }

            return "";
        }

        private string Message()
        {
            switch ((messageType)
)
            {
                case eCreateAccountResult.Succeeful:
                    return "CreateNewAccountSucceeful";
                case eCreateAccountResult.AlreadySameName:
                    return "";
                case eCreateAccountResult.Unknown:
                    return "Unknown";
                default:
                    return "Unknown";
            }

            return "";
        }

        public JObject ToJson()
        {
            var result = new JObject();

            result["MessageType"] = MessageType();
            result["Message"] = Message();

            
            return result;
        }

    }

   

  
   
}
