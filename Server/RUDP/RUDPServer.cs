
using LiteNetLib;
using LiteNetLib.Utils;

namespace OpenGSServer
{
    public class UDPServer
    {
        private NetManager _netManager;

        private NetPacketProcessor _packetProcessor;

        private int _serverTick;

        public void StartServer()
        {
            _packetProcessor = new NetPacketProcessor();

        }

        private void OnLogicUpdate()
        {

        }



    }
}
