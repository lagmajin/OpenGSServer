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

## ✅ 実装済み

この一覧は、すでに消し込み済みとして扱える主な項目です。ランキング本体は薄さの残りを追うための参考としてそのまま残しています。

- `OpenGSCore`
  - ~~`OpenGSCore\Item\ItemEffect.cs`~~
  - ~~`OpenGSCore\Search\SearchTag.cs`~~
  - ~~`OpenGSCore\GameObjectType.cs`~~
  - ~~`OpenGSCore\Chat\OneOnOneChat.cs`~~
  - ~~`OpenGSCore\Constants\FamousQuotes.cs`~~
  - ~~`OpenGSCore\Event\DeathMatchEvent.cs`~~
  - ~~`OpenGSCore\Event\MetalBreakerEvent.cs`~~
  - ~~`OpenGSCore\Event\MissionGameEvent.cs`~~
  - ~~`OpenGSCore\Event\SuvGameEvent.cs`~~
  - ~~`OpenGSCore\Guild\Guild.cs`~~
  - ~~`OpenGSCore\Loading\LoadingFailReason.cs`~~
  - ~~`OpenGSCore\Map\AbstractStage.cs`~~
  - ~~`OpenGSCore\Map\ArchloadGunster.cs`~~
  - ~~`OpenGSCore\Map\Chiristmas.cs`~~
  - ~~`OpenGSCore\Map\DryDays.cs`~~
  - ~~`OpenGSCore\Map\Forest.cs`~~
  - ~~`OpenGSCore\Map\GhostHouse.cs`~~
  - ~~`OpenGSCore\Map\GreenHill.cs`~~
  - ~~`OpenGSCore\Map\House.cs`~~
  - ~~`OpenGSCore\Map\RobotFactory.cs`~~
  - ~~`OpenGSCore\Map\Ruin.cs`~~
  - ~~`OpenGSCore\Item\InstantItemEffect.cs`~~
  - ~~`OpenGSCore\Match\Setting\SuvMatchSetting.cs`~~
  - ~~`OpenGSCore\Match\Setting\Team\CaptureTheFlagMatchSetting.cs`~~
  - ~~`OpenGSCore\Match\Setting\Team\TDMMatchSetting.cs`~~
  - ~~`OpenGSCore\Match\Setting\Team\TeamSurvival.cs`~~
- `OpenGSServer`
  - ~~`OpenGSServer\Server\Event\ManagementServerEvent.cs`~~
  - ~~`OpenGSServer\Database\ServerInfoDatabaseManager.cs`~~
  - ~~`OpenGSServer\Room\WaitRoomEvent.cs`~~
  - ~~`OpenGSServer\Server\Event\PlayerEventHandler.cs`~~
  - ~~`OpenGSServer\Server\RUDP\MatchRUDPClientSession.cs`~~
  - ~~`OpenGSServer\Lobby\MissionLobby.cs`~~
  - ~~`OpenGSServer\Manager\PlayerScoreManager.cs`~~
  - ~~`OpenGSServer\Manager\ServerSettingManager.cs`~~
  - ~~`OpenGSServer\Match\MatchRoomNetwork.cs`~~
  - ~~`OpenGSServer\Match\MatchRoomNetworkFunc.cs`~~
  - ~~`OpenGSServer\Platform\Windows\WindowsAPI.cs`~~
  - ~~`OpenGSServer\Player\WaitRoomPlayerInfo.cs`~~
  - ~~`OpenGSServer\Result\CreateNewMissionRoom.cs`~~
  - ~~`OpenGSServer\Result\UserInfoResult.cs`~~
  - ~~`OpenGSServer\Result\WaitRoomInfo.cs`~~
  - ~~`OpenGSServer\Room\MissionRoom.cs`~~
  - ~~`OpenGSServer\Server\MissionServer.cs`~~
  - ~~`OpenGSServer\Settings\DefaultServerSetting.cs`~~
- `OpenGSR`
  - ~~`OpenGSR\Assets\Scripts\Core\Base\EventManager.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\Base\MissionManager.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\Base\OpenGSBaseClass.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\BurstArea.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\CustomRenderer.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\ExpEffect.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\FireEffect.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\StageList.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Match\GodModeMainScript.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Match\MatchEventProvider.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Match\TSUVMainScript.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Player\AsmExport\PlayerDataLinker.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Resource\StageList.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Scene\WaitRoom\OnlineWaitRoomSceneEvent.cs`~~
  - ~~`OpenGSR\Assets\Scripts\UI\CommonCanvas.cs`~~
  - ~~`OpenGSR\Assets\Scripts\UI\LoadingSceneCanvas.cs`~~

---

## 🏆 総合ワーストランキング（実装が薄いクラス TOP 30）

| 順位 | プロジェクト | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 |
| :---: | :--- | :--- | :---: | :---: | :--- |
| 1 | `OpenGSR` | `OpenGSR\Assets\Scripts\Player\CharaController.cs` | **200** | 176/522 | Empty methods x 10 (Abstract: False) |
| 2 | `OpenGSCore` | `OpenGSCore\Item\ItemEffect.cs` | **145** | 10/51 | Empty methods x 6 (Abstract: False), Thin implementation (LOC=10) |
| 3 | `OpenGSR` | `OpenGSR\Assets\Scripts\Player\AIPlayerController.cs` | **140** | 61/205 | Empty methods x 7 (Abstract: False) |
| 4 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\TitleScene.cs` | **140** | 54/143 | Empty methods x 7 (Abstract: False) |
| 5 | `OpenGSR` | `OpenGSR\Assets\Scripts\Match\MatchRule.cs` | **120** | 113/275 | Empty methods x 6 (Abstract: False) |
| 6 | `OpenGSR` | `OpenGSR\Assets\Scripts\Mission\SkyFighterMainScript.cs` | **120** | 18/87 | Empty methods x 6 (Abstract: False) |
| 7 | `OpenGSServer` | `OpenGSServer\Server\Event\ManagementServerEvent.cs` | **105** | 4/27 | Empty methods x 3 (Abstract: False), Extremely thin implementation (LOC=4) |
| 8 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\StageList.cs` | **105** | 12/50 | Empty methods x 4 (Abstract: False), Thin implementation (LOC=12) |
| 9 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\MatchNetworkManagerMainScript.cs` | **105** | 7/34 | Empty methods x 4 (Abstract: False), Thin implementation (LOC=7) |
| 10 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\CustomRenderer.cs` | **100** | 0/24 | Empty methods x 2 (Abstract: False), Empty file / only declarations (LOC=0) |
| 11 | `OpenGSR` | `OpenGSR\Assets\Scripts\Player\AsmExport\AbstractPlayer.cs` | **90** | 276/608 | Empty methods x 18 (Abstract: True) |
| 12 | `OpenGSServer` | `OpenGSServer\Database\ServerInfoDatabaseManager.cs` | **85** | 11/45 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=11) |
| 13 | `OpenGSServer` | `OpenGSServer\Room\WaitRoomEvent.cs` | **85** | 3/25 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=3) |
| 14 | `OpenGSServer` | `OpenGSServer\Server\Event\PlayerEventHandler.cs` | **85** | 3/23 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=3) |
| 15 | `OpenGSServer` | `OpenGSServer\Server\RUDP\MatchRUDPClientSession.cs` | **85** | 15/50 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=15) |
| 16 | `OpenGSR` | `OpenGSR\Assets\Scripts\BaseLib\Audio.cs` | **85** | 3/14 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=3) |
| 17 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\FireEffect.cs` | **85** | 9/43 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=9) |
| 18 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\Rigidbody2DExtension.cs` | **85** | 8/34 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=8) |
| 19 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\Base\EventManager.cs` | **85** | 13/58 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=13) |
| 20 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\Base\MissionManager.cs` | **85** | 5/29 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=5) |
| 21 | `OpenGSR` | `OpenGSR\Assets\Scripts\Match\GodModeMainScript.cs` | **85** | 4/25 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=4) |
| 22 | `OpenGSR` | `OpenGSR\Assets\Scripts\Match\TSUVMainScript.cs` | **85** | 14/68 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=14) |
| 23 | `OpenGSR` | `OpenGSR\Assets\Scripts\Player\AsmExport\PlayerDataLinker.cs` | **85** | 4/26 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=4) |
| 24 | `OpenGSR` | `OpenGSR\Assets\Scripts\Resource\StageList.cs` | **85** | 11/34 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=11) |
| 25 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\WaitRoom\OnlineWaitRoomSceneEvent.cs` | **85** | 12/52 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=12) |
| 26 | `OpenGSR` | `OpenGSR\Assets\Scripts\UI\CommonCanvas.cs` | **85** | 4/17 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=4) |
| 27 | `OpenGSR` | `OpenGSR\Assets\Scripts\UI\LoadingSceneCanvas.cs` | **85** | 4/16 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=4) |
| 28 | `OpenGSServer` | `OpenGSServer\Database\AccountDatabaseManager.cs` | **80** | 121/396 | Empty methods x 4 (Abstract: False) |
| 29 | `OpenGSR` | `OpenGSR\Assets\Scripts\Player\PlayerAgent.cs` | **80** | 406/934 | Empty methods x 4 (Abstract: False) |
| 30 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\ExportAssets\ExportAssetScene.cs` | **80** | 203/409 | Unfinished markers x 3, Empty methods x 1 (Abstract: False) |

---

## 📁 OpenGSCore 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `OpenGSCore\Item\ItemEffect.cs` | **145** | 10/51 | Empty methods x 6 (Abstract: False), Thin implementation (LOC=10) | なし |
| 2 | `OpenGSCore\Search\SearchTag.cs` | **65** | 3/17 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3) | なし |
| 3 | `OpenGSCore\FlagState.cs` | **45** | 4/15 | Extremely thin implementation (LOC=4) | なし |
| 4 | `OpenGSCore\GameObjectType.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 5 | `OpenGSCore\Chat\OneOnOneChat.cs` | **45** | 4/14 | Extremely thin implementation (LOC=4) | なし |
| 6 | `OpenGSCore\Constants\FamousQuotes.cs` | **45** | 3/13 | Extremely thin implementation (LOC=3) | なし |
| 7 | `OpenGSCore\Event\DeathMatchEvent.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 8 | `OpenGSCore\Event\MetalBreakerEvent.cs` | **45** | 1/14 | Extremely thin implementation (LOC=1) | なし |
| 9 | `OpenGSCore\Event\MissionGameEvent.cs` | **45** | 2/20 | Extremely thin implementation (LOC=2) | なし |
| 10 | `OpenGSCore\Event\SuvGameEvent.cs` | **45** | 3/22 | Extremely thin implementation (LOC=3) | なし |
| 11 | `OpenGSCore\Extention\DictionaryExtension.cs` | **45** | 2/13 | Extremely thin implementation (LOC=2) | なし |
| 12 | `OpenGSCore\Guild\Guild.cs` | **45** | 4/22 | Extremely thin implementation (LOC=4) | なし |
| 13 | `OpenGSCore\Item\AbstractItemEffect.cs` | **45** | 4/15 | Extremely thin implementation (LOC=4) | なし |
| 14 | `OpenGSCore\Item\InstantItemEffect.cs` | **45** | 3/21 | Extremely thin implementation (LOC=3) | なし |
| 15 | `OpenGSCore\Loading\LoadingFailReason.cs` | **45** | 1/11 | Extremely thin implementation (LOC=1) | なし |
| 16 | `OpenGSCore\Map\AbstractStage.cs` | **45** | 1/11 | Extremely thin implementation (LOC=1) | なし |
| 17 | `OpenGSCore\Map\ArchloadGunster.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 18 | `OpenGSCore\Map\Chiristmas.cs` | **45** | 1/13 | Extremely thin implementation (LOC=1) | なし |
| 19 | `OpenGSCore\Map\DryDays.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 20 | `OpenGSCore\Map\Forest.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 21 | `OpenGSCore\Map\GhostHouse.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 22 | `OpenGSCore\Map\GreenHill.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 23 | `OpenGSCore\Map\House.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 24 | `OpenGSCore\Map\RobotFactory.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 25 | `OpenGSCore\Map\Ruin.cs` | **45** | 1/10 | Extremely thin implementation (LOC=1) | なし |
| 26 | `OpenGSCore\Match\Rule\IMatchResultEvaluator.cs` | **45** | 2/19 | Extremely thin implementation (LOC=2) | なし |
| 27 | `OpenGSCore\Match\Setting\SuvMatchSetting.cs` | **45** | 2/15 | Extremely thin implementation (LOC=2) | なし |
| 28 | `OpenGSCore\Match\Setting\Team\CaptureTheFlagMatchSetting.cs` | **45** | 4/19 | Extremely thin implementation (LOC=4) | なし |
| 29 | `OpenGSCore\Match\Setting\Team\TDMMatchSetting.cs` | **45** | 3/16 | Extremely thin implementation (LOC=3) | なし |
| 30 | `OpenGSCore\Match\Setting\Team\TeamSurvival.cs` | **45** | 5/31 | Extremely thin implementation (LOC=5) | なし |

## 📁 OpenGSServer 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `OpenGSServer\Server\Event\ManagementServerEvent.cs` | **105** | 4/27 | Empty methods x 3 (Abstract: False), Extremely thin implementation (LOC=4) | なし |
| 2 | `OpenGSServer\Database\ServerInfoDatabaseManager.cs` | **85** | 11/45 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=11) | なし |
| 3 | `OpenGSServer\Room\WaitRoomEvent.cs` | **85** | 3/25 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=3) | なし |
| 4 | `OpenGSServer\Server\Event\PlayerEventHandler.cs` | **85** | 3/23 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=3) | なし |
| 5 | `OpenGSServer\Server\RUDP\MatchRUDPClientSession.cs` | **85** | 15/50 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=15) | なし |
| 6 | `OpenGSServer\Database\AccountDatabaseManager.cs` | **80** | 121/396 | Empty methods x 4 (Abstract: False) | なし |
| 7 | `OpenGSServer\Match\MatchRoomNetworkFunc.cs` | **65** | 2/19 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=2) | なし |
| 8 | `OpenGSServer\Server\MissionServer.cs` | **65** | 2/16 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=2) | なし |
| 9 | `OpenGSServer\Database\MatchDatabaseManager.cs` | **60** | 26/101 | Empty methods x 3 (Abstract: False) | なし |
| 10 | `OpenGSServer\Game\Grenade.cs` | **60** | 21/42 | Empty methods x 3 (Abstract: False) | なし |
| 11 | `OpenGSServer\ゴミ\Socket.cs` | **60** | 0/28 | Empty file / only declarations (LOC=0) | なし |
| 12 | `OpenGSServer\DataBase.cs` | **45** | 4/15 | Extremely thin implementation (LOC=4) | なし |
| 13 | `OpenGSServer\Hash.cs` | **45** | 1/13 | Extremely thin implementation (LOC=1) | なし |
| 14 | `OpenGSServer\Account\FriendList.cs` | **45** | 2/16 | Extremely thin implementation (LOC=2) | なし |
| 15 | `OpenGSServer\Constants\GlobalConstants.cs` | **45** | 4/19 | Extremely thin implementation (LOC=4) | なし |
| 16 | `OpenGSServer\Constants\RoomConstants.cs` | **45** | 2/14 | Extremely thin implementation (LOC=2) | なし |
| 17 | `OpenGSServer\Database\AbstractDatabaseManager.cs` | **45** | 1/15 | Extremely thin implementation (LOC=1) | なし |
| 18 | `OpenGSServer\Database\DBAccountDetail.cs` | **45** | 1/12 | Extremely thin implementation (LOC=1) | なし |
| 19 | `OpenGSServer\Database\MissionScoreManager.cs` | **45** | 1/12 | Extremely thin implementation (LOC=1) | なし |
| 20 | `OpenGSServer\Extentions\DictionaryExtention.cs` | **45** | 2/13 | Extremely thin implementation (LOC=2) | なし |
| 21 | `OpenGSServer\Lobby\MissionLobby.cs` | **45** | 4/30 | Extremely thin implementation (LOC=4) | なし |
| 22 | `OpenGSServer\Manager\PlayerScoreManager.cs` | **45** | 3/20 | Extremely thin implementation (LOC=3) | なし |
| 23 | `OpenGSServer\Manager\ServerSettingManager.cs` | **45** | 14/57 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=14) | なし |
| 24 | `OpenGSServer\Match\MatchRoomNetwork.cs` | **45** | 3/26 | Extremely thin implementation (LOC=3) | なし |
| 25 | `OpenGSServer\Platform\Windows\WindowsAPI.cs` | **45** | 5/18 | Extremely thin implementation (LOC=5) | なし |
| 26 | `OpenGSServer\Player\WaitRoomPlayerInfo.cs` | **45** | 1/14 | Extremely thin implementation (LOC=1) | なし |
| 27 | `OpenGSServer\Result\CreateNewMissionRoom.cs` | **45** | 1/12 | Extremely thin implementation (LOC=1) | なし |
| 28 | `OpenGSServer\Result\UserInfoResult.cs` | **45** | 4/27 | Extremely thin implementation (LOC=4) | なし |
| 29 | `OpenGSServer\Result\WaitRoomInfo.cs` | **45** | 1/13 | Extremely thin implementation (LOC=1) | なし |
| 30 | `OpenGSServer\Room\MissionRoom.cs` | **45** | 4/24 | Extremely thin implementation (LOC=4) | なし |

## 📁 OpenGSR 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `OpenGSR\Assets\Scripts\Player\CharaController.cs` | **200** | 176/522 | Empty methods x 10 (Abstract: False) | なし |
| 2 | `OpenGSR\Assets\Scripts\Player\AIPlayerController.cs` | **140** | 61/205 | Empty methods x 7 (Abstract: False) | なし |
| 3 | `OpenGSR\Assets\Scripts\Scene\TitleScene.cs` | **140** | 54/143 | Empty methods x 7 (Abstract: False) | なし |
| 4 | `OpenGSR\Assets\Scripts\Match\MatchRule.cs` | **120** | 113/275 | Empty methods x 6 (Abstract: False) | なし |
| 5 | `OpenGSR\Assets\Scripts\Mission\SkyFighterMainScript.cs` | **120** | 18/87 | Empty methods x 6 (Abstract: False) | なし |
| 6 | `OpenGSR\Assets\Scripts\Core\StageList.cs` | **105** | 12/50 | Empty methods x 4 (Abstract: False), Thin implementation (LOC=12) | なし |
| 7 | `OpenGSR\Assets\Scripts\Scene\MatchNetworkManagerMainScript.cs` | **105** | 7/34 | Empty methods x 4 (Abstract: False), Thin implementation (LOC=7) | なし |
| 8 | `OpenGSR\Assets\Scripts\Core\CustomRenderer.cs` | **100** | 0/24 | Empty methods x 2 (Abstract: False), Empty file / only declarations (LOC=0) | なし |
| 9 | `OpenGSR\Assets\Scripts\Player\AsmExport\AbstractPlayer.cs` | **90** | 276/608 | Empty methods x 18 (Abstract: True) | なし |
| 10 | `OpenGSR\Assets\Scripts\BaseLib\Audio.cs` | **85** | 3/14 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=3) | なし |
| 11 | `OpenGSR\Assets\Scripts\Core\FireEffect.cs` | **85** | 9/43 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=9) | なし |
| 12 | `OpenGSR\Assets\Scripts\Core\Rigidbody2DExtension.cs` | **85** | 8/34 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=8) | なし |
| 13 | `OpenGSR\Assets\Scripts\Core\Base\EventManager.cs` | **85** | 13/58 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=13) | なし |
| 14 | `OpenGSR\Assets\Scripts\Core\Base\MissionManager.cs` | **85** | 5/29 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=5) | なし |
| 15 | `OpenGSR\Assets\Scripts\Match\GodModeMainScript.cs` | **85** | 4/25 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=4) | なし |
| 16 | `OpenGSR\Assets\Scripts\Match\TSUVMainScript.cs` | **85** | 14/68 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=14) | なし |
| 17 | `OpenGSR\Assets\Scripts\Player\AsmExport\PlayerDataLinker.cs` | **85** | 4/26 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=4) | なし |
| 18 | `OpenGSR\Assets\Scripts\Resource\StageList.cs` | **85** | 11/34 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=11) | なし |
| 19 | `OpenGSR\Assets\Scripts\Scene\WaitRoom\OnlineWaitRoomSceneEvent.cs` | **85** | 12/52 | Empty methods x 3 (Abstract: False), Thin implementation (LOC=12) | なし |
| 20 | `OpenGSR\Assets\Scripts\UI\CommonCanvas.cs` | **85** | 4/17 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=4) | なし |
| 21 | `OpenGSR\Assets\Scripts\UI\LoadingSceneCanvas.cs` | **85** | 4/16 | Empty methods x 2 (Abstract: False), Extremely thin implementation (LOC=4) | なし |
| 22 | `OpenGSR\Assets\Scripts\Player\PlayerAgent.cs` | **80** | 406/934 | Empty methods x 4 (Abstract: False) | なし |
| 23 | `OpenGSR\Assets\Scripts\Scene\ExportAssets\ExportAssetScene.cs` | **80** | 203/409 | Unfinished markers x 3, Empty methods x 1 (Abstract: False) | 未実装 (L54): `noop for now - placeholder for editor...` |
| 24 | `OpenGSR\Assets\Scripts\Scene\AbstractScene.cs` | **75** | 251/511 | Empty methods x 15 (Abstract: True) | なし |
| 25 | `OpenGSR\Assets\Scripts\Match\AbstractMatchMainScript.cs` | **70** | 273/666 | Empty methods x 14 (Abstract: True) | なし |
| 26 | `OpenGSR\Assets\Scripts\SandBag.cs` | **65** | 10/40 | Empty methods x 2 (Abstract: False), Thin implementation (LOC=10) | なし |
| 27 | `OpenGSR\Assets\Scripts\BaseLib\GameFlagManagerControlPanel.cs` | **65** | 2/17 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=2) | なし |
| 28 | `OpenGSR\Assets\Scripts\BaseLib\HUDPlayerNameCanvas.cs` | **65** | 5/42 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=5) | なし |
| 29 | `OpenGSR\Assets\Scripts\Core\BurstArea.cs` | **65** | 7/28 | Empty methods x 2 (Abstract: False), Thin implementation (LOC=7) | なし |
| 30 | `OpenGSR\Assets\Scripts\Core\ExpEffect.cs` | **65** | 13/49 | Empty methods x 2 (Abstract: False), Thin implementation (LOC=13) | なし |

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
