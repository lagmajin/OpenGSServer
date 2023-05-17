using System;
using System.Collections;
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
            if(dic.TryGetValue("Room",out var token))
            {
            }

            var value = dic.TryGetValue("room", out var result) ? result : "test";

            var test = dic.GetValueOrDefaultString("room");

        }

        public static void SendUpdateRoom(in ClientSession session,IDictionary<string, JToken> dic)
        {
            //var roomManager


        }


        public static void CloseRoomRequest(in ClientSession session,IDictionary<string, JToken> dic)
        {

        }

        public static void ExitRoomRequest(in ClientSession session)
        {

        }

        public static void GameStartRequest(in ClientSession session,IDictionary<string,JToken> dic)
        {

        }

    }
}
