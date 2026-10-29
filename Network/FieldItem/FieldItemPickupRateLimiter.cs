using System;
using System.Collections.Concurrent;

namespace OpenGSServer
{
    /// <summary>
    /// Per player budget for field item pickup claims.
    /// <para>
    /// A pickup is the one client message that grants something for free, so it
    /// gets its own allowance rather than sharing the general message budget.
    /// The budget is per player, because one client looping the message must not
    /// throttle everyone else in the room.
    /// </para>
    /// <para>
    /// It lives in one place rather than in either transport because a budget
    /// per transport would be defeated by alternating between the reliable and
    /// the realtime channel to get twice the allowance.
    /// </para>
    /// </summary>
    public sealed class FieldItemPickupRateLimiter
    {
        /// <summary>
        /// How many claims are allowed back to back. Generous enough to sweep a
        /// dense cluster of spawns without being cut off halfway through.
        /// </summary>
        public const double BurstCapacity = 8;

        /// <summary>
        /// The steady rate once the burst is spent.
        /// </summary>
        public const double RatePerSecond = 4;

        /// <summary>
        /// How many refused claims are tolerated before the player is treated as
        /// looping the message rather than having a bad round.
        /// </summary>
        public const int AbuseLimit = 20;

        private readonly ConcurrentDictionary<string, TokenBucket> _buckets =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, int> _refusals =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raised when a player keeps claiming past the abuse limit, so the
        /// caller can act on the connection rather than just logging.
        /// </summary>
        public event Action<string>? OnAbuseLimitReached;

        /// <summary>
        /// Decides whether a claim may proceed.
        /// </summary>
        public bool TryConsume(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            var bucket = _buckets.GetOrAdd(
                playerId,
                _ => new TokenBucket(BurstCapacity, RatePerSecond));

            if (bucket.TryConsume(1))
            {
                // An accepted claim clears the refusal tally, so a burst that is
                // later accommodated does not count against the player.
                _refusals.TryRemove(playerId, out _);
                return true;
            }

            var refusals = _refusals.AddOrUpdate(playerId, 1, (_, current) => current + 1);

            if (refusals >= AbuseLimit)
            {
                // Drop the budget as well, so a player who is let back in starts
                // from a full allowance rather than an empty one.
                _refusals.TryRemove(playerId, out _);
                _buckets.TryRemove(playerId, out _);
                OnAbuseLimitReached?.Invoke(playerId);
            }

            return false;
        }

        /// <summary>
        /// Drops a player's budget, so per player state does not grow without
        /// bound as players come and go.
        /// </summary>
        public void Clear(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            _buckets.TryRemove(playerId, out _);
            _refusals.TryRemove(playerId, out _);
        }

        /// <summary>
        /// How many claims the player currently has left, for logging.
        /// </summary>
        public int RefusalCount(string playerId)
        {
            return _refusals.TryGetValue(playerId ?? string.Empty, out var count) ? count : 0;
        }
    }
}