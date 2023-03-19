using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public class ServerSettings
    {
        private int MaxRoom { get; set; } = 32;
        private int MaxUser { get; set; } = 200;


        private int TickRate { get; set; } = 60;


        private bool CanRegisterAccounts { get; set; } = true;



        public List<EGameMode> AllowGameMode { get; set; }=new();


        public JObject ToJson()
        {
            var result = new JObject();
            result["MaxRoom"] = 100;
            result["MaxUser"] = MaxUser;
            result["TickRate"] = TickRate;
            result["AllowRegisterAccount"] = true;

            return result;
        }

        public void SetFromJson(JObject json)
        {
            if (json.TryGetValue("MaxRoom", out var maxRoomToken))
            {
                var maxRoom=maxRoomToken.ToString();

                if (Int32.TryParse("MaxRoom", out var rooms))
                {
                    //maxRooms = rooms;
                }

            }

            if (json.TryGetValue("MaxUser", out var maxUserToken))
            {
                var maxUser = maxUserToken.ToString();

                if(Int128.TryParse(maxUser,out var t))
                {

                }

            }


            if(json.TryGetValue("TickRate",out var tickrateToken))
            {
                var tickrate = tickrateToken.ToString();

                if(Int128.TryParse(tickrate,out var t))
                {

                }

            }


        }

        public override bool Equals(object obj)
        {
            return obj is ServerSettings settings;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MaxRoom, MaxUser, CanRegisterAccounts);
        }

        public static bool operator ==(ServerSettings a, ServerSettings b)
        {
            if(a.MaxRoom!=b.MaxRoom)
            {

                return false;
            }

            if(a.MaxUser!=b.MaxUser)
            {

                return false;
            }

            if(a.CanRegisterAccounts!=b.CanRegisterAccounts)
            {
                return false;

            }


            return true;
        }

        public static bool operator !=(ServerSettings a, ServerSettings b)
        {

            return !(a == b);
        }

    }



}
