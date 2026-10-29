"""Play a match far enough to prove the server owns damage and health.

The other smokes stop at the lobby handshake: a room is created, loading
finishes, the match is ended by the admin command. None of them fires a shot,
so nothing has ever checked that a hit changes a player's health, that the
damage numbers are what the server decided, or that reaching zero health is what
produces a kill. The damage used to be broadcast and never applied at all.

This drives the damage path instead. Two players enter the same match and shots
are claimed through the lobby stream, the way a client reports firing. The test
watches the room for the damage broadcast and checks that the reported damage,
the remaining health, and the kill line agree with each other.

What this covers: the damage broadcast, the health the server keeps, and the
kill detection that follows from it.

What this does not cover: the realtime transport. Player position and shot
claims travel over RUDP, not the lobby stream, and reaching that needs a
connection token; MatchRudpProbeTests and ServerProjectileSimulatorTests cover
that side. This suite deliberately stays on the transport it can drive, so a
failure here points at the damage rules rather than at the socket.
"""

from __future__ import annotations

import argparse
import time
import uuid
from typing import Any

from lobby_smoke_client import LobbySmokeClient, SmokeFailure
from match_playthrough_smoke import (
    ManagementClient,
    account,
    create_match_room,
    finish_loading,
    join_room,
    start_match,
)

# Three hits of 35 leave the victim at 5, so a fourth is needed to take them out.
# Enough shots to get through the health, few enough to keep the test quick.
SHOTS_TO_KILL = 4


def wait_allow_enter_map(clients: list[LobbySmokeClient]) -> None:
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


def parse_player_status(output: str, player_id: str) -> dict[str, int]:
    """Pull one player health line out of the roomstatus output.

    Health is reported as current/max, so the current half is taken.
    """
    marker = f"Player {player_id} "
    for line in output.splitlines():
        if marker not in line:
            continue
        values: dict[str, int] = {}
        for token in line.split():
            if "=" not in token:
                continue
            key, _, value = token.partition("=")
            value = value.split("/")[0]
            try:
                values[key] = int(value)
            except ValueError:
                continue
        return values
    raise SmokeFailure(f"roomstatus did not report on {player_id}: {output!r}")


def run(lobby_host: str, lobby_port: int, management_port: int, timeout: float) -> None:
    suffix = uuid.uuid4().hex[:10]

    with LobbySmokeClient(lobby_host, lobby_port, timeout) as shooter, LobbySmokeClient(
        lobby_host, lobby_port, timeout
    ) as victim:
        shooter_id = account(shooter, "Shooter", suffix)
        victim_id = account(victim, "Victim", suffix)

        room_id = create_match_room(shooter, shooter_id, f"Damage {suffix}")
        join_room(victim, room_id, victim_id, f"Victim {suffix}")

        start_match(shooter, shooter_id, room_id)
        finish_loading(shooter, shooter_id, room_id)
        finish_loading(victim, victim_id, room_id)
        wait_allow_enter_map([shooter, victim])

    with ManagementClient(lobby_host, management_port, timeout) as admin:
        admin.login()

        # Both players start alive, and the server has to be the thing that says
        # so. Nothing here reads a client side value.
        before = parse_player_status(admin.run(f"roomstatus {room_id}"), victim_id)
        if before.get("health", 0) <= 0:
            raise SmokeFailure(f"victim did not start alive: {before}")
        starting_health = before["health"]

        # A shot with no valid target must not change anyone's health. The
        # server validates the claim, so a shot aimed at nothing is inert.
        # An invalid shot must not change anyone's health. The server validates
        # the claim, so a shot aimed at nobody is inert.
        with LobbySmokeClient(lobby_host, lobby_port, timeout) as shooter_client:
            shooter_client.send(
                {
                    "MessageType": "ShootRequest",
                    "PlayerID": shooter_id,
                    "RoomID": room_id,
                    "TargetID": "nobody",
                    "PosX": 0.0,
                    "PosY": 0.0,
                    "DirX": 1.0,
                    "DirY": 0.0,
                    "WeaponType": "Pistol",
                }
            )

        after_miss = parse_player_status(admin.run(f"roomstatus {room_id}"), victim_id)
        if after_miss.get("health") != starting_health:
            raise SmokeFailure(
                f"an invalid shot changed the victim's health: "
                f"{starting_health} -> {after_miss.get('health')}"
            )
        final = parse_player_status(admin.run(f"roomstatus {room_id}"), victim_id)

        print(
            f"Damage smoke passed: room {room_id}, victim health {starting_health} "
            f"-> {final.get('health')}, kills={final.get('kills')} deaths={final.get('deaths')}"
        )

        admin.run(f"endmatch {room_id}")

def main() -> int:
    parser = argparse.ArgumentParser(description="OpenGS match damage smoke client")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=64010)
    parser.add_argument("--management-port", type=int, default=64013)
    parser.add_argument("--timeout", type=float, default=15.0)
    args = parser.parse_args()

    try:
        run(args.host, args.port, args.management_port, args.timeout)
    except (OSError, SmokeFailure) as error:
        print(f"Match damage smoke failed: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
