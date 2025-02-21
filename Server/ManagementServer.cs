using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using NetCoreServer;

namespace OpenGSServer
{  public class ManagementTcpServer : TcpServer
    {
        private int serverFrameCount;



        public ManagementTcpServer(IPAddress address, int port,ManagementServer manager) : base(address, port)
        {
            ConsoleWrite.WriteMessage("[INFO]Management Server initialized..." + "Port:" + port, ConsoleColor.Green);
        }

        protected override TcpSession CreateSession()
        {
           
            return new ManagementServerSession(this);

        }

        protected override void OnConnected(TcpSession session)
        {
            //[System.Runtime.CompilerServices.CallerFilePath] string path

            ClientSession clientSession = session as ClientSession;


            var ip = clientSession.ClientIpAddress();




            Console.WriteLine(MethodBase.GetCurrentMethod().Name + "()");


        }

        protected override void OnError(SocketError error)
        {
            
        }

        public void SendToAllClient()
        {


        }



    }
    interface IManagementServer
    {


    }

    public class ManagementServer:AbstractServer
    {
        private ManagementTcpServer server = null;

        public static ManagementServer Instance { get; } = new();

        bool working = false;

        public ManagementServer():base(false)
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

        public void Listen(int port)
        {
            if (server == null)
            {
                server = new ManagementTcpServer(IPAddress.Any, port, this);

                server.Start();
            }
        }

        public void Stop()
        {
            server?.Stop();

            server?.Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                server?.Dispose();

                //hourTimer?.Dispose();
            }


        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);

        }

        public void MultiCast()
        {
            server?.Multicast("aa");
        }

        ~ManagementServer()
        {
            Dispose(false);
        }
        

    }





}
