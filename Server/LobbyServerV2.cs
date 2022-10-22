﻿using System;

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NetCoreServer;




namespace OpenGSServer
{



    public class LobbyTcpServer : TcpServer
    {
        private LobbyServerManagerV2 _manager = null;




        public LobbyTcpServer(IPAddress address, int port, LobbyServerManagerV2 manager) : base(address, port)
        {

            _manager = manager;


            Console.WriteLine("LobbyServer initialized..." + "Port:" + port);
            Console.WriteLine("LobbyServerV2");
        }

        private string CreateUniqueID()
        {
            string str = Guid.NewGuid().ToString("N");



            return str;
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

    public class LobbyServerManagerV2 : IDisposable
    {
        private static LobbyServerManagerV2 _singleInstance = new LobbyServerManagerV2();
        private LobbyTcpServer? server;


        System.Timers.Timer hourTimer = new System.Timers.Timer(600000);

        public static LobbyServerManagerV2 GetInstance()
        {
            return _singleInstance;
        }

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