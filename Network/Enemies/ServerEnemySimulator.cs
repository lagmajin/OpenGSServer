using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

#nullable enable

namespace OpenGSServer.Network
{
    /// <summary>
    /// What kind of thing an enemy is, which is what decides how it moves and what
    /// it does when it reaches somebody.
    /// </summary>
    public enum ServerEnemyKind
    {
        /// <summary>Walks at the nearest player and hits them on contact.</summary>
        Chaser = 0,

        /// <summary>Walks to a range and shoots from there.</summary>
        Gunner = 1,

        /// <summary>Does not move. A thing to shoot that shoots back only on contact.</summary>
        Turret = 2
    }

    public sealed class ServerEnemyState
    {
        public string EnemyId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public ServerEnemyKind Kind { get; set; }

        public Vector2 Position { get; set; }
        public Vector2 Facing { get; set; } = new(1f, 0f);
        public string Timestamp { get; set; } = string.Empty;

        public int Health { get; set; }
        public int MaxHealth { get; set; }

        /// <summary>How fast it walks, in world units a second.</summary>
        public float MoveSpeed { get; set; }

        /// <summary>How close it has to be before it does its attack.</summary>
        public float AttackRange { get; set; }

        public float AttackDamage { get; set; }

        /// <summary>Seconds between attacks. Zero means as fast as it can reach.</summary>
        public float AttackIntervalSeconds { get; set; }

        /// <summary>
        /// How long until it may attack again. A clock, not a state that something
        /// else has to remember to advance.
        /// </summary>
        public double NextAttackAtSeconds { get; set; }

        public bool IsAlive => Health > 0;

        /// <summary>Which player it last hit, for the kill credit.</summary>
        public string CurrentTargetId { get; set; } = string.Empty;
    }

    /// <summary>
    /// The enemies in a match, and what they do.
    /// <para>
    /// The project had three enemy brain classes and not one enemy: no prefab, no
    /// scene, no spawner, and nothing on the server that knew an enemy could
    /// exist. A mission therefore had nothing to fight, and its objective was a
    /// number nothing could move.
    /// </para>
    /// <para>
    /// This is the server's half. It holds the health, the position and the
    /// behaviour, and it decides what a shot did, so an enemy's health is the
    /// server's number rather than whatever each client worked out. The client's
    /// half is told what happened and draws it.
    /// </para>
    /// <para>
    /// It is built like the projectile simulator on purpose: the same injected
    /// lookups and callbacks, no transport of its own. That is what makes it
    /// testable without a socket, which is the only way a mission's combat can be
    /// checked at all.
    /// </para>
    /// </summary>
    public sealed class ServerEnemySimulator
    {
        // EnemySnapshot is bounded to this many records by the native client
        // codec. Keep the authoritative room state within that wire limit so a
        // single oversized mission wave cannot make the entire snapshot fail.
        public const int MaxEnemiesPerRoom = 256;

        public const float DefaultHitRadius = 0.6f;

        private readonly ConcurrentDictionary<string, ServerEnemyState> _enemies =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();
        private readonly object _spawnLock = new();
        private readonly Random _random = new();
        private long _lastTimestampTicks;

        /// <summary>Where a player is right now, keyed by player id.</summary>
        public Func<string, Vector2>? PlayerPositionLookup { get; set; }

        /// <summary>The players in play.</summary>
        public Func<IEnumerable<string>>? PlayerIds { get; set; }

        /// <summary>Called when an enemy enters play.</summary>
        public Action<ServerEnemyState>? OnSpawn { get; set; }

        /// <summary>Called when an enemy leaves play for any reason.</summary>
        public Action<ServerEnemyState>? OnDespawn { get; set; }

        /// <summary>Called after an enemy's position or facing changes in a tick.</summary>
        public Action<ServerEnemyState>? OnStateChanged { get; set; }

        /// <summary>
        /// Called when an enemy takes damage. The enemy, the attacker, the damage
        /// that was actually applied, and where it landed.
        /// </summary>
        public Action<ServerEnemyState, string, int, Vector2>? OnDamaged { get; set; }

        /// <summary>
        /// Called when an enemy is destroyed. The enemy and whoever finished it.
        /// </summary>
        public Action<ServerEnemyState, string>? OnKilled { get; set; }

        /// <summary>
        /// Called when an enemy lands a hit on a player. The player, the enemy, and
        /// the damage.
        /// </summary>
        public Action<string, ServerEnemyState, int, Vector2>? OnPlayerDamaged { get; set; }

        public int AliveCount => _enemies.Values.Count(enemy => enemy.IsAlive);

        public IReadOnlyCollection<ServerEnemyState> Snapshot()
        {
            return Snapshot(out _);
        }

        public IReadOnlyCollection<ServerEnemyState> Snapshot(out string timestamp)
        {
            lock (_lock)
            {
                timestamp = NextTimestampLocked();
                return _enemies.Values
                    .Where(enemy => enemy.IsAlive)
                    .Select(CopyState)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>
        /// Puts an enemy into the match.
        /// </summary>
        public ServerEnemyState Spawn(
            string roomId,
            ServerEnemyKind kind,
            Vector2 position,
            int health = 60,
            float moveSpeed = 2.5f,
            float attackRange = 1.2f,
            int attackDamage = 15,
            float attackIntervalSeconds = 1.0f)
        {
            ValidateSpawn(roomId, kind, position, health, moveSpeed, attackRange,
                attackDamage, attackIntervalSeconds);
            lock (_spawnLock)
            {
                var enemy = new ServerEnemyState
                {
                    EnemyId = $"enemy_{Guid.NewGuid():N}",
                    RoomId = roomId,
                    Kind = kind,
                    Position = position,
                    Health = Math.Max(1, health),
                    MaxHealth = Math.Max(1, health),
                    MoveSpeed = Math.Max(0f, moveSpeed),
                    AttackRange = Math.Max(0f, attackRange),
                    AttackDamage = Math.Max(0, attackDamage),
                    AttackIntervalSeconds = Math.Max(0f, attackIntervalSeconds)
                };

                if (kind == ServerEnemyKind.Turret)
                {
                    // A turret is a thing to shoot, not a thing that walks.
                    enemy.MoveSpeed = 0f;
                }

                lock (_lock)
                {
                    if (_enemies.Count >= MaxEnemiesPerRoom)
                    {
                        throw new InvalidOperationException(
                            $"A room cannot contain more than {MaxEnemiesPerRoom} enemies.");
                    }

                    _enemies[enemy.EnemyId] = enemy;
                    enemy.Timestamp = NextTimestampLocked();
                }

                OnSpawn?.Invoke(CopyState(enemy));
                return CopyState(enemy);
            }
        }

        /// <summary>
        /// Puts a spread of enemies in, the way a mission wants them.
        /// </summary>
        public IReadOnlyList<ServerEnemyState> SpawnWave(
            string roomId,
            Vector2 centre,
            int count,
            float spacing = 2f,
            ServerEnemyKind kind = ServerEnemyKind.Chaser,
            int health = 60,
            int attackDamage = 15)
        {
            if (string.IsNullOrWhiteSpace(roomId) || roomId.Length > 128)
                throw new ArgumentException("A room id is required.", nameof(roomId));
            if (!Enum.IsDefined(typeof(ServerEnemyKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!IsValidPosition(centre))
                throw new ArgumentOutOfRangeException(nameof(centre));
            if (!float.IsFinite(spacing) || spacing < 0f || spacing > 100_000f)
                throw new ArgumentOutOfRangeException(nameof(spacing));
            if (health > 1_000_000)
                throw new ArgumentOutOfRangeException(nameof(health));
            if (attackDamage < 0 || attackDamage > 1_000_000)
                throw new ArgumentOutOfRangeException(nameof(attackDamage));

            lock (_spawnLock)
            {
                int availableSlots;
                lock (_lock)
                {
                    availableSlots = Math.Max(0, MaxEnemiesPerRoom - _enemies.Count);
                }

                var total = Math.Clamp(count, 0, availableSlots);
                var spawned = new List<ServerEnemyState>(total);
                var positions = new Vector2[total];

                for (var i = 0; i < total; i++)
                {
                    // A ring, so a wave is not a line and does not arrive as one
                    // column that can be met with one shot.
                    var angle = (MathF.Tau * i) / MathF.Max(1, total);
                    var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spacing * (1f + i * 0.1f);
                    positions[i] = centre + offset;
                    if (!IsValidPosition(positions[i]))
                        throw new ArgumentOutOfRangeException(nameof(spacing),
                            "The requested wave extends beyond the supported world bounds.");
                }

                foreach (var position in positions)
                {
                    spawned.Add(Spawn(
                        roomId,
                        kind,
                        position,
                        health: health,
                        attackDamage: attackDamage));
                }

                return spawned;
            }
        }

        private static void ValidateSpawn(
            string roomId,
            ServerEnemyKind kind,
            Vector2 position,
            int health,
            float moveSpeed,
            float attackRange,
            int attackDamage,
            float attackIntervalSeconds)
        {
            if (string.IsNullOrWhiteSpace(roomId) || roomId.Length > 128)
                throw new ArgumentException("A room id is required.", nameof(roomId));
            if (!Enum.IsDefined(typeof(ServerEnemyKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!IsValidPosition(position))
                throw new ArgumentOutOfRangeException(nameof(position));
            if (health > 1_000_000)
                throw new ArgumentOutOfRangeException(nameof(health));
            if (!float.IsFinite(moveSpeed) || moveSpeed > 1_000f ||
                !float.IsFinite(attackRange) || attackRange > 100_000f ||
                !float.IsFinite(attackIntervalSeconds) || attackIntervalSeconds > 86_400f)
                throw new ArgumentOutOfRangeException(nameof(moveSpeed),
                    "Enemy movement and attack values must be finite and within supported limits.");
            if (attackDamage < 0 || attackDamage > 1_000_000)
                throw new ArgumentOutOfRangeException(nameof(attackDamage));
        }

        private static bool IsValidPosition(Vector2 position) =>
            float.IsFinite(position.X) && float.IsFinite(position.Y) &&
            MathF.Abs(position.X) <= 100_000f && MathF.Abs(position.Y) <= 100_000f;

        public bool TryPeek(string enemyId, out ServerEnemyState enemy)
        {
            lock (_lock)
            {
                if (_enemies.TryGetValue(enemyId, out var found))
                {
                    enemy = CopyState(found);
                    return true;
                }
            }

            enemy = null!;
            return false;
        }

        /// <summary>
        /// Applies a hit from a player, and settles what it did.
        /// <para>
        /// The damage is the weapon's, not the shooter's claim about it, and a hit
        /// on something already dead does nothing. The kill is credited to the
        /// attacker so a mission's objective can be moved by the thing that
        /// achieved it rather than by a client saying it was achieved.
        /// </para>
        /// </summary>
        /// <returns>
        /// The damage that was applied, which is zero when the hit did not land.
        /// </returns>
        public int ApplyPlayerDamage(
            string enemyId,
            string attackerId,
            int damage,
            Vector2 hitPosition)
        {
            if (string.IsNullOrWhiteSpace(enemyId) || damage <= 0)
            {
                return 0;
            }

            ServerEnemyState? enemy = null;
            var applied = 0;
            var killed = false;

            lock (_lock)
            {
                if (!_enemies.TryGetValue(enemyId, out var found) || !found.IsAlive)
                {
                    return 0;
                }

                applied = Math.Min(damage, found.Health);
                found.Health -= applied;
                found.Timestamp = NextTimestampLocked();
                killed = found.Health <= 0;
                enemy = CopyState(found);
            }

            if (enemy == null)
            {
                return 0;
            }

            OnDamaged?.Invoke(enemy, attackerId ?? string.Empty, applied, hitPosition);

            if (killed)
            {
                OnKilled?.Invoke(enemy, attackerId ?? string.Empty);
            }

            return applied;
        }

        /// <summary>
        /// Advances every enemy by one tick.
        /// </summary>
        public void Update(float deltaSeconds, double nowSeconds)
        {
            if (deltaSeconds <= 0f || !float.IsFinite(deltaSeconds))
            {
                return;
            }

            var players = PlayerIds?.Invoke()?.ToList() ?? new List<string>();
            var lookup = PlayerPositionLookup;

            List<ServerEnemyState> dead = new();
            lock (_lock)
            {
                dead = _enemies.Values
                    .Where(enemy => !enemy.IsAlive)
                    .Select(CopyState)
                    .ToList();
            }

            // The dead are taken off the map whatever else is happening. This ran
            // after the early return, so a room with no players in it at that
            // moment, which is every room between a player leaving and the match
            // ending, kept its corpses for the rest of the match and the client
            // was never told to take them down.
            foreach (var enemy in dead)
            {
                Remove(enemy);
            }

            if (players.Count == 0 || lookup == null)
            {
                return;
            }

            var hits = new List<(string PlayerId, ServerEnemyState Enemy, int Damage, Vector2 Hit)>();
            var despawned = new List<ServerEnemyState>();
            var changed = new List<ServerEnemyState>();

            lock (_lock)
            {
                foreach (var enemy in _enemies.Values)
                {
                    if (!enemy.IsAlive)
                    {
                        despawned.Add(CopyState(enemy));
                        continue;
                    }

                    var previousPosition = enemy.Position;
                    var previousFacing = enemy.Facing;
                    var target = NearestPlayer(enemy, players, lookup);
                    if (target == null)
                    {
                        continue;
                    }

                    var targetId = target.Value.PlayerId;
                    var targetPosition = target.Value.Position;
                    enemy.CurrentTargetId = targetId;

                    var toTarget = targetPosition - enemy.Position;
                    var distance = Vector2.Distance(Vector2.Zero, toTarget);

                    if (distance > 0.0001f)
                    {
                        enemy.Facing = Vector2.Normalize(toTarget);
                    }

                    if (enemy.MoveSpeed > 0f && distance > enemy.AttackRange)
                    {
                        // A step no longer than the distance left, so a long tick
                        // cannot carry the enemy straight past the player it was
                        // walking towards. A single big step used to do exactly
                        // that: at five units a second and a two second tick, an
                        // enemy starting at the origin ended up on the far side of
                        // a player three units away, shooting away from them.
                        var step = enemy.MoveSpeed * deltaSeconds;
                        if (step > distance)
                        {
                            step = distance;
                        }

                        enemy.Position += enemy.Facing * step;
                    }

                    if (distance <= enemy.AttackRange + DefaultHitRadius &&
                        nowSeconds >= enemy.NextAttackAtSeconds)
                    {
                        enemy.NextAttackAtSeconds = nowSeconds + enemy.AttackIntervalSeconds;
                        if (enemy.AttackDamage > 0)
                        {
                            hits.Add((targetId, CopyState(enemy), (int)enemy.AttackDamage, enemy.Position));
                        }
                    }

                    if (Vector2.DistanceSquared(previousPosition, enemy.Position) > 0.0001f ||
                        Vector2.DistanceSquared(previousFacing, enemy.Facing) > 0.0025f)
                    {
                        enemy.Timestamp = NextTimestampLocked();
                        changed.Add(CopyState(enemy));
                    }
                }
            }

            foreach (var enemy in changed)
            {
                OnStateChanged?.Invoke(enemy);
            }

            foreach (var enemy in despawned)
            {
                Remove(enemy);
            }

            foreach (var hit in hits)
            {
                OnPlayerDamaged?.Invoke(hit.PlayerId, hit.Enemy, hit.Damage, hit.Hit);
            }
        }

        /// <summary>
        /// Whether a shot that arrived at this point reached an enemy.
        /// <para>
        /// The point and the radius are the caller's, because the caller is
        /// holding the line the shot was fired along and only it knows how far
        /// along it the shot has come. This asks whether anything is on it.
        /// </para>
        /// </summary>
        public bool TryResolveEnemyAt(Vector2 point, float radius, out ServerEnemyState enemy, string? excludeOwnerId = null)
        {
            foreach (var candidate in Snapshot())
            {
                if (string.Equals(candidate.EnemyId, excludeOwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Vector2.Distance(candidate.Position, point) <= radius + DefaultHitRadius)
                {
                    enemy = candidate;
                    return true;
                }
            }

            enemy = null!;
            return false;
        }

        public bool TryResolveEnemyOnSegment(
            Vector2 segmentStart,
            Vector2 segmentEnd,
            float radius,
            out ServerEnemyState enemy,
            out Vector2 hitPosition)
        {
            enemy = null!;
            hitPosition = segmentEnd;
            var segment = segmentEnd - segmentStart;
            var segmentLengthSquared = segment.LengthSquared();
            var hitRadius = Math.Max(0f, radius) + DefaultHitRadius;
            var hitRadiusSquared = hitRadius * hitRadius;
            var firstImpactAlong = float.PositiveInfinity;

            foreach (var candidate in Snapshot())
            {
                var closestAlong = segmentLengthSquared > 0.000001f
                    ? Math.Clamp(Vector2.Dot(candidate.Position - segmentStart, segment) /
                        segmentLengthSquared, 0f, 1f)
                    : 0f;
                var closestPoint = segmentStart + segment * closestAlong;
                var distanceSquared = Vector2.DistanceSquared(closestPoint, candidate.Position);
                if (distanceSquared > hitRadiusSquared) continue;

                var entryOffset = segmentLengthSquared > 0.000001f
                    ? MathF.Sqrt(Math.Max(0f, hitRadiusSquared - distanceSquared) /
                        segmentLengthSquared)
                    : 0f;
                var impactAlong = Math.Max(0f, closestAlong - entryOffset);
                if (impactAlong >= firstImpactAlong) continue;

                firstImpactAlong = impactAlong;
                enemy = candidate;
                hitPosition = segmentStart + segment * impactAlong;
            }

            return !float.IsPositiveInfinity(firstImpactAlong);
        }

        public void Remove(ServerEnemyState enemy)
        {
            if (enemy == null)
            {
                return;
            }

            ServerEnemyState? removed = null;
            lock (_lock)
            {
                if (_enemies.TryRemove(enemy.EnemyId, out var current))
                {
                    current.Timestamp = NextTimestampLocked();
                    removed = CopyState(current);
                }
            }

            // The periodic dead-enemy sweep, match cleanup, and explicit removal
            // can race. Only the operation that actually removed the authoritative
            // entry publishes its despawn, so clients see one terminal transition.
            if (removed != null)
            {
                OnDespawn?.Invoke(removed);
            }
        }

        /// <summary>Drops every enemy, for a match that has ended.</summary>
        public void Clear()
        {
            lock (_spawnLock)
            {
                List<ServerEnemyState> leaving;
                lock (_lock)
                {
                    foreach (var enemy in _enemies.Values)
                        enemy.Timestamp = NextTimestampLocked();
                    leaving = _enemies.Values.Select(CopyState).ToList();
                    _enemies.Clear();
                }

                foreach (var enemy in leaving)
                {
                    OnDespawn?.Invoke(CopyState(enemy));
                }
            }
        }

        private static ServerEnemyState CopyState(ServerEnemyState enemy)
        {
            return new ServerEnemyState
            {
                EnemyId = enemy.EnemyId,
                RoomId = enemy.RoomId,
                Kind = enemy.Kind,
                Position = enemy.Position,
                Facing = enemy.Facing,
                Timestamp = enemy.Timestamp,
                Health = enemy.Health,
                MaxHealth = enemy.MaxHealth,
                MoveSpeed = enemy.MoveSpeed,
                AttackRange = enemy.AttackRange,
                AttackDamage = enemy.AttackDamage,
                AttackIntervalSeconds = enemy.AttackIntervalSeconds,
                NextAttackAtSeconds = enemy.NextAttackAtSeconds,
                CurrentTargetId = enemy.CurrentTargetId
            };
        }

        private string NextTimestampLocked()
        {
            var ticks = Math.Max(DateTime.UtcNow.Ticks, _lastTimestampTicks + 1);
            _lastTimestampTicks = ticks;
            return new DateTime(ticks, DateTimeKind.Utc).ToString("o");
        }

        private static (string PlayerId, Vector2 Position)? NearestPlayer(
            ServerEnemyState enemy,
            List<string> players,
            Func<string, Vector2> lookup)
        {
            (string PlayerId, Vector2 Position)? nearest = null;
            var bestDistance = float.MaxValue;

            foreach (var playerId in players)
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    continue;
                }

                var position = lookup(playerId);
                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                {
                    continue;
                }
                var distance = Vector2.Distance(enemy.Position, position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                nearest = (playerId, position);
            }

            return nearest;
        }

        /// <summary>
        /// A point an enemy can be spawned at, away from where the players are.
        /// <para>
        /// A wave that appeared on top of the party would be a fight that was over
        /// before it began, and a wave that always appeared in the same place would
        /// be the same fight every time.
        /// </para>
        /// <para>
        /// When nothing far enough away is found inside the spread the search widens
        /// rather than giving up. It used to return the centre, which is the one
        /// point the caller was asking it not to return, so a map too small for the
        /// spread it was given put every wave on top of the party.
        /// </para>
        /// </summary>
        public Vector2 PickSpawnPoint(Vector2 centre, float minimumDistanceFromPlayers, float spread)
        {
            var players = PlayerIds?.Invoke()?.ToList() ?? new List<string>();
            var lookup = PlayerPositionLookup;

            // Eight attempts, each reaching further than the last, so a tight spread
            // still finds somewhere when there is room for one.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var widening = 1f + attempt;
                var angle = (MathF.Tau * (float)_random.NextDouble());
                var distance = spread * widening * (0.5f + (0.5f * (float)_random.NextDouble()));
                var candidate = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

                if (players.Count == 0 || lookup == null)
                {
                    return candidate;
                }

                var clear = players.All(playerId =>
                    Vector2.Distance(candidate, lookup(playerId)) >= minimumDistanceFromPlayers);
                if (clear)
                {
                    return candidate;
                }
            }

            // Nowhere far enough away. The furthest point reached is still better
            // than the centre the caller asked to be kept away from.
            return centre + new Vector2(spread * 4f, spread * 4f);
        }
    }
}
