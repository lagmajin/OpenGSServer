
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public class MatchRoomDelegate
    {
        public static void ShotFromPlayer(MatchRUdpSession session,in IDictionary<string, JToken> dic)
        {
            string playerID;

            var bulletType = dic.GetValueString("BulletType");
            var roomId = dic.GetValueString("GameRoomID");
            var frame = dic.GetValueString("Frame");

            
 



        }

        public static void RecieveGrenadeFromPlayer(MatchRUdpSession session,in IDictionary<string, JToken> dic)
        {







        }

        public static void UseInstanceItem(MatchRUdpSession session, in IDictionary<string, JToken> dic)
        {
            string instanceItemType=dic.GetValueString("InstantItemType"); ;

            string roomID= dic.GetValueString("RoomID");

            string playerID=dic.GetValueString("PlayerID"); 



        }

    }
}
