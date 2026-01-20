# 完成度が低いクラスの分析

## ?? 完成度評価（現在）

| クラス | 実装度 | 状態 | 優先度 |
|--------|-------|------|--------|
| **? 完成済み** |
| LobbyServerManager | 95% ?? | TCP統合済み | - |
| MatchServerV2 | 95% ?? | OpenGSCore統合済み | - |
| MatchUDPServer | 95% ?? | メッセージハンドリング実装 | - |
| PingManager | 95% ?? | PlayerID型統合 | - |
| **?? 低～中完成度** |
| ManagementServer | 30% ?? | 骨組みのみ | ??? |
| AccountManager | 60% ?? | 基本実装のみ | ?? |
| ServerBatchService | 40% ?? | 最小限の機能 | ? |
| AbstractGranade | 70% ?? | OnDestroy未実装 | ? |
| **?? 未完成・問題あり** |
| GameScene | 0% ?? | ビルドから除外 | ??? |
| ClientSession | 50% ?? | 要確認 | ?? |

---

## ?? 最優先: 完成度が低いクラス

### **1. ManagementServer.cs - 30%実装**

#### **現状:**
```csharp
public class ManagementServer : AbstractServer
{
    private ManagementTcpServer server = null; // ? 初期化されていない

    public override void Listen()
    {
        // ? 何もしていない！
        ConsoleWrite.WriteMessage("Management server started...", ConsoleColor.Green);
        working = true;
    }

    protected override void Update()
    {
        // ? 空実装
    }
    
    public override void Shutdown()
    {
        // ? 空実装
    }
}
```

#### **問題点:**
- ? ManagementTcpServerが初期化されていない
- ? Listen()が実際には何もしていない
- ? Update()が空
- ? Shutdown()が空
- ? サーバーが起動しない

#### **推奨実装:**
```csharp
public sealed class ManagementServer : AbstractServer
{
    public static ManagementServer Instance { get; } = new();
    private ManagementTcpServer? _server;

    public void Listen(int port)
    {
        if (_server != null)
        {
            ConsoleWrite.WriteMessage("[Management] Already running", ConsoleColor.Yellow);
            return;
        }

        _server = new ManagementTcpServer(IPAddress.Any, port, this);
        _server.Start();
        
        ConsoleWrite.WriteMessage($"[Management] Server started on port {port}", ConsoleColor.Green);
    }

    public override void Shutdown()
    {
        _server?.Stop();
        _server?.Dispose();
        _server = null;
    }
}
```

**実装度: 30% → 95%に改善可能**

---

### **2. GameScene.cs - 0%（ビルドから除外中）**

#### **現状:**
```xml
<!-- OpenGSServer.csproj -->
<Compile Remove="Game\GameScene.cs" />
```

#### **問題点:**
- ? OpenGSCore.Time型が見つからない
- ? OpenGSCore.PlayerGameObjectとの型不一致
- ? ビルドエラーで除外されている

#### **推奨アクション:**
- **Option A:** OpenGSCore.GameSceneを使用（推奨）
- **Option B:** 型を修正して復活
- **Option C:** 削除（OpenGSCore版を使う）

**実装度: 0% → OpenGSCore使用推奨**

---

### **3. AccountManager.cs - 60%実装**

#### **現状:**
```csharp
public class AccountManager : IAccountManager
{
    private Dictionary<string, PlayerAccount> logonUser = new(); // ?? スレッドセーフでない
    private Dictionary<string, PlayerServerInformation> playerInformation = new();
    
    public void AddNewLogonUser(in DBAccount db)
    {
        lock (lockObject) // ?? 複数のロックで競合の可能性
        {
            if (!logonUser.ContainsKey(db.AccountId))
            {
                logonUser.Add(db.AccountId, new PlayerAccount(...));
                
                lock (playerInformation) // ?? ネストしたロック
                {
                    playerInformation.Add(db.AccountId, info);
                }
            }
        }
    }
}
```

#### **問題点:**
- ?? Dictionary（非スレッドセーフ）を使用
- ?? ネストしたロックでデッドロックの可能性
- ?? 重複ログインの処理が未実装
- ? RemoveLogonUser()が不完全

#### **推奨実装:**
```csharp
public sealed class AccountManager
{
    private readonly ConcurrentDictionary<string, PlayerAccount> _logonUsers = new();
    private readonly ConcurrentDictionary<string, PlayerServerInformation> _playerInfo = new();
    
    public Result<PlayerAccount> AddNewLogonUser(DBAccount db)
    {
        var account = new PlayerAccount(db.AccountId, db.DisplayName, db.Password);
        
        if (!_logonUsers.TryAdd(db.AccountId, account))
        {
            return Result<PlayerAccount>.Error("User already logged in");
        }

        var info = new PlayerServerInformation(EPlayerPlayingStatus.Unknown, EPlayerLocation.Lobby);
        _playerInfo.TryAdd(db.AccountId, info);
        
        return Result<PlayerAccount>.Success(account);
    }
}
```

**実装度: 60% → 90%に改善可能**

---

### **4. AbstractGranade.cs - 70%実装**

#### **現状:**
```csharp
public void DisposeGrenade()
{
    state = GrenadeState.Disposed;
    // OnDestroy(); // TODO: AbstractGameObjectにOnDestroyメソッドを追加する必要がある
}
```

#### **問題点:**
- ? OnDestroy()がコメントアウト
- ? OpenGSCore.AbstractGameObjectにOnDestroy()がない

#### **推奨実装:**
```csharp
public void DisposeGrenade()
{
    state = GrenadeState.Disposed;
    
    // OpenGSCoreのメソッドを使用
    // または独自のクリーンアップ処理
    CleanupResources();
}

private void CleanupResources()
{
    // リソース解放処理
}
```

**実装度: 70% → 90%に改善可能**

---

## ?? 中程度の完成度

### **5. ServerBatchService.cs - 40%実装**

#### **現状:**
```csharp
internal class ServerBatchService
{
    public void OnStart()
    {
        // ? コメントアウト
        //string path = Path.Combine(UnityPaths.PersistentDataPath, "local_server.txt");
        //File.WriteAllText(path, port.ToString());
    }

    public void WriteLocalPortToFile(int port)
    {
        // ? 実装済み
        string path = Path.Combine(UnityPaths.PersistentDataPath, "local_server.txt");
        try
        {
            File.WriteAllText(path, port.ToString());
        }
        catch (Exception ex)
        {
            Console.Write("error"); // ?? ログが不十分
        }
    }
}
```

#### **問題点:**
- ? OnStart()が未実装
- ?? エラーログが"error"のみ
- ? バリデーションなし

#### **推奨実装:**
```csharp
public sealed class ServerBatchService
{
    private readonly string _portFilePath;

    public ServerBatchService()
    {
        _portFilePath = Path.Combine(UnityPaths.PersistentDataPath, "local_server.txt");
    }

    public Result<bool> WriteLocalPortToFile(int port)
    {
        if (port <= 0 || port > 65535)
            return Result<bool>.Error("Invalid port number");

        try
        {
            File.WriteAllText(_portFilePath, port.ToString());
            ConsoleWrite.WriteMessage($"[Batch] Port {port} written to file", ConsoleColor.Green);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error writing port: {ex.Message}", ConsoleColor.Red);
            return Result<bool>.Error($"Failed to write port: {ex.Message}");
        }
    }

    public void OnStop()
    {
        try
        {
            if (File.Exists(_portFilePath))
            {
                File.Delete(_portFilePath);
                ConsoleWrite.WriteMessage("[Batch] Port file deleted", ConsoleColor.Green);
            }
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error deleting port file: {ex.Message}", ConsoleColor.Red);
        }
    }
}
```

**実装度: 40% → 80%に改善可能**

---

### **6. ClientSession.cs - 50%実装（要確認）**

#### **確認が必要:**
- メッセージハンドリングの完成度
- エラーハンドリング
- スレッドセーフ性

---

## ?? 完成度サマリー

### **完成済み（95%以上）:**
- ? LobbyServerManager（TCP統合版）
- ? MatchServerV2（OpenGSCore統合版）
- ? MatchUDPServer（完全実装）
- ? PingManager（PlayerID型統合）

### **要改善（30-70%）:**
- ?? ManagementServer（30%） - 最優先
- ?? AccountManager（60%） - 高優先度
- ?? ServerBatchService（40%） - 中優先度
- ?? AbstractGranade（70%） - 低優先度

### **未実装・除外中:**
- ?? GameScene（0%） - OpenGSCore使用推奨
- ? SimpleMatchRoom - 削除済み（OpenGSCore使用）

---

## ?? 推奨優先順位

| 順位 | クラス | 理由 | 作業時間 |
|------|--------|------|---------|
| 1?? | **ManagementServer** | サーバーが起動しない | 30分 |
| 2?? | **AccountManager** | スレッドセーフ問題 | 45分 |
| 3?? | **GameScene** | OpenGSCore統合 | 15分 |
| 4?? | **ServerBatchService** | エラーハンドリング改善 | 20分 |
| 5?? | **AbstractGranade** | OnDestroy実装 | 10分 |

---

## ?? 次のアクション

### **最優先:**
**ManagementServer.cs を充実させる**
- Listen()を実装
- TCPサーバー初期化
- Shutdown()を実装

### **その次:**
**AccountManager.cs をスレッドセーフに**
- ConcurrentDictionary使用
- Result型導入
- 重複ログイン処理

実装しますか？ ??
