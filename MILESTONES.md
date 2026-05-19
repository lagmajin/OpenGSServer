# OpenGSServer Milestones

This repository owns the authoritative backend work. The next milestones should
keep the server runnable while tightening the real lobby and match paths.

## S0. Startup, Shutdown, And Configuration Cleanup

Goal: make the server bootstrap path predictable.

Scope:
- `Program.cs`
- `ServerManager`
- startup / shutdown / process-exit handling

Why this matters:
- The server currently mixes bootstrap, diagnostics, and runtime wiring in one
  place.
- A clear bootstrap path makes later server milestones much safer to change.

Done when:
- startup and shutdown are explicit and repeatable
- configuration loading happens before network services depend on it
- process exit leaves the server in a clean state

## S1. Authoritative Lobby And Account State

Goal: move lobby and account behavior into a single backend source of truth.

Scope:
- `Lobby/*`
- `Account/*`
- `Server/Event/*`
- any local-test server wrappers that currently simulate backend behavior

Why this matters:
- Room list, account, chat, and friend state all need the same authority.
- If these flows stay split, reconnects and room transitions will keep being
  fragile.

Done when:
- login, account creation, room list, room create, join, and leave all use the
  same backend contract
- server-side state survives reconnects in the intended environment
- local test wrappers are clearly separated from the authoritative path

## S2. Match Server Core

Goal: make the real-time match path authoritative instead of log-driven.

Scope:
- `Server/MatchServerV2.cs`
- `Server/MatchUDPServer.cs`
- `Server/MatchTcpServer.cs`
- `Room/*`
- `Game/*`

Why this matters:
- Match state progression is currently split between loop code and transport
  shells.
- Broadcast and room update paths need to be real before gameplay validation can
  trust the server.

Done when:
- match updates are driven by a real server loop
- room state and match state stay in sync
- broadcast paths are implemented instead of stubbed

## S3. Loading And Room Transition Handshake

Goal: make the lobby-to-match transition explicit.

Scope:
- `Server/ManagementServer.cs`
- loading / room transition endpoints
- any room launch or approval flow

Why this matters:
- The client already expects a handshake before entering a map.
- Without a reliable server transition, the end-to-end flow will keep stalling
  at the loading boundary.

Done when:
- loading start, progress, completion, and map-entry approval are handled by
  the server path
- timeout and fallback behavior are deterministic

## S4. Integration Checks And Observability

Goal: make server regressions easier to catch.

Scope:
- `test_client.py`
- `build_output.txt`
- `server_status.txt`
- any repeatable local bootstrap script or diagnostics command

Why this matters:
- A lot of the backend behavior is still validated manually.
- The server needs a tighter smoke-test loop before deeper gameplay work lands.

Done when:
- there is a repeatable login -> lobby -> room -> match smoke path
- protocol changes fail fast in scripted validation or a small test harness

## Suggested Order

1. `S0`
2. `S1`
3. `S2`
4. `S3`
5. `S4`
