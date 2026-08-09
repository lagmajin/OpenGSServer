# OpenGSServer Debug MCP

`server_debug_mcp.py` exposes a small MCP interface for inspecting a running
OpenGSServer during local development. It talks to the existing management TCP
endpoint and does not embed an AI model in the server process.

## Setup

Set the management credentials in the environment used to launch the bridge:

```powershell
$env:OPENGS_ADMIN_ID = "your-admin-id"
$env:OPENGS_ADMIN_PASSWORD = "your-admin-password"
python tools/server_debug_mcp.py --host 127.0.0.1 --port 50020
```

The MCP bridge uses stdio for JSON-RPC and defaults to `127.0.0.1:50020`.
`server_health` only checks reachability. The other tools authenticate for each
request and currently provide read-only status and user inspection:

- `server_health`
- `server_status`
- `server_users`
- `server_snapshot`

Keep the management endpoint bound to localhost when using this bridge. The
bridge intentionally does not expose shutdown, arbitrary code execution, or
data mutation tools.
