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
        private Dictionary<string, FriendList> friendList = new();


        //private ConcurrentDictionary<string, PlayerAccount> logonUser2 = new ConcurrentDictionary<string, PlayerAccount>();

        private static AccountManager _singleInstance = new();

        public static AccountManager GetInstance()
        {
            return _singleInstance;
        }

        private AccountManager()
        {

        }

        public static string HomeDirectory()
        {
            return System.Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        }

        public static string OpenGSDir()
        {
            var homeDir = HomeDirectory();
            var gsDir = homeDir + "\\OpenGS";

            if (Directory.Exists(gsDir))
            {

            }
            else
            {
                Directory.CreateDirectory(gsDir);
            }

            return gsDir;
        }

        public static string OpenGSAccountDir()
        {
            var gsDir = OpenGSDir();
            var accountDir = gsDir + "\\Account";

            if (Directory.Exists(accountDir))
            {

            }
            else
            {
                Directory.CreateDirectory(accountDir);
            }

            return accountDir;
        }

        public void AddNewLogonUser(in string accountID, in string id, in string displayName)
        {
            var account = new PlayerAccount(accountID, id, displayName);


            accountList.Add(account);


            //var playerInformation = new PlayerInformation();



        }

        public void AddNewLogonUser(in DBPlayer db)
        {
            //accountList.Add(new UserAccount(db.AccountID, db.DisplayName, db.Password));

            if (logonUser.ContainsKey(db.AccountID))
            {
                lock (logonUser)
                {

                    logonUser.Add(db.AccountID, new PlayerAccount(db.AccountID, db.DisplayName, db.Password));
                }

                lock (playerInformation)
                {
                    var info = new PlayerInformation(ePlayerPlayingStatus.Unknown);

                    playerInformation.Add(db.AccountID, info);

                }

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
                logonUser.Remove(db.AccountID);
            }



        }


        public JObject CreateNewAccount()
        {
            var result = new JObject();

            result["MessageType"] = "Create new account failed...";


            return result;
        }

        public bool CreateNewAccountOld(in string id, in string name, in string pass)
        {
            var result = new JObject();
            var directory = OpenGSAccountDir();

            var userDir = directory + "\\" + id;

            if (!Directory.Exists(userDir))
            {
                var userAccount = new PlayerAccount(id, name, pass);

                var json = userAccount.ToJson();

                Directory.CreateDirectory(userDir);

                var sw = new StreamWriter(userDir + "\\" + id + ".json", true);

                sw.WriteLine(json.ToString());
                sw.Close();

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Creating new account...");
                Console.ForegroundColor = ConsoleColor.White;


                return true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Can not register new account");
                Console.ForegroundColor = ConsoleColor.White;

            }



            return false;
        }

        public static void RemoveAccount(in string id)
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

        public bool old(String id, String pass)
        {
            if (id == "test" && pass == "test")
            {
                return true;
            }

            return false;
        }

        public LoginResult Old(in string id, in string pass)
        {
            ConsoleWrite.WriteMessage("id:" + id + "pass:" + pass);

            var accountDir = OpenGSAccountDir();

            var userDir = accountDir + "\\" + id;
            var userJsonPath = userDir + "\\" + id + ".json";

            string reason;
            bool succeded = false;

            eLoginResultType type = eLoginResultType.Unknown;

            if (Directory.Exists(userDir))
            {
                Console.WriteLine("File Exist");
                Console.WriteLine(userJsonPath);


                if (File.Exists(userJsonPath))
                {
                    var fs = new FileStream(userJsonPath, FileMode.Open);
                    var sr = new StreamReader(fs);
                    var text = sr.ReadToEnd();

                    Console.WriteLine(text);
                    fs.Close();
                    sr.Close();

                    var json = JObject.Parse(text);

                    var name = json["name"].ToString();
                    var guid = json["guid"].ToString();
                    var acpass = json["pass"].ToString();

                    Console.WriteLine(guid);



                    if (logonUser.ContainsKey(id))
                    {
                        type = eLoginResultType.AlreadyLogonSameUser;

                        succeded = false;
                    }

                    if (pass == acpass)
                    {
                        Console.WriteLine("Password correct...");

                        //result["MessageType"] = "LoginSuccess";
                        //result["guid"] = guid;

                        var account = new PlayerAccount(id, name, pass);

                        Console.WriteLine("New user logged in...");
                        Console.WriteLine("UserID:" + id);
                        Console.WriteLine("UserName:" + name);


                        accountList.Add(account);

                        type = eLoginResultType.LoginSucceeded;
                        succeded = true;
                    }
                    else
                    {
                        type = eLoginResultType.InvalidIDorPassword;
                        succeded = false;
                    }


                }
                else
                {
                    type = eLoginResultType.InvalidIDorPassword;
                    succeded = false;
                }




            }
            else
            {
                //result["MessageType"] = "LoginFailed";
                //result["Reason"] = "Player not exits ";
            }

            var result = new LoginResult(succeded, type);

            return result;
        }

        public LoginResult Login(in string id, in string pass)
        {
            var databaseManager = AccountDatabaseManager.GetInstance();


            var account = databaseManager.GetDBPlayerInfo(id);


            var type = eLoginResultType.Unknown;


            if (account == null)
            {
                type = eLoginResultType.AccountNotFound;


                goto RESULT;
            }

            if (account.Password != pass)
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


            bool succeded = false;
            var result = new LoginResult(type);

            return result;

        }


        public JObject Login2(String id, String pass)
        {
            ConsoleWrite.WriteMessage("id:" + id + "pass:" + pass);

            var result = new JObject();
            result["testt"] = "";

            var accountDir = OpenGSAccountDir();

            var userDir = accountDir + "\\" + id;
            var userJsonPath = userDir + "\\" + id + ".json";

            if (Directory.Exists(userDir))
            {
                Console.WriteLine("File Exist");
                Console.WriteLine(userJsonPath);


                if (File.Exists(userJsonPath))
                {
                    var fs = new FileStream(userJsonPath, FileMode.Open);
                    var sr = new StreamReader(fs);
                    var text = sr.ReadToEnd();

                    Console.WriteLine(text);
                    fs.Close();
                    sr.Close();

                    var json = JObject.Parse(text);

                    var name = json["name"].ToString();
                    var guid = json["guid"].ToString();
                    var acpass = json["pass"].ToString();

                    Console.WriteLine(guid);

                    if (logonUser.ContainsKey(id))
                    {
                        result["MessageType"] = "LoginFailed";
                        result["Reason"] = "Already log on same user";

                    }

                    if (pass == acpass)
                    {
                        Console.WriteLine("Password correct...");

                        result["MessageType"] = "LoginSuccess";
                        //result["guid"] = guid;

                        var account = new PlayerAccount(id, name, pass);

                        Console.WriteLine("New user logged in...");
                        Console.WriteLine("UserID:" + id);
                        Console.WriteLine("UserName:" + name);


                        accountList.Add(account);

                        result["MessageType"] = "LoginSuccess";
                        result["Reason"] = "Great login successful";
                        result["YourGlobalID"] = account.Gid;
                    }
                    else
                    {
                        result["MessageType"] = "LoginFailed";
                        result["Reason"] = "Incorrect username or password";

                    }


                }
                else
                {
                    result["MessageType"] = "LoginFailed";
                    result["Reason"] = "User file not exits ";
                }




            }
            else
            {
                result["MessageType"] = "LoginFailed";
                result["Reason"] = "Player not exits ";
            }

            result["TimeStampLocalTime"] = DateTime.Now;
            result["TimeStampUtc"] = DateTime.UtcNow.ToString();
            ConsoleWrite.WriteMessage("Result");
            ConsoleWrite.WriteMessage(result.ToString());

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
