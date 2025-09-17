using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class ConsoleWrite
    {
        private static object _MessageLock = new object();

        public enum eMessageType:int
        {
            Success,
            Info,
            Warning,
            Error,

        }

        public static void setLanguage()
        {


        }

        public static void WriteMessage(string message, 
            System.ConsoleColor color = ConsoleColor.White, 
            System.ConsoleColor bg = ConsoleColor.Black,
            bool newLine = true) // newLine引数を追加
        {
            lock (_MessageLock)
            {
                Console.ForegroundColor = color;
                Console.BackgroundColor = bg;

                if (newLine)
                {
                    Console.WriteLine(message); // 改行あり
                }
                else
                {
                    Console.Write(message); // 改行なし
                }

                Console.ResetColor();
            }
        }
        public static void WriteMessage(string message,eMessageType type)
        {
            System.ConsoleColor color=ConsoleColor.White;


            if(type==eMessageType.Warning)
            {
                color = ConsoleColor.Red;

            }

            if(type==eMessageType.Success)
            {
                color = ConsoleColor.Green;

            }

            if (type == eMessageType.Info)
            {

                color = ConsoleColor.Cyan;
            }

            if (type == eMessageType.Error)
            {


            }

            lock (_MessageLock)
            {
                Console.ForegroundColor = color;
                Console.BackgroundColor = ConsoleColor.Black;
                Console.WriteLine(message);
                Console.ResetColor();
            }

        }


    }




}
