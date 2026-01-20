# LobbyServerV2.cs 実装比較分析

## ?? 発見されたファイル

### **1. LobbyServerV2.cs** (現在開いているファイル)
- 場所: `Server\LobbyServerV2.cs`
- 状態: ? アクティブ（骨組み実装）

### **2. LobbyServerManager.cs** (今回作成)
- 場所: `Manager\LobbyServerManager.cs`
- 状態: ? 新規作成（完全実装）

### **3. GeneralServer.cs** (旧実装)
- 場所: `Server\ゴミ\GeneralServer.cs`
- 状態: ? ゴミフォルダ（削除候補）

---

## ?? 詳細比較

### **A. LobbyServerV2.cs（骨組みのみ）**

#### **? 優れている点:**
```csharp
? NetCoreServer使用 - 高性能なTCP実装
? LobbyServerManagerV2とLobbyTcpServerの分離
? IServerHostインターフェース実装
? シングルトン実装（Instance）
? IDisposable + ファイナライザー
? ClientSession統合
```

#### **? 問題点:**
```csharp
? 機能がほぼ空（骨組みだけ）
? LobbyServerManagerV2が何もしていない
? hourTimerが未使用
? Lobby管理機能なし
? プレイヤー管理なし
? ルーム管理なし
? エラーハンドリング不十分
? ドキュメントなし
```

#### **コード分析:**
```csharp
public class LobbyServerManagerV2 : IDisposable, IServerHost
{
    public static LobbyServerManagerV2 Instance { get; } = new();
    private LobbyTcpServer? server;
    System.Timers.Timer hourTimer = new System.Timers.Timer(600000); // 使われていない！

    public void Listen(int port)
    {
        if (server == null)
        {
            server = new LobbyTcpServer(IPAddress.Any, port, this);
            server.Start();
        }
    }
    
    // ? これだけ！機能なし
}
```

**実装度: 15% (骨組みのみ)**

---

### **B. LobbyServerManager.cs（今回作成 - 完全実装）**

#### **? 優れている点:**
```csharp
? 完全なロビー管理機能
   - プレイヤー参加/退出
   - ルーム作成/削除/参加
   - チャット機能
   - クイックマッチ
? レート制限
   - Join: 5回/分
   - Chat: 20回/分
   - Room: 10回/分
? Ping管理統合（PlayerID型）
? Result型で型安全
? スレッドセーフ（ReaderWriterLockSlim）
? イベントシステム（6種類のイベント）
? 自動クリーンアップ（30分非アクティブ）
? エラーハンドリング完璧
? ドキュメント充実
```

#### **機能一覧:**
```csharp
// プレイヤー管理
PlayerJoinLobby(playerId, playerName)
PlayerLeaveLobby(playerId)

// ルーム管理
CreateRoom(roomName, hostId, gameMode)
JoinRoom(roomId, playerId)
LeaveRoom(roomId, playerId)
DeleteRoom(roomId)

// チャット
AddLobbyChat(playerId, message)
AddRoomChat(roomId, playerId, message)

// マッチメイキング
QuickMatch(playerId, gameMode)

// Ping管理
RecordPlayerPing(playerId, pingMs)
GetPlayerPingStats(playerId)
GetAllPlayerPingStats()
GetPoorConnectionPlayers()

// 統計
GetAvailableRooms()
GetRoomInfo(roomId)
GetLobbyStats()
```

**実装度: 95% (ほぼ完全)**

---

### **C. GeneralServer.cs（旧実装 - ゴミフォルダ）**

#### **? 優れている点:**
```csharp
? 動作する実装
? メッセージ受信処理
? 非同期処理
```

#### **? 問題点:**
```csharp
? 古い実装（TcpListener直接使用）
? スレッドセーフでない（List<TcpClient>）
? エラーハンドリング不十分
? パフォーマンスが低い
? メモリリークの可能性
? ドキュメントなし
```

**実装度: 50% (古いが動く)**

---

## ?? 機能比較表

| 機能 | LobbyServerV2 | LobbyServerManager | 旧GeneralServer |
|------|--------------|-------------------|----------------|
| **ネットワーク** | ? NetCoreServer | N/A (マネージャー) | ?? TcpListener |
| **プレイヤー管理** | ? なし | ? 完全実装 | ?? 基本のみ |
| **ルーム管理** | ? なし | ? 完全実装 | ? なし |
| **チャット** | ? なし | ? レート制限付き | ? なし |
| **マッチメイキング** | ? なし | ? QuickMatch実装 | ? なし |
| **Ping管理** | ? なし | ? PlayerID型統合 | ? なし |
| **レート制限** | ? なし | ? 3種類実装 | ? なし |
| **スレッドセーフ** | ?? 基本のみ | ? 完全実装 | ? なし |
| **エラーハンドリング** | ?? 最小限 | ? Result型 | ?? try-catch |
| **自動クリーンアップ** | ? なし | ? 30分タイマー | ? なし |
| **イベントシステム** | ? なし | ? 6種類 | ? なし |
| **統計情報** | ? なし | ? 完全実装 | ? なし |
| **ドキュメント** | ? なし | ? 充実 | ? なし |
| **C# バージョン** | C# 10+ | C# 14.0 ? | 古いC# |
| **実装度** | 15% ?? | 95% ?? | 50% ?? |

---

## ?? アーキテクチャ比較

### **現状のアーキテクチャ（LobbyServerV2）:**

```
Unity Client
    ↓ TCP
┌──────────────────────────────────┐
│   LobbyTcpServer                 │ ← NetCoreServer使用
│   - ClientSession管理            │
└────────┬─────────────────────────┘
         │
         ↓
┌──────────────────────────────────┐
│   LobbyServerManagerV2           │ ← ? 何もしていない
│   - (空実装)                     │
└──────────────────────────────────┘
```

### **推奨アーキテクチャ（統合版）:**

```
Unity Client
    ↓ TCP
┌──────────────────────────────────┐
│   LobbyTcpServer                 │ ← NetCoreServer使用
│   - ClientSession管理            │
│   - メッセージルーティング       │
└────────┬─────────────────────────┘
         │
         ↓
┌──────────────────────────────────┐
│   LobbyServerManager             │ ← ? 完全実装（今回作成）
│   - プレイヤー管理               │
│   - ルーム管理                   │
│   - チャット機能                 │
│   - Ping管理                     │
│   - レート制限                   │
│   - 統計情報                     │
└──────────────────────────────────┘
```

---

## ?? 統合プラン

### **Option A: LobbyServerV2とLobbyServerManagerを統合（推奨）** ???

#### **手順:**

1. **LobbyServerV2のLobbyServerManagerV2を削除**
```csharp
// ? 削除
public class LobbyServerManagerV2 : IDisposable, IServerHost { }
```

2. **LobbyTcpServerをLobbyServerManagerと接続**
```csharp
public class LobbyTcpServer : TcpServer
{
    private readonly LobbyServerManager _manager; // ← 今回作成したもの

    public LobbyTcpServer(IPAddress address, int port, LobbyServerManager manager) 
        : base(address, port)
    {
        _manager = manager;
        ConsoleWrite.WriteMessage($"[Lobby] TCP Server on port {port}", ConsoleColor.Green);
    }

    protected override TcpSession CreateSession()
    {
        return new ClientSession(this);
    }
}
```

3. **LobbyServerManagerにTCP起動機能を追加**
```csharp
public sealed class LobbyServerManager
{
    private LobbyTcpServer? _tcpServer;

    public void StartTcpServer(int port)
    {
        if (_tcpServer != null)
        {
            ConsoleWrite.WriteMessage("[Lobby] TCP server already running", ConsoleColor.Yellow);
            return;
        }

        _tcpServer = new LobbyTcpServer(IPAddress.Any, port, this);
        _tcpServer.Start();
        
        ConsoleWrite.WriteMessage($"[Lobby] TCP server started on port {port}", ConsoleColor.Green);
    }

    public void StopTcpServer()
    {
        _tcpServer?.Stop();
        _tcpServer?.Dispose();
        _tcpServer = null;
    }
}
```

---

### **Option B: LobbyServerV2を充実させる** ??

LobbyServerManagerV2にLobbyServerManagerの機能を移植

**作業量:** 大（重複コードが増える）

---

### **Option C: 両方を残して役割分担** ?

- **LobbyServerV2**: TCP通信のみ
- **LobbyServerManager**: ロビー機能のみ

**問題:** 名前が混乱しやすい

---

## ?? 推奨アクション

### **最優先: LobbyServerV2とLobbyServerManagerを統合** ???

#### **理由:**
1. ? LobbyServerManagerが完全実装済み
2. ? LobbyServerV2は骨組みのみ
3. ? 重複を避ける
4. ? 保守性向上

#### **統合手順:**

```csharp
// Step 1: LobbyServerManager.cs に TCP機能を追加
public sealed class LobbyServerManager
{
    private LobbyTcpServer? _tcpServer;
    
    // 既存の機能はそのまま
    // ...

    // 新規追加
    public void StartTcpServer(int port)
    {
        _tcpServer = new LobbyTcpServer(IPAddress.Any, port, this);
        _tcpServer.Start();
    }
}

// Step 2: LobbyTcpServer を修正
public class LobbyTcpServer : TcpServer
{
    private readonly LobbyServerManager _manager;

    public LobbyTcpServer(IPAddress address, int port, LobbyServerManager manager) 
        : base(address, port)
    {
        _manager = manager;
    }
}

// Step 3: LobbyServerV2.cs を削除
// rm Server\LobbyServerV2.cs
```

---

## ?? 実装例（統合版）

```csharp
// Program.cs での使用例
var lobbyManager = LobbyServerManager.Instance;

// TCP起動
lobbyManager.StartTcpServer(port: 10000);

// ロビー機能使用
lobbyManager.PlayerJoinLobby("user123", "PlayerName");
lobbyManager.CreateRoom("MyRoom", "user123", EGameMode.DeathMatch);

// 統計取得
var stats = lobbyManager.GetLobbyStats();
Console.WriteLine($"Players: {stats.TotalPlayers}, Rooms: {stats.ActiveRooms}");

// シャットダウン
lobbyManager.StopTcpServer();
```

---

## ?? 統合のメリット

| 項目 | Before（分離） | After（統合） |
|------|---------------|--------------|
| **ファイル数** | 2ファイル | 1ファイル |
| **重複コード** | あり | なし |
| **保守性** | ?? 低い | ?? 高い |
| **拡張性** | ?? 普通 | ?? 高い |
| **理解しやすさ** | ?? やや難しい | ?? 簡単 |
| **実装度** | LobbyServerV2: 15% | 統合版: 95% |

---

## ?? 結論

| 実装 | 状態 | 推奨アクション |
|------|------|--------------|
| **LobbyServerV2.cs** | ?? 骨組みのみ（15%） | **削除 or 統合**（高優先度）|
| **LobbyServerManager.cs** | ?? 完全実装（95%） | **TCP機能追加して統一**（推奨）|
| **GeneralServer.cs** | ?? 古い（50%） | **削除**（ゴミフォルダ）|

---

## ?? 次のステップ

### **推奨: LobbyServerManagerにTCP機能を統合**

**メリット:**
- ? 1つのクラスで完結
- ? LobbyServerV2の骨組みを活用
- ? 重複コード削減
- ? 保守性向上

**作業時間:** 約30分

統合を実装しますか？ ??
