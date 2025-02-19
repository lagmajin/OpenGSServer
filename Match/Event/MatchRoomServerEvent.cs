using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    internal abstract class IServerMatchEvent
    {
        public MatchRoom MatchRoom { get; }

        public IServerMatchEvent(MatchRoom room)

        {
            MatchRoom = room;

        }
    }
    internal class ServerMatchStartEvent:IServerMatchEvent
    {
        public ServerMatchStartEvent(MatchRoom room):base(room)
        {
            


        }

    }
    internal class ServerMatchEndEvent : IServerMatchEvent
    {
        public readonly MatchResult matchResult;

        public ServerMatchEndEvent(MatchResult result,MatchRoom room) : base(room)
        {
            matchResult = result;
            

        }
    }




}
