using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenGSServer;

/// <summary>
/// サーバーメンテナンス用の管理者アカウント
/// パスワードはPBKDF2でハッシュ化して保存
/// </summary>
public sealed class ServerAdminAccount
{
    private const int SaltSize = 16; // 128bit
    private const int HashSize = 32; // 256bit
    private const int Iterations = 10_000;

    public string Id { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Salt { get; init; } = string.Empty;

    private ServerAdminAccount() { }

    /// <summary>
    /// 新しい管理者アカウントを作成（パスワードをハッシュ化）
    /// </summary>
    public static ServerAdminAccount Create(string id, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required", nameof(id));
        if (string.IsNullOrWhiteSpace(plainPassword)) throw new ArgumentException("Password is required", nameof(plainPassword));

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var salt = Convert.ToBase64String(saltBytes);
        var hash = HashPassword(plainPassword, saltBytes);

        return new ServerAdminAccount
        {
            Id = id,
            PasswordHash = Convert.ToBase64String(hash),
            Salt = salt
        };
    }

    /// <summary>
    /// パスワード検証
    /// </summary>
    public bool VerifyPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword)) return false;
        if (string.IsNullOrEmpty(Salt) || string.IsNullOrEmpty(PasswordHash)) return false;

        var saltBytes = Convert.FromBase64String(Salt);
        var expectedHash = Convert.FromBase64String(PasswordHash);
        var inputHash = HashPassword(plainPassword, saltBytes);

        return CryptographicOperations.FixedTimeEquals(expectedHash, inputHash);
    }

    /// <summary>
    /// ファイルへ保存（JSON）
    /// </summary>
    public void SaveToFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required", nameof(path));

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    /// <summary>
    /// ファイルから読み込み（JSON）
    /// </summary>
    public static ServerAdminAccount LoadFromFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Admin account file not found", path);

        var json = File.ReadAllText(path, Encoding.UTF8);
        var account = JsonSerializer.Deserialize<ServerAdminAccount>(json);
        if (account == null) throw new InvalidDataException("Invalid admin account file");
        return account;
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(HashSize);
    }
}
