
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;


namespace OpenGSServer
{

    public class MatchServer
    {
        private int max = 30;
        int port = 20000;
        string ip = "127.0.0.1";
        TcpListener server = null;

        bool working = false;

        //private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<TcpClient> clientsList = new List<TcpClient>();



        //bool isRunning_ = false;

        //private static MatchServer _singleInstance = new MatchServer();


        private MatchRoomManager matchManager = new MatchRoomManager();

        internal MatchRoomManager MatchManager { get => matchManager; }

        /*public static MatchServer GetInstance()
        {
            return _singleInstance;
        }
        */

        public MatchServer()
        {

        }

        public void Update()
        {


        }

        public void Listen()
        {
            //Console.ForegroundColor = ConsoleColor.Green;
            //Console.WriteLine("MatchServerStarted...");
            //Console.ForegroundColor = ConsoleColor.White;

            ConsoleWrite.WriteMessage("MatchServer Started...", ConsoleWrite.eMessageType.Success);


            IPAddress localAddr = IPAddress.Parse(ip);
            server = new TcpListener(localAddr, port);
            server.Server.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            server.Start();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("MatchServerWaitingConnection...");
            Console.ForegroundColor = ConsoleColor.White;

            //ConsoleWrite.WriteMessage("aa");

            var task = Task.Run(() =>
            {
                pendingConnection();
            });

            var task2 = Task.Run(() =>
            {
                //recvDataFromClients();
            });

            working = true;

            //str = rs + str + lf;

        }

        private async void pendingConnection()
        {
            while (true)
            {
                try
                {
                    var client = server.AcceptTcpClient();
                    await Task.Run(() => ConnectClient(client)).ConfigureAwait(false);
                }
                catch (SocketException)
                {

                }

                await Task.Delay(1).ConfigureAwait(false);
            }
        }

        void Shutdown()
        {
            var json = new JObject();

            json["MessageType"] = "ServerClosed";





        }


        async void RecvDataSingleClient(TcpClient client)
        {

            var ns = client.GetStream();
            ns.ReadTimeout = 10000;
            ns.WriteTimeout = 10000;


            while (true)
            {
                Console.WriteLine("recv2");
                var reader = new StreamReader(ns, Encoding.UTF8);
                try
                {



                    var data = reader.ReadLine();

                    if (!(data == null))
                    {
                        var obj = JObject.Parse(data);
                        Console.WriteLine(obj.ToString());

                        var messageType = obj["MessageType"].ToString();
                        var userID = obj["UserID"].ToString();

                        Console.WriteLine(messageType);

                        if ("BurstPlayer" == messageType)
                        {

                        }

                        if ("UseInstantItem" == messageType)
                        {
                            var type = obj["ItemType"].ToString();


                        }

                        if ("Move" == messageType)
                        {


                        }



                        if ("ThrowGranade" == messageType)
                        {



                        }

                        if ("TakeFieldItem" == messageType)
                        {

                        }

                        if ("Jump" == messageType)
                        {
                            var type = obj["PlayerID"].ToString();



                        }

                        if (messageType == "Shot")
                        {
                            var type = obj["GunType"].ToString();
                            var bulletCount = obj["BulletCount"].ToString();


                        }

                        if (messageType == "SitDown")
                        {


                        }



                    }
                    await Task.Delay(1).ConfigureAwait(false);
                }
                catch (IOException e)
                {

                }
                catch (ArgumentException e)
                {

                }
                finally
                {
                    reader.Dispose();
                }
                //Thread.Sleep(1);

            }


        }

        void sendData(string ip, JObject json)
        {


        }

        void SendDataAllClients(JObject json)
        {
            foreach (var client in clientsList)
            {


                var task = Task.Run(() =>
                {
                    var ns = client.GetStream();
                    ns.ReadTimeout = 10000;
                    ns.WriteTimeout = 10000;
                    var str = json.ToString(Formatting.None);
                    var enc = System.Text.Encoding.UTF8;
                    byte[] sendBytes = enc.GetBytes(str + '\n');

                    ns.Write(sendBytes, 0, sendBytes.Length);

                });
            }


        }

        void ConnectClient(TcpClient client)
        {
            lock (clientsList)
            {
                clientsList.Add(client);
            }
            Console.WriteLine("New client connected to  MatchServer.");

            var task = Task.Run(() =>
             {
                 RecvDataSingleClient(client);
             });

        }



    }



}
