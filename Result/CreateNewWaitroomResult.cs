using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class CreateNewWaitRoomResult
    {
        private string message = "";

        private WaitRoom? room = null;

        
        public CreateNewWaitRoomResult()
        {

        }

        public CreateNewWaitRoomResult(string message, WaitRoom? room)
        {
            this.message = message;
            this.room = room;
        }
    }
}
