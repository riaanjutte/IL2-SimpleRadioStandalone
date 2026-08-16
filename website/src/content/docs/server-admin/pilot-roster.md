---
title: Pilot Roster integration
description: Supply callsigns and vehicle names to the SRS Pilot Roster from a server-side JSON file.
---

The Pilot Roster combines connected-client radio state with optional callsign and aircraft assignments supplied by the server owner.

| Data | Source |
| --- | --- |
| Pilot name and coalition | Connected SRS client |
| Radio 1 and Radio 2 channels | Connected SRS client telemetry |
| Assigned callsign | Server-provided JSON |
| Aircraft or vehicle | Server-provided JSON |

## JSON format

Create UTF-8 JSON with a top-level `players` array:

```json
{
  "generatedAtUtc": "2026-07-15T18:30:00Z",
  "players": [
    {
      "name": "=TBAS=Mayhem-1",
      "coalitionCode": 1,
      "callsign": "MANIAC-1",
      "vehicle": "P-51D-15"
    },
    {
      "name": "JG27_PilotTwo",
      "coalitionCode": 2,
      "callsign": "RAVEN-2",
      "vehicle": "Bf 109 G-14"
    }
  ]
}
```

`name` matching is case-insensitive and surrounding whitespace is ignored. `coalitionCode` must match the player's current SRS coalition. Callsign and vehicle are optional, but each record needs at least one of them.

## Configure the server

Stop the SRS server and add the path under `[General Settings]`:

```ini
[General Settings]
ASSIGNED_CALLSIGNS_JSON_FILE=C:\IL2-SRS\data\pilot-roster.json
```

Local, relative, and UNC filesystem paths are supported. HTTP and HTTPS URLs are not. Relative paths are resolved from the directory containing `IL2-SR-Server.exe`.

## Publish updates safely

The server checks the file approximately once per second. Generate a complete temporary file, validate it, then atomically replace the live file. Do not gradually rewrite the file while the server is reading it.

| File condition | Server behavior |
| --- | --- |
| Valid JSON | Replaces assignments and advertises roster availability |
| Valid empty `players` array | Clears assignments |
| Missing file | Clears assignments and marks the roster unavailable |
| Invalid JSON or read error | Keeps the previous valid assignments and logs a warning |

## Validate

1. Inspect `serverlog.txt` for roster-file warnings.
2. Connect a client whose player name and coalition appear in the JSON.
3. Open **Show Pilot Roster**.
4. Change a callsign, publish the file, and confirm the client updates without restarting SRS.

The complete reference guide remains available as [Pilot-Roster-Server-Guide.md](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/blob/master/Pilot-Roster-Server-Guide.md).

:::caution[Do not publish sensitive data]
Assignment metadata is distributed during SRS synchronization. Do not place private infrastructure details, credentials, or security-sensitive information in the JSON.
:::
