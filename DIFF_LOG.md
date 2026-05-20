# Diff Log From Original Code Base

This repository folder does not contain a `.git` history or a separate original-source snapshot, so this is a reconstructed diff log rather than a literal `git diff`.

It documents the source, config, and documentation changes made in this working tree compared with the original IL2-SRS 1.0.2.0 code base as it existed before this update work.

## Summary

The update focuses on six areas:

- Client localization for English, German, French, and Spanish.
- UI layout fixes for translated text and wider controls.
- Safer default profile settings.
- More reliable installer and client handling of IL-2 `startup.cfg` telemetry.
- Joystick/input-device reconnect recovery for PTT.
- IL-2 intercom routing fixes for vehicle owners and crew members.
- More reliable update flow for automatic and manual GitHub release updates.
- One-time opt-in migration prompt for community recommended profile settings.
- PTT fail-safe handling to clear local transmit state if input polling fails.
- Documentation for translators and release/change tracking.

## Added Files

### `CHANGELOG.md`

Added a release-oriented changelog describing the feature and fix set completed during this update.

### `DIFF_LOG.md`

Added this reconstructed file-by-file diff log.

### `IL2-SR-Client/Localization/LocalizationManager.cs`

Original:

- The client UI was effectively English-only.
- No centralized runtime localization manager existed.
- No external translation loading existed.

Current:

- Adds built-in language catalogs for English, German, French, and Spanish.
- Applies translations to WPF visual and logical trees.
- Handles common WPF text surfaces including labels, buttons, tab headers, window titles, group headers, text blocks, text boxes, and flow document runs.
- Supports fallback to English when a translated key is missing.
- Loads community-editable `.resx` translation files from the runtime `Localization` folder.
- Detects the OS language on first run and stores the selected client language.
- Supports restart-to-apply behavior for the selected language.

### `IL2-SR-Client/Localization/en.resx`

Added the English external translation catalog.

### `IL2-SR-Client/Localization/de.resx`

Added the German external translation catalog.

### `IL2-SR-Client/Localization/fr.resx`

Added the French external translation catalog.

### `IL2-SR-Client/Localization/es.resx`

Added the Spanish external translation catalog.

### `IL2-SR-Client/Localization/README.md`

Original:

- No documented community translation workflow existed.

Current:

- Explains how contributors can edit translation `.resx` files.
- Documents fallback behavior and how to test translation changes.

### `IL2-SR-CommonTests/DCSState/PlayerGameStateTests.cs`

Original:

- No regression coverage existed for IL-2 intercom vehicle/crew matching.

Current:

- Adds tests covering owner-to-crew, crew-to-owner, crew-to-crew, and different-vehicle intercom routing.

### `Installer/StartupConfigTelemetry.cs`

Original:

- Installer telemetry setup was embedded directly in `MainWindow.xaml.cs`.
- It used raw string checks and fragile append logic.

Current:

- Adds a dedicated robust helper for IL-2 `startup.cfg` telemetry repair.
- Adds or repairs `[KEY = telemetrydevice]`.
- Ensures `enable = true`.
- Ensures SRS telemetry endpoint `127.0.0.1:4322` is present.
- Preserves existing third-party telemetry endpoints by adding the next available `addrN`.
- Handles read-only files, retries file IO, writes via temp file, and verifies the final result.

### `IL2-SR-Client/Utils/StartupConfigTelemetry.cs`

Original:

- The running client did not check IL-2 `startup.cfg` at startup.
- If `startup.cfg` was reverted, missing telemetry, disabled telemetry, or changed after installation, the client did not warn or repair it.

Current:

- Adds the same robust telemetry repair logic to the client.
- Allows the client to verify and repair `startup.cfg` at launch using the installer-saved IL-2 path.
- Warns the user if repair fails.

## Modified Files

### `README.md`

Original:

- No note about community translation files.

Current:

- Adds guidance that translations can be improved through the `.resx` files in `IL2-SR-Client/Localization`.

### `IL2-SR-Client/IL2-SR-Client.csproj`

Original:

- Did not include loose localization resource files.
- Did not include `LocalizationManager.cs`.
- Did not include client-side `StartupConfigTelemetry.cs`.

Current:

- Compiles the new localization and startup telemetry helpers.
- Copies localization `.resx` files and translator README to the Release output.

### `IL2-SR-Client/App.xaml.cs`

Original:

- Initialized logging, localization was absent, checked required audio DLLs, single instance, admin relaunch, and tray icon.
- Did not initialize a localization manager.
- Did not check IL-2 `startup.cfg`.

Current:

- Initializes `LocalizationManager` after logging.
- Changes admin relaunch flow so startup continues only in the final process.
- Reads installer registry key `HKEY_CURRENT_USER\SOFTWARE\IL2-SRS\IL2Path`.
- Checks `data/startup.cfg` at startup.
- Repairs telemetry settings if possible.
- Logs successful verification or repair.
- Shows a warning if telemetry verification or repair fails.

### `IL2-SR-Client/Settings/GlobalSettingsStore.cs`

Original:

- No stored language preference.

Current:

- Adds a client language setting used by first-run OS language detection and the language picker.
- Adds a theme setting for light/dark mode.
- Adds an auto-start radio overlay setting.
- Adds `CommunityRecommendedSettingsChoice` so the community recommended profile settings prompt is shown only once.

### `IL2-SR-Client/Settings/ProfileSettingsStore.cs`

Original profile defaults:

- Radio RX/TX effects were not all enabled by default.
- Text to Speech beta was not enabled by default.
- PTT-as-switch, PTT delay, and radio panning defaults did not match the requested profile behavior.

Current profile defaults:

- `RadioRxEffects_Start=true`
- `RadioRxEffects_End=true`
- `RadioTxEffects_Start=true`
- `RadioTxEffects_End=true`
- `EnableTextToSpeech=true`
- `RadioSwitchIsPTT=true`
- `PTTReleaseDelay=250`
- `Radio1Channel=-0.5`
- `Radio2Channel=0.5`
- `IntercomChannel=0`
- Top-level radio voice effect and clipping remain off.

### `IL2-SR-Common/DCSState/PlayerGameState.cs`

Original:

- Intercom receive checks compared IDs in a way that excluded the IL-2 vehicle owner/commander, because that player is reported with `vehicleId=-1`.
- Crew-to-owner and some owner-to-crew intercom cases could fail even when both players were in the same vehicle.

Current:

- Normalizes intercom membership to an IL-2 vehicle group id.
- Uses the parent vehicle/client id for crew members and the player unit id for the owner/commander.
- Allows intercom only when both resolved group ids are valid and equal.

### `IL2-SR-Common/Network/UpdaterChecker.cs`

Original:

- The update dialog's `No` button did not open the manual download page because the browser launch was commented out.

Current:

- Opens the GitHub release page through the shell when users choose manual update.
- Uses the same browser launch helper when automatic update launch fails.
- Resolves the updater executable path before launch.
- Supports the packaged `IL2-SRS-AutoUpdater.exe` name and the legacy `AutoUpdater.exe` name.
- Shows a simple manual-update fallback if the updater cannot be launched.

### `AutoUpdater/MainWindow.xaml.cs`

Original:

- The auto updater selected the first matching release zip returned by the GitHub API.
- It assumed API response order was equivalent to highest release version.

Current:

- Parses release tags as versions.
- Ignores drafts and excludes prereleases unless beta updates are requested.
- Selects the highest valid version with an `IL2-SimpleRadioStandalone*.zip` asset.
- Shows an error if no matching asset can be found.

### `IL2-SimpleRadio Server/Network/UDPVoiceRouter.cs`

Original:

- Server-side voice routing passed the receiving client's `vehicleId` as the sender vehicle id when checking whether the recipient could hear an intercom transmission.

Current:

- Reads the sender's current `PlayerGameState`.
- Passes the sender's `unitId` and `vehicleId` into intercom reachability checks.
- Falls back to the packet unit id when sender state is unavailable.

### `IL2-SR-Client/Singletons/ConnectedClientsSingleton.cs`

Original:

- Tuned-client counting passed each remote client's `vehicleId` as the sender vehicle id.

Current:

- Uses the local player's current unit id and vehicle id when asking whether remote clients can hear the local transmission.

### `IL2-SR-Client/Input/InputDeviceManager.cs`

Original:

- A joystick or DirectInput device failure during polling showed an error telling users the controls would not work until SRS restarted.
- Failed devices were disposed but recovery depended on restart or manual rediscovery.
- Device dictionaries were enumerated without a dedicated lock.

Current:

- Adds locking around the input device dictionary.
- Removes stale and disposed devices before rediscovery.
- On polling failure, removes and disposes only the failed device.
- Suppresses the disruptive in-flight restart dialog.
- Logs the failure and attempts automatic rediscovery every few seconds.
- Lets PTT bindings resume when Windows exposes the same DirectInput device instance again.
- Keeps the existing manual "Rescan Input Devices" behavior, but makes it better at clearing stale devices.
- Adds the bindable `Mute / Unmute Selected Radio` action and handles it as a one-shot control.
- Keeps the PTT input polling thread alive after polling exceptions.
- Clears active PTT state and requests device rediscovery when the polling loop catches an exception.

### `IL2-SR-Client/Utils/RadioHelper.cs`

Original:

- Radio selection, channel changes, status reading, and overlay volume setting were supported.
- No helper existed for muting and restoring the currently selected radio.

Current:

- Adds selected-radio mute toggling.
- Stores the previous volume per radio before muting.
- Uses the profile-configured selected-radio muted volume instead of hard-coding mute to zero.
- Restores the previous volume when the same selected-radio mute control is pressed again.
- Ignores disabled radios and intercom for the selected-radio mute action.

### `IL2-SR-Client/Network/UDPVoiceHandler.cs`

Original:

- Local PTT state could remain set if the input polling loop stopped updating after a DirectInput failure.

Current:

- Tracks the last successful PTT input poll.
- Clears local PTT state if input polling is stale for more than two seconds.
- Rate-limits failsafe logging so repeated stale-poll checks do not flood the client log.

### `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml`

Original:

- Window and tab layout was tuned for English-length labels.
- Translated tab labels and settings labels could clip.
- Controls page columns could drift or crowd in Spanish/French/German.
- Language picker did not exist.

Current:

- Increases the main client window size constraints.
- Allows resizing.
- Adds localized language selection to settings.
- Widens and aligns controls to better fit translated text.
- Adds the `Mute / Unmute Selected Radio` control binding row.
- Adds a `Selected Radio Muted Volume` profile slider with a 5% to 50% range.
- Adds layout changes that reduce clipping in General, Controls, Settings, and Profile Settings areas.
- Preserves existing client workflow while making translated UI usable.

### `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs`

Original:

- UI text was hard-coded or initialized in English.
- Toggle button content was used as behavior state in places.
- Localization could overwrite toggle content and break click behavior.
- Controls showed `OFF` even when enabled after localization.

Current:

- Applies runtime localization to the main window.
- Adds language selection handling and restart-to-apply messaging.
- Adds a one-time startup prompt that lets users opt into community recommended profile settings without silently overwriting existing profiles.
- Applies the accepted community recommended settings to the current profile and records accepted/declined state so the prompt is not shown again.
- Updates localized on/off toggle text from `IsChecked`, not from stale content.
- Saves toggle state from `IsChecked` rather than displayed text.
- Saves and reloads the selected-radio muted volume profile setting.
- Refreshes toggle labels after profile/settings reload.
- Localizes several dynamically created or code-behind strings.

### `IL2-SR-Client/UI/ClientWindow/InputBindingControl.xaml`

Original:

- Input binding rows used fixed English-oriented spacing.
- Long translated binding names clipped.

Current:

- Adds wrapping for long binding labels.
- Uses stable widths for device/button/assign/clear columns.
- Keeps rows aligned through longer Spanish, French, and German text.

### `IL2-SR-Client/UI/ClientWindow/InputBindingControl.xaml.cs`

Original:

- Used label content directly in ways that did not align cleanly with wrapped text.
- Dynamic text was not localized consistently.

Current:

- Uses text block content for wrapped input labels.
- Localizes "None" and other dynamic binding display values.

### `IL2-SR-Client/UI/ClientWindow/Favourites/FavouriteServersView.xaml`

Original:

- Favourites view text was English-only.

Current:

- Updated for localization coverage.

### `IL2-SR-Client/UI/ClientWindow/ClientList/ClientListWindow.xaml`

Original:

- Client list window text was English-only.

Current:

- Updated for localization coverage.

### `IL2-SR-Client/UI/ClientWindow/ServerSettingsWindow/ServerSettingsWindow.xaml.cs`

Original:

- Server settings window strings were not fully localized.

Current:

- Updated for localization coverage.

### `IL2-SR-Client/UI/ClientWindow/RadioChannelConfigUI.xaml`

Original:

- Radio channel config UI text was English-only or only partially localizable.

Current:

- Updated for localization coverage.

### `IL2-SR-Client/UI/InputProfileWindow/InputProfileWindow.xaml.cs`

Original:

- Input profile window dynamic text was not fully localized.

Current:

- Updated for localization coverage.

### `IL2-SR-Client/UI/RadioOverlayWindow/RadioOverlay.xaml.cs`

Original:

- Overlay window text/state was not part of localization pass.

Current:

- Updated for localization coverage.

### `IL2-SR-Client/UI/RadioOverlayWindow/RadioControlGroup.xaml.cs`

Original:

- Radio overlay control group text was not fully localized.

Current:

- Updated for localization coverage.

### `IL2-SR-Client/UI/RadioOverlayWindow/IntercomControlGroup.xaml.cs`

Original:

- Intercom overlay text was not fully localized.

Current:

- Updated for localization coverage.

### `Installer/MainWindow.xaml.cs`

Original:

- `EnableTelemetry` read `startup.cfg` as raw text.
- It checked for `telemetrydevice` with simple substring logic.
- It could incorrectly assume any `addr1` meant SRS was configured.
- It could append malformed content if line endings or existing sections were unusual.
- It changed file attributes with `File.SetAttributes(path, ~FileAttributes.ReadOnly)`, which is unsafe because it sets many unrelated attribute bits.

Current:

- Delegates telemetry repair to `StartupConfigTelemetry.EnsureEnabled`.
- Avoids brittle raw-string surgery in installer UI code.
- Preserves original file attributes.
- Handles missing section, existing disabled section, and existing third-party telemetry more reliably.

### `Installer/Installer.csproj`

Original:

- Did not compile `StartupConfigTelemetry.cs`.

Current:

- Compiles the new installer telemetry helper.

## Config And Packaged Output Changes

### `IL2-SR-Client/bin/Release/default.cfg`

Original packaged defaults:

- Did not match the requested new profile defaults.

Current packaged defaults:

- Radio RX start/end effects enabled.
- Radio TX start/end effects enabled.
- Text to Speech beta enabled.
- PTT-as-switch enabled.
- PTT release delay set to `250`.
- Radio 1 panned left and Radio 2 panned right.
- Radio voice/clipping effects remain disabled.

### `IL2-SR-Client/bin/Release/IL2-SR-ClientRadio.exe`

Current:

- Rebuilt after localization, layout, profile default, toggle, joystick reconnect, and startup telemetry check changes.

### `Installer/bin/Release/Installer.exe`

Current:

- Rebuilt after installer telemetry hardening.

## Behavior Changes

### Localization

Original:

- Client UI was English-centric.
- Restarting after changing language still left many labels in English.
- Community translation improvements required code changes.

Current:

- English, German, French, and Spanish are supported.
- The app detects OS language on first run.
- Users can select language from Settings.
- Restart applies language selection.
- External `.resx` files let community translators improve text without rebuilding.

### Layout

Original:

- Longer translations clipped in General, Controls, and Settings.
- Controls page columns were visually misaligned.

Current:

- Window size and control widths were adjusted.
- Long control labels wrap.
- Controls page columns align consistently.
- Tab headers and settings labels have more room.

### Toggles

Original:

- Localized toggle content could break state display and click behavior.
- Some toggles displayed `OFF` even when enabled.

Current:

- Toggle display text is derived from `IsChecked`.
- Toggle click handlers persist boolean state correctly.
- Localized on/off text updates after settings and profile reloads.

### Installer Telemetry

Original:

- Installer telemetry configuration could silently fail or incorrectly decide SRS was already configured.

Current:

- Installer repairs telemetry settings robustly.
- Existing telemetry consumers such as JetSeat-style entries are preserved.
- File access and read-only cases are handled with retries and verification.

### Client Startup Telemetry Check

Original:

- Client did not verify IL-2 telemetry settings at launch.

Current:

- Client verifies and repairs `startup.cfg` on launch when the installer-saved IL-2 path is available.
- User is warned if repair fails.

### Joystick PTT Recovery

Original:

- A momentary joystick disconnect could leave PTT unusable until SRS restarted.

Current:

- Input device failures remove only the failed device.
- Automatic rediscovery is attempted.
- PTT can recover after reconnect when the same DirectInput device instance returns.

## Validation Performed

- Built the Release client executable after the changes.
- Built the Release installer executable after the installer telemetry changes.
- Smoke-tested telemetry repair logic against:
  - missing telemetry section
  - disabled existing section
  - third-party telemetry section
  - read-only `startup.cfg`
  - second-pass idempotency
- Verified fresh profile defaults after profile-setting changes.
- Added and ran intercom regression tests for owner/crew vehicle matching.
- Used UI screenshots during localization/layout iteration to check French and Spanish clipping and alignment.

## Known Limitations

- This is not a literal unified diff because no original baseline or git repository is available in the workspace.
- Joystick reconnect recovery depends on Windows/DirectInput exposing the reconnected controller with the same instance GUID used by the saved binding.
- Voice proximity falloff was investigated but not implemented because current IL-2 player state does not include position data.
