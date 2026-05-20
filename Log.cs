using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class Log
    {
        public static void Info(string message)
        {
            ConsoleWrite.WriteMessage(message, ConsoleColor.Cyan);
        }

        public static void Warning(string message)
        {
            ConsoleWrite.WriteMessage(message, ConsoleColor.Yellow);
        }

        public static void Error(string message)
        {
            ConsoleWrite.WriteMessage(message, ConsoleColor.Red);
        }
    }
}
