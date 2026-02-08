using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenGSCore;


namespace OpenGSServer
{
    enum EDealWithDuplicateLogin
    {
        KICK_FIRST_USER=0,
        KICK_SECOND_USER,
        KICK_BOTH_USER

    }

    public class PlayerAccountData
    {
        public PlayerAccount PlayerAccount { get; set; } 
        public PlayerServerInformation PlayerServerInformation { get; set; } = new();

        public PlayerAccountData(PlayerAccount account, PlayerServerInformation information)
        {
            PlayerAccount = account ?? throw new ArgumentNullException(nameof(account));
            PlayerServerInformation = information ?? throw new ArgumentNullException(nameof(information));
        }
    }

    internal interface IAccountManager
    {

    }

    public sealed class AccountManager : IAccountManager
    {
        /// <summary>
        /// スレッドセーフなログイン中のプレイヤーアカウント管理
        /// </summary>
        private readonly ConcurrentDictionary<string, PlayerAccount> _logonUsers = new();

        /// <summary>
        /// スレッドセーフなプレイヤーサーバー情報管理
        /// </summary>
        private readonly ConcurrentDictionary<string, PlayerServerInformation> _playerInfo = new();

        /// <summary>
        /// プレイヤーアカウントデータの統合管理（将来用）
        /// </summary>
        private readonly List<PlayerAccountData> _logonUserData = new();

        private static readonly Lazy<AccountManager> _singleInstance = 
            new(() => new AccountManager());

        private const int DefaultSaltCount = 8;

        /// <summary>
        /// シングルトンインスタンスを取得
        /// </summary>
        public static AccountManager GetInstance()
        {
            return _singleInstance.Value;
        }

        public AccountManager()
        {
        }

        /// <summary>
        /// 新しくログインしたユーザーを追加（DBAccountから）
        /// </summary>
        /// <param name="db">データベース上のアカウント情報</param>
        /// <returns>追加成功時true</returns>
        public bool AddNewLogonUser(in DBAccount db)
        {
            // PlayerAccountとPlayerServerInformationを一括作成
            var account = new PlayerAccount(db.AccountId, db.DisplayName, db.Password);
            var info = new PlayerServerInformation(EPlayerPlayingStatus.Unknown, EPlayerLocation.Lobby);

            // ConcurrentDictionaryを使用することで、ロックなしにスレッドセーフに追加
            bool accountAdded = _logonUsers.TryAdd(db.AccountId, account);
            
            if (!accountAdded)
            {
                // ユーザーが既にログイン中
                return false;
            }

            // プレイヤー情報も追加
            bool infoAdded = _playerInfo.TryAdd(db.AccountId, info);
            
            if (!infoAdded)
            {
                // 情報追加に失敗した場合、ロールバック
                _logonUsers.TryRemove(db.AccountId, out _);
                return false;
            }

            return true;
        }

        /// <summary>
        /// ログインしたユーザーを追加（PlayerAccountDataから）
        /// </summary>
        public bool AddNewLogonUser(in PlayerAccountData data)
        {
            if (data?.PlayerAccount == null || data.PlayerServerInformation == null)
            {
                return false;
            }

            var accountId = data.PlayerAccount.Id;
            
            bool accountAdded = _logonUsers.TryAdd(accountId, data.PlayerAccount);
            if (!accountAdded)
            {
                return false;
            }

            bool infoAdded = _playerInfo.TryAdd(accountId, data.PlayerServerInformation);
            if (!infoAdded)
            {
                _logonUsers.TryRemove(accountId, out _);
                return false;
            }

            return true;
        }

        /// <summary>
        /// ログインしているユーザーを削除
        /// </summary>
        /// <param name="db">削除するアカウント情報</param>
        /// <returns>削除成功時true</returns>
        public bool RemoveLogonUser(in DBAccount db)
        {
            bool userRemoved = _logonUsers.TryRemove(db.AccountId, out _);
            bool infoRemoved = _playerInfo.TryRemove(db.AccountId, out _);
            
            return userRemoved || infoRemoved;
        }

        /// <summary>
        /// プレイヤーのサーバー情報を取得
        /// </summary>
        /// <param name="id">プレイヤーID</param>
        /// <returns>プレイヤー情報、存在しない場合はnull</returns>
        public PlayerServerInformation? GetPlayerInformation(in string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // ユーザーがログイン中か確認
            if (!_logonUsers.ContainsKey(id))
            {
                return null;
            }

            // プレイヤー情報を取得
            _playerInfo.TryGetValue(id, out var info);
            return info;
        }

        /// <summary>
        /// 非推奨: GetPlayerInformationを使用してください
        /// </summary>
        [Obsolete("GetPlayerInformation(string id)を使用してください")]
        public PlayerServerInformation PlayerInformation(in string id)
        {
            return GetPlayerInformation(id);
        }

        /// <summary>
        /// アカウントIDが既にログイン中か確認
        /// </summary>
        public bool ExistID(in string id)
        {
            return !string.IsNullOrEmpty(id) && _logonUsers.ContainsKey(id);
        }

        /// <summary>
        /// 新しいアカウントを作成
        /// </summary>
        public CreateNewAccountResult CreateNewAccount(in string accountID, in string pass, in string displayName)
        {
            var databaseManager = AccountDatabaseManager.GetInstance();
            var createdTimeUTC = DateTime.UtcNow.ToString("O"); // ISO 8601形式
            var salt = OpenGSCore.Hash.CreateSalt(DefaultSaltCount);
            var hashedPass = OpenGSCore.Hash.CreateHashWithSalt(pass, salt);

            var dbAccount = new DBAccount
            {
                AccountId = accountID,
                DisplayName = displayName,
                HashedPassword = hashedPass,
                Salt = salt,
                CreatedTimeUtc = createdTimeUTC
            };

            try
            {
                databaseManager.AddNewPlayerData(dbAccount);
                return new CreateNewAccountResult(eCreateAccountResult.Succeeful);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[AccountManager] アカウント作成失敗: {ex.Message}", ConsoleColor.Red);
                return new CreateNewAccountResult(eCreateAccountResult.Unknown);
            }
        }

        /// <summary>
        /// アカウントを削除
        /// </summary>
        public void RemoveAccount(in string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            _logonUsers.TryRemove(id, out _);
            _playerInfo.TryRemove(id, out _);
        }

        /// <summary>
        /// ログイン処理
        /// </summary>
        public LoginResult Login(in string id, in string pass)
        {
            var databaseManager = AccountDatabaseManager.GetInstance();
            var account = databaseManager.GetDBPlayerInfoOld(id);
            var type = eLoginResultType.Unknown;

            if (account == null)
            {
                type = eLoginResultType.AccountNotFound;
                return new LoginResult(id, type);
            }

            // パスワードハッシュ検証
            var withSalt = OpenGSCore.Hash.CreateHashWithSalt(pass, account.Salt);

            if (account.HashedPassword != withSalt)
            {
                type = eLoginResultType.InvalidIDorPassword;
                return new LoginResult(id, type);
            }

            // ログイン成功 - ユーザーを追加
            if (AddNewLogonUser(account))
            {
                type = eLoginResultType.LoginSucceeded;
            }
            else
            {
                // 既にログイン中の場合
                type = eLoginResultType.Unknown;
            }

            return new LoginResult(id, type);
        }

        /// <summary>
        /// ログアウト処理
        /// </summary>
        public bool Logout(string id, bool force = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return _logonUsers.TryRemove(id, out _);
        }

        /// <summary>
        /// ログアウト処理（パスワード検証付き）
        /// </summary>
        public JObject LogoutWithPassword(string id, string pass)
        {
            var result = new JObject();

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pass))
            {
                result["Success"] = false;
                result["Message"] = "Invalid input";
                return result;
            }

            var databaseManager = AccountDatabaseManager.GetInstance();
            var account = databaseManager.GetDBPlayerInfoOld(id);

            if (account == null)
            {
                result["Success"] = false;
                result["Message"] = "Account not found";
                return result;
            }

            var withSalt = OpenGSCore.Hash.CreateHashWithSalt(pass, account.Salt);
            if (account.HashedPassword != withSalt)
            {
                result["Success"] = false;
                result["Message"] = "Invalid password";
                return result;
            }

            bool logoutSuccess = Logout(id, false);
            result["Success"] = logoutSuccess;
            result["Message"] = logoutSuccess ? "Logout succeeded" : "User not logged in";

            return result;
        }

        /// <summary>
        /// ログイン中のユーザー数を取得
        /// </summary>
        public int GetLoggedInUserCount()
        {
            return _logonUsers.Count;
        }

        /// <summary>
        /// ログイン中のすべてのユーザーを取得
        /// </summary>
        public IEnumerable<string> GetLoggedInUserIds()
        {
            return _logonUsers.Keys;
        }

        /// <summary>
        /// 特定のプレイヤーアカウントを取得
        /// </summary>
        public PlayerAccount? GetPlayerAccount(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            _logonUsers.TryGetValue(id, out var account);
            return account;
        }

    }

}
