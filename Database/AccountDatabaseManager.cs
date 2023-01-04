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

        public static string accountTableName = "Account";

        public static string accountScoreTableName = "AccountScore";

        public static string filename = "Database/Account.db";

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
            db?.Dispose();


        }
        public void AddNewPlayerData(in PlayerInfo info)
        {


        }

        public bool Exist(in string accountID)
        {
            var col = db.GetCollection<DBAccount>(accountTableName);


            if (col.FindOne(Query.EQ("AccountID", accountID)) == null)
            {
                return false;
            }
            else
            {
                return true;
            }


        }

        public DBAccount? GetDBPlayerInfoOld(in string id)
        {
            var col = db?.GetCollection<DBAccount>(accountTableName);

            DBAccount? data = null;

            if (col != null)
            {
                data = col.FindOne(Query.EQ("AccountId", id));


            }

            return data;
        }

        public void GetPlayerInfo(in string account)
        {
            var col = db?.GetCollection<DBAccount>(accountTableName);

            DBAccount? acount = null;

            if (col != null)
            {


            }



        }

        public int UserCount()
        {
            var acCol=db.GetCollection<DBAccount>();

            return acCol.Count();
        }



        public void AddNewPlayerData(in DBAccount player)
        {
            if (db == null)
            {
                Connect();
            }

            if (db != null)
            {
                var col = db.GetCollection<DBAccount>(accountTableName);

                var collection = db.GetCollection<DBAccountScore>(accountScoreTableName);

                var friendCol = db.GetCollection<DBAccountFriendList>("Friend");
                




                if (col.FindOne(Query.EQ("AccountID", player.AccountId)) == null)
                {
                 
                    col.Insert(player);

                    if (collection.FindOne(Query.EQ("AccountID", player.AccountId)) == null)
                    {
                        var dbAccountDetail = new DBAccountScore
                        {
                            accountID = player.AccountId
                        };

                        collection.Insert(dbAccountDetail);
                    }
                    else
                    {

                    }

                    if (friendCol.FindOne(Query.EQ("AccountID", player.AccountId))==null)
                    {
                        var dbAccountFriend = new DBAccountFriendList()
                        {
                            AccountID = player.AccountId

                        };

                        friendCol.Insert(dbAccountFriend);
                    }

                }
                else
                {
                    ConsoleWrite.WriteMessage("Already have data in db");
                }






            }
            //col.FindOne()



        }
        public void UpdateAccountData(in DBAccount player)
        {
            if (db == null)
            {
                Connect();
            }

            if (db != null)
            {
                var col = db.GetCollection<DBAccount>("players");


                col.EnsureIndex(player => player.AccountId, true);

                var rec = col.FindOne(Query.EQ("AccountID", player.AccountId));

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

        public void UpdateAccountScore(in DBAccountScore score)
        {

        }

        public void UpdateFriendList()
        {

        }
        public void ChangePassword(in string id, in string currentPass, in string newPass)
        {
            if (db == null)
            {
                Connect();
            }
            if (db != null)
            {
                var col = db.GetCollection<DBAccount>("players");




                col.EnsureIndex(player => player.Id, true);



                var newSalt = OpenGSCore.Hash.CreateSalt(8);

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
                var col = db.GetCollection<DBAccount>("players");

                col.EnsureIndex(player => player.Id, true);

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
                var col = db.GetCollection<DBAccount>("players");

                col.EnsureIndex(player => player.Id, true);

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
