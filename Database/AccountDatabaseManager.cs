using LiteDB;
using OpenGSCore;
using OpenGSServer.Network;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace OpenGSServer
{
    public class AccountDatabaseManager : IDisposable
    {
        private LiteDatabase? db;
        private readonly object _accountMutationLock = new();

        public static AccountDatabaseManager Instance { get; set; } = new();

        public static string tableName = "TaskGroup";
        public static string accountTableName = "Account";
        public static string accountScoreTableName = "AccountScore";
        public static string friendTableName = "Friend";

        public static string filename = "Database/Account.db";
        public static string connectionString = $"Filename={filename};connection=shared";

        public static AccountDatabaseManager GetInstance()
        {
            return Instance;
        }

        private LiteDatabase EnsureDatabase()
        {
            if (db == null)
            {
                Connect();
            }

            return db!;
        }

        private static DBAccount? FindAccountRecord(
            ILiteCollection<DBAccount> collection, string accountId)
        {
            return collection.FindOne(Query.EQ(nameof(DBAccount.AccountId), accountId))
                ?? collection.FindOne(Query.EQ(nameof(DBAccount.Id), accountId))
                ?? collection.FindAll().FirstOrDefault(account =>
                    string.Equals(account.AccountId, accountId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(account.Id, accountId, StringComparison.OrdinalIgnoreCase));
        }

        private static List<DBFriend> FindFriendRecords(
            ILiteCollection<DBFriend> collection, string playerId)
        {
            return collection.FindAll()
                .Where(friend => string.Equals(
                    friend.PlayerID, playerId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void Connect()
        {
            if (db != null)
            {
                return;
            }

            db = new LiteDatabase(connectionString);
        }

        public void Disconnect()
        {
            db?.Dispose();
            db = null;
        }

        public void AddNewPlayerData(in PlayerInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.Id))
            {
                return;
            }

            AddNewPlayerData(new DBAccount
            {
                AccountId = info.Id,
                DisplayName = string.IsNullOrWhiteSpace(info.Name) ? info.Id : info.Name,
                HashedPassword = string.Empty,
                Salt = string.Empty,
                Level = info.Level,
                Exp = info.Exp,
                Status = DBAccount.EAccountStatus.Active,
                LastUpdatedUtc = DateTime.UtcNow.ToString("O")
            });
        }

        public bool Exist(in string accountID)
        {
            if (string.IsNullOrWhiteSpace(accountID))
            {
                return false;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);
            return FindAccountRecord(col, accountID) != null;
        }

        public DBAccount? GetDBPlayerInfoOld(in string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            try
            {
                var col = EnsureDatabase().GetCollection<DBAccount>(accountTableName);
                return FindAccountRecord(col, id);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[AccountDB] Account lookup failed: {ex.Message}", ConsoleColor.Red);
                return null;
            }
        }

        public DBAccount? GetAccount(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return null;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);

            return FindAccountRecord(col, accountId);
        }

        public DBAccount? GetAccountSnapshot(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return null;
            }

            lock (_accountMutationLock)
            {
                var account = GetAccount(accountId);
                return account == null ? null : new DBAccount(account);
            }
        }

        public void GetPlayerInfo(in string account)
        {
            var accountInfo = GetAccount(account);
            if (accountInfo == null)
            {
                ConsoleWrite.WriteMessage($"[AccountDB] Player '{account}' not found", ConsoleColor.Yellow);
                return;
            }

            ConsoleWrite.WriteMessage(
                $"[AccountDB] Player '{accountInfo.AccountId}' DisplayName='{accountInfo.DisplayName}' Level={accountInfo.Level}",
                ConsoleColor.Cyan);
        }

        public int UserCount()
        {
            var database = EnsureDatabase();
            var acCol = database.GetCollection<DBAccount>(accountTableName);
            return acCol.Count();
        }

        public void AddNewPlayerData(in DBAccount player)
        {
            TryAddNewPlayerData(player);
        }

        public bool TryAddNewPlayerData(in DBAccount player)
        {
            if (string.IsNullOrWhiteSpace(player.AccountId))
            {
                ConsoleWrite.WriteMessage("[AccountDB] Skip insert: account ID is empty", ConsoleColor.Yellow);
                return false;
            }

            lock (_accountMutationLock)
            {
                var database = EnsureDatabase();
                var col = database.GetCollection<DBAccount>(accountTableName);
                var scoreCol = database.GetCollection<DBAccountScore>(accountScoreTableName);

                if (FindAccountRecord(col, player.AccountId) != null)
                {
                    ConsoleWrite.WriteMessage($"[AccountDB] Already have data in db: {player.AccountId}", ConsoleColor.Yellow);
                    return false;
                }

                player.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
                player.Credits = Math.Max(0, player.Credits);
                col.Insert(player);

                if (scoreCol.FindOne(Query.EQ(nameof(DBAccountScore.accountID), player.AccountId)) == null)
                {
                    scoreCol.Insert(new DBAccountScore
                    {
                        accountID = player.AccountId
                    });
                }

                return true;
            }
        }

        public void AddNewFriend(in string account, in string friendid)
        {
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(friendid))
            {
                return;
            }

            lock (_accountMutationLock)
            {
                var database = EnsureDatabase();
                var accounts = database.GetCollection<DBAccount>(accountTableName);
                var ownerId = FindAccountRecord(accounts, account)?.AccountId ?? account;
                var targetId = FindAccountRecord(accounts, friendid)?.AccountId ?? friendid;
                var col = database.GetCollection<DBFriend>(friendTableName);
                if (FindFriendRecords(col, ownerId).Any(existing => string.Equals(
                    existing.FriendAccountId, targetId, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                col.Insert(new DBFriend
                {
                    PlayerID = ownerId,
                    FriendAccountId = targetId,
                    CreatedTimeUtc = DateTime.UtcNow.ToString("O")
                });
            }
        }

        public void UpdateAccountData(in DBAccount player)
        {
            if (string.IsNullOrWhiteSpace(player.AccountId))
            {
                return;
            }

            lock (_accountMutationLock)
            {
                var database = EnsureDatabase();
                var col = database.GetCollection<DBAccount>(accountTableName);
                col.EnsureIndex(x => x.AccountId, true);

                var rec = FindAccountRecord(col, player.AccountId);
                if (rec == null)
                {
                    ConsoleWrite.WriteMessage("[AccountDB] Account not found", ConsoleColor.Yellow);
                    return;
                }

                rec.DisplayName = player.DisplayName;
                rec.Password = player.Password;
                rec.HashedPassword = player.HashedPassword;
                rec.Salt = player.Salt;
                rec.Level = player.Level;
                rec.Exp = player.Exp;
                rec.LifeTimeScore = player.LifeTimeScore ?? new PlayerLifeTimeScore();
                rec.Status = player.Status;
                rec.LastUpdatedUtc = DateTime.UtcNow.ToString("O");

                col.Update(rec);
            }
        }

        public MatchResultResponse? SettleMatchProgress(
            string matchId, string accountId, Func<DBAccount, MatchResultResponse> updateProgress)
        {
            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(accountId) || updateProgress == null)
                return null;

            lock (_accountMutationLock)
            {
                var database = EnsureDatabase();
                var accounts = database.GetCollection<DBAccount>(accountTableName);
                var account = FindAccountRecord(accounts, accountId);
                if (account == null) return null;

                var receipts = database.GetCollection<DBMatchSettlementReceipt>("MatchSettlementReceipt");
                var receiptId = $"{account.AccountId}:{matchId}";
                var existingReceipt = receipts.FindById(receiptId);
                if (existingReceipt != null) return existingReceipt.ToResponse();

                if (!database.BeginTrans())
                    throw new InvalidOperationException("Could not begin match settlement transaction.");

                try
                {
                    account = FindAccountRecord(accounts, account.AccountId);
                    if (account == null)
                    {
                        database.Rollback();
                        return null;
                    }

                    existingReceipt = receipts.FindById(receiptId);
                    if (existingReceipt != null)
                    {
                        if (!database.Commit())
                            throw new InvalidOperationException("Could not close match settlement transaction.");
                        return existingReceipt.ToResponse();
                    }

                    var response = updateProgress(account);
                    if (response == null)
                    {
                        database.Rollback();
                        return null;
                    }

                    account.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
                    if (!accounts.Update(account))
                    {
                        database.Rollback();
                        return null;
                    }
                    receipts.Insert(new DBMatchSettlementReceipt
                    {
                        Id = receiptId,
                        AccountId = account.AccountId,
                        MatchId = matchId,
                        PlayerId = response.PlayerId,
                        XpGained = response.XpGained,
                        NewTotalXp = response.NewTotalXp,
                        NewLevel = response.NewLevel,
                        LevelXp = response.LevelXp,
                        IsLevelUp = response.IsLevelUp,
                        CreatedUtc = DateTime.UtcNow.ToString("O")
                    });

                    if (!database.Commit())
                        throw new InvalidOperationException("Could not commit match settlement transaction.");
                    return response;
                }
                catch
                {
                    database.Rollback();
                    throw;
                }
            }
        }

        private sealed class DBMatchSettlementReceipt
        {
            [BsonId] public string Id { get; set; } = string.Empty;
            public string AccountId { get; set; } = string.Empty;
            public string MatchId { get; set; } = string.Empty;
            public string PlayerId { get; set; } = string.Empty;
            public int XpGained { get; set; }
            public int NewTotalXp { get; set; }
            public int NewLevel { get; set; }
            public int LevelXp { get; set; }
            public bool IsLevelUp { get; set; }
            public string CreatedUtc { get; set; } = string.Empty;

            public MatchResultResponse ToResponse() => new()
            {
                PlayerId = PlayerId,
                XpGained = XpGained,
                NewTotalXp = NewTotalXp,
                NewLevel = NewLevel,
                LevelXp = LevelXp,
                IsLevelUp = IsLevelUp
            };
        }

        public void UpdateAccountScore(in string accountID, in DBAccountScore score)
        {
            if (string.IsNullOrWhiteSpace(accountID))
            {
                return;
            }

            var database = EnsureDatabase();
            var accounts = database.GetCollection<DBAccount>(accountTableName);
            var canonicalId = FindAccountRecord(accounts, accountID)?.AccountId ?? accountID;
            var scoreCol = database.GetCollection<DBAccountScore>(accountScoreTableName);

            var record = scoreCol.FindOne(Query.EQ(nameof(DBAccountScore.accountID), canonicalId))
                ?? scoreCol.FindAll().FirstOrDefault(existing => string.Equals(
                    existing.accountID, canonicalId, StringComparison.OrdinalIgnoreCase));
            score.accountID = canonicalId;

            if (record == null)
            {
                scoreCol.Insert(score);
            }
            else
            {
                scoreCol.Update(score);
            }
        }

        public void UpdateFriendList()
        {
            var database = EnsureDatabase();
            var col = database.GetCollection<DBFriend>(friendTableName);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<DBFriend>();

            foreach (var friend in col.FindAll())
            {
                var key = $"{friend.PlayerID}:{friend.FriendAccountId}";
                if (!seen.Add(key))
                {
                    duplicates.Add(friend);
                }
            }

            foreach (var duplicate in duplicates)
            {
                col.Delete(duplicate.Id);
            }
        }

        public long GetCredits(string accountId)
        {
            var account = GetAccount(accountId);
            return account?.Credits ?? 0;
        }

        public (bool Success, long Granted, long Credits) TryClaimDailyReward(
            string accountId, string claimId, long amount)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(claimId) || amount < 0)
                return (false, 0, 0);

            lock (_accountMutationLock)
            {
                var account = GetAccount(accountId);
                if (account == null) return (false, 0, 0);
                account.ClaimedDailyRewards ??= new List<string>();
                var current = Math.Max(0L, account.Credits);
                if (account.ClaimedDailyRewards.Contains(claimId, StringComparer.OrdinalIgnoreCase))
                    return (true, 0, current);

                var next = (long)Math.Min(
                    (decimal)long.MaxValue, (decimal)current + amount);
                account.Credits = next;
                account.ClaimedDailyRewards.Add(claimId);
                // Daily progress is retained for sixty days, so this bounded
                // receipt window still covers every claim that can be retried.
                if (account.ClaimedDailyRewards.Count > 512)
                    account.ClaimedDailyRewards.RemoveRange(
                        0, account.ClaimedDailyRewards.Count - 512);
                account.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
                var updated = EnsureDatabase().GetCollection<DBAccount>(accountTableName).Update(account);
                return updated ? (true, next - current, next) : (false, 0, current);
            }
        }

        public (bool Success, string Error, long Credits, bool AlreadyOwned) TryPurchaseItem(
            string accountId, string itemId, long price)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(itemId) || price < 0)
                return (false, "Account or item is invalid", 0, false);

            lock (_accountMutationLock)
            {
                var account = GetAccount(accountId);
                if (account == null) return (false, "Account not found", 0, false);
                account.PurchasedItems ??= new List<string>();
                var current = Math.Max(0L, account.Credits);
                if (account.PurchasedItems.Any(existing =>
                    string.Equals(existing, itemId, StringComparison.OrdinalIgnoreCase)))
                    return (true, string.Empty, current, true);
                if (current < price)
                    return (false, "Insufficient credits", current, false);

                account.Credits = current - price;
                account.PurchasedItems.Add(itemId);
                account.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
                var updated = EnsureDatabase().GetCollection<DBAccount>(accountTableName).Update(account);
                return updated
                    ? (true, string.Empty, account.Credits, false)
                    : (false, "Failed to complete purchase", current, false);
            }
        }

        public IReadOnlyList<string> GetPurchasedItems(string accountId)
        {
            var account = GetAccount(accountId);
            return account?.PurchasedItems ?? new List<string>();
        }

        public bool SetPurchasedItem(string accountId, string itemId, bool purchased)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            lock (_accountMutationLock)
            {
                var account = GetAccount(accountId);
                if (account == null)
                {
                    return false;
                }

                account.PurchasedItems ??= new List<string>();
                if (purchased)
                {
                    if (!account.PurchasedItems.Any(existing => string.Equals(existing, itemId, StringComparison.OrdinalIgnoreCase)))
                    {
                        account.PurchasedItems.Add(itemId);
                    }
                }
                else
                {
                    account.PurchasedItems.RemoveAll(existing => string.Equals(existing, itemId, StringComparison.OrdinalIgnoreCase));
                }

                account.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
                return EnsureDatabase().GetCollection<DBAccount>(accountTableName).Update(account);
            }
        }

        public bool IsPurchased(string accountId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            return GetPurchasedItems(accountId).Any(existing => string.Equals(existing, itemId, StringComparison.OrdinalIgnoreCase));
        }

        public string GetEquippedItem(string accountId, string category)
        {
            var account = GetAccount(accountId);
            if (account == null)
            {
                return string.Empty;
            }

            return account.EquippedItems?
                .FirstOrDefault(item => string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase))
                ?.ItemId ?? string.Empty;
        }

        public IReadOnlyList<DBEquippedItem> GetEquippedItems(string accountId)
        {
            var account = GetAccountSnapshot(accountId);
            if (account == null)
            {
                return Array.Empty<DBEquippedItem>();
            }

            var purchasedItems = new HashSet<string>(
                account.PurchasedItems ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            return (account.EquippedItems ?? new List<DBEquippedItem>())
                .Where(item => item != null &&
                    !string.IsNullOrWhiteSpace(item.Category) &&
                    !string.IsNullOrWhiteSpace(item.ItemId) &&
                    purchasedItems.Contains(item.ItemId))
                .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                .Select(group => new DBEquippedItem(group.First()))
                .ToList();
        }

        public IReadOnlyList<DBInstantEquippedItem> GetEquippedInstantItems(string accountId)
        {
            var account = GetAccountSnapshot(accountId);
            if (account == null)
            {
                return Array.Empty<DBInstantEquippedItem>();
            }

            var purchasedItems = new HashSet<string>(
                account.PurchasedItems ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            return (account.EquippedInstantItems ?? new List<DBInstantEquippedItem>())
                .Where(item => item != null && item.Slot >= 0 && item.Slot < 3 &&
                    !string.IsNullOrWhiteSpace(item.ItemId) && purchasedItems.Contains(item.ItemId))
                .GroupBy(item => item.Slot)
                .Select(group => new DBInstantEquippedItem(group.First()))
                .OrderBy(item => item.Slot)
                .ToList();
        }

        public string GetEquippedInstantItem(string accountId, int slot)
        {
            var account = GetAccount(accountId);
            if (account == null)
            {
                return string.Empty;
            }

            return account.EquippedInstantItems?
                .FirstOrDefault(item => item.Slot == slot)
                ?.ItemId ?? string.Empty;
        }

        public bool SetEquippedItem(string accountId, string category, string itemId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            lock (_accountMutationLock)
            {
                var account = GetAccount(accountId);
                if (account == null)
                {
                    return false;
                }

                var normalizedItemId = itemId?.Trim() ?? string.Empty;
                account.PurchasedItems ??= new List<string>();
                if (normalizedItemId.Length > 0 && !account.PurchasedItems.Any(existing =>
                    string.Equals(existing, normalizedItemId, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                account.EquippedItems ??= new List<DBEquippedItem>();
                account.EquippedItems.RemoveAll(item => item == null ||
                    string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase));
                if (normalizedItemId.Length > 0)
                {
                    account.EquippedItems.Add(new DBEquippedItem
                    {
                        Category = category,
                        ItemId = normalizedItemId
                    });
                }

                account.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
                return EnsureDatabase().GetCollection<DBAccount>(accountTableName).Update(account);
            }
        }

        public bool SetEquippedInstantItem(string accountId, int slot, string itemId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || slot < 0 || slot >= 3)
            {
                return false;
            }

            lock (_accountMutationLock)
            {
                var account = GetAccount(accountId);
                if (account == null)
                {
                    return false;
                }

                var normalizedItemId = itemId?.Trim() ?? string.Empty;
                account.PurchasedItems ??= new List<string>();
                if (normalizedItemId.Length > 0 && !account.PurchasedItems.Any(existing =>
                    string.Equals(existing, normalizedItemId, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                account.EquippedInstantItems ??= new List<DBInstantEquippedItem>();
                account.EquippedInstantItems.RemoveAll(item => item == null || item.Slot == slot);
                if (normalizedItemId.Length > 0)
                {
                    account.EquippedInstantItems.Add(new DBInstantEquippedItem
                    {
                        Slot = slot,
                        ItemId = normalizedItemId
                    });
                }

                account.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
                return EnsureDatabase().GetCollection<DBAccount>(accountTableName).Update(account);
            }
        }

        public bool RemoveFriend(string accountId, string friendId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(friendId))
            {
                return false;
            }

            lock (_accountMutationLock)
            {
                var col = EnsureDatabase().GetCollection<DBFriend>(friendTableName);
                var removed = false;
                foreach (var friendship in FindFriendRecords(col, accountId)
                    .Where(friend => string.Equals(
                        friend.FriendAccountId, friendId, StringComparison.OrdinalIgnoreCase)))
                {
                    removed |= col.Delete(friendship.Id);
                }

                foreach (var friendship in FindFriendRecords(col, friendId)
                    .Where(friend => string.Equals(
                        friend.FriendAccountId, accountId, StringComparison.OrdinalIgnoreCase)))
                {
                    removed |= col.Delete(friendship.Id);
                }

                return removed;
            }
        }

        public bool AddFriend(string accountId, string friendId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(friendId) || string.Equals(accountId, friendId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            lock (_accountMutationLock)
            {
                var database = EnsureDatabase();
                var accounts = database.GetCollection<DBAccount>(accountTableName);
                var owner = FindAccountRecord(accounts, accountId);
                var friend = FindAccountRecord(accounts, friendId);
                if (owner == null || friend == null) return false;

                accountId = owner.AccountId;
                friendId = friend.AccountId;
                var col = database.GetCollection<DBFriend>(friendTableName);

                var existing = FindFriendRecords(col, accountId)
                    .Any(record => string.Equals(
                        record.FriendAccountId, friendId, StringComparison.OrdinalIgnoreCase));
                if (!existing)
                {
                    col.Insert(new DBFriend
                    {
                        PlayerID = accountId,
                        FriendAccountId = friendId,
                        CreatedTimeUtc = DateTime.UtcNow.ToString("O")
                    });
                }

                var reverseExisting = FindFriendRecords(col, friendId)
                    .Any(record => string.Equals(
                        record.FriendAccountId, accountId, StringComparison.OrdinalIgnoreCase));
                if (!reverseExisting)
                {
                    col.Insert(new DBFriend
                    {
                        PlayerID = friendId,
                        FriendAccountId = accountId,
                        CreatedTimeUtc = DateTime.UtcNow.ToString("O")
                    });
                }

                return true;
            }
        }

        public List<string> GetFriendIds(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<string>();
            }

            var database = EnsureDatabase();
            var accounts = database.GetCollection<DBAccount>(accountTableName);
            var ownerId = FindAccountRecord(accounts, accountId)?.AccountId ?? accountId;
            var col = database.GetCollection<DBFriend>(friendTableName);
            return FindFriendRecords(col, ownerId)
                .Select(friendship => FindAccountRecord(accounts, friendship.FriendAccountId)?.AccountId
                    ?? friendship.FriendAccountId)
                .Where(friendId => !string.IsNullOrWhiteSpace(friendId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<DBAccountDetail> GetFriendDetails(string accountId)
        {
            var result = new List<DBAccountDetail>();
            foreach (var friendId in GetFriendIds(accountId))
            {
                var account = GetAccount(friendId);
                if (account == null)
                {
                    continue;
                }

                result.Add(new DBAccountDetail
                {
                    AccountId = account.AccountId ?? account.Id,
                    DisplayName = account.DisplayName ?? string.Empty,
                    Level = account.Level,
                    Exp = account.Exp,
                    TotalKill = 0,
                    Death = 0
                });
            }

            return result;
        }

        public DBAccount? GetAccountWithInventory(string accountId)
        {
            return GetAccountSnapshot(accountId);
        }

        public void RemoveAllFriendFromAccount(in string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var database = EnsureDatabase();
            var accounts = database.GetCollection<DBAccount>(accountTableName);
            var ownerId = FindAccountRecord(accounts, id)?.AccountId ?? id;
            var col = database.GetCollection<DBFriend>(friendTableName);
            foreach (var friendship in FindFriendRecords(col, ownerId))
            {
                col.Delete(friendship.Id);
            }
        }

        public void ChangePassword(in string id, in string currentPass, in string newPass)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newPass))
            {
                return;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);
            col.EnsureIndex(player => player.Id, true);

            var rec = FindAccountRecord(col, id);
            if (rec == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(currentPass))
            {
                var withSalt = OpenGSCore.Hash.CreateHashWithSalt(currentPass, rec.Salt ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(rec.HashedPassword) && rec.HashedPassword != withSalt)
                {
                    ConsoleWrite.WriteMessage("[AccountDB] Current password mismatch", ConsoleColor.Yellow);
                    return;
                }
            }

            var newSalt = OpenGSCore.Hash.CreateSalt(8);
            rec.Salt = newSalt;
            rec.HashedPassword = OpenGSCore.Hash.CreateHashWithSalt(newPass, newSalt);
            rec.Password = newPass;
            rec.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
            col.Update(rec);
        }

        public void RemoveAccount(in string accountID, in string password)
        {
            if (string.IsNullOrWhiteSpace(accountID))
            {
                return;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);
            var scoreCol = database.GetCollection<DBAccountScore>(accountScoreTableName);
            var friendCol = database.GetCollection<DBFriend>(friendTableName);

            col.EnsureIndex(player => player.Id, true);

            var rec = FindAccountRecord(col, accountID);
            if (rec == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                var hashed = OpenGSCore.Hash.CreateHashWithSalt(password, rec.Salt ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(rec.HashedPassword) && rec.HashedPassword != hashed)
                {
                    ConsoleWrite.WriteMessage("[AccountDB] Password mismatch on account removal", ConsoleColor.Yellow);
                    return;
                }
            }

            col.Delete(rec.Id);
            scoreCol.DeleteMany(Query.EQ(nameof(DBAccountScore.accountID), rec.AccountId));
            foreach (var friendship in friendCol.FindAll().Where(friend =>
                string.Equals(friend.PlayerID, rec.AccountId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(friend.FriendAccountId, rec.AccountId, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                friendCol.Delete(friendship.Id);
            }
        }

        public void RemoveAll()
        {
            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);
            var scoreCol = database.GetCollection<DBAccountScore>(accountScoreTableName);
            var friendCol = database.GetCollection<DBFriend>(friendTableName);

            col.DeleteAll();
            scoreCol.DeleteAll();
            friendCol.DeleteAll();
        }

        public void FindUser()
        {
            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);

            var count = 0;
            foreach (var account in col.FindAll())
            {
                count++;
                ConsoleWrite.WriteMessage($"[AccountDB] {account.AccountId} / {account.DisplayName}", ConsoleColor.DarkCyan);
            }

            ConsoleWrite.WriteMessage($"[AccountDB] Total users: {count}", ConsoleColor.Cyan);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            db?.Dispose();
            db = null;
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
