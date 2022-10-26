using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class WaitRoomManager
    {
        private static WaitRoomManager _singleInstance = new WaitRoomManager();

        public static WaitRoomManager GetInstance()
        {
            return _singleInstance;
        }
        public bool CreateNewRoom(in string name, int capasity = 8)
        {




            return false;
        }

        public void FindWaitRooms()
        {

        }

        public void FindWaitRoomsByGameMode()
        {

        }

    }
}
