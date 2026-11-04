using System.Collections.Generic;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

public sealed class CtfMapLayoutTests
{
    public static IEnumerable<object[]> SupportedMaps => new[]
    {
        new object[] { EMap.BattlePortCTF, 16f, 5f, -12.37f, -3.435f },
        new object[] { EMap.TheParkCTF, 15f, 7.5f, -12.64f, -5.285f },
        new object[] { EMap.SkyHighCTF, 6f, 12.5f, -4.655f, 10.315f }
    };

    [Theory]
    [MemberData(nameof(SupportedMaps))]
    public void SpawnsAndFlagStandsStayInsideTheirRecoveredMap(
        EMap map, float halfWidth, float halfHeight, float redX, float redY)
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
        Assert.Equal(redX, redSpawn.X, 3);
        Assert.Equal(redY, redSpawn.Y, 3);
        Assert.True(CtfMapLayout.IsInsideMapBounds(map, redSpawn.X, redSpawn.Y));
        Assert.True(CtfMapLayout.IsInsideMapBounds(map, blueSpawn.X, blueSpawn.Y));

        Assert.True(CtfMapLayout.TryGetFlagStand(map, ETeam.Red, out var redFlag));
        Assert.True(CtfMapLayout.TryGetFlagStand(map, ETeam.Blue, out var blueFlag));
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
}
