# 未使用クラス削除プラン

## ?? 即削除推奨（安全に削除可能）

### Phase 1: 完全未使用ファイル
```bash
# これらのファイルは完全に削除できる
- Game\InstantItem.cs          # すべて空実装
- Game\MakeGameObject.cs       # 未完成、常にnull返す
```

### Phase 2: 部分的削除
```csharp
// GameObject.cs から削除
- class NormalGranade          # AbstractGranadeで代替済み
- interface ISyncable          # どこからも使われていない

// CoreServerBridge.cs から削除
- class TypeAliases            # 完全に空

// Utility\Time.cs から削除
- class Time                   # .NET標準TimeSpanで代替
  ※ class Ping は使われている可能性があるため要確認
```

### Phase 3: 段階的削除（@Obsoleteマーク済み）
```csharp
// 将来的に削除
- Game\GameMode.cs の GameMode クラス
- Game\GameObject.cs の AbstractGameObject（後方互換用）
```

---

## ?? 削除による効果

### コードサイズ削減:
- **InstantItem.cs**: ~50行 → 0行
- **MakeGameObject.cs**: ~45行 → 0行
- **NormalGranade**: ~20行 → 0行
- **ISyncable**: ~7行 → 0行
- **TypeAliases**: ~5行 → 0行
- **Time クラス**: ~10行 → 0行

**合計削減: 約137行**

### 保守性向上:
- ? 混乱を招く未完成コードの削除
- ? 使われていないインターフェースの削除
- ? OpenGSCoreとの統合が明確化

---

## ?? 削除前の確認事項

1. **Pingクラスの使用確認**
```bash
# 検索して使われているか確認
git grep "Ping.CalcPing"
git grep "new Ping"
```

2. **GameModeクラスの参照確認**
```bash
# どこから使われているか確認
git grep "new GameMode"
git grep "GameMode("
```

---

## ?? 削除実行コマンド（案）

```bash
# Phase 1: 安全に削除できるファイル
rm Game\InstantItem.cs
rm Game\MakeGameObject.cs

# Phase 2: Git経由で削除（履歴に残す）
git rm Game\InstantItem.cs
git rm Game\MakeGameObject.cs
git commit -m "Remove unused classes: InstantItem, MakeGameObject"
```

---

## ? 削除後のビルド確認

```bash
dotnet build
# エラーがないことを確認

dotnet test
# すべてのテストが通ることを確認
```
