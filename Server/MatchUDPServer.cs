
using LiteNetLib;


namespace OpenGSServer
{
    public class MatchUDPServer
    {
        private string connectionString = "OpenGS";

        private NetManager server;
        EventBasedNetListener listener = new EventBasedNetListener();
        //private NetManager server ;
        
        //NetListener netListener;

        public void Listen(int port)
        {
            ConsoleWrite.WriteMessage("MatchRUDPServer");


             server = new NetManager(listener);

             server.Start(port);

             listener.ConnectionRequestEvent += OnUserConnected;

             listener.PeerDisconnectedEvent += OnUserDisConnected;





             //netManager = new NetManager(server);
        }

        void OnUserConnected(ConnectionRequest request)
        {
            request.AcceptIfKey("OpenGS");
        }

        void OnUserDisConnected(NetPeer peer,DisconnectInfo info)
        {

        }

        public void Shutdown()
        {
            listener.ConnectionRequestEvent -= OnUserConnected;
            listener.PeerDisconnectedEvent -= OnUserDisConnected;

            server.Stop();

        }

        public void PollingEvent()
        {
            if (server.IsRunning)
            {
                server.PollEvents();
            }

        }



    }
}
