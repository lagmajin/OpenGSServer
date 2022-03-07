using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace OpenGSServer
{
    public static class AccountEventDelegate
    {
        public static void Login(in ClientSession session,in IDictionary<string,JToken> dic)
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



            //var result = AccountManager.GetInstance().Login(id, pass);

            //var result = AccountManager.GetInstance().Login2(id, pass);

            var result = AccountManager.GetInstance().Login(id, pass);


            ConsoleWrite.WriteMessage(result.ToString());
            var str = result.ToJson().ToString(Formatting.None);

            ConsoleWrite.WriteMessage(str);

            Thread.Sleep(5);

            session.SendAsync(str);

        }

        public static void Logout(ClientSession session,in IDictionary<string, JToken> dic)
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




        }

        public static void CreateNewAccount(in ClientSession session, IDictionary<string, JToken> dic)
        {
            string accountID;

            string pass;

            string displayName;

            var result = new CreateNewAccountResult();


            if(dic.ContainsKey("AccountID"))
            {
                accountID = dic["Account"].ToString();
            }

            if(dic.ContainsKey("Password"))
            {
                pass=dic["Password"].ToString();
            }

            if(dic.ContainsKey("displayName"))
            {
                displayName = dic["DisplayName"].ToString();
            }











        }

        public static void RemoveAccount(ClientSession session, IDictionary<string, JToken> dic)
        {

        }

        public static void AddFriendRequest(IDictionary<string, JToken> dic)
        {

        }

        public static void RemoveFriend(IDictionary<string, JToken> dic)
        {

        }

        public static void FriendsDataRequest(IDictionary<string,JToken> dic)
        {
            string id;

            string pass;








        }


    }
}
