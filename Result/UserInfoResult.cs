using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public class UserInfoResult : AbstractResult
    {
        public PlayerAccount? Account { get; set; }
        public PlayerServerInformation? ServerInformation { get; set; }

        public UserInfoResult()
        {
        }

        public UserInfoResult(PlayerAccount? account, PlayerServerInformation? serverInformation)
        {
            Account = account;
            ServerInformation = serverInformation;
        }

        public JObject ToJson()
        {
            var result = new JObject
            {
                ["MessageType"] = MessageType.PlayerInfoResponse
            };

            if (Account != null)
            {
                result["Account"] = Account.ToJson();
            }

            if (ServerInformation != null)
            {
                result["ServerInformation"] = ServerInformation.ToJson();
            }

            return result;
        }
    }
}
