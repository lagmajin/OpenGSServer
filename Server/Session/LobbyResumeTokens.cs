using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace OpenGSServer
{
    /// <summary>
    /// Lets a client that has logged in once reconnect without sending its password
    /// again.
    /// <para>
    /// A client that opens the lobby connection has no way to authenticate: the
    /// password belongs to the login screen and is deliberately not kept. Sending
    /// the player id alone would let anyone claim any account, so the reconnect
    /// carries a token the server issued and holds the only copy of.
    /// </para>
    /// <para>
    /// This is the same shape the realtime channel already uses for its udp
    /// token, so there is one idea in the server rather than two.
    /// </para>
    /// </summary>
    internal sealed class LobbyResumeTokens
    {
        /// <summary>
        /// Long enough that a reconnect after a crash or a scene change still
        /// works, short enough that a token left in a log or a crash dump stops
        /// being useful.
        /// </summary>
        private const int LifetimeMinutes = 30;

        private const int MaxOutstandingTokens = 4096;

        private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

        /// <summary>
        /// Issues a token for an account, replacing any the account already had.
        /// </summary>
        public string Issue(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return string.Empty;
            }

            Prune();

            // One outstanding token per account: a second login must not leave the
            // first one usable, or a token copied before the second login still
            // works afterwards.
            Revoke(accountId);

            var token = NewToken();
            _entries[token] = new Entry(accountId, DateTime.UtcNow.AddMinutes(LifetimeMinutes));
            return token;
        }

        /// <summary>
        /// Whether a token identifies a live account, spending it in the process.
        /// <para>
        /// A token is spent by using it: the caller issues a fresh one for the
        /// response. A token seen twice on the wire is therefore only accepted
        /// once, and a copy taken off the wire is good for a single connection.
        /// </para>
        /// </summary>
        public bool TryRedeem(string token, out string accountId)
        {
            accountId = string.Empty;

            if (string.IsNullOrWhiteSpace(token) || !_entries.TryRemove(token, out var entry))
            {
                return false;
            }

            if (entry.ExpiresAtUtc <= DateTime.UtcNow)
            {
                return false;
            }

            accountId = entry.AccountId;
            return true;
        }

        /// <summary>
        /// Looks up a token's account without spending the token. Callers must
        /// redeem it later while holding that account's session gate.
        /// </summary>
        public bool TryGetAccountId(string token, out string accountId)
        {
            accountId = string.Empty;
            if (string.IsNullOrWhiteSpace(token) || !_entries.TryGetValue(token, out var entry))
            {
                return false;
            }

            if (entry.ExpiresAtUtc <= DateTime.UtcNow)
            {
                ((ICollection<KeyValuePair<string, Entry>>)_entries).Remove(
                    new KeyValuePair<string, Entry>(token, entry));
                return false;
            }

            accountId = entry.AccountId;
            return true;
        }

        /// <summary>
        /// Drops every token an account holds. Used on logout so that a client
        /// that logged out cannot come back on the token it was given.
        /// </summary>
        public void Revoke(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            foreach (var pair in _entries)
            {
                if (string.Equals(pair.Value.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
                {
                    _entries.TryRemove(pair.Key, out _);
                }
            }
        }

        private void Prune()
        {
            if (_entries.Count < MaxOutstandingTokens)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var pair in _entries)
            {
                if (pair.Value.ExpiresAtUtc <= now)
                {
                    _entries.TryRemove(pair.Key, out _);
                }
            }
        }

        private static string NewToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes);
        }

        private sealed record Entry(string AccountId, DateTime ExpiresAtUtc);
    }
}
