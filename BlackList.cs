using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace OpenGSServer
{
    public sealed class BlackList
    {
        private static readonly Lazy<BlackList> _instance = new(() => new BlackList());

        private readonly HashSet<string> _bannedIpAddresses = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncRoot = new();

        private BlackList()
        {
        }

        public static BlackList Instance => _instance.Value;

        public bool AddIp(string address)
        {
            if (!TryNormalizeIp(address, out var normalized))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _bannedIpAddresses.Add(normalized);
            }
        }

        public bool RemoveIp(string address)
        {
            if (!TryNormalizeIp(address, out var normalized))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _bannedIpAddresses.Remove(normalized);
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _bannedIpAddresses.Clear();
            }
        }

        public bool IsBlocked(string address)
        {
            if (!TryNormalizeIp(address, out var normalized))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _bannedIpAddresses.Contains(normalized);
            }
        }

        public IReadOnlyCollection<string> GetEntries()
        {
            lock (_syncRoot)
            {
                return _bannedIpAddresses.ToArray();
            }
        }

        private static bool TryNormalizeIp(string address, out string normalized)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            if (IPAddress.TryParse(address, out var parsed))
            {
                normalized = parsed.ToString();
                return true;
            }

            return false;
        }
    }
}
