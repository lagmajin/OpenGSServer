using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

#nullable enable

namespace OpenGSServer.Network
{
    /// <summary>
    /// How one class of weapon behaves: how many rounds a magazine holds, how long
    /// a reload takes, how long between rounds, and how much reserve ammo there
    /// is behind the magazine.
    /// </summary>
    public readonly struct MagazineSpec
    {
        public MagazineSpec(string weaponType, int capacity, int reserve, float reloadSeconds, float intervalSeconds)
        {
            WeaponType = weaponType;
            Capacity = capacity;
            Reserve = reserve;
            ReloadSeconds = reloadSeconds;
            IntervalSeconds = intervalSeconds;
        }

        public string WeaponType { get; }

        public int Capacity { get; }

        /// <summary>Rounds carried outside the magazine. A reload draws on this.</summary>
        public int Reserve { get; }

        public float ReloadSeconds { get; }

        /// <summary>Minimum time between two rounds of this weapon.</summary>
        public float IntervalSeconds { get; }
    }

    /// <summary>
    /// What the server knows about one weapon in one player's hands.
    /// </summary>
    public sealed class ServerMagazine
    {
        private static readonly object Sync = new();

        public ServerMagazine(string weaponType, MagazineSpec spec)
        {
            WeaponType = weaponType;
            Capacity = spec.Capacity;
            Reserve = spec.Reserve;
            MaxReserve = spec.Reserve;
            ReloadSeconds = spec.ReloadSeconds;
            IntervalSeconds = spec.IntervalSeconds;
            Remaining = spec.Capacity;
            NextAllowedShotUtc = DateTime.MinValue;
        }

        public string WeaponType { get; }
        public int Capacity { get; }
        public int Reserve { get; set; }
        public int MaxReserve { get; }
        public float ReloadSeconds { get; }
        public float IntervalSeconds { get; }

        public int Remaining { get; private set; }

        /// <summary>When the magazine may next be fired. Held through a reload.</summary>
        public DateTime NextAllowedShotUtc { get; private set; }

        /// <summary>While a reload is running, the moment it finishes.</summary>
        public DateTime ReloadCompletesUtc { get; private set; }

        public bool IsReloading => ReloadCompletesUtc > DateTime.MinValue;

        public int ReadRemaining()
        {
            lock (Sync)
            {
                return Remaining;
            }
        }

        public int ReadReserve()
        {
            lock (Sync)
            {
                return Reserve;
            }
        }

        public bool ReadIsReloading()
        {
            lock (Sync)
            {
                return IsReloading;
            }
        }

        /// <summary>
        /// Takes one round out of the magazine.
        /// <para>
        /// The whole of the check and the decrement happen under one lock, because a
        /// client can put several shots into the network in the same tick and two
        /// of them reading the same count before either writes it is how a
        /// ten round magazine fires eleven.
        /// </para>
        /// </summary>
        public bool TryConsume(DateTime nowUtc, out int remainingAfter)
        {
            lock (Sync)
            {
                remainingAfter = Remaining;
                if (IsReloading || Remaining <= 0 || nowUtc < NextAllowedShotUtc)
                {
                    return false;
                }

                Remaining--;
                remainingAfter = Remaining;
                NextAllowedShotUtc = nowUtc.AddSeconds(IntervalSeconds);
                return true;
            }
        }

        /// <summary>
        /// Starts a reload if one is worth starting, and refuses otherwise.
        /// <para>
        /// A reload with a full magazine, or with nothing in reserve, is refused
        /// rather than started, so a client cannot sit in a permanent reload or
        /// burn a reload animation per trigger pull.
        /// </para>
        /// </summary>
        public ReloadResult TryBeginReload(DateTime nowUtc, TimeSpan requestWindow)
        {
            lock (Sync)
            {
                if (Remaining >= Capacity)
                {
                    return new ReloadResult(ReloadResultKind.AlreadyFull, Remaining, Reserve, 0f);
                }

                if (Reserve <= 0)
                {
                    return new ReloadResult(ReloadResultKind.NoReserve, Remaining, Reserve, 0f);
                }

                if (IsReloading)
                {
                    // A second request inside the first reload is the same reload.
                    var inFlight = (float)(ReloadCompletesUtc - nowUtc).TotalSeconds;
                    return new ReloadResult(ReloadResultKind.InProgress, Remaining, Reserve, inFlight);
                }

                if (nowUtc < NextAllowedShotUtc)
                {
                    var wait = (float)(NextAllowedShotUtc - nowUtc).TotalSeconds;
                    return new ReloadResult(ReloadResultKind.TooSoon, Remaining, Reserve, wait);
                }

                // The client's own duration is honoured only when it is longer than the
                // weapon's. Taking the shorter of the two would let a client ask
                // for a tenth of a second and come out of it ready to fire, which
                // is the reload turned back into the rate limit it replaced.
                var requested = requestWindow > TimeSpan.Zero
                    ? (float)requestWindow.TotalSeconds
                    : ReloadSeconds;
                var duration = Math.Max(0.1f, Math.Max(ReloadSeconds, requested));
                ReloadCompletesUtc = nowUtc.AddSeconds(duration);
                return new ReloadResult(ReloadResultKind.Started, Remaining, Reserve, duration);
            }
        }

        /// <summary>
        /// Finishes a reload that is due, moving rounds from the reserve into the
        /// magazine.
        /// </summary>
        public bool TryFinishReload(DateTime nowUtc, out int loaded)
        {
            lock (Sync)
            {
                loaded = 0;
                if (!IsReloading || nowUtc < ReloadCompletesUtc)
                {
                    return false;
                }

                ReloadCompletesUtc = DateTime.MinValue;
                var wanted = Capacity - Remaining;
                loaded = Math.Min(wanted, Reserve);
                if (loaded <= 0)
                {
                    return false;
                }

                Remaining += loaded;
                Reserve -= loaded;
                // A reload is a pause, not a licence to shoot through it.
                NextAllowedShotUtc = nowUtc;
                return true;
            }
        }

        /// <summary>Refills the reserve. This is what an ammo item is for.</summary>
        public int RefillReserve(int amount)
        {
            lock (Sync)
            {
                var before = Reserve;
                Reserve = Math.Min(MaxReserve, Reserve + Math.Max(0, amount));
                return Reserve - before;
            }
        }

        /// <summary>Puts the magazine back to how it was found, for a respawn.</summary>
        public void ResetToFull()
        {
            lock (Sync)
            {
                Remaining = Capacity;
                Reserve = MaxReserve;
                ReloadCompletesUtc = DateTime.MinValue;
                NextAllowedShotUtc = DateTime.MinValue;
            }
        }
    }

    public enum ReloadResultKind
    {
        Started,
        InProgress,
        AlreadyFull,
        NoReserve,
        TooSoon
    }

    public readonly struct ReloadResult
    {
        public ReloadResult(ReloadResultKind kind, int remaining, int reserve, float seconds)
        {
            Kind = kind;
            Remaining = remaining;
            Reserve = reserve;
            Seconds = seconds;
        }

        public ReloadResultKind Kind { get; }
        public int Remaining { get; }
        public int Reserve { get; }

        /// <summary>How long the reload takes, or how long the caller must wait.</summary>
        public float Seconds { get; }

        public bool Accepted => Kind == ReloadResultKind.Started;
    }

    /// <summary>
    /// Server owned magazines, one per player per weapon class.
    /// <para>
    /// The server used to hold no magazine at all: a shot was rate limited and
    /// that was the whole of the economy, so a player could fire forever and the
    /// ammo items the client spent did nothing. The magazine lives here so that
    /// what a client may fire is the server's answer and not the client's own.
    /// </para>
    /// </summary>
    public static class ServerWeaponMagazineStore
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ServerMagazine>> Magazines = new();

        /// <summary>
        /// The weapon table, in the same normalized names the shot handler uses.
        /// </summary>
        private static readonly Dictionary<string, MagazineSpec> Specs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Pistol"] = new MagazineSpec("Pistol", 12, 60, 1.4f, 0.20f),
            ["SMG"] = new MagazineSpec("SMG", 30, 180, 2.0f, 0.08f),
            ["Shotgun"] = new MagazineSpec("Shotgun", 6, 36, 2.6f, 0.80f),
            ["Rifle"] = new MagazineSpec("Rifle", 30, 180, 2.2f, 0.15f),
            ["Sniper"] = new MagazineSpec("Sniper", 5, 35, 2.8f, 1.00f),
            ["RocketLauncher"] = new MagazineSpec("RocketLauncher", 10, 0, 0f, 0.1f),
            ["FlameThrower"] = new MagazineSpec("FlameThrower", 100, 0, 0f, 0.1f),
            ["GrenadeLauncher"] = new MagazineSpec("GrenadeLauncher", 10, 0, 0f, 0.1f)
        };

        public static bool IsKnownWeapon(string? weaponType)
        {
            return weaponType != null && Specs.ContainsKey(weaponType);
        }

        public static MagazineSpec Spec(string weaponType)
        {
            if (Specs.TryGetValue(weaponType, out var spec))
            {
                return spec;
            }

            return new MagazineSpec(weaponType, 30, 180, 2.0f, 0.20f);
        }

        /// <summary>
        /// Finishes any reload that is due. Called from the match loop, because a
        /// reload finishing is a state that becomes true on a timer rather than
        /// an event anybody reports, and the client is the one waiting on it.
        /// </summary>
        public static void TickReloads(DateTime nowUtc, Action<string, ServerMagazine>? onCompleted)
        {
            foreach (var entry in Magazines)
            {
                foreach (var magazine in entry.Value.Values)
                {
                    if (magazine.TryFinishReload(nowUtc, out _))
                    {
                        onCompleted?.Invoke(entry.Key, magazine);
                    }
                }
            }
        }

        public static ServerMagazine GetOrCreate(string playerId, string weaponType)
        {
            var byWeapon = Magazines.GetOrAdd(
                playerId,
                _ => new ConcurrentDictionary<string, ServerMagazine>(StringComparer.OrdinalIgnoreCase));
            return byWeapon.GetOrAdd(weaponType, type => new ServerMagazine(type, Spec(type)));
        }

        public static bool TryPeek(string playerId, string weaponType, out ServerMagazine magazine)
        {
            magazine = null!;
            return Magazines.TryGetValue(playerId, out var byWeapon) &&
                   byWeapon.TryGetValue(weaponType, out magazine!);
        }

        /// <summary>Whether a player has a magazine for this weapon, and how much is in it.</summary>
        public static bool TryReadRemaining(string playerId, string weaponType, out int remaining, out int capacity)
        {
            if (TryPeek(playerId, weaponType, out var magazine))
            {
                remaining = magazine.ReadRemaining();
                capacity = magazine.Capacity;
                return true;
            }

            remaining = 0;
            capacity = 0;
            return false;
        }

        /// <summary>
        /// Takes one round, unless the magazine is empty, reloading, or still
        /// cooling down from the last round.
        /// </summary>
        public static bool TryFire(string playerId, string weaponType, DateTime nowUtc, out int remainingAfter)
        {
            if (!IsKnownWeapon(weaponType))
            {
                remainingAfter = 0;
                return false;
            }

            return GetOrCreate(playerId, weaponType).TryConsume(nowUtc, out remainingAfter);
        }

        /// <summary>
        /// Begins a reload. The window the client asked for is honoured only up to
        /// the weapon's own reload time, so a client cannot shorten the reload into
        /// a rate limit bypass.
        /// </summary>
        public static ReloadResult TryReload(string playerId, string weaponType, DateTime nowUtc, TimeSpan requestWindow)
        {
            if (!IsKnownWeapon(weaponType))
            {
                return new ReloadResult(ReloadResultKind.NoReserve, 0, 0, 0f);
            }

            return GetOrCreate(playerId, weaponType).TryBeginReload(nowUtc, requestWindow);
        }

        /// <summary>Refills reserve ammo across every weapon a player holds.</summary>
        public static int RefillAllReserve(string playerId, int amount)
        {
            if (!Magazines.TryGetValue(playerId, out var byWeapon))
            {
                return 0;
            }

            var total = 0;
            foreach (var magazine in byWeapon.Values)
            {
                total += magazine.RefillReserve(amount);
            }

            return total;
        }

        public static void ResetPlayer(string playerId)
        {
            if (Magazines.TryRemove(playerId, out var byWeapon))
            {
                foreach (var magazine in byWeapon.Values)
                {
                    magazine.ResetToFull();
                }
            }
        }

        /// <summary>
        /// Puts every weapon a player holds back to a full magazine. A respawn or a
        /// match start uses this, so coming back is not a return to a half spent
        /// gun from the previous life.
        /// </summary>
        public static void RefillAllMagazines(string playerId)
        {
            if (!Magazines.TryGetValue(playerId, out var byWeapon))
            {
                return;
            }

            foreach (var magazine in byWeapon.Values)
            {
                magazine.ResetToFull();
            }
        }

        public static void ClearRoomPlayers(IEnumerable<string> playerIds)
        {
            foreach (var playerId in playerIds)
            {
                Magazines.TryRemove(playerId, out _);
            }
        }
    }
}
