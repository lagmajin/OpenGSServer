using System;
using System.Collections.Generic;
using System.Text;

using OpenGSCore;

namespace OpenGSServer
{


    public class MatchSettings
    {
        private eGameMode gameMode = eGameMode.Unknown;


        public int MatchTime { get; set; } = 30000;
        public bool RandomTeam { get; set; } = true;
        //public bool JoinableInMatch { get; set; } = true;
        public bool TimeLimit { get; set; } = false;

        public static bool operator ==(MatchSettings a, MatchSettings b)
        {
            if (a.RandomTeam != b.RandomTeam)
            {
                return false;
            }


            return true;
        }

        public static bool operator !=(MatchSettings a, MatchSettings b)
        {

            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }


            return false;

        }
    }
}
