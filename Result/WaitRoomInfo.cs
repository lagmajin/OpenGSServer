using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public class WaitRoomInfo : AbstractResult
    {
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomOwnerId { get; set; } = string.Empty;
        public string GameMode { get; set; } = string.Empty;
        public int PlayerCount { get; set; } = 0;
        public int Capacity { get; set; } = 0;
        public bool TeamBalance { get; set; } = true;

        public JObject ToJson()
        {
            return new JObject
            {
                ["MessageType"] = MessageType.WaitRoomUpdateNotification,
                ["RoomId"] = RoomId,
                ["RoomName"] = RoomName,
                ["RoomOwnerId"] = RoomOwnerId,
                ["GameMode"] = GameMode,
                ["PlayerCount"] = PlayerCount,
                ["Capacity"] = Capacity,
                ["TeamBalance"] = TeamBalance
            };
        }
    }
}
