using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenGSCore;


namespace OpenGSServer
{


    public enum eCreateAccountResult
    {
        Succeeful,
        ExistAlreadySameAccount,
        InvalidPassword,
        Unknown,
    }

    public class CreateNewAccountResult : AbstractResult
    {
        eCreateAccountResult messageType = eCreateAccountResult.Unknown;

        public CreateNewAccountResult(eCreateAccountResult messageType = eCreateAccountResult.Unknown)
        {
            this.messageType = messageType;
        }

        private string MessageType()
        {

            switch ((messageType)
)
            {
                case eCreateAccountResult.Succeeful:
                    return "CreateNewAccountSucceeful";
                case eCreateAccountResult.ExistAlreadySameAccount:
                    return "CreateNewAccountAlreadyExists";
                case eCreateAccountResult.InvalidPassword:
                    return "CreateNewAccountInvalidPassword";
                case eCreateAccountResult.Unknown:
                    return "Unknown";
                default:
                    return "Unknown";
            }

        }

        private string Message()
        {
            switch ((messageType)
)
            {
                case eCreateAccountResult.Succeeful:
                    return "CreateNewAccountSuccessful";
                case eCreateAccountResult.ExistAlreadySameAccount:
                    return "ExistAlreadySameAccount";
                case eCreateAccountResult.InvalidPassword:
                    return "InvalidPassword";
                case eCreateAccountResult.Unknown:
                    return "Unknown";
                default:
                    return "Unknown";
            }

        }

        public JObject ToJson()
        {
            var succeeded = messageType == eCreateAccountResult.Succeeful;
            var result = new JObject
            {
                ["MessageType"] = OpenGSCore.MessageType.CreateAccountResponse,
                ["Success"] = succeeded,
                ["Message"] = Message(),
                ["LegacyMessageType"] = MessageType()
            };

            if (!succeeded)
            {
                result["Error"] = Message();
            }


            return result;
        }

    }





}
