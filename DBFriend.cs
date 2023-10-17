using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class DBFriend
    {

        [BsonId] public string Id { get; set; }

        public string PlayerID{ get; set; }

        public string FriendAccountId { get; set; }

        public string CreatedTimeUtc { get; set; }
        public DBFriend()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public DBFriend(in DBFriend other)
        {
            this.Id = other.Id;
            FriendAccountId=other.FriendAccountId;
            CreatedTimeUtc=other.CreatedTimeUtc;


        }

    }
}
