using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

using OpenGSCore;

namespace OpenGSServer
{
    public static class AccountEventDelegate
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


            var salt = OpenGSCore.Hash.CreateSalt(8);

            //var hashedPassword = OpenGSCore.Hash.CreateHashWithSalt(pass, salt);

            //var dbPlayer=new DBPlayer()






        }

        public static void Login(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string id;
            string pass;

            if (dic.ContainsKey("id"))
            {
                id = dic["id"].ToString();
            }
            else
            {
                return;
            }

            if (dic.ContainsKey("pass"))
            {
                pass = dic["pass"].ToString();
            }
            else
            {
                return;
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

            

            var result=AccountManager.GetInstance().Logout(id, pass);


            session.SendAsyncJsonWithTimeStamp(result);


        }

        public static void SendUserInfo(ClientSession session, in IDictionary<string, JToken> dic)
        {
            string id;
            string pass;


            if (dic.TryGetValue("id", out var idToken))
            {
                id = idToken.ToString();

            }



            if (dic.TryGetValue("pass", out var passToken))
            {
                pass = passToken.ToString();
            }

            var json = new JObject();

            json["YourIPAddress"] = session.ClientIpAddress();

            session.SendAsync(json.ToString() + "\n");

            //session.Socket.endp



        }

        

        public static void RemoveAccount(ClientSession session, IDictionary<string, JToken> dic)
        {

        }

        public static void PlayerInfoRequest(ClientSession session,IDictionary<string,JToken> dic)
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
