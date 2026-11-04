using System.Numerics;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// Map-authored CTF positions recovered from the original MPF files.
    /// The source spawn is the Blue-side spawn used by the native offline
    /// client; Red starts at its horizontal mirror on these symmetric maps.
    /// </summary>
    internal static class CtfMapLayout
    {
        public static bool TryGetTeamSpawn(EMap map, ETeam team, out Vector3 position)
        {
            var blueSpawn = map switch
            {
                EMap.BattlePortCTF => new Vector3(-12.37f, -2.71f, 0f),
                EMap.TheParkCTF => new Vector3(-12.64f, -4.63f, 0f),
                EMap.SkyHighCTF => new Vector3(-4.655f, 10.52f, 0f),
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
