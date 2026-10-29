using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Net.Sockets;

namespace OpenGSServer.Network
{
    class UDPReceiver : IDisposable
    {
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
