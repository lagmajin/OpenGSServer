using NetCoreServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using System.Timers;

namespace OpenGSServer
{
  
    public class MatchTCPServer : TcpServer
    {

        public MatchTCPServer(IPAddress address, int port) : base(address, port)
        {
            Console.WriteLine("Match Server initialized..." + "Port:" + port);
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
    public class MatchServerV2 : IDisposable
    {
        private static MatchServerV2 _singleInstance=new MatchServerV2();

        private MatchTCPServer server;

        //private readonly int tickrate=100;

        //private System.Timers.Timer timer = new System.Timers.Timer(100);

        private TickTimer timer;

        private Stopwatch sw;
        public MatchServerV2()
        {
            timer = new TickTimer(ServerCallback, 40);

            timer.Start();

            sw = new Stopwatch();



            var task = Task.Run(() =>
            {


            });

        }
        private void ServerCallback(object obj)
        {
            var matchRoomManager = GameRoomManager.GetInstance();

            var allRoom=matchRoomManager.AllRooms();

            /*
      
            foreach(var room in allRoom)
            {

                room.GameUpdate();
            }
            */
            //var msec = DateTime.Now.Millisecond;
            //Console.WriteLine("msec: {0}", msec);


        }

        private void MCoreServerCallback(object obj)
        {
            var matchRoomManager = GameRoomManager.GetInstance();

            var allRoom = matchRoomManager.AllRooms();

            var options = new ParallelOptions();

            Parallel.ForEach(allRoom, options, calc =>
            {
                // 計算実行
                
            });

        }

        public static MatchServerV2 GetInstance()
        {
            return _singleInstance;
        }

        public void Listen(int port)
        {
            if (server == null)
            {
                server = new MatchTCPServer(IPAddress.Any, port);

                //server.Endpoint.Port;

                server.Start();
            }
        }

        public int? Port()
        {
            return server.Endpoint.Port;
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                server?.Dispose();
            }


        }
        public void Dispose()
        {

            Dispose(true);

            GC.SuppressFinalize(this);

            //throw new NotImplementedException();
        }
    }
}
