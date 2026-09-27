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

        public int ActiveCount => _projectiles.Count;

        public static int CalculateGrenadeDamage(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "power" => 160,
                "cluster" => 90,
                "fire" => 120,
                "magnet" => 80,
                _ => 110
            };
        }

        public static float CalculateGrenadeRadius(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "power" => 4.5f,
                "cluster" => 3.0f,
                "fire" => 3.5f,
                "magnet" => 2.5f,
                _ => 3.0f
            };
        }

        public static float CalculateGrenadeFuse(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "power" => 2.5f,
                "cluster" => 2.0f,
                "fire" => 2.2f,
                "magnet" => 2.0f,
                _ => 3.0f
            };
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
            string grenadeType)
        {
            var id = NewProjectileId("grenade");
            var projectile = new ServerProjectileState
            {
                ProjectileId = id,
                OwnerId = ownerId,
                RoomId = roomId,
                Kind = ServerProjectileKind.Grenade,
                Position = origin,
                Velocity = Normalize(direction) * GrenadeSpeed,
                Radius = GrenadeRadius,
                RemainingLifetime = GrenadeLifetimeSeconds,
                GrenadeType = grenadeType,
                ExplosionDamage = CalculateGrenadeDamage(grenadeType),
                ExplosionRadius = CalculateGrenadeRadius(grenadeType),
                FuseTime = CalculateGrenadeFuse(grenadeType)
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
            List<(ServerProjectileState Grenade, string TargetId, int Damage, Vector2 Hit)> hits = new();

            lock (_lock)
            {
                foreach (var projectile in _projectiles.Values)
                {
                    if (!projectile.IsActive)
                    {
                        continue;
                    }

                    projectile.RemainingLifetime -= deltaSeconds;
                    projectile.Position += projectile.Velocity * deltaSeconds;

                    if (projectile.Kind == ServerProjectileKind.Bullet)
                    {
                        if (TryResolveBulletHit(projectile, out var bulletHit))
                        {
                            hits.Add((projectile, bulletHit, projectile.Damage, projectile.Position));
                            Retire(projectile, expired);
                            continue;
                        }
                    }
                    else
                    {
                        projectile.ElapsedFuse += deltaSeconds;
                        if (projectile.ElapsedFuse >= projectile.FuseTime || projectile.RemainingLifetime <= 0f)
                        {
                            CollectClusterChildren(projectile, spawned);
                            CollectGrenadeHits(projectile, hits);
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
        }

        /// <summary>
        /// Drops every projectile, for a match that has ended.
        /// </summary>
        public void Clear()
        {
            _projectiles.Clear();
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

        private bool TryResolveBulletHit(ServerProjectileState projectile, out string targetId)
        {
            targetId = string.Empty;
            var players = PlayerIds?.Invoke();
            var lookup = PlayerPositionLookup;
            if (players == null || lookup == null)
            {
                return false;
            }

            foreach (var candidate in players)
            {
                if (string.Equals(candidate, projectile.OwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var position = lookup(candidate);
                if (Vector2.Distance(projectile.Position, position) <= projectile.Radius + PlayerHitRadius)
                {
                    targetId = candidate;
                    return true;
                }
            }

            return false;
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
                var position = lookup(candidate);
                var distance = Vector2.Distance(grenade.Position, position);
                if (distance > grenade.ExplosionRadius)
                {
                    continue;
                }

                // Falls off linearly to the edge of the blast, so a player right
                // next to the grenade takes the most and one at the rim the least.
                var falloff = 1f - Math.Clamp(distance / Math.Max(0.001f, grenade.ExplosionRadius), 0f, 1f);
                var damage = Math.Max(1, (int)MathF.Round(grenade.ExplosionDamage * falloff));
                hits.Add((grenade, candidate, damage, grenade.Position));
            }
        }

        private void CollectClusterChildren(ServerProjectileState grenade, List<ServerProjectileState> spawned)
        {
            if (!string.Equals(grenade.GrenadeType, "Cluster", StringComparison.OrdinalIgnoreCase))
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
                    Velocity = Normalize(direction) * (GrenadeSpeed * 1.15f),
                    Radius = 0.25f,
                    RemainingLifetime = 1.25f,
                    GrenadeType = "ClusterChild",
                    ExplosionDamage = 60,
                    ExplosionRadius = 2.0f,
                    FuseTime = 1.0f
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
