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
        private static readonly ConcurrentDictionary<string, ServerEnemySimulator> Simulators =
            new(StringComparer.OrdinalIgnoreCase);

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
            var simulator = ForRoom(roomId);
            simulator.PlayerIds = () => room.Players
                .Where(player => player != null && room.Playing && player.Health > 0 &&
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
            var simulator = ForRoom(roomId);
            var spawnPoint = simulator.PickSpawnPoint(centre, minimumDistanceFromPlayers: 8f, spread: 4f);
            var wave = simulator.SpawnWave(roomId, spawnPoint, count, spacing: 2f, kind: kind);
            return wave.Count;
        }

        /// <summary>Advances every room's enemies and announces what moved.</summary>
        public static void Update(float deltaSeconds, double nowSeconds, Action<string, ServerEnemyState, string>? onKilled = null)
        {
            foreach (var entry in Simulators)
            {
                entry.Value.Update(deltaSeconds, nowSeconds);
            }
        }

        public static void ClearRoom(string roomId)
        {
            if (Simulators.TryRemove(roomId, out var simulator))
            {
                simulator.Clear();
            }
        }

                /// <summary>
        /// An enemy was destroyed, so the room's mission is one step further on.
        /// </summary>
        private static void OnEnemyKilled(ServerEnemyState enemy, string attackerId)
        {
            var room = MatchRoomManager.Instance.GetRoomById(enemy.RoomId);
            if (room == null)
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
                    killer.Kills++;
                    killer.Score += 100;
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
            if (room == null || !room.TryGetPlayer(playerId, out var player) || player == null)
            {
                return;
            }

            var pose = room.GetPlayerPoseState(playerId);
            var outcome = ServerDamageResolver.Apply(player, damage, pose,
                InGameMatchEventHandler.HasDefenseBuff(enemy.RoomId, playerId));
            if (outcome.Applied <= 0)
            {
                return;
            }

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
                ["HitPosition"] = new JObject { ["X"] = hit.X, ["Y"] = hit.Y },
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });

            if (!outcome.IsNowDown)
            {
                return;
            }

                        room.RecordDeath(playerId);

            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerKilled,
                ["RoomID"] = enemy.RoomId,
                ["KillerID"] = string.Empty,
                ["KilledPlayerID"] = playerId,
                ["DeadPlayerID"] = playerId,
                ["IsSelfInflicted"] = false,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

                /// <summary>
        /// Publishes the whole set of enemies, for a client that has just arrived
        /// and missed every spawn.
        /// </summary>
        public static void PublishSnapshot(string roomId, string? playerId = null)
        {
            var simulator = TryGet(roomId);
            var enemies = new JArray();
            foreach (var enemy in simulator?.Snapshot() ?? Array.Empty<ServerEnemyState>())
            {
                enemies.Add(BuildEnemyMessage("EnemySnapshot", enemy));
            }

            var message = new JObject
            {
                ["MessageType"] = "EnemySnapshot",
                ["RoomID"] = roomId,
                ["Enemies"] = enemies,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
            if (string.IsNullOrWhiteSpace(playerId))
                GameMessageDispatcher.BroadcastToRoom(roomId, message);
            else
                GameMessageDispatcher.SendToPlayer(playerId, message);
        }

        private static void BroadcastEnemyState(ServerEnemyState enemy)
        {
            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, BuildEnemyMessage(
                "EnemyState", enemy));
        }

        private static void BroadcastEnemySpawn(ServerEnemyState enemy)
        {
            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, BuildEnemyMessage(
                "EnemySpawn", enemy));
        }

        private static void BroadcastEnemyDespawn(ServerEnemyState enemy)
        {
            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, BuildEnemyMessage(
                "EnemyDespawn", enemy));
        }

        private static void BroadcastEnemyDamaged(ServerEnemyState enemy, string attackerId, int damage)
        {
            GameMessageDispatcher.BroadcastToRoom(enemy.RoomId, new JObject
            {
                ["MessageType"] = "EnemyDamaged",
                ["RoomID"] = enemy.RoomId,
                ["EnemyId"] = enemy.EnemyId,
                ["AttackerId"] = attackerId ?? string.Empty,
                ["Damage"] = damage,
                ["Health"] = enemy.Health,
                ["MaxHealth"] = enemy.MaxHealth,
                ["IsDown"] = !enemy.IsAlive,
                ["PosX"] = enemy.Position.X,
                ["PosY"] = enemy.Position.Y,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
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
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }
    }
}
