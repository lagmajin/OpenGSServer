using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace OpenGSServer
{
    /// <summary>
    /// Watches rooms that are waiting for their players to finish loading and
    /// releases them when the wait is hopeless.
    /// <para>
    /// S3 asks for the loading start, progress, completion, and map entry
    /// approval to be handled by the server path, with deterministic timeout and
    /// fallback behaviour. LoadingStarted and LoadingCompleted were already
    /// handled, and AllowEnterMap is correctly withheld until every player
    /// reports in. What was missing was the other half: if one client never
    /// reported LoadingCompleted, the room stayed locked forever. There was no
    /// timer anywhere in the loading path, so a dropped or hung client left the
    /// whole room unable to start a match.
    /// </para>
    /// </summary>
    internal sealed class LoadingTimeoutMonitor : IDisposable
    {
        private const int DefaultTimeoutSeconds = 60;

        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

        private readonly Func<IEnumerable<WaitRoom>>[] roomSources;
        private readonly Func<string, bool> roomIsWaiting;
        private readonly Action<WaitRoom, IReadOnlyList<string>> onTimeout;
        private readonly TimeSpan timeout;

        private readonly Dictionary<string, DateTime> startedAtUtc = new(StringComparer.OrdinalIgnoreCase);
        private readonly object gate = new();

        private Timer? timer;
        private int disposed;

        public LoadingTimeoutMonitor(
            TimeSpan timeout,
            Func<IEnumerable<WaitRoom>>[] roomSources,
            Func<string, bool> roomIsWaiting,
            Action<WaitRoom, IReadOnlyList<string>> onTimeout)
        {
            this.timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(DefaultTimeoutSeconds) : timeout;
            this.roomSources = roomSources;
            this.roomIsWaiting = roomIsWaiting;
            this.onTimeout = onTimeout;
        }

        public void Start()
        {
            if (Interlocked.Exchange(ref disposed, 0) != 0)
            {
                throw new ObjectDisposedException(nameof(LoadingTimeoutMonitor));
            }

            timer ??= new Timer(_ => Sweep(), null, CheckInterval, CheckInterval);
        }

        /// <summary>
        /// Records that a room entered the loading state, so the sweep knows
        /// when its deadline is.
        /// </summary>
        public void TrackLoadingStarted(string? roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            lock (gate)
            {
                startedAtUtc[roomId] = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Stops tracking a room. Called when loading completes, when the room
        /// closes, or when a player leaves.
        /// </summary>
        public void Forget(string? roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            lock (gate)
            {
                startedAtUtc.Remove(roomId);
            }
        }

        private void Sweep()
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            try
            {
                foreach (var room in roomSources.SelectMany(source => source() ?? Enumerable.Empty<WaitRoom>()))
                {
                    TryHandleRoom(room);
                }
            }
            catch (Exception ex)
            {
                // The timer callback must never throw, or the sweep stops.
                ConsoleWrite.WriteMessage($"[Loading] sweep failed: {ex.Message}", ConsoleColor.Red);
            }
        }

        private void TryHandleRoom(WaitRoom room)
        {
            var roomId = room.RoomId;
            if (string.IsNullOrWhiteSpace(roomId) || !roomIsWaiting(roomId))
            {
                Forget(roomId);
                return;
            }

            lock (gate)
            {
                if (!startedAtUtc.TryGetValue(roomId, out var since) ||
                    DateTime.UtcNow - since < timeout)
                {
                    // Not tracked yet means the room is in the loading state but
                    // this monitor has not seen the start, so start the clock.
                    startedAtUtc[roomId] = since == default ? DateTime.UtcNow : since;
                    return;
                }
            }

            // The room is past its deadline. Report the players still missing,
            // then stop tracking so a failed handler cannot spin.
            var pending = room.AllPlayers()
                .Select(player => player.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            Forget(roomId);

            if (pending.Count == 0)
            {
                return;
            }

            ConsoleWrite.WriteMessage(
                $"[Loading] room {roomId} timed out after {timeout.TotalSeconds:0}s; " +
                $"{pending.Count} player(s) did not report completion: {string.Join(", ", pending)}",
                ConsoleColor.Yellow);

            try
            {
                onTimeout(room, pending);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Loading] timeout handler failed for {roomId}: {ex.Message}", ConsoleColor.Red);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            timer?.Dispose();
            timer = null;
        }
    }
}