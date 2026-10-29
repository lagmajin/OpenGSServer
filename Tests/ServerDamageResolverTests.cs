using OpenGSCore;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the damage arithmetic and the health it acts on.
/// <para>
/// Damage used to be scaled in the message handler and broadcast without being
/// applied to anything, so no player health was tracked and no kill could be
/// detected. The arithmetic is separated here so it can be checked on its own.
/// </para>
/// </summary>
public sealed class ServerDamageResolverTests
{
    private static PlayerInfo NewPlayer(int health = 100) =>
        new PlayerInfo("p-1", "Player", null, 1, 0, health);

    // ---- Pose scaling ---------------------------------------------------

    [Fact]
    public void AStandingPlayerTakesFullDamage()
    {
        var player = NewPlayer();

        var outcome = ServerDamageResolver.Apply(player, 40, EPlayerPoseState.Stand);

        Assert.Equal(40, outcome.Applied);
        Assert.Equal(60, outcome.RemainingHealth);
    }

    [Fact]
    public void ASittingPlayerTakesHalf()
    {
        var player = NewPlayer();

        var outcome = ServerDamageResolver.Apply(player, 40, EPlayerPoseState.Sit);

        Assert.Equal(20, outcome.Applied);
    }

    [Fact]
    public void APronePlayerTakesThreeQuarters()
    {
        var player = NewPlayer();

        var outcome = ServerDamageResolver.Apply(player, 40, EPlayerPoseState.LieDown);

        Assert.Equal(30, outcome.Applied);
    }

    // ---- Health ---------------------------------------------------------

    [Fact]
    public void HealthNeverGoesBelowZero()
    {
        var player = NewPlayer(30);

        var outcome = ServerDamageResolver.Apply(player, 500, EPlayerPoseState.Stand);

        Assert.Equal(0, player.Health);
        Assert.Equal(0, outcome.RemainingHealth);
        Assert.True(outcome.IsNowDown);
    }

    [Fact]
    public void ADamageBiggerThanTheHealthLeftIsClampedToWhatIsLeft()
    {
        var player = NewPlayer(30);

        var outcome = ServerDamageResolver.Apply(player, 500, EPlayerPoseState.Stand);

        // Health is a hundred, not a credit the player can spend later.
        Assert.Equal(30, outcome.Applied);
    }

    [Fact]
    public void AHitOnAPlayerWhoIsAlreadyDownDoesNothing()
    {
        var player = NewPlayer(0);

        var outcome = ServerDamageResolver.Apply(player, 50, EPlayerPoseState.Stand);

        Assert.Equal(0, outcome.Applied);
        Assert.True(outcome.WasAlreadyDown);
        Assert.False(outcome.IsNowDown);
    }

    [Fact]
    public void ANonPositiveHitIsIgnored()
    {
        var player = NewPlayer();

        Assert.Equal(0, ServerDamageResolver.Apply(player, 0, EPlayerPoseState.Stand).Applied);
        Assert.Equal(0, ServerDamageResolver.Apply(player, -10, EPlayerPoseState.Stand).Applied);
        Assert.Equal(100, player.Health);
    }

    [Fact]
    public void AHalfPointHitStillDoesAtLeastOne()
    {
        var player = NewPlayer();

        // Rounding a reduced hit down to nothing would make sitting behind cover
        // a way to be invulnerable.
        var outcome = ServerDamageResolver.Apply(player, 1, EPlayerPoseState.Sit);

        Assert.True(outcome.Applied >= 1);
    }

    [Fact]
    public void ReviveRestoresFullHealth()
    {
        var player = NewPlayer(100);
        ServerDamageResolver.Apply(player, 150, EPlayerPoseState.Stand);

        ServerDamageResolver.Revive(player);

        Assert.Equal(player.MaxHealth, player.Health);
        Assert.False(ServerDamageResolver.IsDown(player));
    }

    [Fact]
    public void APlayerWithNoHealthIsDown()
    {
        Assert.True(ServerDamageResolver.IsDown(NewPlayer(0)));
        Assert.False(ServerDamageResolver.IsDown(NewPlayer(1)));
        Assert.True(ServerDamageResolver.IsDown(null));
    }

    // ---- Blast falloff --------------------------------------------------

    [Fact]
    public void ABlastAtItsCentreDealsTheFullDamage()
    {
        Assert.Equal(110, ServerDamageResolver.BlastDamage(110, 0f, 3f));
    }

    [Fact]
    public void ABlastFallsOffTowardsItsEdge()
    {
        var near = ServerDamageResolver.BlastDamage(110, 0.5f, 3f);
        var far = ServerDamageResolver.BlastDamage(110, 2.8f, 3f);

        Assert.True(near > far);
    }

    [Fact]
    public void ABlastDoesNotReachBeyondItsRadius()
    {
        Assert.Equal(0, ServerDamageResolver.BlastDamage(110, 3.1f, 3f));
        Assert.Equal(0, ServerDamageResolver.BlastDamage(110, 50f, 3f));
    }

    [Fact]
    public void ABlastAtItsEdgeStillDoesOne()
    {
        Assert.True(ServerDamageResolver.BlastDamage(110, 2.99f, 3f) >= 1);
    }

    [Fact]
    public void ABlastWithNoDamageOrNoRadiusIsNothing()
    {
        Assert.Equal(0, ServerDamageResolver.BlastDamage(0, 0f, 3f));
        Assert.Equal(0, ServerDamageResolver.BlastDamage(110, 0f, 0f));
    }
}