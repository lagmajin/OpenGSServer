# 未使用クラスの整理状況

このファイルは、参照調査とビルド結果として検証済みの削除内容を記録します。
推測ではなく、`git grep` の参照調査とビルド結果に基づきます。

## 完了: 削除済み

### `Server/ゴミ/` と `ゴミ/`

これらのファイルは削除候補当时、ビルド対象に含まれていました
（`OpenGSServer.csproj` の `Compile Remove` に `ゴミ` の指定が無く、
実際にコンパイルされていました）。

| ファイル | 内容 | 判定 |
| --- | --- | --- |
| `Server/ゴミ/GeneralServer.cs` | 旧TCPリスナ。`ServerManager` の static フィールドから new されるだけ | 削除 |
| `Server/ゴミ/MatchServer.cs` | 旧マッチサーバ。同様に static フィールドから new されるだけ | 削除 |
| `Server/ゴミ/OldServer2.cs` | 全体がコメントアウトされた旧サーバ | 削除 |
| `ゴミ/Socket.cs` | 全体がコメントアウト | 削除 |
| `ゴミ/GameManager.cs` | `[Obsolete]` 付き、`parseMessage` が空 | 削除 |
| `ゴミ/CityOfDarkness2.cs` | 空クラス、参照ゼロ | 削除 |

`GeneralServer` と `MatchServer` は副作用のない空コンストラクタでした。
そのため `ServerManager` のフィールドと `GetGeneralServer()` /
`GetMatchServer()` / `GetManagementServer()` を一体として削除しても、
起動と通信に影響しないことを確認しています。

現在の `ServerManager` は設定と管理者アカウントのみを担当します。
実際のリスナは `ServerHost`（`Server/ServerHost.cs`）が
`LobbyServerManager` / `MatchServerV2` / `ManagementServer` を直接扱います。

## 完了: 存在しないファイルへの除外指定を削除

`OpenGSServer.csproj` に、実ファイルが存在しない
`Compile Remove` / `None Include` 指定が6件ありました。
実ファイルが存在しないため無効なので、該当グループを削除しました。

- `Game/GameScene.cs`（実体は `Deprecated/GameScene.cs` へ移動済み）
- `Server/Event/LobbyEventHandlerV2.cs`（`Deprecated/` へ移動済み）
- `Constants/Tickrate.cs`（`Deprecated/` へ移動済み）
- `Constants/ItemConstants.cs`（`Deprecated/` へ移動済み）
- `Room/OldWaitRoom.cs`（`Deprecated/` へ移動済み）

## 残存: `Deprecated/` 配下（現在もビルド対象外）

以下は `OpenGSServer.csproj` の `<Compile Remove="Deprecated\**" />` で
確実に除外されています。将来的に削除する候補です。

- `GameScene.cs`
- `InstantItem.cs`
- `ItemConstants.cs`
- `LobbyEventHandlerV2.cs`
- `OldAbstractGameRoom.cs`
- `OldMatchRoom.cs`
- `OldWaitRoom.cs`
- `Tickrate.cs`

## 残存: `OpenGSCore/Deprecated/`

- `AbstactFieldItem.cs`

`OpenGSCore` 側は `UnityEngine` 依存の整理が絡むため、別課題で扱ってください。

## 削除後の検証手順

削除後は必ず次を通します。

```powershell
dotnet build OpenGSServer.sln
dotnet test Tests/OpenGSServer.Tests.csproj
.\tools\run_smoke.ps1
```
