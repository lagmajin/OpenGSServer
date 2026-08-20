"""Verify that map entry is allowed only after every room player loads."""

from __future__ import annotations

import argparse
import time
import uuid

from lobby_smoke_client import LobbySmokeClient, SmokeFailure, require_success


def account(client: LobbySmokeClient, suffix: str) -> str:
    account_id = f"smoke2_{suffix}"
    password = f"Smoke2-{suffix}!"
    client.send(
        {
            "MessageType": "CreateAccountRequest",
            "AccountID": account_id,
            "Password": password,
            "DisplayName": f"Smoke2 {suffix}",
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


def wait_loading(client: LobbySmokeClient, player_id: str) -> None:
    started = require_success(
        client.wait_for(lambda message: message.get("MessageType") == "LoadingStartedNotification"),
        "loading started",
    )
    if not (started.get("RoomInfo") or {}).get("Players"):
        raise SmokeFailure("LoadingStartedNotification did not include room players")


def assert_no_allow(client: LobbySmokeClient, timeout: float = 0.5) -> None:
    original_timeout = client.timeout
    client.timeout = timeout
    try:
        while True:
            try:
                message = client.receive()
            except SmokeFailure as error:
                if "Timed out" in str(error):
                    return
                raise
            if message.get("MessageType") == "AllowEnterMap":
                raise SmokeFailure("AllowEnterMap arrived before every player completed loading")
            client.pending.append(message)
    finally:
        client.timeout = original_timeout


def run_smoke(host: str, port: int, timeout: float) -> None:
    suffix = uuid.uuid4().hex[:10]
    owner_suffix = f"owner_{suffix}"
    guest_suffix = f"guest_{suffix}"

    with LobbySmokeClient(host, port, timeout) as owner, LobbySmokeClient(host, port, timeout) as guest:
        owner_id = account(owner, owner_suffix)
        guest_id = account(guest, guest_suffix)
        owner.send(
            {
                "MessageType": "CreateRoomRequest",
                "PlayerID": owner_id,
                "PlayerName": owner_suffix,
                "RoomName": f"Two Player Smoke {suffix}",
                "Capacity": 2,
                "GameMode": "TeamDeathMatch",
            }
        )
        created = require_success(
            owner.wait_for(lambda message: message.get("MessageType") == "CreateRoomResponse"),
            "room creation",
        )
        room_id = created.get("RoomID") or created.get("RoomId") or ""
        if not room_id:
            raise SmokeFailure("Two-player room did not include RoomID")

        guest.send(
            {
                "MessageType": "JoinRoomRequest",
                "RoomID": room_id,
                "PlayerID": guest_id,
                "PlayerName": guest_suffix,
            }
        )
        require_success(
            guest.wait_for(lambda message: message.get("MessageType") == "JoinRoomResponse"),
            "guest join",
        )
        room_list_update = owner.wait_for(
            lambda message: message.get("MessageType") == "RoomListUpdateNotification"
            and any(
                (room.get("RoomId") or room.get("RoomID")) == room_id
                and room.get("PlayerCount") == 2
                for room in message.get("Rooms", [])
            )
        )
        listed_room = next(
            (
                room
                for room in room_list_update.get("Rooms", [])
                if (room.get("RoomId") or room.get("RoomID")) == room_id
            ),
            None,
        )
        if listed_room is None or listed_room.get("PlayerCount") != 2:
            raise SmokeFailure("lobby room list was not refreshed after guest join")
        if "Players" in listed_room:
            raise SmokeFailure("lobby room list broadcast leaked player details")
        owner.wait_for(lambda message: message.get("MessageType") == "WaitRoomUpdateNotification")

        owner.send(
            {
                "MessageType": "WaitRoomKickPlayer",
                "RoomID": room_id,
                "PlayerID": guest_id,
                "Reason": "smoke kick",
            }
        )
        guest.wait_for(lambda message: message.get("MessageType") == "WaitRoomKickPlayer")
        owner.wait_for(
            lambda message: message.get("MessageType") == "RoomListUpdateNotification"
            and any(
                (room.get("RoomId") or room.get("RoomID")) == room_id
                and room.get("PlayerCount") == 1
                for room in message.get("Rooms", [])
            )
        )
        guest.send(
            {
                "MessageType": "JoinRoomRequest",
                "RoomID": room_id,
                "PlayerID": guest_id,
                "PlayerName": guest_suffix,
            }
        )
        require_success(
            guest.wait_for(lambda message: message.get("MessageType") == "JoinRoomResponse"),
            "guest rejoin after kick",
        )
        owner.wait_for(
            lambda message: message.get("MessageType") == "RoomListUpdateNotification"
            and any(
                (room.get("RoomId") or room.get("RoomID")) == room_id
                and room.get("PlayerCount") == 2
                for room in message.get("Rooms", [])
            )
        )
        owner.wait_for(lambda message: message.get("MessageType") == "WaitRoomUpdateNotification")

        guest.send(
            {
                "MessageType": "WaitRoomSettingsChange",
                "RoomID": room_id,
                "Settings": {"Map": "IceValley"},
            }
        )
        denied_settings = guest.wait_for(
            lambda message: message.get("MessageType") == "RoomSettingChanged"
        )
        if denied_settings.get("Success") is not False:
            raise SmokeFailure(f"non-owner settings change was accepted: {denied_settings}")

        guest.send(
            {
                "MessageType": "WaitRoomPlayerReady",
                "RoomID": room_id,
                "PlayerID": owner_id,
            }
        )
        spoofed_ready = guest.wait_for(
            lambda message: message.get("MessageType") == "WaitRoomUpdateNotification"
        )
        ready_players = (spoofed_ready.get("RoomInfo") or {}).get("Players") or []
        owner_ready = next((player for player in ready_players if player.get("Id") == owner_id), None)
        if owner_ready is None or owner_ready.get("IsReady") is not False:
            raise SmokeFailure("guest changed the owner's ready state")

        guest.send(
            {
                "MessageType": "GameStartRequest",
                "PlayerAccountID": owner_id,
                "RoomID": room_id,
            }
        )
        denied_start = guest.wait_for(lambda message: message.get("MessageType") == "ErrorNotification")
        if denied_start.get("Success") is not False:
            raise SmokeFailure("guest started the match by spoofing the owner's ID")

        owner.send(
            {
                "MessageType": "GameStartRequest",
                "PlayerAccountID": owner_id,
                "RoomID": room_id,
            }
        )
        wait_loading(owner, owner_id)
        wait_loading(guest, guest_id)

        owner.send({"MessageType": "LoadingCompleted", "PlayerID": owner_id, "RoomID": room_id})
        owner.wait_for(lambda message: message.get("MessageType") == "LoadingCompletedNotification")
        assert_no_allow(owner)
        assert_no_allow(guest)
        guest_owner_completion = guest.wait_for(
            lambda message: message.get("MessageType") == "LoadingCompletedNotification"
        )
        if guest_owner_completion.get("PlayerID") != owner_id:
            raise SmokeFailure("guest did not receive the owner's loading completion")

        guest.send({"MessageType": "LoadingCompleted", "PlayerID": owner_id, "RoomID": room_id})
        spoofed_loading = guest.wait_for(
            lambda message: message.get("MessageType") == "LoadingCompletedNotification"
        )
        if spoofed_loading.get("PlayerID") != guest_id:
            raise SmokeFailure("guest spoofed the owner's loading completion")

        owner_allow = require_success(
            owner.wait_for(lambda message: message.get("MessageType") == "AllowEnterMap"),
            "owner map entry",
        )
        guest_allow = require_success(
            guest.wait_for(lambda message: message.get("MessageType") == "AllowEnterMap"),
            "guest map entry",
        )
        for label, message in (("owner", owner_allow), ("guest", guest_allow)):
            if message.get("Approved") is not True or message.get("RoomID") != room_id:
                raise SmokeFailure(f"{label} AllowEnterMap was missing approval or RoomID")

        print(f"Two-player loading smoke passed: {room_id}")


def main() -> int:
    parser = argparse.ArgumentParser(description="OpenGS two-player loading gate smoke client")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=60000)
    parser.add_argument("--timeout", type=float, default=10.0)
    args = parser.parse_args()
    try:
        run_smoke(args.host, args.port, args.timeout)
    except (OSError, SmokeFailure) as error:
        print(f"Two-player loading smoke failed: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
