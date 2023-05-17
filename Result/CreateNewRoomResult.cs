using System;

using Newtonsoft.Json.Linq;


namespace OpenGSServer
{
    public enum eCreateNewRoomResult
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
        public eCreateNewRoomResult Result { get; } = eCreateNewRoomResult.Fail;

        public ECreateNewRoomReason Reason { get; } = ECreateNewRoomReason.NoReason;

        public CreateNewRoomResult(eCreateNewRoomResult result,ECreateNewRoomReason reason)
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
