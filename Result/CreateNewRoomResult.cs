using System;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public enum ECreateNewRoomResult
    {
        Successful,
        Fail,
    }

    public enum ECreateNewRoomReason
    {
        NoReason,
        InvalidPlayerId,
        AlreadyOtherRoom,
        OverflowServerMaxCapacity
    }

    public class CreateNewRoomResult : AbstractResult
    {
        public ECreateNewRoomResult Result { get; }
        public ECreateNewRoomReason Reason { get; }
        public string RoomId { get; set; } = string.Empty;

        public CreateNewRoomResult(ECreateNewRoomResult result, ECreateNewRoomReason reason)
        {
            Result = result;
            Reason = reason;
        }

        public CreateNewRoomResult()
        {
            Result = ECreateNewRoomResult.Fail;
            Reason = ECreateNewRoomReason.NoReason;
        }

        public string Message()
        {
            switch (Result)
            {
                case ECreateNewRoomResult.Successful:
                    return "Room created successfully";
                case ECreateNewRoomResult.Fail:
                    switch (Reason)
                    {
                        case ECreateNewRoomReason.InvalidPlayerId:
                            return "Invalid player ID";
                        case ECreateNewRoomReason.AlreadyOtherRoom:
                            return "Player is already in another room";
                        case ECreateNewRoomReason.OverflowServerMaxCapacity:
                            return "Server capacity reached";
                        default:
                            return "Unknown error";
                    }
                default:
                    return "Unknown error";
            }
        }

        public JObject ToJson()
        {
            var result = new JObject
            {
                ["Result"] = Result.ToString(),
                ["Reason"] = Reason.ToString(),
                ["RoomId"] = RoomId,
                ["Message"] = Message()
            };
            return result;
        }
    }
}
