using Newtonsoft.Json.Linq;
using System.Collections.Generic;

#nullable enable

namespace OpenGSServer
{
    public class MatchRoomEventHandler
    {
        public void MatchRoomUpdateRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomID") ?? dic.GetStringOrNull("RoomId");
            if (string.IsNullOrWhiteSpace(roomId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject { ["MessageType"] = "MatchRoomUpdateResponse", ["Success"] = false, ["ErrorMessage"] = "RoomID required" });
                return;
            }

            var matchRoomManager = MatchRoomManager.Instance;
            var room = matchRoomManager.FindRoom(roomId);
            if (room == null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject { ["MessageType"] = "MatchRoomUpdateResponse", ["Success"] = false, ["ErrorMessage"] = "Room not found" });
                return;
            }
            if (room is not MatchRoom matchRoom)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject { ["MessageType"] = "MatchRoomUpdateResponse", ["Success"] = false, ["ErrorMessage"] = "Room is not a match room" });
                return;
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = "MatchRoomUpdateResponse",
                ["Success"] = true,
                ["RoomID"] = roomId,
                ["PlayerCount"] = matchRoom.PlayerCount,
                ["NowPlaying"] = matchRoom.Playing
            });
        }

        public void ChangeRoomOwnerRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomID") ?? dic.GetStringOrNull("RoomId");
            var currentOwnerId = dic.GetStringOrNull("CurrentOwnerId") ?? dic.GetValueString("currentOwnerId");
            var newOwnerId = dic.GetStringOrNull("NewOwnerId") ?? dic.GetValueString("newOwnerId");

            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(newOwnerId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject { ["MessageType"] = "OwnerChangeResponse", ["Success"] = false, ["ErrorMessage"] = "RoomID and NewOwnerId required" });
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject { ["MessageType"] = "OwnerChangeResponse", ["Success"] = false, ["ErrorMessage"] = "Room not found" });
                return;
            }

            if (!waitRoom.Players.ContainsKey(newOwnerId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject { ["MessageType"] = "OwnerChangeResponse", ["Success"] = false, ["ErrorMessage"] = "New owner is not in room" });
                return;
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = "OwnerChangeResponse",
                ["Success"] = true,
                ["RoomID"] = roomId,
                ["NewOwnerId"] = newOwnerId
            });
        }

        public void QuitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomID") ?? dic.GetStringOrNull("RoomId");
            var playerId = dic.GetStringOrNull("PlayerID") ?? dic.GetStringOrNull("PlayerId");

            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject { ["MessageType"] = MessageType.LeaveRoomResponse, ["Success"] = false, ["ErrorMessage"] = "RoomID and PlayerID required" });
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject { ["MessageType"] = MessageType.LeaveRoomResponse, ["Success"] = false, ["ErrorMessage"] = "Room not found" });
                return;
            }

            waitRoom.RemovePlayer(playerId);
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.LeaveRoomResponse,
                ["Success"] = true,
                ["RoomID"] = roomId,
                ["PlayerID"] = playerId
            });
        }
    }
}
