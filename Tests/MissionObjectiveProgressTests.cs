using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// A mission that a clear gate can actually end.
/// <para>
/// The mission rule, the mission situation and the mission result evaluator were
/// all written and wired, and none of them could ever be reached in play: the
/// objective counter that the rule ends a match on and that the result is read out
/// of was never moved by anything. A mission therefore had exactly two ways to
/// end, the clock and running out of lives, and completing its objective was not
/// one of them.
/// </para>
/// <para>
/// This pins the whole chain down rather than the one new call: the rule, the
/// situation, the tick that checks them and the evaluator that reads the result
/// out. A fix that recorded progress into the rule without the situation or the
/// evaluator following it would leave the room settling a mission it had already
/// declared complete.
/// </para>
/// </summary>
public sealed class MissionObjectiveProgressTests
{
    private static MatchRoom NewMissionRoom(int objectiveCount = 2, int lifeCount = 3)
    {
        var setting = new MissionMatchSetting(4, "TestMission")
        {
            ObjectiveCount = objectiveCount,
            LifeCount = lifeCount
        };

        return new MatchRoom(1, "mission-room", "owner-1", setting, new MatchRoomEventBus());
    }

    private static MissionMatchSituation SituationOf(MatchRoom room)
    {
        return Assert.IsType<MissionMatchSituation>(room.CurrentSituation);
    }

    // ---- The counter, before anything reaches it ------------------------

    [Fact]
    public void AMissionStartsWithNoObjectiveProgress()
    {
        var room = NewMissionRoom();

        Assert.Equal(2, SituationOf(room).ObjectiveCount);
        Assert.Equal(0, SituationOf(room).ObjectiveProgress);
        Assert.False(SituationOf(room).IsObjectiveComplete());
    }

    [Fact]
    public void AStartedMissionIsNotFinishedOnItsOwn()
    {
        var room = NewMissionRoom();
        room.AddNewPlayer(new PlayerInfo("p1", "p1"));
        room.GameStart();

        // The clock is the only thing that has moved, so the objective is the
        // assertion: a mission must not be over before anything was done.
        Assert.True(room.Playing);
        Assert.False(room.Finished);
    }

    // ---- Recording progress --------------------------------------------

    [Fact]
    public void RecordingProgressMovesTheCounterAndTheSituationWithIt()
    {
        var room = NewMissionRoom(objectiveCount: 2);
        room.AddNewPlayer(new PlayerInfo("p1", "p1"));
        room.GameStart();

        var completed = room.RecordObjectiveProgress();

        Assert.False(completed);
        Assert.Equal(1, SituationOf(room).ObjectiveProgress);
    }

    [Fact]
    public void ReachingTheObjectiveCountReportsTheObjectiveComplete()
    {
        var room = NewMissionRoom(objectiveCount: 2);
        room.AddNewPlayer(new PlayerInfo("p1", "p1"));
        room.GameStart();

        Assert.False(room.RecordObjectiveProgress());
        Assert.True(room.RecordObjectiveProgress());

        Assert.Equal(2, SituationOf(room).ObjectiveProgress);
        Assert.True(SituationOf(room).IsObjectiveComplete());
    }

    [Fact]
    public void ProgressCannotPassTheObjectiveCount()
    {
        // Two clients reaching the same gate, or one reporting more than it did,
        // used to be able to push the count past what the mission asked for. The
        // result screen then showed an objective bigger than the objective.
        var room = NewMissionRoom(objectiveCount: 2);
        room.AddNewPlayer(new PlayerInfo("p1", "p1"));
        room.GameStart();

        room.RecordObjectiveProgress();
        room.RecordObjectiveProgress(50);

        Assert.Equal(2, SituationOf(room).ObjectiveProgress);
    }

    [Fact]
    public void ASecondReportAfterTheObjectiveIsMetChangesNothing()
    {
        var room = NewMissionRoom(objectiveCount: 1);
        room.AddNewPlayer(new PlayerInfo("p1", "p1"));
        room.GameStart();

        Assert.True(room.RecordObjectiveProgress());
        Assert.False(room.RecordObjectiveProgress());

        Assert.Equal(1, SituationOf(room).ObjectiveProgress);
    }

    [Fact]
    public void RecordingProgressOnANonMissionRoomIsRefused()
    {
        // The same call has to be harmless in every other mode. A gate firing in a
        // death match must not end it.
        var room = new MatchRoom(1, "dm-room", "owner-1", new DeathMatchSetting(20, true), new MatchRoomEventBus());
        room.AddNewPlayer(new PlayerInfo("p1", "p1"));
        room.GameStart();

        Assert.False(room.RecordObjectiveProgress());
        Assert.False(room.Finished);
    }

    // ---- The tick and the result ---------------------------------------

    [Fact]
    public void TheTickEndsTheMissionOnceTheObjectiveIsDone()
    {
        var room = NewMissionRoom(objectiveCount: 1);
        room.AddNewPlayer(new PlayerInfo("p1", "p1"));
        room.GameStart();

        room.RecordObjectiveProgress();
        room.GameUpdate();

        Assert.True(room.Finished);
    }

    [Fact]
    public void TheMissionIsNotEndedByATickThatProgressDidNotFinish()
    {
        var room = NewMissionRoom(objectiveCount: 2);
        room.AddNewPlayer(new PlayerInfo("p1", "p1"));
        room.GameStart();

        room.RecordObjectiveProgress();
        room.GameUpdate();

        Assert.False(room.Finished);
    }

    [Fact]
    public void ACompletedMissionIsReportedAsCleared()
    {
        var bus = new MatchRoomEventBus();
        JObject? published = null;
        bus.OnGameEndedWithResult += result => published = result;

        var setting = new MissionMatchSetting(4, "TestMission") { ObjectiveCount = 1, LifeCount = 3 };
        var room = new MatchRoom(1, "mission-room", "owner-1", setting, bus);
        room.AddNewPlayer(new PlayerInfo("p1", "p1") { LifeCount = 2 });
        room.GameStart();

        room.RecordObjectiveProgress();
        room.GameUpdate();

        Assert.True(room.Finished);
        Assert.NotNull(published);
        Assert.Equal("Clear", published!["Result"]?.ToString());
        Assert.True(published["Success"]?.ToObject<bool>() ?? false);
        Assert.Equal("Mission", published["ResultType"]?.ToString());
        Assert.Equal(1, published["ObjectiveProgress"]?.ToObject<int>() ?? 0);
        Assert.Equal(1, published["ObjectiveCount"]?.ToObject<int>() ?? 0);
    }

    [Fact]
    public void AMissionLeftOnTheClockIsNotReportedAsCleared()
    {
        // The counter and the outcome have to agree. A mission that ran out of time
        // without the objective is a TimeUp and not a Clear, and that is the
        // difference between finishing a mission and being let out of one.
        //
        // Success stays true here on purpose: it means the party was not beaten by
        // running out of lives, which running out of time is not. Asserting the
        // Clear is what would be wrong.
        var evaluator = MatchResultEvaluatorFactory.CreateEvaluator(EGameMode.Mission);

        var result = evaluator.Evaluate(
            new MissionMatchSituation { ObjectiveCount = 2, ObjectiveProgress = 1, LifeRemaining = 2 },
            new List<PlayerInfo> { new PlayerInfo("p1", "p1") { LifeCount = 2 } });

        Assert.Equal("TimeUp", result["Result"]?.ToString());
        Assert.False(result["ObjectiveCompleted"]?.ToObject<bool>() ?? true);
    }

    [Fact]
    public void RunningOutOfLivesIsReportedAsAFailure()
    {
        var evaluator = MatchResultEvaluatorFactory.CreateEvaluator(EGameMode.Mission);

        var result = evaluator.Evaluate(
            new MissionMatchSituation { ObjectiveCount = 2, ObjectiveProgress = 0, LifeRemaining = 0 },
            new List<PlayerInfo> { new PlayerInfo("p1", "p1") { LifeCount = 0 } });

        Assert.Equal("Fail", result["Result"]?.ToString());
        Assert.False(result["Success"]?.ToObject<bool>() ?? true);
    }
}