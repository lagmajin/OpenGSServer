﻿using LiteDB;
using System;

using OpenGSCore;


namespace OpenGSServer
{
    public class AccountDatabaseManager : IDisposable
    {
        private LiteDatabase db;



        static AccountDatabaseManager instance = new AccountDatabaseManager();

        public static string tableName = "TaskGroup";

        public static string filename = "Database/account.db";

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
            var col = db.GetCollection<DBPlayer>("players");


            if (col.FindOne(Query.EQ("AccountID", accountID)) == null)
            {
                return false;
            }
            else
            {
                return true;
            }


        }

        public DBPlayer? GetDBPlayerInfoOld(in string id)
        {
            var col = db?.GetCollection<DBPlayer>("players");

            DBPlayer? data = null;

            if (col != null)
            {
                data = col.FindOne(Query.EQ("AccountID", id));

            }

            return data;
        }

        public void GetPlayerInfo(in string account)
        {
            var col = db?.GetCollection<DBPlayer>("players");


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

                //var salt = OpenGSCore.Hash.CreateSalt(8);




                if (col.FindOne(Query.EQ("AccountID", player.AccountId)) == null)
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
        public void ChangePassword(in string id, in string currentPass, in string newPass)
        {
            if (db == null)
            {
                Connect();
            }
            if (db != null)
            {
                var col = db.GetCollection<DBPlayer>("players");



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
                var col = db.GetCollection<DBPlayer>("players");

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
                var col = db.GetCollection<DBPlayer>("players");

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
