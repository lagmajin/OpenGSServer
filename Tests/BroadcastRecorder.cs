using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Captures what the authoritative match path broadcasts.
/// <para>
/// The rulings the server makes go out through <see cref="GameMessageDispatcher"/>,
/// which resolves to the lobby session. A test has no lobby session, so the
/// broadcasts would go nowhere and the ruling could not be observed at all. That
/// is not a test convenience: it is the same reason a client has to be looking
/// on the right channel to see one, so what is asserted here is which name a
/// ruling is sent under and that it is sent at all.
/// </para>
/// </summary>
public sealed class BroadcastRecorder : IGameMessageSender, IDisposable
{
    private readonly List<JObject> broadcasts = new();
    private readonly List<DirectedMessage> sentToPlayer = new();

    public IReadOnlyList<JObject> Broadcasts => broadcasts;

    public void SendToPlayer(string playerId, JObject message) =>
        sentToPlayer.Add(new DirectedMessage(playerId, message));

    public void BroadcastToRoom(string roomId, JObject message) => broadcasts.Add(message);

    public void BroadcastToAll(JObject message) => broadcasts.Add(message);

    /// <summary>
    /// Takes the dispatcher over for the duration of one action, then hands it
    /// back. The dispatcher is process wide, so a test that kept it would
    /// silence the ruling for every other test on the lane.
    /// </summary>
    public static void During(Action action)
    {
        var recorder = new BroadcastRecorder();
        GameMessageDispatcher.Initialize(recorder);
        try
        {
            action();
        }
        finally
        {
            GameMessageDispatcher.Initialize(MatchServerV2.Instance);
            recorder.Dispose();
        }
    }

    /// <summary>
    /// The rulings of one type that were sent, in the order they were sent.
    /// </summary>
    public IReadOnlyList<JObject> RulingsOfType(string messageType)
    {
        return broadcasts
            .Where(message => string.Equals(
                message["MessageType"]?.ToString(),
                messageType,
                StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// The single ruling of a type, failing when there was not exactly one.
    /// </summary>
    public JObject TheRuling(string messageType)
    {
        return Assert.Single(RulingsOfType(messageType));
    }

    /// <summary>
    /// The single message sent to one player rather than to a room, of a type.
    /// </summary>
    public JObject TheAnswerTo(string playerId, string messageType)
    {
        return Assert.Single(SentTo(playerId, messageType));
    }

    /// <summary>
    /// What was sent to one player, of one type.
    /// </summary>
    public IReadOnlyList<JObject> SentTo(string playerId, string messageType)
    {
        return sentToPlayer
            .Where(entry => string.Equals(entry.PlayerId, playerId, StringComparison.Ordinal) &&
                string.Equals(entry.Message["MessageType"]?.ToString(), messageType, StringComparison.Ordinal))
            .Select(entry => entry.Message)
            .ToList();
    }

    /// <summary>
    /// The player a message was addressed to, kept alongside it because a
    /// directed answer and a broadcast are different facts about the same
    /// payload.
    /// </summary>
    public readonly record struct DirectedMessage(string PlayerId, JObject Message);

    public void Dispose()
    {
        broadcasts.Clear();
        sentToPlayer.Clear();
    }
}
