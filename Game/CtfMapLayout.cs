using System.Numerics;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// CTF positions recovered from the original MPF files and packaged native
    /// map data. Red starts at the horizontal mirror of the Blue-side spawn on
    /// these symmetric maps.
    /// </summary>
    internal static class CtfMapLayout
    {
        public static bool TryGetFlagStand(EMap map, ETeam team, out Vector3 position)
        {
            position = (map, team) switch
            {
                (EMap.BattlePortCTF, ETeam.Red) => new Vector3(-13.435f, 0.865f, 0f),
                (EMap.BattlePortCTF, ETeam.Blue) => new Vector3(13.115f, 0.945f, 0f),
                (EMap.TheParkCTF, ETeam.Red) => new Vector3(-9.725f, -1.365f, 0f),
                (EMap.TheParkCTF, ETeam.Blue) => new Vector3(9.185f, -1.405f, 0f),
                (EMap.SkyHighCTF, ETeam.Red) => new Vector3(-2.755f, -4.245f, 0f),
                (EMap.SkyHighCTF, ETeam.Blue) => new Vector3(2.795f, -4.235f, 0f),
                _ => default
            };
            return (map is EMap.BattlePortCTF or EMap.TheParkCTF or EMap.SkyHighCTF) &&
                (team is ETeam.Red or ETeam.Blue);
        }

        public static bool TryGetTeamSpawn(EMap map, ETeam team, out Vector3 position)
        {
            var sourceSpawn = map switch
            {
                EMap.BattlePortCTF => new Vector3(-12.37f, -3.435f, 0f),
                EMap.TheParkCTF => new Vector3(-12.64f, -5.285f, 0f),
                EMap.SkyHighCTF => new Vector3(-4.655f, 10.315f, 0f),
                _ => default
            };
            if (map is not (EMap.BattlePortCTF or EMap.TheParkCTF or EMap.SkyHighCTF) ||
                (team != ETeam.Blue && team != ETeam.Red))
            {
                position = default;
                return false;
            }

            // The recovered flag sheets identify the left stand as Red and the
            // right stand as Blue. Keep each team on its own side: the source
            // spawn is on the left, so Red uses it and Blue uses its mirror.
            position = team == ETeam.Red
                ? sourceSpawn
                : new Vector3(-sourceSpawn.X, sourceSpawn.Y, sourceSpawn.Z);
            return true;
        }

        public static bool IsInsideMapBounds(EMap map, float x, float y)
        {
            return TryGetBounds(map, out var bounds) && float.IsFinite(x) && float.IsFinite(y) &&
                x >= bounds.Item1 + 0.42f && x <= bounds.Item3 - 0.42f &&
                y >= bounds.Item2 + 0.42f && y <= bounds.Item4 - 0.42f;
        }

        public static bool TryGetMovementBounds(
            EMap map, out float minX, out float maxX, out float maxY)
        {
            if (!TryGetBounds(map, out var bounds))
            {
                minX = maxX = maxY = 0f;
                return false;
            }

            minX = bounds.Item1 + 0.42f;
            maxX = bounds.Item3 - 0.42f;
            maxY = bounds.Item4 - 0.42f;
            return true;
        }

        public static bool TryGetHorizontalMovementBounds(
            EMap map, out float minX, out float maxX)
        {
            if (!TryGetBounds(map, out var bounds))
            {
                bounds = map switch
                {
                    EMap.DryDays => (-13.5f, -7f, 13.5f, 4f),
                    EMap.GreenHillSide1 =>
                        (-25.395573f, -3.3120005f, 25.130336f, 12.957719f),
                    EMap.GreenHillSide2 => (-47.975f, -5.97f, 2.26047f, 3.447347f),
                    // Both recovered 013-NightCity MPFs identify City of Darkness
                    // and use a 2000-pixel map width (20 world units at 0.01 per pixel).
                    EMap.CityOfDarkness1 or EMap.CityOfDarkness2 => (-10f, 0f, 10f, 0f),
                    EMap.Nocturne => (-19.435f, -17.14f, 23.61f, 6.2399998f),
                    EMap.GhostHouse => (-3.6199998f, -5.4650001f, 2.6630752f, 2.043577f),
                    _ => default
                };
                if (map is not (EMap.DryDays or EMap.GreenHillSide1 or EMap.GreenHillSide2 or
                    EMap.CityOfDarkness1 or EMap.CityOfDarkness2 or EMap.Nocturne or EMap.GhostHouse))
                {
                    minX = maxX = 0f;
                    return false;
                }
            }

            minX = bounds.Item1 + 0.42f;
            maxX = bounds.Item3 - 0.42f;
            return true;
        }

        public static bool IsBelowFallDeathBoundary(EMap map, float y)
        {
            var bottom = map switch
            {
                EMap.BattlePortCTF => -5f,
                EMap.TheParkCTF => -7.5f,
                EMap.SkyHighCTF => -12.5f,
                _ => float.NaN
            };
            return float.IsFinite(y) && float.IsFinite(bottom) && y < bottom - 4f;
        }

        public static bool IsBelowMapBottom(EMap map, float y)
        {
            return TryGetBounds(map, out var bounds) && float.IsFinite(y) && y < bounds.Bottom;
        }

        private static bool TryGetBounds(
            EMap map, out (float Left, float Bottom, float Right, float Top) bounds)
        {
            bounds = map switch
            {
                EMap.BattlePortCTF => (-16f, -5f, 16f, 5f),
                EMap.TheParkCTF => (-15f, -7.5f, 15f, 7.5f),
                EMap.SkyHighCTF => (-6f, -12.5f, 6f, 12.5f),
                _ => default
            };
            return map is EMap.BattlePortCTF or EMap.TheParkCTF or EMap.SkyHighCTF;
        }
    }
}
