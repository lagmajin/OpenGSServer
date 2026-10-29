using System;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the magazine, which is the whole of the ammunition economy.
/// <para>
/// The server used to hold no magazine at all: a shot was rate limited and that
/// was the entire limit on how much a player could fire, so a client could hold
/// the trigger down indefinitely, and an ammo pack was spent and did nothing
/// because there was nothing to refill. Nothing here tested that a shot spends a
/// round because there was nothing to spend, so a magazine could be added and
/// left unwired and everything would still be green.
/// </para>
/// </summary>
public sealed class ServerWeaponMagazineTests : IDisposable
{
    // The store is process wide and keyed by player, so every test needs a player
    // of its own. Sharing one id would have each test inherit the magazine the
    // last one left behind.
    private readonly string shooter = "magazine-" + Guid.NewGuid().ToString("N").Substring(0, 8);

    public void Dispose()
    {
        ServerWeaponMagazineStore.ClearRoomPlayers(new[] { shooter });
    }

    private string Shooter => shooter;

    private static DateTime Now => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ---- Spending a round ------------------------------------------------

    [Fact]
    public void AShotTakesARoundOutOfTheMagazine()
    {
        var capacity = ServerWeaponMagazineStore.Spec("Rifle").Capacity;

        Assert.True(ServerWeaponMagazineStore.TryFire(Shooter, "Rifle", Now, out var remaining));

        Assert.Equal(capacity - 1, remaining);
    }

    [Fact]
    public void AMagazineRunsOutRatherThanGoingNegative()
    {
        var capacity = ServerWeaponMagazineStore.Spec("Rifle").Capacity;
        for (var i = 0; i < capacity; i++)
        {
            Assert.True(ServerWeaponMagazineStore.TryFire(
                Shooter, "Rifle", Now.AddSeconds(i * 1f), out _));
        }

        Assert.False(ServerWeaponMagazineStore.TryFire(
            Shooter, "Rifle", Now.AddSeconds(capacity + 1f), out var remaining));
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void ASecondShotInsideTheIntervalIsRefused()
    {
        var interval = ServerWeaponMagazineStore.Spec("Sniper").IntervalSeconds;

        Assert.True(ServerWeaponMagazineStore.TryFire(Shooter, "Sniper", Now, out _));

        // Inside the interval, and refused. The refusal must not cost a round, or
        // the rate limit would silently double as a magazine drain.
        Assert.False(ServerWeaponMagazineStore.TryFire(
            Shooter, "Sniper", Now.AddSeconds(interval / 2f), out var afterRefusal));
        Assert.Equal(ServerWeaponMagazineStore.Spec("Sniper").Capacity - 1, afterRefusal);

        Assert.True(ServerWeaponMagazineStore.TryFire(
            Shooter, "Sniper", Now.AddSeconds(interval * 1.1f), out _));
    }

    [Fact]
    public void AnUnknownWeaponIsNotAWeapon()
    {
        Assert.False(ServerWeaponMagazineStore.IsKnownWeapon("Crossbow"));
        Assert.False(ServerWeaponMagazineStore.TryFire(Shooter, "Crossbow", Now, out _));
    }

    // ---- Reloading -------------------------------------------------------

    [Fact]
    public void AReloadDrawsTheReserveIntoTheMagazine()
    {
        var spec = ServerWeaponMagazineStore.Spec("SMG");
        for (var i = 0; i < spec.Capacity; i++)
        {
            Assert.True(ServerWeaponMagazineStore.TryFire(
                Shooter, "SMG", Now.AddSeconds(i + 1f), out _));
        }

        // Well past the last round, so the reload is not refused for being asked
        // for inside the weapon's own interval.
        var reloadAt = spec.Capacity + 10f;
        var result = ServerWeaponMagazineStore.TryReload(Shooter, "SMG", Now.AddSeconds(reloadAt), TimeSpan.Zero);
        Assert.Equal(ReloadResultKind.Started, result.Kind);

        var magazine = ServerWeaponMagazineStore.GetOrCreate(Shooter, "SMG");
        Assert.False(magazine.TryFinishReload(Now.AddSeconds(reloadAt + 0.1f), out _));
        Assert.True(magazine.TryFinishReload(Now.AddSeconds(reloadAt + spec.ReloadSeconds + 0.1f), out var loaded));

        Assert.Equal(spec.Capacity, loaded);
        Assert.Equal(spec.Capacity, magazine.ReadRemaining());
        Assert.Equal(spec.Reserve - spec.Capacity, magazine.ReadReserve());
    }

    [Fact]
    public void AReloadOnAFullMagazineIsRefused()
    {
        var result = ServerWeaponMagazineStore.TryReload(Shooter, "Pistol", Now, TimeSpan.Zero);

        Assert.Equal(ReloadResultKind.AlreadyFull, result.Kind);
    }

    [Fact]
    public void AReloadWithAnEmptyReserveIsRefused()
    {
        var magazine = ServerWeaponMagazineStore.GetOrCreate(Shooter, "Pistol");
        for (var i = 0; i < magazine.Capacity; i++)
        {
            Assert.True(
                magazine.TryConsume(Now.AddSeconds(60f + i), out _),
                $"round {i} of {magazine.Capacity} was refused; " +
                $"remaining {magazine.ReadRemaining()}, interval {magazine.IntervalSeconds}");
        }

        // Everything the player was carrying has been fired. There is nothing
        // behind the magazine, so a reload would be an animation over nothing.
        magazine.Reserve = 0;

        var result = ServerWeaponMagazineStore.TryReload(Shooter, "Pistol", Now.AddSeconds(120f), TimeSpan.Zero);

        Assert.Equal(ReloadResultKind.NoReserve, result.Kind);
    }

    [Fact]
    public void AClientCannotShortenAReloadIntoARateLimitBypass()
    {
        var magazine = ServerWeaponMagazineStore.GetOrCreate(Shooter, "Sniper");
        for (var i = 0; i < magazine.Capacity; i++)
        {
            magazine.TryConsume(Now.AddSeconds(60f + i), out _);
        }

        var result = ServerWeaponMagazineStore.TryReload(
            Shooter, "Sniper", Now.AddSeconds(120f), TimeSpan.FromSeconds(0.01f));

        Assert.Equal(ReloadResultKind.Started, result.Kind);
        Assert.True(
            result.Seconds >= magazine.ReloadSeconds,
            $"a requested reload of a second was granted in {result.Seconds}");
    }

    [Fact]
    public void FiringDuringAReloadIsRefused()
    {
        var magazine = ServerWeaponMagazineStore.GetOrCreate(Shooter, "Shotgun");
        for (var i = 0; i < magazine.Capacity; i++)
        {
            magazine.TryConsume(Now.AddSeconds(60f + i), out _);
        }

        ServerWeaponMagazineStore.TryReload(Shooter, "Shotgun", Now.AddSeconds(120f), TimeSpan.Zero);

        Assert.False(magazine.TryConsume(Now.AddSeconds(120.5f), out _));
    }

    // ---- Ammo packs and respawns ----------------------------------------

    [Fact]
    public void AnAmmoPackTopsTheReserveBackUp()
    {
        var magazine = ServerWeaponMagazineStore.GetOrCreate(Shooter, "Rifle");
        magazine.Reserve = 20;

        var added = ServerWeaponMagazineStore.RefillAllReserve(Shooter, 90);

        Assert.Equal(90, added);
        Assert.Equal(110, magazine.ReadReserve());
    }

    [Fact]
    public void AnAmmoPackDoesNotOverflowTheReserve()
    {
        var magazine = ServerWeaponMagazineStore.GetOrCreate(Shooter, "Pistol");

        ServerWeaponMagazineStore.RefillAllReserve(Shooter, magazine.MaxReserve * 10);

        Assert.Equal(magazine.MaxReserve, magazine.ReadReserve());
    }

    [Fact]
    public void ARespawnComesBackWithFullMagazines()
    {
        var magazine = ServerWeaponMagazineStore.GetOrCreate(Shooter, "Rifle");
        for (var i = 0; i < magazine.Capacity; i++)
        {
            magazine.TryConsume(Now.AddSeconds(i + 1f), out _);
        }

        ServerWeaponMagazineStore.RefillAllMagazines(Shooter);

        Assert.Equal(magazine.Capacity, magazine.ReadRemaining());
        Assert.Equal(magazine.MaxReserve, magazine.ReadReserve());
    }

    [Fact]
    public void APlayerWhoLeftTakesTheirMagazinesWithThem()
    {
        var magazine = ServerWeaponMagazineStore.GetOrCreate(Shooter, "Rifle");
        magazine.TryConsume(Now, out _);

        ServerWeaponMagazineStore.ResetPlayer(Shooter);

        Assert.False(ServerWeaponMagazineStore.TryPeek(Shooter, "Rifle", out _));
    }
}
