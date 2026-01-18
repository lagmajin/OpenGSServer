using System;
using System.Collections.Generic;
using System.Threading;
using OpenGSCore;


namespace OpenGSServer
{
    public enum EMatchRoomEventType
    {
        Started,
        Ended,

    }

    public struct MatchResult
    {
        public EMatchRoomEventType type;
        public MatchRoom room;
    }

    public class MatchRoomManager:IMatchSubscriber
    {

    private Dictionary<string, MatchRoom> matchRooms = new Dictionary<string, MatchRoom>();
    private readonly object matchRoomsLock = new();

    private int roomNumberCount = 0;

    //public readonly MatchRoomEventBus roomEventBus = new MatchRoomEventBus();

    public readonly Dictionary<string,MatchRoomEventBus> roomEventBuses = new Dictionary<string,MatchRoomEventBus>();

    public static MatchRoomManager Instance { get; } = new();


    private void IncreaseRoomCounter()
    {
        Interlocked.Increment(ref roomNumberCount);
    }

    public MatchRoomManager()
    {

    }

    public void CreateMatchRoomByWaitRoom(WaitRoom waitRoom)
    {
        waitRoom.MatchRoomLink = null;

        //var newMatchRoom = new MatchRoom(roomNumberCount,"Live!Live!Live!","",);


        IncreaseRoomCounter();

    }

    public List<AbstractGameRoom> AllRooms()
    {
        lock (matchRoomsLock)
        {
            return new List<AbstractGameRoom>(matchRooms.Values);
        }
    }

    public CreateNewRoomResult CreateNewRoom(in string roomName, in string ownerID,AbstractMatchSetting setting)
    {
        var room=MatchRoomFactory.CreateMatchRoom(0,roomName, ownerID,setting,this);

        lock (matchRoomsLock)
        {
            matchRooms.Add(room.Id, room);
        }

        var createNewRoomResult =
            new CreateNewRoomResult(ECreateNewRoomResult.Successful, ECreateNewRoomReason.NoReason);

        return createNewRoomResult;
    }

    public CreateNewRoomResult CreateNewDeathMatchRoom(in string roomName, in string ownerID, int capacity = 10,
        bool teamBalance = true)
    {
        var matchSetting = new DeathMatchSetting(10);

        var matchRule = new DeathMatchRule();


        var matchRoomInstance=    MatchRoomManager.Instance;


        var bus = new MatchRoomEventBus();

        //var matchRoom = new MatchRoom(roomNumberCount, roomName, ownerID, matchRule,bus);

        //matchRooms.Add(matchRoom.Id, matchRoom);

        //IncreaseRoomCounter();


        var createNewRoomResult =
            new CreateNewRoomResult(ECreateNewRoomResult.Successful, ECreateNewRoomReason.NoReason);

        return createNewRoomResult;
    }

    public void CreateNewTeamDeathMatchRoom(in string roomName, in string ownerID, int capacity = 10,
        bool teamBalance = true)
    {
        var tdmMatchSetting = new TDMMatchSetting();

        //var matchRule = new TDMMatchRule();
        IncreaseRoomCounter();

        var matchRoomInstance = MatchRoomManager.Instance;
        //var bus = new MatchRoomEventBus();
        //var matchRoom = new MatchRoom(0, roomName, ownerID, matchRule, bus);

        //matchRoom.Subscribe(this);



       // matchRooms.Add(matchRoom.Id, matchRoom);

        //var tdmMatchRule = new TDMMatchRule();

        //var matchSetting = new TDMMatchSetting(capacity,);
    }


    public void CreateNewSuvRoom(in string roomName, in string OwnerID, int capacity = 10)
    {
        //var suvMatchRule = new SuvMatchRule();
        var matchSetting = new SuvMatchSetting(capacity, true);

        //var matchRoom = new MatchRoom(OwnerID, matchSetting);



    }

    public void CreateNewTSuvRoom(in string roomName)
    {

        var matchSetting = new TSuvMatchRule();


        //var matchRoom = new MatchRoom(0, "", "",);


    }

    public void CreateNewCTFMatchRoom(string roomName, int capacity = 10)
    {

    }


    public void CreateNewMissionRoom(in string roomName, in string ownerID, int capacity = 3)
    {
        //var missionRoom = new MissionRoom();



    }

    public EnterMatchRoomResult EnterRoom()
    {
        return null;
    }

    public EnterMatchRoomResult EnterRoom(in Guid id)
    {
        id.ToString();

        bool succeeded = false;

        string message = "";

        lock (matchRoomsLock)
        {
            if (matchRooms.TryGetValue(id.ToString(), out var room))
            {
                //var player
                //room.AddNewPlayer()
                succeeded = true;
            }
            else
            {
                message = "No room";
            }
        }

        var result = new EnterMatchRoomResult(succeeded, message);

        return result;
    }

    public void ExitRoom(string userid)
    {


    }

    public MatchRoom? SearchRoomByMemberID(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        lock (matchRoomsLock)
        {
            foreach (var room in matchRooms.Values)
            {
                if (room.Players.Exists(p => p.Id == id))
                {
                    return room;
                }
            }
        }

        return null;
    }

    public bool StartMatch(in string id)
    {
        string message = "";

        lock (matchRoomsLock)
        {
            if (matchRooms.TryGetValue(id, out var room))
            {
                room.StartLoading();
                return true;
            }
        }



        return false;
    }

    public bool StartMatchTest()
    {
        lock (matchRoomsLock)
        {
            foreach (var m in matchRooms)
            {
                
                //m.Value.GameStart();
            }
        }




        return false;
    }

    public bool StartAllMatch()
    {
        return false; 
    }

    public void UpdateAllRoom()
    {


    }

    public int RoomCount()
    {
        lock (matchRoomsLock)
        {
            return matchRooms.Count;
        }
    }

    public void RemoveRoom(in string roomId, bool forceShutdownNowPlayingRooms = false)
    {


    }

    public void RemoveAllRooms(bool forceShutdownNowPlayingRooms = false)
    {
        if (forceShutdownNowPlayingRooms)
        {

        }
        else
        {


        }


    }

    public List<MatchRoom> CanEnterRooms()
    {

        return null;
    }


   



    }
}
