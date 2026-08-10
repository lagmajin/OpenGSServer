# OpenGS プロジェクト実装完成度・薄さランキング

このドキュメントは、`OpenGSCore`、`OpenGSServer`、`OpenGSR` のソースコード（C#）を静的解析し、実装が薄い（スカスカな）箇所、未実装、TODO放置、`NotImplementedException` などが多いファイルやクラスを抽出してランキング化したものです。

## 📊 スコアリング基準
- **NotImplementedException**: `+35点` / 件 （例外を投げて放置されている重要箇所）
- **TODO / FIXME コメント**: `+15点` / 件
- **日本語未実装表記 (`未実装`, `あとで`, `仮実装`, `ダミー` など)**: `+20点` / 件
- **空のメソッド定義 `{ }`**: 具象クラス `+20点` / 件（実装漏れの可能性大）, 抽象クラス/IF `+5点` / 件
- **極めて少ないLOC (実質コード行数)**: LOC=0は `+60点`, LOC<=5は `+45点`, LOC<=15は `+25点`
- **非推奨 (Deprecated/Obsolete) フォルダ・属性**: スコア `80% 減算`（過去の遺産は除外するため）

---

## 🏆 総合ワーストランキング（実装が薄いクラス TOP 30）

| 順位 | プロジェクト | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 |
| :---: | :--- | :--- | :---: | :---: | :--- |
| 1 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\Functions.cs` | **65** | 5/27 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=5) |
| 2 | `OpenGSServer` | `OpenGSServer\ゴミ\Socket.cs` | **60** | 0/28 | Empty file / only declarations (LOC=0) |
| 3 | `OpenGSR` | `OpenGSR\Assets\Scripts\BaseLib\GameMode.cs` | **60** | 0/40 | Empty file / only declarations (LOC=0) |
| 4 | `OpenGSR` | `OpenGSR\Assets\Scripts\BaseLib\UIEvent.cs` | **60** | 0/16 | Empty file / only declarations (LOC=0) |
| 5 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\NewBehaviourScript.cs` | **60** | 0/22 | Empty file / only declarations (LOC=0) |
| 6 | `OpenGSCore` | `OpenGSCore\Constants\FamousQuotes.cs` | **45** | 5/14 | Extremely thin implementation (LOC=5) |
| 7 | `OpenGSCore` | `OpenGSCore\Event\CTFGameEvent.cs` | **45** | 3/10 | Extremely thin implementation (LOC=3) |
| 8 | `OpenGSCore` | `OpenGSCore\Event\SuvGameEvent.cs` | **45** | 5/13 | Extremely thin implementation (LOC=5) |
| 9 | `OpenGSCore` | `OpenGSCore\Map\ArchloadGunster.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 10 | `OpenGSCore` | `OpenGSCore\Map\Chiristmas.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 11 | `OpenGSCore` | `OpenGSCore\Map\DryDays.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 12 | `OpenGSCore` | `OpenGSCore\Map\Forest.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 13 | `OpenGSCore` | `OpenGSCore\Map\GhostHouse.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 14 | `OpenGSCore` | `OpenGSCore\Map\GreenHill.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 15 | `OpenGSCore` | `OpenGSCore\Map\House.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 16 | `OpenGSCore` | `OpenGSCore\Map\RobotFactory.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 17 | `OpenGSCore` | `OpenGSCore\Map\Ruin.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) |
| 18 | `OpenGSCore` | `OpenGSCore\Match\Result\MatchResultFactory.cs` | **45** | 3/12 | Extremely thin implementation (LOC=3) |
| 19 | `OpenGSCore` | `OpenGSCore\Match\Result\MatchResultService.cs` | **45** | 4/12 | Extremely thin implementation (LOC=4) |
| 20 | `OpenGSCore` | `OpenGSCore\Player\DeadReason.cs` | **45** | 5/16 | Extremely thin implementation (LOC=5) |
| 21 | `OpenGSCore` | `OpenGSCore\Player\PlayerType.cs` | **45** | 5/14 | Extremely thin implementation (LOC=5) |
| 22 | `OpenGSCore` | `OpenGSCore\Score\AbstractFinalScore.cs` | **45** | 5/22 | Extremely thin implementation (LOC=5) |
| 23 | `OpenGSCore` | `OpenGSCore\Time\TimeDefines.cs` | **45** | 4/14 | Extremely thin implementation (LOC=4) |
| 24 | `OpenGSCore` | `OpenGSCore\Utility\GenerateUniqueID.cs` | **45** | 4/23 | Extremely thin implementation (LOC=4) |
| 25 | `OpenGSCore` | `OpenGSCore\Utility\MakeGameObject.cs` | **45** | 4/17 | Extremely thin implementation (LOC=4) |
| 26 | `OpenGSCore` | `OpenGSCore\Utility\TagAttribute.cs` | **45** | 4/14 | Extremely thin implementation (LOC=4) |
| 27 | `OpenGSServer` | `OpenGSServer\Account\AdminJsonSerializerContext.cs` | **45** | 4/11 | Extremely thin implementation (LOC=4) |
| 28 | `OpenGSServer` | `OpenGSServer\Constants\RoomConstants.cs` | **45** | 5/10 | Extremely thin implementation (LOC=5) |
| 29 | `OpenGSServer` | `OpenGSServer\Database\AbstractDatabaseManager.cs` | **45** | 1/15 | Extremely thin implementation (LOC=1) |
| 30 | `OpenGSServer` | `OpenGSServer\Room\MissionRoom.cs` | **45** | 4/24 | Extremely thin implementation (LOC=4) |

---

## 📁 OpenGSCore 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `OpenGSCore\Constants\FamousQuotes.cs` | **45** | 5/14 | Extremely thin implementation (LOC=5) | なし |
| 2 | `OpenGSCore\Event\CTFGameEvent.cs` | **45** | 3/10 | Extremely thin implementation (LOC=3) | なし |
| 3 | `OpenGSCore\Event\SuvGameEvent.cs` | **45** | 5/13 | Extremely thin implementation (LOC=5) | なし |
| 4 | `OpenGSCore\Map\ArchloadGunster.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 5 | `OpenGSCore\Map\Chiristmas.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 6 | `OpenGSCore\Map\DryDays.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 7 | `OpenGSCore\Map\Forest.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 8 | `OpenGSCore\Map\GhostHouse.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 9 | `OpenGSCore\Map\GreenHill.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 10 | `OpenGSCore\Map\House.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 11 | `OpenGSCore\Map\RobotFactory.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 12 | `OpenGSCore\Map\Ruin.cs` | **45** | 2/9 | Extremely thin implementation (LOC=2) | なし |
| 13 | `OpenGSCore\Match\Result\MatchResultFactory.cs` | **45** | 3/12 | Extremely thin implementation (LOC=3) | なし |
| 14 | `OpenGSCore\Match\Result\MatchResultService.cs` | **45** | 4/12 | Extremely thin implementation (LOC=4) | なし |
| 15 | `OpenGSCore\Player\DeadReason.cs` | **45** | 5/16 | Extremely thin implementation (LOC=5) | なし |
| 16 | `OpenGSCore\Player\PlayerType.cs` | **45** | 5/14 | Extremely thin implementation (LOC=5) | なし |
| 17 | `OpenGSCore\Score\AbstractFinalScore.cs` | **45** | 5/22 | Extremely thin implementation (LOC=5) | なし |
| 18 | `OpenGSCore\Time\TimeDefines.cs` | **45** | 4/14 | Extremely thin implementation (LOC=4) | なし |
| 19 | `OpenGSCore\Utility\GenerateUniqueID.cs` | **45** | 4/23 | Extremely thin implementation (LOC=4) | なし |
| 20 | `OpenGSCore\Utility\MakeGameObject.cs` | **45** | 4/17 | Extremely thin implementation (LOC=4) | なし |
| 21 | `OpenGSCore\Utility\TagAttribute.cs` | **45** | 4/14 | Extremely thin implementation (LOC=4) | なし |
| 22 | `OpenGSCore\FlagState.cs` | **25** | 11/33 | Thin implementation (LOC=11) | なし |
| 23 | `OpenGSCore\GameObjectType.cs` | **25** | 9/23 | Thin implementation (LOC=9) | なし |
| 24 | `OpenGSCore\Chat\ChatMacro.cs` | **25** | 6/33 | Thin implementation (LOC=6) | なし |
| 25 | `OpenGSCore\Constants\Tickrate.cs` | **25** | 9/19 | Thin implementation (LOC=9) | なし |
| 26 | `OpenGSCore\Encrypt\Encrypt.cs` | **25** | 8/39 | Thin implementation (LOC=8) | なし |
| 27 | `OpenGSCore\Event\AbstractGameEvent.cs` | **25** | 7/27 | Thin implementation (LOC=7) | なし |
| 28 | `OpenGSCore\Event\BuffExpiredEvent.cs` | **25** | 14/34 | Thin implementation (LOC=14) | なし |
| 29 | `OpenGSCore\Event\DeathMatchEvent.cs` | **25** | 9/20 | Thin implementation (LOC=9) | なし |
| 30 | `OpenGSCore\Event\MatchTimeSyncEvent.cs` | **25** | 11/28 | Thin implementation (LOC=11) | なし |

## 📁 OpenGSServer 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `OpenGSServer\ゴミ\Socket.cs` | **60** | 0/28 | Empty file / only declarations (LOC=0) | なし |
| 2 | `OpenGSServer\Account\AdminJsonSerializerContext.cs` | **45** | 4/11 | Extremely thin implementation (LOC=4) | なし |
| 3 | `OpenGSServer\Constants\RoomConstants.cs` | **45** | 5/10 | Extremely thin implementation (LOC=5) | なし |
| 4 | `OpenGSServer\Database\AbstractDatabaseManager.cs` | **45** | 1/15 | Extremely thin implementation (LOC=1) | なし |
| 5 | `OpenGSServer\Room\MissionRoom.cs` | **45** | 4/24 | Extremely thin implementation (LOC=4) | なし |
| 6 | `OpenGSServer\Server\IPBanList.cs` | **45** | 11/54 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=11) | なし |
| 7 | `OpenGSServer\Server\IServerEventHandler.cs` | **45** | 1/11 | Extremely thin implementation (LOC=1) | なし |
| 8 | `OpenGSServer\Server\IServerHost.cs` | **45** | 2/16 | Extremely thin implementation (LOC=2) | なし |
| 9 | `OpenGSServer\Server\Event\MatchRoomEventHandler.cs` | **45** | 9/42 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=9) | なし |
| 10 | `OpenGSServer\Server\Session\MissionClientSession.cs` | **45** | 1/18 | Extremely thin implementation (LOC=1) | なし |
| 11 | `OpenGSServer\Stage\Stage.cs` | **45** | 1/12 | Extremely thin implementation (LOC=1) | なし |
| 12 | `OpenGSServer\Utility\LiteNetLibReaderExtensions.cs` | **45** | 2/13 | Extremely thin implementation (LOC=2) | なし |
| 13 | `OpenGSServer\Utility\Version.cs` | **45** | 3/20 | Extremely thin implementation (LOC=3) | なし |
| 14 | `OpenGSServer\ゴミ\CityOfDarkness2.cs` | **45** | 1/15 | Extremely thin implementation (LOC=1) | なし |
| 15 | `OpenGSServer\Command\ServerCommand.cs` | **40** | 528/1057 | Unfinished markers x 2 | 未実装 (L794): `ダミーの設定とイベントバスを使用` |
| 16 | `OpenGSServer\DBFriend.cs` | **25** | 11/33 | Thin implementation (LOC=11) | なし |
| 17 | `OpenGSServer\Hash.cs` | **25** | 13/33 | Thin implementation (LOC=13) | なし |
| 18 | `OpenGSServer\Log.cs` | **25** | 7/24 | Thin implementation (LOC=7) | なし |
| 19 | `OpenGSServer\Constants\GlobalConstants.cs` | **25** | 6/11 | Thin implementation (LOC=6) | なし |
| 20 | `OpenGSServer\Core\MakeJson.cs` | **25** | 12/45 | Thin implementation (LOC=12) | なし |
| 21 | `OpenGSServer\Database\MatchDatabaseStruct.cs` | **25** | 12/63 | Thin implementation (LOC=12) | なし |
| 22 | `OpenGSServer\Database\Ranking.cs` | **25** | 15/53 | Thin implementation (LOC=15) | なし |
| 23 | `OpenGSServer\Infrastructure\CoreServerBridge.cs` | **25** | 12/41 | Thin implementation (LOC=12) | なし |
| 24 | `OpenGSServer\Manager\ServerInfoManager.cs` | **25** | 12/28 | Thin implementation (LOC=12) | なし |
| 25 | `OpenGSServer\Match\MatchRoomFactory.cs` | **25** | 8/21 | Thin implementation (LOC=8) | なし |
| 26 | `OpenGSServer\Match\MatchRoomNetwork.cs` | **25** | 13/40 | Thin implementation (LOC=13) | なし |
| 27 | `OpenGSServer\Match\MatchRoomNetworkFunc.cs` | **25** | 11/26 | Thin implementation (LOC=11) | なし |
| 28 | `OpenGSServer\Match\Event\MatchRoomServerEvent.cs` | **25** | 10/45 | Thin implementation (LOC=10) | なし |
| 29 | `OpenGSServer\Network\UDPReceiver.cs` | **25** | 6/31 | Thin implementation (LOC=6) | なし |
| 30 | `OpenGSServer\Platform\Windows\WindowsAPI.cs` | **25** | 13/28 | Thin implementation (LOC=13) | なし |

## 📁 OpenGSR 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `OpenGSR\Assets\Scripts\Core\Functions.cs` | **65** | 5/27 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=5) | なし |
| 2 | `OpenGSR\Assets\Scripts\BaseLib\GameMode.cs` | **60** | 0/40 | Empty file / only declarations (LOC=0) | なし |
| 3 | `OpenGSR\Assets\Scripts\BaseLib\UIEvent.cs` | **60** | 0/16 | Empty file / only declarations (LOC=0) | なし |
| 4 | `OpenGSR\Assets\Scripts\Core\NewBehaviourScript.cs` | **60** | 0/22 | Empty file / only declarations (LOC=0) | なし |
| 5 | `OpenGSR\Assets\Scripts\PlayerWeaponAttachment.cs` | **45** | 3/24 | Extremely thin implementation (LOC=3) | なし |
| 6 | `OpenGSR\Assets\Scripts\BaseLib\Controller2D.cs` | **45** | 3/18 | Extremely thin implementation (LOC=3) | なし |
| 7 | `OpenGSR\Assets\Scripts\BaseLib\DoEvent.cs` | **45** | 1/11 | Extremely thin implementation (LOC=1) | なし |
| 8 | `OpenGSR\Assets\Scripts\BaseLib\EDirection.cs` | **45** | 3/13 | Extremely thin implementation (LOC=3) | なし |
| 9 | `OpenGSR\Assets\Scripts\BaseLib\Initializer.cs` | **45** | 3/22 | Extremely thin implementation (LOC=3) | なし |
| 10 | `OpenGSR\Assets\Scripts\BaseLib\Object.cs` | **45** | 3/23 | Extremely thin implementation (LOC=3) | なし |
| 11 | `OpenGSR\Assets\Scripts\BaseLib\Interface\Scene.cs` | **45** | 11/29 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=11) | なし |
| 12 | `OpenGSR\Assets\Scripts\BaseLib\UI\BattleSceneWallpaper.cs` | **45** | 10/44 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=10) | なし |
| 13 | `OpenGSR\Assets\Scripts\Core\ApplicationSettingManager.cs` | **45** | 1/14 | Extremely thin implementation (LOC=1) | なし |
| 14 | `OpenGSR\Assets\Scripts\Core\AudioSetting.cs` | **45** | 4/19 | Extremely thin implementation (LOC=4) | なし |
| 15 | `OpenGSR\Assets\Scripts\Core\BurstArea.cs` | **45** | 15/40 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=15) | なし |
| 16 | `OpenGSR\Assets\Scripts\Core\CameraAspectRatio.cs` | **45** | 4/22 | Extremely thin implementation (LOC=4) | なし |
| 17 | `OpenGSR\Assets\Scripts\Core\CursorManager.cs` | **45** | 3/21 | Extremely thin implementation (LOC=3) | なし |
| 18 | `OpenGSR\Assets\Scripts\Core\DataStoreManager.cs` | **45** | 5/55 | Extremely thin implementation (LOC=5) | なし |
| 19 | `OpenGSR\Assets\Scripts\Core\Defines.cs` | **45** | 5/18 | Extremely thin implementation (LOC=5) | なし |
| 20 | `OpenGSR\Assets\Scripts\Core\EffectPrefabMasterData.cs` | **45** | 5/16 | Extremely thin implementation (LOC=5) | なし |
| 21 | `OpenGSR\Assets\Scripts\Core\EnemyBotController.cs` | **45** | 3/28 | Extremely thin implementation (LOC=3) | なし |
| 22 | `OpenGSR\Assets\Scripts\Core\ImageExtension.cs` | **45** | 6/26 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=6) | なし |
| 23 | `OpenGSR\Assets\Scripts\Core\Initialize.cs` | **45** | 4/22 | Extremely thin implementation (LOC=4) | なし |
| 24 | `OpenGSR\Assets\Scripts\Core\InstantItemSlotSetting.cs` | **45** | 5/17 | Extremely thin implementation (LOC=5) | なし |
| 25 | `OpenGSR\Assets\Scripts\Core\Interface.cs` | **45** | 1/8 | Extremely thin implementation (LOC=1) | なし |
| 26 | `OpenGSR\Assets\Scripts\Core\MetalBreakerScore.cs` | **45** | 1/16 | Extremely thin implementation (LOC=1) | なし |
| 27 | `OpenGSR\Assets\Scripts\Core\MonoBehaviorExtension.cs` | **45** | 1/14 | Extremely thin implementation (LOC=1) | なし |
| 28 | `OpenGSR\Assets\Scripts\Core\MuzzleLaser.cs` | **45** | 3/16 | Extremely thin implementation (LOC=3) | なし |
| 29 | `OpenGSR\Assets\Scripts\Core\Random.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 30 | `OpenGSR\Assets\Scripts\Core\ResourcePath.cs` | **45** | 1/12 | Extremely thin implementation (LOC=1) | なし |

---

## 🔍 主要な「薄い実装」の詳細とコード解説

ランキング上位から、特に実装不足や放置されたメソッドが目立ち、機能追加の余地が大きい主要なクラスをピックアップして詳細を解説します。

### 🚨 1位：`CharaController.cs` (OpenGSR) — 具象クラスの空メソッド放置
- **現状スコア**: **180** (LOC: 176 / Total: 522)
- **主な理由**: 具象クラスでありながら、空のメソッド定義 `{ }` が 9 箇所も放置されています。
- **薄い実装のコード抜粋**:
  ```csharp
  [Button("ローリングテスト")]
  public new void Rolling()
  {
      // 空っぽ
  }

  void Scope()
  {
      // 空っぽ
  }

  public void FlipWeapon()
  {
      // 空っぽ
  }

  void TakeNewWeapon()
  {
      // 空っぽ
  }
  ```
- **分析と影響**: プレイヤーキャラクターの基本アクション（ローリング、スコープ覗き込み、武器の切り替え、武器の拾得・投棄など）の機能枠だけが宣言され、ロジックが未実装になっています。特に `Rolling` や `Scope` はゲームプレイの根幹に関わる部分であり、最優先での実装が必要です。

### 🚨 2位：`ItemEffect.cs` (OpenGSCore) — アイテム効果ロジックの不在
- **現状スコア**: **145** (LOC: 10 / Total: 51)
- **主な理由**: 実質 LOC がわずか 10 行しかなく、派生クラスがすべて空のメソッドで定義されています。
- **薄い実装 of コード抜粋**:
  ```csharp
  public class PowerUpItemEffect : AbstractItemEffect
  {
      public PowerUpItemEffect() { }

      public override void ApplyItemEffect(PlayerStatus status)
      {
          // 空っぽ
      }

      public override void UnApplyItemEffect(PlayerStatus status)
      {
          // 空っぽ
      }
  }
  ```
- **分析と影響**: 攻撃力アップ (`PowerUp`)、防御力アップ (`DefenceUp`)、グレネードパック (`NormalGranadePack`) のアイテム効果クラスが定義されているものの、プレイヤーへのバフ適用ロジックが全く存在しません。アイテムを拾った際の効果が機能していないことを示しています。

### 🚨 3位：`AIPlayerController.cs` (OpenGSR) — AIキャラクター制御の未実装
- **現状スコア**: **140** (LOC: 61 / Total: 205)
- **主な理由**: 具象クラスにおける空メソッドが 7 箇所。
- **分析と影響**: AI（ボット）キャラクターの移動、攻撃、意思決定などのフレームワークは定義されている可能性がありますが、個別の状態更新ロジックやターゲット追跡処理などが空になっており、AIがその場で静止するか、意図通りに動かない原因となっています。

### 🚨 4位：`ServerInfoDatabaseManager.cs` (OpenGSServer) — データベース管理機能の不足
- **現状スコア**: **85** (LOC: 11 / Total: 45)
- **主な理由**: 具象クラスの空メソッドが 3 箇所、実質 LOC=11 行の非常に薄いクラス。
- **薄い実装のコード抜粋**:
  ```csharp
  public void UpdateDatabase()
  {
      // 空っぽ
  }

  public void ClearDatabase()
  {
      // 空っぽ
  }

  public void RemoveDatabase()
  {
      // 空っぽ
  }
  ```
- **分析と影響**: LiteDB の接続初期化 `Connect()` 自体は行っていますが、DBのアップデート、クリア、削除などの実際のデータ操作処理が空のままです。サーバー情報テーブルのメンテナンスや削除処理が未実装であることを意味します。

---

## 💡 今後の改善に向けた推奨アプローチ

1. **基本アクションの実装 (`CharaController`)**: 
   `Rolling()`, `Scope()`, `FlipWeapon()` など、プレイヤーに紐づくアクションの処理を追加し、入力システムやアニメーションシステムと連動させます。
2. **アイテム効果の具象ロジック実装 (`ItemEffect`)**:
   `PlayerStatus` クラスに対し、バフ値（攻撃力、防御力）の加算・減算処理を追加し、タイマー処理と連動して効果時間が切れたら `UnApply` を呼ぶロジックを追加します。
3. **データベース管理メソッドの実装 (`ServerInfoDatabaseManager`)**:
   LiteDB のコレクション取得・書き込みロジックを追加し、管理用APIからDBの状態を変更できるようにします。

---
*このレポートは `code_analyzer3.py` の静的解析により自動生成され、手動で重要度を評価したものです。*
