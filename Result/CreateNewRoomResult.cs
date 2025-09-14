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

    public class CreateNewRoomResult:AbstractResult
    {
        public ECreateNewRoomResult Result { get; } = ECreateNewRoomResult.Fail;

        public ECreateNewRoomReason Reason { get; } = ECreateNewRoomReason.NoReason;

        public CreateNewRoomResult(ECreateNewRoomResult result,ECreateNewRoomReason reason)
        {

        }

        public CreateNewRoomResult()
        {
            //throw new NotImplementedException();
        }

        public  string Message()
        {
            string result="";




            return result;
        }

        public JObject ToJson()
        {
            var result = new JObject();



            return result;
        }
        




    }


}
