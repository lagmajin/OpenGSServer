using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

#nullable enable

namespace OpenGSServer.Network
{
    public enum ServerProjectileKind
    {
        Bullet,
        Grenade
    }

    public sealed class ServerProjectileState
    {
        public string ProjectileId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public ServerProjectileKind Kind { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Radius { get; set; }
        public float RemainingLifetime { get; set; }
        public bool IsActive { get; set; } = true;
        public string WeaponType { get; set; } = string.Empty;
        public string GrenadeType { get; set; } = string.Empty;
        public int Damage { get; set; }
        public int ExplosionDamage { get; set; }
        public float ExplosionRadius { get; set; }
        public float FuseTime { get; set; }
        public float ElapsedFuse { get; set; }
        public float Gravity { get; set; }
    }

    /// <summary>
    /// Server side flight of bullets and grenades.
    /// <para>
    /// The simulation lived inside MatchRUdpServerManager, which nothing
    /// constructs, so a grenade throw was logged and echoed back to the room
    /// and never simulated. This is the same model, moved somewhere the live
    /// match path can reach.
    /// </para>
    /// <para>
    /// The simulator owns the projectile list and the timing but knows nothing
    /// about transport: the caller supplies where players are and receives
    /// spawn, explode, and damage callbacks. That keeps the rules testable
    /// without a socket.
    /// </para>
    /// </summary>
    public sealed class ServerProjectileSimulator
    {
        public const float BulletSpeed = 22f;
        public const float GrenadeSpeed = 12f;
        public const float GrenadeLifetimeSeconds = 4.0f;
        public const float GrenadeRadius = 0.35f;
        public const float PlayerHitRadius = 0.6f;
        public const float GrenadeGravity = 18.0f;
        public const float GrenadeArmingSeconds = 0.15f;
        public const float RocketProjectileSpeed = 30.0f;
        public const float LaunchedGrenadeSpeed = 18.0f;

        private readonly ConcurrentDictionary<string, ServerProjectileState> _projectiles =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        /// <summary>
        /// Where a player is right now, keyed by player id.
        /// </summary>
        public Func<string, Vector2>? PlayerPositionLookup { get; set; }

        /// <summary>
        /// Players currently in play, used to resolve hits.
        /// </summary>
        public Func<IEnumerable<string>>? PlayerIds { get; set; }

        /// <summary>Whether a projectile's match is still accepting combat.</summary>
        public Func<string, bool>? IsRoomActive { get; set; }

        /// <summary>
        /// Called when a projectile enters play, so the room can be told.
        /// </summary>
        public Action<ServerProjectileState, string>? OnSpawn { get; set; }

        /// <summary>
        /// Called when a projectile leaves play.
        /// </summary>
        public Action<ServerProjectileState>? OnExpire { get; set; }

        /// <summary>
        /// Called when a projectile hits a player. The target, the owner, the
        /// damage, and where the hit landed.
        /// </summary>
        public Action<string, string, int, Vector2>? OnDamage { get; set; }

        /// <summary>
        /// Resolves the first enemy touched by a bullet segment in its room.
        /// The caller owns enemy state and returns the enemy ID and impact point;
        /// the simulator compares that hit with player collisions.
        /// </summary>
        public Func<string, Vector2, Vector2, (string EnemyId, Vector2 HitPosition)>?
            ResolveEnemyOnSegment { get; set; }

        /// <summary>
        /// Called when a bullet reaches an enemy. Includes its room, enemy ID,
        /// owner, impact point, and damage.
        /// </summary>
        public Action<string, string, string, Vector2, int>? OnBulletHitEnemy { get; set; }

        /// <summary>Called when a grenade blast can damage mission enemies.</summary>
        public Action<ServerProjectileState>? OnGrenadeExplode { get; set; }

        public int ActiveCount => _projectiles.Count;

        public IReadOnlyList<ServerProjectileState> SnapshotActiveGrenades(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) return Array.Empty<ServerProjectileState>();

            lock (_lock)
            {
                return _projectiles.Values
                    .Where(projectile => projectile.IsActive &&
                        projectile.Kind == ServerProjectileKind.Grenade &&
                        string.Equals(projectile.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
                    .Select(projectile => new ServerProjectileState
                    {
                        ProjectileId = projectile.ProjectileId,
                        OwnerId = projectile.OwnerId,
                        RoomId = projectile.RoomId,
                        Kind = projectile.Kind,
                        Position = projectile.Position,
                        Velocity = projectile.Velocity,
                        Radius = projectile.Radius,
                        RemainingLifetime = projectile.RemainingLifetime,
                        IsActive = projectile.IsActive,
                        WeaponType = projectile.WeaponType,
                        GrenadeType = projectile.GrenadeType,
                        Damage = projectile.Damage,
                        ExplosionDamage = projectile.ExplosionDamage,
                        ExplosionRadius = projectile.ExplosionRadius,
                        FuseTime = projectile.FuseTime,
                        ElapsedFuse = projectile.ElapsedFuse,
                        Gravity = projectile.Gravity
                    })
                    .ToArray();
            }
        }

        public static int CalculateGrenadeDamage(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "rocketprojectile" => 180,
                "launchedgrenade" => 120,
                "smoke" or "smokegrenade" => 0,
                "fire" or "firegrenade" => 150,
                "clusterchild" or "childclustergrenade" => 30,
                "normal" or "normalgrenade" or "power" or "powergrenade" or
                    "cluster" or "clustergrenade" or "magnet" or "magnetic" or
                    "magneticgrenade" or "mine" or "minegrenade" => 120,
                _ => 120
            };
        }

        public static float CalculateGrenadeRadius(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "rocketprojectile" => 3.5f,
                _ => 2.5f
            };
        }

        public static float CalculateGrenadeFuse(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "rocketprojectile" => 1.5f,
                "launchedgrenade" => 2.5f,
                "power" or "powergrenade" => 2.0f,
                "cluster" or "clustergrenade" => 2.0f,
                "fire" or "firegrenade" => 1.125f,
                "magnet" or "magnetic" or "magneticgrenade" => 2.0f,
                "smoke" or "smokegrenade" => 2.0f,
                _ => 3.0f
            };
        }

        public static float CalculateGrenadeGravity(string? grenadeType)
        {
            if (string.Equals(grenadeType, "RocketProjectile", StringComparison.OrdinalIgnoreCase))
                return 0.0f;
            if (string.Equals(grenadeType, "LaunchedGrenade", StringComparison.OrdinalIgnoreCase))
                return 10.0f;
            return string.Equals(grenadeType, "Smoke", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grenadeType, "SmokeGrenade", StringComparison.OrdinalIgnoreCase)
                ? 14.0f
                : GrenadeGravity;
        }

        public string CreateBullet(
            string roomId,
            string ownerId,
            Vector2 origin,
            Vector2 direction,
            float damage,
            string weaponType)
        {
            var id = NewProjectileId("bullet");
            var projectile = new ServerProjectileState
            {
                ProjectileId = id,
                OwnerId = ownerId,
                RoomId = roomId,
                Kind = ServerProjectileKind.Bullet,
                Position = origin,
                Velocity = Normalize(direction) * BulletSpeed,
                Radius = 0.1f,
                RemainingLifetime = 1.5f,
                WeaponType = weaponType,
                Damage = (int)MathF.Round(Math.Max(1f, damage))
            };

            Add(projectile);
            return id;
        }

        public string CreateGrenade(
            string roomId,
            string ownerId,
            Vector2 origin,
            Vector2 direction,
            string grenadeType,
            float power = 1f)
        {
            power = float.IsFinite(power) ? Math.Clamp(power, 0.1f, 1.0f) : 1.0f;
            var normalizedType = grenadeType.ToLowerInvariant();
            var speed = normalizedType switch
            {
                "rocketprojectile" => RocketProjectileSpeed,
                "launchedgrenade" => LaunchedGrenadeSpeed,
                _ => GrenadeSpeed
            };
            var id = NewProjectileId(normalizedType == "rocketprojectile" ? "rocket" : "grenade");
            var projectile = new ServerProjectileState
            {
                ProjectileId = id,
                OwnerId = ownerId,
                RoomId = roomId,
                Kind = ServerProjectileKind.Grenade,
                Position = origin,
                Velocity = Normalize(direction) * (speed * power),
                Radius = GrenadeRadius,
                RemainingLifetime = GrenadeLifetimeSeconds,
                GrenadeType = grenadeType,
                ExplosionDamage = CalculateGrenadeDamage(grenadeType),
                ExplosionRadius = CalculateGrenadeRadius(grenadeType),
                FuseTime = CalculateGrenadeFuse(grenadeType),
                Gravity = CalculateGrenadeGravity(grenadeType)
            };

            Add(projectile);
            return id;
        }

        /// <summary>
        /// Advances every projectile by one tick.
        /// </summary>
        public void Update(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || !float.IsFinite(deltaSeconds))
            {
                return;
            }

            List<ServerProjectileState> spawned = new();
            List<ServerProjectileState> expired = new();
            List<ServerProjectileState> detonated = new();
            List<(ServerProjectileState Grenade, string TargetId, int Damage, Vector2 Hit)> hits = new();
            List<(string RoomId, string EnemyId, string OwnerId, Vector2 Hit, int Damage)> enemyHits = new();

            lock (_lock)
            {
                foreach (var projectile in _projectiles.Values)
                {
                    if (!projectile.IsActive)
                    {
                        continue;
                    }

                    if (IsRoomActive != null && !IsRoomActive(projectile.RoomId))
                    {
                        Retire(projectile, expired);
                        continue;
                    }

                    var previousPosition = projectile.Position;
                    var previousFuse = projectile.ElapsedFuse;
                    var collisionSegmentStart = previousPosition;
                    if (projectile.Kind == ServerProjectileKind.Grenade)
                    {
                        var simulationStep = Math.Min(deltaSeconds, projectile.RemainingLifetime);
                        simulationStep = Math.Min(simulationStep,
                            Math.Max(0f, projectile.FuseTime - projectile.ElapsedFuse));
                        projectile.RemainingLifetime -= simulationStep;
                        projectile.Velocity += new Vector2(0f, -projectile.Gravity * simulationStep);
                        projectile.Position += projectile.Velocity * simulationStep;
                        projectile.ElapsedFuse += simulationStep;
                        if (previousFuse < GrenadeArmingSeconds &&
                            projectile.ElapsedFuse >= GrenadeArmingSeconds && simulationStep > 0f)
                        {
                            var armedFraction = (GrenadeArmingSeconds - previousFuse) / simulationStep;
                            collisionSegmentStart = Vector2.Lerp(
                                previousPosition, projectile.Position, armedFraction);
                        }
                    }
                    else
                    {
                        var simulationStep = Math.Min(deltaSeconds, projectile.RemainingLifetime);
                        projectile.RemainingLifetime -= simulationStep;
                        projectile.Position += projectile.Velocity * simulationStep;
                    }

                    if (projectile.Kind == ServerProjectileKind.Bullet)
                    {
                        var enemyHit = ResolveEnemyOnSegment?.Invoke(
                            projectile.RoomId, previousPosition, projectile.Position) ??
                            (string.Empty, projectile.Position);
                        var hitEnemy = !string.IsNullOrEmpty(enemyHit.EnemyId);
                        var enemyHitPosition = enemyHit.HitPosition;
                        var hitPlayer = TryResolveBulletHit(
                            projectile, previousPosition, out var playerId, out var playerHitPosition);

                        // Players and mission enemies share one flight path. The
                        // nearest server-owned target absorbs the bullet.
                        if (hitEnemy && (!hitPlayer ||
                                Vector2.DistanceSquared(previousPosition, enemyHitPosition) <=
                                Vector2.DistanceSquared(previousPosition, playerHitPosition)))
                        {
                            enemyHits.Add((projectile.RoomId, enemyHit.EnemyId, projectile.OwnerId,
                                enemyHitPosition, projectile.Damage));
                            Retire(projectile, expired);
                            continue;
                        }

                        if (hitPlayer)
                        {
                            hits.Add((projectile, playerId, projectile.Damage, playerHitPosition));
                            Retire(projectile, expired);
                            continue;
                        }
                    }
                    else
                    {
                        // Check the whole traveled segment so a fast launcher
                        // round cannot cross a player between server ticks.
                        if (projectile.ElapsedFuse >= GrenadeArmingSeconds &&
                            TryResolveGrenadePlayerHit(
                                projectile, collisionSegmentStart, out var impactPosition))
                        {
                            projectile.Position = impactPosition;
                            CollectClusterChildren(projectile, spawned);
                            CollectGrenadeHits(projectile, hits);
                            detonated.Add(projectile);
                            Retire(projectile, expired);
                            continue;
                        }

                        if (projectile.ElapsedFuse >= projectile.FuseTime)
                        {
                            CollectClusterChildren(projectile, spawned);
                            CollectGrenadeHits(projectile, hits);
                            detonated.Add(projectile);
                            Retire(projectile, expired);
                            continue;
                        }
                    }

                    if (projectile.RemainingLifetime <= 0f)
                    {
                        Retire(projectile, expired);
                    }
                }
            }

            foreach (var child in spawned)
            {
                OnSpawn?.Invoke(child, "ChildClusterGrenade");
            }

            foreach (var projectile in expired)
            {
                OnExpire?.Invoke(projectile);
            }

            foreach (var hit in hits)
            {
                OnDamage?.Invoke(hit.TargetId, hit.Grenade.OwnerId, hit.Damage, hit.Hit);
            }

            foreach (var grenade in detonated)
            {
                OnGrenadeExplode?.Invoke(grenade);
            }

            foreach (var enemyHit in enemyHits)
            {
                OnBulletHitEnemy?.Invoke(enemyHit.RoomId, enemyHit.EnemyId,
                    enemyHit.OwnerId, enemyHit.Hit, enemyHit.Damage);
            }
        }

        /// <summary>
        /// Drops every projectile, for a match that has ended.
        /// </summary>
        public void Clear()
        {
            _projectiles.Clear();
        }

        public void ClearRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) return;

            List<ServerProjectileState> removed = new();
            lock (_lock)
            {
                foreach (var projectile in _projectiles.Values)
                {
                    if (!string.Equals(projectile.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    projectile.IsActive = false;
                    if (_projectiles.TryRemove(projectile.ProjectileId, out _))
                        removed.Add(projectile);
                }
            }

            foreach (var projectile in removed)
            {
                OnExpire?.Invoke(projectile);
            }
        }

        private void Add(ServerProjectileState projectile)
        {
            lock (_lock)
            {
                _projectiles[projectile.ProjectileId] = projectile;
            }

            OnSpawn?.Invoke(projectile, projectile.Kind == ServerProjectileKind.Bullet ? "Bullet" : "Grenade");
        }

        private void Retire(ServerProjectileState projectile, List<ServerProjectileState> expired)
        {
            projectile.IsActive = false;
            _projectiles.TryRemove(projectile.ProjectileId, out _);
            expired.Add(projectile);
        }

        private bool TryResolveBulletHit(
            ServerProjectileState projectile,
            Vector2 segmentStart,
            out string targetId,
            out Vector2 hitPosition)
        {
            targetId = string.Empty;
            hitPosition = projectile.Position;
            var players = PlayerIds?.Invoke();
            var lookup = PlayerPositionLookup;
            if (players == null || lookup == null)
            {
                return false;
            }

            var segment = projectile.Position - segmentStart;
            var segmentLengthSquared = segment.LengthSquared();
            var hitRadius = projectile.Radius + PlayerHitRadius;
            var hitRadiusSquared = hitRadius * hitRadius;
            var firstImpactAlong = float.PositiveInfinity;
            foreach (var candidate in players)
            {
                if (string.Equals(candidate, projectile.OwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var position = lookup(candidate);
                var closestAlong = segmentLengthSquared > 0.000001f
                    ? Math.Clamp(Vector2.Dot(position - segmentStart, segment) / segmentLengthSquared,
                        0f, 1f)
                    : 0f;
                var closestPoint = segmentStart + segment * closestAlong;
                var distanceSquared = Vector2.DistanceSquared(closestPoint, position);
                if (distanceSquared > hitRadiusSquared) continue;

                var entryOffset = segmentLengthSquared > 0.000001f
                    ? MathF.Sqrt(Math.Max(0f, hitRadiusSquared - distanceSquared) /
                        segmentLengthSquared)
                    : 0f;
                var impactAlong = Math.Max(0f, closestAlong - entryOffset);
                if (impactAlong >= firstImpactAlong) continue;
                firstImpactAlong = impactAlong;
                targetId = candidate;
                hitPosition = segmentStart + segment * impactAlong;
            }

            return !float.IsPositiveInfinity(firstImpactAlong);
        }

        private void CollectGrenadeHits(
            ServerProjectileState grenade,
            List<(ServerProjectileState Grenade, string TargetId, int Damage, Vector2 Hit)> hits)
        {
            var players = PlayerIds?.Invoke();
            var lookup = PlayerPositionLookup;
            if (players == null || lookup == null)
            {
                return;
            }

            foreach (var candidate in players)
            {
                if (string.Equals(candidate, grenade.OwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    // The thrower is not a victim of their own throw. A grenade
                    // leaves the hand, so the owner is inside its own blast radius
                    // on the first tick and would take the full damage every time
                    // they threw one. The bullet path already skips the owner.
                    continue;
                }

                var position = lookup(candidate);
                var distance = Vector2.Distance(grenade.Position, position);
                if (distance > grenade.ExplosionRadius)
                {
                    continue;
                }

                // Falls off linearly to the edge of the blast, so a player right
                // next to the grenade takes the most and one at the rim the least.
                var damage = ServerDamageResolver.BlastDamage(
                    grenade.ExplosionDamage, distance, grenade.ExplosionRadius);
                hits.Add((grenade, candidate, damage, grenade.Position));
            }
        }

        private bool TryResolveGrenadePlayerHit(
            ServerProjectileState grenade, Vector2 segmentStart, out Vector2 impactPosition)
        {
            impactPosition = grenade.Position;
            var players = PlayerIds?.Invoke();
            var lookup = PlayerPositionLookup;
            if (players == null || lookup == null)
            {
                return false;
            }

            var segment = grenade.Position - segmentStart;
            var segmentLengthSquared = segment.LengthSquared();
            var hitRadius = grenade.Radius + PlayerHitRadius;
            var hitRadiusSquared = hitRadius * hitRadius;
            var firstImpactAlong = float.PositiveInfinity;
            var firstImpactPosition = grenade.Position;
            foreach (var candidate in players)
            {
                if (string.Equals(candidate, grenade.OwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var position = lookup(candidate);
                var closestAlong = segmentLengthSquared > 0.000001f
                    ? Math.Clamp(Vector2.Dot(position - segmentStart, segment) / segmentLengthSquared,
                        0f, 1f)
                    : 0f;
                var closestPoint = segmentStart + segment * closestAlong;
                var distanceSquared = Vector2.DistanceSquared(closestPoint, position);
                if (distanceSquared > hitRadiusSquared) continue;

                var entryOffset = segmentLengthSquared > 0.000001f
                    ? MathF.Sqrt(Math.Max(0f, hitRadiusSquared - distanceSquared) /
                        segmentLengthSquared)
                    : 0f;
                var impactAlong = Math.Max(0f, closestAlong - entryOffset);
                if (impactAlong >= firstImpactAlong) continue;
                firstImpactAlong = impactAlong;
                firstImpactPosition = segmentStart + segment * impactAlong;
            }

            if (float.IsPositiveInfinity(firstImpactAlong)) return false;
            impactPosition = firstImpactPosition;
            return true;
        }

        private void CollectClusterChildren(ServerProjectileState grenade, List<ServerProjectileState> spawned)
        {
            if (!string.Equals(grenade.GrenadeType, "Cluster", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(grenade.GrenadeType, "ClusterGrenade", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var directions = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(-0.35f, 0.94f),
                new Vector2(-0.35f, -0.94f)
            };

            foreach (var direction in directions)
            {
                var child = new ServerProjectileState
                {
                    ProjectileId = NewProjectileId("cluster-child"),
                    OwnerId = grenade.OwnerId,
                    RoomId = grenade.RoomId,
                    Kind = ServerProjectileKind.Grenade,
                    Position = grenade.Position,
                    Velocity = Normalize(direction) * 8.0f,
                    Radius = 0.25f,
                    RemainingLifetime = 3.0f,
                    GrenadeType = "ClusterChild",
                    ExplosionDamage = 30,
                    ExplosionRadius = 2.5f,
                    FuseTime = float.PositiveInfinity,
                    Gravity = 30.0f
                };

                lock (_lock)
                {
                    _projectiles[child.ProjectileId] = child;
                }

                spawned.Add(child);
            }
        }

        private static Vector2 Normalize(Vector2 direction)
        {
            if (direction.LengthSquared() < 0.0001f)
            {
                return new Vector2(1f, 0f);
            }

            return Vector2.Normalize(direction);
        }

        private static string NewProjectileId(string prefix)
        {
            return $"{prefix}_{Guid.NewGuid():N}";
        }
    }
}
