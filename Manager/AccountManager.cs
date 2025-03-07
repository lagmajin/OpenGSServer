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

    public class PlayerAccountData
    {
        public PlayerAccount PlayerAccount { get; set; } 
        public PlayerServerInformation PlayerServerInformation { get; set; } = new();

        public PlayerAccountData(PlayerAccount account,PlayerServerInformation information)
        {

        }

    }

    internal interface IAccountManager
    {

    }

    public class AccountManager:IAccountManager
    {

        private Dictionary<string, PlayerAccount> logonUser = new();

        private Dictionary<string, PlayerServerInformation> playerInformation = new();

        private List<PlayerAccountData> logonUserData=new();

      

        private static AccountManager _singleInstance = new();

        private int DefaultSaltCount = 8;

        public static AccountManager GetInstance()
        {
            return _singleInstance;
        }

        public AccountManager()
        {

        }


        public void AddNewLogonUser(in DBAccount db)
        {
            //accountList.Add(new UserAccount(db.AccountID, db.DisplayName, db.Password));

            lock (logonUser)
            {

                if (!logonUser.ContainsKey(db.AccountId))
                {


                    logonUser.Add(db.AccountId, new PlayerAccount(db.AccountId, db.DisplayName, db.Password));


                    lock (playerInformation)
                    {
                        var info = new PlayerServerInformation(EPlayerPlayingStatus.Unknown, EPlayerLocation.Lobby);

                        playerInformation.Add(db.AccountId, info);

                    }

                }
                else
                {


                }

            }



        }

        public void AddNewLogonUser(in PlayerAccountData data)
        {

        }


        public void RemoveLogonUser(in DBAccount db)
        {



            lock (logonUser)
            {
                logonUser.Remove(db.AccountId);
            }



        }


        public PlayerServerInformation PlayerInformation(in string id)
        {
            //var information = new PlayerServerInformation();

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



        public CreateNewAccountResult CreateNewAccount(in string accountID, in string pass, in string displayName)
        {



            var databaseManager = AccountDatabaseManager.GetInstance();

            var createdTimeUTC = DateTime.UtcNow.ToString();

            var salt = OpenGSCore.Hash.CreateSalt(DefaultSaltCount);

            var hashedPass = OpenGSCore.Hash.CreateHashWithSalt(pass, salt);

            var dbAccount = new DBAccount
            {
                AccountId = accountID,
                DisplayName = displayName,
                HashedPassword = hashedPass,
                Salt=salt,
                CreatedTimeUtc = createdTimeUTC
            };


            databaseManager.AddNewPlayerData(dbAccount);



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

            if (account == null)
            {
                type = eLoginResultType.AccountNotFound;


                goto RESULT;
            }

            var withSalt = OpenGSCore.Hash.CreateHashWithSalt(pass, account.Salt);

            ConsoleWrite.WriteMessage(withSalt);

            if (account.Salt == null)
            {

            }





            if (account.HashedPassword != withSalt)
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

        public void Logout(string id,bool force=false)
        {

        }


        public JObject Logout(string id, string pass)
        {
            lock (logonUser)
            {
                if (logonUser.ContainsKey(id))
                {
                    


                }
                else
                {

                }
            }


            var result = new JObject();

            return result;
        }

    }

}
