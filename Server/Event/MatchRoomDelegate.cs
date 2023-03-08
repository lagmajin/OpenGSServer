
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public class MatchRoomDelegate
    {
        public static void RecieveShotFromPlayer(MatchRUdpSession session,in IDictionary<string, JToken> dic)
        {
            string bulletType;

            string roomID;

            string playerID;

            if(dic.TryGetValue("BulletType",out var token))
            {

                return;

                 
            }
            else
            {

            }

            if(dic.TryGetValue("GameRoomID",out var roomIdToken))
            {

            }
            else
            {

            }

            if(dic.TryGetValue("Frame",out var frameToken))
            {
                return;
            }
            else
            {

            }


        }

        public static void RecieveGrenadeFromPlayer(MatchRUdpSession session,in IDictionary<string, JToken> dic)
        {
            //eGrenadeType type;

            string grenadeType;

            string roomID;

            string playerID;



            if(dic.TryGetValue("GrenadeType",out var bulletToken))
            {


                
            }
            else
            {

            }

            if(dic.TryGetValue("GameRoomID",out var roomIdToken))
            {

            }
            else
            {

            }

            if(dic.TryGetValue("",out var r))
            {

            }
            else
            {

            }

        






        }

        public static void UseInstanceItem()
        {
            string instanceItemType;

            string roomID;

            string playerID;
        }

    }
}
