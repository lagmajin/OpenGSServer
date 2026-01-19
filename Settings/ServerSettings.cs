using System;
using System.Collections.Generic;
using System.Linq;
using OpenGSCore; // OpenGSCore.eGameModeを使用
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class ServerSettings
    {
        private readonly List<eGameMode> _allowGameModes = new();

        public int MaxRoom { get; private set; } = 32;
        public int MaxUser { get; private set; } = 200;
        public int TickRate { get; private set; } = 60;
        public bool CanRegisterAccounts { get; private set; } = true;

        public IReadOnlyList<eGameMode> AllowGameModes => _allowGameModes;

        public ServerSettings()
        {
            // allow the most common modes by default
            _allowGameModes.AddRange(new[]
            {
                eGameMode.DeathMatch,
                eGameMode.TeamDeathMatch,
                eGameMode.Survival,
                eGameMode.TeamSurvival,
                eGameMode.CTF
            });
        }

        public void SetMaxRoom(int value) => MaxRoom = Math.Max(1, value);

        public void SetMaxUser(int value) => MaxUser = Math.Max(1, value);

        public void SetTickRate(int value) => TickRate = Math.Max(1, value);

        public void SetCanRegisterAccounts(bool value) => CanRegisterAccounts = value;

        public void SetAllowedGameModes(IEnumerable<eGameMode> modes)
        {
            if (modes == null)
            {
                return;
            }

            _allowGameModes.Clear();

            foreach (var mode in modes.Where(m => m != eGameMode.Unknown))
            {
                if (!_allowGameModes.Contains(mode))
                {
                    _allowGameModes.Add(mode);
                }
            }
        }

        public JObject ToJson()
        {
            var result = new JObject
            {
                ["MaxRoom"] = MaxRoom,
                ["MaxUser"] = MaxUser,
                ["TickRate"] = TickRate,
                ["AllowRegisterAccount"] = CanRegisterAccounts,
                ["AllowGameModes"] = new JArray(_allowGameModes.Select(m => m.ToString()))
            };

            return result;
        }

        public void SetFromJson(JObject json)
        {
            if (json == null)
            {
                return;
            }

            MaxRoom = ReadInt(json, "MaxRoom", MaxRoom, 1);
            MaxUser = ReadInt(json, "MaxUser", MaxUser, 1);
            TickRate = ReadInt(json, "TickRate", TickRate, 1);
            CanRegisterAccounts = ReadBool(json, "AllowRegisterAccount", CanRegisterAccounts);

            if (json.TryGetValue("AllowGameModes", out var modesToken))
            {
                var parsedModes = ParseModes(modesToken);

                if (parsedModes.Count > 0)
                {
                    SetAllowedGameModes(parsedModes);
                }
            }
        }

        private static int ReadInt(JObject json, string propertyName, int fallback, int minimum)
        {
            if (!json.TryGetValue(propertyName, out var token))
            {
                return fallback;
            }

            if (token.Type == JTokenType.Integer)
            {
                return Math.Max(minimum, token.Value<int>());
            }

            return int.TryParse(token.ToString(), out var value)
                ? Math.Max(minimum, value)
                : fallback;
        }

        private static bool ReadBool(JObject json, string propertyName, bool fallback)
        {
            if (!json.TryGetValue(propertyName, out var token))
            {
                return fallback;
            }

            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>() != 0;
            }

            return bool.TryParse(token.ToString(), out var value) ? value : fallback;
        }

        private static List<eGameMode> ParseModes(JToken token)
        {
            var result = new List<eGameMode>();

            void TryAdd(string value)
            {
                if (Enum.TryParse(value, true, out eGameMode mode) && mode != eGameMode.Unknown)
                {
                    result.Add(mode);
                }
            }

            if (token is JArray jArray)
            {
                foreach (var entry in jArray)
                {
                    TryAdd(entry.ToString());
                }
            }
            else
            {
                TryAdd(token.ToString());
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is not ServerSettings other)
            {
                return false;
            }

            return MaxRoom == other.MaxRoom &&
                   MaxUser == other.MaxUser &&
                   TickRate == other.TickRate &&
                   CanRegisterAccounts == other.CanRegisterAccounts &&
                   _allowGameModes.SequenceEqual(other._allowGameModes);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(MaxRoom);
            hash.Add(MaxUser);
            hash.Add(TickRate);
            hash.Add(CanRegisterAccounts);
            foreach (var mode in _allowGameModes)
            {
                hash.Add(mode);
            }

            return hash.ToHashCode();
        }

        public static bool operator ==(ServerSettings a, ServerSettings b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(ServerSettings a, ServerSettings b)
        {
            return !(a == b);
        }

    }



}
