using System;
using System.Collections.Generic;
using OpenGSCore;

#nullable enable

namespace OpenGSServer.Network
{
    /// <summary>
    /// The outcome of applying damage to a player.
    /// </summary>
    public readonly struct DamageOutcome
    {
        public DamageOutcome(int requested, int applied, int remainingHealth, bool wasAlreadyDown, bool isNowDown)
        {
            Requested = requested;
            Applied = applied;
            RemainingHealth = remainingHealth;
            WasAlreadyDown = wasAlreadyDown;
            IsNowDown = isNowDown;
        }

        /// <summary>Damage the attacker asked for, after the caller's scaling.</summary>
        public int Requested { get; }

        /// <summary>Damage actually taken. Zero when the player was already down.</summary>
        public int Applied { get; }

        public int RemainingHealth { get; }

        /// <summary>True when the player had already reached zero.</summary>
        public bool WasAlreadyDown { get; }

        /// <summary>True when this hit is the one that took the player out.</summary>
        public bool IsNowDown { get; }
    }

    /// <summary>
    /// Server owned damage arithmetic and the health it acts on.
    /// <para>
    /// Damage used to be computed in the message handler and broadcast without
    /// being applied to anything, so nothing tracked a player's health, nothing
    /// could detect a kill, and the match had no way to end. The arithmetic is
    /// here so it can be reasoned about and tested on its own, away from the
    /// transport that delivers it.
    /// </para>
    /// </summary>
    public static class ServerDamageResolver
    {
        /// <summary>
        /// Poses that reduce incoming damage, mirroring what the client shows.
        /// </summary>
        public static float PoseMultiplier(EPlayerPoseState pose)
        {
            return pose switch
            {
                EPlayerPoseState.Sit => 0.5f,
                EPlayerPoseState.LieDown => 0.75f,
                _ => 1f
            };
        }

        /// <summary>
        /// Applies damage to a player, mutating their health.
        /// <para>
        /// Health never goes below zero, a hit on a player who is already down
        /// does nothing, and a hit that lands is never rounded away to nothing:
        /// a player cannot be invulnerable by standing still.
        /// </para>
        /// </summary>
        public static DamageOutcome Apply(PlayerInfo? player, int requested, EPlayerPoseState pose)
        {
            if (player == null)
            {
                return new DamageOutcome(requested, 0, 0, true, false);
            }

            if (requested <= 0)
            {
                return new DamageOutcome(0, 0, player.Health, player.Health <= 0, false);
            }

            var wasDown = player.Health <= 0;
            if (wasDown)
            {
                return new DamageOutcome(requested, 0, 0, true, false);
            }

            var scaled = (int)MathF.Round(requested * PoseMultiplier(pose));
            var applied = Math.Max(1, scaled);
            if (applied > player.Health)
            {
                applied = player.Health;
            }

            player.Health -= applied;
            if (player.Health < 0)
            {
                player.Health = 0;
            }

            return new DamageOutcome(requested, applied, player.Health, false, player.Health <= 0);
        }

        /// <summary>
        /// Restores a player to full health, for a respawn or a match start.
        /// </summary>
        public static void Revive(PlayerInfo? player)
        {
            if (player == null)
            {
                return;
            }

            player.Health = player.MaxHealth > 0 ? player.MaxHealth : 100;
        }

        /// <summary>
        /// True when the player has no health left.
        /// </summary>
        public static bool IsDown(PlayerInfo? player)
        {
            return player == null || player.Health <= 0;
        }

        /// <summary>
        /// Linear falloff used by a blast, so a player near the centre of it takes
        /// the most and a player at the rim takes the least. A blast always does
        /// at least one point of damage, so standing in one is never free.
        /// </summary>
        public static int BlastDamage(int totalDamage, float distance, float radius)
        {
            if (totalDamage <= 0 || radius <= 0f)
            {
                return 0;
            }

            if (distance > radius)
            {
                return 0;
            }

            var falloff = 1f - Math.Clamp(distance / radius, 0f, 1f);
            return Math.Max(1, (int)MathF.Round(totalDamage * falloff));
        }
    }
}