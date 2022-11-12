using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenGSCore;


namespace OpenGSServer
{
    enum EDealWithDuplicateLogin
    {
        KICK_FIRST_USER,
        KICK_SECOND_USER,
        KICK_BOTH_USER

    }

    public class AccountManager
    {
        private List<PlayerAccount> accountList = new List<PlayerAccount>();

        private Dictionary<string, PlayerAccount> logonUser = new();

        //private Dictionary<string,PlayerData> playerData=new();


        private Dictionary<string, PlayerInformation> playerInformation = new();
        private Dictionary<string, FriendList> _friendList = new();


        //private ConcurrentDictionary<string, PlayerAccount> logonUser2 = new ConcurrentDictionary<string, PlayerAccount>();

        private static AccountManager _singleInstance = new();

        private int DefaultSaltCount = 8;

        public static AccountManager GetInstance()
        {
            return _singleInstance;
        }

        private AccountManager()
        {

        }


        public void AddNewLogonUser(in DBPlayer db)
        {
            //accountList.Add(new UserAccount(db.AccountID, db.DisplayName, db.Password));



            if (logonUser.ContainsKey(db.AccountId))
            {
                lock (logonUser)
                {

                    logonUser.Add(db.AccountId, new PlayerAccount(db.AccountId, db.DisplayName, db.Password));
                }

                lock (playerInformation)
                {
                    var info = new PlayerInformation(ePlayerPlayingStatus.Unknown);

                    playerInformation.Add(db.AccountId, info);

                }

            }
            else
            {


            }




        }




        public PlayerInformation PlayerInformation(in string id)
        {
            //var information = new PlayerInformation();

            lock (logonUser)
            {
                if (!logonUser.ContainsKey(id)) return null;
                lock (playerInformation)
                {

                    if (playerInformation.ContainsKey(id))
                    {

                        return playerInformation[id];

                    }
                    else
                    {




                    }


                }

            }



            return null;
        }


        public FriendList FriendList(in string id)
        {





            return null;
        }

        public void RemoveLogonUser(in DBPlayer db)
        {



            lock (logonUser)
            {
                logonUser.Remove(db.AccountId);
            }



        }


        public CreateNewAccountResult CreateNewAccount(in string accountID, in string pass, in string displayName)
        {



            var databaseManager = AccountDatabaseManager.GetInstance();

            var createdTimeUTC = DateTime.UtcNow.ToString();

            var salt = OpenGSCore.Hash.CreateSalt(DefaultSaltCount);







            var result = new CreateNewAccountResult();







            return result;
        }

        public void RemoveAccount(in string id)
        {

        }

        public bool ExistID(in string id)
        {
            if (logonUser.ContainsKey(id))
            {

            }
            else
            {

            }



            return false;
        }



        public LoginResult Login(in string id, in string pass)
        {
            var databaseManager = AccountDatabaseManager.GetInstance();


            var account = databaseManager.GetDBPlayerInfoOld(id);


            var type = eLoginResultType.Unknown;



            //var pass2 = account.Salt + "pass";

            var pass2 = OpenGSCore.Hash.CreateHashWithSalt(pass, account.Salt);

            ConsoleWrite.WriteMessage(pass2);

            if (account.Salt == null)
            {

            }



            if (account == null)
            {
                type = eLoginResultType.AccountNotFound;


                goto RESULT;
            }

            if (account.HashedPassword != pass2)
            {
                type = eLoginResultType.InvalidIDorPassword;

                goto RESULT;

            }
            else
            {



                type = eLoginResultType.LoginSucceeded;

                AddNewLogonUser(account);


                goto RESULT;

            }





        RESULT:


            var result = new LoginResult(id, type);



            //bool succeded = false;


            return result;

        }




        public JObject Logout(string id, string pass)
        {
            if (logonUser.ContainsKey(id))
            {

            }
            else
            {

            }


            var result = new JObject();

            return result;
        }

    }

}
