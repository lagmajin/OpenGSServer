"""Verify that a dropped lobby connection does not leave a ghost occupant.

S1 requires lobby state to have a single backend authority that survives a
reconnect. This test drops the TCP connection without logging out, then
reconnects and checks that the server released the previous session state
instead of keeping the old player in the lobby or in the wait room.
"""

from __future__ import annotations

import argparse
import time
import uuid

from lobby_smoke_client import LobbySmokeClient, SmokeFailure, require_success


def create_and_login(
    client: LobbySmokeClient, account_id: str, password: str, display_name: str
) -> str:
    client.send(
        {
            "MessageType": "CreateAccountRequest",
            "AccountID": account_id,
            "Password": password,
            "DisplayName": display_name,
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


def rooms(client: LobbySmokeClient) -> list[dict]:
    client.send({"MessageType": "RoomListUpdateRequest"})
    return require_success(
        client.wait_for(lambda message: message.get("MessageType") == "RoomListUpdateNotification"),
        "room list",
    ).get("Rooms", [])


def run(host: str, port: int, timeout: float) -> None:
    suffix = uuid.uuid4().hex[:10]
    account_id = f"reconnect_{suffix}"
    password = f"Reconnect-{suffix}!"
    display_name = f"Reconnect {suffix}"
    room_name = f"Reconnect Room {suffix}"

    # First session: log in, create a room, then drop the socket without logout.
    with LobbySmokeClient(host, port, timeout) as first:
        player_id = create_and_login(first, account_id, password, display_name)

        first.send(
            {
                "MessageType": "CreateRoomRequest",
                "PlayerID": player_id,
                "PlayerName": display_name,
                "RoomName": room_name,
                "Capacity": 2,
            }
        )
        created = require_success(
            first.wait_for(lambda message: message.get("MessageType") == "CreateRoomResponse"),
            "room creation",
        )
        room_id = created.get("RoomID") or created.get("RoomId") or ""
        if not room_id:
            raise SmokeFailure("room creation did not return a room id")

        if not any((room.get("RoomID") or room.get("RoomId")) == room_id for room in rooms(first)):
            raise SmokeFailure("created room was missing from the room list")

        # Hard drop: shutdown the socket so the server sees a dead connection.
        if first.sock is not None:
            first.sock.shutdown(2)

    # Give the server's stale session sweep a moment to notice the dead socket.
    time.sleep(2.0)

    # Second session: the same account must be able to log in again, and the
    # room the dropped session created must no longer be held by it.
    with LobbySmokeClient(host, port, timeout) as second:
        reconnected_id = create_and_login(second, account_id, password, display_name)
        if reconnected_id != player_id:
            raise SmokeFailure(
                f"reconnect changed the player id: {player_id} -> {reconnected_id}"
            )

        listed = rooms(second)
        still_listed = any(
            (room.get("RoomID") or room.get("RoomId")) == room_id for room in listed
        )
        if still_listed:
            raise SmokeFailure(
                "room created by the dropped session survived; the player is a ghost"
            )

        # The account session must still work, proving the drop did not
        # invalidate the credentials along with the connection.
        second.send({"MessageType": "ShopStateRequest", "PlayerID": reconnected_id})
        require_success(
            second.wait_for(lambda message: message.get("MessageType") == "ShopStateResponse"),
            "shop state after reconnect",
        )

    print(f"Reconnect smoke passed: {account_id} / room {room_id or '<none>'}")


def main() -> int:
    parser = argparse.ArgumentParser(description="OpenGS reconnect smoke client")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=64010)
    parser.add_argument("--timeout", type=float, default=10.0)
    args = parser.parse_args()

    try:
        run(args.host, args.port, args.timeout)
    except (OSError, SmokeFailure) as error:
        print(f"Reconnect smoke failed: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())