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
        public OpenGSCore.MatchRoom room;
    }

    public class MatchRoomManager:IMatchSubscriber
    {

    private Dictionary<string, OpenGSCore.MatchRoom> matchRooms = new Dictionary<string, OpenGSCore.MatchRoom>();
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

    /// <summary>
    /// WaitRoom から MatchRoom を生成し、プレイヤーを移行する
    /// </summary>
    public OpenGSCore.MatchRoom? CreateMatchRoomByWaitRoom(WaitRoom waitRoom)
    {
        if (waitRoom == null) return null;
        if (!waitRoom.CanStartMatch()) return null;

        lock (matchRoomsLock)
        {
            // 設定とオーナーを取得
            var setting = waitRoom.GetOrCreateSetting();
            var ownerId = waitRoom.GetFirstPlayerId();

            // EventBus を作成
            var bus = new MatchRoomEventBus();

            // MatchRoom を生成
            var matchRoom = MatchRoomFactory.CreateMatchRoom(roomNumberCount, waitRoom.RoomName, ownerId, setting, this);

            // プレイヤーを移行
            matchRoom.AddNewPlayers(waitRoom.AllPlayers());

            // 相互リンク
            waitRoom.LinkMatchRoom(matchRoom);

            // 管理辞書に追加
            matchRooms.Add(matchRoom.Id, matchRoom);
            roomEventBuses[matchRoom.Id] = bus;

            IncreaseRoomCounter();

            return matchRoom;
        }
    }

    /// <summary>
    /// WaitRoom から MatchRoom を生成し、ゲームを開始する
    /// </summary>
    public OpenGSCore.MatchRoom? StartMatchFromWaitRoom(WaitRoom waitRoom)
    {
        var matchRoom = CreateMatchRoomByWaitRoom(waitRoom);
        if (matchRoom == null) return null;

        // ゲーム開始
        matchRoom.GameStart();
        return matchRoom;
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
        var matchSetting = new DeathMatchSetting(20, teamBalance);
        matchSetting.MaxPlayerCount = capacity;

        var result = CreateNewRoom(roomName, ownerID, matchSetting);

        return result;
    }

    public CreateNewRoomResult CreateNewTeamDeathMatchRoom(in string roomName, in string ownerID, int capacity = 10,
        bool teamBalance = true)
    {
        var matchSetting = new TDMMatchSetting(capacity, teamBalance);

        var result = CreateNewRoom(roomName, ownerID, matchSetting);

        return result;
    }


    public CreateNewRoomResult CreateNewSuvRoom(in string roomName, in string ownerID, int capacity = 10)
    {
        var matchSetting = new SuvMatchSetting(capacity, true);
        matchSetting.MaxPlayerCount = capacity;

        var result = CreateNewRoom(roomName, ownerID, matchSetting);
        return result;
    }

    public CreateNewRoomResult CreateNewTSuvRoom(in string roomName, in string ownerID, int capacity = 10)
    {
        var matchSetting = new TeamSurvivalMatchSetting(capacity, true);
        matchSetting.MaxPlayerCount = capacity;

        var result = CreateNewRoom(roomName, ownerID, matchSetting);

        return result;
    }

    public CreateNewRoomResult CreateNewCTFMatchRoom(string roomName, string ownerID, int capacity = 10)
    {
        // CTFモード - チームベースなのでTDM設定を使用
        var matchSetting = new TDMMatchSetting(capacity, true);
        matchSetting.MaxPlayerCount = capacity;

        var result = CreateNewRoom(roomName, ownerID, matchSetting);
        return result;
    }

    public CreateNewRoomResult CreateNewOneShotKillRoom(in string roomName, in string ownerID, int capacity = 10)
    {
        var matchSetting = new OneShotKillMatchSetting(capacity, true);
        matchSetting.MaxPlayerCount = capacity;

        var result = CreateNewRoom(roomName, ownerID, matchSetting);

        return result;
    }

    public CreateNewRoomResult CreateNewArmsRaceRoom(in string roomName, in string ownerID, int capacity = 10)
    {
        var matchSetting = new ArmsRaceMatchSetting(capacity, true);
        matchSetting.MaxPlayerCount = capacity;

        var result = CreateNewRoom(roomName, ownerID, matchSetting);

        return result;
    }

    public CreateNewRoomResult CreateNewMissionRoom(in string roomName, in string ownerID, int capacity = 3)
    {
        var matchSetting = new MissionMatchSetting(capacity, "Default");
        matchSetting.MaxPlayerCount = capacity;

        var result = CreateNewRoom(roomName, ownerID, matchSetting);
        return result;
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
