using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenGSCore;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests
{
    /// <summary>
    /// The enemies in a mission, and what they do.
    /// <para>
    /// The project had three enemy brain classes and not one enemy: no prefab, no
    /// scene, no spawner, and nothing on the server that knew an enemy could
    /// exist. A mission had nothing to fight, so its objective was a number
    /// nothing could move. These drive the server's half directly, with no socket,
    /// because that is the only way any of it can be checked.
    /// </para>
    /// </summary>
    public sealed class ServerEnemySimulatorTests
    {
        // ---- Spawning ----------------------------------------------------

        [Fact]
        public void ASpawnedEnemyIsAliveAtThePointItWasSpawnedAt()
        {
            var simulator = new ServerEnemySimulator();

            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, new Vector2(3f, 4f), health: 60);

            Assert.True(enemy.IsAlive);
            Assert.Equal(60, enemy.Health);
            Assert.Equal(60, enemy.MaxHealth);
            Assert.Equal(new Vector2(3f, 4f), enemy.Position);
            Assert.Equal(1, simulator.AliveCount);
            Assert.StartsWith("enemy_", enemy.EnemyId);
        }

        [Fact]
        public void AnEnemyIsToldWhenItEntersPlay()
        {
            var simulator = new ServerEnemySimulator();
            var spawned = new List<ServerEnemyState>();
            simulator.OnSpawn = spawned.Add;

            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero);

            Assert.Single(spawned);
            Assert.Equal(enemy.EnemyId, spawned[0].EnemyId);
        }

        [Fact]
        public void ATurretDoesNotWalk()
        {
            var simulator = new ServerEnemySimulator();

            var turret = simulator.Spawn("room-1", ServerEnemyKind.Turret, Vector2.Zero, moveSpeed: 5f);

            // A turret is a thing to shoot, not a thing that walks. The speed was
            // authored and ignored, so a turret built as a turret walked at the
            // party like anything else.
            Assert.Equal(0f, turret.MoveSpeed);
        }

        [Fact]
        public void AWaveArrivesAsARingRatherThanAColumn()
        {
            var simulator = new ServerEnemySimulator();

            var wave = simulator.SpawnWave("room-1", new Vector2(5f, 5f), 4, spacing: 2f);

            Assert.Equal(4, wave.Count);

            // Four enemies on a line can be met with one shot. A ring cannot, and
            // it is the ring that makes a wave a thing to fight rather than a
            // queue.
            var offsets = wave
                .Select(e => e.Position - new Vector2(5f, 5f))
                .Select(o => MathF.Round(Vector2.Distance(Vector2.Zero, o), 3))
                .ToList();
            Assert.True(offsets.Distinct().Count() > 1);
        }

        [Fact]
        public void ASpawnPointIsNotOnTopOfTheParty()
        {
            var simulator = new ServerEnemySimulator();
            simulator.PlayerIds = () => new[] { "p1", "p2" };
            simulator.PlayerPositionLookup = _ => new Vector2(0f, 0f);

            var point = simulator.PickSpawnPoint(new Vector2(0f, 0f), minimumDistanceFromPlayers: 8f, spread: 4f);

            // A wave that appeared on top of the party would be over before it
            // began.
            Assert.True(Vector2.Distance(point, Vector2.Zero) >= 8f);
        }

        // ---- Damage ------------------------------------------------------

        [Fact]
        public void AHitTakesOffTheDamageItCarried()
        {
            var simulator = new ServerEnemySimulator();
            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, health: 60);

            var applied = simulator.ApplyPlayerDamage(enemy.EnemyId, "p1", 25, new Vector2(0f, 0f));

            Assert.Equal(25, applied);
            Assert.Equal(35, simulator.Snapshot().Single(e => e.EnemyId == enemy.EnemyId).Health);
        }

        [Fact]
        public void AHitNeverTakesMoreThanIsLeft()
        {
            var simulator = new ServerEnemySimulator();
            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, health: 10);

            var applied = simulator.ApplyPlayerDamage(enemy.EnemyId, "p1", 500, Vector2.Zero);

            // The reported damage is what the enemy lost, not what the shot was
            // worth, so a mission's score counts the fight that happened rather
            // than the shots that were fired.
            Assert.Equal(10, applied);
        }

        [Fact]
        public void AHitOnSomethingAlreadyDeadDoesNothing()
        {
            var simulator = new ServerEnemySimulator();
            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, health: 10);
            simulator.ApplyPlayerDamage(enemy.EnemyId, "p1", 10, Vector2.Zero);

            var applied = simulator.ApplyPlayerDamage(enemy.EnemyId, "p1", 10, Vector2.Zero);

            // Two clients firing at the same enemy must not both be credited, or a
            // single enemy is worth a second hit from whoever was second.
            Assert.Equal(0, applied);
        }

        [Fact]
        public void AKillIsCreditedToWhoeverFinishedIt()
        {
            var simulator = new ServerEnemySimulator();
            var killed = new List<(string EnemyId, string AttackerId)>();
            simulator.OnKilled = (enemy, attacker) => killed.Add((enemy.EnemyId, attacker));

            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, health: 10);
            simulator.ApplyPlayerDamage(enemy.EnemyId, "p2", 10, Vector2.Zero);

            Assert.Single(killed);
            Assert.Equal("p2", killed[0].AttackerId);
        }

        [Fact]
        public void AHitOnNothingIsRefused()
        {
            var simulator = new ServerEnemySimulator();

            Assert.Equal(0, simulator.ApplyPlayerDamage("no-such-enemy", "p1", 25, Vector2.Zero));
            Assert.Equal(0, simulator.ApplyPlayerDamage("", "p1", 25, Vector2.Zero));
            Assert.Equal(0, simulator.ApplyPlayerDamage("enemy_x", "p1", 0, Vector2.Zero));
        }

        [Fact]
        public void ADamagedEnemyIsStillCountedUntilItIsDead()
        {
            var simulator = new ServerEnemySimulator();
            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, health: 60);

            simulator.ApplyPlayerDamage(enemy.EnemyId, "p1", 30, Vector2.Zero);

            // A mission's objective counts what is still standing, so a wounded
            // enemy is still part of it.
            Assert.Equal(1, simulator.AliveCount);
        }

        // ---- Behaviour ---------------------------------------------------

        [Fact]
        public void AnEnemyWalksTowardsTheNearestPlayer()
        {
            var simulator = new ServerEnemySimulator();
            simulator.PlayerIds = () => new[] { "p1", "p2" };
            simulator.PlayerPositionLookup = id => id == "p1" ? new Vector2(20f, 0f) : new Vector2(2f, 0f);

            simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, moveSpeed: 2f);

            simulator.Update(0.5f, nowSeconds: 0d);

            // It went for the nearer one, not the first in the list.
            var after = simulator.Snapshot().Single();
            Assert.True(after.Position.X > 0f);
            Assert.True(after.Position.X < 2f);
        }

        [Fact]
        public void AnEnemyStopsAtItsAttackRange()
        {
            var simulator = new ServerEnemySimulator();
            simulator.PlayerIds = () => new[] { "p1" };
            simulator.PlayerPositionLookup = _ => new Vector2(3f, 0f);

            simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, moveSpeed: 5f, attackRange: 2f);

            simulator.Update(2f, nowSeconds: 0d);

            // It walks up and then holds its ground rather than walking into the
            // player it is shooting.
            var after = simulator.Snapshot().Single();
            Assert.True(after.Position.X <= 3f);
        }

        [Fact]
        public void AnEnemyThatReachesAPlayerHitsThem()
        {
            var simulator = new ServerEnemySimulator();
            var hits = new List<(string PlayerId, int Damage)>();
            simulator.PlayerIds = () => new[] { "p1" };
            simulator.PlayerPositionLookup = _ => new Vector2(1f, 0f);
            simulator.OnPlayerDamaged = (playerId, enemy, damage, hit) => hits.Add((playerId, damage));

            simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, attackDamage: 12, attackRange: 2f);

            simulator.Update(0.1f, nowSeconds: 10d);

            Assert.Single(hits);
            Assert.Equal("p1", hits[0].PlayerId);
            Assert.Equal(12, hits[0].Damage);
        }

        [Fact]
        public void AnEnemyCannotHitFasterThanItsInterval()
        {
            var simulator = new ServerEnemySimulator();
            var hits = 0;
            simulator.PlayerIds = () => new[] { "p1" };
            simulator.PlayerPositionLookup = _ => new Vector2(0.5f, 0f);
            simulator.OnPlayerDamaged = (playerId, enemy, damage, hit) => hits++;

            simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, attackDamage: 10, attackIntervalSeconds: 1f);

            simulator.Update(0.1f, nowSeconds: 0d);
            simulator.Update(0.1f, nowSeconds: 0.2f);

            // Two ticks well inside one interval is one hit, not two. Without the
            // interval an enemy standing on a player empties them in a single tick.
            Assert.Equal(1, hits);

            simulator.Update(0.1f, nowSeconds: 1.5d);
            Assert.Equal(2, hits);
        }

        [Fact]
        public void AnEnemyDoesNothingWithoutAPlayerToGoAfter()
        {
            var simulator = new ServerEnemySimulator();
            simulator.PlayerIds = () => Array.Empty<string>();
            simulator.PlayerPositionLookup = _ => Vector2.Zero;
            var hits = 0;
            simulator.OnPlayerDamaged = (playerId, enemy, damage, hit) => hits++;

            simulator.Spawn("room-1", ServerEnemyKind.Chaser, new Vector2(5f, 5f), moveSpeed: 3f);

            simulator.Update(0.5f, nowSeconds: 0d);

            // It does not wander, and it does not invent a target.
            Assert.Equal(0, hits);
            Assert.Equal(new Vector2(5f, 5f), simulator.Snapshot().Single().Position);
        }

        [Fact]
        public void ATurretShootsWithoutWalkingAnywhere()
        {
            var simulator = new ServerEnemySimulator();
            var hits = new List<string>();
            simulator.PlayerIds = () => new[] { "p1" };
            simulator.PlayerPositionLookup = _ => new Vector2(5f, 0f);
            simulator.OnPlayerDamaged = (playerId, enemy, damage, hit) => hits.Add(playerId);

            simulator.Spawn("room-1", ServerEnemyKind.Turret, Vector2.Zero, attackDamage: 8, attackRange: 10f, moveSpeed: 4f);

            simulator.Update(0.5f, nowSeconds: 0d);

            // It fires from where it stands. A turret that walked to the party
            // was not a turret.
            Assert.Single(hits);
            Assert.Equal(Vector2.Zero, simulator.Snapshot().Single().Position);
        }

        // ---- Hit resolution ----------------------------------------------

        [Fact]
        public void AShotLineThatPassesThroughAnEnemyFindsIt()
        {
            var simulator = new ServerEnemySimulator();
            simulator.Spawn("room-1", ServerEnemyKind.Chaser, new Vector2(3f, 0f));

            var found = simulator.TryResolveEnemyAt(new Vector2(3f, 0f), radius: 0.1f, out var enemy);

            Assert.True(found);
            Assert.Equal(3f, enemy.Position.X);
        }

        [Fact]
        public void AShotLineThatPassesBesideAnEnemyFindsNothing()
        {
            var simulator = new ServerEnemySimulator();
            simulator.Spawn("room-1", ServerEnemyKind.Chaser, new Vector2(0f, 5f));

            Assert.False(simulator.TryResolveEnemyAt(Vector2.Zero, radius: 0.1f, out _));
        }

        [Fact]
        public void ADeadEnemyIsNotOnTheShotLine()
        {
            var simulator = new ServerEnemySimulator();
            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, new Vector2(3f, 0f), health: 10);
            simulator.ApplyPlayerDamage(enemy.EnemyId, "p1", 10, Vector2.Zero);

            // A corpse that is still a target is an enemy that can be farmed for
            // objective progress.
            Assert.False(simulator.TryResolveEnemyAt(new Vector2(3f, 0f), 0.1f, out _));
        }

        // ---- Tearing down -----------------------------------------------

        [Fact]
        public void ClearingDropsEveryEnemyAndSaysSo()
        {
            var simulator = new ServerEnemySimulator();
            var despawned = 0;
            simulator.OnDespawn = _ => despawned++;
            simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero);
            simulator.Spawn("room-1", ServerEnemyKind.Gunner, new Vector2(4f, 0f));

            simulator.Clear();

            Assert.Equal(0, simulator.AliveCount);
            Assert.Equal(2, despawned);
        }

        [Fact]
        public void ADeadEnemyIsToldItHasLeftPlay()
        {
            var simulator = new ServerEnemySimulator();
            var enemy = simulator.Spawn("room-1", ServerEnemyKind.Chaser, Vector2.Zero, health: 5);
            var despawned = new List<string>();
            simulator.OnDespawn = e => despawned.Add(e.EnemyId);

            simulator.ApplyPlayerDamage(enemy.EnemyId, "p1", 5, Vector2.Zero);
            simulator.Update(0.1f, nowSeconds: 0d);

            // The client is told to take it off the map, or a dead enemy stands
            // there until the match ends.
            Assert.Single(despawned);
        }
    }
}