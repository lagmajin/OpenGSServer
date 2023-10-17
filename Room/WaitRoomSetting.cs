using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class WaitRoomSetting
    {
        public string RoomName { get; set; } = "";
        public int Capacity { get; set; } = 8;

        public WaitRoomSetting()
        {
        }
    }
}
