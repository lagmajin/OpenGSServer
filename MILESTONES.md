# OpenGSServer Milestones

This repository owns the authoritative backend work. The next milestones should
keep the server runnable while tightening the real lobby and match paths.
The server should remain fully usable without any Unity client dependency.

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

Current status:
- `Server/ServerHost.cs` owns the bootstrap and shutdown order; `Program.cs`
  now only parses arguments and decides when to stop
- configuration and admin credentials are loaded before any listener starts
- `Shutdown` is idempotent and safe to call from both the normal path and
  `AppDomain` exit; the instance mutex is always released
- `Server/StartupBanner.cs` holds the startup log, so it can be removed or
  redirected without touching the bootstrap sequence
- `Tests/ServerHostTests.cs` covers single-instance locking, idempotent
  shutdown, and option validation
- `tools/run_smoke.ps1` repeats the full local bootstrap and smoke path in
  one command, so startup and shutdown regressions fail fast

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

Current status:
- `lobby_smoke_client.py` already covers the shared contract: login, account
  creation, room list, create, join, leave, and the same session refusing a
  second login
- `reconnect_smoke.py` covers a dropped connection: same account, same player
  id, no ghost room left by the dead session, and a working account
- `Server/Event/PlayerSessionCleanup.cs` is the single teardown path. It was
  added because LogoutRequest and the dropped connection had drifted apart:
  logout left in-game match state and the account session behind, while a
  drop cleared all four
- the client side has `NetworkAuthorityGate`, so a call site no longer has to
  read `localServerTestMode` itself to know whether a request reaches the
  backend or a local stub
- giving `Assets/Scripts/NetworkTest` its own assembly is blocked, not
  overlooked. `LocalTestTcpServer` holds a `NetworkRequestRouter` and
  `LocalTestMatchRUDPServer` holds a `ServerReplayTape`, both in the OpenGSR
  assembly, while OpenGSR registers both servers in its Autofac containers.
  The dependency is bidirectional, so Unity reports a cyclic dependency.
  Breaking it means moving those two types into a shared lower assembly

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

Current status:
- `MatchServerV2` runs a 25Hz `TickTimer` and drives `room.GameUpdate()` for
  every playing room. Verified by reading the loop, not by a headless match
  test; there is still no test that plays a match end to end
- UDP input is applied on the fixed tick rather than in the receive callback,
  and the lag compensation manager is ticked with the same delta
- `BroadcastToRoom` and `BroadcastToAll` are implemented for both LiteNetLib
  writers and JObject messages; no stubs remain in the file

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

Current status:
- start, progress, and completion are server handled, and `AllowEnterMap` is
  correctly withheld until every player reports in
- `MatchRoomManager` now registers the room as waiting when it announces
  `LoadingStartedNotification`. It did not before, so the gate and the timeout
  both had nothing to work with
- `LoadingTimeoutMonitor` sweeps every 5s and, past the deadline, sends
  `LoadingFailed` with the players that never reported, clears the loading
  state, and releases the wait room. The deadline defaults to 60s and comes
  from `OPENGS_LOADING_TIMEOUT_SECONDS` so tests need not wait it out
- `WaitRoom.CancelPendingMatch` releases a room whose match never started.
  Without it `NowPlaying` stayed true and `CanStartMatch()` returned false
  forever, so the room could never be retried
- the client already handles `LoadingFailed` in
  `OnlineLoadingSceneNetworkManager` and returns to the wait room through
  `OnlineLoadingScene.OnLoadingFailed`
- `loading_timeout_smoke.py` covers the stall, the fallback message, and the
  retry

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

Current status:
- `test_client.py` drives the management listener
- `tools/run_smoke.ps1` builds, boots a throwaway server, and runs
  `lobby_smoke_client.py`, `two_player_loading_smoke.py`,
  `mission_room_lifecycle_smoke.py`, and `reconnect_smoke.py` in one command
- the same command runs in `.github/workflows/server-build.yml`
- `ServerHostTests` and `ServerPlayerStateManagerTests` cover the
  regression-prone server rules

## S5. Core-Only Runtime Harness

Goal: keep the backend runnable as a pure C# system even if the Unity client is
replaced.

Scope:
- `Core/*`
- `Network/*`
- `Server/*`
- `test_client.py`
- repeatable headless validation scripts

Why this matters:
- the server should not depend on Unity scene behavior to validate gameplay
- a pure core/server loop is the best hedge against future client changes
- headless tests are easier to automate and maintain than scene-based checks

Done when:
- core gameplay and room flow can be exercised without Unity assets
- server smoke tests only rely on core/server code and a lightweight client
- Unity-specific assumptions are no longer required for backend validation

## Suggested Order

1. `S0`
2. `S1`
3. `S2`
4. `S3`
5. `S4`
6. `S5`
