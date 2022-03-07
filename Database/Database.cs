using OpenGSCore;

namespace OpenGSServer
{


    public class DBPlayer
    {


        public int Id { get; set; }
        public string AccountID { get; set; }

        public string Password { get; set; }
        public string DisplayName { get; set; }
        //public int DBUniqueKey { get; set; }

        public string[] Friends { get; set; }


        public int TotalKill { get; set; }
        public int DeathMatchKill { get; set; }

        public int TeamDeathMatchKill { get; set; }
        
        public int TeamSuvivalKill { get; set; }

        //public string[] Phones { get; set; }
        //public int CTFKill { get; set; }

        //public int TotalDeath { get; set; }

        


        public int CTFFlagReturn { get; set; }

        public int Flag { get; set; }

        public DBPlayer()
        {

        }

        public DBPlayer(in DBPlayer player)
        {
            this.AccountID = player.AccountID;
            this.Password = player.Password;

            this.TotalKill = player.TotalKill;


            this.CTFFlagReturn = player.CTFFlagReturn;
        }

        public DBPlayer(in string accountID, in string pass,in string displayName)
        {
            AccountID = accountID;

            Password = pass;
            DisplayName = displayName;

            

        }


        static DBPlayer CreateFromPlayerInfo(PlayerInfo info)
        {
            //var result = new DBPlayer();

            

            return null;
        }
    }

}
