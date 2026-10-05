## Commands

Console commands on dedicated servers are controlled by the permissions system, not by hard-coded role lists in `server_config.toml`.

- `permissions.toml` defines who can open the console and which nodes they can execute
- `console.open` gates console access
- most ordinary commands evaluate against `console.command.<commandword>`
- framework management commands also expose dedicated nodes such as `server.save`, `player.kick`, and `permissions.reload`

The built-in group defaults are:

- `default`: help
- `support`: server info
- `moderator`: player moderation commands
- `administrator`: config reload and save
- `operator`: permission mutation and shutdown

You can change that layout by editing `permissions.toml`.

## Common Commands

- `spawnvehicle <player_name_or_id> <vehicle_code>`: spawn a player-owned vehicle near a connected player
- `give <player_name_or_id> <item_id> [quantity]`: request an inventory grant for a connected player (default 1, maximum 1000)

- `help`: show command help
- `serverinfo`: print server status
- `save`: trigger a manual save
- `reloadconfig`: reload `server_config.toml`
- `reloadpermissions`: reload `permissions.toml`
- `shutdown [reason]`: gracefully stop the server
- `settime <HHMM>`: set the authoritative server time of day
- `settimescale <scale>`: set the authoritative Unity time scale
- `listplayers`: list connected players
- `kick <player_name_or_id> [reason]`: disconnect a player
- `ban <player_name_or_id> [reason]`: ban a player
- `unban <steamid>`: remove a ban
- `group <list|assign|unassign> ...`: manage permission groups
- `perm <info|grant|deny|revoke|tempgrant> ...`: inspect and mutate direct permission nodes
- `op`, `deop`, `admin`, `deadmin`: compatibility wrappers for built-in staff groups

`settime` expects a four-digit game clock value such as `0800`, `1330`, or `1800`. `settimescale` changes Unity's global time scale and should be treated as an operator-only diagnostic or recovery tool, not normal gameplay tuning.

### Vehicles and inventory grants

Host consoles require an explicit player target. Both commands accept a unique name or partial name, Steam ID, or client ID; quote names containing spaces. Ambiguous names are rejected. The target must be authenticated, connected, and spawned. In-game callers retain `spawnvehicle <vehicle_code>` and `give <item_id> [quantity]` for themselves. To give to another player in-game, always include quantity: `give <player_name_or_id> <item_id> <quantity>`.

These commands use `console.command.spawnvehicle` and `console.command.give`, included in the operator group's `console.command.*`. Giving relays the game's existing inventory command to the selected client; a queued reply confirms transport acceptance, not inventory delivery. The client's game handles inventory capacity and item behavior. Vehicle spawning runs on the server. No new client protocol is required.

See [Permissions](configuration/permissions.md) for file structure, group defaults, and rule precedence.
