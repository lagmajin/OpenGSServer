




using OpenGSCore;

namespace OpenGSServer
{

    public class MatchRoomFactory
    {
        public static OpenGSCore.MatchRoom CreateMatchRoom(int roomNumber, string roomName, string ownerID, AbstractMatchSetting setting, MatchRoomManager manager)
        {
            // OpenGSCore.MatchRoomEventBus ‚ðŽg—p
            var bus = new OpenGSCore.MatchRoomEventBus();
            
            var room = new OpenGSCore.MatchRoom(roomNumber, roomName, ownerID, setting, bus);
            
            return room;
        }
    }

}