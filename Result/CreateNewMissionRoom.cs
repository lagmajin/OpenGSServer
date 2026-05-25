using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class CreateNewMissionRoom
    {
        public string Message { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public bool Success { get; set; } = false;

        public JObject ToJson()
        {
            return new JObject
            {
                ["Message"] = Message,
                ["RoomId"] = RoomId,
                ["Success"] = Success
            };
        }
    }
}
