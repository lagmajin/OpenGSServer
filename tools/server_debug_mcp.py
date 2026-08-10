"""Read-only/debug MCP bridge for a running OpenGSServer management endpoint.

The bridge speaks MCP over stdin/stdout and the server's newline-delimited JSON
management protocol over localhost TCP. Credentials are read from
OPENGS_ADMIN_ID / OPENGS_ADMIN_PASSWORD and are never returned by a tool.

Usage:
    python tools/server_debug_mcp.py [--host 127.0.0.1] [--port 50020]
"""

import json
import os
import socket
import sys
import argparse
from typing import Any

PROTOCOL_VERSION = "2025-03-26"
DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 50020
MAX_REQUEST_BYTES = 1_048_576

TOOLS = [
    {
        "name": "server_health",
        "description": "Check whether the OpenGSServer management endpoint is reachable.",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
    {
        "name": "server_status",
        "description": "Read authenticated server status and uptime.",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
    {
        "name": "server_users",
        "description": "List currently logged-in user IDs from the server.",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
    {
        "name": "server_snapshot",
        "description": "Read a compact authenticated status and connected-user snapshot.",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
]


def port_number(value: str) -> int:
    port = int(value)
    if not 1 <= port <= 65535:
        raise argparse.ArgumentTypeError("port must be between 1 and 65535")
    return port


class ServerProtocolError(RuntimeError):
    pass


def receive_json(sock: socket.socket) -> dict[str, Any]:
    buffer = bytearray()
    while True:
        chunk = sock.recv(65536)
        if not chunk:
            raise ServerProtocolError("server closed the management connection")
        buffer.extend(chunk)
        delimiter = buffer.find(b"\n")
        if delimiter < 0:
            continue
        raw = bytes(buffer[:delimiter]).strip()
        if not raw:
            del buffer[: delimiter + 1]
            continue
        try:
            result = json.loads(raw.decode("utf-8"))
        except json.JSONDecodeError as exc:
            raise ServerProtocolError(f"invalid server JSON response: {exc}") from exc
        if not isinstance(result, dict):
            raise ServerProtocolError("server response was not a JSON object")
        return result


def call_management(messages: list[dict[str, Any]], host: str, port: int) -> list[dict[str, Any]]:
    admin_id = os.environ.get("OPENGS_ADMIN_ID", "")
    admin_password = os.environ.get("OPENGS_ADMIN_PASSWORD", "")
    if not admin_id or not admin_password:
        raise ServerProtocolError("OPENGS_ADMIN_ID and OPENGS_ADMIN_PASSWORD are required")

    responses: list[dict[str, Any]] = []
    with socket.create_connection((host, port), timeout=5) as sock:
        sock.settimeout(5)
        responses.append(receive_json(sock))  # ConnectManagementServerSucceeded
        sock.sendall((json.dumps({
            "MessageType": "AdminLoginRequest",
            "AdminID": admin_id,
            "AdminPassword": admin_password,
        }) + "\n").encode("utf-8"))
        login = receive_json(sock)
        responses.append(login)
        if login.get("Success") is not True:
            raise ServerProtocolError(login.get("Message", "admin login failed"))

        for message in messages:
            sock.sendall((json.dumps(message) + "\n").encode("utf-8"))
            response = receive_json(sock)
            responses.append(response)

        sock.sendall(b'{"MessageType":"AdminLogoutRequest"}\n')
        receive_json(sock)
        sock.sendall(b"!\n")
    return responses


def call_tool(name: str, arguments: dict[str, Any], host: str, port: int) -> Any:
    del arguments
    if name == "server_health":
        try:
            with socket.create_connection((host, port), timeout=2) as sock:
                sock.settimeout(2)
                greeting = receive_json(sock)
                sock.sendall(b"!\n")
            return {"reachable": True, "host": host, "port": port, "greeting": greeting}
        except OSError as exc:
            return {"reachable": False, "host": host, "port": port, "error": str(exc)}

    if name == "server_status":
        return call_management([{"MessageType": "ServerStatusRequest"}], host, port)[-1]
    if name == "server_users":
        return call_management([{"MessageType": "GetConnectedUsersRequest"}], host, port)[-1]
    if name == "server_snapshot":
        responses = call_management([
            {"MessageType": "ServerStatusRequest"},
            {"MessageType": "GetConnectedUsersRequest"},
        ], host, port)
        return {"status": responses[-2], "users": responses[-1]}
    raise ServerProtocolError(f"unknown tool: {name}")


def respond(request_id: Any, result: Any = None, error: dict[str, Any] | None = None) -> None:
    payload = {"jsonrpc": "2.0", "id": request_id}
    payload["error" if error else "result"] = error if error else result
    sys.stdout.write(json.dumps(payload, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--port", type=port_number, default=DEFAULT_PORT)
    args = parser.parse_args()
    host, port = args.host, args.port

    for line in sys.stdin:
        if not line.strip():
            continue
        request_id = None
        try:
            if len(line.encode("utf-8")) > MAX_REQUEST_BYTES:
                raise ValueError("MCP request exceeds 1 MiB limit")
            request = json.loads(line)
            request_id = request.get("id")
            method = request.get("method", "")
            params = request.get("params", {})
            if method == "initialize":
                respond(request_id, {
                    "protocolVersion": PROTOCOL_VERSION,
                    "capabilities": {"tools": {}},
                    "serverInfo": {"name": "OpenGSServer Debug MCP", "version": "1.0.0"},
                })
            elif method == "notifications/initialized":
                continue
            elif method == "tools/list":
                respond(request_id, {"tools": TOOLS})
            elif method == "tools/call":
                result = call_tool(params.get("name", ""), params.get("arguments", {}), host, port)
                respond(request_id, {"content": [{"type": "text", "text": json.dumps(result, ensure_ascii=False, indent=2)}]})
            elif method == "shutdown":
                respond(request_id, None)
            elif request_id is not None:
                respond(request_id, error={"code": -32601, "message": f"Method not found: {method}"})
        except Exception as exc:
            if request_id is not None:
                respond(request_id, {"content": [{"type": "text", "text": str(exc)}], "isError": True})


if __name__ == "__main__":
    main()
