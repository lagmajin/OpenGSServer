using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public sealed class DailyDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int Target { get; init; }
        public long RewardCredits { get; init; }
    }

    public sealed class DailyProgressRecord
    {
        [BsonId] public string Id { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string DailyId { get; set; } = string.Empty;
        public string ResetDateUtc { get; set; } = string.Empty;
        public int Progress { get; set; }
        public bool Claimed { get; set; }
    }

    public sealed class DailyService : IDisposable
    {
        private static readonly IReadOnlyList<DailyDefinition> Definitions = new[]
        {
            new DailyDefinition { Id = "play_match", Description = "Play 1 match", Target = 1, RewardCredits = 100 },
            new DailyDefinition { Id = "win_match", Description = "Win 1 match", Target = 1, RewardCredits = 250 },
            new DailyDefinition { Id = "deal_damage", Description = "Deal 1000 damage", Target = 1000, RewardCredits = 150 }
        };

        private readonly object _sync = new();
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<DailyProgressRecord> _progress;
        private DateTime _lastCleanupDate = DateTime.MinValue;

        public DailyService(string path = "Database/daily.db")
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            _database = new LiteDatabase($"Filename={path};connection=shared");
            _progress = _database.GetCollection<DailyProgressRecord>("daily_progress");
            _progress.EnsureIndex(x => x.PlayerId);
            _progress.EnsureIndex(x => x.ResetDateUtc);
        }

        public IReadOnlyList<DailyDefinition> GetDefinitions() => Definitions;

        public JArray GetPlayerDailies(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return new JArray();
            lock (_sync)
            {
                var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var today = DateTime.UtcNow.Date;
                if (_lastCleanupDate != today)
                {
                    CleanupExpiredRecords(today.AddDays(-60));
                    _lastCleanupDate = today;
                }
                return new JArray(Definitions.Select(definition => ToJson(definition, GetOrCreate(playerId, definition.Id, date))));
            }
        }

        public bool AddProgress(string playerId, string dailyId, int amount)
        {
            if (string.IsNullOrWhiteSpace(playerId) || amount <= 0 || Definitions.All(x => x.Id != dailyId)) return false;
            lock (_sync)
            {
                var record = GetOrCreate(playerId, dailyId, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                var target = Definitions.First(x => x.Id == dailyId).Target;
                record.Progress = Math.Min(target, record.Progress + amount);
                _progress.Upsert(record);
                return true;
            }
        }

        public (bool Success, string Error, long Reward) Claim(string playerId, string dailyId)
        {
            lock (_sync)
            {
                var definition = Definitions.FirstOrDefault(x => x.Id == dailyId);
                var record = definition == null ? null : GetOrCreate(playerId, dailyId, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                if (definition == null || record == null) return (false, "Daily not found", 0);
                if (record.Claimed) return (false, "Daily reward already claimed", 0);
                if (record.Progress < definition.Target) return (false, "Daily is not complete", 0);

                var database = AccountDatabaseManager.GetInstance();
                if (database.GetAccount(playerId) == null) return (false, "Account not found", 0);
                if (!database.UpdateCredits(playerId, database.GetCredits(playerId) + definition.RewardCredits)) return (false, "Failed to grant reward", 0);
                record.Claimed = true;
                _progress.Upsert(record);
                return (true, string.Empty, definition.RewardCredits);
            }
        }

        private DailyProgressRecord GetOrCreate(string playerId, string dailyId, string date)
        {
            var id = $"{playerId}:{date}:{dailyId}";
            var existing = _progress.FindById(id);
            if (existing != null) return existing;
            var created = new DailyProgressRecord { Id = id, PlayerId = playerId, DailyId = dailyId, ResetDateUtc = date };
            _progress.Insert(created);
            return created;
        }

        private void CleanupExpiredRecords(DateTime cutoff)
        {
            var expiredIds = _progress.FindAll()
                .Where(record => DateTime.TryParse(record.ResetDateUtc, out var date) && date < cutoff)
                .Select(record => record.Id)
                .ToList();
            foreach (var id in expiredIds) _progress.Delete(id);
        }

        private static JObject ToJson(DailyDefinition definition, DailyProgressRecord record) => new()
        {
            ["DailyId"] = definition.Id,
            ["Description"] = definition.Description,
            ["Target"] = definition.Target,
            ["Progress"] = record.Progress,
            ["Completed"] = record.Progress >= definition.Target,
            ["Claimed"] = record.Claimed,
            ["RewardCredits"] = definition.RewardCredits,
            ["ResetDateUtc"] = record.ResetDateUtc
        };

        public void Dispose() => _database.Dispose();
    }
}
