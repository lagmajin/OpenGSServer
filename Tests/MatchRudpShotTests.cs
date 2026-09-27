using System;
using System.Threading;
using LiteNetLib;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Drives a shot to a kill over the realtime transport, end to end.
/// <para>
/// The damage tests assert that a shot leaves the room intact, which a server
/// that ignored the shot entirely would also satisfy. Nothing checked that a
/// shot actually changes the health the server holds, so the shot handler could
/// lose its hit resolution and stay green.
/// </para>
/// <para>
/// These send the message a client really sends. RUDPMessageTypes.CreatePlayerShot
/// carries a position, a direction and a weapon, and no target: a client does not
/// know who it hit, because on a real client that is not its call to make. The
/// assertion is on the health, kills and deaths the server ends up holding.
/// </para>
/// </summary>
// These tests each bind a real udp port, so two of them running at once
// can land on the same one. Sharing a collection keeps them on one lane.
[Collection("RealtimeSocket")]
public sealed class MatchRudpShotTests : IDisposable
{
    private static int nextPort = 65350;

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

    /// <summary>
    /// Pumps both sides. The probe only transmits what it sends while it is
    /// polled, and the server only receives a packet while it is polled, so
    /// pumping one of them leaves the other waiting.
    /// </summary>
    private void Pump(int milliseconds)
    {
        shooter?.Poll(milliseconds, () =>
        {
            server.PollingEvent();
            server.Tick(0.04f);
        });
    }

    private void SetUpRoom()
    {
        // The realtime parser resolves the room through MatchRoomManager.Instance,
        // so a standalone manager would build a room nothing can find.
        var rooms = MatchRoomManager.Instance;
        shooterId = "shooter-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        victimId = "victim-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var created = rooms.CreateNewDeathMatchRoom("rudp-shot", shooterId, capacity: 4);
        roomId = created.RoomId;
        room = rooms.GetRoomById(created.RoomId) as MatchRoom;
        Assert.NotNull(room);
        room!.AddNewPlayer(new PlayerInfo(shooterId, "Shooter"));
        room.AddNewPlayer(new PlayerInfo(victimId, "Victim"));

        // The room has to be playing. The projectile simulation only resolves
        // hits against players in a live match, so without this the grenade
        // would explode in a room that was never in play and reach nobody. A real
        // match reaches this state through the lobby, which is part of why the
        // routing went unchecked.
        room.GameStart();

        // The hit path refuses a shot when either player has no position, so both
        // are registered before the handshake.
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
        Assert.False(string.IsNullOrWhiteSpace(token), $"no realtime token was issued for {playerId}");

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

    /// <summary>
    /// Throws the way a client does, which is what
    /// RUDPMessageTypes.CreateGrenadeThrow builds: a position, a direction, a
    /// type and a power, naming nobody.
    /// </summary>
    private void ThrowGrenade(string grenadeType = "Normal", float dirX = 1f, float dirY = 0f)
    {
        shooter.Send(shooterId, roomId, new JObject
        {
            ["MessageType"] = "GrenadeThrow",
            ["PosX"] = 0f,
            ["PosY"] = 0f,
            ["DirX"] = dirX,
            ["DirY"] = dirY,
            ["GrenadeType"] = grenadeType,
            ["Power"] = 1f
        });
    }

    /// <summary>
    /// Runs the server's projectile simulation for long enough for a fuse to run
    /// out. The loop only steps projectiles when it ticks, and a test drives that
    /// loop itself, so the fuse has to be waited out rather than assumed.
    /// </summary>
    private void RunProjectiles(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            InGameMatchEventHandler.UpdateProjectiles(0.05f);
            Thread.Sleep(5);
        }
    }

    /// <summary>
    /// The position the server currently holds for a player.
    /// </summary>
    private System.Numerics.Vector2 AuthoritativePosition(string playerId)
    {
        var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
        return new System.Numerics.Vector2(state.PositionX, state.PositionY);
    }

    /// <summary>
    /// Waits until the server is holding a position the test can reason about.
    /// <para>
    /// Position packets are delivered unreliably and the listener is pumped on a
    /// timer, so a fixed number of sends is a guess about how much of the walk
    /// arrived. Firing before the walk landed meant aiming at where the victim was
    /// meant to be rather than where it was, which failed intermittently. The
    /// geometry these tests are about only means something once both positions are
    /// the ones the test asked for, so that is waited for rather than assumed.
    /// </para>
    /// </summary>
    private void WaitForPosition(MatchRudpProbe probe, string playerId, float x, float y)
    {
        byte sequence = 1;
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            probe.SendPosition(playerId, roomId, x, y, 0f, 0f, 0.04f, sequence++);
            Pump(60);

            var held = AuthoritativePosition(playerId);
            if (MathF.Abs(held.X - x) < 0.25f && MathF.Abs(held.Y - y) < 0.25f)
            {
                return;
            }
        }

        var final = AuthoritativePosition(playerId);
        Assert.Fail(
            $"the server never adopted the position for {playerId}: " +
            $"asked for ({x}, {y}) but hold ({final.X}, {final.Y})");
    }

    /// <summary>
    /// Walks a player to a spot and waits for the server to be holding it.
    /// <para>
    /// It walks rather than teleports on purpose. The server only adopts a
    /// position within a tolerance of the one it holds, and a step of one unit at
    /// a 0.04s delta is inside that while a jump across the room is not, so a
    /// single packet claiming the far side of the map would be refused and the
    /// player would never be where the test believes it is.
    /// </para>
    /// </summary>
    private void MoveTo(MatchRudpProbe probe, string playerId, float x, float y)
    {
        const float StepSize = 1f;
        byte sequence = 1;

        float currentX = 0f;
        float currentY = 0f;
        while (MathF.Abs(currentX - x) > 0.001f || MathF.Abs(currentY - y) > 0.001f)
        {
            var dx = x - currentX;
            var dy = y - currentY;
            var distance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (distance > StepSize)
            {
                dx = (dx / distance) * StepSize;
                dy = (dy / distance) * StepSize;
            }

            currentX += dx;
            currentY += dy;
            probe.SendPosition(playerId, roomId, currentX, currentY, 0f, 0f, 0.04f, sequence++);
            Pump(60);
        }

        WaitForPosition(probe, playerId, x, y);
    }

    /// <summary>
    /// Fires the way a client does: a position, a direction and a weapon, with
    /// no target. This mirrors RUDPMessageTypes.CreatePlayerShot.
    /// </summary>
    private void Fire(string weaponType = "Pistol", float dirX = 1f, float dirY = 0f)
    {
        shooter.Send(shooterId, roomId, new JObject
        {
            ["MessageType"] = "PlayerShot",
            ["PosX"] = 0f,
            ["PosY"] = 0f,
            ["DirX"] = dirX,
            ["DirY"] = dirY,
            ["WeaponType"] = weaponType
        });
    }

    /// <summary>
    /// Fires, naming the player it wants to hit.
    /// <para>
    /// No client builds this: RUDPMessageTypes.CreatePlayerShot has no target
    /// field, and the TCP smoke that does send one aims at "nobody". It exists
    /// here to pin down what the server does when a message names a target, since
    /// a target in the message is a claim about who was hit and that is the
    /// server's call to make.
    /// </para>
    /// </summary>
    private void FireAt(string targetId, string weaponType = "Pistol", float dirX = 1f, float dirY = 0f)
    {
        shooter.Send(shooterId, roomId, new JObject
        {
            ["MessageType"] = "PlayerShot",
            ["PosX"] = 0f,
            ["PosY"] = 0f,
            ["DirX"] = dirX,
            ["DirY"] = dirY,
            ["TargetID"] = targetId,
            ["WeaponType"] = weaponType
        });
    }

    private int VictimHealth()
    {
        Assert.True(room.TryGetPlayer(victimId, out var info), "the victim left the room");
        return info!.Health;
    }

    private int ShooterKills()
    {
        Assert.True(room.TryGetPlayer(shooterId, out var info), "the shooter left the room");
        return info!.Kills;
    }

    private int ShooterHealth()
    {
        Assert.True(room.TryGetPlayer(shooterId, out var info), "the shooter left the room");
        return info!.Health;
    }

    /// <summary>
    /// How many projectiles the server is currently simulating.
    /// <para>
    /// This is reached through the static simulation the match loop steps, rather
    /// than a simulator the test built, because the question is whether a message
    /// that arrived over a socket reached the one the live match uses. A test with
    /// its own simulator would answer a different question.
    /// </para>
    /// </summary>
    private static int ProjectileCount()
    {
        return InGameMatchEventHandler.LiveProjectileCount();
    }

    [Fact]
    public void AShotAtAPlayerInFrontReducesTheHealthTheServerHolds()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        MoveTo(victim, victimId, 3f, 0f);

        var before = VictimHealth();

        Fire("Pistol", dirX: 1f);
        Pump(400);

        // This is the assertion nothing made before: the shot handler had no way
        // to resolve a target, because a client sends no target, so every shot a
        // real client took was broadcast and applied to nobody.
        Assert.True(
            VictimHealth() < before,
            $"a shot aimed at a player standing in front did no damage: {before} -> {VictimHealth()}");
    }

    [Fact]
    public void AShotBehindTheShooterMisses()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        MoveTo(victim, victimId, -3f, 0f);

        var before = VictimHealth();

        // Aimed away from the victim. A hit here means the direction is not being
        // consulted at all and any shot in the room lands.
        Fire("Pistol", dirX: 1f);
        Pump(400);

        Assert.Equal(before, VictimHealth());
    }

    [Fact]
    public void AShotOffToTheSideMisses()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        MoveTo(victim, victimId, 0f, 3f);

        var before = VictimHealth();

        // The victim is beside the shooter, not in front of it.
        Fire("Pistol", dirX: 1f);
        Pump(400);

        Assert.Equal(before, VictimHealth());
    }

    [Fact]
    public void AShotAtAPlayerOutOfRangeMisses()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        // A shotgun reaches 25 units. Forty is past it but still close enough that
        // the victim walks there rather than claiming a position it could not
        // have reached, which the server would refuse.
        MoveTo(victim, victimId, 40f, 0f);

        var before = VictimHealth();

        Fire("Shotgun", dirX: 1f);
        Pump(400);

        Assert.Equal(before, VictimHealth());
    }

    [Fact]
    public void AShotThatLandsTheKillCreditsTheShooterAndTheVictim()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        MoveTo(victim, victimId, 3f, 0f);

        // A pistol does 25 and the victim starts at full health, so the shot has
        // to be repeated. The cooldown is per weapon, so the timing has to clear it.
        var guard = 0;
        while (VictimHealth() > 0 && guard++ < 12)
        {
            Fire("Pistol", dirX: 1f);
            Pump(300);
        }

        Assert.Equal(0, VictimHealth());

        Assert.True(room.TryGetPlayer(victimId, out var victimInfo));
        Assert.Equal(1, victimInfo!.Deaths);

        // The kill is credited from the health change, not from a client claiming
        // one, so a shot that really landed is what puts the tally up.
        Assert.True(ShooterKills() >= 1, "the shot that landed did not raise a kill");
    }

    [Fact]
    public void AShotFromAPlayerWithNoPositionDoesNothing()
    {
        SetUpRoom();
        // The victim moves so the geometry would reach it, but the shooter never
        // reports a position. A shooter that has never been seen cannot aim, and
        // its coordinates would otherwise read as the origin.
        MoveTo(victim, victimId, 3f, 0f);

        var before = VictimHealth();

        Fire("Pistol", dirX: 1f);
        Pump(400);

        Assert.Equal(before, VictimHealth());
    }

    [Fact]
    public void NamingATargetDoesNotHitAPlayerTheShotIsNotPointedAt()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        // The victim is behind the shooter, and the shot is aimed the other way.
        MoveTo(victim, victimId, -3f, 0f);

        var before = VictimHealth();

        // The message says who to hit. A client that can pick its target can hit
        // someone it never aimed at, which is why the aim has to decide and not
        // the message. The distance check alone would let this through: the
        // victim is well inside a pistol's range.
        FireAt(victimId, "Pistol", dirX: 1f);
        Pump(400);

        Assert.Equal(before, VictimHealth());
    }

    [Fact]
    public void NamingATargetDoesNotHitAPlayerOffToTheSide()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        MoveTo(victim, victimId, 0f, 3f);

        var before = VictimHealth();

        FireAt(victimId, "Pistol", dirX: 1f);
        Pump(400);

        Assert.Equal(before, VictimHealth());
    }

    [Fact]
    public void NamingAPlayerTheShotIsAimedAtStillLands()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        MoveTo(victim, victimId, 3f, 0f);

        var before = VictimHealth();

        // A named target that agrees with the aim is still a hit. Refusing every
        // named target would break the caller that sends one, and the test that
        // matters is that the aim decides, not that the field is always dropped.
        FireAt(victimId, "Pistol", dirX: 1f);
        Pump(400);

        Assert.True(
            VictimHealth() < before,
            $"a shot aimed at a named target did no damage: {before} -> {VictimHealth()}");
    }

    [Fact]
    public void NamingATargetDoesNotLetAShooterWithoutAPositionHit()
    {
        SetUpRoom();
        MoveTo(victim, victimId, 3f, 0f);

        var before = VictimHealth();

        // The target is named and in range, but the shooter has never been seen
        // at a position, so the server has no origin to measure a line from.
        FireAt(victimId, "Pistol", dirX: 1f);
        Pump(400);

        Assert.Equal(before, VictimHealth());
    }

    [Fact]
    public void AGrenadeThrownOverRealtimeReachesTheSimulation()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        // A cluster grenade has a two second fuse and travels at twelve units a
        // second, so it bursts about twenty four units down the line. The blast
        // radius is three, so the player stands inside that.
        MoveTo(victim, victimId, 24f, 0f);

        var before = VictimHealth();

        // The throw message has to reach the simulator at all. The projectile
        // model is unit tested with no socket, so nothing here proved the message
        // was routed to it: the allow list and the dispatch name are the same two
        // places the item claim and the shot were both wrong at.
        ThrowGrenade("Cluster", dirX: 1f);
        Pump(300);

        Assert.Equal(1, ProjectileCount());

        // The fuse has to run out, which only happens when the loop steps the
        // simulation, and a test that drives the loop itself has to drive that.
        RunProjectiles(4200);

        Assert.Equal(0, ProjectileCount());
        Assert.True(
            VictimHealth() < before,
            $"a grenade that went off beside the player did no damage: {before} -> {VictimHealth()}");
    }

    [Fact]
    public void AGrenadeDoesNotHurtThePlayerWhoThrewIt()
    {
        SetUpRoom();
        MoveTo(shooter, shooterId, 0f, 0f);
        MoveTo(victim, victimId, 24f, 0f);

        var before = ShooterHealth();

        ThrowGrenade("Cluster", dirX: 1f);
        Pump(300);
        RunProjectiles(4200);

        // The thrower is not a victim of their own throw. A grenade that is
        // thrown rather than dropped starts at the hand, so including the owner
        // means every throw costs the thrower health, which is its own kind of
        // wrong: the blast damage code already skips the owner for bullets.
        Assert.Equal(before, ShooterHealth());
    }

    [Fact]
    public void AGrenadeThrownFromAPlayerWithNoPositionDoesNothing()
    {
        SetUpRoom();
        MoveTo(victim, victimId, 24f, 0f);

        var before = VictimHealth();

        // The thrower has never reported a position, so its coordinates in the
        // message are a claim about where it is standing, not a fact.
        ThrowGrenade("Cluster", dirX: 1f);
        Pump(300);

        Assert.Equal(0, ProjectileCount());
        Assert.Equal(before, VictimHealth());
    }
}
