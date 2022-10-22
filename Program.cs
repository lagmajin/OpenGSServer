using LiteDB;
using System;
using System.IO;
using System.Linq;
using System.Threading;

using System.CommandLine.Parser;



namespace OpenGSServer
{
    class Program
    {
        private static bool flag = false;

        public bool Flag { get => flag; set => flag = value; }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            var generalServerV2 = LobbyServerManagerV2.GetInstance();

            generalServerV2.Stop();
            Console.WriteLine("exit");
        }
        static void Main(string[] args)
        {

            // Get a collection (or create, if doesn't exist)



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


                    var dbplayerTest = new DBPlayer("test", "test", "test");

                    var dbplayerTest2 = new DBPlayer("test2", "test2", "test2");

                    dbplayerTest.Friends = new string[] { "8000-0000", "9000-0000" };

                    var accountDatabaseManager = AccountDatabaseManager.GetInstance();

                    accountDatabaseManager.Connect();



                    accountDatabaseManager.AddNewPlayerData(dbplayerTest);
                    accountDatabaseManager.AddNewPlayerData(dbplayerTest2);



                    var generalServerV2 = LobbyServerManagerV2.GetInstance();

                    generalServerV2.Listen(50000);

                    //var matchServer = serverManager.GetMatchServer() ;

                    var matchServerV2 = MatchServerV2.GetInstance();

                    matchServerV2.Listen(50010);




                    //matchServer.Listen();


                    //var acManager = AccountManager.GetInstance();

                    //acManager.CreateNewAccountOld("test1", "aa", "test1");
                    //acManager.CreateNewAccount("test3", "", "test3");

                    //acManager.login("test1", "test1");

                    //matchServer.MatchManager.createNewRoom("", eGameMode.DM, 10);


                    while (!flag)
                    {
                        Thread.Sleep(5000);
                    }


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
