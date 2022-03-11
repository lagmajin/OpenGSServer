using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{





    public class PlayerAccount
    {
        private string userID;
        private string name;
        private string pass;
        private string globalID = Guid.NewGuid().ToString("N");

        private int matchs = 0;

        private int exp = 0;
        private int character = 0;
        DateTime time;


        public int Wins { get; set; }
        public int Kill { get; set; }
        public int Death { get; set; }
        public int FlagReturn { get; set; }
        public int Lv { get; set; } = 1;
        public string Name { get => name; set => name = value; }
        public string Pass { get => pass; set => pass = value; }
        public string Gid { get => globalID; set => globalID = value; }
        public int Exp { get => exp; set => exp = value; }
        public int Character { get => character; set => character = value; }
        public string Id { get => userID; set => userID = value; }
        public int Matchs { get => matchs; set => matchs = value; }
        public DateTime Time { get => time; set => time = value; }

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
            json["matchs"] = matchs;
            json["wins"] = Wins;
            json["kill"] = Kill;
            json["death"] = Death;



            return json;
        }




    }




}
