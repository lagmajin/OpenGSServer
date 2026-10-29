using System;
using System.Threading;
using LiteNetLib;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Drives gameplay movement over the realtime transport, end to end.
/// <para>
/// S2 asks for the match state progression to be server authoritative, and it
/// claims UDP input is applied on the fixed tick rather than in the receive
/// callback. Nothing checked either half of that. The gameplay tests reach the
/// lag compensation manager directly, so the whole receive path was untested:
/// the message type allow list, the parse of the movement fields, the queue the
/// tick drains, and the tick itself. The probe tests send a PlayerMove and
/// assert a state entry exists, which a server that queued the packet and threw
/// it away would also satisfy.
/// </para>
/// <para>
/// These send the message a real client sends and assert on the position the
/// server ends up holding. The rules themselves are covered by
/// ServerPlayerStateManagerTests; what is covered here is that the path from a
/// real packet to a moved player is wired at all.
/// </para>
/// </summary>
// These tests each bind a real udp port, so two of them running at once
// can land on the same one. Sharing a collection keeps them on one lane.
[Collection("RealtimeSocket")]
public sealed class MatchRudpGameplayInputTests : IDisposable
{
    private static int nextPort = 65450;

    // The loop runs at 25Hz. A test that drives the server has to use the same
    // step the loop does, because the tick is what applies the queued input.
    private const float TickSeconds = 0.04f;

    private MatchUDPServer server = null!;
    private MatchRudpProbe probe = null!;
    private MatchRoom room = null!;

    private string playerId = string.Empty;
    private string roomId = string.Empty;

    public void Dispose()
    {
        probe?.Dispose();
        server?.Dispose();

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            MatchRoomManager.Instance.RemoveRoom(roomId, forceShutdownNowPlayingRooms: true);
        }
    }

    /// <summary>
    /// Pumps both sides, in the order the real match loop does.
    /// <para>
    /// The probe only transmits what it sends while it is polled, and the server
    /// only receives a packet while it is polled, so pumping one of them leaves
    /// the other waiting. The order matters as much as the pumping: the loop
    /// polls first so a packet received on this tick can be applied on this tick,
    /// and it drains the input queue before it advances the simulation.
    /// </para>
    /// <para>
    /// <paramref name="runMatchTick"/> exists so a test can prove the receive
    /// callback on its own does not move anybody. Polling without ticking is not
    /// a thing production does, which is exactly why it needs a test.
    /// </para>
    /// </summary>
    private void Pump(int milliseconds, bool runMatchTick = true)
    {
        probe.Poll(milliseconds, () =>
        {
            server.PollingEvent();
            if (!runMatchTick)
            {
                return;
            }

            server.Tick(TickSeconds);
            MatchServerV2.Instance.ServerLagCompensationManager.Update(TickSeconds);
        });
    }

    private void SetUpRoom()
    {
        playerId = "mover-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        // The realtime listener resolves the room through MatchRoomManager.Instance,
        // so a standalone manager would build a room nothing can find.
        var rooms = MatchRoomManager.Instance;
        var created = rooms.CreateNewDeathMatchRoom("rudp-move", playerId, capacity: 4);
        roomId = created.RoomId;
        room = rooms.GetRoomById(created.RoomId) as MatchRoom;
        Assert.NotNull(room);
        room!.AddNewPlayer(new PlayerInfo(playerId, "Mover"));
        room.GameStart();

        // Registered with the authoritative state before the handshake, which is
        // what every other realtime test does and what this one was missing.
        // The handshake registers the player too, but it is too late to be the
        // thing that makes the state exist: the movement this test is about is
        // validated against that state, and a player the manager has only just
        // heard of is a player it will not simulate.
        MatchServerV2.Instance.ServerLagCompensationManager.AddPlayer(playerId);

        server = new MatchUDPServer();
        server.Listen(Interlocked.Increment(ref nextPort));

        var token = server.IssueConnectionToken(playerId);
        Assert.False(string.IsNullOrWhiteSpace(token), "no realtime token was issued");

        probe = new MatchRudpProbe();
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
    }

    /// <summary>
    /// The position the server currently holds for this room's player.
    /// </summary>
    private System.Numerics.Vector2 AuthoritativePosition()
    {
        var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
        return new System.Numerics.Vector2(state.PositionX, state.PositionY);
    }

    /// <summary>
    /// The movement sequence number this test is on.
    /// <para>
    /// It has to keep counting across calls rather than restart. The server
    /// compares it against the last one it accepted and treats anything that is
    /// not newer as a replay, so a second walk starting at one again is
    /// discarded as stale and the player never moves. That is the server
    /// behaving correctly; the sequence belongs to the client, and a client
    /// restarting it is a client bug.
    /// </para>
    /// </summary>
    private byte sequence = 1;

    /// <summary>
    /// Sends one movement packet and advances the server by a single tick.
    /// <para>
    /// One packet per tick is the point. The movement model accelerates towards
    /// the requested velocity and an idle tick drags it back towards zero, so a
    /// client that sends one packet and then lets several ticks pass never gets
    /// up to speed: it moves a fraction of a unit per packet and stops again. A
    /// real client sends input on every tick it runs, and pairing the send with
    /// the tick is what makes the walk this test asks for the walk a real client
    /// would produce.
    /// </para>
    /// </summary>
    private void Step(float moveX, float deltaTime = TickSeconds, bool runMatchTick = true)
    {
        // The send is repeated inside the pump rather than sent once outside it.
        // The probe only transmits while it is polled, and a single poll can
        // cover many ticks, so one send per pump turned into a handful of inputs
        // over a whole walk: the server ticks far more often than the unreliable
        // packet gets through, and the player accelerates and then coasts back
        // to a stop on every tick that arrives without input. Sending inside the
        // loop is what a client running at the loop rate actually does.
        var deadline = Environment.TickCount64 + 40;

        while (Environment.TickCount64 < deadline)
        {
            probe.SendMove(playerId, roomId, moveX, 0f, false, false, deltaTime, sequence++);
            probe.Poll(5, () =>
            {
                server.PollingEvent();
                if (!runMatchTick)
                {
                    return;
                }

                server.Tick(TickSeconds);
                MatchServerV2.Instance.ServerLagCompensationManager.Update(TickSeconds);
            });
        }
    }

    /// <summary>
    /// Sends movement packets and pumps until the server has actually moved the
    /// player, or the deadline passes.
    /// <para>
    /// Movement packets go out unreliably, so a fixed number of sends is a guess
    /// about how much of the walk arrived. Every other realtime test that asserts
    /// on a position the server holds waits for that position rather than
    /// assuming it, and this is the same problem.
    /// </para>
    /// </summary>
    private void WalkUntilMoved(float moveX, float deltaTime = TickSeconds, bool runMatchTick = true)
    {
        var start = AuthoritativePosition();

        // Generous, and deliberately not a tick count. The realtime tests share
        // one process wide state manager and one room registry, and everything
        // the earlier tests registered is still registered when this class runs.
        // Each server update walks all of it, so how much a wall clock second is
        // worth here depends on what ran before, and a deadline derived from the
        // 25Hz this test stands in for would be a deadline for a machine this
        // test does not run on.
        var deadline = Environment.TickCount64 + 60000;

        while (Environment.TickCount64 < deadline)
        {
            Step(moveX, deltaTime, runMatchTick);

            if (AuthoritativePosition().X > start.X + 0.5f)
            {
                return;
            }
        }

        Assert.Fail(
            $"the server never moved the player: asked for {moveX} on X from {start.X}, " +
            $"still at {AuthoritativePosition().X}\n" +
            $"connected peers: {server.ConnectedPlayerCount}\n" +
            $"{MatchServerV2.Instance.ServerLagCompensationManager.GetDebugInfo()}");
    }

    /// <summary>
    /// Ticks with no input until the player has stopped moving.
    /// <para>
    /// The movement model drags velocity towards zero rather than stopping dead,
    /// so a player who has just been walking carries on for a few ticks. A test
    /// that reads the position straight after a walk is therefore reading that
    /// momentum, not whatever was asked of the server since.
    /// </para>
    /// </summary>
    private void WaitUntilStopped()
    {
        // The same shared state makes a tick worth less wall clock time here than
        // on an idle machine, so this waits on the state rather than on a count
        // of ticks. See WalkUntilMoved.
        var deadline = Environment.TickCount64 + 60000;

        while (Environment.TickCount64 < deadline)
        {
            server.PollingEvent();
            server.Tick(TickSeconds);
            MatchServerV2.Instance.ServerLagCompensationManager.Update(TickSeconds);
            probe.Poll(5);

            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (MathF.Abs(state.VelocityX) < 0.01f)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Sends movement packets for a fixed stretch of pumping.
    /// <para>
    /// This is the counterpart to <see cref="WalkUntilMoved"/> for the
    /// assertions about what the server refuses. Those cannot wait for a
    /// movement that is the thing under test, so they send for a while and then
    /// ask whether the position changed. The wait is generous for the reason
    /// given on that method: enough packets go out that a refusal caused by the
    /// rules is distinguishable from one caused by nothing arriving.
    /// </para>
    /// </summary>
    private void SendFor(int milliseconds, float moveX, float deltaTime = TickSeconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Step(moveX, deltaTime);
        }
    }

    [Fact]
    public void AMovementPacketMovesThePlayerTheServerIsHolding()
    {
        SetUpRoom();

        var before = AuthoritativePosition();

        // Everything between the socket and the state manager is what this
        // covers: the message has to be recognised as input, its movement field
        // has to be read, it has to survive the queue, and the tick has to apply
        // it. A server that accepted the packet and dropped it leaves the player
        // exactly where they were, which is what a probe test would pass on.
        WalkUntilMoved(moveX: 1f);

        var after = AuthoritativePosition();
        Assert.True(
            after.X > before.X,
            $"the player did not move: {before.X} -> {after.X}");
    }

    [Fact]
    public void AMovementPacketIsAppliedOnTheTickAndNotInTheReceiveCallback()
    {
        SetUpRoom();

        var before = AuthoritativePosition();

        // Only the listener runs. Every one of these packets reaches the server
        // and is queued, and none of them may move anybody: applying input where
        // it arrives makes the simulation run at the rate a client's packets
        // happen to arrive rather than at the rate the server ticks, which is
        // the whole reason the input is deferred to the tick.
        var noTickDeadline = Environment.TickCount64 + 2000;
        while (Environment.TickCount64 < noTickDeadline)
        {
            Step(moveX: 1f, runMatchTick: false);
        }

        var received = AuthoritativePosition();
        Assert.True(
            MathF.Abs(received.X - before.X) < 0.001f,
            $"input was applied in the receive callback: {before.X} -> {received.X}");

        // Now the loop runs. The queued input is what moves the player, so the
        // movement that appears is the tick applying it rather than the receive
        // path having held it back for the assertion above.
        WalkUntilMoved(moveX: 1f);

        var after = AuthoritativePosition();
        Assert.True(
            after.X > received.X + 0.5f,
            $"the queued input was never applied by a tick: {received.X} -> {after.X}");
    }

    [Fact]
    public void AnOverlongDeltaTimeMovementPacketIsRefused()
    {
        SetUpRoom();

        // The walk first, so the refusal is measured against a server that has
        // just proved it moves this player. Without it the assertion would also
        // be satisfied by a server that never received the packet at all, which
        // is the failure this test is not about.
        WalkUntilMoved(moveX: 1f);

        // Let the walk finish. The player is still carrying the velocity the
        // walk built up, and momentum carries them on for a few ticks whatever
        // arrives next, so the position cannot be read as a measure of whether a
        // packet was refused until that has run out. Waiting for it is also what
        // gives the refused packets time to arrive and be turned down.
        WaitUntilStopped();

        var before = AuthoritativePosition();

        // The listener admits the packet and the tick refuses it, which is the
        // half of this the state manager tests cannot see. A client claiming a
        // quarter of a second per packet could otherwise walk a player across
        // the map several times faster than the loop runs.
        SendFor(milliseconds: 2000, moveX: 1f, deltaTime: 5f);

        var after = AuthoritativePosition();
        Assert.True(
            MathF.Abs(after.X - before.X) < 0.5f,
            $"a movement packet with an overlong delta time moved the player: {before.X} -> {after.X}");
    }

    [Fact]
    public void AMovementPacketCannotPlaceThePlayerWhereItSaysItIs()
    {
        SetUpRoom();

        // The walk first, so the claim is measured against a server that has
        // proved it moves this player over the same wire. Otherwise "the player
        // was not teleported" would also be true of a server that ignored the
        // packet entirely, which is not what this is about.
        WalkUntilMoved(moveX: 1f);

        // Settled for the same reason as the refusal test: the momentum from the
        // walk would otherwise be read as the effect of the packets that follow.
        WaitUntilStopped();

        var before = AuthoritativePosition();

        // The movement message carries the same optional position field the
        // position update message carries. A client naming one on a movement
        // packet is claiming its location, and the server has to treat it the way
        // it treats any other claim: measured against what it already believes,
        // and refused when it does not add up.
        var claimDeadline = Environment.TickCount64 + 2000;
        while (Environment.TickCount64 < claimDeadline)
        {
            probe.Send(playerId, roomId, new JObject
            {
                ["MessageType"] = "PlayerMove",
                ["VelX"] = 1f,
                ["MoveX"] = 1f,
                ["PosX"] = 900f,
                ["PosY"] = 900f,
                ["DeltaTime"] = TickSeconds,
                ["SequenceNumber"] = sequence++,
                ["Timestamp"] = Environment.TickCount64 / 1000.0
            });
            Pump(40);
        }

        var after = AuthoritativePosition();

        // The player walked, because the movement part of the packet is real
        // input. What must not have happened is the teleport.
        Assert.True(
            MathF.Abs(after.X - 900f) > 100f && MathF.Abs(after.Y - 900f) > 100f,
            $"a movement packet placed the player at the position it claimed: ({after.X}, {after.Y})");
        Assert.True(
            MathF.Abs(after.X - before.X) < 20f,
            $"the player left the walk the server allows: {before.X} -> {after.X}");
    }
}