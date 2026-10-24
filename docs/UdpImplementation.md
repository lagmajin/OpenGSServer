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

The server settings file can provide team-owned spawn points and an authoritative
respawn delay (default 5 seconds, configurable from 0 to 60). The server records
the down time and refuses early requests; the delay is included in `MatchStatus`
so clients can show the same countdown. Coordinates from the client are ignored.
Spawn points use X/Y for the movement plane and Z for vertical height; the default
points are on the ground plane at Z=0. A settings file containing the exact prior
built-in spawn list is migrated to the new ground-plane layout when loaded; custom
spawn lists are preserved. An empty list or one with no valid entries falls back to
the built-in points, so a malformed configuration cannot respawn players at death.
Configured coordinates must be finite, within ±100,000, and use a non-negative Z.
Example:

```json
{
  "RespawnDelaySeconds": 5,
  "RespawnPoints": [
    { "Team": "Red", "X": -8, "Y": 0, "Z": 0 },
    { "Team": "Blue", "X": 8, "Y": 0, "Z": 0 }
  ]
}
```

If no point exists for a player's team, the server keeps the authoritative
position already held by the lag-compensation state manager.
