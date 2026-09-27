using System;
using System.Linq;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the room lifecycle surface of MatchRoomManager: creation, entry
/// gating, start, and teardown.
///
/// MatchRoomManager is the gatekeeper for what a player sees in the room list
/// and who is allowed into a match. Nothing else in the test suite exercised
/// it, even though it is where the S2 and S3 fixes landed. These tests pin the
/// behaviour those fixes depend on so a later change cannot silently reopen
/// them.
///
/// Every test builds its own manager instead of using MatchRoomManager.
/// Instance, because the rooms, the event buses, and the field item managers
/// are all instance state that would otherwise leak between tests.
/// </summary>
public sealed class MatchRoomManagerTests
{
    private static MatchRoomManager NewManager() => new();

    private static PlayerAccount NewAccount(string? id = null, string name = "player")
    {
        return new PlayerAccount(id ?? Guid.NewGuid().ToString("N"), name, "test-password");
    }

    // EnterMatchRoomResult keeps its success flag private, so the only way a
    // caller can observe the outcome is the serialized MessageType. Asserting
    // through it keeps these tests honest about what the server actually
    // reports to a client.
    private static void AssertEnterSucceeded(EnterMatchRoomResult result)
    {
        Assert.Equal("MatchRoomEnterSuccess", result.ToJson()["MessageType"]?.ToString());
    }

    private static void AssertEnterFailed(EnterMatchRoomResult result)
    {
        Assert.Equal("MatchRoomEnterFailed", result.ToJson()["MessageType"]?.ToString());
    }

    // ---- Creation -------------------------------------------------------

    [Fact]
    public void CreateNewRoomRegistersTheRoomAndReturnsItsId()
    {
        var manager = NewManager();

        var result = manager.CreateNewRoom("dm-room", "owner-1", new DeathMatchSetting(20, true));

        Assert.True(result.Result == ECreateNewRoomResult.Successful);
        Assert.False(string.IsNullOrEmpty(result.RoomId));
        Assert.Equal(1, manager.RoomCount());
        Assert.NotNull(manager.GetRoomById(result.RoomId));
    }

    [Fact]
    public void CreateNewRoomGivesEachRoomADistinctId()
    {
        var manager = NewManager();

        var first = manager.CreateNewRoom("a", "owner-1", new DeathMatchSetting(20, true));
        var second = manager.CreateNewRoom("b", "owner-1", new DeathMatchSetting(20, true));

        Assert.NotEqual(first.RoomId, second.RoomId);
        Assert.Equal(2, manager.RoomCount());
    }

    [Fact]
    public void AddRoomIgnoresANullRoom()
    {
        var manager = NewManager();

        manager.AddRoom(null!);

        Assert.Equal(0, manager.RoomCount());
    }

    [Fact]
    public void CreateNewRoomAppliesTheRequestedCapacity()
    {
        var manager = NewManager();

        var result = manager.CreateNewDeathMatchRoom("small", "owner-1", capacity: 2);

        var room = manager.GetRoomById(result.RoomId);
        Assert.NotNull(room);
        Assert.Equal(2, room!.Setting.MaxPlayerCount);
    }

    [Fact]
    public void CreateNewCtfRoomUsesTeamDeathMatchSettings()
    {
        var manager = NewManager();

        var result = manager.CreateNewCTFMatchRoom("ctf", "owner-1");

        var room = manager.GetRoomById(result.RoomId);
        Assert.NotNull(room);
        // The CTF factory has no CaptureTheFlagMatchSetting to build, because
        // that setting type does not exist in the shared package. It hands the
        // room a TDMMatchSetting instead, so a room created through the CTF
        // entry point reports EGameMode.TeamDeathMatch. This assertion records
        // the current behaviour so a future CaptureTheFlagMatchSetting becomes
        // a visible, deliberate change rather than a silent one.
        Assert.Equal(EGameMode.TeamDeathMatch, room!.Setting.Mode);
    }

    // ---- Entry ----------------------------------------------------------

    [Fact]
    public void EnterRoomAddsThePlayerToAnExistingRoom()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;

        var result = manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-1"));

        AssertEnterSucceeded(result);
        var room = manager.GetRoomById(roomId);
        Assert.NotNull(room);
        Assert.True(room!.ContainsPlayer("p-1"));
    }

    [Fact]
    public void EnterRoomRejectsAnUnknownRoom()
    {
        var manager = NewManager();

        var result = manager.EnterRoom(Guid.NewGuid(), NewAccount("p-1"));

        AssertEnterFailed(result);
    }

    [Fact]
    public void EnterRoomRejectsAFullRoom()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1", capacity: 1).RoomId;

        var first = manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-1"));
        var second = manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-2"));

        AssertEnterSucceeded(first);
        AssertEnterFailed(second);
    }

    [Fact]
    public void EnterRoomRejectsAPlayingRoom()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        var room = manager.GetRoomById(roomId)!;
        manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-1"));

        manager.StartMatch(roomId);
        manager.BeginMatch(room);

        var result = manager.EnterRoom(Guid.Parse(roomId), NewAccount("late-joiner"));

        AssertEnterFailed(result);
    }

    [Fact]
    public void EnterRoomWithoutArgumentsAlwaysFails()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;

        AssertEnterFailed(manager.EnterRoom());
        AssertEnterFailed(manager.EnterRoom(Guid.Parse(roomId)));
    }

    [Fact]
    public void SearchIsCaseInsensitiveButContainsPlayerIsNot()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        manager.EnterRoom(Guid.Parse(roomId), NewAccount("MixedCase-Id"));

        // Room membership lookups and the room's own ContainsPlayer disagree
        // about casing: SearchRoomByMemberID compares with OrdinalIgnoreCase
        // while MatchRoom.ContainsPlayer uses a plain string equality. The
        // asymmetric behaviour is recorded here so that unifying them later is
        // a deliberate decision rather than an accident.
        Assert.NotNull(manager.SearchRoomByMemberID("mixedcase-id"));

        var room = manager.GetRoomById(roomId)!;
        Assert.True(room.ContainsPlayer("MixedCase-Id"));
        Assert.False(room.ContainsPlayer("mixedcase-id"));
    }

    // ---- Leaving --------------------------------------------------------

    [Fact]
    public void ExitRoomRemovesThePlayerButKeepsAPopulatedRoom()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1", capacity: 4).RoomId;
        manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-1"));
        manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-2"));

        manager.ExitRoom("p-1");

        var room = manager.GetRoomById(roomId);
        Assert.NotNull(room);
        Assert.False(room!.ContainsPlayer("p-1"));
        Assert.True(room.ContainsPlayer("p-2"));
        Assert.Equal(1, manager.RoomCount());
    }

    [Fact]
    public void ExitRoomDeletesTheRoomOnceItBecomesEmpty()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-1"));

        manager.ExitRoom("p-1");

        Assert.Equal(0, manager.RoomCount());
        Assert.Null(manager.GetRoomById(roomId));
    }

    [Fact]
    public void ExitRoomIgnoresAnUnknownPlayer()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-1"));

        manager.ExitRoom("nobody");

        Assert.Equal(1, manager.RoomCount());
    }

    // ---- Lookup ---------------------------------------------------------

    [Fact]
    public void SearchRoomByMemberIdFindsTheOwningRoom()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        manager.EnterRoom(Guid.Parse(roomId), NewAccount("p-1"));

        var found = manager.SearchRoomByMemberID("P-1");

        Assert.NotNull(found);
        Assert.Equal(roomId, found!.Id);
    }

    [Fact]
    public void SearchRoomByMemberIdReturnsNullForUnknownAndEmptyIds()
    {
        var manager = NewManager();

        Assert.Null(manager.SearchRoomByMemberID("nobody"));
        Assert.Null(manager.SearchRoomByMemberID(""));
        Assert.Null(manager.SearchRoomByMemberID(null!));
    }

    [Fact]
    public void FindRoomAndGetRoomByIdAgree()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;

        Assert.Equal(roomId, manager.GetRoomById(roomId)?.Id);
        Assert.Equal(roomId, manager.FindRoom(roomId)?.Id);
        Assert.Null(manager.GetRoomById("missing"));
    }

    // ---- Start ----------------------------------------------------------

    [Fact]
    public void StartMatchOnlyBeginsLoadingAndBeginMatchIsWhatStartsPlaying()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        var room = manager.GetRoomById(roomId)!;

        manager.StartMatch(roomId);

        // StartMatch only calls StartLoading, which does not flip Playing, so
        // the room is still advertised as enterable while clients are loading.
        // BeginMatch is the separate step that actually starts the match.
        Assert.False(room.Playing);
        Assert.Contains(manager.CanEnterRooms(), r => r.Id == roomId);

        manager.BeginMatch(room);

        Assert.True(room.Playing);
        Assert.DoesNotContain(manager.CanEnterRooms(), r => r.Id == roomId);
    }

    [Fact]
    public void StartMatchReturnsFalseForAnUnknownRoom()
    {
        var manager = NewManager();

        Assert.False(manager.StartMatch("missing"));
    }

    [Fact]
    public void StartLoadingAloneDoesNotFlipPlayingUntilBeginMatchRuns()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        var room = manager.GetRoomById(roomId)!;

        manager.StartMatch(roomId);

        // StartLoading and GameStart are separate steps. Loading alone must not
        // mark the room as playing, otherwise clients are refused entry during
        // the loading handshake.
        Assert.False(room.Playing);

        manager.BeginMatch(room);

        Assert.True(room.Playing);
    }

    [Fact]
    public void BeginMatchIsIdempotent()
    {
        var manager = NewManager();
        var room = manager.GetRoomById(
            manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId)!;

        manager.BeginMatch(room);
        manager.BeginMatch(room);

        Assert.True(room.Playing);
    }

    [Fact]
    public void BeginMatchIgnoresNullAndUnknownRooms()
    {
        var manager = NewManager();

        manager.BeginMatch(null!);
        manager.BeginMatchForWaitRoom(null!);

        Assert.Equal(0, manager.RoomCount());
    }

    [Fact]
    public void StartAllMatchReportsSuccessAndKeepsEveryRoom()
    {
        var manager = NewManager();
        manager.CreateNewDeathMatchRoom("a", "o");
        manager.CreateNewDeathMatchRoom("b", "o");

        Assert.True(manager.StartAllMatch());

        Assert.Equal(2, manager.AllRooms().Count);
    }

    // ---- Entry listing --------------------------------------------------

    [Fact]
    public void CanEnterRoomsExcludesFullRooms()
    {
        var manager = NewManager();
        var full = manager.CreateNewDeathMatchRoom("full", "o", capacity: 1).RoomId;
        var open = manager.CreateNewDeathMatchRoom("open", "o", capacity: 2).RoomId;
        manager.EnterRoom(Guid.Parse(full), NewAccount("p-1"));

        var ids = manager.CanEnterRooms().Select(r => r.Id).ToList();

        Assert.DoesNotContain(full, ids);
        Assert.Contains(open, ids);
    }

    // ---- Teardown -------------------------------------------------------

    [Fact]
    public void RemoveRoomKeepsAPlayingRoomUnlessForced()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        manager.StartMatch(roomId);
        manager.BeginMatch(manager.GetRoomById(roomId)!);

        manager.RemoveRoom(roomId);
        Assert.Equal(1, manager.RoomCount());

        manager.RemoveRoom(roomId, forceShutdownNowPlayingRooms: true);
        Assert.Equal(0, manager.RoomCount());
    }

    [Fact]
    public void RemoveRoomIgnoresAnUnknownId()
    {
        var manager = NewManager();
        manager.CreateNewDeathMatchRoom("room", "owner-1");

        manager.RemoveRoom("missing");

        Assert.Equal(1, manager.RoomCount());
    }

    [Fact]
    public void RemoveAllRoomsKeepsPlayingRoomsByDefault()
    {
        var manager = NewManager();
        var playing = manager.CreateNewDeathMatchRoom("playing", "o").RoomId;
        var idle = manager.CreateNewDeathMatchRoom("idle", "o").RoomId;
        manager.StartMatch(playing);
        manager.BeginMatch(manager.GetRoomById(playing)!);

        manager.RemoveAllRooms();

        Assert.Equal(1, manager.RoomCount());
        Assert.NotNull(manager.GetRoomById(playing));
        Assert.Null(manager.GetRoomById(idle));
    }

    [Fact]
    public void RemoveAllRoomsForcedClearsEverything()
    {
        var manager = NewManager();
        var playing = manager.CreateNewDeathMatchRoom("playing", "o").RoomId;
        manager.StartMatch(playing);
        manager.BeginMatch(manager.GetRoomById(playing)!);

        manager.RemoveAllRooms(forceShutdownNowPlayingRooms: true);

        Assert.Equal(0, manager.RoomCount());
    }

    [Fact]
    public void AllRoomsSnapshotsTheCurrentRooms()
    {
        var manager = NewManager();
        manager.CreateNewDeathMatchRoom("a", "o");
        manager.CreateNewDeathMatchRoom("b", "o");

        var snapshot = manager.AllRooms();
        manager.RemoveAllRooms(forceShutdownNowPlayingRooms: true);

        // The snapshot must not track later removals, otherwise a caller
        // iterating it while the manager tears down would see it mutate.
        Assert.Equal(2, snapshot.Count);
    }

    // ---- Event bus ------------------------------------------------------

    [Fact]
    public void AddRoomCreatesAnEventBusForTheRoom()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;

        Assert.True(manager.roomEventBuses.ContainsKey(roomId));
    }

    [Fact]
    public void BeginMatchPublishesGameStartedOnTheRoomEventBus()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        var room = manager.GetRoomById(roomId)!;

        var started = 0;
        manager.roomEventBuses[roomId].OnGameStarted += () => started++;

        manager.BeginMatch(room);

        Assert.Equal(1, started);
    }

    [Fact]
    public void AddRoomKeepsAnExistingEventBusSoSubscriptionsSurvive()
    {
        var manager = NewManager();
        var roomId = manager.CreateNewDeathMatchRoom("room", "owner-1").RoomId;
        var bus = manager.roomEventBuses[roomId];
        var fired = 0;
        bus.OnGameStarted += () => fired++;

        var room = manager.GetRoomById(roomId)!;
        manager.AddRoom(room);
        manager.BeginMatch(room);

        Assert.Same(bus, manager.roomEventBuses[roomId]);
        Assert.Equal(1, fired);
    }

    // ---- Event bus hiding ----------------------------------------------

    [Fact]
    public void ServerBusPublishStillRaisesTheBaseEvent()
    {
        // The server bus hides PublishLoadingStart and PublishGameStart with
        // `new`, because the base methods are not virtual. When the hidden
        // version stopped at a Console.WriteLine the event was swallowed for
        // anything holding a server typed reference, so assert that reaching
        // the method through the derived type still notifies subscribers.
        var loading = 0;
        var started = 0;
        var ended = 0;
        MatchRoomEventBus bus = new MatchRoomEventBus();
        bus.OnLoadingStarted += () => loading++;
        bus.OnGameStarted += () => started++;
        bus.OnGameEnded += () => ended++;

        bus.PublishLoadingStart();
        bus.PublishGameStart();
        bus.PublishGameEnd();

        Assert.Equal(1, loading);
        Assert.Equal(1, started);
        Assert.Equal(1, ended);
    }

    [Fact]
    public void StartMatchTestWasRemovedBecauseItWasAlwaysFalse()
    {
        var manager = NewManager();
        manager.CreateNewDeathMatchRoom("room", "owner-1");

        // StartMatchTest used to iterate the rooms, start nothing, and return
        // false unconditionally. It had no callers, so it is gone rather than
        // left as a trap that looks like a way to start every match.
        Assert.Empty(typeof(MatchRoomManager)
            .GetMethods()
            .Where(m => m.Name == "StartMatchTest"));
    }

    // ---- Wait room bridge ----------------------------------------------
    // ---- Wait room bridge ----------------------------------------------

    [Fact]
    public void CreateMatchRoomByWaitRoomReturnsNullForNull()
    {
        var manager = NewManager();

        Assert.Null(manager.CreateMatchRoomByWaitRoom(null!));
    }


}
