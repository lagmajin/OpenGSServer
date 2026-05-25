using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer.Player
{
    public class WaitRoomPlayerInfo
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public EPlayerCharacter PlayerCharacter { get; set; } = EPlayerCharacter.Misty;
        public bool IsReady { get; set; } = false;

        public JObject ToJson()
        {
            return new JObject
            {
                ["PlayerId"] = PlayerId,
                ["PlayerName"] = PlayerName,
                ["PlayerCharacter"] = PlayerCharacter.ToString(),
                ["IsReady"] = IsReady
            };
        }
    }
}
