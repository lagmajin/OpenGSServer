# UDP implementation boundary

The production match server is hosted by `Server/MatchUDPServer.cs` and is
started by `Server/MatchServerV2.cs`. It owns:

- LiteNetLib connection-key and `UdpToken` authentication;
- player connection and reconnect lifecycle;
- fixed-tick input admission;
- lag-compensation state processing and transform broadcast.

`Server/RUDP/MatchRUdpServer.cs` and `Manager/MatchRUdpServerManager.cs` are
legacy/experimental implementations. They are not referenced by
`MatchServerV2` or `Program`. New protocol or gameplay work must target
`MatchUDPServer` first. Keep the legacy files available until a separate
cleanup change removes them after downstream branches have migrated.

## Authoritative gameplay boundary

`PlayerMove` inputs are simulated by the server. Client positions are never
used to initialize or overwrite server state; the legacy `PlayerPositionUpdate`
path only validates the claim and sends back the server transform. Server
movement also advances during input gaps and caps the amount of queued input
time simulated in one tick.

Combat and match events are sanitized before broadcast: damage, kills,
respawns, projectile positions, timestamps, sequence numbers, and flag carrier
state are server-owned. TCP-only control messages cannot be injected through
UDP.

## Regression tests

Run the movement authority tests with:

```powershell
dotnet test Tests/OpenGSServer.Tests.csproj
```

## Respawn points

The server settings file can provide team-owned spawn points. Coordinates from
the client are ignored. Example:

```json
{
  "RespawnPoints": [
    { "Team": "Red", "X": -8, "Y": 0, "Z": 0 },
    { "Team": "Blue", "X": 8, "Y": 0, "Z": 0 }
  ]
}
```

If no point exists for a player's team, the server keeps the authoritative
position already held by the lag-compensation state manager.
