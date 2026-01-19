using System;
using System.Collections.Concurrent;

namespace OpenGSServer;

/// <summary>
/// プレイヤーのレート制限管理
/// DoS攻撃や悪用を防ぐ
/// </summary>
public sealed class PlayerRateLimiter
{
    private readonly ConcurrentQueue<DateTime> _joinAttempts = new();
    private readonly ConcurrentQueue<DateTime> _chatMessages = new();
    private readonly ConcurrentQueue<DateTime> _roomActions = new();

    private const int MaxJoinAttemptsPerMinute = 5;
    private const int MaxChatMessagesPerMinute = 20;
    private const int MaxRoomActionsPerMinute = 10;

    public bool AllowJoin()
    {
        CleanupOldAttempts(_joinAttempts);
        
        if (_joinAttempts.Count >= MaxJoinAttemptsPerMinute)
            return false;

        _joinAttempts.Enqueue(DateTime.UtcNow);
        return true;
    }

    public bool AllowChat()
    {
        CleanupOldAttempts(_chatMessages);
        
        if (_chatMessages.Count >= MaxChatMessagesPerMinute)
            return false;

        _chatMessages.Enqueue(DateTime.UtcNow);
        return true;
    }

    public bool AllowRoomAction()
    {
        CleanupOldAttempts(_roomActions);
        
        if (_roomActions.Count >= MaxRoomActionsPerMinute)
            return false;

        _roomActions.Enqueue(DateTime.UtcNow);
        return true;
    }

    private void CleanupOldAttempts(ConcurrentQueue<DateTime> queue)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-1);
        
        while (queue.TryPeek(out var oldest) && oldest < cutoff)
        {
            queue.TryDequeue(out _);
        }
    }

    public void Reset()
    {
        _joinAttempts.Clear();
        _chatMessages.Clear();
        _roomActions.Clear();
    }
}

/// <summary>
/// ロビー操作の結果
/// C# 14.0: Result pattern
/// </summary>
public readonly record struct LobbyResult<T>
{
    public T? Value { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSuccess { get; init; }
    public DateTime Timestamp { get; init; }

    public static LobbyResult<T> Success(T value) => new()
    {
        Value = value,
        IsSuccess = true,
        Timestamp = DateTime.UtcNow
    };

    public static LobbyResult<T> Error(string errorMessage) => new()
    {
        ErrorMessage = errorMessage,
        IsSuccess = false,
        Timestamp = DateTime.UtcNow
    };

    public void Match(Action<T> onSuccess, Action<string> onError)
    {
        if (IsSuccess && Value is not null)
            onSuccess(Value);
        else if (ErrorMessage is not null)
            onError(ErrorMessage);
    }
}
