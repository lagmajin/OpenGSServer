using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace OpenGSServer.Network
{
    /// <summary>
    /// Server-owned grenade slots for clients that use the native loadout contract.
    /// Legacy clients that do not register a loadout keep their existing throw path.
    /// </summary>
    public static class ServerGrenadeLoadoutStore
    {
        private sealed class Loadout
        {
            public readonly object Sync = new();
            public string[] Slots = { string.Empty, string.Empty, string.Empty };
            public bool Initialized;
        }

        private static readonly ConcurrentDictionary<string, Loadout> Loadouts =
            new(StringComparer.OrdinalIgnoreCase);

        private static string Key(string roomId, string playerId) => roomId + "\n" + playerId;

        public static string[] InitializeOrRead(
            string roomId, string playerId, IReadOnlyList<string> initialSlots)
        {
            var loadout = Loadouts.GetOrAdd(Key(roomId, playerId), _ => new Loadout());
            lock (loadout.Sync)
            {
                if (!loadout.Initialized && initialSlots.Count == 3)
                {
                    loadout.Slots = initialSlots.ToArray();
                    loadout.Initialized = true;
                }
                return (string[])loadout.Slots.Clone();
            }
        }

        public static bool TryRead(string roomId, string playerId, out string[] slots)
        {
            slots = Array.Empty<string>();
            if (!Loadouts.TryGetValue(Key(roomId, playerId), out var loadout))
                return false;
            lock (loadout.Sync)
                slots = (string[])loadout.Slots.Clone();
            return true;
        }

        public static bool TryAdd(
            string roomId, string playerId, string grenadeType, out string[] slots)
        {
            var loadout = Loadouts.GetOrAdd(Key(roomId, playerId), _ => new Loadout());
            lock (loadout.Sync)
            {
                var empty = Array.FindIndex(loadout.Slots, string.IsNullOrEmpty);
                if (empty < 0)
                {
                    slots = (string[])loadout.Slots.Clone();
                    return false;
                }
                loadout.Slots[empty] = grenadeType;
                loadout.Initialized = true;
                slots = (string[])loadout.Slots.Clone();
                return true;
            }
        }

        /// <summary>
        /// Consumes one matching slot. Unregistered legacy clients remain on their
        /// pre-existing server contract and are reported as untracked.
        /// </summary>
        public static bool TryConsume(
            string roomId, string playerId, string grenadeType,
            out bool tracked, out string[] slots)
        {
            tracked = false;
            slots = Array.Empty<string>();
            if (!Loadouts.TryGetValue(Key(roomId, playerId), out var loadout))
                return true;

            lock (loadout.Sync)
            {
                tracked = loadout.Initialized;
                if (!tracked)
                    return true;
                var slot = Array.FindIndex(loadout.Slots, value =>
                    string.Equals(value, grenadeType, StringComparison.OrdinalIgnoreCase));
                if (slot < 0)
                {
                    slots = (string[])loadout.Slots.Clone();
                    return false;
                }
                loadout.Slots[slot] = string.Empty;
                slots = (string[])loadout.Slots.Clone();
                return true;
            }
        }

        public static void ClearPlayer(string playerId)
        {
            var suffix = "\n" + playerId;
            foreach (var key in Loadouts.Keys.Where(key =>
                key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).ToArray())
                Loadouts.TryRemove(key, out _);
        }

        public static void ClearRoom(string roomId)
        {
            var prefix = roomId + "\n";
            foreach (var key in Loadouts.Keys.Where(key =>
                key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                Loadouts.TryRemove(key, out _);
        }
    }
}
