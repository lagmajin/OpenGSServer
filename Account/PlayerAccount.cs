using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{





    public class PlayerAccount
    {
        //private string pass;
        private string dbUnitueID;

        private string globalID = Guid.NewGuid().ToString("N");


        public int Wins { get; set; }
        public int Kill { get; set; }
        public int Death { get; set; }
        public int FlagReturn { get; set; }
        public int Lv { get; set; } = 1;
        public string Name { get; set; }
        public string Gid { get => globalID; set => globalID = value; }
        public int Exp { get; set; } = 0;
        public int Character { get; set; } = 0;
        public string Id { get; set; }
        public int Matches { get; set; }
        public DateTime Time { get; set; }

        public PlayerAccount(in string id, in string name, in string pass)
        {
            Id = id;
            Name = name;



        }

        public JObject ToJson()
        {
            var json = new JObject();

            json["id"] = Id;
            json["name"] = Name;

            json["character"] = Character;
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
