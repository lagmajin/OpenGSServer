using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

using System.Text.Json;

namespace OpenGSServer
{
    public enum eCreateNewRoomResult
    {
        Succeessful,
        Fail,
    }

    public enum eCreateNewRoomReason
    {
        NoReason,
        InvalidPlayerID,
        AlreadyOtherRoom,
        OverflowServerMaxCapacity

    }

    public class CreateNewRoomResult:AbstractResult
    {
        public eCreateNewRoomResult Result { get; } = eCreateNewRoomResult.Fail;

        public eCreateNewRoomReason Reason { get; } = eCreateNewRoomReason.NoReason;

        public CreateNewRoomResult(eCreateNewRoomResult result,eCreateNewRoomReason reason)
        {

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
