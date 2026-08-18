---
title: Configuration reference
description: Current server.cfg sections, defaults, accepted values, and restart requirements.
---

The server creates missing settings on startup while preserving existing values. Prefer the server UI for routine changes. Stop and restart the server after editing `server.cfg` manually because the file is not monitored for external changes.

## Complete example

```ini
[Server Settings]
SERVER_PORT=6002
CLIENT_EXPORT_FILE_PATH=clients-list.json
CHECK_FOR_BETA_UPDATES=false
SERVER_UI_THEME=White
UPNP_ENABLED=false

[General Settings]
CLIENT_EXPORT_ENABLED=false
COALITION_AUDIO_SECURITY=false
IRL_RADIO_TX=false
SPECTATORS_AUDIO_DISABLED=false
SHOW_TUNED_COUNT=true
GLOBAL_LOBBY_FREQUENCIES=248.22
SHOW_TRANSMITTER_NAME=true
SECOND_RADIO_ENABLED=true
CHANNEL_LIMIT=5
SHOW_SQUAD_CHANNEL_LABELS=false
RADIO_COLLISION_EFFECTS=false
PRIORITY_TRANSMITTER_NAMES=Axis Command,Allies Command,Axis Airfield,Allies Airfield
ASSIGNED_CALLSIGNS_JSON_FILE=

[Channel Names]
1=Command
2=Tower/ATC
```

The generated file can also contain an empty `[External AWACS Mode Settings]` section and reserved DServer RCon keys. Those integrations are not required by the current server and should not be relied upon as active features.

## Server Settings

These values control the local server process rather than in-game radio behavior.

| Setting | Default | Purpose | Restart after manual edit |
| --- | --- | --- | --- |
| `SERVER_PORT` | `6002` | TCP control and UDP voice port | Yes |
| `CLIENT_EXPORT_FILE_PATH` | `clients-list.json` | Output path for the optional client export | Yes |
| `CHECK_FOR_BETA_UPDATES` | `false` | Include beta releases in the server update check | Yes |
| `SERVER_UI_THEME` | `White` | Local `White` or `Dark` server-window theme | No when changed in UI |
| `UPNP_ENABLED` | `false` | Request automatic TCP and UDP port mappings from a compatible router | Yes |

Manual firewall rules and port forwarding are generally more predictable than UPnP on a dedicated host.

## General Settings

| Setting | Default | Purpose |
| --- | --- | --- |
| `CLIENT_EXPORT_ENABLED` | `false` | Write connected-client state to the configured JSON export every five seconds |
| `COALITION_AUDIO_SECURITY` | `false` | Separate radio traffic by IL-2 coalition |
| `IRL_RADIO_TX` | `false` | Prevent receiving on the selected AM/FM radio while transmitting on it |
| `SPECTATORS_AUDIO_DISABLED` | `false` | Restrict spectator access to normal radio traffic |
| `SHOW_TUNED_COUNT` | `true` | Let clients display how many users are tuned to a radio |
| `GLOBAL_LOBBY_FREQUENCIES` | `248.22` | Comma-separated AM frequencies available globally |
| `SHOW_TRANSMITTER_NAME` | `true` | Let clients display the current transmitter's name |
| `SECOND_RADIO_ENABLED` | `true` | Make Radio 2 available to supporting clients |
| `CHANNEL_LIMIT` | `5` | Highest selectable channel; the UI provides 5, 10, 15, 20, or 25 |
| `SHOW_SQUAD_CHANNEL_LABELS` | `false` | Append a majority squad tag to eligible channel names above channel 2 |
| `RADIO_COLLISION_EFFECTS` | `false` | Signal overlapping same-channel transmissions to supporting clients |
| `PRIORITY_TRANSMITTER_NAMES` | See below | Comma-separated exact pilot names protected during collisions |
| `ASSIGNED_CALLSIGNS_JSON_FILE` | blank | Local, relative, or UNC path to Pilot Roster assignments |

The default priority list is:

```text
Axis Command,Allies Command,Axis Airfield,Allies Airfield
```

Most General Settings changed in the UI are synchronized live. Restart after manually changing `PRIORITY_TRANSMITTER_NAMES` or `ASSIGNED_CALLSIGNS_JSON_FILE`. The contents of an already configured roster file are refreshed automatically.

## Channel Names

Define one optional name per channel under `[Channel Names]`:

```ini
[Channel Names]
1=Command
2=Tower/ATC
9=Squad Operations
25=Guard
```

- Valid channel numbers are 1 through 25.
- Names are trimmed and limited to 32 characters.
- Blank entries are ignored and fall back to `CHN n`.
- Names change the client display and spoken announcement, not the underlying channel or frequency.

When a friendly RCI is active, current clients temporarily show channel 1 as `RCI Control`. The configured channel 1 name returns when the RCI is no longer active.

## Generated and reserved values

Do not manually add or distribute these as normal operator settings:

- `PILOT_ROSTER_DATA_AVAILABLE` is calculated by the server and advertised to clients. It is not a persistent configuration switch.
- `DSERVER_RCON_ADDRESS`, `DSERVER_RCON_USERNAME`, and `DSERVER_RCON_PASSWORD` are reserved for unfinished integration work. Current server operation does not depend on them.

Server-only roster paths and reserved credentials are filtered from synchronized client settings. They can still be present in your local file, so redact `server.cfg` before sharing it for support.

## Invalid configuration recovery

If the file cannot be parsed, the server logs the error, writes a `server.cfg.bak` backup, and regenerates defaults. After recovery, compare the backup with the new file and restore only known-good values. Do not replace the regenerated file wholesale without identifying the invalid entry.
