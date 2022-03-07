using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Net.Sockets;

namespace OpenGSServer.Network
{
    class UDPReceiver : IDisposable
    {
        string localIpString = "127.0.0.1";

        System.Text.Encoding enc = System.Text.Encoding.UTF8;


        int port = 66666;
        UdpClient udp = new UdpClient(1234);

        UDPReceiver()
        {

        }

        void listen()
        {


        }



        public void Dispose()
        {
            udp.Close();
        }
    }
}
