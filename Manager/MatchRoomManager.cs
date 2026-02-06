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

    public class MatchRoomManager : IMatchSubscriber
    {
        private Dictionary<string, OpenGSCore.MatchRoom> matchRooms = new Dictionary<string, OpenGSCore.MatchRoom>();
        private readonly object matchRoomsLock = new();
        private int roomNumberCount = 0;
        public readonly Dictionary<string, MatchRoomEventBus> roomEventBuses = new Dictionary<string, MatchRoomEventBus>();
        public static MatchRoomManager Instance { get; } = new();

        private void IncreaseRoomCounter()
        {
            Interlocked.Increment(ref roomNumberCount);
        }

        public MatchRoomManager()
        {
        }

        /// <summary>
        /// IMatchSubscriber インターフェース実装: マッチ開始通知
        /// </summary>
        public void OnMatchStarted(OpenGSCore.MatchRoom room)
        {
            // マッチ開始時の処理（必要に応じて拡張）
            if (roomEventBuses.TryGetValue(room.Id, out var bus))
            {
                bus.PublishMatchStarted(room);
            }
        }

        /// <summary>
        /// IMatchSubscriber インターフェース実装: マッチ終了通知
        /// </summary>
        public void OnMatchEnded(OpenGSCore.MatchRoom room)
        {
            // マッチ終了時の処理（必要に応じて拡張）
            if (roomEventBuses.TryGetValue(room.Id, out var bus))
            {
                bus.PublishMatchEnded(room);
            }
        }

        /// <summary>
        /// IMatchSubscriber インターフェース実装: プレイヤー参加通知
        /// </summary>
        public void OnPlayerJoined(OpenGSCore.MatchRoom room, PlayerAccount player)
        {
            if (roomEventBuses.TryGetValue(room.Id, out var bus))
            {
                bus.PublishPlayerJoined(room, player);
            }
        }

        /// <summary>
        /// IMatchSubscriber インターフェース実装: プレイヤー退出通知
        /// </summary>
        public void OnPlayerLeft(OpenGSCore.MatchRoom room, PlayerAccount player)
        {
            if (roomEventBuses.TryGetValue(room.Id, out var bus))
            {
                bus.PublishPlayerLeft(room, player);
            }
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
                var setting = waitRoom.GetOrCreateSetting();
                var ownerId = waitRoom.GetFirstPlayerId();
                var bus = new MatchRoomEventBus();
                var matchRoom = MatchRoomFactory.CreateMatchRoom(roomNumberCount, waitRoom.RoomName, ownerId, setting, this);
                matchRoom.AddNewPlayers(waitRoom.AllPlayers());
                waitRoom.LinkMatchRoom(matchRoom);
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

        public CreateNewRoomResult CreateNewRoom(in string roomName, in string ownerID, AbstractMatchSetting setting)
        {
            var room = MatchRoomFactory.CreateMatchRoom(roomNumberCount, roomName, ownerID, setting, this);
            
            lock (matchRoomsLock)
            {
                matchRooms.Add(room.Id, room);
                roomEventBuses[room.Id] = new MatchRoomEventBus();
                IncreaseRoomCounter();
            }

            var createNewRoomResult = new CreateNewRoomResult(ECreateNewRoomResult.Successful, ECreateNewRoomReason.NoReason)
            {
                RoomId = room.Id
            };

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
        return new EnterMatchRoomResult(false, "Room ID and player information missing");
    }

    public EnterMatchRoomResult EnterRoom(in Guid id, PlayerAccount player)
    {
        string message = "";

        lock (matchRoomsLock)
        {
            if (matchRooms.TryGetValue(id.ToString(), out var room))
            {
                if (room.Playing)
                {
                    message = "Match has already started";
                    return new EnterMatchRoomResult(false, message);
                }

                if (room.Players.Count >= room.Setting.MaxPlayerCount)
                {
                    message = "Room capacity is full";
                    return new EnterMatchRoomResult(false, message);
                }

                room.AddNewPlayer(player.ToPlayerInfo());
                return new EnterMatchRoomResult(true, "Success");
            }
            else
            {
                message = "Room not found";
                return new EnterMatchRoomResult(false, message);
            }
        }
    }

    public EnterMatchRoomResult EnterRoom(in Guid id)
    {
        return new EnterMatchRoomResult(false, "Player information missing");
    }

    public void ExitRoom(string userid)
    {
        lock (matchRoomsLock)
        {
            foreach (var room in matchRooms.Values)
            {
                if (room.Players.Exists(p => p.Id == userid))
                {
                    room.RemovePlayer(userid);
                    // ルームが空になったら削除
                    if (room.Players.Count == 0)
                    {
                        matchRooms.Remove(room.Id);
                        roomEventBuses.Remove(room.Id);
                    }
                    break;
                }
            }
        }
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
        lock (matchRoomsLock)
        {
            foreach (var room in matchRooms.Values)
            {
                if (!room.Playing)
                {
                    room.StartLoading();
                }
            }
        }
        return true;
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
        lock (matchRoomsLock)
        {
            if (matchRooms.TryGetValue(roomId, out var room))
            {
                // プレイ中のルームの強制シャットダウンチェック
                if (!forceShutdownNowPlayingRooms && room.Playing)
                {
                    return;
                }

                matchRooms.Remove(roomId);
                roomEventBuses.Remove(roomId);
            }
        }
    }

    public void RemoveAllRooms(bool forceShutdownNowPlayingRooms = false)
    {
        lock (matchRoomsLock)
        {
            if (forceShutdownNowPlayingRooms)
            {
                matchRooms.Clear();
                roomEventBuses.Clear();
            }
            else
            {
                // プレイ中のルーム以外を削除
                var roomsToRemove = new List<string>();
                foreach (var room in matchRooms.Values)
                {
                    if (!room.Playing)
                    {
                        roomsToRemove.Add(room.Id);
                    }
                }

                foreach (var roomId in roomsToRemove)
                {
                    matchRooms.Remove(roomId);
                    roomEventBuses.Remove(roomId);
                }
            }
        }
    }

    public List<MatchRoom> CanEnterRooms()
    {
        lock (matchRoomsLock)
        {
            var result = new List<MatchRoom>();
            foreach (var room in matchRooms.Values)
            {
                if (!room.Playing && room.Players.Count < room.Setting.MaxPlayerCount)
                {
                    result.Add(room);
                }
            }
            return result;
        }
    }

    public MatchRoom? GetRoomById(string roomId)
    {
        lock (matchRoomsLock)
        {
            if (matchRooms.TryGetValue(roomId, out var room))
            {
                return room;
            }
            return null;
        }
    }


   



    }
}
