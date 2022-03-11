using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenGSCore;

namespace OpenGSServer
{
    public interface IMissionRoom
    {

    }

    public class MissionRoom : AbstractGameRoom
    {
        public List<string> Players { get; set; }

        public MissionRoom(int roomNumber, in string roomOwnerID) : base(roomNumber, roomOwnerID)
        {

        }
    }
}
