"""Repeatable lobby-to-match smoke test for the real OpenGS TCP protocol.

The Unity lobby client uses ``JS`` + compact JSON + ASCII unit separator
(``0x1f``) frames. This script intentionally exercises that wire contract
without requiring Unity.
"""

from __future__ import annotations

import argparse
import json
import socket
import time
import uuid
from typing import Any, Callable


FRAME_PREFIX = b"JS"
FRAME_SEPARATOR = b"\x1f"


class SmokeFailure(RuntimeError):
    pass


class LobbySmokeClient:
    def __init__(self, host: str, port: int, timeout: float) -> None:
        self.host = host
        self.port = port
        self.timeout = timeout
        self.sock: socket.socket | None = None
        self.buffer = bytearray()
        self.pending: list[dict[str, Any]] = []

    def __enter__(self) -> "LobbySmokeClient":
        self.sock = socket.create_connection((self.host, self.port), self.timeout)
        self.sock.settimeout(self.timeout)
        return self

    def __exit__(self, exc_type: object, exc: object, tb: object) -> None:
        if self.sock is not None:
            self.sock.close()
            self.sock = None

    def send(self, message: dict[str, Any]) -> None:
        if self.sock is None:
            raise SmokeFailure("TCP socket is not connected")
        payload = json.dumps(message, separators=(",", ":")).encode("utf-8")
        self.sock.sendall(FRAME_PREFIX + payload + FRAME_SEPARATOR)
        print(f"-> {message.get('MessageType', '<unknown>')}")

    def receive(self) -> dict[str, Any]:
        if self.sock is None:
            raise SmokeFailure("TCP socket is not connected")

        deadline = time.monotonic() + self.timeout
        while time.monotonic() < deadline:
            separator_index = self.buffer.find(FRAME_SEPARATOR)
            if separator_index >= 0:
                frame = bytes(self.buffer[:separator_index])
                del self.buffer[: separator_index + len(FRAME_SEPARATOR)]
                if frame.startswith(FRAME_PREFIX):
                    frame = frame[len(FRAME_PREFIX) :]
                try:
                    message = json.loads(frame.decode("utf-8"))
                except (UnicodeDecodeError, json.JSONDecodeError) as error:
                    raise SmokeFailure(f"Invalid server frame: {error}") from error
                if not isinstance(message, dict):
                    raise SmokeFailure("Server frame was not a JSON object")
                print(f"<- {message.get('MessageType', '<unknown>')}")
                return message

            remaining = max(0.05, deadline - time.monotonic())
            self.sock.settimeout(remaining)
            try:
                chunk = self.sock.recv(4096)
            except socket.timeout:
                continue
            if not chunk:
                raise SmokeFailure("Lobby TCP connection closed")
            self.buffer.extend(chunk)

        raise SmokeFailure("Timed out waiting for a server frame")

    def wait_for(self, predicate: Callable[[dict[str, Any]], bool]) -> dict[str, Any]:
        deadline = time.monotonic() + self.timeout
        for index, message in enumerate(self.pending):
            if predicate(message):
                return self.pending.pop(index)

        while time.monotonic() < deadline:
            message = self.receive()
            if predicate(message):
                return message
            self.pending.append(message)
        raise SmokeFailure("Timed out waiting for the expected server message")


def require_success(message: dict[str, Any], context: str) -> dict[str, Any]:
    if message.get("Success") is False:
        raise SmokeFailure(f"{context} failed: {message.get('Error') or message.get('ErrorMessage') or message}")
    return message


def run_smoke(host: str, port: int, timeout: float) -> None:
    suffix = uuid.uuid4().hex[:10]
    account_id = f"smoke_{suffix}"
    password = f"Smoke-{suffix}!"
    display_name = f"Smoke {suffix}"
    room_id = ""

    with LobbySmokeClient(host, port, timeout) as client:
        client.send(
            {
                "MessageType": "ShopStateRequest",
                "PlayerID": "someone-else",
            }
        )
        unauthenticated_shop = client.wait_for(
            lambda message: message.get("MessageType") == "ShopStateResponse"
        )
        if unauthenticated_shop.get("Success") is not False:
            raise SmokeFailure("Unauthenticated client accessed another player's shop state")

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
        player_id = login.get("PlayerID") or login.get("GlobalUserId") or account_id

        client.send(
            {
                "MessageType": "LoginRequest",
                "id": account_id,
                "pass": password,
            }
        )
        repeated_login = client.wait_for(
            lambda message: message.get("MessageType") == "LoginResponse"
        )
        if repeated_login.get("Success") is not False:
            raise SmokeFailure("An authenticated session was allowed to log in again")

        client.send(
            {
                "MessageType": "CreateRoomRequest",
                "PlayerID": account_id,
                "PlayerName": display_name,
                "RoomName": f"Logout Cleanup Room {suffix}",
                "Capacity": 1,
            }
        )
        logout_room = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "CreateRoomResponse"),
            "logout cleanup room creation",
        )
        logout_room_id = logout_room.get("RoomID") or logout_room.get("RoomId") or ""
        if not logout_room_id:
            raise SmokeFailure("Logout cleanup room did not include RoomID")

        client.send({"MessageType": "LogoutRequest"})
        logout = client.wait_for(
            lambda message: message.get("MessageType") == "LogoutSuccessful"
        )
        if logout.get("Success") is not True:
            raise SmokeFailure("LogoutRequest did not clear the authenticated session")

        client.send(
            {
                "MessageType": "LoginRequest",
                "id": account_id,
                "pass": password,
            }
        )
        login_after_logout = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "LoginResponse"),
            "login after logout",
        )
        player_id = login_after_logout.get("PlayerID") or login_after_logout.get("GlobalUserId") or account_id

        client.send({"MessageType": "RoomListUpdateRequest"})
        client.wait_for(
            lambda message: message.get("MessageType") == "RoomListUpdateNotification"
            and not any(
                (room.get("RoomID") or room.get("RoomId")) == logout_room_id
                for room in message.get("Rooms") or []
            )
        )
        client.send({"MessageType": "RoomListUpdateRequest"})
        require_success(
            client.wait_for(lambda message: message.get("MessageType") == "RoomListUpdateNotification"),
            "room list",
        )

        client.send(
            {
                "MessageType": "CreateRoomRequest",
                "PlayerID": player_id,
                "PlayerName": display_name,
                "RoomName": f"Smoke Room {suffix}",
                "Capacity": 2,
                "GameMode": "TeamDeathMatch",
                "Map": "AuroraClassic",
                "TeamBalance": False,
                "Password": f"Room-{suffix}!",
            }
        )
        created = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "CreateRoomResponse"),
            "room creation",
        )
        room_id = created.get("RoomID") or created.get("RoomId") or ""
        if not room_id:
            raise SmokeFailure("CreateRoomResponse did not include RoomID")
        room_info = created.get("RoomInfo") or {}
        returned_game_mode = created.get("GameMode") or room_info.get("GameMode")
        if returned_game_mode != "TeamDeathMatch":
            raise SmokeFailure(
                f"CreateRoomResponse returned GameMode={returned_game_mode!r}, expected 'TeamDeathMatch'"
            )
        returned_owner_id = room_info.get("OwnerId")
        if not returned_owner_id or room_info.get("OwnerID") != returned_owner_id:
            raise SmokeFailure("CreateRoomResponse returned inconsistent OwnerId/OwnerID values")
        returned_map = created.get("Map") or room_info.get("Map")
        if returned_map != "AuroraClassic":
            raise SmokeFailure(
                f"CreateRoomResponse returned Map={returned_map!r}, expected 'AuroraClassic'"
            )
        if created.get("TeamBalance") is not False:
            raise SmokeFailure("CreateRoomResponse did not preserve TeamBalance=false")
        if room_info.get("HasPassword") is not True:
            raise SmokeFailure("CreateRoomResponse did not report the configured password")
        if created.get("HasPassword") is not True:
            raise SmokeFailure("CreateRoomResponse did not expose top-level HasPassword")

        client.send({"MessageType": "RoomListUpdateRequest"})
        room_list = client.wait_for(
            lambda message: message.get("MessageType") == "RoomListUpdateNotification"
            and any(
                (room.get("RoomId") or room.get("RoomID")) == room_id
                for room in message.get("Rooms") or []
            )
        )
        listed_room = next(
            (
                room
                for room in room_list.get("Rooms", [])
                if (room.get("RoomId") or room.get("RoomID")) == room_id
            ),
            None,
        )
        if listed_room is None:
            raise SmokeFailure("RoomListUpdateNotification did not include the created room")
        if "Players" in listed_room:
            raise SmokeFailure("RoomListUpdateNotification leaked full player details")
        if listed_room.get("PlayerCount") != 1 or listed_room.get("HasPassword") is not True:
            raise SmokeFailure("RoomListUpdateNotification returned an incomplete room summary")

        client.send(
            {
                "MessageType": "CreateRoomRequest",
                "PlayerID": player_id,
                "PlayerName": display_name,
                "RoomName": f"Duplicate Smoke Room {suffix}",
            }
        )
        duplicate_room = client.wait_for(lambda message: message.get("MessageType") == "CreateRoomResponse")
        if duplicate_room.get("Success") is not False:
            raise SmokeFailure("A player was allowed to create a second wait room")

        client.send(
            {
                "MessageType": "JoinRoomRequest",
                "RoomID": room_id,
                "PlayerID": player_id,
                "PlayerName": display_name,
                "Password": f"Room-{suffix}!",
            }
        )
        joined = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "JoinRoomResponse"),
            "room join",
        )
        joined_info = joined.get("RoomInfo") or {}
        if joined_info.get("OwnerID") != joined_info.get("OwnerId"):
            raise SmokeFailure("JoinRoomResponse returned inconsistent OwnerId/OwnerID values")
        if joined.get("HasPassword") is not True:
            raise SmokeFailure("JoinRoomResponse did not expose top-level HasPassword")
        updated = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "WaitRoomUpdateNotification"),
            "wait room update",
        )
        updated_info = updated.get("RoomInfo") or {}
        if updated_info.get("OwnerID") != updated_info.get("OwnerId"):
            raise SmokeFailure("WaitRoomUpdateNotification returned inconsistent OwnerId/OwnerID values")

        updated_password = f"Updated-{suffix}!"
        client.send(
            {
                "MessageType": "WaitRoomSettingsChange",
                "RoomID": room_id,
                "Settings": {
                    "Map": "IceValley",
                    "TeamBalance": True,
                    "Password": updated_password,
                },
            }
        )
        setting_changed = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "RoomSettingChanged"),
            "room settings change",
        )
        changed_info = setting_changed.get("RoomInfo") or {}
        if changed_info.get("Map") != "IceValley":
            raise SmokeFailure("RoomSettingChanged did not apply Map")
        if changed_info.get("TeamBalance") is not True:
            raise SmokeFailure("RoomSettingChanged did not apply TeamBalance=true")
        if changed_info.get("HasPassword") is not True:
            raise SmokeFailure("RoomSettingChanged did not apply Password")
        changed_settings = setting_changed.get("Settings") or {}
        expected_settings = {
            "RoomName": f"Smoke Room {suffix}",
            "Capacity": 2,
            "GameMode": "TeamDeathMatch",
            "TeamBalance": True,
            "Map": "IceValley",
            "HasPassword": True,
            "RoomId": room_id,
        }
        for key, expected in expected_settings.items():
            if changed_settings.get(key) != expected:
                raise SmokeFailure(
                    f"RoomSettingChanged Settings returned {key}={changed_settings.get(key)!r}, "
                    f"expected {expected!r}"
                )
        if "Password" in changed_settings:
            raise SmokeFailure("RoomSettingChanged Settings leaked the room password")
        client.wait_for(lambda message: message.get("MessageType") == "WaitRoomUpdateNotification")

        client.send(
            {
                "MessageType": "PlayerReadyRequest",
                "PlayerID": player_id,
                "RoomID": room_id,
            }
        )
        ready_update = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "WaitRoomUpdateNotification"),
            "player ready",
        )
        ready_players = (ready_update.get("RoomInfo") or {}).get("Players") or []
        if not any(player.get("Id") == player_id and player.get("IsReady") is True for player in ready_players):
            raise SmokeFailure("WaitRoomUpdateNotification did not mark the player ready")

        client.send(
            {
                "MessageType": "PlayerUnready",
                "PlayerID": player_id,
                "RoomID": room_id,
            }
        )
        unready_update = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "WaitRoomUpdateNotification"),
            "player unready",
        )
        unready_players = (unready_update.get("RoomInfo") or {}).get("Players") or []
        if not any(player.get("Id") == player_id and player.get("IsReady") is False for player in unready_players):
            raise SmokeFailure("WaitRoomUpdateNotification did not clear the player's ready state")

        chat_message = f"Smoke chat {suffix}"
        client.send(
            {
                "MessageType": "LobbyChatRequest",
                "PlayerID": player_id,
                "PlayerName": display_name,
                "RoomID": room_id,
                "Message": chat_message,
            }
        )
        chat = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "LobbyChatNotification"),
            "wait room chat",
        )
        if chat.get("Message") != chat_message or chat.get("RoomID") != room_id:
            raise SmokeFailure("LobbyChatNotification did not preserve the room or message")

        client.send(
            {
                "MessageType": "LeaveRoomRequest",
                "RoomID": room_id,
                "PlayerID": player_id,
            }
        )
        require_success(
            client.wait_for(lambda message: message.get("MessageType") == "LeaveRoomResponse"),
            "room leave",
        )
        deleted = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "RoomDeleted"),
            "room deletion",
        )
        if deleted.get("RoomID") != room_id and deleted.get("RoomId") != room_id:
            raise SmokeFailure("RoomDeleted did not identify the closed room")

        client.send({"MessageType": "RoomListUpdateRequest"})
        rooms_after_leave = require_success(
            client.wait_for(
                lambda message: message.get("MessageType") == "RoomListUpdateNotification"
                and not any(
                    (room.get("RoomID") or room.get("RoomId")) == room_id
                    for room in message.get("Rooms") or []
                )
            ),
            "room list after leave",
        )

        client.send(
            {
                "MessageType": "CreateRoomRequest",
                "PlayerID": player_id,
                "PlayerName": display_name,
                "RoomName": f"Smoke Match Room {suffix}",
                "Capacity": 2,
                "GameMode": "TeamDeathMatch",
                "Map": "AuroraClassic",
                "TeamBalance": False,
            }
        )
        match_room = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "CreateRoomResponse"),
            "match room creation",
        )
        room_id = match_room.get("RoomID") or match_room.get("RoomId") or ""
        if not room_id:
            raise SmokeFailure("Match room response did not include RoomID")

        client.send(
            {
                "MessageType": "MatchServerInfoRequest",
                "RoomID": room_id,
            }
        )
        match_info = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "MatchServerInfoResponse"),
            "match server info",
        )
        if not isinstance(match_info.get("Port"), int) or match_info.get("Port") <= 0:
            raise SmokeFailure("MatchServerInfoResponse did not include a valid TCP port")
        if not isinstance(match_info.get("UdpPort"), int) or match_info.get("UdpPort") <= 0:
            raise SmokeFailure("MatchServerInfoResponse did not include a valid UDP port")
        if not match_info.get("UdpToken"):
            raise SmokeFailure("MatchServerInfoResponse did not include a UDP authorization token")
        if match_info.get("RoomID") != room_id and match_info.get("RoomId") != room_id:
            raise SmokeFailure("MatchServerInfoResponse did not preserve RoomID")

        client.send(
            {
                "MessageType": "GameStartRequest",
                "PlayerAccountID": player_id,
                "RoomID": room_id,
            }
        )
        require_success(
            client.wait_for(lambda message: message.get("MessageType") == "LoadingStartedNotification"),
            "match start",
        )

        client.send(
            {
                "MessageType": "LoadingProgress",
                "PlayerID": player_id,
                "RoomID": room_id,
                "Progress": "0.5",
            }
        )
        progress = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "LoadingProgressNotification"),
            "loading progress",
        )
        if progress.get("PlayerID") != player_id or progress.get("Progress") != 0.5:
            raise SmokeFailure("LoadingProgressNotification did not preserve the normalized progress")

        client.send(
            {
                "MessageType": "LoadingCompleted",
                "PlayerID": player_id,
                "RoomID": room_id,
            }
        )
        completed = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "LoadingCompletedNotification"),
            "loading completed",
        )
        if completed.get("PlayerID") != player_id:
            raise SmokeFailure("LoadingCompletedNotification did not identify the player")

        allow_enter_map = require_success(
            client.wait_for(lambda message: message.get("MessageType") == "AllowEnterMap"),
            "allow enter map",
        )
        if allow_enter_map.get("Approved") is not True:
            raise SmokeFailure("AllowEnterMap did not approve map entry")
        if allow_enter_map.get("RoomID") != room_id:
            raise SmokeFailure("AllowEnterMap did not preserve RoomID")

        print(f"Smoke test passed: {account_id} -> {room_id}")


def main() -> int:
    parser = argparse.ArgumentParser(description="OpenGS real lobby-to-match smoke client")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=60000)
    parser.add_argument("--timeout", type=float, default=10.0)
    args = parser.parse_args()

    try:
        run_smoke(args.host, args.port, args.timeout)
    except (OSError, SmokeFailure) as error:
        print(f"Smoke test failed: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
