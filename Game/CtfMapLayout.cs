using System.Numerics;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// CTF positions and recovered world bounds used by online movement and
    /// fall-death checks. Team spawns follow the playable solid-mask surface,
    /// including SkyHighCTF's small vertical platform offset.
    /// </summary>
    internal static class CtfMapLayout
    {
        public static EMap GetMap(OpenGSCore.MatchRoom room) =>
            room?.WaitRoomLink?.Map ?? EMap.Unknown;

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
            // SkyHighCTF's mirrored platform is 0.08 units lower in the native
            // solid mask, so its Blue spawn must sit lower to preserve the same
            // 0.06-unit ground clearance as Red.
            var blueSpawnY = map == EMap.SkyHighCTF
                ? sourceSpawn.Y - 0.08f
                : sourceSpawn.Y;
            position = team == ETeam.Red
                ? sourceSpawn
                : new Vector3(-sourceSpawn.X, blueSpawnY, sourceSpawn.Z);
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
            if (map is not (EMap.BattlePortCTF or EMap.TheParkCTF or EMap.SkyHighCTF) ||
                !TryGetBounds(map, out var bounds))
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
                minX = maxX = 0f;
                return false;
            }

            minX = bounds.Left + 0.42f;
            maxX = bounds.Right - 0.42f;
            return true;
        }

        public static bool IsBelowMapFallDeathBoundary(EMap map, float y)
        {
            return TryGetBounds(map, out var bounds) && float.IsFinite(y) &&
                y < bounds.Bottom - 4f;
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
                // Bounds are taken from the recovered native map JSON generated
                // from the corresponding Unity scenes or MPF dimensions.
                EMap.ArchLoadOfGunster => (-7.3421549f, -6.9800004f, 7.2278451f, 10.9799993f),
                EMap.BattlePort1 => (-9.44f, -6.94f, 8.04f, 8.0949485f),
                EMap.BattlePortCTF => (-16f, -5f, 16f, 5f),
                EMap.Christmas => (-10.4429551f, -7.5207486f, 11.6149997f, 6.0648394f),
                EMap.CityOfDarkness1 or EMap.CityOfDarkness2 => (-10f, -5f, 10f, 5f),
                EMap.DesertedJungleSide1 => (-16.925f, -7.805f, 18.325f, 3.115f),
                EMap.DryDays => (-13.5f, -7f, 13.5f, 4f),
                EMap.FullHouse => (-7.5494452f, -9.2774458f, 5.6391058f, 1.4925756f),
                EMap.GhostHouse => (-3.6199998f, -5.4650001f, 2.6630752f, 2.043577f),
                EMap.GreenHillSide1 => (-25.3955728f, -3.3120005f, 25.130336f, 12.9577184f),
                EMap.GreenHillSide2 => (-47.975f, -5.97f, 2.26047f, 3.4473468f),
                EMap.IceValley => (-21.445f, -14.195f, 19.285f, 2.901f),
                EMap.Nocturne => (-19.435f, -17.14f, 23.61f, 6.2399998f),
                EMap.OnStudio => (-12.8462972f, -7.384382f, 5.095f, 11.78f),
                EMap.RedStorm1 => (-10f, -5f, 10f, 5f),
                EMap.RobotFactory => (-8f, -6.0409155f, 11.7093969f, 7.56f),
                EMap.RuinOfWarSide1 => (-9.85f, -7.10499997f, 19.27f, -1.753817f),
                EMap.BluffStructure1 or EMap.BluffStructure2 => (-6f, -12.5f, 6f, 12.5f),
                EMap.SkyHighCTF => (-6f, -12.5f, 6f, 12.5f),
                EMap.SeaSideBase => (-10.95f, -5f, 10.95f, 5f),
                EMap.ThePark => (-8.8f, -8.4499999f, 13.7500002f, 6.20999996f),
                EMap.TheParkCTF => (-15f, -7.5f, 15f, 7.5f),
                EMap.Waterfall => (-11.5f, -5.25f, 11.5f, 5.25f),
                _ => default
            };
            return map is EMap.ArchLoadOfGunster or EMap.BattlePort1 or EMap.BattlePortCTF or
                EMap.BluffStructure1 or EMap.BluffStructure2 or EMap.Christmas or
                EMap.CityOfDarkness1 or EMap.CityOfDarkness2 or
                EMap.DesertedJungleSide1 or EMap.DryDays or EMap.FullHouse or EMap.GhostHouse or
                EMap.GreenHillSide1 or EMap.GreenHillSide2 or EMap.IceValley or EMap.Nocturne or
                EMap.OnStudio or EMap.RedStorm1 or EMap.RobotFactory or EMap.RuinOfWarSide1 or
                EMap.SeaSideBase or EMap.SkyHighCTF or EMap.ThePark or EMap.TheParkCTF or
                EMap.Waterfall;
        }
    }
}
