using System;
using System.IO;
using System.Linq;
using OpenGSCore;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the leaderboard store.
/// <para>
/// Every method on it used to fetch a collection and drop it, so the database
/// file was created and nothing was ever written. Nothing tested that a result
/// survives, which is the only property a leaderboard has.
/// </para>
/// </summary>
public sealed class RankingDatabaseTests : IDisposable
{
    private readonly string path = Path.Combine(
        Path.GetTempPath(),
        $"ranking-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        DeleteDatabaseFiles(path);
    }

    private static void DeleteDatabaseFiles(string path)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var candidate = path + suffix;
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    private RankingDatabase Open()
    {
        var database = new RankingDatabase();
        RankingDatabase.rankingDBfileName = path;
        database.ConnectRankingDB();
        return database;
    }

    [Fact]
    public void AResultIsStored()
    {
        using var database = Open();

        var stored = database.Record("acct-1", "Ami", EGameMode.DeathMatch, 12);

        Assert.NotNull(stored);
        Assert.Equal(12, stored!.BestScore);
        Assert.Equal(12, stored.TotalScore);
        Assert.Equal(1, stored.Matches);
    }

    [Fact]
    public void ASecondMatchAddsToTheSameRow()
    {
        using var database = Open();
        database.Record("acct-1", "Ami", EGameMode.DeathMatch, 12);

        var stored = database.Record("acct-1", "Ami", EGameMode.DeathMatch, 5);

        // The key carries the account and the mode, so the second match updates
        // the standing rather than adding a second row for the same player.
        Assert.Equal(2, stored!.Matches);
        Assert.Equal(17, stored.TotalScore);
        Assert.Equal(12, stored.BestScore);
    }

    [Fact]
    public void ModesAreRankedSeparately()
    {
        using var database = Open();
        database.Record("acct-1", "Ami", EGameMode.DeathMatch, 20);
        database.Record("acct-1", "Ami", EGameMode.TeamDeathMatch, 3);

        Assert.Equal(20, database.Find("acct-1", EGameMode.DeathMatch)!.BestScore);
        Assert.Equal(3, database.Find("acct-1", EGameMode.TeamDeathMatch)!.BestScore);
    }

    [Fact]
    public void TheBoardIsOrderedBestFirst()
    {
        using var database = Open();
        database.Record("acct-1", "Ami", EGameMode.DeathMatch, 5);
        database.Record("acct-2", "Jack", EGameMode.DeathMatch, 30);
        database.Record("acct-3", "Mary", EGameMode.DeathMatch, 12);

        var board = database.Top(EGameMode.DeathMatch).ToList();

        Assert.Equal(new[] { "Jack", "Mary", "Ami" }, board.Select(entry => entry.DisplayName));
    }

    [Fact]
    public void TheBoardIsBoundedByTheLimit()
    {
        using var database = Open();
        for (var i = 0; i < 5; i++)
        {
            database.Record($"acct-{i}", $"P{i}", EGameMode.DeathMatch, i);
        }

        Assert.Equal(2, database.Top(EGameMode.DeathMatch, limit: 2).Count);
        Assert.Empty(database.Top(EGameMode.DeathMatch, limit: 0));
    }

    [Fact]
    public void AResultSurvivesTheStoreBeingClosedAndReopened()
    {
        using (var database = Open())
        {
            database.Record("acct-1", "Ami", EGameMode.CaptureTheFlag, 4);
        }

        using var reopened = Open();

        Assert.Equal(4, reopened.Find("acct-1", EGameMode.CaptureTheFlag)!.BestScore);
    }

    [Fact]
    public void ThereIsNothingToRecordWithoutAnAccountOrAMode()
    {
        using var database = Open();

        Assert.Null(database.Record("", "Ami", EGameMode.DeathMatch, 5));
        Assert.Null(database.Record("acct-1", "Ami", EGameMode.Unknown, 5));
    }
}
