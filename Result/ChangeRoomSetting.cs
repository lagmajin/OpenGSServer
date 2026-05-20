using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using OpenGSCore;

namespace OpenGSServer
{
    internal class ChangeRoomSettingResult
    {
        public ChangeRoomSettingResult()
        {

        }


        public JsonObject ToJson()
        {
            var result = new JsonObject();

            result["MessageType"] = MessageType.RoomSettingChanged;
            result["GameMode"] = "";
            result[""] = "";


            return result;
        }

    }
}
