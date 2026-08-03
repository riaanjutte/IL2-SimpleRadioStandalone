# Neutral Lobby Music

IL2-SRS server owners can optionally play a looping playlist to clients in the neutral lobby. The feature is disabled by default and supports Ogg Vorbis (`.ogg`) files.

## Setup

1. Start the updated SRS server once so the new settings are added to `server.cfg`.
2. Open the `LobbyMusic` directory beside `IL2-SR-Server.exe`.
3. Add, replace, or remove `.ogg` files to create the server's playlist.
4. Click **Neutral Lobby Music** in the server window so it shows **ON**.

Tracks play in filename order and repeat continuously. Only connected clients whose current coalition is neutral receive the music. Playback uses the configured **Global Lobby Freq. AM (MHz)**.

The server volume defaults to 25% and can be adjusted while music is playing. Players can independently disable **Neutral Lobby Music** under the client's **Settings > Audio Options**; the client option is enabled by default. Lobby music is excluded from radio collision interference.

The installer creates the playlist directory but does not bundle music, keeping normal client downloads small. Updates leave files in that directory unchanged.

The server log reports the filename when each track starts and warns when the directory, playlist, or lobby frequency is unavailable.

## Configuration

The defaults added under `[General Settings]` are:

```ini
LOBBY_MUSIC_ENABLED=false
LOBBY_MUSIC_DIRECTORY=LobbyMusic
LOBBY_MUSIC_VOLUME=0.25
```

`LOBBY_MUSIC_DIRECTORY` accepts an absolute path or a path relative to the directory containing `IL2-SR-Server.exe`.

`LOBBY_MUSIC_VOLUME` accepts a value from `0.0` to `1.0`. Values outside that range are clamped. The server slider changes the current track immediately; restart the server after changing the directory manually.

Server owners are responsible for ensuring they have permission to stream every file they place in the playlist. "Royalty free" does not always mean unrestricted redistribution or public performance.
