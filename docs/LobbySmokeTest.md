# Lobby-to-match smoke test

`lobby_smoke_client.py` validates the real lobby TCP path without Unity:

1. create a temporary account;
2. log in;
3. request the room list;
4. create a wait room;
5. request match start and verify `LoadingStartedNotification`.

The client uses the Unity-compatible `JS` + JSON + `0x1f` framing. Start the
server first, then run:

```powershell
python .\lobby_smoke_client.py --host 127.0.0.1 --port 60000
```

The account is intentionally unique per run. The test requires the server's
account database to be writable and does not remove the generated account.

# One-command smoke run

`tools/run_smoke.ps1` repeats the manual steps above: it builds the solution,
starts a server on disposable ports (`64010` lobby, `64011` match TCP, `64012`
match UDP, `64013` management), waits for the lobby listener, runs every smoke
client, and always stops the server afterwards. It does not change any server
behaviour and does not touch the ports you normally use, so you can keep a real
server running while you use it.

```powershell
.\tools\run_smoke.ps1
.\tools\run_smoke.ps1 -Only mission
.\tools\run_smoke.ps1 -Configuration Release
```

The script exits non-zero when any smoke client fails, so the same command is
used by `.github/workflows/server-build.yml`.

## Multi-player loading gate

To verify the multi-player loading gate, start the server and run:

```powershell
python .\two_player_loading_smoke.py --host 127.0.0.1 --port 60000
```

This test confirms that `AllowEnterMap` is withheld until both players send
`LoadingCompleted`.

## Mission room lifecycle

`mission_room_lifecycle_smoke.py` covers mission room create, list, leave, and
disconnect cleanup. It defaults to port `64010`, which is the port used by
`tools/run_smoke.ps1`.

```powershell
python .\mission_room_lifecycle_smoke.py --host 127.0.0.1 --port 64010
```

## Management server

`test_client.py` talks to the management listener with newline-delimited JSON
instead of the `JS` + `0x1f` lobby framing. Credentials come from
`OPENGS_ADMIN_ID` and `OPENGS_ADMIN_PASSWORD`.

```powershell
python .\test_client.py --host 127.0.0.1 --port 50020
```

The account database must be writable, and each run creates a new account that
is not removed afterwards.
