using LiteDB;
using OpenGSCore;
using System;
using System.Collections.Generic;

namespace OpenGSServer
{
    public class AccountDatabaseManager : IDisposable
    {
        private LiteDatabase db;

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

            return db;
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
            return col.FindOne(Query.EQ(nameof(DBAccount.AccountId), accountID)) != null
                || col.FindOne(Query.EQ(nameof(DBAccount.Id), accountID)) != null;
        }

        public DBAccount? GetDBPlayerInfoOld(in string id)
        {
            var col = db?.GetCollection<DBAccount>(accountTableName);

            DBAccount? data = null;
            if (col != null)
            {
                try
                {
                    data = col.FindOne(Query.EQ(nameof(DBAccount.AccountId), id))
                        ?? col.FindOne(Query.EQ(nameof(DBAccount.Id), id));
                }
                catch (Exception)
                {
                }
            }

            return data;
        }

        public DBAccount? GetAccount(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return null;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);

            return col.FindOne(Query.EQ(nameof(DBAccount.AccountId), accountId))
                ?? col.FindOne(Query.EQ(nameof(DBAccount.Id), accountId));
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
            if (string.IsNullOrWhiteSpace(player.AccountId))
            {
                ConsoleWrite.WriteMessage("[AccountDB] Skip insert: account ID is empty", ConsoleColor.Yellow);
                return;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);
            var scoreCol = database.GetCollection<DBAccountScore>(accountScoreTableName);

            var existing = col.FindOne(Query.EQ(nameof(DBAccount.AccountId), player.AccountId))
                ?? col.FindOne(Query.EQ(nameof(DBAccount.Id), player.Id));

            if (existing != null)
            {
                ConsoleWrite.WriteMessage($"[AccountDB] Already have data in db: {player.AccountId}", ConsoleColor.Yellow);
                return;
            }

            player.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
            col.Insert(player);

            if (scoreCol.FindOne(Query.EQ(nameof(DBAccountScore.accountID), player.AccountId)) == null)
            {
                scoreCol.Insert(new DBAccountScore
                {
                    accountID = player.AccountId
                });
            }
        }

        public void AddNewFriend(in string account, in string friendid)
        {
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(friendid))
            {
                return;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBFriend>(friendTableName);

            foreach (var existing in col.Find(Query.EQ(nameof(DBFriend.PlayerID), account)))
            {
                if (string.Equals(existing.FriendAccountId, friendid, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            col.Insert(new DBFriend
            {
                PlayerID = account,
                FriendAccountId = friendid,
                CreatedTimeUtc = DateTime.UtcNow.ToString("O")
            });
        }

        public void UpdateAccountData(in DBAccount player)
        {
            if (string.IsNullOrWhiteSpace(player.AccountId))
            {
                return;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBAccount>(accountTableName);
            col.EnsureIndex(x => x.AccountId, true);

            var rec = col.FindOne(Query.EQ(nameof(DBAccount.AccountId), player.AccountId));
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
            rec.Status = player.Status;
            rec.LastUpdatedUtc = DateTime.UtcNow.ToString("O");

            col.Update(rec);
        }

        public void UpdateAccountScore(in string accountID, in DBAccountScore score)
        {
            if (string.IsNullOrWhiteSpace(accountID))
            {
                return;
            }

            var database = EnsureDatabase();
            var scoreCol = database.GetCollection<DBAccountScore>(accountScoreTableName);

            var record = scoreCol.FindOne(Query.EQ(nameof(DBAccountScore.accountID), accountID));
            score.accountID = accountID;

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

        public void RemoveAllFriendFromAccount(in string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var database = EnsureDatabase();
            var col = database.GetCollection<DBFriend>(friendTableName);
            col.DeleteMany(Query.EQ(nameof(DBFriend.PlayerID), id));
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

            var rec = col.FindOne(Query.EQ(nameof(DBAccount.AccountId), id));
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

            var rec = col.FindOne(Query.EQ(nameof(DBAccount.AccountId), accountID));
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

            col.DeleteMany(Query.EQ(nameof(DBAccount.AccountId), accountID));
            scoreCol.DeleteMany(Query.EQ(nameof(DBAccountScore.accountID), accountID));
            friendCol.DeleteMany(Query.EQ(nameof(DBFriend.PlayerID), accountID));
            friendCol.DeleteMany(Query.EQ(nameof(DBFriend.FriendAccountId), accountID));
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
