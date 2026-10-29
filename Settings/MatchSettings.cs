using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{


    public class MatchSettings
    {
        private EGameMode gameMode = EGameMode.Unknown;

        public bool TimeLimit { get; set; } = false;
        public int MatchTime { get; set; } = 30000;
        public bool RandomTeam { get; set; } = true;

       





        public JObject ToJson()
        {
            var result = new JObject();
            result["GameMode"] = gameMode.ToString();
            result["TimeLimit"] = TimeLimit.ToString();
            result["MatchTime"] = MatchTime.ToString();
            result["RandomTeam"] = RandomTeam.ToString();



            return result;
        }


        public static bool operator ==(MatchSettings a, MatchSettings b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
            {
                return false;
            }

            if (a.RandomTeam != b.RandomTeam)
            {
                return false;
            }

            if (a.TimeLimit != b.TimeLimit)
            {
                return false;
            }

            return a.MatchTime == b.MatchTime;
        }

        public static bool operator !=(MatchSettings a, MatchSettings b)
        {

            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return this == obj as MatchSettings;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TimeLimit, MatchTime, RandomTeam);

        }
    }
}
