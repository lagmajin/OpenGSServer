﻿using System;
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

        private Dictionary<string, NetPeer> players = new();
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



            server = new NetManager(listener);

            server.Start(port);

            Task.Run(() => ServerUpdateTask(tokenSource.Token));

            ConsoleWrite.WriteMessage("Match Server Online(RUDP)"+port.ToString());

        }

        private async void ServerUpdateTask(CancellationToken token)
        {
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




    public class MatchRUdpServerManager
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

            eventTask=Task.Run(() => EventTask(token), token);

            //ConsoleWrite("")


            server.Listen(port);
        }

        public void Stop()
        {
            source.Cancel();



            server.Stop();

            eventTask.Wait();
        }

        private async void EventTask(CancellationToken token)
        {
            ConsoleWrite.WriteMessage("Start RUdp Event Task",ConsoleColor.DarkGreen);
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                server.PolingEvent();
                

                await Task.Delay(1);
            }

            ConsoleWrite.WriteMessage("End RUdp Event Task",ConsoleColor.Cyan);
        }

    }


