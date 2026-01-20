using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OpenGSServer;

/// <summary>
/// サーバーバッチ処理サービス
/// 定期的なタスク（1時間/1日）を実行
/// </summary>
public sealed class ServerBatchService : IDisposable
{
    private readonly string _dataPath;
    private readonly string _portFilePath;
    private readonly string _statsFilePath;
    private readonly string _logDirectory;
    
    private Timer? _hourlyTimer;
    private Timer? _dailyTimer;
    private bool _disposed;
    private bool _isRunning;

    public ServerBatchService()
    {
        _dataPath = UnityPaths.PersistentDataPath;
        _portFilePath = Path.Combine(_dataPath, "local_server.txt");
        _statsFilePath = Path.Combine(_dataPath, "server_stats.json");
        _logDirectory = Path.Combine(_dataPath, "logs");

        // ログディレクトリ作成
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    /// <summary>
    /// サービス開始
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            ConsoleWrite.WriteMessage("[Batch] Service already running", ConsoleColor.Yellow);
            return;
        }

        ConsoleWrite.WriteMessage("[Batch] Starting batch service...", ConsoleColor.Cyan);

        // 1時間ごとのタイマー（3600秒）
        _hourlyTimer = new Timer(
            HourlyTaskCallback,
            null,
            TimeSpan.FromMinutes(1), // 初回は1分後に実行（テスト用）
            TimeSpan.FromHours(1)    // その後は1時間ごと
        );

        // 1日ごとのタイマー（86400秒）
        _dailyTimer = new Timer(
            DailyTaskCallback,
            null,
            TimeSpan.FromMinutes(5), // 初回は5分後に実行（テスト用）
            TimeSpan.FromDays(1)     // その後は1日ごと
        );

        _isRunning = true;
        ConsoleWrite.WriteMessage("[Batch] Service started successfully", ConsoleColor.Green);
        ConsoleWrite.WriteMessage("[Batch] Hourly tasks: Every 1 hour", ConsoleColor.Gray);
        ConsoleWrite.WriteMessage("[Batch] Daily tasks: Every 1 day", ConsoleColor.Gray);
    }

    /// <summary>
    /// サービス停止
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        ConsoleWrite.WriteMessage("[Batch] Stopping batch service...", ConsoleColor.Yellow);

        _hourlyTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _dailyTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        // 最終統計を保存
        SaveServerStats();

        _isRunning = false;
        ConsoleWrite.WriteMessage("[Batch] Service stopped", ConsoleColor.Green);
    }

    #region 1時間ごとのタスク

    /// <summary>
    /// 1時間ごとのタスクコールバック
    /// </summary>
    private void HourlyTaskCallback(object? state)
    {
        try
        {
            ConsoleWrite.WriteMessage("[Batch] Running hourly tasks...", ConsoleColor.Cyan);

            var sw = Stopwatch.StartNew();

            // 統計保存
            SaveServerStats();

            // Ping統計保存
            SavePingStats();

            // 非アクティブプレイヤーのクリーンアップ
            CleanupInactivePlayers();

            // 接続品質レポート
            GenerateConnectionQualityReport();

            sw.Stop();
            ConsoleWrite.WriteMessage($"[Batch] Hourly tasks completed in {sw.ElapsedMilliseconds}ms", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error in hourly tasks: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// サーバー統計を保存
    /// </summary>
    private void SaveServerStats()
    {
        try
        {
            var lobby = LobbyServerManager.Instance;
            var match = MatchServerV2.Instance;

            var lobbyStatsResult = lobby.GetLobbyStats();
            var matchStats = match.GetStats();

            object? lobbyData = null;
            lobbyStatsResult.Match(
                onSuccess: s =>
                {
                    lobbyData = new
                    {
                        s.TotalPlayers,
                        s.ActiveRooms,
                        s.AveragePing
                    };
                },
                onError: _ => { }
            );

            var stats = new
            {
                Timestamp = DateTime.UtcNow,
                Lobby = lobbyData,
                Match = new
                {
                    matchStats.TotalFrames,
                    matchStats.AverageFrameTime,
                    matchStats.ActiveRooms,
                    matchStats.PlayingRooms,
                    matchStats.TotalPlayers
                }
            };

            var json = JsonConvert.SerializeObject(stats, Formatting.Indented);
            File.WriteAllText(_statsFilePath, json);

            ConsoleWrite.WriteMessage($"[Batch] Server stats saved to {_statsFilePath}", ConsoleColor.Gray);
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error saving stats: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// Ping統計を保存
    /// </summary>
    private void SavePingStats()
    {
        try
        {
            var lobby = LobbyServerManager.Instance;
            var allStatsResult = lobby.GetAllPlayerPingStats();

            allStatsResult.Match(
                onSuccess: stats =>
                {
                    var pingFile = Path.Combine(_dataPath, $"ping_stats_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                    var json = JsonConvert.SerializeObject(stats, Formatting.Indented);
                    File.WriteAllText(pingFile, json);

                    ConsoleWrite.WriteMessage($"[Batch] Ping stats saved ({stats.Count} players)", ConsoleColor.Gray);
                },
                onError: error =>
                {
                    ConsoleWrite.WriteMessage($"[Batch] Error getting ping stats: {error}", ConsoleColor.Yellow);
                }
            );
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error saving ping stats: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// 非アクティブプレイヤーのクリーンアップ（30分以上非アクティブ）
    /// </summary>
    private void CleanupInactivePlayers()
    {
        try
        {
            // LobbyServerManagerで自動クリーンアップされているが、念のため確認
            ConsoleWrite.WriteMessage("[Batch] Checking for inactive players...", ConsoleColor.Gray);
            
            // 追加のクリーンアップロジックがあればここに追加
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error cleaning up players: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// 接続品質レポートを生成
    /// </summary>
    private void GenerateConnectionQualityReport()
    {
        try
        {
            var lobby = LobbyServerManager.Instance;
            var poorPlayersResult = lobby.GetPoorConnectionPlayers();

            poorPlayersResult.Match(
                onSuccess: poorPlayers =>
                {
                    if (poorPlayers.Count > 0)
                    {
                        ConsoleWrite.WriteMessage($"[Batch] Found {poorPlayers.Count} players with poor connection", ConsoleColor.Yellow);

                        foreach (var (playerId, stats) in poorPlayers.Take(5))
                        {
                            ConsoleWrite.WriteMessage(
                                $"  - {playerId}: {stats.AveragePing:F1}ms (Jitter: {stats.Jitter:F1}ms, Loss: {stats.PacketLoss:F1}%)",
                                ConsoleColor.Gray);
                        }
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage("[Batch] All players have good connection quality", ConsoleColor.Green);
                    }
                },
                onError: error =>
                {
                    ConsoleWrite.WriteMessage($"[Batch] Error generating connection report: {error}", ConsoleColor.Yellow);
                }
            );
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error in connection quality report: {ex.Message}", ConsoleColor.Red);
        }
    }

    #endregion

    #region 1日ごとのタスク

    /// <summary>
    /// 1日ごとのタスクコールバック
    /// </summary>
    private void DailyTaskCallback(object? state)
    {
        try
        {
            ConsoleWrite.WriteMessage("[Batch] Running daily tasks...", ConsoleColor.Cyan);

            var sw = Stopwatch.StartNew();

            // ログファイルのローテーション
            RotateLogFiles();

            // 古いPing統計ファイルの削除（7日以上前）
            CleanupOldFiles();

            // デイリーレポート生成
            GenerateDailyReport();

            sw.Stop();
            ConsoleWrite.WriteMessage($"[Batch] Daily tasks completed in {sw.ElapsedMilliseconds}ms", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error in daily tasks: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// ログファイルのローテーション
    /// </summary>
    private void RotateLogFiles()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return;
            }

            var logFiles = Directory.GetFiles(_logDirectory, "*.log");
            var today = DateTime.UtcNow.ToString("yyyyMMdd");

            foreach (var logFile in logFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(logFile);
                var newFileName = $"{fileName}_{today}.log";
                var newFilePath = Path.Combine(_logDirectory, "archive", newFileName);

                // アーカイブディレクトリ作成
                Directory.CreateDirectory(Path.Combine(_logDirectory, "archive"));

                // ファイル移動
                if (File.Exists(logFile))
                {
                    File.Move(logFile, newFilePath);
                }
            }

            ConsoleWrite.WriteMessage($"[Batch] Log files rotated ({logFiles.Length} files)", ConsoleColor.Gray);
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error rotating logs: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// 古いファイルのクリーンアップ（7日以上前）
    /// </summary>
    private void CleanupOldFiles()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var deletedCount = 0;

            // 古いPing統計ファイルを削除
            var pingFiles = Directory.GetFiles(_dataPath, "ping_stats_*.json");
            foreach (var file in pingFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTimeUtc < cutoffDate)
                {
                    File.Delete(file);
                    deletedCount++;
                }
            }

            // 古いログアーカイブを削除
            var archiveDir = Path.Combine(_logDirectory, "archive");
            if (Directory.Exists(archiveDir))
            {
                var archiveFiles = Directory.GetFiles(archiveDir, "*.log");
                foreach (var file in archiveFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTimeUtc < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }
            }

            ConsoleWrite.WriteMessage($"[Batch] Cleaned up {deletedCount} old files (>7 days)", ConsoleColor.Gray);
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error cleaning up old files: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// デイリーレポートを生成
    /// </summary>
    private void GenerateDailyReport()
    {
        try
        {
            var lobby = LobbyServerManager.Instance;
            var match = MatchServerV2.Instance;

            var report = new StringBuilder();
            report.AppendLine("=== Daily Server Report ===");
            report.AppendLine($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine();

            // Lobby統計
            var lobbyStatsResult = lobby.GetLobbyStats();
            lobbyStatsResult.Match(
                onSuccess: stats =>
                {
                    report.AppendLine("Lobby Statistics:");
                    report.AppendLine($"  Total Players: {stats.TotalPlayers}");
                    report.AppendLine($"  Active Rooms: {stats.ActiveRooms}");
                    report.AppendLine($"  Average Ping: {stats.AveragePing:F1}ms");
                    report.AppendLine();
                },
                onError: _ => { }
            );

            // Match統計
            var matchStats = match.GetStats();
            report.AppendLine("Match Statistics:");
            report.AppendLine($"  Total Frames: {matchStats.TotalFrames}");
            report.AppendLine($"  Average Frame Time: {matchStats.AverageFrameTime:F2}ms");
            report.AppendLine($"  Active Rooms: {matchStats.ActiveRooms}");
            report.AppendLine($"  Playing Rooms: {matchStats.PlayingRooms}");
            report.AppendLine($"  Total Players: {matchStats.TotalPlayers}");
            report.AppendLine();

            var reportFile = Path.Combine(_dataPath, $"daily_report_{DateTime.UtcNow:yyyyMMdd}.txt");
            File.WriteAllText(reportFile, report.ToString());

            ConsoleWrite.WriteMessage($"[Batch] Daily report generated: {reportFile}", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error generating daily report: {ex.Message}", ConsoleColor.Red);
        }
    }

    #endregion

    #region ポートファイル管理

    /// <summary>
    /// ローカルポート番号をファイルに書き込み
    /// </summary>
    public void WriteLocalPortToFile(int port)
    {
        if (port <= 0 || port > 65535)
        {
            ConsoleWrite.WriteMessage($"[Batch] Invalid port number: {port}", ConsoleColor.Red);
            return;
        }

        try
        {
            File.WriteAllText(_portFilePath, port.ToString());
            ConsoleWrite.WriteMessage($"[Batch] Port {port} written to {_portFilePath}", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error writing port file: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// ポートファイルを削除
    /// </summary>
    private void DeletePortFile()
    {
        try
        {
            if (File.Exists(_portFilePath))
            {
                File.Delete(_portFilePath);
                ConsoleWrite.WriteMessage("[Batch] Port file deleted", ConsoleColor.Gray);
            }
        }
        catch (Exception ex)
        {
            ConsoleWrite.WriteMessage($"[Batch] Error deleting port file: {ex.Message}", ConsoleColor.Red);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        DeletePortFile();

        _hourlyTimer?.Dispose();
        _dailyTimer?.Dispose();

        _disposed = true;
    }

    #endregion
}
