using System;
using System.Collections.Generic;
using System.Text;

using OpenGSCore;

namespace OpenGSServer
{


    public class MatchSettings
    {

        
        private bool randomTeam = true;
        private bool joinableInMatch = true;
        private eGameMode gameMode = eGameMode.Unknown;

        private bool timeLimit = false;
        private int matchTime = 30000;


        public int MatchTime { get => matchTime; set => matchTime = value; }
        public bool RandomTeam { get => randomTeam; set => randomTeam = value; }
        public bool JoinableInMatch { get => joinableInMatch; set => joinableInMatch = value; }
        public bool TimeLimit { get => timeLimit; set => timeLimit = value; }

        public static bool operator ==(MatchSettings a, MatchSettings b)
        {
            if(a.randomTeam!=b.randomTeam)
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
