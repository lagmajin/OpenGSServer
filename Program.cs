using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

#nullable enable

namespace OpenGSServer
{
    class Program
    {
        private static bool IsEnd { get; set; } = false;

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

            var host = new ServerHost(startupOptions);
            if (!host.TryAcquireInstanceLock())
            {
                ConsoleWrite.WriteMessage("[ERR] Server is already running", ConsoleColor.Red);
                host.ReleaseInstanceLock();
                return 1;
            }

            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                IsEnd = true;
                cts.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
                StartupBanner.Print();
                host.Start();

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

                await RunInteractiveLoopAsync(startupOptions, cts.Token);
            }
            catch (Exception ex)
            {
                // Log full exception (includes stack trace) to aid root-cause analysis
                ConsoleWrite.WriteMessage($"[ERR] Exception: {ex.ToString()}", ConsoleColor.Red);
                return 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
                host.Shutdown();
            }

            return 0;
        }

        private static async Task RunInteractiveLoopAsync(ServerStartupOptions options, CancellationToken token)
        {
            var commandParser = new InteractiveCommandParser();

            while (!IsEnd && !options.NoConsole && !token.IsCancellationRequested)
            {
                var input = await Console.In.ReadLineAsync().ConfigureAwait(false);
                if (input is null)
                {
                    // Standard input was closed; there is nothing left to read.
                    return;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                var commandInput = input.Trim();
                ConsoleWrite.WriteMessage($"[CMD] {input}", ConsoleColor.Yellow);

                if (commandInput.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    commandInput.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    for (var i = 0; i < 3; i++)
                    {
                        ConsoleWrite.WriteMessage("Shutting down in 3 seconds...", ConsoleColor.Red);
                        await Task.Delay(1000).ConfigureAwait(false);
                    }

                    IsEnd = true;
                    return;
                }

                // コマンドを InteractiveCommandParser に委譲
                commandParser.Execute(commandInput);
            }
        }
    }
}
