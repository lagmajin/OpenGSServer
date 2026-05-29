
using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using NUlid;
using OpenGSCore;

namespace OpenGSServer
{
    
    public class DBAccount
    {


        public enum EAccountStatus
        {
            Unknown = 0,
            Active,
            Suspended,
            Banned,
        }

        [BsonId] public string Id { get; set; }
        public string AccountId { get; set; }

        public string Password { get; set; }
        public string HashedPassword { get; set; }

        public string Salt { get; set; }

        public string DisplayName { get; set; }

        public string CreatedTimeUtc { get; set; }

        public string TimeEncode { get; set; }

        public string FirstAccessIPAddress { get; set; }

        public string LastAccessIPAddress { get; set; }
        public string LastLoginIPAddress { get; set; }
        public string LastLoginTimeUtc { get; set; }
        public string LastUpdatedUtc { get; set; }
        public EAccountStatus Status { get; set; } = EAccountStatus.Active;
        public int Level { get; set; } = 1;
        public long Exp { get; set; } = 0;
        public long Credits { get; set; } = 1000;
        public List<string> PurchasedItems { get; set; } = new();
        public List<DBEquippedItem> EquippedItems { get; set; } = new();
        public List<DBInstantEquippedItem> EquippedInstantItems { get; set; } = new();
        public PlayerLifeTimeScore LifeTimeScore { get; set; } = new();
        public List<string> ProfileTags { get; set; } = new();

        public DBAccount()
        {
            Id = Guid.NewGuid().ToString("N");

        }

        public DBAccount(in DBAccount player)
        {
            this.Id = player.Id;
            this.AccountId = player.AccountId;
            this.Password = player.Password;
            this.Salt = player.Salt;
            this.HashedPassword = player.HashedPassword;
            this.DisplayName = player.DisplayName;
            this.Credits = player.Credits;
            this.PurchasedItems = new List<string>(player.PurchasedItems ?? new List<string>());
            this.EquippedItems = new List<DBEquippedItem>((player.EquippedItems ?? new List<DBEquippedItem>()).Select(item => new DBEquippedItem(item)));
            this.EquippedInstantItems = new List<DBInstantEquippedItem>((player.EquippedInstantItems ?? new List<DBInstantEquippedItem>()).Select(item => new DBInstantEquippedItem(item)));

            FirstAccessIPAddress = player.FirstAccessIPAddress;
        }


        public DBAccount(in string accountID, in string hashedPassword, in string salt, in string displayName)
        {


           TimeEncode="yyyy MMMM dd";

            CreatedTimeUtc = DateTime.UtcNow.ToString(TimeEncode);

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
        //[BsonId] public string Id { get; set; }

        public string AccountID { get; set; }
        //public string[] FriendList { get; set; }

        public List<string> FriendList2 { get; set; } = new();

        public DBAccountFriendList()
        {
            //var sp=FriendList.AsSpan();

            FriendList2.Add("Test2");
            FriendList2.Add("Test3");
        }

        public DBAccountFriendList(in DBAccountFriendList list)
        {

        }


    }

    public class DBEquippedItem
    {
        public string Category { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;

        public DBEquippedItem()
        {
        }

        public DBEquippedItem(in DBEquippedItem other)
        {
            Category = other?.Category ?? string.Empty;
            ItemId = other?.ItemId ?? string.Empty;
        }
    }

    public class DBInstantEquippedItem
    {
        public int Slot { get; set; } = 0;
        public string ItemId { get; set; } = string.Empty;

        public DBInstantEquippedItem()
        {
        }

        public DBInstantEquippedItem(in DBInstantEquippedItem other)
        {
            Slot = other?.Slot ?? 0;
            ItemId = other?.ItemId ?? string.Empty;
        }
    }

    public class DBAccountScore
    {
        public string accountID { get; set; }
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
