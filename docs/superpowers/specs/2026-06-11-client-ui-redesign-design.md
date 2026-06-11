# IL2-SRS Client main window redesign — "Military utility"

**Date:** 2026-06-11
**Scope:** Main client window (`IL2-SR-Client/UI/ClientWindow/MainWindow.xaml`) only.
Popup windows and the radio overlay are out of scope (they inherit app-wide control
styles as a side effect, but receive no structural changes).

## Goals

- Modern, compact UI replacing the 6-year-old layout.
- Grey theme with a WW2-military character ("military utility" intensity: gunmetal
  grey-green, olive accents, stencil/mono typography, LED indicators — flat, not
  skeuomorphic).
- All existing functionality preserved: every control keeps its name, bindings,
  event handlers, and localization keys.

## Decisions made during brainstorming

| Question | Decision |
|---|---|
| Scope | Main window only |
| Dependencies | Upgrades allowed, but Approach 1 chosen: no new packages |
| Theme intensity | Option B — military utility |
| Layout | Option A — top tabs + persistent status bar |
| Tab organization | 5 tabs: General, Controls, Audio, Settings, Help |
| Theme modes | Single fixed military theme; Light/Dark/System picker removed |
| Implementation | Full restyle on existing MahApps.Metro 1.5, delete `ClientThemeManager.cs` |

## 1. Visual design system

### Palette

| Role | Hex |
|---|---|
| Window background | `#32342F` |
| Header / chrome / tab strip | `#272924` |
| Input & well backgrounds | `#23251F` |
| Panel borders & section rules | `#4A4D44` |
| Primary text | `#E4E6DD` |
| Secondary text | `#878A7E` |
| Accent (selected tab, section markers) | `#A8B06A` (olive) |
| Action button fill / border | `#5C6644` / `#79855A` |
| Status LED on / off / error | `#8FB573` / `#54564F` / `#C25B4E` |

### Typography

- **Allerta Stencil** (OFL) — window title / branding only.
- **Share Tech Mono** (OFL) — tab headers, section headers, status bar, button
  labels. Uppercase with letter spacing.
- Default UI font at 12px for body text and settings labels (readability over
  40+ settings rows; mono everywhere would hurt legibility).
- Both fonts bundled as embedded resources under `IL2-SR-Client/Fonts/` with
  their OFL license files. Referenced via WPF embedded-font syntax
  (`./Fonts/#Family Name`).

### Control treatments

- Flat 1px-bordered controls, 2px corner radius, compact paddings.
- GroupBox restyled as a section header: small olive `▸ TITLE` in mono caps over
  a 1px rule — no boxed border (removes nested-box look, saves vertical space).
- Checkboxes: small square toggles with olive check mark.
- Sliders: thin track, square thumb.
- Status indicators: LED-style ellipses.
- ComboBox, TextBox, ScrollBar, DataGrid, ProgressBar, ToolTip, RadioButton,
  ToggleButton all get implicit styles matching the palette.

## 2. Window structure

```
┌────────────────────────────────────────────┐
│ titlebar: IL2-SRS (stencil) · ver    — □ × │  ← dark chrome via MetroWindow props
│ GENERAL CONTROLS AUDIO SETTINGS HELP       │  ← compact mono tab strip
│                                            │
│ tab content (scrolls if needed)            │
│                                            │
│ ● MIC  ● GAME  ● VOIP   clients: 12 · prof │  ← NEW persistent status bar
└────────────────────────────────────────────┘
```

- Window min size shrinks from 700×650 to ~640×560, still resizable with grip.
- Tab headers go from 22px font to ~13px mono caps.
- New persistent status bar at the window bottom, visible from every tab:
  mic availability, game (IL-2) connection, VOIP connection, connected-clients
  count, current profile, version.

### Tab reorganization

Controls move verbatim: same `x:Name`, bindings, click handlers, and
localization keys.

- **General** — server address + Connect, Show Server Settings, favourites list
  (moves in from the current Favourites tab), current profile selector, toggle
  buttons (radio overlay / client list / pilot roster), Patreon link.
- **Controls** — unchanged content (input bindings, rescan input devices).
- **Audio** (new tab) — from General: microphone select, audio preview,
  speakers & optional mic output, speaker boost. From Settings: microphone
  automatic gain control, microphone noise suppression, allow more input
  devices, play connection sounds.
- **Settings** — remaining global settings, language, profile management
  (create/copy/rename/delete) + profile settings (radio effects, PTT, TTS,
  volumes, audio channels — per-profile, so they stay together). The Theme
  picker row (Light/Dark/Use Windows setting) is removed.
- **Help** — unchanged content, restyled.
- The Server/VOIP/Il-2 indicators and connected-clients count move out of the
  General tab into the status bar.

## 3. Architecture

| File | Change |
|---|---|
| `IL2-SR-Client/Themes/MilitaryPalette.xaml` | **New.** All colors, brushes, and font-family resources as named keys. |
| `IL2-SR-Client/Themes/MilitaryControls.xaml` | **New.** Implicit styles for Button, ToggleButton, TextBox, ComboBox, CheckBox, RadioButton, Slider, GroupBox, TabControl, TabItem, ScrollBar, ScrollViewer, DataGrid, Label, ProgressBar, ToolTip, StatusBar. |
| `IL2-SR-Client/Fonts/` | **New.** AllertaStencil-Regular.ttf, ShareTechMono-Regular.ttf + OFL license files, build action Resource. |
| `IL2-SR-Client/App.xaml` | Merge the two new dictionaries **after** the MahApps dictionaries so the new implicit styles win. MahApps merges stay (popups depend on them). |
| `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml` | Restructure to 5 tabs + status bar. MetroWindow title-bar brushes/properties for dark chrome. |
| `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs` | Remove theme-switch wiring (theme radio button handlers, ClientThemeManager calls). Update any references to moved panels if names change (they should not). |
| `IL2-SR-Client/Utils/ClientThemeManager.cs` | **Deleted.** Replaced by declarative XAML styling. |
| Settings store | `Theme` key tolerated-but-ignored so old `client.cfg` files load cleanly. Theme-related .resx keys left untouched. |

`IL2-SR-Client/Themes/Styles.xaml` is untouched — the radio overlay windows
(out of scope) depend on it.

## 4. Risks and edge cases

- **Popups inherit new implicit styles** app-wide (client list, pilot roster,
  server settings, favourites editor, input prompt). Mostly a consistency bonus;
  each popup gets a smoke test, and visual breakage gets targeted fixes only.
- **Localization:** all strings keep their `.resx` bindings. The new "Audio" tab
  header needs one new key added to all six language files (en, de, fr, es, it,
  ru) — machine translation for non-English matches the existing convention.
- **Settings backward compatibility:** configs saved by older versions with
  `Theme=Dark|Light|System` must load without error.
- **DPI:** no fonts below 11px; grid-based layout; verify at 100% and 150%
  display scaling.
- **MahApps 1.5 constraint:** title-bar styling uses only MetroWindow's existing
  properties (WindowTitleBrush, NonActiveWindowTitleBrush, TitleTemplate,
  TitleBarHeight). No toolkit changes.

## 5. Verification plan

1. Build the full solution (.NET Framework 4.8.1, MSBuild).
2. Launch the client; visually walk all 5 tabs at 100% and 150% DPI.
3. Exercise the status bar states: mic present/absent, game connected/
   disconnected, VOIP connect/disconnect cycle.
4. Open every popup window for the style-inheritance smoke test.
5. Verify a config file from the previous version (containing a `Theme` value)
   loads cleanly.
6. Run `IL2-SR-CommonTests` — must pass unchanged.
7. If feasible, run a local server from this repo and verify the connect flow
   end-to-end.
