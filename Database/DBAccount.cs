
using System;
using LiteDB;
using NUlid;
using OpenGSCore;

namespace OpenGSServer
{
    public class DBAccount
    {


        [BsonId] public string Id { get; set; }
        public string AccountId { get; set; }

        public string Password { get; set; }
        public string HashedPassword { get; set; }

        public string Salt { get; set; }
        public string DisplayName { get; set; }

        public string CreatedTimeUtc { get; set; }



        public DBAccount()
        {
            Id = Guid.NewGuid().ToString("N");

        }

        public DBAccount(in DBAccount player)
        {
            this.Id = player.Id;
            this.AccountId = player.AccountId;
            this.Password = player.Password;

        }


        public DBAccount(in string accountID, in string hashedPassword, in string salt, in string displayName)
        {
            Id = Guid.NewGuid().ToString("N");
            AccountId = accountID;
            HashedPassword = hashedPassword;
            DisplayName = displayName;
            Salt = salt;
        }


        static DBAccount CreateFromPlayerInfo(PlayerInfo info)
        {
            //var result = new DBPlayer();



            return null;
        }
    }

    public class DBAccountFriendList
    {
        public string Id { get; set; }
        public string[] FriendList { get; set; }

        public DBAccountFriendList()
        {

        }

        public DBAccountFriendList(in DBAccountFriendList list)
        {

        }


    }

    public class DBAccountScore
    {
        [BsonId] public string accountID { get; set; }
        public int TotalKill { get; set; } = 0;
        public int DeathMatchKill { get; set; } = 0;

        public int TeamDeathMatchKill { get; set; } = 0;

        public int TeamSurvivalKill { get; set; } = 0;




        public int CtfFlagReturn { get; set; } = 0;

        //public int Flag { get; set; }

        public DBAccountScore()
        {

        }

        public DBAccountScore(in DBAccountScore account)
        {
            accountID = account.accountID;


        }

    }


}
