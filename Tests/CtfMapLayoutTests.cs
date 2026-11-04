using System.Collections.Generic;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

public sealed class CtfMapLayoutTests
{
    public static IEnumerable<object[]> SupportedMaps => new[]
    {
        new object[]
        {
            EMap.BattlePortCTF, 16f, 5f,
            -12.37f, -3.435f, 12.37f, -3.435f,
            -13.435f, 0.865f, 13.115f, 0.945f
        },
        new object[]
        {
            EMap.TheParkCTF, 15f, 7.5f,
            -12.64f, -5.285f, 12.64f, -5.285f,
            -9.725f, -1.365f, 9.185f, -1.405f
        },
        new object[]
        {
            EMap.SkyHighCTF, 6f, 12.5f,
            -4.655f, 10.315f, 4.655f, 10.235f,
            -2.755f, -4.245f, 2.795f, -4.235f
        }
    };

    public static IEnumerable<object[]> MapsWithRecoveredBounds => new[]
    {
        new object[] { EMap.ArchLoadOfGunster, -7.3421549f, -6.9800004f, 7.2278451f, 10.9799993f },
        new object[] { EMap.BattlePort1, -9.44f, -6.94f, 8.04f, 8.0949485f },
        new object[] { EMap.BattlePortCTF, -16f, -5f, 16f, 5f },
        new object[] { EMap.BluffStructure1, -6f, -12.5f, 6f, 12.5f },
        new object[] { EMap.BluffStructure2, -6f, -12.5f, 6f, 12.5f },
        new object[] { EMap.Christmas, -10.4429551f, -7.5207486f, 11.6149997f, 6.0648394f },
        new object[] { EMap.CityOfDarkness1, -10f, -5f, 10f, 5f },
        new object[] { EMap.CityOfDarkness2, -10f, -5f, 10f, 5f },
        new object[] { EMap.DesertedJungleSide1, -16.925f, -7.805f, 18.325f, 3.115f },
        new object[] { EMap.DryDays, -13.5f, -7f, 13.5f, 4f },
        new object[] { EMap.FullHouse, -7.5494452f, -9.2774458f, 5.6391058f, 1.4925756f },
        new object[] { EMap.GhostHouse, -3.6199998f, -5.4650001f, 2.6630752f, 2.043577f },
        new object[] { EMap.GreenHillSide1, -25.3955728f, -3.3120005f, 25.130336f, 12.9577184f },
        new object[] { EMap.GreenHillSide2, -47.975f, -5.97f, 2.26047f, 3.4473468f },
        new object[] { EMap.IceValley, -21.445f, -14.195f, 19.285f, 2.901f },
        new object[] { EMap.Nocturne, -19.435f, -17.14f, 23.61f, 6.2399998f },
        new object[] { EMap.OnStudio, -12.8462972f, -7.384382f, 5.095f, 11.78f },
        new object[] { EMap.RedStorm1, -10f, -5f, 10f, 5f },
        new object[] { EMap.RobotFactory, -8f, -6.0409155f, 11.7093969f, 7.56f },
        new object[] { EMap.RuinOfWarSide1, -9.85f, -7.10499997f, 19.27f, -1.753817f },
        new object[] { EMap.SeaSideBase, -10.95f, -5f, 10.95f, 5f },
        new object[] { EMap.SkyHighCTF, -6f, -12.5f, 6f, 12.5f },
        new object[] { EMap.ThePark, -8.8f, -8.4499999f, 13.7500002f, 6.20999996f },
        new object[] { EMap.TheParkCTF, -15f, -7.5f, 15f, 7.5f },
        new object[] { EMap.Waterfall, -11.5f, -5.25f, 11.5f, 5.25f }
    };

    [Theory]
    [MemberData(nameof(SupportedMaps))]
    public void SpawnsAndFlagStandsStayInsideTheirRecoveredMap(
        EMap map, float halfWidth, float halfHeight,
        float redSpawnX, float redSpawnY, float blueSpawnX, float blueSpawnY,
        float redFlagX, float redFlagY, float blueFlagX, float blueFlagY)
    {
        Assert.True(CtfMapLayout.TryGetMovementBounds(
            map, out var minimumX, out var maximumX, out var maximumY));
        Assert.Equal(-halfWidth + 0.42f, minimumX, 3);
        Assert.Equal(halfWidth - 0.42f, maximumX, 3);
        Assert.Equal(halfHeight - 0.42f, maximumY, 3);

        Assert.True(CtfMapLayout.TryGetHorizontalMovementBounds(
            map, out var horizontalMinimumX, out var horizontalMaximumX));
        Assert.Equal(minimumX, horizontalMinimumX, 3);
        Assert.Equal(maximumX, horizontalMaximumX, 3);

        Assert.True(CtfMapLayout.TryGetTeamSpawn(map, ETeam.Red, out var redSpawn));
        Assert.True(CtfMapLayout.TryGetTeamSpawn(map, ETeam.Blue, out var blueSpawn));
        Assert.Equal(redSpawnX, redSpawn.X, 3);
        Assert.Equal(redSpawnY, redSpawn.Y, 3);
        Assert.Equal(blueSpawnX, blueSpawn.X, 3);
        Assert.Equal(blueSpawnY, blueSpawn.Y, 3);
        Assert.True(CtfMapLayout.IsInsideMapBounds(map, redSpawn.X, redSpawn.Y));
        Assert.True(CtfMapLayout.IsInsideMapBounds(map, blueSpawn.X, blueSpawn.Y));

        Assert.True(CtfMapLayout.TryGetFlagStand(map, ETeam.Red, out var redFlag));
        Assert.True(CtfMapLayout.TryGetFlagStand(map, ETeam.Blue, out var blueFlag));
        Assert.Equal(redFlagX, redFlag.X, 3);
        Assert.Equal(redFlagY, redFlag.Y, 3);
        Assert.Equal(blueFlagX, blueFlag.X, 3);
        Assert.Equal(blueFlagY, blueFlag.Y, 3);
        Assert.True(CtfMapLayout.IsInsideMapBounds(map, redFlag.X, redFlag.Y));
        Assert.True(CtfMapLayout.IsInsideMapBounds(map, blueFlag.X, blueFlag.Y));
    }

    [Theory]
    [InlineData(EMap.BattlePortCTF, -5f)]
    [InlineData(EMap.TheParkCTF, -7.5f)]
    [InlineData(EMap.SkyHighCTF, -12.5f)]
    public void FallDeathStartsFourUnitsBelowTheRecoveredMap(
        EMap map, float mapBottom)
    {
        Assert.False(CtfMapLayout.IsBelowMapFallDeathBoundary(map, mapBottom - 4f));
        Assert.True(CtfMapLayout.IsBelowMapFallDeathBoundary(map, mapBottom - 4.01f));
        Assert.False(CtfMapLayout.IsBelowMapFallDeathBoundary(map, float.NaN));
        Assert.False(CtfMapLayout.IsBelowMapFallDeathBoundary(EMap.Unknown, mapBottom - 5f));
    }

    [Theory]
    [InlineData(EMap.BattlePortCTF, -16f, 16f, -15.58f, 15.58f)]
    [InlineData(EMap.TheParkCTF, -15f, 15f, -14.58f, 14.58f)]
    [InlineData(EMap.SkyHighCTF, -6f, 6f, -5.58f, 5.58f)]
    public void HorizontalMovementKeepsPlayerRadiusInsideTheMap(
        EMap map, float mapLeft, float mapRight, float expectedMinimum, float expectedMaximum)
    {
        Assert.True(CtfMapLayout.TryGetHorizontalMovementBounds(
            map, out var minimumX, out var maximumX));
        Assert.Equal(expectedMinimum, minimumX, 2);
        Assert.Equal(expectedMaximum, maximumX, 2);
        Assert.False(CtfMapLayout.IsInsideMapBounds(map, mapLeft, 0f));
        Assert.False(CtfMapLayout.IsInsideMapBounds(map, mapRight, 0f));
        Assert.True(CtfMapLayout.IsInsideMapBounds(map, minimumX, 0f));
        Assert.True(CtfMapLayout.IsInsideMapBounds(map, maximumX, 0f));
    }

    [Theory]
    [MemberData(nameof(MapsWithRecoveredBounds))]
    public void RecoveredOnlineMapsHaveServerSideWallsAndFallDeath(
        EMap map, float mapLeft, float mapBottom, float mapRight, float mapTop)
    {
        Assert.True(CtfMapLayout.TryGetHorizontalMovementBounds(
            map, out var minimumX, out var maximumX));
        Assert.Equal(mapLeft + 0.42f, minimumX, 3);
        Assert.Equal(mapRight - 0.42f, maximumX, 3);

        Assert.False(CtfMapLayout.IsBelowMapFallDeathBoundary(map, mapBottom - 4f));
        Assert.True(CtfMapLayout.IsBelowMapFallDeathBoundary(map, mapBottom - 4.01f));
        Assert.False(CtfMapLayout.IsBelowMapFallDeathBoundary(map, float.NaN));
        Assert.True(CtfMapLayout.IsBelowMapBottom(map, mapBottom - 0.01f));
        Assert.False(CtfMapLayout.IsBelowMapBottom(map, mapBottom));
        Assert.True(mapTop > mapBottom);
    }

    [Fact]
    public void EveryMapOfferedByOnlineModesHasMovementWallsAndFallDeathBounds()
    {
        foreach (var mode in new[]
        {
            EGameMode.CaptureTheFlag,
            EGameMode.DeathMatch,
            EGameMode.Survival,
            EGameMode.TeamDeathMatch,
            EGameMode.TeamSurvival
        })
        {
            foreach (var map in GameMode.MapsFor(mode))
            {
                Assert.True(CtfMapLayout.TryGetHorizontalMovementBounds(map, out _, out _),
                    $"{mode} map {map} has no horizontal wall bounds.");
                Assert.True(CtfMapLayout.IsBelowMapFallDeathBoundary(map, -1000f),
                    $"{mode} map {map} has no fall-death boundary.");
            }
        }
    }
}
