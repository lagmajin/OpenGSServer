using System.Threading;
using Xunit;

namespace OpenGSServer.Tests;

public sealed class ServerHostTests
{
    private static ServerStartupOptions Options() => new()
    {
        LobbyPort = 61000,
        MatchTcpPort = 61001,
        MatchUdpPort = 61002,
        ManagementPort = 61003,
        NoConsole = true
    };

    [Fact]
    public void SecondInstanceCannotAcquireTheLock()
    {
        using var first = new ServerHost(Options());
        Assert.True(first.TryAcquireInstanceLock());

        using var second = new ServerHost(Options());
        Assert.False(second.TryAcquireInstanceLock());

        // Abandoning startup must not leave the mutex held.
        second.ReleaseInstanceLock();
    }

    [Fact]
    public void ShutdownIsIdempotentAndDoesNotStartAnyService()
    {
        var host = new ServerHost(Options());
        Assert.True(host.TryAcquireInstanceLock());

        host.Shutdown();
        host.Shutdown();
        host.Dispose();

        // The lock is free again, so a fresh host can start.
        using var next = new ServerHost(Options());
        Assert.True(next.TryAcquireInstanceLock());
    }

    [Fact]
    public void HostRejectsMissingOptions()
    {
        Assert.Throws<System.ArgumentNullException>(() => new ServerHost(null!));
    }
}
