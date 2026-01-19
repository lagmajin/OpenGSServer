using OpenGSCore;
using System.Numerics;

namespace OpenGSServer;

/// <summary>
/// OpenGSCoreとOpenGSServerの型統合ヘルパー
/// 既存コードとの互換性を保ちながら段階的に移行するための橋渡し
/// </summary>
public static class CoreServerBridge
{
    /// <summary>
    /// サーバー側で管理するゲームオブジェクト情報
    /// </summary>
    public record GameObjectServerInfo
    {
        public required string Id { get; init; }
        public Vector2 Position { get; init; }
        public DateTime LastUpdate { get; init; }
    }
}

/// <summary>
/// OpenGSCore.AbstractGameObjectの拡張メソッド
/// サーバー固有の機能を追加
/// </summary>
public static class GameObjectExtensions
{
    /// <summary>
    /// サーバー用の追加情報を取得
    /// </summary>
    public static CoreServerBridge.GameObjectServerInfo GetServerInfo(this OpenGSCore.AbstractGameObject obj)
    {
        return new CoreServerBridge.GameObjectServerInfo
        {
            Id = obj.Id,
            Position = new Vector2(obj.Posx, obj.Posy),
            LastUpdate = DateTime.UtcNow
        };
    }
}

/// <summary>
/// 後方互換性のための型エイリアス
/// 段階的な移行中に使用
/// </summary>
public static class TypeAliases
{
    // 将来的に削除予定
    // 現在は既存コードとの互換性のため
}
