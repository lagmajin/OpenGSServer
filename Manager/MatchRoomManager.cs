using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenGSCore;


#nullable enable

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
        private readonly ConcurrentDictionary<string, byte> persistedMatchPlayers = new(StringComparer.OrdinalIgnoreCase);
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

        private readonly Dictionary<string, OpenGSServer.Network.ServerFieldItemManager> roomFieldItemManagers
            = new Dictionary<string, OpenGSServer.Network.ServerFieldItemManager>();

        private OpenGSServer.Network.ServerFieldItemManager CreateFieldItemManager(string roomId)
        {
            var itemManager = new OpenGSServer.Network.ServerFieldItemManager();
            itemManager.StartMatch(roomId);
            itemManager.RegisterSpawnPoint(0, 0f, 0f, 0f, "Center");
            itemManager.RegisterSpawnPoint(1, 8f, 0f, 0f, "East");
            itemManager.RegisterSpawnPoint(2, -8f, 0f, 0f, "West");
            itemManager.RegisterSpawnPoint(3, 0f, 0f, 8f, "North");
            itemManager.RegisterSpawnPoint(4, 0f, 0f, -8f, "South");
            itemManager.RegisterSpawnPoint(5, 6f, 0f, 6f, "Corner");
            itemManager.ConfigureDefaultSpawnRules();
            itemManager.ConfigureSpawnRule(EFieldItemType.GranadeLauncher, 1, 45.0f, 5);
            itemManager.ConfigureSpawnRule(EFieldItemType.FlameThrower, 1, 45.0f, 5);
            itemManager.ConfigureSpawnRule(EFieldItemType.HealItem, 2, 25.0f, 1, 2, 3, 4);
            roomFieldItemManagers[roomId] = itemManager;
            return itemManager;
        }

        private void BroadcastToRoom(OpenGSCore.MatchRoom room, Newtonsoft.Json.Linq.JObject json)
        {
            foreach (var p in room.Players)
            {
                var session = LobbyServerManager.Instance.GetSession(p.Id);
                session?.SendAsyncJsonWithTimeStamp(json);
            }
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
                if (!waitRoom.CanStartMatch()) return null;

                var setting = waitRoom.GetOrCreateSetting();
                var ownerId = waitRoom.GetFirstPlayerId();
                var bus = new MatchRoomEventBus();
                var matchRoom = MatchRoomFactory.CreateMatchRoom(roomNumberCount, waitRoom.RoomName, ownerId, setting, bus);
                // The lobby and native client keep using the wait-room ID
                // through loading and gameplay. Give the live MatchRoom that
                // same canonical identity so UDP admission and match events
                // remain bound to the room the client actually joined.
                matchRoom.Id = waitRoom.RoomId;
                matchRoom.AddNewPlayers(PreparePlayersForMatch(setting, waitRoom.AllPlayers()));
                var itemManager = CreateFieldItemManager(matchRoom.Id);

                bus.OnLoadingStarted += () =>
                {
                    // S3: the loading handshake is server owned, so the room has
                    // to enter the loading state here. Without this the room is
                    // never registered as waiting, the AllowEnterMap gate has
                    // nothing to check, and the loading timeout cannot release
                    // it either.
                    WaitRoomEventHandler.BeginLoadingForRoom(waitRoom);

                    var loadingJson = new JObject
                    {
                        ["MessageType"] = MessageType.LoadingStartedNotification,
                        ["RoomInfo"] = waitRoom.ToJson()
                    };

                    foreach (var p in waitRoom.Players.Values)
                    {
                        var session = LobbyServerManager.Instance.GetSession(p.Id);
                        session?.SendAsyncJsonWithTimeStamp(loadingJson);
                    }
                };

                bus.OnGameStarted += () =>
                {
                    var gameStartJson = new JObject
                    {
                        ["MessageType"] = MessageType.GameStartNotification,
                        ["RoomInfo"] = matchRoom.ToJSon()
                    };

                    foreach (var p in matchRoom.Players)
                    {
                        var session = LobbyServerManager.Instance.GetSession(p.Id);
                        session?.SendAsyncJsonWithTimeStamp(gameStartJson);
                        // AllowEnterMap is not sent here. The loading gate in
                        // WaitRoomEventHandler already approved entry using the
                        // wait room id, and sending a second copy keyed by the
                        // match room id made clients see two different rooms.
                    }
                };
                
                // アイテムイベントの購読
                bus.OnItemSpawned += (type, id) =>
                {
                    OpenGSServer.Network.FieldItemEventHandler.SpawnItem(
                        itemManager,
                        type,
                        id,
                        json => BroadcastToRoom(matchRoom, json));
                };
                bus.OnItemDespawned += () =>
                {
                    OpenGSServer.Network.FieldItemEventHandler.DespawnAllItems(
                        itemManager,
                        json => BroadcastToRoom(matchRoom, json));
                };
                bus.OnGameEndedWithResult += (result) =>
                {
                    RememberMatchResult(matchRoom.Id.ToString(), result);
                    foreach (var p in matchRoom.Players)
                    {
                        var perPlayerResult = SettleMatchResultForPlayer(matchRoom, p, result);
                        if (perPlayerResult == null)
                        {
                            continue;
                        }

                        var session = LobbyServerManager.Instance.GetSession(p.Id);
                        if (session != null)
                        {
                            session.SendAsyncJsonWithTimeStamp((JObject)perPlayerResult.DeepClone());
                        }
                    }
                };
                bus.OnGameEnded += () => {
                    itemManager.EndMatch();
                    // Do not leave live weapons in the reconnect snapshot until
                    // the next projectile tick notices that the room stopped.
                    OpenGSServer.Network.InGameMatchEventHandler.ClearRoomProjectiles(
                        matchRoom.Id.ToString());
                    // The stable room ID is reused for rematches, so do not let
                    // consumed grenade slots carry into the next match.
                    OpenGSServer.Network.ServerGrenadeLoadoutStore.ClearRoom(
                        matchRoom.Id.ToString());
                    OpenGSServer.Network.InGameMatchEventHandler.ClearRoomCombatEffects(
                        matchRoom.Id.ToString());
                    OpenGSServer.Network.InGameMatchEventHandler.ClearRoomFlagState(
                        matchRoom.Id.ToString());
                    InGameMatchEventHandler.ClearRoomMatchTimeState(matchRoom.Id.ToString());
                    foreach (var player in matchRoom.Players)
                    {
                        OpenGSServer.Network.ServerWeaponMagazineStore.ResetPlayer(player.Id);
                        InGameMatchEventHandler.ClearPlayerShotCooldowns(player.Id);
                    }
                    // UDPで全プレイヤーに正規のマッチ終了通知を送る
                    var firstPlayer = matchRoom.Players.Find(p => !string.IsNullOrEmpty(p.Id));
                    if (firstPlayer != null)
                    {
                        // The winner and the score travel with the notification.
                        // It used to be a message type and a room and nothing
                        // else, so a client that received it had to work out who
                        // won from a score it had never been told and was holding
                        // its own copy of. Both sides could finish a match
                        // disagreeing about the result, and a mode whose result is
                        // a score is the one where that matters most.
                        var result = roomEventBuses[matchRoom.Id] != null
                            ? LastMatchResult(matchRoom.Id)
                            : null;

                        MatchServerV2.Instance.BroadcastToRoom(
                            firstPlayer.Id,
                            MessageType.MatchEndNotification,
                            result);

                        ConsoleWrite.WriteMessage($"[Match] Broadcasted {MessageType.MatchEndNotification} via UDP to Room: {matchRoom.RoomName}", ConsoleColor.Cyan);
                    }

                    // Finished rooms stay available while players collect their
                    // results or reconnect. Their mission simulation does not:
                    // remove it now so the global tick does not retain and scan
                    // a finished wave for the lifetime of that room.
                    MissionEnemyRegistry.ClearRoom(matchRoom.Id);

                    // 待機室の状態を更新して全プレイヤーに通知
                    if (waitRoom != null)
                    {
                        var roomInfo = new JObject();
                        roomInfo["MessageType"] = MessageType.WaitRoomUpdateNotification;
                        roomInfo["RoomInfo"] = waitRoom.ToJson();

                        foreach (var p in waitRoom.Players.Values)
                        {
                            var session = LobbyServerManager.Instance.FindSessionByPlayerId(p.Id);
                            session?.SendAsyncJsonWithTimeStamp(roomInfo);
                        }
                    }
                };

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
            if (waitRoom == null || !waitRoom.CanStartMatch()) return null;

            var previousRoom = GetRoomById(waitRoom.RoomId);
            if (previousRoom != null)
            {
                // A finished MatchRoom keeps its result state available until
                // the next match starts. Reuse the stable wait-room ID only
                // after its detached-room cleanup has released that identity.
                if (!previousRoom.Finished) return null;
                RemoveRoom(waitRoom.RoomId);
            }

            var matchRoom = CreateMatchRoomByWaitRoom(waitRoom);
            if (matchRoom == null) return null;
            matchRoom.StartLoading();
            return matchRoom;
        }

        /// <summary>
        /// Marks a match room as running, which is what starts its status
        /// updates and publishes the game start event.
        /// <para>
        /// S2 asks for the match progression to be driven by a real server
        /// loop. Nothing called MatchRoom.GameStart, so Playing stayed false
        /// and the game loop skipped the room entirely: the match never
        /// actually started even though the lobby handshake completed. This is
        /// called once the loading gate has approved entry.
        /// </para>
        /// </summary>
        /// <summary>
        /// Starts the match room linked to a wait room, if there is one.
        /// </summary>
        public bool BeginMatchForWaitRoom(WaitRoom waitRoom)
        {
            if (waitRoom == null || string.IsNullOrWhiteSpace(waitRoom.RoomId))
            {
                return false;
            }

            var matchRoom = AllRooms()
                .OfType<OpenGSCore.MatchRoom>()
                .FirstOrDefault(candidate =>
                    candidate.WaitRoomLink != null &&
                    string.Equals(candidate.WaitRoomLink.RoomId, waitRoom.RoomId, StringComparison.OrdinalIgnoreCase));

            return matchRoom != null && (matchRoom.Playing || matchRoom.IsStarting || BeginMatch(matchRoom));
        }

        public bool DiscardPendingMatchForWaitRoom(string waitRoomId)
        {
            if (string.IsNullOrWhiteSpace(waitRoomId))
            {
                return true;
            }

            OpenGSCore.MatchRoom? detachedRoom;
            OpenGSServer.Network.ServerFieldItemManager? itemManager;
            string? matchRoomId;
            lock (matchRoomsLock)
            {
                var pendingRoom = matchRooms.Values.FirstOrDefault(room =>
                    room.WaitRoomLink != null &&
                    string.Equals(room.WaitRoomLink.RoomId, waitRoomId, StringComparison.OrdinalIgnoreCase));
                if (pendingRoom == null)
                {
                    return true;
                }

                if (pendingRoom.Playing || pendingRoom.IsStarting)
                {
                    return false;
                }

                matchRoomId = pendingRoom.Id.ToString();
                if (!TryDetachRoomLocked(matchRoomId, forceShutdownNowPlayingRooms: false,
                        out detachedRoom, out itemManager))
                {
                    return false;
                }
            }

            CleanupDetachedRoom(matchRoomId, detachedRoom!, itemManager);
            return true;
        }

        public bool BeginMatch(OpenGSCore.MatchRoom matchRoom)
        {
            if (matchRoom == null)
            {
                return false;
            }

            if (matchRoom.Playing)
            {
                return false;
            }

            int? missionEnemyCount = null;
            if (matchRoom.Setting?.Mode == EGameMode.Mission &&
                matchRoom.Setting is MissionMatchSetting missionSetting)
            {
                var requestedEnemyCount = Math.Max(0, missionSetting.ObjectiveCount);
                var supportedEnemyCount = Math.Min(
                    requestedEnemyCount,
                    OpenGSServer.Network.ServerEnemySimulator.MaxEnemiesPerRoom);
                if (!matchRoom.SetMissionObjectiveCountBeforeStart(supportedEnemyCount))
                    return false;
                missionEnemyCount = supportedEnemyCount;

                if (supportedEnemyCount != requestedEnemyCount)
                {
                    ConsoleWrite.WriteMessage(
                        $"[Mission] Room {matchRoom.Id} objective count was capped at {supportedEnemyCount} enemies to fit the snapshot protocol",
                        ConsoleColor.Yellow);
                }
            }

            // A new match starts with full magazines even when players stayed
            // in the lobby after the previous match and retained server ammo.
            foreach (var player in matchRoom.Players)
            {
                ServerWeaponMagazineStore.RefillAllMagazines(player.Id);
            }

            if (!matchRoom.GameStart())
            {
                return false;
            }

            // A mission needs something to fight. Its objective target was
            // reconciled before GameStart, when the rule and situation were
            // created from the room settings.
            if (missionEnemyCount.HasValue)
            {
                InGameMatchEventHandler.BeginMissionEnemies(
                    matchRoom, missionEnemyCount.Value, new System.Numerics.Vector2(0f, 0f));
                // Individual spawn events keep clients responsive, while this
                // room snapshot gives every current player one authoritative
                // baseline after the initial wave has been created.
                OpenGSServer.Network.MissionEnemyRegistry.PublishSnapshot(
                    matchRoom.Id.ToString());
                OpenGSServer.Network.MissionEnemyRegistry.PublishObjectiveProgress(
                    matchRoom.Id.ToString());
            }

            ConsoleWrite.WriteMessage(
                $"[Match] Room {matchRoom.Id} ({matchRoom.RoomName}) is now playing",
                ConsoleColor.Green);
            return true;
        }

        public List<AbstractGameRoom> AllRooms()
        {
            lock (matchRoomsLock)
            {
                return new List<AbstractGameRoom>(matchRooms.Values);
            }
        }

        public void AddRoom(OpenGSCore.MatchRoom room)
        {
            if (room == null)
            {
                return;
            }

            lock (matchRoomsLock)
            {
                matchRooms[room.Id] = room;
                // Keep an existing bus so handlers subscribed by the caller
                // survive a re-add, but never replace it with a fresh one:
                // the room publishes to the bus it was constructed with, so a
                // different instance would never receive those events.
                if (!roomEventBuses.ContainsKey(room.Id))
                {
                    roomEventBuses[room.Id] = new MatchRoomEventBus();
                }
                if (!roomFieldItemManagers.ContainsKey(room.Id))
                {
                    CreateFieldItemManager(room.Id);
                }
            }
        }

        /// <summary>
        /// The field item manager for a room, or null when the room has none.
        /// The managers are created alongside the room and torn down with it, so
        /// this is the only way to reach the item state for a live match.
        /// </summary>
        public OpenGSServer.Network.ServerFieldItemManager? GetFieldItemManager(string roomId)
        {
            lock (matchRoomsLock)
            {
                return roomFieldItemManagers.TryGetValue(roomId, out var manager) ? manager : null;
            }
        }
        public AbstractGameRoom? FindRoom(string roomId)
        {
            return GetRoomById(roomId);
        }

        public CreateNewRoomResult CreateNewRoom(in string roomName, in string ownerID, AbstractMatchSetting setting)
        {
            // The manager overload of MatchRoomFactory builds a throwaway event bus
            // that nothing can subscribe to, because the manager keeps its own
            // bus per room. Create the bus here and hand it to the room so
            // the dictionary entry and the room share one instance.
            var bus = new MatchRoomEventBus();
            var room = MatchRoomFactory.CreateMatchRoom(roomNumberCount, roomName, ownerID, setting, bus);
            
            lock (matchRoomsLock)
            {
                matchRooms.Add(room.Id, room);
                roomEventBuses[room.Id] = bus;
                CreateFieldItemManager(room.Id);
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
        // CaptureTheFlagMatchSetting exists, so a CTF room now actually gets one.
        // It used to be handed a TDMMatchSetting, which made the room report
        // EGameMode.TeamDeathMatch: the rule was a team death match rule and a
        // flag capture ended the match the way a kill did.
        var matchSetting = new CaptureTheFlagMatchSetting(winCondition: 3, teamBalance: true)
        {
            MaxPlayerCount = capacity
        };

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

        /// <summary>
        /// Rooms are keyed by the hyphen free form of their id, because
        /// AbstractGameRoom builds it with Guid.ToString("N"). An id arriving
        /// from the network is a Guid, so it has to be reduced to the same
        /// shape before it can be used as a lookup key.
        /// </summary>
        private static string NormalizeRoomKey(in Guid id)
        {
            return id.ToString("N");
        }

    public EnterMatchRoomResult EnterRoom(in Guid id, PlayerAccount player)
    {
        string message = "";

        lock (matchRoomsLock)
        {
            
                // Room ids are generated as Guid.NewGuid().ToString("N"), so they
                // carry no hyphens. Looking the room up by id.ToString() used
                // the hyphenated form and therefore never matched a real
                // room, which made every entry attempt report "Room not
                // found". Normalize the incoming id the same way the room id
                // was built.
                var roomKey = NormalizeRoomKey(id);

                if (matchRooms.TryGetValue(roomKey, out var room))
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
        string? emptyRoomId = null;
        OpenGSCore.MatchRoom? detachedRoom = null;
        OpenGSServer.Network.ServerFieldItemManager? itemManager = null;
        lock (matchRoomsLock)
        {
            var room = matchRooms.Values.FirstOrDefault(candidate =>
                candidate.Players.Exists(player =>
                    string.Equals(player.Id, userid, StringComparison.OrdinalIgnoreCase)));
            if (room == null)
            {
                return;
            }

            room.RemovePlayer(userid);
            // ルームが空になったら削除
            if (room.Players.Count == 0)
            {
                emptyRoomId = room.Id.ToString();
                TryDetachRoomLocked(emptyRoomId, forceShutdownNowPlayingRooms: true,
                    out detachedRoom, out itemManager);
            }
        }

        // Do the teardown after releasing the room lock. Projectile cleanup
        // takes the simulator lock, whose update path can query this manager.
        if (emptyRoomId != null)
        {
            if (detachedRoom != null)
            {
                CleanupDetachedRoom(emptyRoomId, detachedRoom, itemManager);
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
                if (room.Players.Exists(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
                {
                    return room;
                }
            }
        }

        return null;
    }

        public bool StartMatch(in string id)
        {
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
        OpenGSCore.MatchRoom? room;
        OpenGSServer.Network.ServerFieldItemManager? itemManager;
        lock (matchRoomsLock)
        {
            if (!TryDetachRoomLocked(roomId, forceShutdownNowPlayingRooms,
                    out room, out itemManager)) return;
        }

        CleanupDetachedRoom(roomId, room!, itemManager);
    }

    private bool TryDetachRoomLocked(
        string roomId,
        bool forceShutdownNowPlayingRooms,
        out OpenGSCore.MatchRoom? room,
        out OpenGSServer.Network.ServerFieldItemManager? itemManager)
    {
        itemManager = null;
        if (!matchRooms.TryGetValue(roomId, out room) ||
            (!forceShutdownNowPlayingRooms && room.Playing))
        {
            room = null;
            return false;
        }

        // The caller holds matchRoomsLock. Detach atomically, then run callbacks
        // outside the lock to avoid manager/simulator lock inversion.
        matchRooms.Remove(roomId);
        roomEventBuses.Remove(roomId);
        roomFieldItemManagers.Remove(roomId, out itemManager);
        return true;
    }

    private static void CleanupDetachedRoom(
        string roomId,
        OpenGSCore.MatchRoom room,
        OpenGSServer.Network.ServerFieldItemManager? itemManager)
    {
        ClearDetachedRoomWeaponState(room);
        InGameMatchEventHandler.ClearRoomProjectiles(roomId);
        InGameMatchEventHandler.ClearRoomCombatEffects(roomId);
        InGameMatchEventHandler.ClearRoomFlagState(room.Id);
        InGameMatchEventHandler.ClearRoomMatchTimeState(roomId);
        MissionEnemyRegistry.ClearRoom(room.Id);
        ClearMatchResults(roomId);
        foreach (var key in persistedMatchPlayers.Keys)
        {
            if (key.StartsWith($"{roomId}:", StringComparison.OrdinalIgnoreCase))
            {
                persistedMatchPlayers.TryRemove(key, out _);
            }
        }
        itemManager?.EndMatch();
    }

    private static void ClearDetachedRoomWeaponState(OpenGSCore.MatchRoom room)
    {
        // The magazine store is keyed by player, while a player may already have
        // entered a different room by the time an old completed room is removed.
        var playerIds = room.Players
            .Select(player => player.Id)
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId) &&
                MatchRoomManager.Instance.SearchRoomByMemberID(playerId) == null)
            .ToList();
        OpenGSServer.Network.ServerWeaponMagazineStore.ClearRoomPlayers(playerIds);
        foreach (var playerId in playerIds)
            InGameMatchEventHandler.ClearPlayerShotCooldowns(playerId);
    }

    public void RemoveAllRooms(bool forceShutdownNowPlayingRooms = false)
    {
        List<(string RoomId, OpenGSCore.MatchRoom Room, OpenGSServer.Network.ServerFieldItemManager? ItemManager)> removed = new();
        lock (matchRoomsLock)
        {
            foreach (var (roomId, room) in matchRooms.ToList())
            {
                if (!forceShutdownNowPlayingRooms && room.Playing)
                {
                    continue;
                }

                matchRooms.Remove(roomId);
                roomEventBuses.Remove(roomId);
                roomFieldItemManagers.Remove(roomId, out var itemManager);
                removed.Add((roomId, room, itemManager));
            }
        }

        foreach (var (roomId, room, itemManager) in removed)
        {
            ClearDetachedRoomWeaponState(room);
            InGameMatchEventHandler.ClearRoomProjectiles(roomId);
            InGameMatchEventHandler.ClearRoomCombatEffects(roomId);
            InGameMatchEventHandler.ClearRoomFlagState(room.Id);
            InGameMatchEventHandler.ClearRoomMatchTimeState(roomId);
            MissionEnemyRegistry.ClearRoom(room.Id);
            ClearMatchResults(roomId);
            foreach (var key in persistedMatchPlayers.Keys)
            {
                if (key.StartsWith($"{roomId}:", StringComparison.OrdinalIgnoreCase))
                {
                    persistedMatchPlayers.TryRemove(key, out _);
                }
            }
            itemManager?.EndMatch();
        }
    }

    public List<MatchRoom> CanEnterRooms()
    {
        lock (matchRoomsLock)
        {
            var result = new List<MatchRoom>();
            foreach (var room in matchRooms.Values)
            {
                if (!room.Playing && !room.Finished &&
                    room.Players.Count < room.Setting.MaxPlayerCount)
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

        /// <summary>
        /// The last result the room's rule produced, kept so it can be repeated.
        /// <para>
        /// The end of a match is published twice: once with the result, and once
        /// as the bare notification. The rule has already worked the answer out
        /// by the time the first one goes out, and working it out again from a
        /// room that has already been torn down is a way of getting a different
        /// answer, so the first one is remembered instead.
        /// </para>
        /// </summary>
        private static readonly ConcurrentDictionary<string, JObject> lastMatchResults =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, JObject> lastPlayerMatchResults =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, DateTime> matchResultCompletedAtUtc =
            new(StringComparer.OrdinalIgnoreCase);

        private static string PlayerMatchResultKey(string roomId, string playerId) =>
            $"{roomId}:{playerId}";

        private static void RememberMatchPlayerResult(string roomId, string playerId, JObject result)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                return;
            lastPlayerMatchResults[PlayerMatchResultKey(roomId, playerId)] =
                (JObject)result.DeepClone();
        }

        private static void ClearMatchResults(string roomId)
        {
            lastMatchResults.TryRemove(roomId, out _);
            matchResultCompletedAtUtc.TryRemove(roomId, out _);
            var prefix = $"{roomId}:";
            foreach (var key in lastPlayerMatchResults.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    lastPlayerMatchResults.TryRemove(key, out _);
            }
        }

        private static void RememberMatchResult(string roomId, JObject result)
        {
            if (string.IsNullOrWhiteSpace(roomId) || result == null)
            {
                return;
            }

            if (lastMatchResults.TryAdd(roomId, (JObject)result.DeepClone()))
            {
                matchResultCompletedAtUtc.TryAdd(roomId, DateTime.UtcNow);
            }
        }

        private static JObject? LastMatchResult(string roomId)
        {
            return lastMatchResults.TryGetValue(roomId, out var result) ? result : null;
        }

        private JObject? SettleMatchResultForPlayer(
            MatchRoom room, PlayerInfo player, JObject result)
        {
            var roomId = room.Id.ToString();
            if (lastPlayerMatchResults.TryGetValue(
                PlayerMatchResultKey(roomId, player.Id), out var existingResult))
            {
                TryRecordMatchDailyProgress(player.Id, roomId,
                    ExtractWinnerIds(result).Contains(player.Id));
                return (JObject)existingResult.DeepClone();
            }

            var resultKey = PlayerMatchResultKey(roomId, player.Id);
            if (!persistedMatchPlayers.TryAdd(resultKey, 0))
            {
                return lastPlayerMatchResults.TryGetValue(resultKey, out existingResult)
                    ? (JObject)existingResult.DeepClone()
                    : null;
            }

            var matchResultPersisted = false;
            try
            {
                var won = ExtractWinnerIds(result).Contains(player.Id);
                // Build all room/player payload data before committing XP. If
                // serialization fails here, the next reconnect can retry the
                // whole settlement without risking duplicate progression.
                var perPlayerResult = BuildMatchResultEnvelope(room, result, player);
                var persisted = LobbyServerManager.Instance.RecordMatchResult(
                    roomId,
                    player.Id,
                    player.Score,
                    player.Kills,
                    player.Deaths,
                    won,
                    room.Setting.Mode);
                if (persisted == null)
                {
                    throw new InvalidOperationException(
                        $"No account record was updated for match player '{player.Id}'.");
                }

                // Keep the idempotency marker after the durable XP update, so
                // retrying delivery or reconnect recovery cannot award it twice.
                matchResultPersisted = true;
                perPlayerResult["XpGained"] = persisted.XpGained;
                perPlayerResult["TotalExp"] = persisted.NewTotalXp;
                perPlayerResult["NewLevel"] = persisted.NewLevel;
                perPlayerResult["IsLevelUp"] = persisted.IsLevelUp;
                RememberMatchPlayerResult(roomId, player.Id, perPlayerResult);

                TryRecordMatchDailyProgress(player.Id, roomId, won);
                RecordStanding(room, player);
                return (JObject)perPlayerResult.DeepClone();
            }
            catch (Exception ex)
            {
                if (!matchResultPersisted)
                {
                    persistedMatchPlayers.TryRemove(resultKey, out _);
                }

                ConsoleWrite.WriteMessage(
                    $"[Match] Failed to settle results for player '{player.Id}' in room '{room.Id}': {ex.Message}",
                    ConsoleColor.Red);
                return lastPlayerMatchResults.TryGetValue(resultKey, out existingResult)
                    ? (JObject)existingResult.DeepClone()
                    : null;
            }
        }

        private static void TryRecordMatchDailyProgress(string playerId, string roomId, bool won)
        {
            try
            {
                var completedAtUtc = matchResultCompletedAtUtc.TryGetValue(roomId, out var recordedAt)
                    ? recordedAt
                    : DateTime.UtcNow;
                LobbyServerManager.Instance.RecordMatchDailyProgress(playerId, won, completedAtUtc);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage(
                    $"[Daily] Could not record match progress for {playerId} in room {roomId}: {ex.Message}",
                    ConsoleColor.Yellow);
            }
        }

        public JObject? GetLastMatchResultEnvelope(OpenGSCore.MatchRoom room, PlayerInfo player)
        {
            if (room == null || player == null)
            {
                return null;
            }

            if (lastPlayerMatchResults.TryGetValue(
                PlayerMatchResultKey(room.Id.ToString(), player.Id), out var playerResult))
                return (JObject)playerResult.DeepClone();

            var result = LastMatchResult(room.Id.ToString());
            if (result == null) return null;

            return SettleMatchResultForPlayer(room, player, result) ??
                BuildMatchResultEnvelope(room, result, player);
        }

        private static JObject BuildMatchResultEnvelope(OpenGSCore.MatchRoom matchRoom, JObject result, PlayerInfo player)
        {
            var envelope = result != null ? new JObject(result) : new JObject();
            envelope["MessageType"] = envelope["MessageType"]?.ToString() ?? MessageType.MatchEndNotification;

            if (matchRoom != null)
            {
                envelope["RoomId"] = matchRoom.Id;
                envelope["RoomName"] = matchRoom.RoomName;
                envelope["PlayerCount"] = matchRoom.PlayerCount;
                envelope["RoomInfo"] = matchRoom.ToJSon();
            }

            if (envelope["Players"] == null && matchRoom != null)
            {
                envelope["Players"] = new JArray(matchRoom.Players.Select(p => p.ToJson()));
            }

            if (player != null)
            {
                envelope["MyTeam"] = player.Team.ToString();
                envelope["MyPlayerId"] = player.Id;
                envelope["MyPlayerName"] = player.Name;
            }

            return envelope;
        }

        private static HashSet<string> ExtractWinnerIds(JObject? result)
        {
            var winners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (result == null) return winners;

            foreach (var id in result["Winners"]?.Values<string>() ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(id)) winners.Add(id);
            }

            foreach (var key in new[] { "Winner", "WinningPlayerId" })
            {
                var id = result[key]?.ToString();
                if (!string.IsNullOrWhiteSpace(id) && !id.Equals("Draw", StringComparison.OrdinalIgnoreCase) && !id.Equals("NoPlayers", StringComparison.OrdinalIgnoreCase))
                {
                    winners.Add(id);
                }
            }

            var winningTeam = result["WinningTeam"]?.ToString();
            if (!string.IsNullOrWhiteSpace(winningTeam) && !winningTeam.Equals("Draw", StringComparison.OrdinalIgnoreCase) && !winningTeam.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var player in result["Players"] as JArray ?? new JArray())
                {
                    var team = player["Team"]?.ToString() ?? player["TeamName"]?.ToString();
                    var id = player["Id"]?.ToString() ?? player["PlayerId"]?.ToString() ?? player["PlayerID"]?.ToString();
                    if (string.Equals(team, winningTeam, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(id))
                    {
                        winners.Add(id);
                    }
                }
            }

            return winners;
        }

        private static List<PlayerInfo> PreparePlayersForMatch(AbstractMatchSetting setting, IEnumerable<PlayerInfo> players)
        {
            var result = new List<PlayerInfo>();
            if (players == null)
            {
                return result;
            }

            var healthMultiplier = ResolveHealthMultiplier(setting);
            foreach (var player in players)
            {
                result.Add(PreparePlayerForMatch(player, healthMultiplier));
            }

            return result;
        }

        /// <summary>
        /// Adds a finished match to the leaderboard.
        /// <para>
        /// The standing used to be written nowhere. The ranking store existed and
        /// every one of its methods fetched a collection and dropped it, so a
        /// player's record lasted exactly as long as the process did.
        /// </para>
        /// <para>
        /// A failure here must not fail the match. Everyone has already been told
        /// their result and their experience by this point, and a leaderboard that
        /// is not written is worth less than a match that still ends.
        /// </para>
        /// </summary>
        private static void RecordStanding(MatchRoom room, PlayerInfo player)
        {
            if (room?.Setting == null || player == null || string.IsNullOrWhiteSpace(player.Id))
            {
                return;
            }

            try
            {
                RankingDatabase.Instance.Record(
                    player.Id, player.Name, room.Setting.Mode, player.Score);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage(
                    $"[Ranking] Could not record {player.Id} in room {room.Id}: {ex.Message}",
                    ConsoleColor.Yellow);
            }
        }

        private static PlayerInfo PreparePlayerForMatch(PlayerInfo source, float healthMultiplier)        {
            if (source == null)
            {
                return new PlayerInfo();
            }

            var clone = PlayerInfo.FromJson(source.ToJson()) ?? new PlayerInfo(source.Id, source.Name)
            {
                playerCharacter = source.playerCharacter,
                Level = source.Level,
                Exp = source.Exp,
                AttackPower = source.AttackPower,
                DefensePower = source.DefensePower,
                Team = source.Team,
                IsReady = source.IsReady,
                Kills = source.Kills,
                Deaths = source.Deaths,
                IsBot = source.IsBot
            };

            var maxHealth = clone.MaxHealth > 0 ? clone.MaxHealth : 100;
            if (healthMultiplier > 1.0f)
            {
                maxHealth = Math.Max(1, (int)Math.Round(maxHealth * healthMultiplier));
            }

            // A player enters every match at full health, not at whatever they
            // finished the last one with. This used to apply the multiplier only
            // when it was above one, so with the default multiplier nothing here
            // touched health at all and a player who died in the previous match
            // entered this one already dead: the survival rules read alive straight
            // off the health, so they could end the match having counted a player
            // who never took part in it.
            clone.MaxHealth = Math.Max(1, maxHealth);
            clone.Health = clone.MaxHealth;
            clone.Kills = 0;
            clone.Deaths = 0;
            clone.Score = 0;
            return clone;
        }

        private static float ResolveHealthMultiplier(AbstractMatchSetting setting)
        {
            return setting switch
            {
                SuvMatchSetting suvSetting => Math.Max(1.0f, suvSetting.HealthMultiplier),
                TeamSurvivalMatchSetting teamSetting => Math.Max(1.0f, teamSetting.HealthMultiplier),
                _ => 1.0f
            };
        }


   



    }
}
