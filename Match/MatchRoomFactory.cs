

using OpenGSCore;

namespace OpenGSServer
{

    public class MatchRoomFactory
    {
        public static MatchRoom CreateMatchRoom(int roomNumber, string roomName, string ownerID, AbstractMatchSetting setting,MatchRoomManager manager)
        {

            
            var bus = new MatchRoomEventBus();
            
            
         
            var room= new MatchRoom(roomNumber, roomName, ownerID,setting,bus);
            
            bus.setMatchRoom(room);
            bus.setMatchRoomManager(manager);
            
            return room;
        }
        
        
    }



}