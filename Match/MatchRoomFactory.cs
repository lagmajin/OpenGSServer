using OpenGSCore;

namespace OpenGSServer
{
    public class MatchRoomFactory
    {
        public static OpenGSCore.MatchRoom CreateMatchRoom(int roomNumber, in string roomName, in string ownerID, AbstractMatchSetting setting, MatchRoomEventBus bus)
        {
            var room = new OpenGSCore.MatchRoom(roomNumber, roomName, ownerID, setting, bus);
            return room;
        }

        // 互換性のため、managerを引数に取るオーバーロードも残す
        public static OpenGSCore.MatchRoom CreateMatchRoom(int roomNumber, in string roomName, in string ownerID, AbstractMatchSetting setting, MatchRoomManager manager)
        {
            var bus = new MatchRoomEventBus();
            var room = new OpenGSCore.MatchRoom(roomNumber, roomName, ownerID, setting, bus);
            return room;
        }
    }
}
