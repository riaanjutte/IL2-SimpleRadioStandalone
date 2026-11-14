# Custom Main-Window Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players arrange the main window themselves: named tabs, panels placed in rows, pairs, hidden panels and a collapse arrow per row, edited in a layout editor window.

**Architecture:** A pure, unit-tested `CustomLayout` model (tabs → rows → panel keys, hidden panels, collapsed rows) with editing operations and Base64-JSON storage. `MainWindow` rebuilds its TabControl from the model by moving the existing panel GroupBoxes into generated rows (non-Classic only). A `LayoutEditorWindow` edits a copy of the model. The collapse height rule becomes "shrink by the freed empty space".

**Tech Stack:** WPF on .NET Framework 4.8.1, C# 7.3, Newtonsoft.Json 13 (already referenced), SharpConfig, MSTest (`IL2-SR-CommonTests`), old-style csproj files with explicit `<Compile Include>`/`<Page Include>` entries.

**Spec:** `docs/superpowers/specs/2026-11-14-custom-layout-design.md`

## Global Constraints

- Panel keys (exactly): `microphone`, `speakers`, `server`, `overlays`, `favourites`, `controls`, `globalSettings`, `rciController`, `audioOptions`, `profile`, `profileSettings`, `help`.
- Built-in tab ids: `general`, `controls`, `settings`, `help`; their display names are the translated "General", "Controls", "Settings", "Help" until renamed.
- Default layout = today's window (see spec). Row key = the row's panel keys joined with `+` (e.g. `server+overlays`).
- Stored in a new `GlobalSettingsKeys.CustomLayout` as **Base64 of UTF-8 JSON**: raw JSON with quotes/`;`/`#` reads back empty from SharpConfig (checked 2026-11-14), Base64 round-trips.
- Classic: fixed layout, no ⚙, keeps `ServerOverlaysCollapsed` and its XAML arrow exactly as today.
- All new user-facing text in all six `IL2-SR-Client/Localization/*.resx`; `scripts/Validate-ResxTranslations.ps1` must pass.

## Review Focus

- A player types a tab name with quotes, `;`, `#`, accents or emoji: it must survive save and restart.
- A saved layout from this version loaded after a later version adds a panel: the new panel appears at the end of the first tab.
- The RCI Controller panel hidden by its own rule (no access keys) while in a pair: the pair must not show a broken half; the other panel takes the row.
- Hiding the Server panel: the overlay's connect screw, auto-connect and hotkeys must still work.
- Collapsing a row on the always-scrolling Settings tab: the window must not shrink.

---

### Task 1: Layout model, rules and storage

**Files:**
- Create: `IL2-SR-Client/UI/ClientWindow/CustomLayout.cs` (add `<Compile Include>`)
- Test: `IL2-SR-CommonTests/UI/CustomLayoutTests.cs` (add `<Compile Include>`)

**Interfaces** (namespace `Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow`):
- `internal static class PanelKeys` — `const string` per key above; `static readonly string[] All` in that order.
- `internal sealed class LayoutTab` — `string BuiltIn` (null for custom), `string Name` (null until renamed), `List<List<string>> Rows`.
- `internal sealed class CustomLayout`
  - `List<LayoutTab> Tabs`, `List<string> Hidden`, `HashSet<string> Collapsed` (row keys).
  - `static CustomLayout Default()`.
  - `static CustomLayout Load(string stored, bool serverOverlaysCollapsed, bool favouritesCollapsed)` — null/empty → `Default()` with `server+overlays` / `favourites` collapsed per the two flags; Base64/JSON errors or no tabs → `Default()` (no seeding); then `Normalise()`.
  - `string Save()` — Base64(UTF-8 JSON).
  - `void Normalise()` — drops unknown keys and later duplicates (rows first, then `Hidden`), drops empty rows, rows of more than two keep the first two, a blank `Name` becomes null, adds panels in neither rows nor `Hidden` as single rows at the end of the first tab, drops `Collapsed` keys that match no row.
  - `static string RowKey(IList<string> row)`.
  - Editing (all keep the model normalised): `void AddTab(string name)`, `bool RenameTab(int tab, string name)` (false for blank), `void DeleteTab(int tab)` (panels to `Hidden`; refuses the last tab — returns false), `void MoveTab(int tab, int delta)`, `void MoveRow(int tab, int row, int delta)`, `void MoveRowToTab(int tab, int row, int targetTab)` (appends), `bool Pair(int tab, int row)` (with the row below; both must be single), `void Unpair(int tab, int row)` (into two single rows, same place), `void HideRow(int tab, int row)`, `void ShowPanel(string key, int tab)` (appends a single row). Moving, pairing or unpairing a row removes its key from `Collapsed`.
  - `CustomLayout Clone()`.

- [ ] **Step 1: Write failing tests** `CustomLayoutTests`:
  - `Default()` tabs `general|controls|settings|help`, General rows `[microphone,speakers] [server,overlays] [favourites]`, Settings rows are the five single rows in spec order, `Hidden` empty.
  - `Load(null, true, false)` collapses `server+overlays` only; `Load("not base64", true, true)` is default with nothing collapsed.
  - Round trip: `AddTab("In \"flight\"; #1 Größe ü 🛩")`, `Save()`, `Load(saved, false, false)` keeps the name.
  - Normalise: unknown `foo` dropped; `microphone` twice keeps the first; a layout without `help` anywhere gets `[help]` appended to the first tab; a row of three keeps two; `Collapsed` key `nonexistent` dropped.
  - `DeleteTab` moves its panels to `Hidden`; deleting the only tab returns false.
  - `Pair` joins two single rows, fails when either is a pair or no row below; `Unpair` splits back; `MoveRowToTab` appends; `HideRow` + `ShowPanel` round-trip; `RenameTab(0, "  ")` returns false.
- [ ] **Step 2: Build; expect compile failure.**
- [ ] **Step 3: Implement.** JSON via `JsonConvert`; `Load` wraps decoding and parsing in one try/catch that returns `Default()`.
- [ ] **Step 4: Run** `vstest.console.exe IL2-SR-CommonTests/bin/Debug/DCS-SR-CommonTests.dll /Tests:CustomLayoutTests` — expect PASS.
- [ ] **Step 5: Commit.**

### Task 2: Shrink by the freed empty space

**Files:**
- Modify: `IL2-SR-Client/UI/ClientWindow/CollapsedPanelsHeight.cs`
- Test: `IL2-SR-CommonTests/UI/CollapsedPanelsHeightTests.cs`

**Interfaces:**
- Produces: `static double FreedHeight(double viewportAtNormalHeight, double expandedContent, double collapsedContent)` = `max(0, viewport − collapsed) − max(0, viewport − expanded)`.
- `Resize(...)` is unchanged; its `collapsedHeight` argument is now the freed height.

- [ ] **Step 1: Failing tests:** `FreedHeight(500, 400, 250) == 150`; `FreedHeight(500, 700, 450) == 50`; `FreedHeight(500, 900, 600) == 0`; `FreedHeight(500, 400, 400) == 0`.
- [ ] **Step 2: Build; expect compile failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `/Tests:CollapsedPanelsHeightTests` — expect PASS.
- [ ] **Step 5: Commit.**

### Task 3: Main window built from the layout

**Files:**
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml` — name the unnamed panel GroupBoxes `MicrophonePanel`, `SpeakersPanel`, `ServerPanel`, `GlobalSettingsPanel`, `AudioOptionsPanel`, `ProfilePanel`, `ProfileSettingsPanel`, `HelpPanel`; add the ⚙ `Button x:Name="CustomiseLayoutButton"` at the right end of the tab strip (ToolTip/automation name "Customise layout"); add a collapsed `Grid x:Name="HiddenPanelsHost"` inside the window for hidden panels.
- Create: `IL2-SR-Client/UI/ClientWindow/MainWindow.Layout.cs` (partial class, like `MainWindow.RciController.cs`; add `<Compile Include>`).
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs`, `IL2-SR-Client/Settings/GlobalSettingsStore.cs` (key `CustomLayout`, default `""`).

**Interfaces:**
- Consumes: Task 1 `CustomLayout`, `PanelKeys`, `RowKey`; Task 2 `FreedHeight`, `Resize`.
- Produces in `MainWindow`: `CustomLayout _layout`; `FrameworkElement PanelFor(string key)` (`overlays` → `OverlaysColumn`, `controls` → `ControlsGroupBox`, `rciController` → `RciControllerGroup`, `favourites` → `FavouritesGroupBox`, others → the named GroupBoxes); `void ApplyLayout(CustomLayout layout)` (rebuilds tabs, keeps the selected tab index when possible, saves); `bool IsPanelCollapsed(string key)`.

- [ ] **Step 1: Build tabs.** Non-Classic only, after `ApplyClassicThemeLayout`: load with `CustomLayout.Load(setting, ServerOverlaysCollapsed, FavouritesCollapsed)` and save once (so later Classic changes don't reseed). `ApplyLayout`: detach every panel from its current parent, clear `TabControl.Items`, and per tab add a `TabItem` (header = `Name` or `LocalizationManager.Get` of the built-in name) holding `ScrollViewer` (vertical Auto, horizontal Disabled) → `StackPanel` of rows. A row is a `Grid` with one or two star columns, the panel(s), and a `ToggleButton` with `PanelCollapseToggleStyle` over the right panel's top-right; hidden panels go into `HiddenPanelsHost`. A pair whose panel is hidden by its own rule shows the other panel full width (bind or update column spans on visibility change). The XAML `ServerOverlaysCollapseToggle` and `FavouritesCollapseToggle` are collapsed outside Classic.
- [ ] **Step 2: Collapse per row.** A row toggle's state is `_layout.Collapsed.Contains(RowKey)`; toggling saves the layout. Collapsing a panel hides its GroupBox `Content`; for `overlays` hide `OverlaysLayout` and `CombatBoxRciPanel`. `UpdatePilotRosterAndRciVisibility` and `SetPanelContentVisibility` use `IsPanelCollapsed("overlays")` (Classic: the XAML toggle, as today). `ApplyPanelCollapse` works on the selected tab's row `StackPanel` (Classic: `GeneralLayout` on General, as today): measures expanded and collapsed `DesiredSize`, takes `ScrollViewer.ViewportHeight + _heightReduction` as the viewport at normal height, and passes `FreedHeight(...)` to `Resize`. Tab switching keeps its deferred re-measure for every tab.
- [ ] **Step 3: ⚙ button** opens the editor (Task 4; until then a no-op) and is collapsed in Classic.
- [ ] **Step 4: Build, run all tests** — expect success.
- [ ] **Step 5: Run the client** (test settings copy via `-cfg=`): default layout looks like today plus arrows and ⚙; collapse a row on General and on Settings (Settings: no shrink); restart keeps collapsed rows; Classic unchanged. Screenshots.
- [ ] **Step 6: Commit.**

### Task 4: Layout editor window

**Files:**
- Create: `IL2-SR-Client/UI/ClientWindow/LayoutEditorWindow.xaml` + `.xaml.cs` (add `<Page Include>`/`<Compile Include>` like other windows).
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.Layout.cs`, the six `Localization/*.resx`, `CHANGELOG.md`.

**Interfaces:**
- Consumes: `CustomLayout.Clone` and the Task 1 editing operations; `MainWindow.ApplyLayout`.
- Produces: `LayoutEditorWindow(CustomLayout layout, Action<CustomLayout> apply)`; the window edits `layout.Clone()` and calls `apply` on OK/Apply.

- [ ] **Step 1: Window.** Left: tab list with Add, Rename (inline text box), Delete (asks; says its panels will be hidden), Up, Down. Right: rows of the selected tab (a pair shows "A + B") with Up, Down, Move to tab (combo), Pair with row below / Unpair, Hide; under it a "Hidden panels" list with "Add to this tab". Bottom: Reset layout (asks), OK, Apply, Cancel. Buttons disable when an operation can't apply. Panel names use the panels' translated headers. Owner = main window, `WindowStartupLocation.CenterOwner`, themed like the other client windows, keyboard reachable, controls named for screen readers.
- [ ] **Step 2: Wire** the ⚙ button to open it; Reset applies `CustomLayout.Default()` with nothing collapsed.
- [ ] **Step 3: Strings + CHANGELOG** (one line under Unreleased › Added); run `scripts/Validate-ResxTranslations.ps1` — expect "RESX validation passed for 6 file(s)."
- [ ] **Step 4: Build, run all tests** — expect success.
- [ ] **Step 5: Run the client:** add and rename a tab (with `"; #` and an accent), move Microphone and Speakers onto it, unpair and re-pair, hide Favourites and show it again, delete a tab, Cancel discards, Reset restores, restart keeps the layout; hide Server and check the overlay connect screw still connects (no server needed: it attempts a connection and logs it). Screenshots.
- [ ] **Step 6: Commit.**
