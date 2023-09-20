using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{
    internal class WaitRoomEventDelegate
    {

        public static void ChangeRoomSetting(in ClientSession session,IDictionary<string, JToken> dic)
        {

        }

        public static void SendUpdateRoom(in ClientSession session,IDictionary<string, JToken> dic)
        {

        }


        public static void CloseRoomRequest(in ClientSession session,IDictionary<string, JToken> dic)
        {

        }

        public static void ExitRoomRequest(in ClientSession session)
        {

        }

    }
}
