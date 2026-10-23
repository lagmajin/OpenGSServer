using System;
using System.Collections.Generic;
using System.Linq;
using OpenGSCore; // OpenGSCore.eGameModeを使用
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class ServerSettings
    {
        private readonly List<EGameMode> _allowGameModes = new();
        private readonly List<ServerRespawnPoint> _respawnPoints = new();

        public int MaxRoom { get; private set; } = 32;
        public int MaxUser { get; private set; } = 200;
        public int TickRate { get; private set; } = 60;
        public float RespawnDelaySeconds { get; private set; } = 5f;
        public bool CanRegisterAccounts { get; private set; } = true;

        public IReadOnlyList<EGameMode> AllowGameModes => _allowGameModes;
        public IReadOnlyList<ServerRespawnPoint> RespawnPoints => _respawnPoints;

        public ServerSettings()
        {
            // allow the most common modes by default
            _allowGameModes.AddRange(new[]
            {
                EGameMode.DeathMatch,
                EGameMode.TeamDeathMatch,
                EGameMode.Survival,
                EGameMode.TeamSurvival,
                EGameMode.CaptureTheFlag
            });

            // A respawn that does not move the player is not a respawn.
            //
            // The handler falls back to the position the player died at when no
            // table is configured, and the class default used to configure none,
            // so on a server with no settings file everybody came back exactly
            // where they fell: a free spawn with no risk of being shot coming out
            // of it. These defaults are rough, symmetric and separated, so a team
            // respawns into its own half rather than into its own grave. A settings
            // file replaces them.
            _respawnPoints.AddRange(new[]
            {
                new ServerRespawnPoint(ETeam.NoTeam, -6f, 0f, 0f),
                new ServerRespawnPoint(ETeam.NoTeam, 6f, 0f, 0f),
                new ServerRespawnPoint(ETeam.NoTeam, 0f, 0f, -6f),
                new ServerRespawnPoint(ETeam.NoTeam, 0f, 0f, 6f),
                new ServerRespawnPoint(ETeam.Red, -12f, 0f, -12f),
                new ServerRespawnPoint(ETeam.Red, -12f, 0f, 12f),
                new ServerRespawnPoint(ETeam.Blue, 12f, 0f, -12f),
                new ServerRespawnPoint(ETeam.Blue, 12f, 0f, 12f)
            });
        }

        public void SetMaxRoom(int value) => MaxRoom = Math.Max(1, value);

        public void SetMaxUser(int value) => MaxUser = Math.Max(1, value);

        public void SetTickRate(int value) => TickRate = Math.Max(1, value);
        public void SetRespawnDelaySeconds(float value) =>
            RespawnDelaySeconds = float.IsFinite(value) ? Math.Clamp(value, 0f, 60f) : 5f;

        public void SetCanRegisterAccounts(bool value) => CanRegisterAccounts = value;

        public void SetAllowedGameModes(IEnumerable<EGameMode> modes)
        {
            if (modes == null)
            {
                return;
            }

            _allowGameModes.Clear();

            foreach (var mode in modes.Where(m => m != EGameMode.Unknown))
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
                ["RespawnDelaySeconds"] = RespawnDelaySeconds,
                ["AllowRegisterAccount"] = CanRegisterAccounts,
                ["AllowGameModes"] = new JArray(_allowGameModes.Select(m => m.ToString()))
            };

            result["RespawnPoints"] = new JArray(_respawnPoints.Select(point => point.ToJson()));

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
            if (json["RespawnDelaySeconds"]?.Type == JTokenType.Float ||
                json["RespawnDelaySeconds"]?.Type == JTokenType.Integer)
                SetRespawnDelaySeconds(json["RespawnDelaySeconds"]!.Value<float>());
            CanRegisterAccounts = ReadBool(json, "AllowRegisterAccount", CanRegisterAccounts);

            if (json.TryGetValue("AllowGameModes", out var modesToken))
            {
                var parsedModes = ParseModes(modesToken);

                if (parsedModes.Count > 0)
                {
                    SetAllowedGameModes(parsedModes);
                }
            }

            if (json["RespawnPoints"] is JArray respawnPoints)
            {
                _respawnPoints.Clear();
                foreach (var token in respawnPoints.OfType<JObject>())
                {
                    if (ServerRespawnPoint.TryParse(token, out var point))
                    {
                        _respawnPoints.Add(point);
                    }
                }
            }
        }

public bool TryGetRespawnPoint(ETeam team, int slot, out ServerRespawnPoint point)
        {
            var candidates = CandidatesFor(team);
            if (candidates.Count == 0)
            {
                point = null!;
                return false;
            }

            point = candidates[Math.Abs(slot) % candidates.Count];
            return true;
        }

        /// <summary>
        /// The spawn points a player on this team may come back at.
        /// <para>
        /// A team's own points come first, then the neutral ones, then anything at
        /// all. Matching on the team alone meant a team the settings file had no
        /// points for had none to choose from even though the map was covered in
        /// neutral ones, so a red player who had no red spawn came back where the
        /// server already had them rather than on a spawn at all.
        /// </para>
        /// </summary>
        private List<ServerRespawnPoint> CandidatesFor(ETeam team)
        {
            var own = _respawnPoints.Where(candidate => candidate.Team == team).ToList();
            if (own.Count > 0)
            {
                return own;
            }

            var neutral = _respawnPoints
                .Where(candidate => candidate.Team == ETeam.NoTeam)
                .ToList();
            return neutral.Count > 0 ? neutral : _respawnPoints.ToList();
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

        private static List<EGameMode> ParseModes(JToken token)
        {
            var result = new List<EGameMode>();

            void TryAdd(string value)
            {
                if (Enum.TryParse(value, true, out EGameMode mode) && mode != EGameMode.Unknown)
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
                   RespawnDelaySeconds.Equals(other.RespawnDelaySeconds) &&
                   CanRegisterAccounts == other.CanRegisterAccounts &&
                   _allowGameModes.SequenceEqual(other._allowGameModes);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(MaxRoom);
            hash.Add(MaxUser);
            hash.Add(TickRate);
            hash.Add(RespawnDelaySeconds);
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

public sealed class ServerRespawnPoint
{
    public ETeam Team { get; init; } = ETeam.NoTeam;
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    public ServerRespawnPoint()
    {
    }

    public ServerRespawnPoint(ETeam team, float x, float y, float z)
    {
        Team = team;
        X = x;
        Y = y;
        Z = z;
    }

    public JObject ToJson() => new()
    {
        ["Team"] = Team.ToString(),
        ["X"] = X,
        ["Y"] = Y,
        ["Z"] = Z
    };

    public static bool TryParse(JObject json, out ServerRespawnPoint point)
    {
        point = null!;

        // A neutral point is a real kind of point. It was rejected here, so a
        // settings file could only ever describe team spawns, and the neutral
        // defaults below were the only ones that could never be configured,
        // replaced, or reasoned about.
        if (!Enum.TryParse(json["Team"]?.ToString(), true, out ETeam team) ||
            !Enum.IsDefined(typeof(ETeam), team))
        {
            return false;
        }

        if (!float.TryParse(json["X"]?.ToString(), out var x) ||
            !float.TryParse(json["Y"]?.ToString(), out var y) ||
            !float.TryParse(json["Z"]?.ToString(), out var z) ||
            float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
            float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z))
        {
            return false;
        }

        point = new ServerRespawnPoint { Team = team, X = x, Y = y, Z = z };
        return true;
    }
}
