using System;
using System.Linq;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the wait room registry: creation limits, lookup, filtering, and the
/// room listing that the lobby screen renders.
///
/// WaitRoomManager is the entry point for every lobby action, and it is where
/// the wait room half of the match start handshake begins. Nothing exercised
/// it before, so the room limit, the name fallback, and the game mode filter
/// had no regression cover at all.
///
/// Each test builds its own manager. The class also exposes a singleton, but
/// the rooms live in an instance field, so sharing that singleton would let
/// rooms leak from one test into the next.
/// </summary>
public sealed class WaitRoomManagerTests
{
    private static WaitRoomManager NewManager() => new();

    // ---- Creation -------------------------------------------------------

    [Fact]
    public void CreateWaitRoomRegistersTheRoomAndReturnsIt()
    {
        var manager = NewManager();

        var result = manager.CreateWaitRoom("lobby-room");

        Assert.Equal("Successful", result.Message);
        Assert.NotNull(result.Room);
        Assert.Equal("lobby-room", result.Room!.RoomName);
        Assert.Same(result.Room, manager.FindWaitRoom(result.Room.RoomId));
        Assert.Single(manager.GetAllRooms());
    }

    [Fact]
    public void CreateWaitRoomAppliesCapacityAndMode()
    {
        var manager = NewManager();

        var result = manager.CreateWaitRoom("room", capacity: 4, mode: EGameMode.TeamDeathMatch);

        Assert.Equal(4, result.Room!.Capacity);
        Assert.Equal(EGameMode.TeamDeathMatch, result.Room.GameMode);
    }

    [Fact]
    public void CreateWaitRoomGeneratesANameWhenTheRequestedOneIsBlank()
    {
        var manager = NewManager();

        var result = manager.CreateWaitRoom("   ");

        Assert.NotNull(result.Room);
        Assert.False(string.IsNullOrWhiteSpace(result.Room!.RoomName));
        // The generated name must differ from the blank input, otherwise the
        // room would be listed with an empty name in the lobby.
        Assert.NotEqual("   ", result.Room.RoomName);
    }

    [Fact]
    public void CreateWaitRoomGivesEachRoomADistinctId()
    {
        var manager = NewManager();

        var first = manager.CreateWaitRoom("a").Room!;
        var second = manager.CreateWaitRoom("b").Room!;

        Assert.NotEqual(first.RoomId, second.RoomId);
        Assert.Equal(2, manager.GetAllRooms().Count);
    }

    [Fact]
    public void CreateWaitRoomFallsBackToADefaultSettingWhenGivenNull()
    {
        var manager = NewManager();

        var result = manager.CreateWaitRoom((WaitRoomSetting)null!);

        Assert.Equal("Successful", result.Message);
        Assert.NotNull(result.Room);
    }

    // ---- Room limit -----------------------------------------------------

    [Fact]
    public void RoomLimitDefaultsToThirtyTwo()
    {
        var manager = NewManager();

        Assert.Equal(32, manager.RoomLimit);
    }

    [Fact]
    public void CreateWaitRoomIsRefusedOnceTheRoomLimitIsReached()
    {
        var manager = NewManager();
        manager.SetRoomLimit(2);

        Assert.Equal("Successful", manager.CreateWaitRoom("a").Message);
        Assert.Equal("Successful", manager.CreateWaitRoom("b").Message);

        var third = manager.CreateWaitRoom("c");

        Assert.Null(third.Room);
        Assert.Equal("Server RoomLimit Over", third.Message);
        Assert.Equal(2, manager.GetAllRooms().Count);
    }

    [Fact]
    public void SetRoomLimitNeverDropsBelowOne()
    {
        var manager = NewManager();

        manager.SetRoomLimit(0);
        Assert.Equal(1, manager.RoomLimit);

        manager.SetRoomLimit(-5);
        Assert.Equal(1, manager.RoomLimit);

        manager.SetRoomLimit(7);
        Assert.Equal(7, manager.RoomLimit);
    }

    [Fact]
    public void ClosingARoomFreesCapacityForANewOne()
    {
        var manager = NewManager();
        manager.SetRoomLimit(1);
        var first = manager.CreateWaitRoom("a").Room!;

        // The registry is full, so the next room has to be refused.
        Assert.Null(manager.CreateWaitRoom("b").Room);

        Assert.True(manager.CloseRoom(first.RoomId));

        Assert.Equal("Successful", manager.CreateWaitRoom("b").Message);
    }

    // ---- Lookup ---------------------------------------------------------

    [Fact]
    public void FindWaitRoomReturnsNullForUnknownAndBlankIds()
    {
        var manager = NewManager();
        manager.CreateWaitRoom("room");

        Assert.Null(manager.FindWaitRoom("missing"));
        Assert.Null(manager.FindWaitRoom(""));
        Assert.Null(manager.FindWaitRoom("   "));
        Assert.Null(manager.FindWaitRoom(null!));
    }

    [Fact]
    public void CloseRoomRemovesTheRoomAndReportsWhetherItExisted()
    {
        var manager = NewManager();
        var room = manager.CreateWaitRoom("room").Room!;

        Assert.True(manager.CloseRoom(room.RoomId));
        Assert.Empty(manager.GetAllRooms());
        Assert.Null(manager.FindWaitRoom(room.RoomId));

        Assert.False(manager.CloseRoom(room.RoomId));
    }

    // ---- Game mode filter ----------------------------------------------

    [Fact]
    public void FindWaitRoomsByGameModeKeepsOnlyMatchingRooms()
    {
        var manager = NewManager();
        manager.CreateWaitRoom("dm", mode: EGameMode.DeathMatch);
        manager.CreateWaitRoom("tdm", mode: EGameMode.TeamDeathMatch);
        manager.CreateWaitRoom("ctf", mode: EGameMode.CaptureTheFlag);

        var names = manager.FindWaitRoomsByGameMode(EGameMode.TeamDeathMatch)
            .Select(r => r.RoomName)
            .ToList();

        Assert.Equal(new[] { "tdm" }, names);
    }

    [Fact]
    public void FindWaitRoomsByGameModeWithUnknownReturnsEverything()
    {
        var manager = NewManager();
        manager.CreateWaitRoom("dm", mode: EGameMode.DeathMatch);
        manager.CreateWaitRoom("tdm", mode: EGameMode.TeamDeathMatch);

        var all = manager.FindWaitRoomsByGameMode(EGameMode.Unknown);

        // Unknown is the wildcard here, which is what the lobby needs to show
        // every room when the client has not filtered yet.
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void FindWaitRoomsByGameModeExcludesRoomsThatStartedPlaying()
    {
        var manager = NewManager();
        var room = manager.CreateWaitRoom("dm", mode: EGameMode.DeathMatch).Room!;
        manager.CreateWaitRoom("tdm", mode: EGameMode.TeamDeathMatch);

        room.AddPlayer("p-1", "Player One");
        room.GameStart();

        var names = manager.FindWaitRoomsByGameMode(EGameMode.DeathMatch)
            .Select(r => r.RoomName)
            .ToList();

        // The started room is filtered out of the mode search but is still
        // registered, because the registry only drops a room when it is closed.
        Assert.DoesNotContain("dm", names);
        Assert.Equal(2, manager.GetAllRooms().Count);
    }

    // ---- Room listing ---------------------------------------------------

    [Fact]
    public void RoomInfoReportsCountsAndListsEveryRoom()
    {
        var manager = NewManager();
        manager.SetRoomLimit(5);
        manager.CreateWaitRoom("first");
        manager.CreateWaitRoom("second");

        var info = manager.RoomInfo();

        Assert.Equal(2, info["WaitRoomCount"]?.ToObject<int>());
        Assert.Equal("2/5", info["RoomCapacity"]?.ToString());
        var rooms = info["WaitRooms"] as Newtonsoft.Json.Linq.JArray;
        Assert.NotNull(rooms);
        Assert.Equal(2, rooms!.Count);
    }

    [Fact]
    public void RoomInfoReportsTheOwnerCountAndPasswordFlag()
    {
        var manager = NewManager();
        var room = manager.CreateWaitRoom("room", capacity: 3).Room!;
        room.AddPlayer("p-1", "Player One");

        var entry = ((Newtonsoft.Json.Linq.JArray)manager.RoomInfo()["WaitRooms"]!)[0]!;

        Assert.Equal("p-1", entry["OwnerId"]?.ToString());
        Assert.Equal(1, entry["PlayerCount"]?.ToObject<int>());
        Assert.Equal(3, entry["RoomCapacity"]?.ToObject<int>());
        Assert.False(entry["HasPassword"]?.ToObject<bool>());
        Assert.Equal(room.RoomId, entry["RoomID"]?.ToString());
    }

    [Fact]
    public void RoomInfoOnlyFlagsAPasswordWhenTheSettingSuppliesOne()
    {
        var manager = NewManager();

        // RoomSetting.ApplyTo only copies the password when HasPasswordValue is
        // set, and that flag is raised by FromJson rather than by the
        // constructor. A password assigned in code therefore never reaches the
        // room, and RoomInfo reports the room as unprotected. Build the setting
        // through FromJson, the only path that carries the flag, so this test
        // covers the behaviour the lobby actually sees.
        //
        // WaitRoomSetting.FromJson is called directly: RoomSetting.FromJson
        // returns the base type, so casting its result to WaitRoomSetting
        // yields null and the room would never be created.
        var token = Newtonsoft.Json.Linq.JObject.Parse(
            "{\"RoomName\":\"locked\",\"Capacity\":4,\"GameMode\":\"DeathMatch\",\"Password\":\"secret\"}");
        var setting = WaitRoomSetting.FromJson(token);
        manager.CreateWaitRoom(setting);

        var entry = ((Newtonsoft.Json.Linq.JArray)manager.RoomInfo()["WaitRooms"]!)[0]!;

        Assert.True(entry["HasPassword"]?.ToObject<bool>());
    }

    [Fact]
    public void CodeAssignedPasswordIsNotAppliedBecauseTheFlagIsNeverSet()
    {
        var manager = NewManager();
        var setting = new WaitRoomSetting("locked", 4, EGameMode.DeathMatch) { Password = "secret" };

        manager.CreateWaitRoom(setting);

        // Recorded, not endorsed: assigning Password on a hand built setting
        // does not set HasPasswordValue, so ApplyTo skips it and the room
        // stays open. Callers that need a locked room must go through FromJson.
        var entry = ((Newtonsoft.Json.Linq.JArray)manager.RoomInfo()["WaitRooms"]!)[0]!;
        Assert.False(entry["HasPassword"]?.ToObject<bool>());
    }

    [Fact]
    public void RoomInfoOnAnEmptyRegistryListsNothing()
    {
        var manager = NewManager();

        var info = manager.RoomInfo();

        Assert.Equal(0, info["WaitRoomCount"]?.ToObject<int>());
        Assert.Equal("0/32", info["RoomCapacity"]?.ToString());
        Assert.Empty((Newtonsoft.Json.Linq.JArray)info["WaitRooms"]!);
    }

    // ---- Room state that the manager filters on -------------------------

    [Fact]
    public void GameStartBlocksAFurtherMatchStart()
    {
        var manager = NewManager();
        var room = manager.CreateWaitRoom("room").Room!;

        // An empty room cannot start, which is the guard CreateMatchRoomByWaitRoom
        // relies on before it builds a match room.
        Assert.False(room.CanStartMatch());

        room.AddPlayer("p-1", "Player One");
        Assert.True(room.CanStartMatch());

        room.GameStart();

        Assert.True(room.NowPlaying);
        Assert.False(room.CanStartMatch());
    }

    [Fact]
    public void ChangingGameModeIsRefusedWhileTheRoomIsPlaying()
    {
        var manager = NewManager();
        var room = manager.CreateWaitRoom("room", mode: EGameMode.DeathMatch).Room!;
        room.AddPlayer("p-1", "Player One");
        room.GameStart();

        room.ChangeGameMode(EGameMode.CaptureTheFlag);

        // Switching the mode mid match would leave the running room and its
        // match room describing different rules.
        Assert.Equal(EGameMode.DeathMatch, room.GameMode);
    }

    [Fact]
    public void ChangingGameModeUpdatesTheSettingWhenTheRoomIsIdle()
    {
        var manager = NewManager();
        var room = manager.CreateWaitRoom("room", mode: EGameMode.DeathMatch).Room!;

        room.ChangeGameMode(EGameMode.TeamDeathMatch);

        Assert.Equal(EGameMode.TeamDeathMatch, room.GameMode);
        Assert.NotNull(room.GetOrCreateSetting());
        Assert.Equal(EGameMode.TeamDeathMatch, room.GetOrCreateSetting().Mode);

    }

}
