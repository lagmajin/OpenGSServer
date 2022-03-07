using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace OpenGSServer
{


    public class MatchRoomEventDelegate
    {



        public void MatchRoomUpdateRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            


        }

        public void ChangeRoomOwnerRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {


        }

        public void ChangeRoomOwnerRequest()
        {

        }

        public void QuitRoom()
        {

        }

    }
}
