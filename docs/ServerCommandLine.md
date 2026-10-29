# Server command-line options

`OpenGSServer` validates its startup arguments before creating listeners or
opening the database. Use `--help` to print the current option list.

```powershell
OpenGSServer.exe --lobby-port 60000 --match-tcp-port 60001 --match-udp-port 63000 --management-port 50020 --public-ip game.example.com
```

| Option | Default | Description |
| --- | ---: | --- |
| `--lobby-port <port>` | 60000 | Lobby TCP listener |
| `--match-tcp-port <port>` | 60001 | Match TCP listener |
| `--match-udp-port <port>` | 63000 | Match UDP listener |
| `--management-port <port>` | 50020 | Management TCP listener |
| `--public-ip <host>` | empty | Host name or IP advertised to remote match clients |
| `--no-console` | off | Do not accept interactive commands; stop with Ctrl+C |
| `--version`, `-v` | | Print the server version and exit |

All ports must be in the range 1–65535. The three TCP listeners must use
different ports. When `--public-ip` is supplied, the value is returned in the
match-server information response so clients can connect to the UDP endpoint
from outside the server host. If omitted, clients use their configured server
address. The value must be an IP address or DNS host name. Invalid options
return exit code `2`; attempting to start a second server instance returns
exit code `1`.

## Interactive commands

After startup, type `help` to list the available management commands or
`help <command>` for one command's usage. Arguments can be wrapped in single or
double quotes, for example `addguild "Night Owls"`. Within an argument, use a
backslash to escape spaces, quotes, or a backslash itself. Unknown commands
offer a close spelling when one exists, and invalid argument counts show the
command-specific usage.
