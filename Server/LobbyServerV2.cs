using System;

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NetCoreServer;




namespace OpenGSServer
{



    public class LobbyTcpServer : TcpServer
    {
        private LobbyServerManagerV2 _manager = null;

        //private Lobby lobby=new();


        public LobbyTcpServer(IPAddress address, int port, LobbyServerManagerV2 manager) : base(address, port)
        {

            _manager = manager;


            ConsoleWrite.WriteMessage("[INFO]LobbyServer initialized..." + "Port:" + port, ConsoleColor.Green);

            
  
        }

        private string CreateUniqueID()
        {
            string str = Guid.NewGuid().ToString("N");



            return str;
        }

        
        protected override Socket CreateSocket()
        {
            var socket = base.CreateSocket();

            //socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true );;

            return socket;
        }
        

        protected override TcpSession CreateSession()
        {
            //Console.WriteLine("aaa");
            return new ClientSession(this);

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
            Console.WriteLine($"Chat TCP server caught an error with code {error}");
        }

        public void SendToAllClient()
        {


        }
    }

    public class LobbyServerManagerV2 : IDisposable,IServerHost
    {
        public static LobbyServerManagerV2 Instance { get; } = new();
        private LobbyTcpServer? server;


        System.Timers.Timer hourTimer = new System.Timers.Timer(600000);



        public void Listen(int port)
        {
            if (server == null)
            {
                server = new LobbyTcpServer(IPAddress.Any, port, this);

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

                hourTimer?.Dispose();
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

        ~LobbyServerManagerV2()
        {
            Dispose(false);
        }

    }


}
