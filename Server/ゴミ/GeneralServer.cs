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
using OpenGSCore;

namespace OpenGSServer
{
    public class GeneralServer:AbstractServer
    {
        private int max = 100;
      

        //string ip= "127.0.0.1";
        TcpListener server = null;
        TcpListener subServer = null;
        bool working = false;
        private bool useSubPort = false;

        private static readonly List<TcpClient> clientsList = new List<TcpClient>();
        private static GeneralServer _singleInstance = new GeneralServer();

        private const char lf = '\x0a'; // data element separator
        private const char rs = '\x1e'; // record separator


        public void Update()
        {


        }

       


        public static GeneralServer GetInstance()
        {
            return _singleInstance;
        }





        public override void Listen()
        {
            ConsoleWrite.WriteMessage("GeneralServerStarted....",ConsoleColor.Green);

            IPAddress localAddr = IPAddress.Parse(Ip);
            server = new TcpListener(localAddr, Port);
            server.Server.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            server.Start();

            ConsoleWrite.WriteMessage("Server IP Address:"+localAddr.ToString()+"Port:"+Port.ToString(), ConsoleColor.DarkCyan);

            var task = Task.Run(() =>
            {
                PendingConnection();
            });

            var task2 = Task.Run(() =>
            {
                //recvDataFromClients();
            });

            ConsoleWrite.WriteMessage("GeneralServerWaitingConnection...", ConsoleColor.Green);

            //str = rs + str + lf;

        }

        private async void PendingConnection()
        {
            while (true)
            {
                try
                {
                    var client = server.AcceptTcpClient();
                   await Task.Run(() => ConnectClient(client)).ConfigureAwait(false);


                }
                catch (SocketException e)
                {
                    
                }

                await Task.Delay(1).ConfigureAwait(false);
            }
        }

        void stop()
        {

        }
        

        async void RecvDataSingleClient(TcpClient client)
        {
            
            var ns = client.GetStream();
            ns.ReadTimeout = 10000;
            ns.WriteTimeout = 10000;

            float elapsedTime = 0.0f;

            var buff = new byte[1024];

            while (true)
            {
                


                if (ns.CanRead)
                {
                    Console.WriteLine("recv g");

                    var reader = new StreamReader(ns, Encoding.UTF8);
                    try
                    {



                        var data = reader.ReadLine();

                        ConsoleWrite.WriteMessage(data);



                            reader.Close();

                        

                    }
                    catch (IOException e)
                    {
                        ConsoleWrite.WriteMessage(e.ToString());

                        reader.Dispose();
                    }
                    finally
                    {
                        reader.Dispose();

                    }

                }
                else
                {


                }
                //Thread.Sleep(1);
                await Task.Delay(8).ConfigureAwait(false);
            }
            

        }

        void SendDataAllClients(JObject json)
        {
            foreach (var client in clientsList)
            {
                

                var  task = Task.Run(() =>
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

            ConsoleWrite.WriteMessage("New client connected to GeneralServer.",ConsoleColor.Blue);

            Task task = Task.Run(() =>
            {
                RecvDataSingleClient(client);
            });

        }

        private void parse(JObject o)
        {



            var messageType = o["MessageType"].ToString();
            Console.WriteLine(messageType);

            if (messageType == "ServerInfoRequest")
                {
                    var json = new JObject();

                    json["ServerInfo"] = "";

                    var str = json.ToString(Formatting.None);

                /*
                    ns.ReadTimeout = 10000;
                    ns.WriteTimeout = 10000;

                    var enc = System.Text.Encoding.UTF8;
                    byte[] sendBytes = enc.GetBytes(str + '\n');

                    ns.Write(sendBytes, 0, sendBytes.Length);
                */
                }

                if (messageType == "LoginRequest")
                {
                    var id = o["id"].ToString();
                    var pass = o["pass"].ToString();

                    var json = AccountManager.GetInstance().Login(id, pass);
                    ConsoleWrite.WriteMessage("aa");
                    ConsoleWrite.WriteMessage(json.ToString());
                    //var str = json.ToString(Formatting.None);

                /*
                    ns.ReadTimeout = 10000;
                    ns.WriteTimeout = 10000;

                    var enc = System.Text.Encoding.UTF8;
                    byte[] sendBytes = enc.GetBytes(str + '\n');

                    ns.Write(sendBytes, 0, sendBytes.Length);
                    ns.Flush();
                */
                }

                if (messageType == "LogoutRequest")
                {
                    var json = new JObject();

                    var id = o["id"].ToString();




                }

                if (messageType == "CreateNewRoomRequest")
                {
                    var roomName = o["RoomName"].ToString();
                    var roomCapacity = o["Capacity"].ToString();
                    var gameMode = o["GameMode"].ToString();

                    var name = new GameMode(gameMode);

                var serverManager = ServerManager.Instance;



                    var matchManager = serverManager.GetMatchServer().MatchManager;

                    //matchManager.CreateNewRoom(roomName, name, 8);

                    //matchManager.CreateNewRoom(roomName,)




                    //matchServer.MatchManager.createNewRoom();




                }

                if (messageType == "EnterRoomRequest")
                {
                    //var roomID = o["TargetRoomID"].ToString();

                    //var serverManager = ServerManager.GetInstance();

                    //var matchServer = serverManager.GetMatchServer();





                }

                if (messageType == "DeleteRoomRequest")
                {



                }

            if (messageType == "OutRoomRequest")
            {
                var serverManager = ServerManager.Instance;

                    //serverManager.GetGeneralServer().

                    var generalServer=serverManager.GetGeneralServer();
                }

            if (messageType == "UpdateRoomRequest")
            {
                //ConsoleWrite.WriteMessage("aaa");
                var serverManager = ServerManager.Instance;



                    serverManager.GetMatchServer();


                    var json = new JObject();

                    json["MessageType"] = "UpdateRooms";
                    json["TDM"][0] = 0;



                }

                if (messageType == "AddLobbyChat")
                {
                    var userName = o["Name"];
                    var say = o["Say"];


                }

                if (messageType == "AddRoomChat")
                {
                    var say = o["Say"];
                    var id = o["RoomID"];
                }

            if (messageType == "MatchServerRequest")
            {
                var matchServer = ServerManager.Instance;




                }

                if (messageType == "ServerTimeRequest")
                {
                    o["TimeType"] = "";

                var generalServer = ServerManager.Instance;



                }
            }

    }
}
