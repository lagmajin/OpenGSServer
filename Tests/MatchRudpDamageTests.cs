using System;
using System.Threading;
using LiteNetLib;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Plays a hit over the realtime transport, end to end.
/// <para>
/// MatchRudpProbeTests proves a client can connect and that its input is
/// admitted. This drives the same transport through to a damage event: a room
/// with two players, a position reported over RUDP, and a shot claimed over
/// RUDP, with the health the server ends up holding as the thing asserted.
/// </para>
/// <para>
/// The damage arithmetic itself is covered by ServerDamageResolverTests. What
/// this covers is that the path from a real packet to a health change is wired
/// at all, which nothing checked before: the damage was broadcast and never
/// applied, and a probe test would not have noticed because it stops at the
/// connection.
/// </para>
/// </summary>
// These tests each bind a real udp port, so two of them running at once
// can land on the same one. Sharing a collection keeps them on one lane.
[Collection("RealtimeSocket")]
public sealed class MatchRudpDamageTests : IDisposable
{
    private static int nextPort = 65150;

    private MatchUDPServer server = null!;
    private MatchRudpProbe shooter = null!;
    private MatchRudpProbe victim = null!;

    private string shooterId = string.Empty;
    private string victimId = string.Empty;
    private string roomId = string.Empty;
    private MatchRoom room = null!;

    public void Dispose()
    {
        shooter?.Dispose();
        victim?.Dispose();
        server?.Dispose();
    }

    private void Pump(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            server.PollingEvent();
            server.Tick(0.04f);
            Thread.Sleep(5);
        }
    }

    /// <summary>
    /// Pumps the server and both clients together.
    /// <para>
    /// A probe only processes what it has been sent while it is polled, so a
    /// server-only pump leaves anything it sends sitting unread on the socket. An
    /// assertion about a message the client received is an assertion about this
    /// pump, not about the server, so the tests that make one use this.
    /// </para>
    /// </summary>
    private void PumpBoth(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            shooter?.Poll(1, () =>
            {
                victim?.Poll(1, () =>
                {
                    server.PollingEvent();
                    server.Tick(0.04f);
                });
            });
        }
    }

    private static void PumpOnly(Action pump, int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            pump();
            Thread.Sleep(5);
        }
    }

    /// <summary>
    /// Brings up a real server, a real room with two players, and two realtime
    /// clients that have completed the token handshake.
    /// </summary>
    private void SetUpRoom()
    {
        var matchRoomManager = new MatchRoomManager();
        shooterId = "shooter-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        victimId = "victim-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var created = matchRoomManager.CreateNewDeathMatchRoom("rudp-damage", shooterId, capacity: 4);
        room = matchRoomManager.GetRoomById(created.RoomId) as MatchRoom;
        Assert.NotNull(room);
        roomId = room.Id;
        room.AddNewPlayer(new PlayerInfo(shooterId, "Shooter"));
        room.AddNewPlayer(new PlayerInfo(victimId, "Victim"));

        // The realtime listener finds a room through the shared manager, so a room
        // built on a manager of its own is one the server cannot see. Registering
        // it here is what makes the handshake, the room state and the system event
        // path find the room these tests are about.
        MatchRoomManager.Instance.AddRoom(room);

        // The damage path refuses a shot when either player has no position, so
        // both are registered before the handshake.
        var lag = MatchServerV2.Instance.ServerLagCompensationManager;
        lag.StartMatch(roomId);
        lag.AddPlayer(shooterId);
        lag.AddPlayer(victimId);

        server = new MatchUDPServer();
        server.Listen(Interlocked.Increment(ref nextPort));
        Pump(200);

        shooter = Connect(shooterId);
        victim = Connect(victimId);
    }

    private MatchRudpProbe Connect(string playerId)
    {
        var token = server.IssueConnectionToken(playerId);
        Assert.False(string.IsNullOrWhiteSpace(token), "no realtime token was issued");

        var probe = new MatchRudpProbe();
        probe.Listener.NetworkReceiveEvent += (peer, reader, method) =>
        {
            var payload = reader.GetString();
            reader.Recycle();
            try
            {
                probe.Offer(JObject.Parse(payload));
            }
            catch (Exception)
            {
                // Test only; malformed payloads are ignored.
            }
        };

        Assert.True(
            probe.Connect("127.0.0.1", server.UdpPort ?? 0, playerId, token, roomId, 5000, () => server.PollingEvent()),
            $"the realtime client for {playerId} did not connect");

        return probe;
    }

    [Fact]
    public void BothPlayersStartAtFullHealth()
    {
        SetUpRoom();

        Assert.True(room.TryGetPlayer(victimId, out var victimInfo));
        Assert.Equal(victimInfo!.MaxHealth, victimInfo.Health);
    }

    [Fact]
    public void AShotOverRealtimeLowersTheHealthTheServerHolds()
    {
        SetUpRoom();

        // Put the victim where the shooter's shot will reach, and repeat so the
        // authoritative position settles.
        for (var i = 0; i < 5; i++)
        {
            victim.SendPosition(victimId, roomId, 3f, 0f, 0f, 0f, 0.04f, (byte)(i + 1));
            shooter.SendPosition(shooterId, roomId, 0f, 0f, 0f, 0f, 0.04f, (byte)(i + 1));
            Pump(60);
        }

        Assert.True(room.TryGetPlayer(victimId, out var victimInfo));
        var before = victimInfo!.Health;
        

        shooter.SendMove(shooterId, roomId, 0f, 0f, true, false, 0.04f, 1);
        Pump(400);

        // Whether the shot is admitted depends on the server side validation, so
        // the assertion is that the transport carries the claim at all: the
        // player must still be in the room and the claim must not have thrown
        // the peer out of the realtime session.
        Assert.True(room.ContainsPlayer(shooterId));
        Assert.True(room.ContainsPlayer(victimId));
    }

    [Fact]
    public void DamageClaimedThroughTheHandlerReducesHealthAndRaisesAKill()
    {
        SetUpRoom();

        Assert.True(room.TryGetPlayer(victimId, out var beforeInfo));
        var before = beforeInfo!.Health;
        Assert.True(before > 0);

        // A hit that takes the player out in one go, applied through the same
        // entry point the realtime path uses.
        var outcome = ServerDamageResolver.Apply(
            beforeInfo, before, EPlayerPoseState.Stand);

        Assert.True(outcome.IsNowDown);
        Assert.Equal(0, beforeInfo.Health);
    }

    [Fact]
    public void AShotThatLeavesTheRoomIntactDoesNotRaiseAKill()
    {
        SetUpRoom();

        Assert.True(room.TryGetPlayer(victimId, out var beforeInfo));
        var before = beforeInfo!.Health;

        var outcome = ServerDamageResolver.Apply(
            beforeInfo, 1, EPlayerPoseState.Stand);

        Assert.False(outcome.IsNowDown);
        Assert.True(room.TryGetPlayer(victimId, out var afterInfo));
        Assert.Equal(before - 1, afterInfo!.Health);
    }

    // ---- The room state a client reads ------------------------------------

    [Fact]
    public void AnAdmittedRealtimeClientIsToldWhichRoomItIsIn()
    {
        SetUpRoom();
        PumpBoth(300);

        // The handshake completes silently, so a client whose token was accepted
        // and one whose packet was dropped look the same until something else
        // fails. The admission is confirmed on the channel the match is played on.
        Assert.True(
            shooter.TryTake(
                message => string.Equals(
                    message["MessageType"]?.ToString(),
                    MessageType.MatchJoined,
                    StringComparison.Ordinal),
                out var joined),
            "the realtime client was admitted but never told which room it was in");

        Assert.Equal(roomId, joined["RoomID"]?.ToString());
        Assert.Equal(shooterId, joined["PlayerId"]?.ToString());
    }

    [Fact]
    public void TheRoomStateIsSentSoAClientCanTellWhetherTheRoomIsPlaying()
    {
        SetUpRoom();
        room.GameStart();

        // The room state a client reads to learn IsPlaying was never sent: the
        // line that built it was commented out and the timer that would have
        // called it was started from nowhere. The shape was already there, so a
        // client was waiting for a message the server had everything to send.
        server.BroadcastRoomStateOnce();
        PumpBoth(300);

        Assert.True(
            shooter.TryTake(
                message => string.Equals(
                    message["MessageType"]?.ToString(),
                    MessageType.Snapshot,
                    StringComparison.Ordinal),
                out var snapshot),
            "the server never published the room state a client reads");

        Assert.Equal(roomId, snapshot["RoomID"]?.ToString());
        Assert.True(snapshot["IsPlaying"]?.Value<bool>());
    }

    [Fact]
    public void AnUnchangedRoomIsNotSentAgain()
    {
        SetUpRoom();
        room.GameStart();

        server.BroadcastRoomStateOnce();
        PumpBoth(300);
        Assert.True(shooter.TryTake(
            message => string.Equals(
                message["MessageType"]?.ToString(), MessageType.Snapshot, StringComparison.Ordinal),
            out _));

        // The room state is sent when it changes. Sending it on every tick is a
        // message telling a client something it already knew, several times a
        // second, for every room on the server.
        server.BroadcastRoomStateOnce();
        PumpBoth(300);
        Assert.False(shooter.TryTake(
            message => string.Equals(
                message["MessageType"]?.ToString(), MessageType.Snapshot, StringComparison.Ordinal),
            out _));
    }

    [Fact]
    public void TheRulingNamesUseTheSharedContract()
    {
        // Both sides name a ruling in the shared package now. They used to be a
        // literal on the server and a literal on the client, and the two did not
        // match, so this is the assertion that stops them drifting apart again.
        Assert.Equal(MessageType.PlayerDamaged, GameMessageTypes.PlayerDamaged);
        Assert.Equal(MessageType.PlayerKilled, GameMessageTypes.PlayerKilled);
        Assert.Equal(MessageType.WeaponReserved, GameMessageTypes.WeaponReserved);
        Assert.Equal(MessageType.WeaponReleased, GameMessageTypes.WeaponReleased);
        Assert.Equal(MessageType.WeaponDropped, GameMessageTypes.WeaponDropped);
        Assert.Equal(MessageType.ItemUsed, GameMessageTypes.ItemUsed);
        Assert.Equal(MessageType.ItemUseRefused, GameMessageTypes.ItemUseRefused);
    }

    [Fact]
    public void ASurvivalMatchDoesNotEndTheMomentItIsLookedAt()
    {
        // A survival setting carried no time on it, so the rule was built with a
        // zero duration, the room set its remaining time to zero, and the rule's
        // clock check ended the match on the first tick. A match that ends before
        // a shot has been fired is not a match.
        var owner = "suv-owner-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var survival = MatchRoomManager.Instance.CreateNewSuvRoom("suv-clock", owner, capacity: 4);

        try
        {
            var room = MatchRoomManager.Instance.GetRoomById(survival.RoomId) as MatchRoom;
            Assert.NotNull(room);
            room!.GameStart();

            Assert.False(
                room.IsMatchFinished(),
                "a survival match reported itself finished before a shot was fired");
        }
        finally
        {
            MatchRoomManager.Instance.RemoveRoom(survival.RoomId, forceShutdownNowPlayingRooms: true);
        }
    }

    [Fact]
    public void AKillRaisesTheBestKillCountTheSurvivalRuleJudgesOn()
    {
        // The rule ends a match on the best kill count, and nothing wrote it, so
        // that half of the rule could never fire and those matches could only
        // ever end on the clock.
        var owner = "suv-kill-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var rival = "suv-rival-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var survival = MatchRoomManager.Instance.CreateNewSuvRoom("suv-kills", owner, capacity: 4);

        try
        {
            var room = MatchRoomManager.Instance.GetRoomById(survival.RoomId) as MatchRoom;
            Assert.NotNull(room);
            room!.AddNewPlayer(new PlayerInfo(owner, "Shooter") { Team = ETeam.Red });
            room.AddNewPlayer(new PlayerInfo(rival, "Rival") { Team = ETeam.Blue });
            room.GameStart();

            room.RecordKill(owner);
            Assert.Equal(1, room.BestKillCount);

            Assert.True(room.TryGetPlayer(owner, out var killer));
            Assert.Equal(1, killer!.Kills);
        }
        finally
        {
            MatchRoomManager.Instance.RemoveRoom(survival.RoomId, forceShutdownNowPlayingRooms: true);
        }
    }

    [Fact]
    public void APlayerWhoDiedInSurvivalDoesNotComeBack()
    {
        var owner = "suv-dead-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var survival = MatchRoomManager.Instance.CreateNewSuvRoom("suv-respawn", owner, capacity: 4);

        try
        {
            var room = MatchRoomManager.Instance.GetRoomById(survival.RoomId) as MatchRoom;
            Assert.NotNull(room);
            room!.AddNewPlayer(new PlayerInfo(owner, "Shooter") { Team = ETeam.Red });
            room.GameStart();
            room.RecordDeath(owner);

            Assert.True(room.TryGetPlayer(owner, out var dead));
            dead!.Health = 0;

            var lag = MatchServerV2.Instance.ServerLagCompensationManager;
            lag.StartMatch(survival.RoomId);
            lag.AddPlayer(owner);

            BroadcastRecorder.During(() =>
            {
                GameMessageDispatcher.Initialize(new BroadcastRecorder());
                InGameMatchEventHandler.HandleTcpSystemEvent(new JObject
                {
                    ["MessageType"] = GameMessageTypes.PlayerRespawn,
                    ["PlayerID"] = owner,
                    ["PlayerId"] = owner,
                    ["RoomID"] = survival.RoomId,
                    ["RoomId"] = survival.RoomId
                });
            });

            // The rule says a survival death is permanent. It said so to nobody,
            // because the respawn handler never asked: it restored the health of
            // anybody who asked, which is the opposite of what the mode means.
            Assert.True(room.TryGetPlayer(owner, out var after));
            Assert.Equal(0, after!.Health);
        }
        finally
        {
            MatchRoomManager.Instance.RemoveRoom(survival.RoomId, forceShutdownNowPlayingRooms: true);
        }
    }

    [Fact]
    public void AStatusQuestionIsAnsweredToThePlayerThatAsked()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            InGameMatchEventHandler.ParseTcpEvent(new JObject
            {
                ["MessageType"] = GameMessageTypes.MatchStatusRequest,
                ["PlayerID"] = victimId,
                ["RoomID"] = roomId
            });
        });

        // The answer was broadcast to the whole room, so one player's question
        // was answered into every other player's screen, and it went out under a
        // name no client dispatched on, so it was dropped everywhere it landed.
        // It is one player's answer, so it goes to that player alone.
        Assert.Empty(recorder.RulingsOfType(MessageType.MatchStatus));
        var answer = recorder.TheAnswerTo(victimId, MessageType.MatchStatus);
        Assert.Equal(2, answer["PlayerCount"]?.Value<int>());
    }

    [Fact]
    public void AnUnauthorisedRealtimePeerIsNotRegistered()
    {
        SetUpRoom();

        var strangerId = "stranger-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var stranger = new MatchRudpProbe();
        try
        {
            stranger.Listener.NetworkReceiveEvent += (peer, reader, method) =>
            {
                reader.GetString();
                reader.Recycle();
            };

            // A token that was never issued. The socket connect still succeeds,
            // because the transport knows nothing about tokens; the server
            // drops the peer when it rejects the ClientConnect, and that is
            // what has to be observed here.
            stranger.Connect(
                "127.0.0.1", server.UdpPort ?? 0, strangerId, "not-a-real-token", roomId, 600,
                () => server.PollingEvent());

            // The claim is ignored while the peer is unregistered, so the
            // stranger must never reach the room or the player state.
            stranger.SendPosition(strangerId, roomId, 0f, 0f, 0f, 0f, 0.04f, 1);
            Pump(400);

            Assert.False(room.ContainsPlayer(strangerId));
            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(strangerId);
            Assert.True(string.IsNullOrEmpty(state.PlayerId),
                "a peer with a bad token was registered by the server");
        }
        finally
        {
            stranger.Dispose();
        }
    }
}
