## Host Console

DedicatedServerMod supports two host console transports:

- TCP console for direct socket-based remote administration
- stdio host console for platforms that inject commands through process stdin and capture logs from stdout/stderr

The optional web panel is a separate loopback-only surface intended for local operators on the server host. Hosted panels should usually keep it disabled and rely on stdio instead.

For native Windows server installs, the packaged `start_server.bat` enables `--stdio-console` by default. That lets operators type DedicatedServerMod commands directly into the MelonLoader console without opening a separate TCP console session.

### Recommended mode

Use:

```toml
[tcpConsole]
stdioConsoleMode = 'Auto'
```

`Auto` starts the stdio host console only when stdin is redirected. That matches hosted environments while avoiding accidental takeover of the local interactive console on normal desktop launches.

### Pterodactyl and Wings

Pterodactyl-style hosts manage the game server as a process whose lifecycle and console are exposed through a control plane. In that environment the server should behave well as a line-oriented stdin/stdout process rather than assuming telnet-style administration or a visible local desktop console.

Reference: [Pterodactyl Wings README](https://github.com/pterodactyl/wings)

### Startup guidance

For panel environments, prefer startup flags that keep Unity and MelonLoader output on stdout:

```text
-logFile -
```

That lets the panel capture the game log stream directly. Hidden desktop console settings are not a substitute for stdout logging in hosted environments.

If you launch the executable manually instead of using the packaged batch file, add `--stdio-console` to force the stdio host console on:

```batch
"Schedule I.exe" --batchmode --nographics --dedicated-server --stdio-console
```

### Behavior

- `stdin` lines are parsed with the same command grammar used by TCP console and admin relay paths
- quoted arguments are preserved consistently
- stdio mode does not print prompts
- stdio mode does not echo typed input
- stdio command replies are written to a single hosted-console reply stream (`stderr` in the current implementation), so informational replies such as `help` and `serverinfo` remain visible on hosted panels
- warnings and errors keep `[WARN]` and `[ERR]` prefixes in console-like transports
- EOF on stdin detaches the stdio reader and does not shut down the server
- `exit` and `quit` remain TCP-session commands only and are not special in stdio mode
- TCP console sessions do not have a server-side idle read timeout, so long-lived `nc` or telnet sessions stay usable until the client or network closes them
- TCP console sessions are command sessions, not live stdout mirrors. Use `logs [lines]` or `tail [lines]` to print a bounded snapshot from the current MelonLoader log when a hosted panel proxies the TCP console instead of forwarding process logs.

### When to use TCP instead

Use the TCP console when you want an explicitly separate remote admin surface with password protection and prompt-driven sessions. Use stdio when the host already owns the process console.

### Exposure guidance

- `tcpConsolePort` defaults to `4050` and uses TCP.
- The default bind address is `127.0.0.1`, which keeps the console local-only.
- If you change `tcpConsoleBindAddress` to `0.0.0.0` or another non-loopback address, the console becomes reachable on that interface and you must open or forward `tcpConsolePort` separately.
- If you expose the TCP console beyond localhost, require a password and treat it as a trusted admin surface, not a public service.
- The built-in web panel does not support LAN/public bind addresses. If you need a browser UI from another machine, use a hosted panel such as Pterodactyl or build an authenticated web panel on top of the TCP console or another supported control surface.

### Vehicles and inventory grants

TCP console, stdio, the web panel, and Pterodactyl's console bridge can run:

```text
spawnvehicle Devotek shitbox
give Devotek baggie 20
give "Player With Spaces" baggie 1
```

The target must be online, authenticated, and spawned. Use `listplayers` to find names, or supply a Steam ID or client ID. A vehicle spawns four metres ahead and one metre above the target as a player-owned vehicle. Invalid vehicle/item codes, ambiguous names, and invalid quantities are rejected. Quantities range from 1 to 1000 and default to 1.

`give` uses the existing `exec_console` message handled by S1DS clients. It reports that the grant was queued, not that inventory delivery succeeded. Inventory capacity and item-specific behavior remain controlled by the game. Updating the server enables these commands; a matching S1DS client with the existing command relay is sufficient.

In-game operators can still run `spawnvehicle shitbox` and `give baggie 20` for themselves. Targeted in-game grants require quantity: `give Devotek baggie 20`. Permissions remain `console.command.spawnvehicle` and `console.command.give`; host-console authority does not require assigning operator status to the target.

### Restart announcements

The fork adds `broadcast <message>` to the shared command pipeline, available through TCP, stdio, the web panel, and the in-game admin console. Pterodactyl console input and scheduled **Send command** tasks can use it directly:

```text
broadcast "Server restarting in 5 minutes."
```

Both the server and each player's client need this fork's announcement-capable build, matching their game branch and runtime. Original S1DS 1.1.0 clients do not display this message. The client shows an audible ten-second game notification and records `[SERVER ANNOUNCEMENT]` in its MelonLoader log. Messages contain 1–240 characters on one line. Quote text containing apostrophes or other quote characters according to the shared console grammar.

The `server.broadcast` permission is granted to the built-in administrator group and inherited by operators. Host consoles already run with console authority. A transport reply reports messages accepted for sending; it does not confirm that a player's UI displayed them. The loopback host and unauthenticated/disconnected peers are excluded, and a broadcast is rejected while messaging is not ready.

For restarts at midnight, 04:00, 08:00, 12:00, 16:00 and 20:00, set the schedule cron to `55 3,7,11,15,19,23 * * *` in the panel's configured timezone. Enable **Only when server is online**. Add these ordered tasks (delays are relative to the preceding task):

| Task | Action | Payload | Delay |
| --- | --- | --- | --- |
| 1 | Send command | `broadcast "Server restarting in 5 minutes."` | 0 seconds |
| 2 | Send command | `broadcast "Server restarting in 1 minute."` | 240 seconds |
| 3 | Send command | `broadcast "Server restarting in 30 seconds."` | 30 seconds |
| 4 | Send command | `save` | 0 seconds |
| 5 | Send power action | `restart` | 30 seconds |

Pterodactyl [queues each following task using that task's delay](https://github.com/pterodactyl/panel/blob/develop/app/Jobs/Schedule/RunTaskJob.php). The full sequence takes five minutes. Saving remains thirty seconds before the power restart; this is a grace period, not verification that every game save file was flushed. The previous deployment restarted thirty seconds past the hour; this sequence restarts at the hour. Queue latency can shift actual execution.

Install matching mods and verify a manual `broadcast "Restart announcements enabled."` with a connected player before enabling the warning schedule. A standard Source RCON client is not compatible with S1DS's text TCP console protocol; Pterodactyl's existing console bridge is sufficient and port 4050 can remain loopback-only.
