using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    /*
    public class EnterRoomResult : AbstractResult
    {
        EnterRoomResult()
        {

        }
        private string MessageType()
        {
            return "";
        }

        private string Message()
        {
            return "";
        }

        public JObject ToJson()
        {
            var result = new JObject();
            result["MessageType"] = MessageType();
            result["Message"] = Message();
            return result;
        }




    }
    */

    public enum eEnterMatchResultType
    {
        Unknown,
        EnterMatchRoomSucceeful,
        EnterMatchRoomFailed
    }

    public enum eEnterMatchRoomResultReason
    {
        Unknown,
        Succeeful,
        RoomCapacityOver,
        MatchHasBeenStarted,

    }


    public class MatchEnterResult
    {
        readonly bool enterSuccess = false;

        readonly String str = "";

        eEnterMatchResultType resultType = eEnterMatchResultType.Unknown;

        eEnterMatchRoomResultReason reason = eEnterMatchRoomResultReason.Unknown;


        public MatchEnterResult(bool success, String message)
        {
            enterSuccess = success;

            str = message;
        }

        private string MessageType()
        {
            switch (resultType)
            {
                case eEnterMatchResultType.Unknown:
                    return "UnknownError";
                case eEnterMatchResultType.EnterMatchRoomSucceeful:
                    return "EnterMatchRoomSucceeful";
                case eEnterMatchResultType.EnterMatchRoomFailed:
                    return "EnterMatchRoomFailed";
                default:
                    break;
            }
            return "";
        }

        private string Message()
        {
            switch (reason)
            {
                case eEnterMatchRoomResultReason.MatchHasBeenStarted:

                    return "UnknownError";
            }
            return "";
        }
        public JObject ToJson()
        {
            var result = new JObject();

            if (enterSuccess)
            {
                result["MessageType"] = "MatchRoomEnterSuccess";
            }
            else
            {
                result["MessageType"] = "MatchRoomEnterFailed";
            }


            return result;
        }

    }
}
