using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{





    public class PlayerAccount
    {
        private string pass;
        private string dbUnitueID;

        private string globalID = Guid.NewGuid().ToString("N");


        private int exp = 0;
        private int character = 0;


        public int Wins { get; set; }
        public int Kill { get; set; }
        public int Death { get; set; }
        public int FlagReturn { get; set; }
        public int Lv { get; set; } = 1;
        public string Name { get; set; }

        public string Pass { get => pass; set => pass = value; }
        public string Gid { get => globalID; set => globalID = value; }
        public int Exp { get => exp; set => exp = value; }
        public int Character { get => character; set => character = value; }
        public string Id { get; set; }
        public int Matches { get; set; }
        public DateTime Time { get; set; }

        public PlayerAccount(in string id, in string name, in string pass)
        {
            Id = id;
            Name = name;
            Pass = pass;


        }

        public JObject ToJson()
        {
            var json = new JObject();

            json["id"] = Id;
            json["name"] = Name;
            json["pass"] = Pass;
            json["character"] = character;
            json["guid"] = globalID;
            json["lv"] = Lv;
            json["exp"] = Exp;
            json["matches"] = Matches;
            json["wins"] = Wins;
            json["kill"] = Kill;
            json["death"] = Death;



            return json;
        }




    }




}
