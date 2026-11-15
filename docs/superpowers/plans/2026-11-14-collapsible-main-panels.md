# Collapsible General-Tab Panels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let players collapse the Server/Overlays/RCI section and the Favourites section of the General tab with a small top-right arrow, shrinking the window while the General tab is shown.

**Architecture:** Each section is wrapped in a `Grid` with a small arrow `ToggleButton` laid over the top-right of its header. `MainWindow` code-behind hides the section contents, saves the state, and adjusts the window height; the height arithmetic lives in a small static helper that is unit-tested.

**Tech Stack:** WPF on .NET Framework 4.8.1, MSTest (`IL2-SR-CommonTests`), old-style csproj files with explicit `<Compile Include>` entries.

**Spec:** `docs/superpowers/specs/2026-11-14-collapsible-main-panels-design.md`

## Global Constraints

- Arrow points up (▴) when expanded, down (▾) when collapsed; tooltips "Collapse" / "Expand", localized in all six `IL2-SR-Client/Localization/*.resx` files (`scripts/Validate-ResxTranslations.ps1` must pass).
- The saved `ClientHeight` is always the normal (all-expanded-equivalent) height, never the shrunken General-tab height.
- A maximised window is never resized.
- Classic shows only the Server/Overlays/RCI arrow.

## Review Focus

- Client starts with a section saved as collapsed: the window must open at the shrunken height on General, and the saved height must not shrink further on every restart.
- Player resizes the window while sections are collapsed, then closes: the saved height is the new height plus the collapsed height.
- RCI panel becomes available (server setting arrives) while its section is collapsed: it must stay hidden until expanded.
- Classic theme: the Favourites arrow must not appear on the Favourites tab, and Classic's fixed height must not be saved.
- Window maximised when toggling or switching tabs: no height change, and restoring keeps the right height.

---

### Task 1: Height arithmetic helper

**Files:**
- Create: `IL2-SR-Client/UI/ClientWindow/CollapsedPanelsHeight.cs` (add `<Compile Include>` in `IL2-SR-Client/IL2-SR-Client.csproj`)
- Test: `IL2-SR-CommonTests/UI/CollapsedPanelsHeightTests.cs` (add `<Compile Include>` in `IL2-SR-CommonTests/IL2-SR-CommonTests.csproj`)

**Interfaces:**
- Produces, in namespace `Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow`, `internal static class CollapsedPanelsHeight`:
  - `double WindowHeight(double normalHeight, double collapsedHeight, bool onGeneralTab, double minHeight)` — on General: `Math.Max(minHeight, normalHeight - collapsedHeight)`; otherwise `normalHeight`.
  - `double NormalHeight(double windowHeight, double collapsedHeight, bool onGeneralTab)` — on General: `windowHeight + collapsedHeight`; otherwise `windowHeight`.

- [ ] **Step 1: Write the failing tests** `CollapsedPanelsHeightTests`:
  - `WindowHeight(650, 250, true, 200) == 400`; `WindowHeight(650, 250, false, 200) == 650`; `WindowHeight(300, 250, true, 200) == 200`; `WindowHeight(650, 0, true, 200) == 650`.
  - `NormalHeight(400, 250, true) == 650`; `NormalHeight(650, 250, false) == 650`.
- [ ] **Step 2: Build and run; expect compile failure** (`CollapsedPanelsHeight` missing).
- [ ] **Step 3: Implement the class.**
- [ ] **Step 4: Run** `vstest.console.exe IL2-SR-CommonTests/bin/Debug/DCS-SR-CommonTests.dll /Tests:CollapsedPanelsHeightTests` — expect PASS.
- [ ] **Step 5: Commit.**

### Task 2: Arrows, collapsing, persistence and window height

**Files:**
- Modify: `IL2-SR-Client/Settings/GlobalSettingsStore.cs` — add enum keys `ServerOverlaysCollapsed`, `FavouritesCollapsed` (before the closing brace of `GlobalSettingsKeys`) and defaults `"false"` in `defaultGlobalSettings`.
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml` — wrap `ServerOverlaysGrid` and `FavouritesGroupBox` each in a `Grid`, adding `ToggleButton x:Name="ServerOverlaysCollapseToggle"` (top-right of the Overlays header) and `ToggleButton x:Name="FavouritesCollapseToggle"` (top-right of Favourites header); both `Checked`/`Unchecked` → `PanelCollapseToggle_Changed`, `IsChecked` = collapsed.
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs`
- Modify: `IL2-SR-Client/Localization/{en,de,es,fr,it,ru}.resx` — keys `Collapse`, `Expand`.
- Modify: `CHANGELOG.md` — one line under Unreleased › Added.

**Interfaces:**
- Consumes: `CollapsedPanelsHeight.WindowHeight`, `CollapsedPanelsHeight.NormalHeight`.
- Produces in `MainWindow`: field `double _collapsedPanelsHeight` (height currently removed from General); method `void ApplyPanelCollapse()` (reads both toggles, sets `ServerLayout`, `OverlaysLayout` and `FavouritesGroupBox.Content` visibility, calls `UpdatePilotRosterAndRciVisibility()`, re-measures `GeneralLayout`, updates `_collapsedPanelsHeight`, resizes).

- [ ] **Step 1: Settings keys and resx strings**; run `scripts/Validate-ResxTranslations.ps1` — expect "RESX validation passed for 6 file(s)."
- [ ] **Step 2: XAML** — arrow: small borderless `ToggleButton` with a `Path` triangle in `MilAccentBrush` (military) / default foreground (Classic), about 10×10, `HorizontalAlignment="Right" VerticalAlignment="Top"`, margin placing it on the header row left of the top-right screw.
- [ ] **Step 3: Code-behind**
  - `UpdatePilotRosterAndRciVisibility(bool, bool)`: RCI panel visible only if `showCombatBoxRci && ServerOverlaysCollapseToggle.IsChecked != true`.
  - `ApplyPanelCollapse()`: measure `GeneralLayout.ActualHeight` before and after (`UpdateLayout()`), accumulate the difference into `_collapsedPanelsHeight`; when `WindowState == Normal` and General is selected, set `MinHeight` and `Height` from `CollapsedPanelsHeight.WindowHeight` (normal min height is the XAML `MinHeight`, or 0 for Classic).
  - Toggle handler saves `ServerOverlaysCollapsed` / `FavouritesCollapsed` and calls `ApplyPanelCollapse()`; tooltip switches between `LocalizationManager.Get("Collapse")` / `Get("Expand")`.
  - Startup: set toggles from settings and call `ApplyPanelCollapse()` in `Loaded` (layout must exist to measure).
  - `TabControl.SelectionChanged` (source must be the `TabControl` itself; nested combo boxes bubble the event): convert between normal and General height with the helper.
  - `OnClosing`: save `CollapsedPanelsHeight.NormalHeight(bounds.Height, _collapsedPanelsHeight, generalSelected)` as `ClientHeight`.
  - `ApplyClassicThemeLayout`: hide `FavouritesCollapseToggle` and treat Favourites as expanded.
- [ ] **Step 4: Build the solution and run all tests** — expect the build to succeed and every test to pass.
- [ ] **Step 5: Run the client** and check, with screenshots: collapse/expand each section on a military theme and on Classic; switch to Settings and back; restart with a section collapsed and confirm the height doesn't creep.
- [ ] **Step 6: Commit.**
