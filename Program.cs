using System;
using System.Linq;
using System.Threading;

using System.Diagnostics;
using System.Threading.Tasks;
using CommandLine;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;
using Autofac;


#nullable enable

 namespace OpenGSServer
{


    class Program
    {
        private static bool IsEnd { get; set; } = false;

        // UDPサーバーマネージャーは MatchServerV2 の内部で管理されます


        static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            DisposeServers();
            Console.WriteLine("exit");
        }

        private static void DisposeServers()
        {
            try
            {
                LobbyServerManager.Instance.Dispose();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Lobby shutdown failed: {ex.Message}", ConsoleColor.Red);
            }

            try
            {
                MatchServerV2.Instance.Dispose();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Match shutdown failed: {ex.Message}", ConsoleColor.Red);
            }

            try
            {
                ManagementServer.Instance.Dispose();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Management shutdown failed: {ex.Message}", ConsoleColor.Red);
            }
        }
        static async Task<int> Main(string[] args)
        {

            var parseResult = ServerStartupOptions.CreateParser().ParseArguments<ServerStartupOptions>(args);
            if (parseResult is not Parsed<ServerStartupOptions> parsed)
            {
                Console.Error.WriteLine(ServerStartupOptions.BuildHelpText());
                return parseResult.Errors.Any(error => error is HelpRequestedError or VersionRequestedError) ? 0 : 2;
            }

            var startupOptions = parsed.Value;
            if (startupOptions.ShowVersion)
            {
                Console.WriteLine($"OpenGS Server {typeof(Program).Assembly.GetName().Version}");
                return 0;
            }

            if (!startupOptions.TryValidate(out var optionError))
            {
                Console.Error.WriteLine($"[ERR] Invalid command line: {optionError}");
                Console.Error.WriteLine(ServerStartupOptions.BuildHelpText());
                return 2;
            }

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

            // OnStart()は削除（Start()を後で呼ぶ）

            Thread.CurrentThread.Name = "MainServerThread";


            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            string mutexName = "Global\\OpenGSServer";

            bool hasHandle = false;

            var mutex = new Mutex(true, mutexName, out hasHandle);

            //bool japanese = true;


            ServerManager.Instance.LoadSetting();
            ServerManager.Instance.InitializeAdminAccounts();

            //ServerManager.GetInstance().SaveSetting();

            var insta=EncryptManager.Instance;










            if (hasHandle)
            {
                
                var cts = new CancellationTokenSource();
                ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    IsEnd = true;
                    cts.Cancel();
                };
                Console.CancelKeyPress += cancelHandler;

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
                    builder.RegisterInstance(LobbyServerManager.Instance).AsSelf().SingleInstance();
                    builder.RegisterInstance(batchService).AsSelf().SingleInstance(); // BatchServiceを登録
                    builder.RegisterType<ManagementServer>().AsSelf().SingleInstance();
                    builder.RegisterType<AccountEventHandler>().As<IAccountEventHandler>().SingleInstance();
                    //builder.RegisterType<AccountManager>().AsSelf().SingleInstance();
                    


                    var container = builder.Build();



                    var lobbyServer = container.Resolve<LobbyServerManager>();
                    lobbyServer.StartTcpServer(startupOptions.LobbyPort);

                    // 新しい MatchServerV2 (同時処理/マルチコア対応) を使用
                    var matchServer = MatchServerV2.Instance;
                    matchServer.Listen(startupOptions.MatchTcpPort, startupOptions.MatchUdpPort);
                    matchServer.EnableMultiCore();

                    var managementServer = ManagementServer.Instance;
                    managementServer.Listen(startupOptions.ManagementPort);

                    if (lobbyServer.IsTcpServerRunning && lobbyServer.TcpPort is int tcpPort)
                    {
                        batchService.WriteLocalPortToFile(tcpPort);
                    }
                    
                    // バッチサービス起動
                    batchService.Start();

                    ConsoleWrite.WriteMessage("System all green...", ConsoleColor.Green);
                    var interactiveCommandParser = new InteractiveCommandParser();

                    if (startupOptions.NoConsole)
                    {
                        ConsoleWrite.WriteMessage("[INFO] Interactive console input is disabled.", ConsoleColor.Gray);
                        try
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // Ctrl+C requests a normal shutdown through the finally block.
                        }
                    }

                    while (!IsEnd && !startupOptions.NoConsole)
                    {
                        // UDPサーバーの更新は MatchServerV2 の内部ループで自動実行されます
                        
                        var input = Console.ReadLine();


                        if (string.IsNullOrWhiteSpace(input))
                        {
                            continue;
                        }

                        var commandInput = input.Trim();

                        ConsoleWrite.WriteMessage($"[CMD] {input}", ConsoleColor.Yellow);

                        // 終了コマンド処理（特別に分離）
                        if (commandInput.Equals("exit", StringComparison.OrdinalIgnoreCase) || 
                            commandInput.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                        {
                            for (var i = 0; i < 3; i++)
                            {
                                ConsoleWrite.WriteMessage("Shutting down in 3 seconds...", ConsoleColor.Red);
                                await Task.Delay(1000);
                            }
                        IsEnd = true;
                        break;
                    }

                    // コマンドを CommandParser に委譲
                    interactiveCommandParser.Execute(commandInput);
                }
                }
                catch (Exception ex)
                {
                    // Log full exception (includes stack trace) to aid root-cause analysis
                    ConsoleWrite.WriteMessage($"[ERR] Exception: {ex.ToString()}", ConsoleColor.Red);
                }
                finally
                {
                    Console.CancelKeyPress -= cancelHandler;
                    cts.Dispose();
                    DisposeServers();
                    if (hasHandle)
                    {
                        mutex.ReleaseMutex();
                        mutex.Close();
                    }
                    
                    ServerManager.Instance.SaveSetting();
                    batchService.Stop();
                    batchService.Dispose();
                }

                return 0;
            }
            else
            {
                ConsoleWrite.WriteMessage("[ERR] Server is already running", ConsoleColor.Red);
                if (hasHandle)
                {
                    mutex.ReleaseMutex();
                    mutex.Close();
                }

                return 1;
            }
        }
    }


}
