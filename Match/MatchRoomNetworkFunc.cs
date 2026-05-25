using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public static class MatchRoomNetworkFunc
    {
        public static void SendMatchResult(this OpenGSCore.MatchRoom room)
        {
            if (room == null)
            {
                return;
            }

            var result = new JObject
            {
                ["MessageType"] = MessageType.MatchEndNotification,
                ["RoomId"] = room.Id,
                ["RoomName"] = room.RoomName,
                ["PlayerCount"] = room.PlayerCount
            };

            room.Broadcast(result);
        }
    }
}
