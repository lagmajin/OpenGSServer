using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{


    public class MatchSettings2
    {
        private eGameMode gameMode = eGameMode.Unknown;

        public bool TimeLimit { get; set; } = false;
        public int MatchTime { get; set; } = 30000;
        public bool RandomTeam { get; set; } = true;

       





        public JObject ToJson()
        {
            var result = new JObject
            {
                ["GameMode"] = gameMode.ToString(),
                ["TimeLimit"] = TimeLimit.ToString(),
                ["MatchTime"] = MatchTime.ToString(),
                ["RandomTeam"] = RandomTeam.ToString()
            };


            return result;
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
