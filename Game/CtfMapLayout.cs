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
            var blueSpawn = map switch
            {
                EMap.BattlePortCTF => new Vector3(-12.37f, -2.71f, 0f),
                EMap.TheParkCTF => new Vector3(-12.64f, -4.63f, 0f),
                EMap.SkyHighCTF => new Vector3(-4.655f, 10.535f, 0f),
                _ => default
            };
            if (map is not (EMap.BattlePortCTF or EMap.TheParkCTF or EMap.SkyHighCTF) ||
                (team != ETeam.Blue && team != ETeam.Red))
            {
                position = default;
                return false;
            }

            position = team == ETeam.Blue
                ? blueSpawn
                : new Vector3(-blueSpawn.X, blueSpawn.Y, blueSpawn.Z);
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
