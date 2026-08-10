using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenGSCore;

#nullable enable

namespace OpenGSServer
{
    public class CreateNewWaitRoomResult
    {
        public string Message { get; set; } = string.Empty;

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
