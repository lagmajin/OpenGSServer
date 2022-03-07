using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class ServerSettings
    {
        int maxRooms = 32;
        int maxUser = 200;

        private int tickRate = 60;
        
        bool canRegisterAccounts = true;

        public override bool Equals(object obj)
        {
            return obj is ServerSettings settings;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(maxRooms, maxUser, canRegisterAccounts);
        }

        public static bool operator ==(ServerSettings a, ServerSettings b)
        {
            if(a.maxRooms!=b.maxRooms)
            {

                return false;
            }

            if(a.maxUser!=b.maxUser)
            {

                return false;
            }

            if(a.canRegisterAccounts!=b.canRegisterAccounts)
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
