using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public class LobbyInfoResult : AbstractResult
    {
        public int UserCount { get; set; } = 0;

        public LobbyInfoResult()
        {
        }

        public LobbyInfoResult(int userCount)
        {
            UserCount = userCount;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["MessageType"] = MessageType.LobbyInfo,
                ["UserOnLobby"] = UserCount
            };
        }
    }
}
