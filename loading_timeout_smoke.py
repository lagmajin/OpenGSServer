"""Verify that a player who never reports loading completion cannot lock a room.

S3 asks for the loading handshake to have deterministic timeout and fallback
behaviour. LoadingStarted, LoadingProgress, LoadingCompleted, and the
AllowEnterMap gate are already server handled; what was missing was the case
where a client starts loading and then never reports in. Without a deadline
the room stays locked and the other players can never start a match.

The server reads OPENGS_LOADING_TIMEOUT_SECONDS at startup, so
tools/run_smoke.ps1 sets a short value for this test.
"""

from __future__ import annotations

import argparse
import time
import uuid

from lobby_smoke_client import LobbySmokeClient, SmokeFailure, require_success


def account(client: LobbySmokeClient, suffix: str) -> str:
    account_id = f"loadtimeout_{suffix}"
    password = f"LoadTimeout-{suffix}!"
    client.send(
        {
            "MessageType": "CreateAccountRequest",
            "AccountID": account_id,
            "Password": password,
            "DisplayName": f"LoadTimeout {suffix}",
        }
    )
    require_success(
        client.wait_for(lambda message: message.get("MessageType") == "CreateAccountResponse"),
        "account creation",
    )
    client.send({"MessageType": "LoginRequest", "id": account_id, "pass": password})
    login = require_success(
        client.wait_for(lambda message: message.get("MessageType") == "LoginResponse"),
        "login",
    )
    return login.get("PlayerID") or login.get("GlobalUserId") or account_id


def run(host: str, port: int, timeout: float) -> None:
    suffix = uuid.uuid4().hex[:10]
    room_name = f"Load Timeout {suffix}"

    with LobbySmokeClient(host, port, timeout) as owner:
        owner_id = account(owner, suffix)

        owner.send(
            {
                "MessageType": "CreateRoomRequest",
                "PlayerID": owner_id,
                "PlayerName": f"LoadTimeout {suffix}",
                "RoomName": room_name,
                "Capacity": 2,
            }
        )
        created = require_success(
            owner.wait_for(lambda message: message.get("MessageType") == "CreateRoomResponse"),
            "room creation",
        )
        room_id = created.get("RoomID") or created.get("RoomId") or ""
        if not room_id:
            raise SmokeFailure("room creation did not return a room id")

        # Start loading, then deliberately never report completion.
        owner.send({"MessageType": "GameStartRequest", "RoomID": room_id})
        started = require_success(
            owner.wait_for(
                lambda message: message.get("MessageType") == "LoadingStartedNotification"
            ),
            "loading start",
        )
        started_room_id = (started.get("RoomInfo") or {}).get("RoomId") or room_id

        failed = owner.wait_for(
            lambda message: message.get("MessageType") == "LoadingFailed"
        )
        if failed.get("Success") is not False:
            raise SmokeFailure(f"LoadingFailed reported success: {failed}")
        if (failed.get("RoomID") or failed.get("RoomId")) != started_room_id:
            raise SmokeFailure("LoadingFailed named a different room than the one that timed out")
        pending = failed.get("PendingPlayers") or []
        if owner_id not in pending:
            raise SmokeFailure(
                f"LoadingFailed did not list the stalled player: {pending}"
            )

        # After the fallback the room must be startable again, otherwise the
        # timeout only moved the stall somewhere else.
        owner.send({"MessageType": "GameStartRequest", "RoomID": started_room_id})
        restarted = owner.wait_for(
            lambda message: message.get("MessageType") in (
                "LoadingStartedNotification",
                "ErrorNotification",
            )
        )
        if restarted.get("MessageType") == "ErrorNotification":
            raise SmokeFailure(
                f"room could not be started again after the timeout: {restarted}"
            )

    print(f"Loading timeout smoke passed: room {room_id}")


def main() -> int:
    parser = argparse.ArgumentParser(description="OpenGS loading timeout smoke client")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=64010)
    parser.add_argument("--timeout", type=float, default=30.0)
    args = parser.parse_args()

    try:
        run(args.host, args.port, args.timeout)
    except (OSError, SmokeFailure) as error:
        print(f"Loading timeout smoke failed: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())