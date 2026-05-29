using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    internal class ChangeRoomSettingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public WaitRoomSetting Settings { get; set; } = new();

        public JObject ToJson()
        {
            Settings.Normalize();

            return new JObject
            {
                ["MessageType"] = MessageType.RoomSettingChanged,
                ["Success"] = Success,
                ["Message"] = Message,
                ["RoomId"] = RoomId,
                ["RoomID"] = RoomId,
                ["Settings"] = Settings.ToJson(),
                ["RoomName"] = Settings.RoomName,
                ["Capacity"] = Settings.Capacity,
                ["GameMode"] = Settings.GameMode.ToString(),
                ["TeamBalance"] = Settings.TeamBalance,
                ["Map"] = Settings.Map
            };
        }
    }
}
