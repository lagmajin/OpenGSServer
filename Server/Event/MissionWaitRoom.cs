using OpenGSCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class MissionWaitRoom
    {
        private int capacity = 8;
        private string roomName_ = "";

        //private List<PlayerInfo>

        public int Capacity()
        {
            return capacity;
        }

        public bool IsAllReady()
        {
            return false;
        }

        public string roomName()
        {
            return roomName_;
        }

    }
}
