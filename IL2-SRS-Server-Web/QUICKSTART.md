# Quickstart

## Docker

```sh
docker run -d --name il2-srs-server --restart unless-stopped \
  -e SRS_ADMIN_PASSWORD='a-long-password' \
  -p 6002:6002/tcp -p 6002:6002/udp -p 127.0.0.1:8080:8080 \
  -v il2-srs-data:/data asken/il2-srs-server:preview
```

Open http://localhost:8080.

Add a plugin:

```sh
docker cp ./MyPlugin il2-srs-server:/data/plugins/MyPlugin
docker restart il2-srs-server
```

## Windows service

Download `IL2-SRS-Server-Web-<version>-win-x64.zip` from
[Releases](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases) and extract it to `C:\IL2-SRS\app`.

In an elevated PowerShell prompt:

```powershell
Get-ChildItem C:\IL2-SRS\app -Recurse | Unblock-File
C:\IL2-SRS\app\Install-WindowsService.ps1 -DataDirectory C:\IL2-SRS\main
```

Open http://localhost:8080.

Add a plugin:

```powershell
Copy-Item .\MyPlugin C:\IL2-SRS\main\plugins\MyPlugin -Recurse
Restart-Service IL2-SRS-Server
```
