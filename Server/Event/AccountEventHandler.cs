﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

using OpenGSCore;

namespace OpenGSServer
{
    internal interface IAccountEventHandler
    {

    }


    internal class AccountEventHandler:IAccountEventHandler
    {
        private static readonly int saltLenght=8;
        private readonly AccountManager _accountManager;
        //public AccountEventHandler() { }
        public AccountEventHandler(AccountManager accountManager)
        {
            _accountManager = accountManager;

        }
        public  void CreateNewAccount(in IClientSession session, IDictionary<string, JToken> dic)
        {
        }

        public void Login(in IClientSession session, in IDictionary<string, JToken> dic)
        {
     

            if (!dic.TryGetValue("id", out var idToken) || !dic.TryGetValue("pass", out var passToken))
            {
                return;
            }

            string id = idToken.ToString();
            string pass = passToken.ToString();

            var result = AccountManager.GetInstance().Login(id, pass);



            ConsoleWrite.WriteMessage(result.ToString());

            var json = result.ToJson();

            json["YourIPAddress"] = session.ClientIpAddress();




            session.SendAsyncJsonWithTimeStamp(json);


            var encryptManager = EncryptManager.Instance;

            var keyJson = new JObject();

            keyJson["MessageType"] = "EncryptKey";
            keyJson["RSAPublicKey"] = encryptManager.GetRSAPublicKey();


            session.SendAsyncJsonWithTimeStamp(keyJson);





        }

        public void Logout(IClientSession session) 
        {
        



        }

        public  void RemoveAccount(ClientSession session, IDictionary<string, JToken> dic)
        {

        }
    }


    public static class OldAccountEventHandler
    {
        public static int saltLength = 8;

        public static void CreateNewAccount(in ClientSession session, IDictionary<string, JToken> dic)
        {
            string accountID;

            string pass;

            string displayName;

            var result = new CreateNewAccountResult();


            if (dic.ContainsKey("AccountID"))
            {
                accountID = dic["Account"].ToString();
            }

            if (dic.ContainsKey("Password"))
            {
                pass = dic["Password"].ToString();
            }

            if (dic.ContainsKey("displayName"))
            {
                displayName = dic["DisplayName"].ToString();
            }


            var salt = OpenGSCore.Hash.CreateSalt(saltLength);

            //var hashedPassword = OpenGSCore.Hash.CreateHashWithSalt(pass, salt);

            //var dbPlayer=new DBPlayer()






        }

        public static string?  Login(in IClientSession session, in IDictionary<string, JToken> dic)
        {
            string id;
            string pass;

            if (dic.ContainsKey("id"))
            {
                id = dic["id"].ToString();
            }
            else
            {
                return null;
            }

            if (dic.ContainsKey("pass"))
            {
                pass = dic["pass"].ToString();
            }
            else
            {
                return null;
            }

            var result = AccountManager.GetInstance().Login(id, pass);



            ConsoleWrite.WriteMessage(result.ToString());

            var json = result.ToJson();

            json["YourIPAddress"] = session.ClientIpAddress();




            session.SendAsyncJsonWithTimeStamp(json);


            var encryptManager=EncryptManager.Instance;

            var keyJson = new JObject();

            keyJson["MessageType"] = "EncryptKey";
            keyJson["RSAPublicKey"]=encryptManager.GetRSAPublicKey();


            session.SendAsyncJsonWithTimeStamp(keyJson);


         
            if(result.succeeded)
            {
                return id;
            }
            else
            {
                return null;
            }


        }

        public static void Logout(IClientSession session)
        {

            //AccountManager.GetInstance().

        }

        public static void Logout(ClientSession session, in IDictionary<string, JToken> dic)
        {
            string id="";
            string pass="";


            if (dic.TryGetValue("id", out var idToken))
            {
                id = idToken.ToString();

            }



            if (dic.TryGetValue("pass", out var passToken))
            {
                pass = passToken.ToString();
            }

            

            var result = AccountManager.GetInstance().LogoutWithPassword(id, pass);


            session.SendAsyncJsonWithTimeStamp(result);


        }

        public static void SendUserInfo(ClientSession session, in IDictionary<string, JToken> dic)
        {
            string id = string.Empty;
            string pass = string.Empty;

            if (dic.TryGetValue("id", out var idToken))
            {
                id = idToken.ToString();

            }



            if (dic.TryGetValue("pass", out var passToken))
            {
                pass = passToken.ToString();
            }





            var json = new JObject
            {
                ["YourIPAddress"] = session.ClientIpAddress(),
        
            };

            session.SendAsync(json.ToString() + "\n");

            //session.Socket.endp



        }

        

        public static void RemoveAccount(IClientSession session, IDictionary<string, JToken> dic)
        {

        }

        public static void PlayerInfoRequest(IClientSession session,IDictionary<string,JToken> dic)
        {
            var instance=AccountDatabaseManager.GetInstance();

            


            if (dic.TryGetValue("id", out var idToken))
            {
                instance.GetPlayerInfo(idToken.ToString());

            }


            //instance.GetPlayerInfo();

            //instance.GetPlayerInfo();

        }
        public static void AddFriendRequest(IDictionary<string, JToken> dic)
        {

        }

        public static void RemoveFriend(IDictionary<string, JToken> dic)
        {

        }

        public static void FriendsDataRequest(IDictionary<string, JToken> dic)
        {
            string id;

            string pass;

            if (dic.TryGetValue("id", out var idToken))
            {
                //instance.GetPlayerInfo(idToken.ToString());

            }


            var accountManager = AccountManager.GetInstance();

            



            var json = new JObject();

            json["Friend"] = "";







        }
    }
}
