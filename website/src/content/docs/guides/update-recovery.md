---
title: Updates and recovery
description: Update, reinstall, roll back, back up, or reset SRS without losing profiles and controls.
---

SRS stores the application and your personal configuration separately:

- **Application:** `C:\Program Files\IL2-SimpleRadio-Standalone`
- **User data:** `%AppData%\IL2-SRS`

Updating or reinstalling the application does not normally remove your profiles, controls, favourites, radio presets, or window settings.

## Before an update or repair

1. Close IL-2 Great Battles, IL-2 Korea, and any dedicated server instance.
2. Close the SRS client and server.
3. If you have a configuration you cannot afford to lose, make a backup of `%AppData%\IL2-SRS`.

To open the user-data folder, press `Win+R`, enter `%AppData%\IL2-SRS`, and select **OK**.

:::caution[Do not update while IL-2 is running]
IL-2 can rewrite `startup.cfg` when it exits. Closing the game first prevents it from undoing telemetry changes made by the installer.
:::

## Update to the latest stable release

1. Download the [stable Auto Updater](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases/latest/download/IL2-SRS-AutoUpdater.exe).
2. Run it and accept the Windows administrator prompt.
3. Keep the recommended installation folder when the installer opens.
4. Start SRS and confirm the version shown in the title bar or status bar.

The Auto Updater closes running SRS components, downloads the latest stable package, runs its installer, and restarts the client after a successful automatic update.

## Install or continue testing a beta

Open [GitHub Releases](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases), select the required release marked **Pre-release**, and download its attached Auto Updater or full ZIP package.

Enable **Settings → Check for beta updates** if you want the client to notify you about later beta builds. Disabling this setting stops beta notifications; it does not replace an installed beta with the stable version.

## Return from beta to stable

If the installed beta is newer than the latest stable release, the Auto Updater will not downgrade it. Use the stable full package instead:

1. Back up `%AppData%\IL2-SRS` and close IL-2 and SRS.
2. Open the [latest stable release](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases/latest).
3. Download the ZIP asset whose name begins with `IL2-SimpleRadioStandalone`.
4. Extract the complete ZIP to a temporary folder.
5. Run `installer.exe` from that folder and install to `C:\Program Files\IL2-SimpleRadio-Standalone`.
6. Start SRS and disable **Check for beta updates**.

Your AppData configuration remains in place, although settings introduced only by a newer beta may not be understood by an older stable client.

## Repair a missing or damaged installation

Use the full ZIP package when the Auto Updater cannot run, files are missing, or SRS no longer starts:

1. Download the required stable or beta ZIP from [GitHub Releases](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/releases).
2. Extract every file to a temporary folder. Do not run the installer from inside the ZIP.
3. Run `installer.exe` as administrator.
4. Install to the recommended Program Files folder.
5. Start SRS and run **Help → Telemetry Diagnostics**.

Do not delete `%AppData%\IL2-SRS` as part of a normal repair. The installer also detects older duplicate installations and can consolidate their user data before retiring known program files from those locations.

## Recover settings

### Restore your own backup

1. Close SRS.
2. Rename the current `%AppData%\IL2-SRS` folder so it remains available as a safety copy.
3. Restore the backed-up `IL2-SRS` folder to `%AppData%`.
4. Start SRS and verify the selected profile, controls, audio devices, and favourites.

### Check migration backups

When the installer consolidates legacy or duplicate installations, it stores dated backups under `%AppData%\IL2-SRS\MigrationBackups`.

Keep the current folder backed up before restoring individual configuration files from one of these folders.

### Recover from a corrupted global.cfg

If SRS cannot parse `global.cfg`, it creates `global.cfg.bak` and starts with default global settings. Preserve both files before attempting recovery. Control profiles are stored in separate `.cfg` files and may still be usable even when the global configuration is damaged.

## Reset SRS configuration

Use this only when reinstalling the application did not solve a configuration problem:

1. Close SRS.
2. Rename `%AppData%\IL2-SRS` to a backup name such as `IL2-SRS-old`.
3. Start SRS to create a clean configuration.
4. Reconfigure audio and test the client before restoring anything from the old folder.

This reset affects profiles, bindings, favourites, presets, and window settings. It does not uninstall SRS.

## Uninstall SRS

1. Download and extract a full release ZIP.
2. Run `installer.exe`.
3. Select **Remove**.

The removal process leaves `%AppData%\IL2-SRS` available for a future reinstall. For a completely fresh removal, delete that folder manually only after confirming that its backup is no longer needed.

## If recovery still fails

Use **Help → Report a Problem** and include `%AppData%\IL2-SRS\clientlog.txt`. For installation failures, also include `installer-log.txt` from the folder where the installer was run.
