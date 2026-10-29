"""Smoke test for the mission-room create/list/leave/disconnect lifecycle."""

from __future__ import annotations

import argparse
import time
import uuid

from lobby_smoke_client import LobbySmokeClient, SmokeFailure, require_success


def login(client: LobbySmokeClient, account_id: str, password: str) -> None:
    client.wait_for(lambda m: m.get("MessageType") == "ConnectServerSuccessful")
    client.send({
        "MessageType": "CreateAccountRequest",
        "AccountID": account_id,
        "Password": password,
        "DisplayName": account_id,
    })
    client.wait_for(lambda m: m.get("MessageType") == "CreateAccountResponse")
    client.send({"MessageType": "LoginRequest", "id": account_id, "pass": password})
    require_success(client.wait_for(lambda m: m.get("MessageType") == "LoginResponse"), "login")


def rooms(client: LobbySmokeClient, predicate=None) -> list[dict]:
    client.send({"MessageType": "RoomListUpdateRequest"})
    return require_success(
        client.wait_for(
            lambda m: m.get("MessageType") == "RoomListUpdateNotification"
            and (predicate is None or predicate(m.get("Rooms", [])))
        ),
        "room list",
    ).get("Rooms", [])


def run(host: str, port: int, timeout: float) -> None:
    suffix = uuid.uuid4().hex[:10]
    account = f"mission_smoke_{suffix}"
    password = "mission-smoke-password"
    room_name = f"Mission Lifecycle {suffix}"

    with LobbySmokeClient(host, port, timeout) as client:
        login(client, account, password)
        client.send({
            "MessageType": "CreateRoomRequest",
            "IsMission": True,
            "MissionId": "SmokeMission",
            "RoomName": room_name,
            "Capacity": 2,
        })
        created = require_success(client.wait_for(lambda m: m.get("MessageType") == "CreateRoomResponse"), "mission create")
        if created.get("IsMission") is not True:
            raise SmokeFailure("mission create was routed as a regular room")
        room_id = created.get("RoomID") or created.get("RoomId")
        if not room_id:
            raise SmokeFailure("mission create did not return a room id")

        listed = rooms(client, lambda current: any(
            (r.get("RoomID") or r.get("RoomId")) == room_id for r in current
        ))
        if not any((r.get("RoomID") or r.get("RoomId")) == room_id and r.get("IsMission") is True for r in listed):
            raise SmokeFailure("mission room was missing from the room list")

        client.send({"MessageType": "LeaveRoomRequest", "RoomID": room_id})
        require_success(client.wait_for(lambda m: m.get("MessageType") == "LeaveRoomResponse"), "mission leave")
        rooms(client, lambda current: not any(
            (r.get("RoomID") or r.get("RoomId")) == room_id for r in current
        ))

        client.send({
            "MessageType": "CreateRoomRequest",
            "IsMission": True,
            "MissionId": "SmokeMissionDisconnect",
            "RoomName": f"{room_name} Disconnect",
            "Capacity": 2,
        })
        disconnected = require_success(client.wait_for(lambda m: m.get("MessageType") == "CreateRoomResponse"), "mission recreate")
        disconnected_id = disconnected.get("RoomID") or disconnected.get("RoomId")
        if not disconnected_id:
            raise SmokeFailure("mission recreate did not return a room id")
        if client.sock is not None:
            client.sock.shutdown(2)

    time.sleep(1.0)
    observer_account = f"mission_observer_{suffix}"
    with LobbySmokeClient(host, port, timeout) as observer:
        login(observer, observer_account, password)
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if not any((r.get("RoomID") or r.get("RoomId")) == disconnected_id for r in rooms(observer)):
                break
            time.sleep(0.1)
        else:
            raise SmokeFailure("disconnected mission room was not cleaned up")

    print(f"Mission room lifecycle smoke passed: {room_id} / {disconnected_id}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=64010)
    parser.add_argument("--timeout", type=float, default=10.0)
    args = parser.parse_args()
    try:
        run(args.host, args.port, args.timeout)
    except (OSError, SmokeFailure) as error:
        print(f"Mission room lifecycle smoke failed: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
