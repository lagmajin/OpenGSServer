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
            var bounds = map switch
            {
                EMap.BattlePortCTF => (-16f, -5f, 16f, 5f),
                EMap.TheParkCTF => (-15f, -7.5f, 15f, 7.5f),
                EMap.SkyHighCTF => (-6f, -12.5f, 6f, 12.5f),
                _ => default
            };
            return map is (EMap.BattlePortCTF or EMap.TheParkCTF or EMap.SkyHighCTF) &&
                float.IsFinite(x) && float.IsFinite(y) &&
                x >= bounds.Item1 + 0.42f && x <= bounds.Item3 - 0.42f &&
                y >= bounds.Item2 + 0.42f && y <= bounds.Item4 - 0.42f;
        }
    }
}
