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
        public new List<string> Players { get; set; } = new();

        public MissionRoom(int roomNumber, in string roomOwnerID) : base(roomNumber, roomOwnerID)
        {

        }

        /// <summary>
        /// A mission room has no scene of its own to step, so a tick of it is
        /// nothing.
        /// <para>
        /// It has to be said here rather than left to an empty base. The server
        /// loop ticks every room it holds, and a room that silently does nothing
        /// looks the same as one that is running.
        /// </para>
        /// </summary>
        public override void GameUpdate()
        {
        }
    }
}
