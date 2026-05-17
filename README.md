# IL2-SRS 1.0.3.0 Community Update

This release is a community-maintained update of IL2-SimpleRadio Standalone based on the original Ciribob IL2-SRS code base.

The goal of this build is to improve usability for Combat Box and other IL-2 communities while keeping the original SRS workflow intact.

## Main Changes From The Original Repo

### Client Localization

- Added client UI localization for English, German, French, and Spanish.
- Added a language picker in the client settings.
- Added first-run OS language detection for supported languages.
- Language changes require a client restart to apply cleanly.
- Translation files are external `.resx` files in `Localization/`, so community members can improve the translated text without rebuilding the app.
- Added translation contribution notes in `Localization/README.md`.

### UI Layout Improvements

- Adjusted the client layout to better support longer German, French, and Spanish text.
- Increased the main client window minimum size and allowed resizing.
- Added wrapping for longer labels where needed.
- Reworked the controls tab alignment so action names, devices, buttons, assign, and clear controls stay lined up.
- Fixed translated toggle buttons showing stale or incorrect `ON` / `OFF` text.

### Input Device Reconnect Recovery

- Added automatic DirectInput device rediscovery.
- Joystick PTT bindings can recover after a temporary disconnect/reconnect without restarting SRS, as long as Windows exposes the reconnected controller with the same DirectInput device instance.
- Manual input-device rescanning is still available.

### Selected Radio Mute Control

- Added a bindable `Mute / Unmute Selected Radio` control.
- Pressing the binding lowers the currently selected radio to a configurable background volume.
- Pressing the binding again restores that radio's previous volume.
- Added a `Selected Radio Muted Volume` profile slider with a 5% to 50% range.
- The default selected-radio muted volume is 25%.

### Profile Defaults

New profile defaults were updated for the community build:

- Radio voice effects off.
- Radio clipping effects off.
- PTT-as-switch on.
- PTT release delay set to `250 ms`.
- Radio 1 panned 50% left.
- Radio 2 panned 50% right.
- Text to Speech beta on.

### IL-2 Telemetry Setup Hardening

The installer and client now handle IL-2 `startup.cfg` telemetry setup more reliably:

- Adds `[KEY = telemetrydevice]` if it is missing.
- Enables telemetry if the section exists but is disabled.
- Preserves existing third-party telemetry devices.
- Adds the next available `addrN` entry for the SRS telemetry endpoint.
- Handles read-only `startup.cfg` files and restores original file attributes.
- Retries file access and verifies that the final config contains the SRS endpoint.
- The client also checks and repairs telemetry setup on startup when an IL-2 path is known.

## Notes For Translators

The non-English translations are machine translated and should be treated as a starting point.

To improve translations:

1. Open the relevant file in `Localization/`, for example `de.resx`, `fr.resx`, or `es.resx`.
2. Keep the English `name` keys unchanged.
3. Edit only the translated `<value>` text.
4. Restart the client after changing translation files.

If a translation key is missing, the client falls back to English.

## Known Scope

- Input reconnect recovery depends on Windows and DirectInput returning the same device instance after reconnect.
- Existing user profiles keep their existing settings unless a setting is new; new settings receive their default value automatically.
