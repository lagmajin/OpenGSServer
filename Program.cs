using LiteDB;
using System;
using System.Linq;
using System.Threading;

using System.CommandLine.Parser;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{
    class Program
    {
        private static bool IsEnd { get; set; } = false;
        private static bool MonitorTaskFlag { get; set; } = false;


        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            var generalServerV2 = LobbyServerManagerV2.GetInstance();

            generalServerV2.Stop();
            Console.WriteLine("exit");
        }
        static async void MonitorTask(CancellationToken cancelToken = default)
        {
            ConsoleWrite.WriteMessage("MonitorTaskRun");

            while (!MonitorTaskFlag)
            {

                //ConsoleWrite.WriteMessage("MonitorTaskRun");

                await Task.Delay(1500);
            }


            ConsoleWrite.WriteMessage("MonitorTaskEnd");


        }
        static void Main(string[] args)
        {

            // Get a collection (or create, if doesn't exist)
            var room = new JObject();

            room["RoomNumber"] = "001";
            room["RoomName"] = "LIVE!LIVE!LIVE!";
            room["GameMode"] = "tdm";

            var room2 = new JObject();

            room2["RoomNumber"] = "002";
            room2["RoomName"] = "LIVE!LIVE!LIVE!";


            var jArray = new JArray();
            jArray.Add(room);
            jArray.Add(room2);

            var json = new JObject();

            json["Rooms"] = jArray;


            Console.Write(json.ToString());


            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            string mutexName = "Global\\OpenGSServer";

            bool hasHandle = false;

            var mutex = new Mutex(true, mutexName, out hasHandle);

            //bool japanese = true;




            if (args.Length > 0)
            {
                while (args.Length >= 0)
                {

                }

            }
            else
            {

                //Console.WriteLine("No Commandline aragment");
            }










            if (hasHandle)
            {
                try
                {
                    ConsoleWrite.WriteMessage("OpenGS game server initializing.....", ConsoleColor.Green);




                    var accountDatabaseManager = AccountDatabaseManager.GetInstance();

                    accountDatabaseManager.Connect();



                    //accountDatabaseManager.AddNewPlayerData(dbplayerTest);
                    //accountDatabaseManager.AddNewPlayerData(dbplayerTest2);

                   // var guildDatabase = GuildDatabaseManager.GetInstance();

                    //guildDatabase.Connect();

                    //guildDatabase.CreateNewGuild("TestPlayers");



                    var generalServerV2 = LobbyServerManagerV2.GetInstance();

                    generalServerV2.Listen(50000);

                    //var matchServer = serverManager.GetMatchServer() ;

                    var matchServerV2 = MatchServerV2.GetInstance();

                    matchServerV2.Listen(50010);


                    var cts = new CancellationTokenSource();


                    var monitorTask = Task.Run(() => MonitorTask(cts.Token), cts.Token).ConfigureAwait(false);




                    while (!IsEnd)
                    {
                        var input = Console.ReadLine();

                        ConsoleWrite.WriteMessage("Command:" + input);

                        if (input == null)
                        {
                            continue;

                        }

                        var words = input.Split(" ").ToArray();

                        var command = words[0].ToLower();

                        var param = words.Skip(1).ToList();



                        if (words.Length > 0)
                        {
                            //var firstWord = words[0].ToLower();

                            if (command == "serverinfo")
                            {
                                var info = WaitRoomManager.GetInstance().Info2();


                                ConsoleWrite.WriteMessage(info.ToString());
                            }


                            if (command == "playerinfo")
                            {
                                if (param.Count < 1)
                                {

                                    var name = param[0];

                                    var instance = AccountDatabaseManager.GetInstance();






                                }


                            }

                            if (command == "guildinfo")
                            {

                                if (param.Count < 1)
                                {
                                    var name = param[1];




                                }

                            }

                            if (command == "addnewuser")
                            {
                                if (param.Count > 2)
                                {
                                    var id = param[0];

                                    var pass = param[1];

                                    var displayName = param[2];

                                    
                                    ConsoleWrite.WriteMessage("AddNewUser");

                                    var accountManager = AccountManager.GetInstance();

                                    accountManager.CreateNewAccount(id, pass,displayName);

                                    //accountManager.CreateNewAccount()



                                }


                            }


                            if (command == "addnewguild")
                            {
                                if (param.Count < 1)
                                {

                                }


                            }

                            if (command == "addnewroom")
                            {

                                if (param.Count > 0)
                                {
                                    ConsoleWrite.WriteMessage("Add new waitroom");
                                    var roomName = param[0];


                                    var roomManager = WaitRoomManager.GetInstance();

                                    roomManager.CreateNewWaitRoom("");



                                }
                                else
                                {


                                }



                            }



                            if (command == "exit" || command == "shutdown")
                            {
                                for (var i = 0; i < 3; i++)
                                {
                                    ConsoleWrite.WriteMessage("Exit application in 3 seconds...", ConsoleColor.Red);
                                    Thread.Sleep(1000);
                                }

                                //Thread.Sleep(3000);

                                Environment.Exit(0);
                            }

                            if (command == "run" | command == "-r")
                            {

                            }

                            if (command == "stop" | command == "-s")
                            {
                                if (param.Count < 0)
                                {
                                    continue;
                                }

                                ConsoleWrite.WriteMessage("Stop server");


                                generalServerV2.Stop();



                            }

                            if (command == "new" | command == "-n")
                            {
                                if (param.Count < 0)
                                {

                                    continue;
                                }

                                if (param[0] == "player")
                                {
                                    if (param.Count < 1)
                                    {

                                        ConsoleWrite.WriteMessage("Create new player", ConsoleColor.Red);



                                    }
                                    else
                                    {
                                        ConsoleWrite.WriteMessage("");



                                    }



                                }

                                if (param[0] == "guild")
                                {

                                }


                                ConsoleWrite.WriteMessage("");




                            }

                            if (command == "remove" | command == "delete")
                            {
                                if (param.Count < 0)
                                {
                                    continue;
                                }

                                if (param[0] == "player")
                                {




                                }

                                if (param[0] == "guild")
                                {
                                    ConsoleWrite.WriteMessage("[Warning] Delete guild [y,n]", ConsoleColor.Red);



                                    var q = Console.ReadLine();



                                    GuildDatabaseManager.GetInstance().RemoveGuild();

                                }



                            }



                            if (command == "info" | command == "-i")
                            {
                                ConsoleWrite.WriteMessage("Server Info");


                            }

                            if (command == "help" | command == "-h")
                            {
                                ConsoleWrite.WriteMessage("help -h");
                                ConsoleWrite.WriteMessage("new -n");
                                ConsoleWrite.WriteMessage("remove -r");
                                ConsoleWrite.WriteMessage("info -i");


                                ConsoleWrite.WriteMessage("run -r");

                            }

                            if (command == "clear")
                            {
                                Console.Clear();

                            }





                        }

                    }

                    MonitorTaskFlag = true;
                    //monitorTask.Wait();
                }
                finally
                {
                    ConsoleWrite.WriteMessage("", ConsoleColor.Red);

                }

            }
            else
            {
                mutex.ReleaseMutex();
                mutex.Close();

            }








        }
        static void timerCB(object obj)
        {
            var msec = DateTime.Now.Millisecond;
            Console.WriteLine("msec: {0}", msec);
        }
    }


}
