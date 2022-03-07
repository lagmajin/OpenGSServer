using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class ManagementServer:AbstractServer
    {
        //private static ManagementServer _singleInstance = new ManagementServer();

        //int max = 2;
        //int port = 45454;
        //string ip = "127.0.0.1";

        bool working = false;

        public ManagementServer():base(true)
        {


        }

        public override void Listen()
        {


            ConsoleWrite.WriteMessage("Management server started...",ConsoleColor.Green);
            ConsoleWrite.WriteMessage("Management server waiting connection...", ConsoleColor.Green);

            working = true;


        }

        protected override void Update()
        {

        }
        public override void Shutdown()
        {


        }


        

    }





}
