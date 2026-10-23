using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// One player's standing in one mode.
    /// <para>
    /// The key is the account, so a second match adds to the same row rather than
    /// adding another one. That is what makes it a standing rather than a history:
    /// a leaderboard that kept every match would rank players by how long they had
    /// been playing rather than by how well.
    /// </para>
    /// </summary>
    public class RankingEntry
    {
        /// <summary>
        /// The row's key: the mode and the account together.
        /// <para>
        /// LiteDB keys on a property called Id. The key used to be the account
        /// alone, stored in a field the store has no mapping for, so FindById found
        /// nothing and every second match started a fresh standing from zero: the
        /// best score stayed at the last result and the match count never rose
        /// above one.
        /// </para>
        /// </summary>
        public string Id { get; set; } = string.Empty;

        public string AccountId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;

        /// <summary>The best single result, which is what the board ranks on.</summary>
        public int BestScore { get; set; }

        public int TotalScore { get; set; }

        public int Matches { get; set; }

        public DateTime LastPlayedUtc { get; set; } = DateTime.MinValue;
    }

    /// <summary>
    /// The leaderboard store.
    /// <para>
    /// Every method here used to fetch a collection and drop it on the floor, so
    /// the database file was created and nothing was ever written or read from it:
    /// a player's standing survived exactly as long as the process did, which is
    /// to say not at all. A ranking that is not persisted is not a ranking.
    /// </para>
    /// </summary>
    public sealed class RankingDatabase : IDisposable
    {
        private const string CollectionName = "rankings";

        private LiteDatabase? rankingDB;
        private readonly object syncRoot = new();

        public static string rankingDBfileName = "ranking.db";

        /// <summary>
        /// One store for the server. It is opened lazily and kept open, because a
        /// leaderboard is read on every result and reopening a file database for
        /// each one costs more than the write does.
        /// </summary>
        public static RankingDatabase Instance { get; } = new RankingDatabase();

        public void ConnectRankingDB()
        {
            lock (syncRoot)
            {
                if (rankingDB != null)
                {
                    return;
                }

                var directory = Path.GetDirectoryName(Path.GetFullPath(rankingDBfileName));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                rankingDB = new LiteDatabase(rankingDBfileName);
            }
        }

        private ILiteCollection<RankingEntry> Entries()
        {
            ConnectRankingDB();
            return rankingDB!.GetCollection<RankingEntry>(CollectionName);
        }

        /// <summary>
        /// Records a result, keeping the best and adding to the total.
        /// </summary>
        /// <returns>The stored row, or null when there is nothing to record.</returns>
        public RankingEntry? Record(
            string accountId,
            string displayName,
            EGameMode mode,
            int score,
            DateTime? playedUtc = null)
        {
            if (string.IsNullOrWhiteSpace(accountId) || mode == EGameMode.Unknown)
            {
                return null;
            }

            var key = BuildKey(accountId, mode);
            lock (syncRoot)
            {
                var entries = Entries();
                var matching = entries.Find(Query.EQ(nameof(RankingEntry.Mode), mode.ToString()))
                    .Where(entry => string.Equals(
                        entry.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var existing = entries.FindById(key);
                if (existing != null && matching.All(entry => entry.Id != existing.Id))
                {
                    matching.Add(existing);
                }

                var totalScore = matching.Sum(entry => Math.Max(0, entry.TotalScore));
                var matchCount = matching.Sum(entry => Math.Max(0, entry.Matches));
                var bestScore = matching.Select(entry => Math.Max(0, entry.BestScore))
                    .DefaultIfEmpty(0)
                    .Max();
                var lastPlayed = matching.Select(entry => entry.LastPlayedUtc)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();

                existing ??= new RankingEntry
                {
                    Id = key,
                    AccountId = accountId,
                    Mode = mode.ToString()
                };

                foreach (var duplicate in matching.Where(entry => entry.Id != key))
                {
                    entries.Delete(duplicate.Id);
                }

                existing.Id = key;
                existing.AccountId = accountId;
                existing.Mode = mode.ToString();
                existing.DisplayName = string.IsNullOrWhiteSpace(displayName) ? accountId : displayName;
                existing.TotalScore = totalScore + Math.Max(0, score);
                existing.BestScore = Math.Max(bestScore, Math.Max(0, score));
                existing.Matches = matchCount + 1;
                existing.LastPlayedUtc = playedUtc ?? DateTime.UtcNow;
                if (lastPlayed > existing.LastPlayedUtc)
                {
                    existing.LastPlayedUtc = lastPlayed;
                }

                entries.Upsert(existing);
                return existing;
            }
        }

        public void AddDMKillRanking(string accountId, string displayName, int kill)
            => Record(accountId, displayName, EGameMode.DeathMatch, kill);

        public void AddTDMKillRanking(string accountId, string displayName, int kill)
            => Record(accountId, displayName, EGameMode.TeamDeathMatch, kill);

        public void AddSuvKillRanking(string accountId, string displayName, int place)
            => Record(accountId, displayName, EGameMode.Survival, place);

        public void AddTSuvKillRanking(string accountId, string displayName, int score)
            => Record(accountId, displayName, EGameMode.TeamSurvival, score);

        public void AddCTFCaptureRanking(string accountId, string displayName, int captures)
            => Record(accountId, displayName, EGameMode.CaptureTheFlag, captures);

        /// <summary>
        /// The board, best first.
        /// <para>
        /// Sorted in memory rather than by an index because the total has to keep
        /// being read and written by every result; an index over a mutable running
        /// total would go stale the moment the next match landed.
        /// </para>
        /// </summary>
        public IReadOnlyList<RankingEntry> Top(EGameMode mode, int limit = 20)
        {
            if (limit <= 0)
            {
                return Array.Empty<RankingEntry>();
            }

            lock (syncRoot)
            {
                return Entries()
                    .Find(Query.EQ(nameof(RankingEntry.Mode), mode.ToString()))
                    .GroupBy(entry => entry.AccountId, StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var bestEntry = group.OrderByDescending(entry => entry.BestScore)
                            .ThenByDescending(entry => entry.Matches)
                            .First();
                        return new RankingEntry
                        {
                            Id = BuildKey(bestEntry.AccountId, mode),
                            AccountId = bestEntry.AccountId,
                            DisplayName = bestEntry.DisplayName,
                            Mode = mode.ToString(),
                            BestScore = group.Max(entry => entry.BestScore),
                            TotalScore = group.Sum(entry => Math.Max(0, entry.TotalScore)),
                            Matches = group.Sum(entry => Math.Max(0, entry.Matches)),
                            LastPlayedUtc = group.Max(entry => entry.LastPlayedUtc)
                        };
                    })
                    .OrderByDescending(entry => entry.BestScore)
                    .ThenByDescending(entry => entry.Matches)
                    .ThenBy(entry => entry.DisplayName, StringComparer.Ordinal)
                    .Take(limit)
                    .ToList();
            }
        }

        public RankingEntry? Find(string accountId, EGameMode mode)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return null;
            }

            lock (syncRoot)
            {
                var entries = Entries();
                return entries.FindById(BuildKey(accountId, mode)) ??
                    entries.Find(Query.EQ(nameof(RankingEntry.Mode), mode.ToString()))
                        .FirstOrDefault(entry => string.Equals(
                            entry.AccountId, accountId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static string BuildKey(string accountId, EGameMode mode)
        {
            // One row per account per mode, so the key has to carry both.
            return $"{mode}:{accountId.ToUpperInvariant()}";
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                rankingDB?.Dispose();
                rankingDB = null;
            }
        }
    }
}
