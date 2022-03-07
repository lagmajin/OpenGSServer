using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    class WaitRoomManager
    {
        private static WaitRoomManager _singleInstance = new WaitRoomManager();

        public static WaitRoomManager GetInstance()
        {
            return _singleInstance;
        }
        public bool createNewRoom(String name,int capasity=8)
        {

            return false;
        }
      
    }
}
