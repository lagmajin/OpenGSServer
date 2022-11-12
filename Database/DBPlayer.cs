
using System;
using LiteDB;
using NUlid;
using OpenGSCore;

namespace OpenGSServer
{
    public class DBPlayer
    {


        [BsonId] public string Id { get; set; }
        public string AccountId { get; set; }

        public string Password { get; set; }
        public string HashedPassword { get; set; }

        public string Salt { get; set; }
        public string DisplayName { get; set; }

        public string CreatedTimeUtc { get; set; }



        public DBPlayer()
        {
            Id = Guid.NewGuid().ToString("N");

        }

        public DBPlayer(in DBPlayer player)
        {
            this.Id = player.Id;
            this.AccountId = player.AccountId;
            this.Password = player.Password;

        }


        public DBPlayer(in string accountID, in string hashedPassword, in string salt, in string displayName)
        {
            Id = Guid.NewGuid().ToString("N");
            AccountId = accountID;
            HashedPassword = hashedPassword;
            DisplayName = displayName;
            Salt = salt;
        }


        static DBPlayer CreateFromPlayerInfo(PlayerInfo info)
        {
            //var result = new DBPlayer();



            return null;
        }
    }

    public class DBPLayerFriendList
    {
        public string Id { get; set; }
        public string[] FriendList { get; set; }

        public DBPLayerFriendList()
        {

        }

        public DBPLayerFriendList(in DBPLayerFriendList list)
        {

        }


    }

    public class DBPlayerDetail
    {
        public int TotalKill { get; set; }
        public int DeathMatchKill { get; set; }

        public int TeamDeathMatchKill { get; set; }

        public int TeamSurvivalKill { get; set; }




        public int CTFFlagReturn { get; set; }

        public int Flag { get; set; }

        public DBPlayerDetail()
        {

        }
    }


}
