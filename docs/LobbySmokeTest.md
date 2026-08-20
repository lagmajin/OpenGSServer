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

To verify the multi-player loading gate, start the server and run:

```powershell
python .\two_player_loading_smoke.py --host 127.0.0.1 --port 60000
```

This test confirms that `AllowEnterMap` is withheld until both players send
`LoadingCompleted`.
