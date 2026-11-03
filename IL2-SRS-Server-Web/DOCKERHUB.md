# IL2-SRS Server

The server for [IL2 SimpleRadio Standalone (Community Edition)](https://github.com/riaanjutte/IL2-SimpleRadioStandalone), the radio for IL-2 Sturmovik: Great Battles and IL-2 Korea, with a browser-based admin UI and a REST API.

Existing IL2-SRS clients connect to it exactly as they do to the Windows server; nothing changes for players.

> **Preview.** This server is new and is being tested before it replaces the Windows server window.

## Quick start

```sh
docker run -d --name il2-srs-server --restart unless-stopped \
  -e SRS_ADMIN_PASSWORD='CHANGE-ME-min-8-chars' \
  -p 6002:6002/tcp -p 6002:6002/udp -p 127.0.0.1:8080:8080 \
  -v il2-srs-data:/data \
  asken/il2-srs-server:preview
```

Open http://localhost:8080 and log in with the admin password. Clients connect to port `6002`.

## Features

- Admin UI: server health, connected clients (mute, kick, ban), settings, channel names, bans with reason and expiry, event log, API keys.
- Settings, bans, event log and API keys stored in SQLite in the `/data` volume.
- Any setting can be fixed with an `SRS_<SETTING>` environment variable; it is then locked in the UI.
- REST API under `/api/v1` with read and write API keys, an OpenAPI document and an interactive API reference at `/scalar`.
- An existing `server.cfg` and `banned.txt` in `/data` are imported on first start.
- Alpine-based, runs as a non-root user, built-in health check, no telemetry.

## Ports and volume

| | |
| --- | --- |
| `6002/tcp`, `6002/udp` | SRS clients (control and voice). Publish both. |
| `8080/tcp` | Admin UI and API, plain HTTP. Keep it on localhost or put a reverse proxy with HTTPS in front. |
| `/data` | Database, logs and login keys. Use a named volume. |

## Configuration

| Variable | Default | |
| --- | --- | --- |
| `SRS_ADMIN_PASSWORD` | – | Admin password, at least 8 characters. Required. |
| `SRS_ADMIN_PASSWORD_FILE` | – | Read the password from a file (Docker secrets). |
| `SRS_SERVER_PORT` | `6002` | SRS port. Change the published ports to match. |
| `SRS_SERVER_NAME` | host name | Server name in integration messages (`server.name`). |
| `SRS_PUBLIC_ADMIN_URL` | – | Admin UI address integration messages can link to (`server.adminUrl`). |
| `SRS_<SETTING>` | – | Fix any server setting, e.g. `SRS_COALITION_AUDIO_SECURITY=true`. |
| `SRS_EVENT_RETENTION_DAYS` | `30` | Keep events for this many days. |
| `SRS_EVENT_MAX_ROWS` | `100000` | Keep at most this many events. |

## Tags

- `preview`: the latest preview build.
- `<version>-preview`: a specific preview build, e.g. `1.0.4.12-preview`.

Images are `linux/amd64`.

## Links

- Source, issues and releases: [riaanjutte/IL2-SimpleRadioStandalone](https://github.com/riaanjutte/IL2-SimpleRadioStandalone)
- Server documentation: [IL2-SRS-Server-Web/README.md](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/blob/master/IL2-SRS-Server-Web/README.md) and [srsforil2.com](https://srsforil2.com)
- Original SimpleRadio Standalone by Ciribob: [ciribob/DCS-SimpleRadioStandalone](https://github.com/ciribob/DCS-SimpleRadioStandalone)

Licensed under the [GNU GPL v3](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/blob/master/LICENSE).
