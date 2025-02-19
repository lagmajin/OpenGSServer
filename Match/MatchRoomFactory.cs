

using OpenGSCore;

namespace OpenGSServer
{

    public class MatchRoomFactory
    {
        public static MatchRoom CreateMatchRoom(int roomNumber, string roomName, string ownerID, AbstractMatchSetting setting)
        {

            
            var bus = new MatchRoomEventBus();
            
         
            return new MatchRoom(roomNumber, roomName, ownerID,setting,bus);
        }
        
        
    }



}