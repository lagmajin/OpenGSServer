# OpenGSCore と OpenGSServer の統合ガイド

## ?? 統合の目的
重複クラスを削除し、OpenGSCoreを共通ライブラリとして活用

## ?? 重複クラスと推奨対応

### 1. AbstractGameObject
**重複:**
- `OpenGSCore\Item\AbstractGameObject.cs` ? (充実)
- `OpenGSServer\Game\GameObject.cs` ? (削除推奨)

**推奨:** OpenGSCoreの実装を使用

**理由:**
- OpenGSCoreの方が実装が充実
- プロパティ変更検知機能
- 同期メカニズム実装済み

**移行手順:**
```csharp
// Before (OpenGSServer)
namespace OpenGSServer
{
    public abstract class AbstractGameObject { }
}

// After (OpenGSCoreを使用)
using OpenGSCore;
// AbstractGameObjectはOpenGSCoreから自動的に利用可能
```

---

### 2. PlayerGameObject
**現状:**
- `OpenGSCore\Player\PlayerCharacter.cs` - Core版
- `OpenGSServer\Game\PlayerGameObject.cs` - Server版（新規作成）

**推奨:** 役割分担

**OpenGSCore版 (PlayerCharacter):**
```csharp
// ゲームロジック用（クライアント/サーバー共通）
public class PlayerCharacter : AbstractGameObject
{
    public PlayerStatus Status { get; set; }
    public Vector2 Position { get; set; }
    // ゲームの基本情報
}
```

**OpenGSServer版 (SimplePlayer):**
```csharp
// サーバー専用（中継のみ）
public sealed class SimplePlayer
{
    public string Id { get; init; }
    public Vector2 Position { get; set; }
    public float Health { get; set; }
    // サーバーが管理する最小限の情報
}
```

---

### 3. Time クラス
**重複:**
- `OpenGSCore\Time\Time.cs` (GSTime)
- `OpenGSServer\Utility\Time.cs` (簡易版)

**推奨:** OpenGSCoreを使用、または削除して.NET標準を使用

**理由:**
- .NET 10では`DateTime`/`DateTimeOffset`/`TimeSpan`が充実
- NodaTimeも利用可能

**移行:**
```csharp
// Before
Time.TotalSec(1, 30, 0);

// After
TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(30)).TotalSeconds;
// または
new TimeSpan(1, 30, 0).TotalSeconds;
```

---

### 4. BulletObject
**現状:**
- OpenGSServerのみに存在（新規作成）

**推奨:** OpenGSServerに残す

**理由:**
- サーバー固有の実装
- OpenGSCoreには不要（Unityクライアントで管理）

---

## ??? 具体的な統合手順

### ステップ1: GameObjectの統合

#### 1-1. OpenGSCoreのAbstractGameObjectを継承
```csharp
// File: Game\BulletObject.cs
using OpenGSCore; // ← OpenGSCoreを使用

namespace OpenGSServer;

// OpenGSCore.AbstractGameObjectを継承
public abstract class AbstractBullet : AbstractGameObject
{
    public float Damage { get; protected set; }
    // ...
    
    public AbstractBullet(float x, float y, float speed, float damage, float angle, float stoppingPower) 
        : base() // ← OpenGSCore.AbstractGameObjectのコンストラクタ
    {
        SetPos(x, y); // ← OpenGSCoreのメソッドを使用
        Speed = speed;
        // ...
    }
}
```

#### 1-2. OpenGSServerのAbstractGameObjectを削除
```xml
<!-- OpenGSServer.csproj -->
<ItemGroup>
  <Compile Remove="Game\GameObject.cs" />
</ItemGroup>
```

---

### ステップ2: 名前空間の整理

#### 2-1. using文を追加
```csharp
// すべての.csファイルの先頭に追加
using OpenGSCore;
```

#### 2-2. 型エイリアスで互換性維持
```csharp
// File: Game\TypeAliases.cs
using OpenGSCore;

namespace OpenGSServer;

// 互換性のためのエイリアス
using CoreAbstractGameObject = OpenGSCore.AbstractGameObject;
using CorePlayerInfo = OpenGSCore.PlayerInfo;
```

---

### ステップ3: GameSceneの更新

#### 3-1. OpenGSCoreのGameSceneを使用
```csharp
// OpenGSCore\GameScene.cs を基に
using OpenGSCore;

namespace OpenGSServer;

public class GameSceneV2
{
    private readonly List<AbstractGameObject> _objects = new();
    
    public bool AddObject(AbstractGameObject obj) // ← OpenGSCoreの型
    {
        _objects.Add(obj);
        obj.OnCreated();
        return true;
    }
}
```

---

## ?? 統合のメリット

| 項目 | Before | After |
|------|--------|-------|
| コード重複 | ? 多数 | ? なし |
| 保守性 | ? 低い（2箇所） | ? 高い（1箇所） |
| 型安全性 | ? 型不一致エラー | ? 統一 |
| パフォーマンス | ? 同じ | ? 同じ |
| 拡張性 | ? 低い | ? 高い |

---

## ?? 注意事項

### 1. 段階的な移行
一度にすべて変更せず、以下の順序で：
1. ? 新規コードはOpenGSCoreを使用
2. ? 既存コードは徐々に移行
3. ? テストして動作確認

### 2. 互換性の維持
古いコードとの互換性が必要な場合：
```csharp
// 型エイリアスで対応
using LegacyGameObject = OpenGSServer.AbstractGameObject;
using NewGameObject = OpenGSCore.AbstractGameObject;
```

### 3. NuGet参照の追加
```xml
<!-- OpenGSServer.csproj -->
<ItemGroup>
  <ProjectReference Include="..\OpenGSCore\OpenGSCore.csproj" />
</ItemGroup>
```

---

## ?? 今すぐできること

### 最小限の変更で統合開始:

1. **BulletObject.csを更新**
```csharp
// using OpenGSServer; を削除
using OpenGSCore;

// AbstractGameObjectはOpenGSCoreから取得
public abstract class AbstractBullet : AbstractGameObject
```

2. **GameScene.csを除外したまま、GameSceneV2.csを使用**
```csharp
// GameSceneV2.csで既にOpenGSCore互換
```

3. **新規実装はすべてOpenGSCore基準で作成**

---

## ?? まとめ

### ? 推奨統合:
- AbstractGameObject → OpenGSCore
- PlayerInfo → OpenGSCore  
- Time → .NET標準 or OpenGSCore

### ? サーバー独自:
- LobbyServerManager
- SimpleMatchRoom
- ネットワーク処理

### ? クライアント独自:
- Unity固有の実装
- レンダリング

統合を進めますか？ ??
