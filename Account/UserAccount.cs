using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{
    public class UserAccount
    {
        private string userID;
        private string name;
        private string pass;
        private string globalID = Guid.NewGuid().ToString("N");

        private int matchs = 0;
        private int wins=0;
        private int kill=0;
        private int death=0;
        private int flagReturn=0;

        private int lv = 1;
        private int exp = 0;
        private int character = 0;
        DateTime time;


        public int Wins { get => wins; set => wins = value; }
        public int Kill { get => kill; set => kill = value; }
        public int Death { get => death; set => death = value; }
        public int FlagReturn { get => flagReturn; set => flagReturn = value; }
        public int Lv { get => lv; set => lv = value; }
        public string Name { get => name; set => name = value; }
        public string Pass { get => pass; set => pass = value; }
        public string Gid { get => globalID; set => globalID = value; }
        public int Exp { get => exp; set => exp = value; }
        public int Character { get => character; set => character = value; }
        public string Id { get => userID; set => userID = value; }
        public int Matchs { get => matchs; set => matchs = value; }
        public DateTime Time { get => time; set => time = value; }

        public UserAccount(in string id,in string name,in string pass)
        {
            Id = id;
            Name = name;
            Pass = pass;

            
        }

        public JObject toJson()
        {
            var json = new JObject();

            json["id"] = Id;
            json["name"] = Name;
            json["pass"] = Pass;
            json["character"] = character;
            json["guid"] =globalID;
            json["lv"] =Lv;
            json["exp"] = Exp;
            json["matchs"] = matchs;
            json["wins"] =Wins;
            json["kill"] = Kill;
            json["death"] = Death;



            return json;
        }




    }




}
