using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#nullable enable

namespace OpenGSServer
{
    /// <summary>
    /// Console output that runs once during startup. Kept separate from
    /// <see cref="ServerHost"/> so the startup log can be removed or redirected
    /// without touching the bootstrap sequence.
    /// </summary>
    internal static class StartupBanner
    {
        public static void Print()
        {
            PrintAsciiArt();
            PrintEnvironment();
        }

        private static void PrintAsciiArt()
        {
            // Unicode表示を有効化（Windows対応）
            Console.OutputEncoding = System.Text.Encoding.UTF8;

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

            ConsoleWrite.WriteMessage("[SYS]OpenGS Server", ConsoleColor.Red);
        }

        private static void PrintEnvironment()
        {
            using var process = Process.GetCurrentProcess();
            var memoryMB = process.MaxWorkingSet / 1024;

            ConsoleWrite.WriteMessage($"[ENV]CPU Archtecture:{Cpu.ArchitectureName()}", ConsoleColor.DarkYellow);
            ConsoleWrite.WriteMessage("[ENV]Core Count:" + System.Environment.ProcessorCount, ConsoleColor.DarkYellow);
            ConsoleWrite.WriteMessage("[ENV]Memory:" + memoryMB + "(MB)", ConsoleColor.DarkYellow);
            ConsoleWrite.WriteMessage("[ENV]OS:" + RuntimeInformation.OSDescription, ConsoleColor.DarkYellow);
            ConsoleWrite.WriteMessage("[ENV].Net core version:" + RuntimeInformation.FrameworkDescription, ConsoleColor.DarkYellow);
            ConsoleWrite.WriteMessage("[ENV]OpenGS Server Version:" + System.Environment.Version, ConsoleColor.DarkYellow);
            ConsoleWrite.WriteMessage("[ENV] Process ID: " + process.Id, ConsoleColor.DarkYellow);
            ConsoleWrite.WriteMessage("[ENV] Thread Count: " + process.Threads.Count, ConsoleColor.DarkYellow);
            ConsoleWrite.WriteMessage("[INFO]Initializing ....OpenGS game server", ConsoleColor.Green);
        }
    }
}
