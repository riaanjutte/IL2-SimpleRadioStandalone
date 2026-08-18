# IL2-SRS Pilot Roster Server Guide

This guide describes how an IL-2 server can supply assigned callsigns and aircraft/vehicle names to the IL2-SRS Community Edition Pilot Roster.

## Client compatibility

Pilot Roster support is intended to be available on every SRS server that provides compatible roster data. No server-specific hostname or approval will be required.

The SRS server advertises Pilot Roster availability after it successfully reads the configured JSON file. This includes a valid file with an empty `players` array. Clients connected to an older server build, an unconfigured server, or a server whose roster file has never been read successfully will show a message asking users to contact the server owners.

Use a current Community Edition client that supports server-advertised roster availability. These clients enable the Pilot Roster for any SRS server that supplies the data documented here; no Combat Box hostname check or server-specific approval is required.

## How it works

The roster combines two sources:

| Data | Source |
| --- | --- |
| Pilot name and coalition | Connected SRS client |
| Radio 1 and Radio 2 channels | Connected SRS client telemetry |
| Assigned callsign | Server-provided JSON file |
| Aircraft or vehicle | Server-provided JSON file |

The JSON file is not a complete snapshot of radio state. It only maps an in-game player name and coalition to an optional callsign and vehicle. The SRS server reads the file, attaches those assignments to connected clients, and broadcasts changes to SRS clients.

Clients display only pilots on their own coalition. Friendly pilots without an assignment still appear, with `--` in the callsign column. The vehicle column is shown when at least one visible pilot has a vehicle value.

SRS does not assign callsigns or translate IL-2 vehicle IDs into display names. The server administrator must provide those values from a mission-management system, stats service, RCON integration, web application, or another local process. The JSON only needs records for pilots who currently have a callsign or vehicle assignment; SRS supplies the rest of the connected pilot list.

## Requirements

- Use the current IL2-SRS Community Edition server and clients. The server must advertise that it is successfully reading Pilot Roster data, so updating only the client is not sufficient.
- Run a process or script that produces the roster JSON file.
- Give the Windows account running `IL2-SR-Server.exe` read access to that file and its directory or network share.
- Configure `ASSIGNED_CALLSIGNS_JSON_FILE` in the SRS server's `server.cfg`.

No web server or HTTP endpoint is required. The SRS server reads a filesystem path directly.

## JSON format

Use UTF-8 JSON with a top-level `players` array:

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
    },
    {
      "name": "VehicleOnlyExample",
      "coalitionCode": 1,
      "vehicle": "Spitfire Mk.IXe"
    }
  ]
}
```

Additional properties such as `generatedAtUtc`, `coalition`, or `callsignAssignedAtUtc` are allowed and ignored by SRS.

### Field contract

| Field | Required | Rules |
| --- | --- | --- |
| `players` | Yes | Array of player records. A missing, `null`, or empty array produces no assignments. |
| `name` | Yes | Must match the player's IL-2/SRS name. Leading and trailing whitespace is ignored and matching is case-insensitive. Internal punctuation and spacing must match. |
| `coalitionCode` | Yes | Positive integer matching the coalition reported to SRS. Current IL-2 convention is `1` for Allies/Red and `2` for Axis/Blue. |
| `callsign` | No | Assigned tactical callsign. Blank values are treated as unassigned. |
| `vehicle` | No | Aircraft or vehicle display name. Blank values are treated as unavailable. |

Each record must contain at least one nonblank `callsign` or `vehicle`. Records with neither are ignored. If the same normalized player name and coalition appear more than once, the last record wins.

Keep callsigns and vehicle names concise. SRS trims their surrounding whitespace and displays them in uppercase, but does not otherwise validate or shorten them.

## Configure the SRS server

Stop the SRS server and add the setting under `[General Settings]` in the `server.cfg` used by that server instance.

Absolute local path:

```ini
[General Settings]
ASSIGNED_CALLSIGNS_JSON_FILE=C:\IL2-SRS\data\pilot-roster.json
```

UNC network path:

```ini
[General Settings]
ASSIGNED_CALLSIGNS_JSON_FILE=\\fileserver\il2-data\pilot-roster.json
```

Relative path:

```ini
[General Settings]
ASSIGNED_CALLSIGNS_JSON_FILE=data\pilot-roster.json
```

A relative path is resolved from the directory containing `IL2-SR-Server.exe`, not from the location of a custom configuration passed with `-cfg=`.

The setting accepts local and UNC filesystem paths. It does not download HTTP or HTTPS URLs. If the SRS server runs as a Windows service or scheduled task, test access using that service account rather than an interactive administrator account.

Restart `IL2-SR-Server.exe` after changing `server.cfg`.

## Producing updates safely

The SRS server checks the configured file approximately once per second. Changed callsigns and vehicles are broadcast to connected clients without restarting SRS.

Use this publishing sequence:

1. Generate the complete JSON into a temporary file in the same directory.
2. Parse the temporary file to confirm it is valid JSON.
3. Atomically replace the live file.
4. Keep the last valid live file when the upstream roster source is unavailable.

Avoid rewriting the live file gradually. Although SRS retains the previous assignments when it encounters malformed or partially written JSON, atomic replacement prevents avoidable warnings and stale data.

File behavior is important:

| Condition | SRS behavior |
| --- | --- |
| Valid file | Replaces the in-memory assignment map and advertises Pilot Roster availability to clients. |
| Valid file with empty `players` | Clears all assignments. |
| Missing file | Clears all assignments, marks the Pilot Roster unavailable, and logs a missing-file message. |
| Invalid JSON or read error | Keeps the previous assignment map and logs a warning. |

## Validate the integration

Validate the JSON from PowerShell:

```powershell
$roster = Get-Content 'C:\IL2-SRS\data\pilot-roster.json' -Raw | ConvertFrom-Json
$roster.players | Format-Table name, coalitionCode, callsign, vehicle
```

Then verify the complete path:

1. Start the SRS server and inspect `serverlog.txt` in the server directory.
2. Confirm there is no `Assigned callsigns JSON file not found` or `Unable to read assigned callsigns JSON file` message.
3. Connect an SRS client using a player name and coalition present in the JSON.
4. Change that player's callsign in the JSON and publish the file again.
5. Confirm the server logs that assigned callsign updates were broadcast.
6. Using a current Community Edition client, open **Show Pilot Roster** and confirm the callsign, vehicle, and radio channels.

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| Client says Pilot Roster data is unavailable | Update both SRS client and server, configure `ASSIGNED_CALLSIGNS_JSON_FILE`, and verify the server can read valid JSON at that path. |
| Every callsign is `--` | Wrong path, unreadable file, names do not match, or coalition codes are wrong. |
| Only some assignments appear | Compare each JSON `name` with the exact in-game player name and check its coalition. |
| Assignments stop updating | The latest JSON is invalid or the producer can no longer replace the file. Check `serverlog.txt`. |
| Assignments disappear | The live file was missing temporarily or an empty roster was published. |
| Vehicle column is absent | No friendly visible pilot currently has a nonblank `vehicle`. |
| Enemy pilots are absent | Expected behavior; the Pilot Roster filters to the local player's coalition. |
| Radio channels are absent or `--` | Those values come from each connected client's telemetry, not from the roster JSON. |

## Data and privacy considerations

The normal Pilot Roster UI filters entries to the local player's coalition. Assignment metadata is nevertheless distributed as part of SRS client synchronization, so the JSON must not contain private or security-sensitive information.

The Active Squad Ops summary does not require additional JSON fields. It is derived from friendly connected player names and their tuned channels.

## Implementation checklist

- [ ] Current Community Edition server installed
- [ ] Roster producer generates valid UTF-8 JSON
- [ ] Player names match IL-2 names
- [ ] Coalition codes match SRS coalition values
- [ ] `ASSIGNED_CALLSIGNS_JSON_FILE` added under `[General Settings]`
- [ ] SRS server account can read the configured path
- [ ] Producer publishes with atomic replacement
- [ ] `serverlog.txt` contains no roster file errors
- [ ] Clients updated to a build that supports server-advertised Pilot Roster availability
- [ ] Callsign and vehicle updates tested with connected clients
