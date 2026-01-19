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

        // UDPサーバーマネージャー
        private static MatchRUdpServerManager? udpServerManager;


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


        static async Task Main(string[] args)
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

                    // Unicode表示を有効化（Windows対応）
                    Console.OutputEncoding = System.Text.Encoding.UTF8;

                    // ASCII Art Banner
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(@"    ╔═══════════════════════════════════════════════════════════════╗");
                    Console.WriteLine(@"    ║   ██████╗ ██████╗ ███████╗███╗   ██╗ ██████╗ ███████╗         ║");
                    Console.WriteLine(@"    ║  ██╔═══██╗██╔══██╗██╔════╝████╗  ██║██╔════╝ ██╔════╝         ║");
                    Console.WriteLine(@"    ║  ██║   ██║██████╔╝█████╗  ██╔██╗ ██║██║  ███╗███████╗         ║");
                    Console.WriteLine(@"    ║  ██║   ██║██╔═══╝ ██╔══╝  ██║╚██╗██║██║   ██║╚════██║         ║");
                    Console.WriteLine(@"    ║  ╚██████╔╝██║     ███████╗██║ ╚████║╚██████╔╝███████║         ║");
                    Console.WriteLine(@"    ║   ╚═════╝ ╚═╝     ╚══════╝╚═╝  ╚═══╝ ╚═════╝ ╚══════╝         ║");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(@"    ║                                                               ║");
                    Console.WriteLine(@"    ║                   - Game Server Edition -                     ║");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(@"    ╚═══════════════════════════════════════════════════════════════╝");
                    Console.ResetColor();
                    Console.WriteLine();

                    //ConsoleWrite.WriteMessage("CPU"+System.Environment.,ConsoleColor.DarkYellow);
                    ConsoleWrite.WriteMessage("[SYS]OpenGS Server", ConsoleColor.Red);
                    ConsoleWrite.WriteMessage($"[ENV]CPU Archtecture:{Cpu.ArchitectureName()}", ConsoleColor.DarkYellow);
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

                    // UDPゲームサーバーの初期化
                    udpServerManager = MatchRUdpServerManager.Instance;
                    ConsoleWrite.WriteMessage("[UDP]Game Server initialized", ConsoleColor.Cyan);

                    //var monitorTask = Task.Run(() => MonitorTask(cts.Token), cts.Token).ConfigureAwait(false);




                    while (!IsEnd)
                    {
                        // UDPサーバーの更新
                        udpServerManager.Update();

                        var input = Console.ReadLine();

                        if (string.IsNullOrWhiteSpace(input))
                        {
                            continue;
                        }

                        ConsoleWrite.WriteMessage($"[CMD] {input}", ConsoleColor.Yellow);

                        // 終了コマンド処理（特別に分離）
                        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) || 
                            input.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                        {
                            for (var i = 0; i < 3; i++)
                            {
                                ConsoleWrite.WriteMessage("Shutting down in 3 seconds...", ConsoleColor.Red);
                                await Task.Delay(1000);
                            }
                            IsEnd = true;
                            Environment.Exit(0);
                        }

                        // コマンドを CommandParser に委譲
                        CommandParser.Parse(input);
                    }
                }
                catch (Exception ex)
                {
                    ConsoleWrite.WriteMessage($"[ERR] Exception: {ex.Message}", ConsoleColor.Red);
                }
                finally
                {
                    if (hasHandle)
                    {
                        mutex.ReleaseMutex();
                        mutex.Close();
                    }
                    
                    ServerManager.Instance.SaveSetting();
                    batchService.OnStop();

                    // UDPサーバーのシャットダウン
                    udpServerManager.Shutdown();
                }
            }
            else
            {
                ConsoleWrite.WriteMessage("[ERR] Server is already running", ConsoleColor.Red);
                if (hasHandle)
                {
                    mutex.ReleaseMutex();
                    mutex.Close();
                }
            }
        }
    }


}
