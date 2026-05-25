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
  - ~~`OpenGSCore\FlagState.cs`~~
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
  - ~~`OpenGSCore\Extention\DictionaryExtension.cs`~~
  - ~~`OpenGSCore\Match\Rule\IMatchResultEvaluator.cs`~~
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
  - ~~`OpenGSServer\Extentions\DictionaryExtention.cs`~~
  - ~~`OpenGSServer\Database\AccountDatabaseManager.cs`~~
  - ~~`OpenGSServer\Database\MatchDatabaseManager.cs`~~
  - ~~`OpenGSServer\Server\MissionServer.cs`~~
  - ~~`OpenGSServer\Hash.cs`~~
  - ~~`OpenGSServer\Account\FriendList.cs`~~
  - ~~`OpenGSServer\Database\DBAccountDetail.cs`~~
  - ~~`OpenGSServer\Database\MissionScoreManager.cs`~~
  - ~~`OpenGSServer\Game\Grenade.cs`~~
  - ~~`OpenGSServer\DataBase.cs`~~
  - ~~`OpenGSServer\Constants\GlobalConstants.cs`~~
  - ~~`OpenGSServer\Constants\RoomConstants.cs`~~
  - ~~`OpenGSServer\Platform\Windows\WindowsAPI.cs`~~
  - ~~`OpenGSServer\Player\WaitRoomPlayerInfo.cs`~~
  - ~~`OpenGSServer\Result\CreateNewMissionRoom.cs`~~
  - ~~`OpenGSServer\Result\UserInfoResult.cs`~~
  - ~~`OpenGSServer\Result\WaitRoomInfo.cs`~~
  - ~~`OpenGSServer\Room\MissionRoom.cs`~~
  - ~~`OpenGSServer\Settings\DefaultServerSetting.cs`~~
  - ~~`OpenGSServer\Lobby\MissionLobby.cs`~~
  - ~~`OpenGSServer\Manager\PlayerScoreManager.cs`~~
- `OpenGSR`
  - ~~`OpenGSR\Assets\Scripts\Core\Base\EventManager.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\Base\MissionManager.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\Base\OpenGSBaseClass.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\BurstArea.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\CustomRenderer.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\SceneInput.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\SpriteRendererExtension.cs`~~
  - ~~`OpenGSR\Assets\Scripts\BaseLib\Audio.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\ExpEffect.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\FireEffect.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\Rigidbody2DExtension.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\TransformExtension.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Core\StageList.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Match\GodModeMainScript.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Scene\MatchNetworkManagerMainScript.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Match\MatchEventProvider.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Match\TSUVMainScript.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Network\ConnectToLobbyNetworkManager.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Player\AsmExport\PlayerDataLinker.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Player\AsmExport\MyScore.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Resource\StageList.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Scene\WaitRoom\OnlineWaitRoomSceneEvent.cs`~~
  - ~~`OpenGSR\Assets\Scripts\UI\CommonCanvas.cs`~~
  - ~~`OpenGSR\Assets\Scripts\UI\LoadingSceneCanvas.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Scene\ExportAssets\ExportAssetScene.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Player\PlayerAgent.cs`~~
  - ~~`OpenGSR\Assets\Scripts\Weapon\FieldWeaponAgent.cs`~~
  - ~~`OpenGSR\Assets\Scripts\SandBag.cs`~~
  - ~~`OpenGSR\Assets\Scripts\BaseLib\GameFlagManagerControlPanel.cs`~~
  - ~~`OpenGSR\Assets\Scripts\BaseLib\HUDPlayerNameCanvas.cs`~~

---

## 🏆 総合ワーストランキング（未実装・薄いクラス TOP 30）

| 順位 | プロジェクト | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 |
| :---: | :--- | :--- | :---: | :---: | :--- |
| 1 | `OpenGSR` | `~~OpenGSR\Assets\Scripts\Core\SceneInput.cs~~` | **65** | 4/35 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=4) |
| 2 | `OpenGSR` | `~~OpenGSR\Assets\Scripts\Core\SpriteRendererExtension.cs~~` | **65** | 3/19 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3) |
| 3 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\Base\GameStartup.cs` | **65** | 3/15 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3) |
| 4 | `OpenGSR` | `OpenGSR\Assets\Scripts\Match\AsmExport\ArmMainScript.cs` | **65** | 2/11 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=2) |
| 5 | `OpenGSR` | `OpenGSR\Assets\Scripts\Mission\MissionMainScript.cs` | **65** | 15/66 | Empty methods x 2 (Abstract: False), Thin implementation (LOC=15) |
| 6 | `OpenGSR` | `~~OpenGSR\Assets\Scripts\Network\ConnectToLobbyNetworkManager.cs~~` | **65** | 3/12 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3) |
| 7 | `OpenGSR` | `~~OpenGSR\Assets\Scripts\Player\AsmExport\MyScore.cs~~` | **65** | 15/32 | Empty methods x 2 (Abstract: False), Thin implementation (LOC=15) |
| 8 | `OpenGSR` | `~~OpenGSR\Assets\Scripts\Weapon\FieldWeaponAgent.cs~~` | **65** | 5/27 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=5) |
| 9 | `OpenGSR` | `OpenGSR\Assets\Scripts\DamageCanvas.cs` | **60** | 24/90 | Empty methods x 3 (Abstract: False) |
| 10 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\MultipleTags.cs` | **60** | 88/224 | Empty methods x 3 (Abstract: False) |
| 11 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\NewBehaviourScript.cs` | **60** | 0/22 | Empty file / only declarations (LOC=0) |
| 12 | `OpenGSR` | `~~OpenGSR\Assets\Scripts\Core\TransformExtension.cs~~` | **60** | 17/56 | Empty methods x 3 (Abstract: False) |
| 13 | `OpenGSR` | `OpenGSR\Assets\Scripts\Interface\IMetalBreakerMainScript.cs` | **60** | 1/13 | TODO comments x 1, Extremely thin implementation (LOC=1) |
| 14 | `OpenGSR` | `OpenGSR\Assets\Scripts\Mission\MetalBreakerMainScript.cs` | **60** | 21/89 | Empty methods x 3 (Abstract: False) |
| 15 | `OpenGSR` | `OpenGSR\Assets\Scripts\NetworkTest\LocalTestMatchRUDPServer.cs` | **60** | 317/562 | Unfinished markers x 3 |
| 16 | `OpenGSR` | `OpenGSR\Assets\Scripts\Player\Character.cs` | **60** | 128/226 | Empty methods x 3 (Abstract: False) |
| 17 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\OfflineLoadingScene.cs` | **60** | 67/155 | Empty methods x 3 (Abstract: False) |
| 18 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\OfflineWaitRoomScene.cs` | **60** | 455/858 | Empty methods x 3 (Abstract: False) |
| 19 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\OnlineLoadingScene.cs` | **60** | 170/348 | Empty methods x 3 (Abstract: False) |
| 20 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\OnlineLobbyScene.cs` | **60** | 450/848 | Empty methods x 3 (Abstract: False) |
| 21 | `OpenGSR` | `OpenGSR\Assets\Scripts\Scene\OnlineWaitRoomScene.cs` | **60** | 399/815 | Empty methods x 3 (Abstract: False) |
| 22 | `OpenGSR` | `OpenGSR\Assets\Scripts\Stages\AsmExport\FlagStand.cs` | **60** | 74/201 | Empty methods x 3 (Abstract: False) |
| 23 | `OpenGSR` | `OpenGSR\Assets\Scripts\Weapon\FieldWeaponController.cs` | **60** | 44/183 | Empty methods x 3 (Abstract: False) |
| 24 | `OpenGSR` | `OpenGSR\Assets\Scripts\Core\TeamBalanceButton.cs` | **55** | 5/32 | Empty methods x 2 (Abstract: True), Extremely thin implementation (LOC=5) |
| 25 | `OpenGSR` | `OpenGSR\Assets\Scripts\BaseLib\Map\JumpStand.cs` | **50** | 11/45 | Empty methods x 5 (Abstract: True), Thin implementation (LOC=11) |
| 26 | `OpenGSCore` | `OpenGSCore\Item\AbstractItemEffect.cs` | **45** | 5/15 | Extremely thin implementation (LOC=5) |
| 27 | `OpenGSCore` | `OpenGSCore\Match\Situation\CaptureTheFlagMatchSituation.cs` | **45** | 4/12 | Extremely thin implementation (LOC=4) |
| 28 | `OpenGSCore` | `OpenGSCore\Match\Situation\DeathMatchMatchSituation.cs` | **45** | 2/10 | Extremely thin implementation (LOC=2) |
| 29 | `OpenGSCore` | `OpenGSCore\Match\Situation\SurvivalMatchSituation.cs` | **45** | 2/10 | Extremely thin implementation (LOC=2) |
| 30 | `OpenGSCore` | `OpenGSCore\Match\Situation\TeamSurvivalMatchSituation.cs` | **45** | 5/16 | Extremely thin implementation (LOC=5) |

---

## 📁 OpenGSCore 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `OpenGSCore\Item\AbstractItemEffect.cs` | **45** | 5/15 | Extremely thin implementation (LOC=5) | なし |
| 2 | `OpenGSCore\Match\Situation\CaptureTheFlagMatchSituation.cs` | **45** | 4/12 | Extremely thin implementation (LOC=4) | なし |
| 3 | `OpenGSCore\Match\Situation\DeathMatchMatchSituation.cs` | **45** | 2/10 | Extremely thin implementation (LOC=2) | なし |
| 4 | `OpenGSCore\Match\Situation\SurvivalMatchSituation.cs` | **45** | 2/10 | Extremely thin implementation (LOC=2) | なし |
| 5 | `OpenGSCore\Match\Situation\TeamSurvivalMatchSituation.cs` | **45** | 5/16 | Extremely thin implementation (LOC=5) | なし |
| 6 | `OpenGSCore\Mission\MissionRule.cs` | **45** | 5/19 | Extremely thin implementation (LOC=5) | なし |
| 7 | `OpenGSCore\Module\Init.cs` | **45** | 4/10 | Extremely thin implementation (LOC=4) | なし |
| 8 | `OpenGSCore\Player\PlayerIDList.cs` | **45** | 1/8 | Extremely thin implementation (LOC=1) | なし |
| 9 | `OpenGSCore\Player\WaitRoomPlayerInfo.cs` | **45** | 2/15 | Extremely thin implementation (LOC=2) | なし |
| 10 | `OpenGSCore\Request\PingRequest.cs` | **45** | 2/12 | Extremely thin implementation (LOC=2) | なし |
| 11 | `OpenGSCore\Result\PlayerResult.cs` | **45** | 2/12 | Extremely thin implementation (LOC=2) | なし |
| 12 | `OpenGSCore\Score\AllPlayerMissionFinalSocre.cs` | **45** | 4/17 | Extremely thin implementation (LOC=4) | なし |
| 13 | `OpenGSCore\Score\MissionFinalScore.cs` | **45** | 3/17 | Extremely thin implementation (LOC=3) | なし |
| 14 | `OpenGSCore\Score\MissionResultScore.cs` | **45** | 2/10 | Extremely thin implementation (LOC=2) | なし |
| 15 | `OpenGSCore\Time\Time.cs` | **45** | 4/23 | Extremely thin implementation (LOC=4) | なし |
| 16 | `OpenGSCore\Time\TimeDefines.cs` | **45** | 5/14 | Extremely thin implementation (LOC=5) | なし |
| 17 | `OpenGSCore\Time\TimeLimit.cs` | **45** | 8/30 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=8) | なし |
| 18 | `OpenGSCore\Utility\GenerateUniqueID.cs` | **45** | 5/23 | Extremely thin implementation (LOC=5) | なし |
| 19 | `OpenGSCore\Utility\MakeGameObject.cs` | **45** | 5/17 | Extremely thin implementation (LOC=5) | なし |
| 20 | `OpenGSCore\Utility\MakeTimeStamp.cs` | **45** | 2/13 | Extremely thin implementation (LOC=2) | なし |
| 21 | `OpenGSCore\Utility\TagAttribute.cs` | **45** | 5/14 | Extremely thin implementation (LOC=5) | なし |
| 22 | `OpenGSCore\WaitRoom\RoomCapacity.cs` | **45** | 3/15 | Extremely thin implementation (LOC=3) | なし |
| 23 | `OpenGSCore\WaitRoom\WaitRoom(Event).cs` | **45** | 4/17 | Extremely thin implementation (LOC=4) | なし |
| 24 | `OpenGSCore\WaitRoom\WaitRoom(Network).cs` | **45** | 2/10 | Extremely thin implementation (LOC=2) | なし |
| 25 | `OpenGSCore\WaitRoom\WaitRoomStatus.cs` | **45** | 5/16 | Extremely thin implementation (LOC=5) | なし |
| 26 | `OpenGSCore\WaitRoom\OfflineWaitRoom.cs` | **40** | 7/36 | Empty methods x 3 (Abstract: True), Thin implementation (LOC=7) | なし |
| 27 | `OpenGSCore\Chat\ChatMacro.cs` | **25** | 7/33 | Thin implementation (LOC=7) | なし |
| 28 | `OpenGSCore\Constants\Tickrate.cs` | **25** | 9/19 | Thin implementation (LOC=9) | なし |
| 29 | `OpenGSCore\Encrypt\Encrypt.cs` | **25** | 9/39 | Thin implementation (LOC=9) | なし |
| 30 | `OpenGSCore\Event\AbstractGameEvent.cs` | **25** | 8/27 | Thin implementation (LOC=8) | なし |

## 📁 OpenGSServer 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `OpenGSServer\Database\AbstractDatabaseManager.cs` | **45** | 2/15 | Extremely thin implementation (LOC=2) | なし |
| 2 | `OpenGSServer\Room\RoomSetting.cs` | **45** | 3/15 | Extremely thin implementation (LOC=3) | なし |
| 3 | `OpenGSServer\Room\WaitRoomSetting.cs` | **45** | 5/18 | Extremely thin implementation (LOC=5) | なし |
| 4 | `OpenGSServer\Server\IPBanList.cs` | **45** | 12/54 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=12) | なし |
| 5 | `OpenGSServer\Server\IServerEventHandler.cs` | **45** | 2/11 | Extremely thin implementation (LOC=2) | なし |
| 6 | `OpenGSServer\Server\IServerHost.cs` | **45** | 3/16 | Extremely thin implementation (LOC=3) | なし |
| 7 | `OpenGSServer\Server\Event\MatchRoomEventHandler.cs` | **45** | 9/40 | Empty methods x 1 (Abstract: False), Thin implementation (LOC=9) | なし |
| 8 | `OpenGSServer\Server\Session\MissionClientSession.cs` | **45** | 2/18 | Extremely thin implementation (LOC=2) | なし |
| 9 | `OpenGSServer\Stage\Stage.cs` | **45** | 2/12 | Extremely thin implementation (LOC=2) | なし |
| 10 | `OpenGSServer\Utility\LiteNetLibReaderExtensions.cs` | **45** | 2/13 | Extremely thin implementation (LOC=2) | なし |
| 11 | `OpenGSServer\Utility\Version.cs` | **45** | 4/20 | Extremely thin implementation (LOC=4) | なし |
| 12 | `OpenGSServer\ゴミ\CityOfDarkness2.cs` | **45** | 2/15 | Extremely thin implementation (LOC=2) | なし |
| 13 | `OpenGSServer\ゴミ\Socket.cs` | **45** | 1/28 | Extremely thin implementation (LOC=1) | なし |
| 14 | `OpenGSServer\Command\ServerCommand.cs` | **40** | 330/677 | Unfinished markers x 2 | 未実装 (L414): `ダミーの設定とイベントバスを使用` |
| 15 | `OpenGSServer\Server\AbstractServer.cs` | **40** | 29/78 | Empty methods x 2 (Abstract: False) | なし |
| 16 | `OpenGSServer\Network\LagCompensation\ServerPlayerStateManager.cs` | **35** | 97/241 | TODO comments x 1, Unfinished markers x 1 | TODO (L131): `TODO: 実際のサーバーサイド移動ロジックを実装`<br>未実装 (L126): `/ サーバー側の移動を適用（プレースホルダー実装）` |
| 17 | `OpenGSServer\DBFriend.cs` | **25** | 12/33 | Thin implementation (LOC=12) | なし |
| 18 | `OpenGSServer\Log.cs` | **25** | 8/24 | Thin implementation (LOC=8) | なし |
| 19 | `OpenGSServer\Core\MakeJson.cs` | **25** | 13/45 | Thin implementation (LOC=13) | なし |
| 20 | `OpenGSServer\Database\MatchDatabaseStruct.cs` | **25** | 13/63 | Thin implementation (LOC=13) | なし |
| 21 | `OpenGSServer\Infrastructure\CoreServerBridge.cs` | **25** | 12/41 | Thin implementation (LOC=12) | なし |
| 22 | `OpenGSServer\Manager\ServerInfoManager.cs` | **25** | 12/28 | Thin implementation (LOC=12) | なし |
| 23 | `OpenGSServer\Match\MatchRoomFactory.cs` | **25** | 8/21 | Thin implementation (LOC=8) | なし |
| 24 | `OpenGSServer\Match\Event\MatchRoomServerEvent.cs` | **25** | 11/45 | Thin implementation (LOC=11) | なし |
| 25 | `OpenGSServer\Network\UDPReceiver.cs` | **25** | 10/37 | Thin implementation (LOC=10) | なし |
| 26 | `OpenGSServer\Result\AbstractResult.cs` | **25** | 8/24 | Thin implementation (LOC=8) | なし |
| 27 | `OpenGSServer\Result\ChangeRoomSetting.cs` | **25** | 9/32 | Thin implementation (LOC=9) | なし |
| 28 | `OpenGSServer\Result\ChatResult.cs` | **25** | 10/38 | Thin implementation (LOC=10) | なし |
| 29 | `OpenGSServer\Result\CreateNewWaitroomResult.cs` | **25** | 8/28 | Thin implementation (LOC=8) | なし |
| 30 | `OpenGSServer\Result\LobbyInfoResult.cs` | **25** | 10/28 | Thin implementation (LOC=10) | なし |

## 📁 OpenGSR 個別ワーストランキング TOP 30

| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | `~~OpenGSR\Assets\Scripts\Core\SceneInput.cs~~` | **65** | 4/35 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=4) | なし |
| 2 | `~~OpenGSR\Assets\Scripts\Core\SpriteRendererExtension.cs~~` | **65** | 3/19 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3) | なし |
| 3 | `OpenGSR\Assets\Scripts\Core\Base\GameStartup.cs` | **65** | 3/15 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3) | なし |
| 4 | `OpenGSR\Assets\Scripts\Match\AsmExport\ArmMainScript.cs` | **65** | 2/11 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=2) | なし |
| 5 | `OpenGSR\Assets\Scripts\Mission\MissionMainScript.cs` | **65** | 15/66 | Empty methods x 2 (Abstract: False), Thin implementation (LOC=15) | なし |
| 6 | `~~OpenGSR\Assets\Scripts\Network\ConnectToLobbyNetworkManager.cs~~` | **65** | 3/12 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3) | なし |
| 7 | `~~OpenGSR\Assets\Scripts\Player\AsmExport\MyScore.cs~~` | **65** | 15/32 | Empty methods x 2 (Abstract: False), Thin implementation (LOC=15) | なし |
| 8 | `~~OpenGSR\Assets\Scripts\Weapon\FieldWeaponAgent.cs~~` | **65** | 5/27 | Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=5) | なし |
| 9 | `OpenGSR\Assets\Scripts\DamageCanvas.cs` | **60** | 24/90 | Empty methods x 3 (Abstract: False) | なし |
| 10 | `OpenGSR\Assets\Scripts\Core\MultipleTags.cs` | **60** | 88/224 | Empty methods x 3 (Abstract: False) | なし |
| 11 | `OpenGSR\Assets\Scripts\Core\NewBehaviourScript.cs` | **60** | 0/22 | Empty file / only declarations (LOC=0) | なし |
| 12 | `~~OpenGSR\Assets\Scripts\Core\TransformExtension.cs~~` | **60** | 17/56 | Empty methods x 3 (Abstract: False) | なし |
| 13 | `OpenGSR\Assets\Scripts\Interface\IMetalBreakerMainScript.cs` | **60** | 1/13 | TODO comments x 1, Extremely thin implementation (LOC=1) | TODO (L11): `TODO: MetalBreaker 固有のメソッドを追加する` |
| 14 | `OpenGSR\Assets\Scripts\Mission\MetalBreakerMainScript.cs` | **60** | 21/89 | Empty methods x 3 (Abstract: False) | なし |
| 15 | `OpenGSR\Assets\Scripts\NetworkTest\LocalTestMatchRUDPServer.cs` | **60** | 317/562 | Unfinished markers x 3 | 未実装 (L33): `テスト用：ダミープレイヤーの状態` |
| 16 | `OpenGSR\Assets\Scripts\Player\Character.cs` | **60** | 128/226 | Empty methods x 3 (Abstract: False) | なし |
| 17 | `OpenGSR\Assets\Scripts\Scene\OfflineLoadingScene.cs` | **60** | 67/155 | Empty methods x 3 (Abstract: False) | なし |
| 18 | `OpenGSR\Assets\Scripts\Scene\OfflineWaitRoomScene.cs` | **60** | 455/858 | Empty methods x 3 (Abstract: False) | なし |
| 19 | `OpenGSR\Assets\Scripts\Scene\OnlineLoadingScene.cs` | **60** | 170/348 | Empty methods x 3 (Abstract: False) | なし |
| 20 | `OpenGSR\Assets\Scripts\Scene\OnlineLobbyScene.cs` | **60** | 450/848 | Empty methods x 3 (Abstract: False) | なし |
| 21 | `OpenGSR\Assets\Scripts\Scene\OnlineWaitRoomScene.cs` | **60** | 399/815 | Empty methods x 3 (Abstract: False) | なし |
| 22 | `OpenGSR\Assets\Scripts\Stages\AsmExport\FlagStand.cs` | **60** | 74/201 | Empty methods x 3 (Abstract: False) | なし |
| 23 | `OpenGSR\Assets\Scripts\Weapon\FieldWeaponController.cs` | **60** | 44/183 | Empty methods x 3 (Abstract: False) | なし |
| 24 | `OpenGSR\Assets\Scripts\Core\TeamBalanceButton.cs` | **55** | 5/32 | Empty methods x 2 (Abstract: True), Extremely thin implementation (LOC=5) | なし |
| 25 | `OpenGSR\Assets\Scripts\BaseLib\Map\JumpStand.cs` | **50** | 11/45 | Empty methods x 5 (Abstract: True), Thin implementation (LOC=11) | なし |
| 26 | `OpenGSR\Assets\Scripts\BulletImpactEffect.cs` | **45** | 5/24 | Extremely thin implementation (LOC=5) | なし |
| 27 | `OpenGSR\Assets\Scripts\PlayerWeaponAttachment.cs` | **45** | 3/24 | Extremely thin implementation (LOC=3) | なし |
| 28 | `OpenGSR\Assets\Scripts\BaseLib\Controller2D.cs` | **45** | 4/18 | Extremely thin implementation (LOC=4) | なし |
| 29 | `OpenGSR\Assets\Scripts\BaseLib\DoEvent.cs` | **45** | 2/11 | Extremely thin implementation (LOC=2) | なし |
| 30 | `OpenGSR\Assets\Scripts\BaseLib\EDirection.cs` | **45** | 4/13 | Extremely thin implementation (LOC=4) | なし |

---

## 🔍 主要な「薄い実装」の詳細とコード解説

ランキング上位から、特に実装不足や放置されたメソッドが目立ち、機能追加の余地が大きい主要なクラスをピックアップして詳細を解説します。

### 🚨 1位：`SceneInput.cs` (OpenGSR) — 実装補強が必要な箇所
- **現状スコア**: **65** (LOC: 4 / Total: 35)
- **ファイルパス**: `OpenGSR\Assets\Scripts\Core\SceneInput.cs`
- **主な検出理由**: Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=4)
- **分析と影響**: このモジュールはクラス宣言やインターフェースの整合性は保たれていますが、実際の動作に必要なロジックが不足しているか、ダミーデータ・例外を返す仮の状態になっています。本番環境の機能連携に支障をきたすため、優先的に本実装を行う必要があります。

### 🚨 2位：`SpriteRendererExtension.cs` (OpenGSR) — 実装補強が必要な箇所
- **現状スコア**: **65** (LOC: 3 / Total: 19)
- **ファイルパス**: `OpenGSR\Assets\Scripts\Core\SpriteRendererExtension.cs`
- **主な検出理由**: Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3)
- **分析と影響**: このモジュールはクラス宣言やインターフェースの整合性は保たれていますが、実際の動作に必要なロジックが不足しているか、ダミーデータ・例外を返す仮の状態になっています。本番環境の機能連携に支障をきたすため、優先的に本実装を行う必要があります。

### 🚨 3位：`GameStartup.cs` (OpenGSR) — 実装補強が必要な箇所
- **現状スコア**: **65** (LOC: 3 / Total: 15)
- **ファイルパス**: `OpenGSR\Assets\Scripts\Core\Base\GameStartup.cs`
- **主な検出理由**: Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=3)
- **分析と影響**: このモジュールはクラス宣言やインターフェースの整合性は保たれていますが、実際の動作に必要なロジックが不足しているか、ダミーデータ・例外を返す仮の状態になっています。本番環境の機能連携に支障をきたすため、優先的に本実装を行う必要があります。

### 🚨 4位：`ArmMainScript.cs` (OpenGSR) — 実装補強が必要な箇所
- **現状スコア**: **65** (LOC: 2 / Total: 11)
- **ファイルパス**: `OpenGSR\Assets\Scripts\Match\AsmExport\ArmMainScript.cs`
- **主な検出理由**: Empty methods x 1 (Abstract: False), Extremely thin implementation (LOC=2)
- **分析と影響**: このモジュールはクラス宣言やインターフェースの整合性は保たれていますが、実際の動作に必要なロジックが不足しているか、ダミーデータ・例外を返す仮の状態になっています。本番環境の機能連携に支障をきたすため、優先的に本実装を行う必要があります。

---

## 💡 今後の改善に向けた推奨アプローチ

1. **実装完了ファイルの消し込み**:
   実装が完了したファイルは、ドキュメント冒頭の「## ✅ 実装済み」セクションに `~~ファイルパス~~` の形式で追加してください。次回スキャン時にランキングから自動で除外されます。
2. **上位クラスからの順次本実装**:
   現在ランキングの上位に入っているクラスは、いずれも機能フレームワークは構築済みで「中身のロジック」だけが空になっている状態です。メソッドの引数と戻り値の設計に沿って、必要な同期処理やデータ処理を追加してください。

---
*このレポートは `update_ranking.py` の静的解析により自動更新されました。*
