using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace OpenGSServer;

/// <summary>
/// 管理者アカウントのシンプルな永続化マネージャ。
/// - 小規模な趣味プロジェクト向けの最小限の機能のみ提供
/// - JSONファイルに全アカウントを保存/読み込み
/// - スレッドセーフ（内部ロック）
/// </summary>
public sealed class AdminManager
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ServerAdminAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; private set; }

    public AdminManager(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>
    /// 現在登録されている管理者ID一覧（コピー）
    /// </summary>
    public List<string> ListAdminIds()
    {
        lock (_lock)
        {
            return _accounts.Keys.ToList();
        }
    }

    /// <summary>
    /// 指定ファイルから読み込み（存在しない場合は空で返す）
    /// </summary>
    public void Load()
    {
        lock (_lock)
        {
            _accounts.Clear();

            if (!File.Exists(FilePath)) return;

            var json = File.ReadAllText(FilePath);
            try
            {
                var list = JsonSerializer.Deserialize<List<ServerAdminAccount>>(json);
                if (list == null) return;

                foreach (var a in list)
                {
                    if (string.IsNullOrWhiteSpace(a.Id)) continue;
                    _accounts[a.Id] = a;
                }
            }
            catch (Exception)
            {
                // 読み込み失敗は無視して空状態にする（ログは呼び出し元で望むなら追加可能）
            }
        }
    }

    /// <summary>
    /// 現在のアカウント一覧をファイルに保存（原子的に書き込む）
    /// </summary>
    public void Save()
    {
        lock (_lock)
        {
            var list = _accounts.Values.ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });

            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            // 上書きは原子的に
            File.Copy(tmp, FilePath, overwrite: true);
            File.Delete(tmp);
        }
    }

    /// <summary>
    /// 管理者を追加。既に存在するIDがあればfalseを返す。
    /// </summary>
    public bool AddAdmin(string id, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id required", nameof(id));
        if (string.IsNullOrWhiteSpace(plainPassword)) throw new ArgumentException("password required", nameof(plainPassword));

        lock (_lock)
        {
            if (_accounts.ContainsKey(id)) return false;
            var account = ServerAdminAccount.Create(id, plainPassword);
            _accounts[id] = account;
            return true;
        }
    }

    /// <summary>
    /// 管理者の削除。存在すればtrue。
    /// </summary>
    public bool RemoveAdmin(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_lock)
        {
            return _accounts.Remove(id);
        }
    }

    /// <summary>
    /// 指定IDのパスワードを検証
    /// </summary>
    public bool VerifyAdmin(string id, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(plainPassword)) return false;
        lock (_lock)
        {
            if (!_accounts.TryGetValue(id, out var account)) return false;
            return account.VerifyPassword(plainPassword);
        }
    }

    /// <summary>
    /// パスワード変更。成功すればtrue。
    /// </summary>
    public bool ChangePassword(string id, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newPassword)) return false;
        lock (_lock)
        {
            if (!_accounts.TryGetValue(id, out var account)) return false;

            // Create a new account instance (ServerAdminAccount is immutable-ish)
            if (!account.VerifyPassword(currentPassword)) return false;

            var updated = ServerAdminAccount.Create(id, newPassword);
            _accounts[id] = updated;
            return true;
        }
    }

    /// <summary>
    /// 管理用ファイルの既定パスを使用するユーティリティ
    /// </summary>
    public static AdminManager CreateDefault()
    {
        var path = Path.Combine(UnityPaths.PersistentDataPath, "admin_accounts.json");
        return new AdminManager(path);
    }
}
