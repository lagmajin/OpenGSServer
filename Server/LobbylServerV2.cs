using System;

using System.Net;
using System.Net.Sockets;

using NetCoreServer;




namespace OpenGSServer
{
    

    
    public class LobbyTCPServer : TcpServer
    {
        
        public LobbyTCPServer(IPAddress address, int port) : base(address, port) {
            Console.WriteLine("LobbyServer initialized..."+"Port:"+port);
        }

        protected override TcpSession CreateSession() 
        {
            Console.WriteLine("aaa");
            return new ClientSession(this);
        
        }



        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP server caught an error with code {error}");
        }

        public void SendToAllClient()
        {
           

        }
    }

    public class LobbyServerV2 : IDisposable
    {
        private static LobbyServerV2 _singleInstance = new LobbyServerV2();
        private LobbyTCPServer? server;


        System.Timers.Timer hourTimer = new System.Timers.Timer(600000);

        public static LobbyServerV2 GetInstance()
        {
            return _singleInstance;
        }

        public void Listen(int port)
        {
            if(server==null)
            {
                server = new LobbyTCPServer(IPAddress.Any, port);

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
            server.Multicast("aa");
        }

        ~LobbyServerV2()
        {
            Dispose(false);
        }

    }


}
