
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

                var id= json.GetStringOrNull("PlayerID");

                if(id!=null)
                {
                    if (type == "Shot")
                    {
                        //ShotFromPlayer()
                    }

                    if (type == "Grenade")
                    {

                    }

                    if (type == "InstantItem")
                    {
                        var itemNum = json.GetValueDefaultInt("InstantItemNum",0);

                    }
                }
 

            }
            else
            {

            }

        }

        public static void InstantItem(PlayerID id,int num)
        {


        }


    }
}
