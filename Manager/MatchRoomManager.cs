using System;
using System.Collections.Generic;
using System.Threading;
using OpenGSCore;


namespace OpenGSServer
{
    

    public struct MatchResult
    {
        public EMatchRoomEventType type;
        public MatchRoom room;
    }

    public class MatchRoomManager:IObserver<MatchResult>
    {

    private Dictionary<string, MatchRoom> matchRooms = new Dictionary<string, MatchRoom>();

    private int roomNumberCount = 0;


    public static MatchRoomManager Instance { get; } = new();

    public static MatchRoomManager GetInstance()
    {
        return Instance;
    }

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
        var rooms = new List<AbstractGameRoom>(matchRooms.Values);




        return rooms;
    }

    public CreateNewRoomResult CreateNewDeathMatchRoom(in string roomName, in string ownerID, int capacity = 10,
        bool teamBalance = true)
    {
        var matchSetting = new DeathMatchSetting(10);

        var matchRule = new DeathMatchRule();



        var matchRoom = new MatchRoom(roomNumberCount, roomName, ownerID, matchRule);

        matchRooms.Add(matchRoom.Id, matchRoom);

        IncreaseRoomCounter();


        var createNewRoomResult =
            new CreateNewRoomResult(eCreateNewRoomResult.Successful, ECreateNewRoomReason.NoReason);

        return createNewRoomResult;
    }

    public void CreateNewTeamDeathMatchRoom(in string roomName, in string ownerID, int capacity = 10,
        bool teamBalance = true)
    {
        var tdmMatchSetting = new TDMMatchSetting();

        var matchRule = new TDMMatchRule();
        IncreaseRoomCounter();

        var matchRoom = new MatchRoom(0, roomName, ownerID, matchRule);

        //matchRoom.Subscribe(this);



        matchRooms.Add(matchRoom.Id, matchRoom);

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

        String message = "";

        if (matchRooms.ContainsKey(id.ToString()))
        {
            var room = matchRooms[id.ToString()];



            succeeded = true;

        }
        else
        {

            message = "No room";
            succeeded = false;
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


        foreach (var item in matchRooms)
        {


        }



        return null;
    }

    public bool StartMatch(in string id)
    {
        string message = "";

        if (matchRooms.ContainsKey(id))
        {
            lock (matchRooms)
            {
                var room = matchRooms[id];

                room.GameStart();

            }


            return true;
        }
        else
        {

        }



        return false;
    }

    public bool StartMatchTest()
    {
        foreach (var m in matchRooms)
        {
            
            m.Value.GameStart();



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

        return matchRooms.Count;
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

        public void OnCompleted()
        {
           
        }

   

        public void OnNext(EMatchRoomEventType value)
        {
            switch (value)
            {
                case EMatchRoomEventType.MatchStarted:
                    
                    
                    break;

                case EMatchRoomEventType.MathEnded:
                    OnMatchEnd();

                    break;

            }


        }


        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        private void OnMatchStarted()
        {

        }

        private void OnMatchEnd()
        {

        }

        public void OnNext(MatchResult value)
        {
            switch (value.type)
            {
                case EMatchRoomEventType.MatchStarted:


                    break;

                case EMatchRoomEventType.MathEnded:
                    OnMatchEnd();

                    break;

            }
        }
    }
}
