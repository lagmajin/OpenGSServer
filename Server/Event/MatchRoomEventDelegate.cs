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
            var currentOwnerId = dic.GetValueString("currentOwnerId");
            var newOwnerId = dic.GetValueString("newOwnerId");

            


        }

        public void QuitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var waitRoom=WaitRoomManager.Instance;
            var matchRoom=MatchRoomManager.Instance;



        }

    }
}
