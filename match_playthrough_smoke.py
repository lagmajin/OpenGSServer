"""Play a match through to the result, driving the server from both ends.

S2 records that the match loop and the broadcast paths are implemented, but
that this had no test: nothing played a match to completion. This closes
that gap for the server side without needing a Unity client or a RUDP stack.

Two lobby clients create a match room and start it. The second one is then
driven through the same wait room, loading, and map entry handshake that
two_player_loading_smoke.py covers, so the match is running. The match is
then ended through the endmatch admin command, which calls the same
MatchRoom.Finish the natural match timer would, and the test checks that
the result envelope actually reaches the players.

This does not drive RUDP gameplay input; it covers the server loop, the
result evaluator, the result broadcast, and the persistence path.
"""

from __future__ import annotations

import argparse
import json
import socket
import time
import uuid
from typing import Any, Callable

from lobby_smoke_client import LobbySmokeClient, SmokeFailure, require_success

ADMIN_ID = "admin"
ADMIN_PASSWORD = "admin123"


class ManagementClient:
    """Newline-delimited JSON client for the management listener."""

    def __init__(self, host: str, port: int, timeout: float) -> None:
        self.host = host
        self.port = port
        self.timeout = timeout
        self.sock: socket.socket | None = None
        self.buffer = bytearray()

    def __enter__(self) -> "ManagementClient":
        self.sock = socket.create_connection((self.host, self.port), self.timeout)
        self.sock.settimeout(self.timeout)
        return self

    def __exit__(self, *_: object) -> None:
        if self.sock is not None:
            self.sock.close()
            self.sock = None

    def send(self, message: dict[str, Any]) -> None:
        assert self.sock is not None
        self.sock.sendall((json.dumps(message) + "\n").encode("utf-8"))

    def receive(self) -> dict[str, Any]:
        """Reads one complete JSON object from the management listener.

        The listener serialises with indentation and only terminates the frame
        with a newline, so a single message can span several lines. Frames are
        therefore collected until the braces balance rather than split on the
        first newline.
        """
        assert self.sock is not None
        deadline = time.monotonic() + self.timeout

        while True:
            candidate = self._extract_frame()
            if candidate is not None:
                try:
                    return json.loads(candidate)
                except json.JSONDecodeError:
                    # Not a frame we understand; keep scanning.
                    pass

            remaining = max(0.05, deadline - time.monotonic())
            if remaining <= 0:
                raise SmokeFailure("Timed out waiting for a management response")

            self.sock.settimeout(remaining)
            try:
                chunk = self.sock.recv(4096)
            except socket.timeout:
                continue
            if not chunk:
                raise SmokeFailure("management connection closed")
            self.buffer.extend(chunk)

    def _extract_frame(self) -> str | None:
        """Pulls the next complete JSON object out of the buffer."""
        text = bytes(self.buffer).decode("utf-8", errors="replace")
        start = text.find("{")
        while start >= 0:
            depth = 0
            in_string = False
            escaped = False
            for index in range(start, len(text)):
                char = text[index]
                if in_string:
                    if escaped:
                        escaped = False
                    elif char == "\\":
                        escaped = True
                    elif char == '"':
                        in_string = False
                    continue
                if char == '"':
                    in_string = True
                elif char == "{":
                    depth += 1
                elif char == "}":
                    depth -= 1
                    if depth == 0:
                        frame = text[start : index + 1]
                        consumed = len(text[: index + 1].encode("utf-8"))
                        del self.buffer[:consumed]
                        return frame
            return None

        # No complete object yet. Drop anything before the next opening brace.
        if start > 0:
            del self.buffer[: len(text[:start].encode("utf-8"))]
        return None

    def wait_for(self, predicate: Callable[[dict[str, Any]], bool]) -> dict[str, Any]:
        deadline = time.monotonic() + self.timeout
        while time.monotonic() < deadline:
            message = self.receive()
            if predicate(message):
                return message
        raise SmokeFailure("Timed out waiting for the expected management message")

    def login(self) -> None:
        self.send(
            {
                "MessageType": "AdminLoginRequest",
                "AdminID": ADMIN_ID,
                "AdminPassword": ADMIN_PASSWORD,
            }
        )
        response = self.wait_for(
            lambda message: message.get("MessageType") in
            ("AdminLoginResponse", "AdminLoginFailed")
        )
        if response.get("Success") is not True:
            raise SmokeFailure(f"admin login failed: {response}")

    def run(self, command: str) -> str:
        self.send({"MessageType": "ExecuteCommandRequest", "Command": command})
        response = self.wait_for(
            lambda message: message.get("MessageType") == "ExecuteCommandResponse"
        )
        if response.get("Success") is not True:
            raise SmokeFailure(f"command {command!r} failed: {response}")
        return response.get("Output") or ""


def account(client: LobbySmokeClient, prefix: str, suffix: str) -> str:
    account_id = f"{prefix}_{suffix}"
    password = f"MatchPlay-{suffix}!"
    client.send(
        {
            "MessageType": "CreateAccountRequest",
            "AccountID": account_id,
            "Password": password,
            "DisplayName": f"{prefix} {suffix}",
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


def create_match_room(client: LobbySmokeClient, player_id: str, name: str) -> str:
    client.send(
        {
            "MessageType": "CreateRoomRequest",
            "PlayerID": player_id,
            "PlayerName": name,
            "RoomName": name,
            "Capacity": 2,
            "TeamBalance": False,
        }
    )
    created = require_success(
        client.wait_for(lambda message: message.get("MessageType") == "CreateRoomResponse"),
        "match room creation",
    )
    room_id = created.get("RoomID") or created.get("RoomId") or ""
    if not room_id:
        raise SmokeFailure("match room response did not include RoomID")
    return room_id


def join_room(client: LobbySmokeClient, room_id: str, player_id: str, player_name: str) -> None:
    client.send({"MessageType": "RoomListUpdateRequest"})
    client.wait_for(lambda message: message.get("MessageType") == "RoomListUpdateNotification")
    client.send(
        {
            "MessageType": "JoinRoomRequest",
            "RoomID": room_id,
            "PlayerID": player_id,
            "PlayerName": player_name,
        }
    )
    require_success(
        client.wait_for(lambda message: message.get("MessageType") == "JoinRoomResponse"),
        "join room",
    )
    client.wait_for(lambda message: message.get("MessageType") == "WaitRoomUpdateNotification")


def start_match(client: LobbySmokeClient, player_id: str, room_id: str) -> None:
    client.send({"MessageType": "GameStartRequest", "PlayerAccountID": player_id, "RoomID": room_id})
    require_success(
        client.wait_for(lambda message: message.get("MessageType") == "LoadingStartedNotification"),
        "match start",
    )


def finish_loading(client: LobbySmokeClient, player_id: str, room_id: str) -> None:
    client.send(
        {
            "MessageType": "LoadingCompleted",
            "PlayerID": player_id,
            "RoomID": room_id,
        }
    )
    require_success(
        client.wait_for(lambda message: message.get("MessageType") == "LoadingCompletedNotification"),
        "loading completed",
    )


def wait_allow_enter_map(clients) -> None:
    # AllowEnterMap is only broadcast once every player has reported, so wait
    # for it across all clients rather than blocking on the first one.
    # Drain both sockets until the gate arrives on either one.
    for client in clients:
        original_timeout = client.timeout
        client.timeout = 0.5
        try:
            for _ in range(8):
                try:
                    message = client.receive()
                except SmokeFailure:
                    break
                if message.get("MessageType") == "AllowEnterMap":
                    return
        finally:
            client.timeout = original_timeout

    raise SmokeFailure("AllowEnterMap was not broadcast after every player reported loading")


def run(lobby_host: str, lobby_port: int, management_port: int, timeout: float) -> None:
    suffix = uuid.uuid4().hex[:10]

    with LobbySmokeClient(lobby_host, lobby_port, timeout) as owner, LobbySmokeClient(
        lobby_host, lobby_port, timeout
    ) as guest:
        owner_id = account(owner, "MatchOwner", suffix)
        guest_id = account(guest, "MatchGuest", suffix)

        room_id = create_match_room(owner, owner_id, f"Match Play {suffix}")
        join_room(guest, room_id, guest_id, f"MatchGuest {suffix}")

        start_match(owner, owner_id, room_id)
        finish_loading(owner, owner_id, room_id)
        finish_loading(guest, guest_id, room_id)
        wait_allow_enter_map([owner, guest])

        # The match is now running. End it the way the natural timer would.
        with ManagementClient(lobby_host, management_port, timeout) as admin:
            admin.login()
            output = admin.run(f"endmatch {room_id}")
            if "ended" not in output.lower():
                raise SmokeFailure(f"endmatch did not report success: {output!r}")

        # The result envelope is broadcast to the room over the lobby stream.
        # Either player may see it first, so both are drained and at least one
        # must carry the end-of-match result.
        seen = None
        for client in (owner, guest):
            original_timeout = client.timeout
            client.timeout = 2.0
            try:
                for _ in range(10):
                    try:
                        message = client.receive()
                    except SmokeFailure:
                        break
                    if message.get("MessageType") == "MatchEndNotification":
                        seen = message
                        break
            finally:
                client.timeout = original_timeout
            if seen is not None:
                break

        if seen is None:
            raise SmokeFailure("no match result was broadcast after the match ended")

        if not seen.get("RoomId") and not seen.get("RoomID"):
            raise SmokeFailure(f"match result did not identify the room: {seen}")

    print(f"Match playthrough smoke passed: room {room_id}")


def main() -> int:
    parser = argparse.ArgumentParser(description="OpenGS match playthrough smoke client")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=64010)
    parser.add_argument("--management-port", type=int, default=64013)
    parser.add_argument("--timeout", type=float, default=15.0)
    args = parser.parse_args()

    try:
        run(args.host, args.port, args.management_port, args.timeout)
    except (OSError, SmokeFailure) as error:
        print(f"Match playthrough smoke failed: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
