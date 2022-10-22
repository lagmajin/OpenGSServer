using LiteDB;
using System;

using OpenGSCore;


namespace OpenGSServer
{
    public class AccountDatabaseManager : IDisposable
    {
        private LiteDatabase db;



        static AccountDatabaseManager instance = new AccountDatabaseManager();

        public static string tableName = "TaskGroup";

        public static string filename = "account.db";

        public static string connectionString = $"Filename={filename};connection=shared";


        public static AccountDatabaseManager GetInstance()
        {
            return instance;
        }
        public void Connect()
        {

            db = new LiteDatabase(connectionString);


            //var col = db.GetCollection<DBUser>("customers");



        }



        public void Disconnect()
        {
            if (db != null)
            {
                db.Dispose();
            }


        }
        public void AddNewPlayerData(in PlayerInfo info)
        {


        }

        public bool Exist(in string accountID)
        {
            return false;

        }

        public DBPlayer? GetDBPlayerInfo(in string id)
        {
            var col = db?.GetCollection<DBPlayer>("players");


            var data = col.FindOne(Query.EQ("AccountID", id));




            //col?.EnsureIndex(player => player.DBUniqueKey, unique: true);




            return data;
        }

        public void AddNewPlayerData(in DBPlayer player)
        {
            if (db == null)
            {
                Connect();
            }

            if (db != null)
            {
                var col = db.GetCollection<DBPlayer>("players");

                //col.EnsureIndex(player => player.AccountID, unique: true);

                if (col.FindOne(Query.EQ("AccountID", player.AccountID)) == null)
                {
                    col.Insert(player);
                }
                else
                {
                    ConsoleWrite.WriteMessage("Already have data in db");
                }



            }
            //col.FindOne()



        }
        public void UpdatePlayerData(in DBPlayer player)
        {
            if (db == null)
            {
                Connect();
            }

            if (db != null)
            {
                var col = db.GetCollection<DBPlayer>("players");

                //col.EnsureIndex(player => player.DBUniqueKey, unique: true);


                //col.Find(Query.EQ("ID", player.ID));

                col.EnsureIndex(player => player.AccountID, true);

                var rec = col.FindOne(Query.EQ("AccountID", player.AccountID));

                if (rec != null)
                {
                    rec.DisplayName = player.DisplayName;
                    rec.Password = player.Password;

                    ConsoleWrite.WriteMessage(rec.DisplayName);

                    //rec = player;

                    col.Update(rec);
                }
                else
                {
                    ConsoleWrite.WriteMessage("Not found");
                }


            }
            //co


        }
        public void ChangePassward(in string id, in string currentPass, in string newPass)
        {
            if (db == null)
            {
                Connect();
            }
            if (db != null)
            {
                var col = db.GetCollection<DBPlayer>("players");

                //col.EnsureIndex(player => player.DBUniqueKey, unique: true);


                //col.Find(Query.EQ("ID", player.ID));

                col.EnsureIndex(player => player.AccountID, true);

                //var rec = col.FindOne(Query.EQ("AccountID", player.AccountID));


                //Disconnect();

            }
        }
        public void RemoveAccount(in string accountID, in string password)
        {
            if (db == null)
            {
                Connect();
            }

            if (db != null)
            {
                var col = db.GetCollection<DBPlayer>("players");

                col.EnsureIndex(player => player.AccountID, true);

                //var rec = col.FindOne(Query.EQ("AccountID", player.AccountID));


                //Disconnect();

            }
        }

        public void RemoveAll()
        {
            if (db == null)
            {
                Connect();
            }


            if (db != null)
            {
                var col = db.GetCollection<DBPlayer>("players");

                col.EnsureIndex(player => player.AccountID, true);

                //var rec = col.FindOne(Query.EQ("AccountID", player.AccountID));

                //var result=col.Find(Query.All());

                col.DeleteAll();

                //Disconnect();
            }



        }


        public void FindUser()
        {

        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

                Dispose(true);
                GC.SuppressFinalize(this);
            }


        }
        void IDisposable.Dispose() => db?.Dispose();
    }
}
