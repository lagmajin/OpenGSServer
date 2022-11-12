
using LiteDB;




namespace OpenGSServer
{



    public class GuildDatabaseManager : IAbstractDatabaseManager
    {
        private LiteDatabase db;

        static GuildDatabaseManager instance = new GuildDatabaseManager();

        public static string filename = "Database/guild.db";
        public static string connectionString = $"Filename={filename};connection=shared";


        public static GuildDatabaseManager GetInstance()
        {
            return instance;
        }
        public void Connect()
        {

            db = new LiteDatabase(connectionString);

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
            if (db == null)
            {
                Connect();
            }

            if (db != null)
            {
                var col = db.GetCollection<DBGuild>("guild");

                //var data = col.FindOne(Query.EQ(("Guild"), ""));

                var dbGuild = new DBGuild(guildName);

                col.Insert(dbGuild);


            }
        }

        public void RemoveGuild()
        {
            if (db == null)
            {
                Connect();
            }
        }


    }



}
