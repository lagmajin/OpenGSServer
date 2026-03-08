# OpenGSServer 実装済みシステム

このディレクトリには、OpenGSServerに実装された拡張システムが含まれています。

## ファイル一覧

### ラグ補償システム
| ファイルパス | 説明 |
|-------------|------|
| `Network/LagCompensation/ServerNetworkTransform.cs` | サーバー側トランスフォーム状態 |
| `Network/LagCompensation/ServerPlayerStateManager.cs` | プレイヤー状態管理・予測 |
| `Network/LagCompensation/ServerStateBroadcaster.cs` | クライアントへの状態送信 |
| `Network/LagCompensation/ServerLagCompensationManager.cs` | サーバー側ラグ補償管理 |
| `Network/LagCompensation/PositionSyncEvents.cs` | 位置同期イベント |
| `Network/LagCompensation/ServerPositionSyncManager.cs` | 位置同期管理 |

### レベルシステム
| ファイルパス | 説明 |
|-------------|------|
| `Network/LevelSystem/MatchLevelCalculator.cs` | マッチ結果XP計算 |

### フィールドアイテム同期
| ファイルパス | 説明 |
|-------------|------|
| `Network/FieldItem/ServerFieldItemManager.cs` | サーバー側アイテム管理 |
| `Network/FieldItem/FieldItemEventHandler.cs` | アイテムイベント処理 |

---

## ラグ補償システム使用方法

### セットアップ
```csharp
// マネージャー作成
var serverLagComp = new ServerLagCompensationManager();

// マッチ開始
serverLagComp.StartMatch(matchId);

// プレイヤー追加
serverLagComp.AddPlayer(playerId);
```

### 使用
```csharp
// クライアント入力処理
serverLagComp.ProcessClientInput(input);

// 位置検証（チート対策）
bool isValid = serverLagComp.ValidatePlayerPosition(playerId, x, y, z);

// 更新（毎フレーム）
serverLagComp.Update(deltaTime);

// マッチ終了
serverLagComp.EndMatch();
```

---

## レベル計算使用方法

```csharp
// マッチ結果からXPを計算
int xpGained = MatchLevelCalculator.CalculateMatchXp(
    matchScore,  // マッチスコア
    kills,       // キル数
    deaths,      // デス数
    isVictory    // 勝利かどうか
);

// レベルを計算
int newLevel = MatchLevelCalculator.CalculateLevel(totalXp);

// レスポンスを作成
var response = MatchLevelCalculator.CreateMatchResultResponse(
    playerId,
    matchScore,
    kills,
    deaths,
    isVictory,
    currentTotalXp
);
```

### XP計算式
```
獲得XP = (スコア + キル×10) - (デス×2) + 勝利ボーナス(50) + アシストbonus
```

### レベルテーブル
| レベル | 必要XP |
|-------|--------|
| Lv1 → Lv2 | 100 |
| Lv2 → Lv3 | 200 |
| Lv3 → Lv4 | 350 |
| Lv4 → Lv5 | 550 |
| ... | ... |
| Lv20 (Max) | - |

---

## フィールドアイテム使用方法

```csharp
// アイテムマネージャー作成
var itemManager = new ServerFieldItemManager();
itemManager.StartMatch(matchId);

// アイテムを出現
string itemId = itemManager.SpawnItem("PowerUp", x, y, z);

// アイテム取得を処理
itemManager.PickupItem(itemId, playerId);

// 全アイテムをJSONで取得
var itemsJson = itemManager.ToJson();

// マッチ終了
itemManager.EndMatch();
```

### アイテムタイプ
- PowerUp
- DefenceUp
- Stealth
- SpeedUp
- NormalGrenadePack
- Random
- RocketLauncher
- FlameThrower

---

作成日: 2026-03-08
