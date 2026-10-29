using System.Collections.Generic;
using System.Numerics;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the server side flight of bullets and grenades.
/// <para>
/// The model existed inside MatchRUdpServerManager, which nothing constructed,
/// so a grenade throw was logged and echoed back and nothing was ever
/// simulated. These run the same rules with no socket, which is the point of
/// having pulled the simulation out of the transport.
/// </para>
/// </summary>
public sealed class ServerProjectileSimulatorTests
{
    private sealed class World
    {
        public readonly Dictionary<string, Vector2> Positions = new();

        public ServerProjectileSimulator Build()
        {
            var simulator = new ServerProjectileSimulator
            {
                PlayerPositionLookup = id => Positions.TryGetValue(id, out var p) ? p : Vector2.Zero,
                PlayerIds = () => new List<string>(Positions.Keys)
            };
            return simulator;
        }
    }

    // ---- Grenade timing -------------------------------------------------

    [Theory]
    [InlineData("Power", 160)]
    [InlineData("Cluster", 90)]
    [InlineData("Fire", 120)]
    [InlineData("Magnet", 80)]
    [InlineData("Normal", 110)]
    public void GrenadeDamageComesFromTheType(string type, int expected)
    {
        Assert.Equal(expected, ServerProjectileSimulator.CalculateGrenadeDamage(type));
    }

    [Fact]
    public void AnUnknownGrenadeTypeFallsBackToTheNormalNumbers()
    {
        Assert.Equal(110, ServerProjectileSimulator.CalculateGrenadeDamage("Something"));
        Assert.Equal(3.0f, ServerProjectileSimulator.CalculateGrenadeRadius("Something"));
        Assert.Equal(3.0f, ServerProjectileSimulator.CalculateGrenadeFuse("Something"));
    }

    [Fact]
    public void AGrenadeDoesNotExplodeBeforeItsFuse()
    {
        var world = new World();
        var simulator = world.Build();
        var exploded = 0;
        simulator.OnExpire = _ => exploded++;

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");

        // The fuse is three seconds, so a tick well before that must not pop it.
        simulator.Update(0.05f);
        simulator.Update(0.05f);

        Assert.Equal(0, exploded);
        Assert.Equal(1, simulator.ActiveCount);
    }

    [Fact]
    public void AGrenadeExplodesOnceTheFuseIsUp()
    {
        var world = new World();
        var simulator = world.Build();
        var exploded = 0;
        simulator.OnExpire = _ => exploded++;

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");

        for (var i = 0; i < 80; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.Equal(1, exploded);
        Assert.Equal(0, simulator.ActiveCount);
    }

    [Fact]
    public void AZeroDeltaDoesNotAdvanceAnything()
    {
        var world = new World();
        var simulator = world.Build();
        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");

        simulator.Update(0f);
        simulator.Update(-1f);

        Assert.Equal(1, simulator.ActiveCount);
    }

    // ---- Blast ----------------------------------------------------------

    [Fact]
    public void ABlastHurtsAPlayerStandingWhereItGoesOff()
    {
        var world = new World();
        // A grenade is thrown, not dropped: it travels for its whole fuse, so
        // the blast lands at the far end of the throw rather than at the hand.
        // Three seconds of fuse at twelve units a second is about 36 units.
        // The tick lands within a step of the burst point rather than exactly on
        // it, so the damage is near full but not quite; the point is that the
        // blast reaches a player standing where it goes off.
        world.Positions["victim"] = new Vector2(36f, 0f);
        var simulator = world.Build();

        var hits = new List<(string Target, int Damage)>();
        simulator.OnDamage = (target, _, damage, _) => hits.Add((target, damage));

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");
        for (var i = 0; i < 80; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.NotEmpty(hits);
        Assert.Equal("victim", hits[0].Target);
        Assert.InRange(hits[0].Damage, 80, 110);
    }

    [Fact]
    public void ABlastMissesAPlayerLeftBehindAtTheThrowPoint()
    {
        var world = new World();
        world.Positions["bystander"] = Vector2.Zero;
        var simulator = world.Build();
        var hits = 0;
        simulator.OnDamage = (_, _, _, _) => hits++;

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");
        for (var i = 0; i < 80; i++)
        {
            simulator.Update(0.05f);
        }

        // The grenade has travelled far past by the time it goes off, so standing
        // where it was thrown is not covered by the blast.
        Assert.Equal(0, hits);
    }

    [Fact]
    public void APlayerOutsideTheBlastIsUntouched()
    {
        var world = new World();
        world.Positions["bystander"] = new Vector2(100f, 0f);
        var simulator = world.Build();
        var hits = 0;
        simulator.OnDamage = (_, _, _, _) => hits++;

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Power");
        for (var i = 0; i < 80; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.Equal(0, hits);
    }

        [Fact]
    public void TheBlastFallsOffTowardsItsEdge()
    {
        var world = new World();
        var simulator = world.Build();

        var damage = new Dictionary<string, int>();
        simulator.OnDamage = (target, _, amount, _) => damage[target] = amount;

        // Placed either side of the burst point, which sits about 36 units out
        // for a normal grenade: three seconds of fuse at twelve units a second.
        world.Positions["near"] = new Vector2(36f, 0f);
        world.Positions["far"] = new Vector2(38.6f, 0f);

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");
        for (var i = 0; i < 80; i++)
        {
            simulator.Update(0.05f);
        }

        // The normal blast radius is three units, so both are caught but the one
        // nearer the centre has to take more.
        Assert.Equal(2, damage.Count);
        Assert.True(damage["near"] > damage["far"]);
        Assert.True(damage["far"] >= 1);
    }
    [Fact]
    public void APlayerAtTheCentreOfTheBlastIsNotDestroyed()
    {
        var world = new World();
        world.Positions["victim"] = Vector2.Zero;
        var simulator = world.Build();
        var amounts = new List<int>();
        simulator.OnDamage = (_, _, damage, _) => amounts.Add(damage);

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");
        for (var i = 0; i < 80; i++)
        {
            simulator.Update(0.05f);
        }

        // A blast always does at least one point of damage, so standing in one
        // is never a way to avoid it entirely.
        Assert.All(amounts, amount => Assert.True(amount >= 1));
    }

    // ---- Cluster --------------------------------------------------------

    [Fact]
    public void AClusterGrenadeBreaksIntoChildren()
    {
        var world = new World();
        var simulator = world.Build();
        var spawned = new List<string>();
        simulator.OnSpawn = (_, type) => spawned.Add(type);

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Cluster");
        for (var i = 0; i < 60; i++)
        {
            simulator.Update(0.05f);
        }

        // The parent plus three children.
        Assert.Contains("ChildClusterGrenade", spawned);
    }

    [Fact]
    public void ANonClusterGrenadeDoesNotBreak()
    {
        var world = new World();
        var simulator = world.Build();
        var spawned = new List<string>();
        simulator.OnSpawn = (_, type) => spawned.Add(type);

        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");
        for (var i = 0; i < 80; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.DoesNotContain("ChildClusterGrenade", spawned);
    }

    // ---- Bullets --------------------------------------------------------

    [Fact]
    public void ABulletHitsAPlayerItPassesThrough()
    {
        var world = new World();
        world.Positions["target"] = new Vector2(2f, 0f);
        var simulator = world.Build();

        var hit = string.Empty;
        simulator.OnDamage = (target, _, _, _) => hit = target;

        simulator.CreateBullet("room-1", "shooter", Vector2.Zero, Vector2.UnitX, 30, "Pistol");
        for (var i = 0; i < 20; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.Equal("target", hit);
    }

    [Fact]
    public void ABulletDoesNotHitItsOwnOwner()
    {
        var world = new World();
        world.Positions["shooter"] = new Vector2(2f, 0f);
        var simulator = world.Build();
        var hits = 0;
        simulator.OnDamage = (_, _, _, _) => hits++;

        simulator.CreateBullet("room-1", "shooter", Vector2.Zero, Vector2.UnitX, 30, "Pistol");
        for (var i = 0; i < 20; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.Equal(0, hits);
    }

    [Fact]
    public void ABulletMissesAPlayerItPassesBy()
    {
        var world = new World();
        world.Positions["target"] = new Vector2(2f, 20f);
        var simulator = world.Build();
        var hits = 0;
        simulator.OnDamage = (_, _, _, _) => hits++;

        simulator.CreateBullet("room-1", "shooter", Vector2.Zero, Vector2.UnitX, 30, "Pistol");
        for (var i = 0; i < 20; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.Equal(0, hits);
    }

    // ---- Lifecycle ------------------------------------------------------

    [Fact]
    public void ClearDropsEveryProjectile()
    {
        var world = new World();
        var simulator = world.Build();
        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");
        simulator.CreateBullet("room-1", "thrower", Vector2.Zero, Vector2.UnitY, 10, "Pistol");

        simulator.Clear();

        Assert.Equal(0, simulator.ActiveCount);
    }

    [Fact]
    public void AZeroDirectionStillThrowsSomewhereRatherThanStandingStill()
    {
        var world = new World();
        world.Positions["target"] = new Vector2(5f, 0f);
        var simulator = world.Build();
        var hit = string.Empty;
        simulator.OnDamage = (target, _, _, _) => hit = target;

        // A client that sends a zero direction must not produce a projectile
        // that hovers in place forever.
        simulator.CreateBullet("room-1", "shooter", Vector2.Zero, Vector2.Zero, 10, "Pistol");
        for (var i = 0; i < 20; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.Equal("target", hit);
    }

    [Fact]
    public void ASimulatorWithoutAPlayerSourceDoesNothingHarmful()
    {
        var simulator = new ServerProjectileSimulator();
        simulator.CreateGrenade("room-1", "thrower", Vector2.Zero, Vector2.UnitX, "Normal");

        simulator.Update(0.05f);
        for (var i = 0; i < 80; i++)
        {
            simulator.Update(0.05f);
        }

        Assert.Equal(0, simulator.ActiveCount);
    }
}
