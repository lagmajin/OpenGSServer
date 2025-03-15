using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;
using OpenGSServer;

namespace OpenGSServer
{
    public partial class MatchRUdpServer
    {
        private EventBasedNetListener listener = new();

        private NetManager server = null;

        
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        public MatchRUdpServer()
        {

        }
        public void Listen(int port)
        {
            

            listener.NtpResponseEvent += packet =>
            {
                
            };

            listener.ConnectionRequestEvent += OnConnectionRequested;

            listener.PeerConnectedEvent += OnConnect;

            listener.PeerDisconnectedEvent += OnDisConnected;
            listener.NetworkReceiveEvent += OnNetworkReceive;


            server = new NetManager(listener);

            server.Start(port);

            Task.Run(() => ServerUpdateTask(tokenSource.Token));

            ConsoleWrite.WriteMessage("[INFO]Match Server(RUDP) Started on Port:"+port.ToString());

        }

        private async Task ServerUpdateTask(CancellationToken token)
        {

            Thread.CurrentThread.Name = "ServerUpdateThread";
            while (true)
            {


                await Task.Delay(100);

            }


        }

        public void Stop()
        {
            tokenSource.Cancel();


        }

        public void SendMessageAllPlayer(JObject json)
        {

        }

        public async Task SendMessageAllPlayer()
        {


        }

        public async Task SendMessageAllPlayer(object obj)
        {

        }

        public void SendMessageToPlayer(in string id,JObject json)
        {
            if(players.TryGetValue(id, out var player))
            {
                var writer = new NetDataWriter();
                writer.Put(json.ToString()); // JSON を文字列化して書き込む

                player.Send(writer, DeliveryMethod.ReliableOrdered);
            }

        }

        public async Task SendMessageToPlayerAsync(string id)
        {

        }

        public void PolingEvent()
        {
            if (server!=null)
            {
                server.PollEvents();
            }
        }

        ~MatchRUdpServer()
        {

        }
    }

    }




    public class MatchRUdpServerManager:IServerHost
    {
        public static MatchRUdpServerManager Instance { get; } = new();

        public MatchRUdpServer server = new();

        private CancellationTokenSource source=new();

        private Task eventTask;
        public MatchRUdpServerManager()
        {

        }

        public void Listen(int port)
        {
        var token = source.Token;

        // 非同期タスク内でスレッド名を設定し、EventTaskを非同期に実行
        eventTask = Task.Run(async () =>
        {
            Thread.CurrentThread.Name = "EventTaskThread";  // スレッドに名前を付ける
            await EventTask(token);  // 非同期タスクの実行
        }, token);

        // Console.WriteLineなどを使ってメッセージを表示
        Console.WriteLine("Listening on port: " + port);

        // サーバーを起動
        server.Listen(port);
    }

        public void Stop()
        {
            source.Cancel();



            server.Stop();

            eventTask.Wait();
        }

        private async Task EventTask(CancellationToken token)
        {
            ConsoleWrite.WriteMessage("[INFO]Start RUdp Event Task",ConsoleColor.DarkGreen);
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                server.PolingEvent();
                

                await Task.Delay(1);
            }

            ConsoleWrite.WriteMessage("[INFO]End RUdp Event Task", ConsoleColor.Cyan);
        }

    }


