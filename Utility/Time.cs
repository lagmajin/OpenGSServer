using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenGSCore; // PlayerID型を使用

namespace OpenGSServer.Utility;

/// <summary>
/// Ping計算ユーティリティ
/// </summary>
public static class PingCalculator
{
    /// <summary>
    /// 2つのタイムスタンプからPingを計算
    /// </summary>
    public static int CalcPing(int m1, int m2) => Math.Abs(m1 - m2);

    /// <summary>
    /// RTT（Round Trip Time）を計算
    /// </summary>
    public static double CalcRtt(DateTime sentTime, DateTime receivedTime) 
        => (receivedTime - sentTime).TotalMilliseconds;

    /// <summary>
    /// タイムスタンプからRTTを計算（Unix時間）
    /// </summary>
    public static long CalcRtt(long sentTimestamp, long receivedTimestamp) 
        => Math.Abs(receivedTimestamp - sentTimestamp);
}

/// <summary>
/// Ping統計情報
/// C# 14.0: Record型
/// </summary>
public readonly record struct PingStats
{
    public required double AveragePing { get; init; }
    public required double MinPing { get; init; }
    public required double MaxPing { get; init; }
    public required double Jitter { get; init; }
    public required int SampleCount { get; init; }
    public required double PacketLoss { get; init; }
    public required NetworkQuality Quality { get; init; }

    /// <summary>
    /// レイテンシ補償値（予測用）
    /// </summary>
    public double LatencyCompensation => AveragePing + (Jitter * 2);

    public override string ToString() =>
        $"Ping: {AveragePing:F1}ms (Min: {MinPing:F1}ms, Max: {MaxPing:F1}ms), Jitter: {Jitter:F1}ms, Quality: {Quality}";
}

/// <summary>
/// ネットワーク品質評価
/// </summary>
public enum NetworkQuality
{
    Excellent,  // < 30ms
    Good,       // 30-50ms
    Fair,       // 50-100ms
    Poor,       // 100-200ms
    VeryPoor    // > 200ms
}

/// <summary>
/// Ping測定値（内部用）
/// </summary>
internal readonly record struct PingMeasurement
{
    public required DateTime Timestamp { get; init; }
    public required double PingMs { get; init; }
}

/// <summary>
/// プレイヤーのPing管理
/// PlayerID型で型安全に
/// </summary>
public sealed class PlayerPingTracker
{
    private readonly ConcurrentQueue<PingMeasurement> _measurements = new();
    private readonly int _maxSamples;
    private int _totalPacketsSent;
    private int _totalPacketsReceived;

    public PlayerID PlayerId { get; init; }
    public DateTime LastUpdate { get; private set; }

    public PlayerPingTracker(PlayerID playerId, int maxSamples = 50)
    {
        PlayerId = playerId;
        _maxSamples = maxSamples;
        LastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Ping測定値を追加
    /// </summary>
    public void AddMeasurement(double pingMs, bool packetLost = false)
    {
        _totalPacketsSent++;
        
        if (!packetLost)
        {
            _totalPacketsReceived++;
            
            _measurements.Enqueue(new PingMeasurement
            {
                Timestamp = DateTime.UtcNow,
                PingMs = pingMs
            });

            // 最大サンプル数を超えたら古いものを削除
            while (_measurements.Count > _maxSamples)
            {
                _measurements.TryDequeue(out _);
            }
        }

        LastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// 統計情報を計算
    /// </summary>
    public PingStats GetStats()
    {
        var samples = _measurements.ToArray();
        
        if (samples.Length == 0)
        {
            return new PingStats
            {
                AveragePing = 0,
                MinPing = 0,
                MaxPing = 0,
                Jitter = 0,
                SampleCount = 0,
                PacketLoss = 0,
                Quality = NetworkQuality.VeryPoor
            };
        }

        var pings = samples.Select(m => m.PingMs).ToArray();
        var avgPing = pings.Average();
        var minPing = pings.Min();
        var maxPing = pings.Max();
        var jitter = CalculateJitter(pings);
        
        var packetLoss = _totalPacketsSent > 0 
            ? (double)(_totalPacketsSent - _totalPacketsReceived) / _totalPacketsSent * 100 
            : 0;

        return new PingStats
        {
            AveragePing = avgPing,
            MinPing = minPing,
            MaxPing = maxPing,
            Jitter = jitter,
            SampleCount = samples.Length,
            PacketLoss = packetLoss,
            Quality = EvaluateQuality(avgPing, jitter, packetLoss)
        };
    }

    private static double CalculateJitter(double[] pings)
    {
        if (pings.Length < 2) return 0;

        var differences = new List<double>();
        for (int i = 1; i < pings.Length; i++)
        {
            differences.Add(Math.Abs(pings[i] - pings[i - 1]));
        }

        return differences.Average();
    }

    private static NetworkQuality EvaluateQuality(double avgPing, double jitter, double packetLoss)
    {
        if (packetLoss > 5) return NetworkQuality.VeryPoor;
        if (packetLoss > 2) return NetworkQuality.Poor;
        if (jitter > 50) return NetworkQuality.Poor;
        if (jitter > 20) return NetworkQuality.Fair;

        return avgPing switch
        {
            < 30 => NetworkQuality.Excellent,
            < 50 => NetworkQuality.Good,
            < 100 => NetworkQuality.Fair,
            < 200 => NetworkQuality.Poor,
            _ => NetworkQuality.VeryPoor
        };
    }

    public void Reset()
    {
        while (_measurements.TryDequeue(out _)) { }
        _totalPacketsSent = 0;
        _totalPacketsReceived = 0;
        LastUpdate = DateTime.UtcNow;
    }
}

/// <summary>
/// 全プレイヤーのPing管理
/// PlayerID型で型安全に
/// </summary>
public sealed class PingManager
{
    private readonly ConcurrentDictionary<PlayerID, PlayerPingTracker> _playerTrackers = new();
    private readonly int _maxSamplesPerPlayer;

    public int PlayerCount => _playerTrackers.Count;

    public PingManager(int maxSamplesPerPlayer = 50)
    {
        _maxSamplesPerPlayer = maxSamplesPerPlayer;
    }

    public PlayerPingTracker AddPlayer(PlayerID playerId)
    {
        if (playerId == null || playerId.IsNull || playerId.IsEmpty)
            throw new ArgumentException("PlayerID cannot be null or empty", nameof(playerId));

        return _playerTrackers.GetOrAdd(playerId, 
            id => new PlayerPingTracker(id, _maxSamplesPerPlayer));
    }

    public bool RemovePlayer(PlayerID playerId)
    {
        if (playerId == null) return false;
        return _playerTrackers.TryRemove(playerId, out _);
    }

    public PlayerPingTracker? GetTracker(PlayerID playerId)
    {
        if (playerId == null) return null;
        return _playerTrackers.GetValueOrDefault(playerId);
    }

    public void RecordPing(PlayerID playerId, double pingMs, bool packetLost = false)
    {
        if (playerId == null || playerId.IsNull || playerId.IsEmpty) return;

        var tracker = AddPlayer(playerId);
        tracker.AddMeasurement(pingMs, packetLost);
    }

    public Dictionary<PlayerID, PingStats> GetAllStats()
    {
        return _playerTrackers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetStats()
        );
    }

    public int CleanupInactive(TimeSpan timeout)
    {
        var cutoff = DateTime.UtcNow - timeout;
        var removed = 0;

        foreach (var kvp in _playerTrackers)
        {
            if (kvp.Value.LastUpdate < cutoff)
            {
                if (_playerTrackers.TryRemove(kvp.Key, out _))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    public List<(PlayerID PlayerId, PingStats Stats)> GetPoorQualityPlayers(NetworkQuality threshold = NetworkQuality.Poor)
    {
        return _playerTrackers
            .Select(kvp => (PlayerId: kvp.Key, Stats: kvp.Value.GetStats()))
            .Where(x => x.Stats.Quality >= threshold)
            .OrderByDescending(x => x.Stats.AveragePing)
            .ToList();
    }
}
