
using LiteDB;
using System.Collections.Generic;

namespace OpenGSServer
{



    public class GuildDatabaseManager : IAbstractDatabaseManager
    {
        private LiteDatabase db;

        static GuildDatabaseManager instance = new GuildDatabaseManager();

        public static string filename = "Database/guild.db";
        public static string connectionString = $"Filename={filename};connection=shared";

        public readonly string dbGuildTableName = "guild";
        public readonly string dbGuildMemberName = "guild_member";

        public static GuildDatabaseManager GetInstance()
        {
            return instance;
        }
        public void Connect()
        {
            if (db == null)
            {
                db = new LiteDatabase(connectionString);
            }
            else
            {

            }
        }


        public void Disconnect()
        {
            db?.Dispose();
        }

        public bool ExistGuild(in string guildName)
        {
            var col = db.GetCollection<DBGuild>("guild");

            if (col.FindOne(Query.EQ("", guildName)) == null)
            {
                return false;
            }
            else
            {
                return true;
            }

        }

        public void CreateNewGuild(in string guildName)
        {
            Connect();

            if (db != null)
            {
                var col = db.GetCollection<DBGuild>("guild");

                //var data = col.FindOne(Query.EQ(("Guild"), ""));

                var dbGuild = new DBGuild(guildName);

                col.Insert(dbGuild);


            }

            Disconnect();
        }

        public void RemoveGuild()
        {
            if (db == null)
            {
                Connect();
            }

            Disconnect();
        }

        public void RemoveAllGuild()
        {

        }

        public void AddGuildMember(in string id,in string guild)
        {
            if (ExistGuild(guild))
            {
                //var member = new DBGuildMember();

                //member.Id = id;
 
                

                var col = db.GetCollection<DBGuildMember>("guild_member");




            }


        }

        public List<DBGuildMember> GetGuildMember()
        {
            var result=new List<DBGuildMember>();

            return result;
        }


        public int GuildCount()
        {
            Connect();

            return db.GetCollection<DBGuild>().Count();

        }




    }



}
