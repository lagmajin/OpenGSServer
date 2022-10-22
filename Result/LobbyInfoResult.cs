
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class LobbyInfoResult : AbstractResult
    {
        private int userCount = 0;

        public LobbyInfoResult()
        {

        }


        public JObject ToJson()
        {
            var result = new JObject();

            result["MessageType"] = "LobbyInfo";
            result["UserOnLobby"] = userCount.ToString();
            result[""] = "";


            return result;
        }


    }
}
