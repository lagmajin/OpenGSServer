using System.Net;
using System.Net.Sockets;

namespace OpenGSServer.Tests;

internal static class MatchTestPort
{
    public static int GetAvailableUdpPort()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }
}
