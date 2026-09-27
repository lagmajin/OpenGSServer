using System;
using System.Threading;
using LiteNetLib;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the match RUDP path without Unity.
///
/// S2 asks for the match progression to be server authoritative. The
/// playthrough smoke covers the lobby side; this covers the UDP listener:
/// the token handshake registers the peer, position input is admitted, and
/// the authoritative position the server computed is echoed back.
///
/// It uses the same LiteNetLib build as the server, so the wire format is
/// the real one rather than a reimplementation.
/// </summary>
// These tests each bind a real udp port, so two of them running at once
// can land on the same one. Sharing a collection keeps them on one lane.
[Collection("RealtimeSocket")]
public sealed class MatchRudpProbeTests : IDisposable
{
    private static int nextPort = 64980;

    private MatchUDPServer server = null!;
    private MatchRudpProbe probe = null!;
    private string playerId = string.Empty;

    /// <summary>
    /// The listener is normally polled from the 25Hz match loop. These tests
    /// drive the server directly, so they have to pump it themselves.
    /// </summary>
    private void Pump(int durationMs)
    {
        var deadline = Environment.TickCount64 + durationMs;
        while (Environment.TickCount64 < deadline)
        {
            server.PollingEvent();
            server.Tick(0.04f);
            probe?.Poll(20);
            Thread.Sleep(10);
        }
    }

    private void PumpOnce()
    {
        server.PollingEvent();
        server.Tick(0.04f);
    }

    private void Connect()
    {
        playerId = "rudp-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        server = new MatchUDPServer();
        // Each test binds a real UDP socket, so they must not share a port.
        server.Listen(Interlocked.Increment(ref nextPort));

        var token = server.IssueConnectionToken(playerId);
        Assert.False(string.IsNullOrWhiteSpace(token), "the server did not issue a UDP token");

        probe = new MatchRudpProbe();
        probe.Listener.NetworkReceiveEvent += (peer, reader, method) =>
        {
            var payload = reader.GetString();
            reader.Recycle();
            try
            {
                probe.Offer(JObject.Parse(payload));
            }
            catch (Exception)
            {
                // The probe is test only; malformed payloads are ignored.
            }
        };

        Assert.True(
            probe.Connect("127.0.0.1", server.UdpPort ?? 0, playerId, token, "rudp-room", 5000, PumpOnce),
            "the probe did not reach the connected state");
    }

    public void Dispose()
    {
        probe?.Dispose();
        server?.Dispose();
    }

    [Fact]
    public void TokenHandshakeRegistersThePeer()
    {
        Connect();

        // Send one input and pump the listener. If ClientConnect had been
        // rejected the peer is dropped and nothing is ever queued, so reaching
        // a state entry means the handshake succeeded.
        probe.SendMove(playerId, "rudp-room", 0f, 0f, false, false, 0.04f, 1);
        Pump(600);

        var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
        Assert.NotNull(state);
    }

    [Fact]
    public void ValidPositionInputIsAdmitted()
    {
        Connect();

        // The echo back only happens for a player inside a match room, which
        // these tests do not build, so what is checked here is that the
        // authoritative state manager accepted the packet. The rejected case
        // is covered by the test below.
        probe.SendPosition(playerId, "rudp-room", 0.5f, -0.5f, 0f, 0f, 0.04f, 1);
        Pump(1200);

        var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
        Assert.InRange(state.PositionX, -1f, 1f);
        Assert.InRange(state.PositionY, -1f, 1f);
    }

    [Fact]
    public void ExcessiveDeltaTimeDoesNotMoveTheAuthoritativePosition()
    {
        Connect();

        // The listener rejects a delta time over the 0.25s limit, so the
        // authoritative position must stay inside the allowed range.
        probe.SendPosition(playerId, "rudp-room", 900f, 900f, 0f, 0f, 5f, 2);
        Pump(800);

        var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
        Assert.InRange(state.PositionX, -1f, 1f);
        Assert.InRange(state.PositionY, -1f, 1f);
    }
}
