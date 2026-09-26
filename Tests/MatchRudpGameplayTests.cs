using System;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the authoritative movement model that the RUDP listener feeds.
///
/// The probe tests prove a client can connect over UDP and that its packets
/// are admitted. These cover the rule on the receiving end: movement the
/// server accepts has to move the player, an over long delta time must not
/// simulate, the movement vector must stay inside the speed cap, and the
/// position a client claims must never be trusted.
///
/// The rules are exercised through ServerLagCompensationManager directly
/// rather than over a socket, because the delivery timing of a UDP probe is
/// not what these assertions are about and made them flaky.
/// </summary>
public sealed class MatchRudpGameplayTests
{
    private const float HorizontalSpeedCap = 10f;

    // The server tolerates a small position error and a per second teleport
    // allowance, so a rejected client claim moves the player by up to
    // PositionTolerance + TeleportAllowancePerSecond * DeltaTime.
    private const float MaxAcceptedDriftPerTick = 2.0f + 18.0f * 0.04f;

    private static ServerLagCompensationManager StartMatch(string playerId)
    {
        var manager = MatchServerV2.Instance.ServerLagCompensationManager;

        // The manager is a shared singleton, and StartMatch clears it, so the
        // player is registered after the match starts.
        manager.StartMatch("gameplay-room");
        manager.AddPlayer(playerId);

        return manager;
    }

    private static void Step(
        ServerLagCompensationManager manager,
        string playerId,
        int ticks,
        float moveX,
        float deltaTime = 0.04f,
        bool claimedPosition = false)
    {
        byte sequence = 1;
        for (var tick = 0; tick < ticks; tick++)
        {
            manager.ProcessClientInput(new ClientInputData
            {
                PlayerId = playerId,
                MoveX = moveX,
                DeltaTime = deltaTime,
                HasClientPosition = claimedPosition,
                ClientPosX = 900f,
                ClientPosY = 900f,
                SequenceNumber = sequence,
                Timestamp = tick * deltaTime
            }, out _);

            manager.Update(deltaTime);
            sequence++;
        }
    }

    [Fact]
    public void AcceptedMovementMovesTheAuthoritativePosition()
    {
        var playerId = "move-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var manager = StartMatch(playerId);

        var before = manager.GetPlayerState(playerId);
        Step(manager, playerId, ticks: 20, moveX: 1f);
        var after = manager.GetPlayerState(playerId);

        Assert.True(
            after.PositionX > before.PositionX,
            $"the player did not move: {before.PositionX} -> {after.PositionX}");
    }

    [Fact]
    public void MovementVectorIsClampedToTheSpeedCap()
    {
        var playerId = "cap-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var manager = StartMatch(playerId);

        // A movement vector longer than 1 is clamped, so a large value must not
        // produce a velocity beyond the cap.
        Step(manager, playerId, ticks: 60, moveX: 5f);

        var state = manager.GetPlayerState(playerId);
        Assert.InRange(MathF.Abs(state.VelocityX), 0f, HorizontalSpeedCap);
    }

    [Fact]
    public void ExcessiveDeltaTimeIsNotSimulated()
    {
        var playerId = "dt-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var manager = StartMatch(playerId);

        var before = manager.GetPlayerState(playerId);
        Step(manager, playerId, ticks: 5, moveX: 1f, deltaTime: 5f);
        var after = manager.GetPlayerState(playerId);

        Assert.InRange(after.PositionX - before.PositionX, -MaxAcceptedDriftPerTick, MaxAcceptedDriftPerTick);
    }

    [Fact]
    public void ClientClaimedPositionIsNotTrusted()
    {
        var playerId = "claim-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var manager = StartMatch(playerId);

        var before = manager.GetPlayerState(playerId);
        Step(manager, playerId, ticks: 10, moveX: 1f, claimedPosition: true);
        var after = manager.GetPlayerState(playerId);

        Assert.InRange(after.PositionX - before.PositionX, -MaxAcceptedDriftPerTick, MaxAcceptedDriftPerTick);
        Assert.InRange(after.PositionY - before.PositionY, -MaxAcceptedDriftPerTick, MaxAcceptedDriftPerTick);
    }
}
