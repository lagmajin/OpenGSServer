using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

public sealed class ServerPlayerStateManagerTests
{
    [Fact]
    public void InitialClientPositionIsNotAuthoritative()
    {
        var manager = new ServerPlayerStateManager();
        manager.RegisterPlayer("player-1");

        manager.QueueClientInput(new ClientInputData
        {
            PlayerId = "player-1",
            SequenceNumber = 1,
            DeltaTime = 0.05f,
            Timestamp = 1f,
            HasClientPosition = true,
            ClientPosX = 1000f,
            ClientPosY = 1000f,
            ClientPosZ = 1000f
        }, out var rejectionReason);

        Assert.True(string.IsNullOrEmpty(rejectionReason));
        manager.ProcessAllInputs(0.05f);

        var state = manager.GetPlayerState("player-1");
        Assert.InRange(state.PositionX, -1f, 1f);
        Assert.InRange(state.PositionY, -1f, 1f);
        Assert.InRange(state.PositionZ, -1f, 1f);
    }

    [Fact]
    public void PositionValidationRejectsExcessiveDeltaTime()
    {
        var manager = new ServerPlayerStateManager();
        manager.RegisterPlayer("player-1");

        var accepted = manager.ValidateClientPosition("player-1", 0f, 0f, 0f, 1f);

        Assert.False(accepted);
    }

    [Fact]
    public void BackwardsClientTimestampIsRejected()
    {
        var manager = new ServerPlayerStateManager();
        manager.RegisterPlayer("player-1");

        var firstAccepted = manager.QueueClientInput(new ClientInputData
        {
            PlayerId = "player-1",
            SequenceNumber = 1,
            DeltaTime = 0.05f,
            Timestamp = 10f
        }, out _);
        manager.ProcessAllInputs(0.05f);
        var secondAccepted = manager.QueueClientInput(new ClientInputData
        {
            PlayerId = "player-1",
            SequenceNumber = 2,
            DeltaTime = 0.05f,
            Timestamp = 9f
        }, out _);

        Assert.True(firstAccepted);
        Assert.False(secondAccepted);
    }

    [Fact]
    public void IdleSimulationAppliesGravityAfterJump()
    {
        var manager = new ServerPlayerStateManager();
        manager.RegisterPlayer("player-1");
        manager.QueueClientInput(new ClientInputData
        {
            PlayerId = "player-1",
            SequenceNumber = 1,
            DeltaTime = 0.05f,
            Timestamp = 1f,
            Jump = true
        }, out _);
        manager.ProcessAllInputs(0.05f);

        var afterJump = manager.GetPlayerState("player-1");
        manager.ProcessAllInputs(0.05f);
        var afterIdleTick = manager.GetPlayerState("player-1");

        Assert.True(afterJump.PositionZ > 0f);
        Assert.True(afterIdleTick.VelocityZ < afterJump.VelocityZ);
    }
}
