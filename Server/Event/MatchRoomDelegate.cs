
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using OpenGSCore;


namespace OpenGSServer
{


    public static class MatchRoomDelegate
    {
        public static void ParseEvent(JObject json)
        {
            var type = json.GetStringOrNull("MessageType");

            if(type!=null)
            {
                if(type=="Shot")
                {
                    //ShotFromPlayer()
                }

                if(type== "Grenade")
                {

                }

                if(type=="InstantItem")
                {

                }

            }

        }

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
