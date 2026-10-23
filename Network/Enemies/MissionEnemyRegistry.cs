using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;

#nullable enable

namespace OpenGSServer
{
    /// <summary>
    /// The enemies in each room, and what they are doing to that room's mission.
    /// <para>
    /// One simulator per room, because a match is the unit a mission is played in
    /// and two rooms' enemies must not be able to see each other or each other's
    /// players. The registry is the seam the match handler reaches enemies through,
    /// so a shot can be resolved against one and a kill can move a mission's
    /// objective.
    /// </para>
    /// </summary>
    public static class MissionEnemyRegistry
    {
        private const double EnemyStateBroadcastIntervalSeconds = 0.10;
        private static readonly ConcurrentDictionary<string, ServerEnemySimulator> Simulators =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, ServerEnemyState> PendingEnemyStates =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly object SimulatorLifecycleLock = new();
        private static double NextEnemyStateBroadcastAtSeconds;

        /// <summary>
        /// Where a room's players are, read from the same lag compensation state
        /// the shot resolver uses, so an enemy walks towards where the server
        /// believes a player is rather than where a client says it is.
        /// </summary>
        private static Vector2 PlayerPosition(string playerId)
        {
            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (!state.HasAuthoritativePosition ||
                !string.Equals(state.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                return new Vector2(float.NaN, float.NaN);
            return new Vector2(state.PositionX, state.PositionY);
        }

        public static ServerEnemySimulator ForRoom(string roomId)
        {
            return Simulators.GetOrAdd(roomId, _ => BuildSimulator());
        }

        public static ServerEnemySimulator? TryGet(string roomId)
        {
            return Simulators.TryGetValue(roomId, out var simulator) ? simulator : null;
        }

        private static ServerEnemySimulator BuildSimulator()
        {
            var simulator = new ServerEnemySimulator
            {
                PlayerPositionLookup = PlayerPosition
            };

            simulator.OnSpawn = enemy => BroadcastEnemySpawn(enemy);
            simulator.OnDespawn = enemy => BroadcastEnemyDespawn(enemy);
            simulator.OnStateChanged = enemy => BroadcastEnemyState(enemy);
            simulator.OnDamaged = (enemy, attackerId, damage, hit) => BroadcastEnemyDamaged(enemy, attackerId, damage);
            simulator.OnPlayerDamaged = (playerId, enemy, damage, hit) =>
                ApplyEnemyDamageOnPlayer(playerId, enemy, damage, hit);

            simulator.OnKilled = (enemy, attackerId) => OnEnemyKilled(enemy, attackerId);

            return simulator;
        }

        /// <summary>
        /// Wires a simulator to the room it belongs to, so it knows the room's
        /// players and so its kills reach the room's mission.
        /// </summary>
        public static void Attach(string roomId, MatchRoom room)
        {
            if (room == null || !room.Playing || room.Finished || string.IsNullOrWhiteSpace(roomId))
                return;

            lock (SimulatorLifecycleLock)
            {
                var simulator = ForRoom(roomId);
                simulator.PlayerIds = () => room.Players
                    .Where(player => player != null && room.Playing && !room.Finished && player.Health > 0 &&
                        !InGameMatchEventHandler.HasStealthBuff(roomId, player.Id) &&
                        MatchServerV2.Instance.IsPlayerConnected(player.Id) &&
                        HasAuthoritativePosition(player.Id))
                    .Select(player => player.Id)
                    .ToList();

                simulator.OnSpawn = enemy => BroadcastEnemySpawn(enemy);
                simulator.OnDespawn = enemy => BroadcastEnemyDespawn(enemy);
                simulator.OnStateChanged = enemy => BroadcastEnemyState(enemy);
                simulator.OnDamaged = (enemy, attackerId, damage, hit) => BroadcastEnemyDamaged(enemy, attackerId, damage);
                simulator.OnKilled = (enemy, attackerId) => OnEnemyKilled(enemy, attackerId);
                simulator.OnPlayerDamaged = (playerId, enemy, damage, hit) =>
                    ApplyEnemyDamageOnPlayer(playerId, enemy, damage, hit);
            }
        }

        private static bool HasAuthoritativePosition(string playerId)
        {
            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            return state.HasAuthoritativePosition &&
                string.Equals(state.PlayerId, playerId, StringComparison.OrdinalIgnoreCase);
        }

        public static int AliveCount(string roomId)
        {
            return TryGet(roomId)?.AliveCount ?? 0;
        }

        /// <summary>
        /// Whether a shot line reached an enemy in this room.
        /// </summary>
        public static bool TryResolveOnShotLine(
            string roomId,
            float originX,
            float originY,
            float dirX,
            float dirY,
            float range,
            out string enemyId)
        {
            return TryResolveOnShotLine(
                roomId, originX, originY, dirX, dirY, range, out enemyId, out _);
        }

        public static bool TryResolveOnShotLine(
            string roomId,
            float originX,
            float originY,
            float dirX,
            float dirY,
            float range,
            out string enemyId,
            out float hitDistance)
        {
            enemyId = string.Empty;
            hitDistance = 0f;
            var simulator = TryGet(roomId);
            if (simulator == null)
            {
                return false;
            }

            if (!float.IsFinite(originX) || !float.IsFinite(originY) ||
                !float.IsFinite(range) || range < 0f)
            {
                return false;
            }

            var origin = new Vector2(originX, originY);
            var direction = new Vector2(dirX, dirY);
            var directionLengthSquared = direction.LengthSquared();
            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) ||
                !float.IsFinite(directionLengthSquared) || directionLengthSquared <= 0.000001f)
                return false;
            direction /= MathF.Sqrt(directionLengthSquared);
            var end = origin + direction * range;
            if (!float.IsFinite(end.X) || !float.IsFinite(end.Y)) return false;
            if (!simulator.TryResolveEnemyOnSegment(
                    origin, end, 0.35f, out var enemy, out var hitPosition))
                return false;

            enemyId = enemy.EnemyId;
            hitDistance = Vector2.Distance(origin, hitPosition);
            return true;
        }

        /// <summary>
        /// Whether an enemy in any active room is at this point.
        /// <para>
        /// Use the room-scoped overload for match projectiles. This lookup is only
        /// for callers that have no room identity to constrain the search.
        /// </para>
        /// </summary>
        public static bool TryResolveAnyEnemyAt(Vector2 point, float radius, out string enemyId)
        {
            foreach (var entry in Simulators)
            {
                if (entry.Value.TryResolveEnemyAt(point, radius, out var enemy))
                {
                    enemyId = enemy.EnemyId;
                    return true;
                }
            }

            enemyId = string.Empty;
            return false;
        }

        public static bool TryResolveEnemyAt(
            string roomId, Vector2 point, float radius, out string enemyId)
        {
            var simulator = TryGet(roomId);
            if (simulator != null && simulator.TryResolveEnemyAt(point, radius, out var enemy))
            {
                enemyId = enemy.EnemyId;
                return true;
            }

            enemyId = string.Empty;
            return false;
        }

        public static bool TryResolveEnemyOnSegment(
            string roomId,
            Vector2 segmentStart,
            Vector2 segmentEnd,
            float radius,
            out string enemyId,
            out Vector2 hitPosition)
        {
            var simulator = TryGet(roomId);
            if (simulator != null && simulator.TryResolveEnemyOnSegment(
                    segmentStart, segmentEnd, radius, out var enemy, out hitPosition))
            {
                enemyId = enemy.EnemyId;
                return true;
            }

            enemyId = string.Empty;
            hitPosition = segmentEnd;
            return false;
        }

        /// <summary>
        /// Applies a player's shot to an enemy, and moves the mission's objective
        /// when it kills one.
        /// </summary>
        /// <returns>The damage that was applied.</returns>
        public static int ApplyPlayerDamageToEnemy(
            string roomId,
            string enemyId,
            string attackerId,
            int damage,
            Vector2 hitPosition)
        {
            var room = MatchRoomManager.Instance.GetRoomById(roomId);
            if (room == null || !room.Playing || room.Finished)
                return 0;

            var simulator = TryGet(roomId);
            if (simulator == null)
            {
                return 0;
            }

            return simulator.ApplyPlayerDamage(enemyId, attackerId, damage, hitPosition);
        }

        public static void ApplyGrenadeDamageToEnemies(
            string roomId,
            string attackerId,
            Vector2 centre,
            int totalDamage,
            float radius)
        {
            if (totalDamage <= 0 || radius <= 0f || !float.IsFinite(radius) ||
                !float.IsFinite(centre.X) || !float.IsFinite(centre.Y))
                return;

            var simulator = TryGet(roomId);
            if (simulator == null) return;

            foreach (var enemy in simulator.Snapshot())
            {
                var distance = Vector2.Distance(centre, enemy.Position);
                var damage = ServerDamageResolver.BlastDamage(totalDamage, distance, radius);
                if (damage <= 0) continue;

                simulator.ApplyPlayerDamage(enemy.EnemyId, attackerId, damage, centre);
            }
        }

        /// <summary>
        /// Starts a mission's objective off, so there is something to fight.
        /// <summary>
        public static int BeginMissionWave(
            string roomId,
            Vector2 centre,
            int count,
            ServerEnemyKind kind = ServerEnemyKind.Chaser)
        {
            lock (SimulatorLifecycleLock)
            {
                if (MatchRoomManager.Instance.GetRoomById(roomId) is not MatchRoom room ||
                    !room.Playing || room.Finished || !Simulators.TryGetValue(roomId, out var simulator))
                    return 0;

                var spawnPoint = simulator.PickSpawnPoint(centre, minimumDistanceFromPlayers: 8f, spread: 4f);
                var wave = simulator.SpawnWave(roomId, spawnPoint, count, spacing: 2f, kind: kind);
                return wave.Count;
            }
        }

        /// <summary>Advances every room's enemies and announces what moved.</summary>
        public static void Update(float deltaSeconds, double nowSeconds, Action<string, ServerEnemyState, string>? onKilled = null)
        {
            lock (SimulatorLifecycleLock)
            {
                foreach (var entry in Simulators)
                {
                    entry.Value.Update(deltaSeconds, nowSeconds);
                }

                if (nowSeconds < NextEnemyStateBroadcastAtSeconds)
                    return;
                NextEnemyStateBroadcastAtSeconds = nowSeconds + EnemyStateBroadcastIntervalSeconds;
                foreach (var key in PendingEnemyStates.Keys)
                {
                    if (PendingEnemyStates.TryRemove(key, out var enemy))
                    {
                        GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, BuildEnemyMessage(
                            "EnemyState", enemy));
                    }
                }
            }
        }

        public static void ClearRoom(string roomId)
        {
            lock (SimulatorLifecycleLock)
            {
                if (Simulators.TryRemove(roomId, out var simulator))
                {
                    simulator.Clear();
                }

                var pendingPrefix = roomId + "\n";
                foreach (var key in PendingEnemyStates.Keys)
                {
                    if (key.StartsWith(pendingPrefix, StringComparison.OrdinalIgnoreCase))
                        PendingEnemyStates.TryRemove(key, out _);
                }
                if (Simulators.IsEmpty)
                    NextEnemyStateBroadcastAtSeconds = 0.0;
            }
        }

                /// <summary>
        /// An enemy was destroyed, so the room's mission is one step further on.
        /// </summary>
        private static void OnEnemyKilled(ServerEnemyState enemy, string attackerId)
        {
            var room = MatchRoomManager.Instance.GetRoomById(enemy.RoomId);
            if (room == null || !room.Playing || room.Finished)
            {
                return;
            }

            // The objective moves because the enemy is dead, which is the one
            // thing the server is in a position to say. Crediting the kill to the
            // player as well is what a scoreboard for a mission reads.
            if (!string.IsNullOrWhiteSpace(attackerId))
            {
                if (room.TryGetPlayer(attackerId, out var killer) && killer != null)
                {
                    var killCount = room.RecordKill(killer.Id, 100);
                    if (killCount > 0)
                    {
                        GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, new JObject
                        {
                            ["MessageType"] = "PlayerScoreState",
                            ["RoomID"] = enemy.RoomId,
                            ["PlayerID"] = killer.Id,
                            ["PlayerId"] = killer.Id,
                            ["PlayerKills"] = killCount,
                            ["IsTeamMode"] = room.Setting.Mode is EGameMode.TeamDeathMatch or
                                EGameMode.CaptureTheFlag or EGameMode.TeamSurvival,
                            ["RedTeamKills"] = room.TeamKillCount(ETeam.Red),
                            ["BlueTeamKills"] = room.TeamKillCount(ETeam.Blue),
                            ["Timestamp"] = DateTime.UtcNow.ToString("o")
                        });
                    }
                }
            }

            // The despawn is broadcast by the simulator's own OnDespawn when the
            // dead enemy is swept, so it is not sent again here.
            room.RecordObjectiveProgress(1);
            PublishObjectiveProgress(enemy.RoomId);
        }

        public static void PublishObjectiveProgress(string roomId)
        {
            var room = MatchRoomManager.Instance.GetRoomById(roomId);
            if (room?.CurrentSituation is not MissionMatchSituation mission)
            {
                return;
            }

            GameMessageDispatcher.BroadcastToRoom(roomId, new JObject
            {
                ["MessageType"] = GameMessageTypes.MissionObjectiveProgress,
                ["RoomID"] = roomId,
                ["ObjectiveCount"] = mission.ObjectiveCount,
                ["ObjectiveProgress"] = mission.ObjectiveProgress,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        private static void ApplyEnemyDamageOnPlayer(
            string playerId,
            ServerEnemyState enemy,
            int damage,
            Vector2 hit)
        {
            var room = MatchRoomManager.Instance.GetRoomById(enemy.RoomId);
            if (room == null || !room.Playing || room.Finished ||
                !MatchServerV2.Instance.IsPlayerConnected(playerId) ||
                !room.TryGetPlayer(playerId, out var player) || player == null ||
                ServerDamageResolver.IsDown(player))
            {
                return;
            }

            var simulator = TryGet(enemy.RoomId);
            if (simulator == null || !simulator.TryPeek(enemy.EnemyId, out var liveEnemy) ||
                !liveEnemy.IsAlive)
                return;

            var playerState = MatchServerV2.Instance.ServerLagCompensationManager
                .GetPlayerState(playerId);
            var attackReach = enemy.AttackRange + ServerEnemySimulator.DefaultHitRadius;
            var dx = playerState.PositionX - hit.X;
            var dy = playerState.PositionY - hit.Y;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (!playerState.HasAuthoritativePosition ||
                !string.Equals(playerState.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) ||
                !float.IsFinite(distanceSquared) || !float.IsFinite(attackReach) ||
                distanceSquared > attackReach * attackReach)
                return;

            var pose = room.GetPlayerPoseState(playerId);
            var outcome = ServerDamageResolver.Apply(player, damage, pose,
                InGameMatchEventHandler.HasDefenseBuff(enemy.RoomId, playerId));
            if (outcome.Applied <= 0)
            {
                return;
            }

            var damageTimestamp = DateTime.UtcNow;
            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerDamaged,
                ["RoomID"] = enemy.RoomId,
                ["DamagedPlayerID"] = playerId,
                ["AttackerID"] = string.Empty,
                ["AttackerType"] = "Enemy",
                ["EnemyId"] = enemy.EnemyId,
                ["Damage"] = outcome.Applied,
                ["RemainingHealth"] = outcome.RemainingHealth,
                ["MaxHealth"] = player.MaxHealth,
                ["IsDown"] = outcome.IsNowDown,
                ["HitPosition"] = new JObject
                {
                    ["X"] = playerState.PositionX,
                    ["Y"] = playerState.PositionY
                },
                ["Timestamp"] = damageTimestamp.ToString("o")
            });

            if (!outcome.IsNowDown)
            {
                return;
            }

            InGameMatchEventHandler.ClearPlayerCombatEffects(enemy.RoomId, playerId);
            room.RecordDeath(playerId);
            var deathTimestamp = DateTime.UtcNow;
            if (deathTimestamp <= damageTimestamp)
                deathTimestamp = damageTimestamp.AddTicks(1);

            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerKilled,
                ["RoomID"] = enemy.RoomId,
                ["KillerID"] = string.Empty,
                ["KilledPlayerID"] = playerId,
                ["DeadPlayerID"] = playerId,
                ["IsSelfInflicted"] = false,
                ["Timestamp"] = deathTimestamp.ToString("o")
            });
        }

                /// <summary>
        /// Publishes the whole set of enemies, for a client that has just arrived
        /// and missed every spawn.
        /// </summary>
        public static void PublishSnapshot(string roomId, string? playerId = null)
        {
            lock (SimulatorLifecycleLock)
            {
                var simulator = TryGet(roomId);
                string snapshotTimestamp;
                IReadOnlyCollection<ServerEnemyState> snapshot;
                if (simulator == null)
                {
                    snapshotTimestamp = DateTime.UtcNow.ToString("o");
                    snapshot = Array.Empty<ServerEnemyState>();
                }
                else
                {
                    // Capture the boundary and states under the simulator lock
                    // so they describe one consistent view of the room.
                    snapshot = simulator.Snapshot(out snapshotTimestamp);
                }
                var enemies = new JArray();
                foreach (var enemy in snapshot)
                {
                    enemies.Add(BuildEnemyMessage("EnemySnapshot", enemy));
                }

                var message = new JObject
                {
                    ["MessageType"] = "EnemySnapshot",
                    ["RoomID"] = roomId,
                    ["Enemies"] = enemies,
                    ["Timestamp"] = snapshotTimestamp
                };
                if (string.IsNullOrWhiteSpace(playerId))
                    GameMessageDispatcher.BroadcastToRoom(roomId, message);
                else
                    GameMessageDispatcher.SendToPlayer(playerId, message);
            }
        }

        private static void BroadcastEnemyState(ServerEnemyState enemy)
        {
            var simulator = TryGet(enemy.RoomId);
            if (simulator == null || !simulator.TryPeek(enemy.EnemyId, out var latest) ||
                !latest.IsAlive || latest.Timestamp.CompareTo(enemy.Timestamp) > 0)
                return;

            var key = enemy.RoomId + "\n" + enemy.EnemyId;
            PendingEnemyStates.AddOrUpdate(key, enemy,
                (_, current) => current.Timestamp.CompareTo(enemy.Timestamp) >= 0
                    ? current
                    : enemy);
        }

        private static void BroadcastEnemySpawn(ServerEnemyState enemy)
        {
            RemovePendingEnemyStateAtOrBefore(enemy);
            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, BuildEnemyMessage(
                "EnemySpawn", enemy));
        }

        private static void BroadcastEnemyDespawn(ServerEnemyState enemy)
        {
            RemovePendingEnemyStateAtOrBefore(enemy);
            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, BuildEnemyMessage(
                "EnemyDespawn", enemy));
        }

        private static void BroadcastEnemyDamaged(ServerEnemyState enemy, string attackerId, int damage)
        {
            RemovePendingEnemyStateAtOrBefore(enemy);
            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, new JObject
            {
                ["MessageType"] = "EnemyDamaged",
                ["RoomID"] = enemy.RoomId,
                ["EnemyId"] = enemy.EnemyId,
                ["EnemyKind"] = enemy.Kind.ToString(),
                ["AttackerId"] = attackerId ?? string.Empty,
                ["Damage"] = damage,
                ["Health"] = enemy.Health,
                ["MaxHealth"] = enemy.MaxHealth,
                ["IsDown"] = !enemy.IsAlive,
                ["PosX"] = enemy.Position.X,
                ["PosY"] = enemy.Position.Y,
                ["FacingX"] = enemy.Facing.X,
                ["FacingY"] = enemy.Facing.Y,
                ["Timestamp"] = string.IsNullOrEmpty(enemy.Timestamp)
                    ? DateTime.UtcNow.ToString("o")
                    : enemy.Timestamp
            });
        }

        private static void RemovePendingEnemyStateAtOrBefore(ServerEnemyState state)
        {
            var key = state.RoomId + "\n" + state.EnemyId;
            if (!PendingEnemyStates.TryGetValue(key, out var pending) ||
                pending.Timestamp.CompareTo(state.Timestamp) > 0)
                return;

            var pendingEntry = new KeyValuePair<string, ServerEnemyState>(key, pending);
            _ = ((ICollection<KeyValuePair<string, ServerEnemyState>>)PendingEnemyStates)
                .Remove(pendingEntry);
        }

        private static JObject BuildEnemyMessage(string messageType, ServerEnemyState enemy)
        {
            return new JObject
            {
                ["MessageType"] = messageType,
                ["RoomID"] = enemy.RoomId,
                ["EnemyId"] = enemy.EnemyId,
                ["EnemyKind"] = enemy.Kind.ToString(),
                ["Health"] = enemy.Health,
                ["MaxHealth"] = enemy.MaxHealth,
                ["PosX"] = enemy.Position.X,
                ["PosY"] = enemy.Position.Y,
                ["FacingX"] = enemy.Facing.X,
                ["FacingY"] = enemy.Facing.Y,
                ["Timestamp"] = string.IsNullOrEmpty(enemy.Timestamp)
                    ? DateTime.UtcNow.ToString("o")
                    : enemy.Timestamp
            };
        }
    }
}
