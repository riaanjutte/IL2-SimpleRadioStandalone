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
[Releases](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases) and extract it to any folder, for example `C:\Downloads\IL2-SRS`.

In an elevated PowerShell prompt:

```powershell
Get-ChildItem C:\Downloads\IL2-SRS -Recurse | Unblock-File
C:\Downloads\IL2-SRS\Install-WindowsService.ps1
```

The server goes to `C:\Program Files\IL2-SRS\IL2-SRS-Server` and its data to
`C:\ProgramData\IL2-SRS\IL2-SRS-Server`. To update, run the new release's script the same way.

Open http://localhost:8080.

Add a plugin:

```powershell
Copy-Item .\MyPlugin C:\ProgramData\IL2-SRS\IL2-SRS-Server\plugins\MyPlugin -Recurse
Restart-Service IL2-SRS-Server
```
