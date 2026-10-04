---
title: Web server (Docker and Windows service)
description: Run the IL2-SRS server in Docker or as a Windows service, managed from the browser and a REST API.
---

The web server is the IL2-SRS server as a .NET 10 application with a browser-based admin UI and a REST API. Existing IL2-SRS clients connect to it exactly as they do to the Windows server window; nothing changes for players.

Use it when you want to:

- run SRS on a Linux host or alongside other containers;
- run SRS as a Windows service without a desktop session;
- manage the server from a browser, or automate it with scripts and bots.

:::note[Preview]
The web server is new. The Windows server window remains available while the web server is proven in production.
:::

## What you get

- **Dashboard** with listener health, uptime, clients, voice links, recent transmitters and recent activity.
- **Clients**: search, mute, kick, and ban with a reason and optional duration.
- **Settings** and **Channel Names**, applied immediately and sent to connected clients.
- **Bans** with reason, who banned, and expiry. Ban an IP address directly or remove a ban.
- **Event log** of logins, admin actions, server start and stop, client connections and refused connections. Old events are removed automatically.
- **API keys** for the REST API, with read-only or read-write access, and an interactive **API reference**.

Times are shown in your own time zone. The **Desktop / Light / Dark** button in the top bar switches the theme; **Desktop** follows your system setting. The choice is remembered in each browser.

Settings, bans, the event log and API keys are stored in `srs.db`, a SQLite database in the server's data folder.

## Run in Docker

The image is published on Docker Hub as [`asken/il2-srs-server`](https://hub.docker.com/r/asken/il2-srs-server). Start it with an admin password:

```sh
docker run -d --name il2-srs-server --restart unless-stopped \
  -e SRS_ADMIN_PASSWORD='a-long-password' \
  -p 6002:6002/tcp -p 6002:6002/udp -p 127.0.0.1:8080:8080 \
  -v il2-srs-data:/data asken/il2-srs-server:preview
```

This publishes the SRS port `6002` for TCP and UDP, keeps the admin UI on `http://127.0.0.1:8080`, and stores data in a named volume. The repository also contains a `docker-compose.yml` with the same setup, and a `Dockerfile` if you prefer to build the image yourself.

`docker ps` shows the container as `healthy` while both SRS listeners are running. It turns `unhealthy` if a listener fails, or while an admin has stopped the server from the dashboard.

:::caution[Protect the admin UI]
The admin UI and API use plain HTTP. Keep port 8080 on localhost, or put a reverse proxy with HTTPS (Caddy, Traefik, nginx) in front of it before exposing it to the internet.
:::

## Run as a Windows service

1. Get a self-contained build, which needs no .NET installation on the server. Download `IL2-SRS-Server-Web-<version>-win-x64.zip` from [GitHub Releases](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases) (from 1.0.5.0-beta.1) and extract it to `C:\IL2-SRS\app`, or publish one yourself:

   ```powershell
   dotnet publish IL2-SRS-Server-Web -c Release -r win-x64 --self-contained -o C:\IL2-SRS\app
   ```

   Windows marks files extracted from a downloaded ZIP as coming from the internet. If PowerShell refuses to run the install script, unblock the folder first:

   ```powershell
   Get-ChildItem C:\IL2-SRS\app -Recurse | Unblock-File
   ```

2. From an elevated PowerShell prompt, install and start the service:

   ```powershell
   C:\IL2-SRS\app\Install-WindowsService.ps1 -DataDirectory C:\IL2-SRS\main
   ```

   The script asks for the admin password, writes it to `srs-server.json` in the data folder (readable only by Administrators and SYSTEM), adds firewall rules for the SRS port, and sets the service to restart after a crash.

3. Open `http://localhost:8080` on the server and log in.

To test without a service, run `IL2-SRS-Server.exe --data-dir C:\IL2-SRS\main` from a console with `SRS_ADMIN_PASSWORD` set. Without `--data-dir`, the server keeps its data next to the exe, or in `C:\ProgramData\IL2-SRS-Server` when that folder is not writable (for example under `C:\Program Files`).

## Move an existing server

Point the data folder at a copy of your existing server folder. On first start, the web server imports `server.cfg` and `banned.txt` into `srs.db`, so settings, channel names and bans carry over. The original files are not changed. After the import, change settings in the admin UI; edits to `server.cfg` are no longer read.

## Configuration

Set these as environment variables, as `--KEY=value` arguments, or in `srs-server.json` in the data folder:

| Key | Meaning |
| --- | --- |
| `SRS_ADMIN_PASSWORD` | Admin password, at least 8 characters. Required. |
| `SRS_ADMIN_PASSWORD_FILE` | Read the password from a file (Docker secrets). |
| `SRS_<SETTING>` | Fix any [server setting](../configuration-reference/), for example `SRS_SERVER_PORT=6002`. Fixed settings are shown as locked in the admin UI. |
| `SRS_EVENT_RETENTION_DAYS` | Keep events for this many days (default 30). |
| `SRS_EVENT_MAX_ROWS` | Keep at most this many events (default 100000). |
| `urls` | Admin UI address. Defaults to `http://localhost:8080` on Windows and port 8080 on all interfaces in Docker. |

Run several servers by giving each its own data folder and SRS port, and on Windows its own service name and admin UI address.

## REST API

Create a key under **API Keys** in the admin UI and send it as `Authorization: Bearer <key>`:

```sh
curl -H "Authorization: Bearer $SRS_API_KEY" http://localhost:8080/api/v1/clients
```

Read keys can view status, clients, settings, channel names, bans and events. Write keys can also change settings, kick, ban and mute clients, manage bans, clear the event log, and start, stop or restart the server. Every change made with a key is recorded in the event log under the key's name.

The **API reference** button on the API Keys page opens an interactive reference (`/scalar`) where you can paste a key and try requests from the browser. Scripts and code generators can use the OpenAPI document at `/openapi/v1.json`.

## Differences from the server window

- UPnP port forwarding is not supported. Forward the SRS port (TCP and UDP) on your router or host.
- The update checker is not included. Update the container image or the published files instead.
- The server-wide White and Dark theme is replaced by the per-browser Desktop / Light / Dark button.
