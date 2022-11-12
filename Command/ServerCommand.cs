



using CommandLine;


namespace OpenGSServer
{
    class Options
    {
        [Option('a', "aaa", Required = false, HelpText = "Aの説明です。")]
        public string A { get; set; }

    }

    public class Command
    {

    }

    public static class CommandParser
    {


        public static void Parser(in string args)
        {



            var commands = args.ToLower().Split("");

            var command = commands[0];

            switch (command)
            {
                case "addplayer":
                    break;

                case "addguild":
                    break;
                case "addwaitroom":

                    break;
                case "addmissionroom":
                    break;
                case "playerinfo":
                    break;
                case "guildinfo":
                    break;
                case "lobbyinfo":
                    break;
                case "matchserverinfo":
                    break;



            }



        }
    }


    public static class CommandExecutor
    {

        //public static void 

        public static void CreatePlayer()
        {

        }
        public static void CreateGuild()
        {

        }

        public static void PlayerInfo()
        {

        }

        public static void LobbyInfo()
        {

        }

        public static void ExecHelp()
        {

        }

    }

}
