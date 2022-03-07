using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleTCP;

namespace OpenGSServer.Server
{
    public class OldServer2
    {
        SimpleTcpServer server;
        const byte NULL_TERMINATED = 0x00;
        const int PORT = 55555;
        private static OldServer2 _singleInstance = new OldServer2();
        public static OldServer2 GetInstance()
        {
            return _singleInstance;
        }

        public void Start()
        {
            server = new SimpleTcpServer();

            server.Start(30000);
            server.Delimiter = NULL_TERMINATED;

            server.DelimiterDataReceived += (sender, msg) =>
            {
                //msg.ReplyLine(msg.MessageString + "_Reply");
                //textBox1.Text += "Receive::" + msg.MessageString;
            };

            }
    }
}
