---
title: Downloads and releases
description: Download the stable updater, find beta builds, and understand the SRS release channels.
---

## Recommended download

Download the [latest stable Auto Updater](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases/latest/download/IL2-SRS-AutoUpdater.exe).

The updater downloads and installs the current stable Community Edition release. Use this option unless you are intentionally testing a beta.

## Beta builds

Beta builds are published on the [GitHub Releases page](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases). A beta release is marked **Pre-release** and includes its own updater and full release package.

Use beta builds when you want to test new functionality and are prepared to report issues. Read the notes for that build before installing because experimental server features can require matching server and client versions.

## Full packages

Each release also provides a ZIP package containing the client, server, installer, and supporting files. Server administrators and users who cannot run the updater can download the ZIP from the release page.

From 1.0.5.0, releases also provide the [web server](../server-admin/web-server/) preview as a separate download, `IL2-SRS-Server-Web-<version>-win-x64.zip`. It is a self-contained Windows build and is not part of the client package or the auto-updater. Docker users can use the image described on that page instead.

## Release notes

**1.0.5.1:** Players who had switched on RCI Controller mode can connect again; the mode stays hidden until Combat Box issues access codes. The installer explains that SRS keeps your other telemetry devices in `startup.cfg`. See the [1.0.5.1 release notes](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases/tag/v1.0.5.1).

**1.0.5.0:** SRS reconnects by itself when the connection drops, PTT no longer lags when a device with a keybind on it disappears, a Classic theme with the look of 1.0.2.0, a Pilot Roster that fits its height and leaves out radio bots, Mute / Unmute keybinds per radio, and fixed audio channel balance sliders. Server administrators can try the new web server (preview). See the [full release notes](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases/tag/v1.0.5.0).

**1.0.4.12:** Bind keys to toggle the Pilot Roster and Client List; those windows now keep their position and size. The client also recovers microphone capture after audio device resets, remembers a separate pilot name for Great Battles and Korea, and repairs an update loop caused by an obsolete DLL. See the [1.0.4.12 release notes](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases/tag/v1.0.4.12).

The client Help tab includes a **What's New in this version** link that opens the GitHub notes for the running build. The repository also keeps a cumulative [CHANGELOG.md](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/blob/master/CHANGELOG.md).

:::note[Stable and beta channels]
GitHub's `releases/latest` address always selects the newest stable release. To find the newest beta, use the Releases page and choose the most recent entry marked **Pre-release**.
:::

For beta-to-stable changes, manual installation, configuration backups, and repair steps, see [Updates and recovery](../guides/update-recovery/).
