# Changelog

## IL2-SRS 1.0.4.5-beta.10 community update

### Changed

- Redesigned the client UI as a vintage 1940s communication set: skeuomorphic equipment plates with bevelled edges, corner screws (randomised slots), and procedural grunge/scratches/chipped-paint weathering.
- Added two selectable themes - warm "Bakelite" (default) and cool "Grey" - chosen from Settings; the whole app and the radio overlay follow the selection.
- Replaced the bar VU meters with analog needle dials (dB scale, overload zone, glass cover).
- Indicator lamps now use bright-green "on" jewels; status lamps and coalition dots render as bezelled jewels.
- Increased UI text size ~20% for readability and switched headings/titles to a stencil face.
- Reorganised the General tab (Server and Overlays side by side; Microphone and Speakers panels moved here) and moved Audio Options into Settings.
- Gave the radio overlay the same equipment-plate treatment, themed to match the selected client theme.
- Themed the Connected Clients, Server Settings, and profile-name dialogs.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.

## IL2-SRS 1.0.4.3 community update

### Changed

- Overlay speaker names now remain visible for 3 seconds after transmission ends, without a fade effect.
- Long speaker names now scroll in the overlay channel display instead of being truncated.
- Overlay channel displays now refresh immediately after changing channels.

### Fixed

- Fixed held speaker names overriding the current speaker display.
- Added diagnostics for slow DirectInput/PTT polling to help identify controller polling delays.
- Increased the PTT input watchdog timeout to reduce false-positive reconnect handling during slow controller polls.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 13/13 passed.

## IL2-SRS 1.0.4.2 community update

### Fixed

- Fixed joystick/controller hot reconnect so PTT and channel-select bindings recover after a temporary USB disconnect/reconnect.
- Fixed automatic input-device rediscovery failing from the background input thread due to WPF window-handle thread affinity.
- Added product GUID persistence for input bindings so recovered devices can be matched more reliably when Windows changes the DirectInput instance ID.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.
- Confirmed by RufusK that joystick PTT reconnect now works after disconnect/reconnect.

## IL2-SRS 1.0.4.1 community update

### Fixed

- Fixed overlay pilot counter clipping when a channel or intercom has 10 or more pilots.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.

## IL2-SRS 1.0.4.0 community update

### Fixed

- Fixed radio mute/unmute so muting a radio only lowers received audio for that radio and no longer affects microphone/transmit audio.
- Radio mute state is now tracked separately from the radio's actual volume, so normal volume settings are preserved while muted.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.

## IL2-SRS 1.0.3.9 community update

### Added

- Added `Use Windows setting` to the client theme picker so SRS can follow the Windows light/dark app theme.
- Added localized labels for the new theme option in English, German, French, and Spanish.

### Changed

- New installs now default to `Use Windows setting`; if Windows theme detection is unavailable, the client falls back to light mode.
- When `Use Windows setting` is selected, the client updates its theme while running after Windows theme changes.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.

## IL2-SRS 1.0.3.8 community update

### Changed

- RCI/RCO detection now accepts common callsign marker variations using `RCI_` prefixes or `_RCI` suffixes.
- Friendly RCI/RCO callsigns are displayed without marker underscores.

### Fixed

- Hardened client disconnect so UDP voice receive/decode is stopped before audio devices are torn down, reducing disconnect freezes and lingering audio.
- Cleared pending receive audio, transmit state, receive state, and PTT state during disconnect.
- Fixed overlay clipping when a Combat Box RCI/RCO callsign is shown below the RCI status bar.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.

## IL2-SRS 1.0.3.7 community update

### Added

- Added Combat Box RCI status detection for clients whose player name ends with `__RCI`.
- Added Combat Box-only RCI status indicators to the radio overlay and main client window.
- Added input bindings to mute/unmute the opposite radio and mute/unmute both radios, using the existing selected-radio muted volume setting.

### Changed

- RCI indicators are only visible while connected to `srs.combatbox.net`.
- RCI status text now shows friendly-only, both-coalitions, opposition-only, inactive, and neutral/unknown states directly inside a color status bar.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.

## IL2-SRS 1.0.3.6 community update

### Fixed

- Fixed radio and intercom overlay volume sliders so user value changes are applied immediately, including track clicks and other non-drag slider changes.
- Hardened microphone capture and encoding against bad audio states that could produce distorted outgoing voice until the client was restarted.
- Added recovery for unexpected microphone capture stops so the client attempts to restart capture without requiring a full app restart.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.

## IL2-SRS 1.0.3.5 community update

### Added

- Added a one-time first-run prompt asking users whether they want to apply the community recommended profile settings to their current profile.
- Added localized prompt text for English, German, French, and Spanish.
- Added a PTT input watchdog that clears local transmit state if input polling stops updating, reducing the chance of a stuck hot mic after input-device failures.

### Changed

- The community recommended settings prompt now applies only after the user explicitly accepts it, and stores either the accepted or declined choice so the prompt is not shown again.
- The accepted settings are:
  - Radio switch works as PTT on.
  - Radio voice effect off.
  - Clipping effect off.
  - Text to speech on.
  - Selected radio muted volume set to `15%`.
  - PTT release delay set to `250 ms`.
  - Radio 1 audio channel set to `-0.75`.
  - Radio 2 audio channel set to `0.75`.

### Fixed

- Fixed local Release/x64 client builds so `IL2-SRS-AutoUpdater.exe` is copied into the client output folder.
- Fixed updater launch handling so the client resolves the updater executable path before launching it and falls back to the manual download page if launch fails.
- Fixed the PTT input polling loop so polling exceptions clear PTT state, request device rediscovery, and keep the polling thread alive.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.

## IL2-SRS 1.0.3.4 community update

### Added

- Added a light/dark theme picker in the client settings, with light mode as the default.
- Added automatic radio overlay startup, controlled by a client setting.
- Added saved main-window and radio-overlay size/position restore on startup.
- Added mute/unmute audio cues for the selected-radio mute binding.

### Changed

- Changed selected-radio mute/unmute so it no longer announces through text-to-speech while muting.
- Changed default IL-2 radio startup channels so radio 1 starts on channel 1 and radio 2 starts on channel 2.

### Fixed

- Fixed radio overlay startup sizing so the larger default size is applied once and does not double on every restart.
- Fixed dark-mode readability for disabled buttons, hover states, favourites/server list headers and rows, generated grid cells, and themed button text.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 12/12 passed.

## IL2-SRS 1.0.3.3 community update

### Added

- Added twelve-channel support to the radio overlay, allowing all IL-2 radio channels to be selected from the overlay.
- Added compact two-row channel selector buttons for each radio in the overlay.

### Changed

- Refined the radio overlay layout to stay close to the original overlay footprint while supporting channels 1-12.
- Aligned radio status dots, intercom status dot, channel selector buttons, channel display boxes, volume sliders, and the opacity slider for a cleaner compact overlay.
- Preserved the fixed-aspect overlay resize behavior to reduce flicker while resizing.
- Clamped oversized saved overlay dimensions from previous test layouts back to the intended compact defaults.

### Fixed

- Fixed the overlay footer spacing so the opacity slider is no longer clipped.
- Fixed overlay status dot alignment between radio and intercom rows.
- Fixed black channel display box alignment against the selected radio status dot.
- Fixed IL-2 telemetry setup to allow SRS to reuse an existing telemetry device port when IL-2 already has a matching enabled endpoint.

### Validation

- Built `IL2-SimpleRadioStandalone.sln` Release/x64 successfully.
- Ran `IL2-SR-CommonTests`: 11/11 passed.

## IL2-SRS 1.0.3.2 community update

### Added

- Added client localization support for English, German, French, and Spanish.
- Added a language picker in the client settings.
- Added first-run OS language detection so new installs can start in the detected supported language.
- Added restart-to-apply behavior for language changes.
- Added external `.resx` translation files in `IL2-SR-Client/Localization/` so the community can improve machine-translated text without rebuilding the app.
- Added translation contribution documentation in `IL2-SR-Client/Localization/README.md`.
- Added automatic input-device rediscovery so joystick PTT bindings can recover after a temporary disconnect/reconnect without restarting SRS, when Windows exposes the same DirectInput device instance again.
- Added a bindable control to mute or unmute the currently selected radio while preserving the previous radio volume.
- Added a profile setting slider for the selected-radio muted volume, configurable from 5% to 50%.

### Changed

- Expanded the client localization pass across the main client tabs, settings, controls, favourites, help text, buttons, labels, headers, and related client windows.
- Updated localized UI layout so longer German, French, and Spanish text has more room.
- Enabled text wrapping for long control labels where needed.
- Aligned the controls page columns so device, button, assign, and clear controls stay visually consistent.
- Increased the main client window minimum size and allowed resizing to reduce clipping in translated layouts.
- Improved on/off toggle localization so toggles display the translated current state while preserving click behavior.
- Changed new profile defaults:
  - Radio voice effects off.
  - Radio clipping effects off.
  - PTT-as-switch on.
  - PTT release delay set to `250 ms`.
  - Radio 1 panned 50% left.
  - Radio 2 panned 50% right.
  - Radio RX start/end effects on.
  - Radio TX start/end effects on.
  - Text to Speech beta on.
- Updated packaged Release `default.cfg` to match the new defaults.
- Hardened installer `startup.cfg` telemetry setup:
  - Adds `[KEY = telemetrydevice]` when missing.
  - Enables telemetry when the section exists but is disabled.
  - Preserves existing third-party telemetry devices by adding the next free `addrN` SRS endpoint.
  - Handles read-only `startup.cfg` files and restores their original attributes.
  - Retries file access and verifies the final config contains the SRS telemetry endpoint.
- Replaced the old input-device restart error dialog with logging plus automatic rediscovery attempts, reducing interruption while flying.

### Fixed

- Fixed a startup crash caused by modifying a collection while it was being enumerated during localization.
- Fixed untranslated English text remaining visible after switching the client to German, French, or Spanish.
- Fixed translated text being clipped in the General, Controls, and Settings tabs.
- Fixed controls page rows and columns drifting out of alignment in Spanish and other longer translations.
- Fixed settings/profile toggles showing `OFF` even when enabled.
- Fixed settings/profile toggles not responding correctly after localization changed their displayed content.
- Fixed brittle installer telemetry insertion that relied on raw substring checks and could fail with existing telemetry sections.
- Fixed unsafe read-only handling in the installer that could overwrite file attributes incorrectly.
- Fixed stale DirectInput devices blocking joystick PTT after a temporary disconnect.
- Fixed IL-2 intercom routing so vehicle owners and crew members can hear each other using IL-2 parent vehicle/client IDs.
- Fixed manual update selection so clicking `No` opens the GitHub release page.
- Fixed auto updater release selection so it downloads the highest available release version instead of relying on GitHub API response order.

### Build Notes

- Rebuilt the Release client executable:
  - `IL2-SR-Client/bin/Release/IL2-SR-ClientRadio.exe`
- Rebuilt the Release installer executable after the telemetry fix:
  - `Installer/bin/Release/Installer.exe`
- Verified telemetry config handling with smoke tests for missing, disabled, existing third-party, idempotent, and read-only `startup.cfg` cases.
- Verified fresh profile defaults load with the updated profile settings.

### Investigated

- Reviewed feasibility for optional server-controlled voice proximity falloff/distance attenuation.
- Confirmed the routing/audio architecture can support proximity attenuation, but player position data is not currently present in `PlayerGameState`.
- Recommended implementation path:
  - Parse player position from IL-2 telemetry if available.
  - Add position fields to `PlayerGameState`.
  - Add server settings to enable proximity attenuation and configure falloff distances.
  - Apply attenuation client-side during voice receive, with optional server-side max-distance routing cutoff.
