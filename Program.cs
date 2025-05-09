﻿
using System;
using System.Linq;
using System.Threading;

using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;
using Autofac;


 namespace OpenGSServer
{


    class Program
    {
        private static bool IsEnd { get; set; } = false;
        private static bool MonitorTaskFlag { get; set; } = false;


        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            var generalServerV2 = LobbyServerManagerV2.Instance;

            generalServerV2.Stop();
            Console.WriteLine("exit");
        }
        static async void MonitorTask(CancellationToken cancelToken = default)
        {
            ConsoleWrite.WriteMessage("MonitorTaskRun");

            while (!MonitorTaskFlag)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }


                Task.Yield();


            }


            ConsoleWrite.WriteMessage("MonitorTaskEnd");


        }


        static async void Main(string[] args)
        {

            /*
            var room = new JObject();

            room["RoomNumber"] = "001";
            room["RoomName"] = "LIVE!LIVE!LIVE!";
            room["RoomID"] = "ferett34fyh";
            room["GameMode"] = "tdm";
            
            //room["RoomID"] = "";
            room["Capacity"] = 8;
            room["PlayerCount"] = 0;
           // room["RoomOptions"] = roomOptions;
            
            //room["MatchOption"] = matchOptions;


            var room2 = new JObject();

            room2["RoomNumber"] = "002";
            room2["RoomName"] = "LIVE!LIVE!LIVE!";


            var jArray = new JArray();
            jArray.Add(room);
            jArray.Add(room2);

            var json = new JObject();

            json["Rooms"] = jArray;

            */
            //Console.Write(json.ToString());

            var batchService = new ServerBatchService();

            batchService.OnStart();

            Thread.CurrentThread.Name = "MainServerThread";


            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            string mutexName = "Global\\OpenGSServer";

            bool hasHandle = false;

            var mutex = new Mutex(true, mutexName, out hasHandle);

            //bool japanese = true;


            ServerManager.Instance.LoadSetting();

            //ServerManager.GetInstance().SaveSetting();


            var insta=EncryptManager.Instance;


            if (args.Length > 0)
            {


            }
            else
            {

                //Console.WriteLine("No Commandline aragment");
            }










            if (hasHandle)
            {
                
                var cts = new CancellationTokenSource();

                try
                {


                    var memoryMB = Process.GetCurrentProcess().MaxWorkingSet / 1024;


                    //ConsoleWrite.WriteMessage("CPU"+System.Environment.,ConsoleColor.DarkYellow);
                    ConsoleWrite.WriteMessage("[SYS]OpenGS Server", ConsoleColor.Red);
                    ConsoleWrite.WriteMessage($"[ENV]CPU Archtecture:{Cpu.ArchtectureName()}", ConsoleColor.DarkYellow);
                    ConsoleWrite.WriteMessage("[ENV]Core Count:" + System.Environment.ProcessorCount, ConsoleColor.DarkYellow);
                    ConsoleWrite.WriteMessage("[ENV]Memory:" + memoryMB + "(MB)", ConsoleColor.DarkYellow);

                    ConsoleWrite.WriteMessage("[ENV]OS:" + System.Runtime.InteropServices.RuntimeInformation.OSDescription, ConsoleColor.DarkYellow);
                    ConsoleWrite.WriteMessage("[ENV].Net core version:" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, ConsoleColor.DarkYellow);

                    ConsoleWrite.WriteMessage("[ENV]OpenGS Server Version:" + System.Environment.Version, ConsoleColor.DarkYellow);
                    ConsoleWrite.WriteMessage("[ENV] Process ID: " + System.Diagnostics.Process.GetCurrentProcess().Id, ConsoleColor.DarkYellow);
                    ConsoleWrite.WriteMessage("[ENV] Thread Count: " + System.Diagnostics.Process.GetCurrentProcess().Threads.Count, ConsoleColor.DarkYellow);
                    //ConsoleWrite.WriteMessage("[ENV] PATH: " + Environment.GetEnvironmentVariable("PATH"), ConsoleColor.DarkYellow);
                    ConsoleWrite.WriteMessage("[INFO]Initializing ....OpenGS game server", ConsoleColor.Green);
                    var accountDatabaseManager = AccountDatabaseManager.GetInstance();

                    accountDatabaseManager.Connect();


                    var builder = new ContainerBuilder();
                    builder.RegisterType<LobbyServerManagerV2>().AsSelf().SingleInstance();

                    builder.RegisterType<MatchRUdpServerManager>().AsSelf().SingleInstance();
                    builder.RegisterType<ManagementServer>().AsSelf().SingleInstance();
                    builder.RegisterType<AccountEventHandler>().As<IAccountEventHandler>().SingleInstance();
                    //builder.RegisterType<AccountManager>().AsSelf().SingleInstance();
                    


                    var container = builder.Build();



                    var lobbyServerV2 = container.Resolve<LobbyServerManagerV2>();

                    lobbyServerV2.Listen(60000);

                    var matchRUdpServer = container.Resolve<MatchRUdpServerManager>();

                    matchRUdpServer.Listen(63000);


                    var managementServer = ManagementServer.Instance;

                    managementServer.Listen(50020);

                    if(lobbyServerV2.IsStarted())
                    {
                        batchService.WriteLocalPortToFile(lobbyServerV2.Port());
                    }
                    


                    int workMin;
                    int ioMin;
                    ThreadPool.GetMinThreads(out workMin, out ioMin);

                    Console.WriteLine("MinThreads work={0}, i/o={1}", workMin, ioMin);
                    ConsoleWrite.WriteMessage("System all green...", ConsoleColor.Green);

                    ThreadPool.SetMinThreads(26, ioMin);

                    //var monitorTask = Task.Run(() => MonitorTask(cts.Token), cts.Token).ConfigureAwait(false);




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
                                //var info = WaitRoomManager.Instance().RoomManagerInfo();


                                //ConsoleWrite.WriteMessage(info.ToString());
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

                                    accountManager.CreateNewAccount(id, pass, displayName);

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
                                    ConsoleWrite.WriteMessage("Add new wait room");
                                    var roomName = param[0];


                                    var roomManager = WaitRoomManager.Instance();

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
                                    await Task.Delay(1000);
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


                                //generalServerV2.Stop();



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
                
                

                    //generalServerV2.Stop();
                   //matchRUdpServer.Stop();
                    //managementServer.Stop();

                    MonitorTaskFlag = true;
                    //monitorTask.Wait();

                    
                }
                finally
                {
                    ConsoleWrite.WriteMessage("", ConsoleColor.Red);

                    cts.Dispose();
                    
                }

            }
            else
            {
                mutex.ReleaseMutex();
                mutex.Close();

            }

            
            ServerManager.Instance.SaveSetting();

            
            batchService.OnStop();



        }
        static void timerCB(object obj)
        {
            var msec = DateTime.Now.Millisecond;
            Console.WriteLine("msec: {0}", msec);
        }
    }


}
