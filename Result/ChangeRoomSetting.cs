using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace OpenGSServer
{
    internal class ChangeRoomSettingResult
    {
        public JsonObject ToJson()
        {
            var result = new JsonObject();

            result["MessageType"] = "RoomSettingChanged";



            return result;
        }

    }
}
