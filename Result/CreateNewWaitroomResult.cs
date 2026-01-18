using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenGSCore;

namespace OpenGSServer
{
    public class CreateNewWaitRoomResult
    {
        public string Message { get; set; }

        public WaitRoom? Room { get; set; }

        
        public CreateNewWaitRoomResult()
        {

        }

        public CreateNewWaitRoomResult(string message, WaitRoom? room)
        {
            Message = message;
            Room = room;
        }
    }
}
