using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Plays a flag through its whole life over the realtime transport.
/// <para>
/// This is where CTF was not playable at all. The client sent the flag events,
/// but only the ones it built itself for its own local book keeping, and the
/// server's flag handlers were reached by nothing. The score was the clearest
/// symptom: the client counted a capture and pushed the number, the server
/// recomputed from state it had never been told about, and told everybody zero.
/// </para>
/// <para>
/// These drive the real handler, so a claim that is not honoured or a ruling that
/// is not sent fails here rather than being noticed in a match.
/// </para>
/// </summary>
[Collection("RealtimeSocket")]
public sealed class MatchRudpFlagTests : IDisposable
{
    private const string RedPlayerId = "flag-red";
    private const string BluePlayerId = "flag-blue";

    private MatchRoom room = null!;
    private string roomId = string.Empty;

    public void Dispose()
    {
        if (!string.IsNullOrWhiteSpace(roomId))
        {
            InGameMatchEventHandler.ClearRoomFlagState(roomId);
            MatchRoomManager.Instance.RemoveRoom(roomId, forceShutdownNowPlayingRooms: true);
        }
    }

    private void SetUpRoom(bool redHasPosition = true)
    {
        var created = MatchRoomManager.Instance.CreateNewCTFMatchRoom("rudp-flag", RedPlayerId, capacity: 4);
        roomId = created.RoomId;
        room = MatchRoomManager.Instance.GetRoomById(roomId) as MatchRoom;
        Assert.NotNull(room);

        room!.AddNewPlayer(new PlayerInfo(RedPlayerId, "Red") { Team = ETeam.Red });
        room.AddNewPlayer(new PlayerInfo(BluePlayerId, "Blue") { Team = ETeam.Blue });
        room.GameStart();

        // A pickup is measured against the server's own record of where the
        // player is, so a player needs a position the server will accept. One who
        // has never reported one still reads as the origin, which is where the
        // flag is, so a reach check would pass by accident and hide the one that
        // should fail.
        var lag = MatchServerV2.Instance.ServerLagCompensationManager;
        lag.StartMatch(roomId);
        lag.AddPlayer(RedPlayerId);
        lag.AddPlayer(BluePlayerId);
        if (redHasPosition)
        {
            lag.SetPlayerPosition(RedPlayerId, 0f, 0f, 0f);
        }

        lag.SetPlayerPosition(BluePlayerId, 0f, 0f, 0f);
    }

    /// <summary>
    /// Sends a claim the way a client does, carrying the room and player fields
    /// the listener would otherwise stamp on for it.
    /// <para>
    /// The flag's own team travels with the claim, because a flag belongs to a
    /// team and a red player picks up the blue flag. A message carrying only the
    /// carrier's team is saying which side the carrier is on, which is a
    /// different fact.
    /// </para>
    /// </summary>
    private static void Claim(string playerId, string roomId, string messageType, ETeam? flagTeam = null)
    {
        ClaimClaiming(playerId, roomId, messageType, flagTeam, carrierId: null);
    }

    /// <summary>
    /// The same claim, naming the player it says is the one holding the flag.
    /// <para>
    /// A client that does not name a carrier is claiming for itself, which is
    /// the only evidence there is. A client that names one is claiming on behalf
    /// of somebody, which is what a client reporting its own copy of a peer's
    /// player looks like, and the two are not the same statement.
    /// </para>
    /// </summary>
    private static void ClaimClaiming(
        string playerId,
        string roomId,
        string messageType,
        ETeam? flagTeam,
        string? carrierId)
    {
        var json = new JObject
        {
            ["MessageType"] = messageType,
            ["PlayerID"] = playerId,
            ["PlayerId"] = playerId,
            ["RoomID"] = roomId,
            ["RoomId"] = roomId
        };

        if (flagTeam.HasValue)
        {
            json["FlagTeam"] = flagTeam.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(carrierId))
        {
            json["CarrierId"] = carrierId;
        }

        InGameMatchEventHandler.HandleUdpGameEvent(
            Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None)),
            "127.0.0.1:0");
    }

    private IReadOnlyDictionary<ETeam, TeamFlag> Flags() =>
        InGameMatchEventHandler.GetFlagStates(roomId);

    /// <summary>
    /// Walks the red player away from the origin, one unit at a time.
    /// <para>
    /// The server only adopts a position within a tolerance of the one it holds,
    /// and a large jump at a fixed delta is outside that while a small step is
    /// inside it. Setting the position directly would skip the code the transport
    /// runs, and a test that skips it is not testing the path.
    /// </para>
    /// </summary>
    private void MoveAway(float x, float y)
    {
        var lag = MatchServerV2.Instance.ServerLagCompensationManager;
        var state = lag.GetPlayerState(RedPlayerId);
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(
            ((x - state.PositionX) * (x - state.PositionX)) +
            ((y - state.PositionY) * (y - state.PositionY)))));

        for (var i = 1; i <= steps; i++)
        {
            var t = (float)i / steps;
            lag.SetPlayerPosition(RedPlayerId, state.PositionX + ((x - state.PositionX) * t), state.PositionY, 0f);
        }
    }

    // ---- The flag itself -------------------------------------------------

    [Fact]
    public void APickupPutsTheFlagInTheCarriersHandsOnTheServer()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        // A red player picks up the blue flag. The state has to be the flag's
        // and not the carrier's, or a team ends up recorded as holding its own
        // flag and the capture rule is judged against the wrong object.
        Assert.Equal(EFlagState.FlagCapturedPlayer, Flags()[ETeam.Blue].State);
        Assert.Equal(RedPlayerId, Flags()[ETeam.Blue].CarrierId);
        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Red].State);
    }

    [Fact]
    public void APlayerCannotPickUpItsOwnTeamsFlag()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Red);
        });

        // A team recovering its own flag is a return, not a pickup, and treating
        // it as a pickup would let a team pick up a flag off its own stand and
        // walk it somewhere.
        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Red].State);
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagPickup));
    }

    [Fact]
    public void AFlagIsOneObjectSoTwoPlayersCannotBothHoldIt()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);

            // A second red player, on its own connection, claims the same flag.
            // The other claim in here is the blue player picking up the red flag,
            // which is a different flag and is meant to succeed.
            Claim("other-red", roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        // The flag is one object, so the second claim names a flag that is not on
        // the ground. The server used to record a carrier without asking whether
        // the flag was available, so the last claimant simply won.
        Assert.Equal(RedPlayerId, Flags()[ETeam.Blue].CarrierId);
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagPickup));
    }

    [Fact]
    public void ALostFlagGoesOnTheGroundAndStaysTheCarriersOwn()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagLost, ETeam.Blue);
        });

        // A carrier who dies drops the flag. It is on the ground, and it is still
        // the blue team's flag: the red player was only ever borrowing it.
        Assert.Equal(EFlagState.FlagOnGround, Flags()[ETeam.Blue].State);
        Assert.Null(Flags()[ETeam.Blue].CarrierId);
    }

    [Fact]
    public void APlayerHoldingNothingCannotClaimToHaveDroppedAFlag()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagLost, ETeam.Blue);
        });

        // The claim is about a carrier, and this player is not one. Accepting it
        // would let a player put a flag on the ground that was never picked up.
        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Blue].State);
    }

    [Fact]
    public void AFlagIsPickedUpOffTheGroundAgain()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagLost, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        // The rule says a dropped flag can be picked up again, by a teammate to
        // keep carrying it or by the other side to return it.
        Assert.Equal(EFlagState.FlagCapturedPlayer, Flags()[ETeam.Blue].State);
        Assert.Equal(RedPlayerId, Flags()[ETeam.Blue].CarrierId);
    }

    [Fact]
    public void AFriendlyRecoveryRestoresTheFlagWithoutScoring()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(BluePlayerId, roomId, GameMessageTypes.FlagReturn, ETeam.Blue);
        });

        // A return is a state reset and not a score, which is what the mode rule
        // says. The server previously removed a carrier record and called it a
        // return, so the flag had no state afterwards at all.
        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Blue].State);
        Assert.Equal(0, room!.GetFlagScore(ETeam.Red));
        Assert.Equal(0, room.GetFlagScore(ETeam.Blue));
    }

    [Fact]
    public void AReturnSaysWhySoAFlagThatCameBackByItselfIsNotMistakenForARecovery()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(BluePlayerId, roomId, GameMessageTypes.FlagReturn, ETeam.Blue);
        });

        var ruling = recorder.TheRuling(GameMessageTypes.FlagReturn);
        Assert.Equal(nameof(ETeam.Blue), ruling["Team"]?.ToString());
        Assert.Equal(nameof(EFlagReturnReason.FriendlyRecovered), ruling["ReturnReason"]?.ToString());
    }

    [Fact]
    public void APlayerWhoLeavesPutsTheFlagTheyWereCarryingBack()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        InGameMatchEventHandler.ClearPlayerState(RedPlayerId);

        // A player who left cannot be holding a flag. The carrier record used to
        // be dropped, which left the flag itself neither home, nor dropped, nor
        // carried: it had no state at all, so nothing could be judged about it.
        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Blue].State);
        Assert.Null(Flags()[ETeam.Blue].CarrierId);
    }

    // ---- The capture rule ------------------------------------------------

    [Fact]
    public void ADeliveryScoresWhileTheCarriersOwnFlagIsHome()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagCaptured, ETeam.Blue);
        });

        // This is the whole point of the mode: red carried the blue flag home
        // while red's own flag was untouched, so red scores.
        Assert.Single(recorder.RulingsOfType(GameMessageTypes.FlagCaptured));
        Assert.Equal(1, room!.GetFlagScore(ETeam.Red));
    }

    [Fact]
    public void ADeliveryDoesNotScoreWhileTheCarriersOwnFlagIsInEnemyHands()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());

            // Blue takes red's flag first, then red tries to deliver blue's.
            Claim(BluePlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Red);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagCaptured, ETeam.Blue);
        });

        // Red's own flag is in blue's hands, so the delivery does not stand. The
        // rule says a team scores only while its own flag is at base, and this
        // half was not checked at all: a capture was believed as long as the
        // player had once claimed a pickup.
        Assert.Equal(0, room!.GetFlagScore(ETeam.Red));
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagCaptured));

        var refusal = recorder.TheAnswerTo(RedPlayerId, GameMessageTypes.FlagCaptureRefused);
        Assert.Equal(nameof(EFlagRefusal.OwnFlagNotAtBase), refusal["Reason"]?.ToString());
    }

    [Fact]
    public void ARefusedDeliveryLeavesTheFlagAloneRatherThanDestroyingIt()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(BluePlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Red);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagCaptured, ETeam.Blue);
        });

        // A delivery that does not score changes nothing. The red player is still
        // carrying the blue flag, which is where they actually are.
        Assert.Equal(EFlagState.FlagCapturedPlayer, Flags()[ETeam.Blue].State);
        Assert.Equal(RedPlayerId, Flags()[ETeam.Blue].CarrierId);
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagBurst));
    }

    [Fact]
    public void APlayerWhoIsNotHoldingTheFlagCannotClaimToHaveDeliveredIt()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);

            // A different red player, on their own connection, claims the
            // delivery. The flag is in somebody else's hands.
            Claim("other-red", roomId, GameMessageTypes.FlagCaptured, ETeam.Blue);
        });

        // A delivery is a statement about who is holding the flag, and a message
        // from somebody who is not holding it is not that statement.
        Assert.Equal(0, room!.GetFlagScore(ETeam.Red));
        Assert.Equal(RedPlayerId, Flags()[ETeam.Blue].CarrierId);
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagCaptured));
    }

    [Fact]
    public void AScoredCaptureSaysWhoseFlagWasDestroyedAndPutsBothFlagsBack()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagCaptured, ETeam.Blue);
        });

        // The flag red carried belonged to blue, so blue's flag is the one that
        // is gone. A client used to claim this about itself and it was dropped,
        // so the flag the server destroyed was a flag nobody heard about.
        var burst = Assert.Single(recorder.RulingsOfType(GameMessageTypes.FlagBurst));
        Assert.Equal(nameof(ETeam.Blue), burst["Team"]?.ToString());

        // The rule resets the flag state when a team scores. Without this the
        // captured flag is still gone and the next delivery has nothing to carry.
        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Red].State);
        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Blue].State);
    }

    [Fact]
    public void ARepeatedDeliveryDoesNotScoreOrBurstAgain()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagCaptured, ETeam.Blue);
        });

        // The first delivery put both flags back on their stands, so the second
        // has no flag in hand to deliver. One message cannot score twice.
        Assert.Single(recorder.RulingsOfType(GameMessageTypes.FlagCaptured));
        Assert.Single(recorder.RulingsOfType(GameMessageTypes.FlagBurst));
        Assert.Equal(1, room!.GetFlagScore(ETeam.Red));
    }

    [Fact]
    public void AClientCannotClaimAPickupOnBehalfOfSomebodyElse()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            ClaimClaiming(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue, "other-red");
        });

        // Every client simulates every player, so a flag standing in the world
        // fires its trigger on each of them and each names whoever their own copy
        // says is carrying it. Whichever report landed first used to win, so a
        // player who never went near the flag ended up recorded as carrying it,
        // and the flag the server then believed in was one a real carrier's death
        // could not drop.
        Assert.Equal(RedPlayerId, Flags()[ETeam.Blue].CarrierId);
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagPickup));
    }

    [Fact]
    public void ADroppedFlagGoesHomeByItselfSoNobodyIsStuckWithoutOne()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagLost, ETeam.Blue);
        });

        Assert.Equal(EFlagState.FlagOnGround, Flags()[ETeam.Blue].State);

        // The server holds the flag, so the server keeps the time. It used to be
        // the only side with a timer, so a dropped flag came back only if some
        // client lived long enough to say so, and one that did not stayed on the
        // ground for the rest of the match with its team unable to score.
        var flags = InGameMatchEventHandler.GetFlagStates(roomId);
        var blue = flags[ETeam.Blue];
        blue.AutoReturnSeconds = 0.1f;

        System.Threading.Thread.Sleep(200);
        InGameMatchEventHandler.ReturnTimedOutFlags(room!);

        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Blue].State);
    }

    [Fact]
    public void AFlagPickedUpBeforeItsDeadlineKeepsIt()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagLost, ETeam.Blue);
        });

        var flags = InGameMatchEventHandler.GetFlagStates(roomId);
        flags[ETeam.Blue].AutoReturnSeconds = 0.1f;

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        System.Threading.Thread.Sleep(200);
        InGameMatchEventHandler.ReturnTimedOutFlags(room!);

        // The clock stops when the flag is taken again, so a carrier who gets to
        // it in time keeps it rather than losing it to a timer that never noticed
        // it had moved.
        Assert.Equal(EFlagState.FlagCapturedPlayer, Flags()[ETeam.Blue].State);
        Assert.Equal(RedPlayerId, Flags()[ETeam.Blue].CarrierId);
    }

    [Fact]
    public void AFlagOnTheGroundCannotBeTakenFromAcrossTheMap()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagLost, ETeam.Blue);
        });

        // The flag lies where the carrier was standing, which is the server's
        // own record of the position rather than anything the client said.
        Assert.Equal(EFlagState.FlagOnGround, Flags()[ETeam.Blue].State);

        MoveAway(200f, 0f);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        // A pickup is the one flag message with nothing to check it against: a
        // field item is refused unless the server's position for the player is
        // near the spawn, and a shot is re-derived rather than believed. Without
        // the same here, a client can assert that it picked up a flag standing
        // safely on the other team's stand, and that team can never score.
        Assert.Equal(EFlagState.FlagOnGround, Flags()[ETeam.Blue].State);
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagPickup));
    }

    [Fact]
    public void AFlagNextToThePlayerCanBeTaken()
    {
        SetUpRoom();

        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(new BroadcastRecorder());
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagLost, ETeam.Blue);
        });

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        // The reach check must not stop the ordinary case, or the flag can never
        // be picked up again and the mode is unplayable in the other direction.
        Assert.Equal(EFlagState.FlagCapturedPlayer, Flags()[ETeam.Blue].State);
        Assert.Single(recorder.RulingsOfType(GameMessageTypes.FlagPickup));
    }

    [Fact]
    public void APlayerWithNoPositionCannotClaimAFlag()
    {
        // Nothing has reported a position for red, so red still reads as the
        // origin, which is where the flag is. Checking the claim on that would
        // let a player the server has never seen take a flag off its stand.
        SetUpRoom(redHasPosition: false);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagPickup, ETeam.Blue);
        });

        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Blue].State);
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagPickup));
    }

    [Fact]
    public void AClientCannotSayAFlagBurst()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagBurst);
        });

        // A flag going is the rule's outcome, not something a client asserts. A
        // client that could say it would destroy a flag sitting safely on its own
        // stand, and would see an effect nobody else saw.
        Assert.Empty(recorder.RulingsOfType(GameMessageTypes.FlagBurst));
        Assert.Equal(EFlagState.FlagOnStand, Flags()[ETeam.Red].State);
    }

    [Fact]
    public void AClientCannotPushItsOwnScore()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);

            // The client used to count a capture itself and push the number. The
            // server recomputed from state it had never been told about, so the
            // room was told zero and the two sides disagreed about the match.
            Claim(RedPlayerId, roomId, GameMessageTypes.FlagScoreUpdate);
        });

        var score = recorder.TheRuling(GameMessageTypes.FlagScoreUpdate);
        Assert.Equal(0, score["RedTeamScore"]?.Value<int>());
        Assert.Equal(0, score["BlueTeamScore"]?.Value<int>());
        Assert.Equal(0, room!.GetFlagScore(ETeam.Red));
    }
}
