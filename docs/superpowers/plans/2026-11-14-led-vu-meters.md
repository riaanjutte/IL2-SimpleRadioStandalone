# LED VU Meters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let players pick realistic 16-segment LED meters with peak hold instead of the analogue dials, with the Microphone and Speakers panels compacting around them.

**Architecture:** Pure scale and peak-hold logic in `LedVuMeterScale` (unit-tested); a `LedVuMeter` FrameworkElement draws the segments in `OnRender` and binds its `Value` to the existing dial's `Value`; `MainWindow` shows either the dial or the LED meter per the VU Meter setting.

**Tech Stack:** WPF on .NET Framework 4.8.1, MSTest (`IL2-SR-CommonTests`), old-style csproj files with explicit `<Compile Include>` entries.

**Spec:** `docs/superpowers/specs/2026-11-14-led-vu-meters-design.md`

## Global Constraints

- Thresholds (dB, lit when level ≥): −40, −35, −30, −25, −20, −16, −13, −10, −8, −6 green; −4, −2, 0 amber; +2, +4, +6 red.
- Scale labels under the segments for −40, −20, −10, −6, 0, +6.
- Peak hold 1 second. Meter about 150 × 28 DIP. LED colours fixed, not from the theme.
- New setting value `"LED"` for the existing `GlobalSettingsKeys.VuMeterStyle`; the key `LED` added to all six `Localization/*.resx` (`scripts/Validate-ResxTranslations.ps1` must pass).
- Classic unchanged: bars stay, LED meter never shown.

## Review Focus

- Level stops changing while a peak is held (preview stopped, value stuck at −100): the peak must still go out after 1 s.
- Levels outside the scale or invalid (−100, +18, NaN): 0 lit, 16 lit, 0 lit.
- Switching between LED and a dial face while Audio Preview runs: the visible meter keeps moving.
- Classic with `VuMeterStyle=LED` saved: thin bars shown, no LED meter.
- A section collapsed (collapse arrows) while switching meter style: no wrong General-tab height.

---

### Task 1: Scale and peak-hold logic

**Files:**
- Create: `IL2-SR-Client/UI/ClientWindow/LedVuMeterScale.cs` (add `<Compile Include>` to `IL2-SR-Client/IL2-SR-Client.csproj`)
- Test: `IL2-SR-CommonTests/UI/LedVuMeterScaleTests.cs` (add `<Compile Include>` to `IL2-SR-CommonTests/IL2-SR-CommonTests.csproj`)

**Interfaces** (namespace `Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow`):
- `internal enum LedColour { Green, Amber, Red }`
- `internal static class LedVuMeterScale`
  - `static readonly double[] Thresholds` (the 16 values above)
  - `static int LitSegments(double db)` — count of thresholds ≤ db; NaN → 0.
  - `static LedColour ColourOf(int segment)` — 0–9 Green, 10–12 Amber, 13–15 Red.
- `internal sealed class LedPeakHold`
  - `static readonly TimeSpan HoldTime = 1 s`
  - `int Update(int lit, DateTime now)` — returns the peak segment count: rises with `lit` at once; when `lit` is lower, keeps the old peak until `HoldTime` after it was set, then drops to `lit`.
  - `DateTime? ReleaseAt` — when a held peak (> last `lit`) will drop; null when none is held.

- [ ] **Step 1: Write failing tests** `LedVuMeterScaleTests`:
  - `LitSegments(-100) == 0`, `(-40) == 1`, `(-39.9) == 1`, `(-6) == 10`, `(0) == 13`, `(6) == 16`, `(18) == 16`, `(double.NaN) == 0`.
  - `ColourOf(9) == Green`, `ColourOf(10) == Amber`, `ColourOf(12) == Amber`, `ColourOf(13) == Red`.
  - Peak: `Update(12, t0) == 12`; `Update(5, t0+0.5s) == 12`; `Update(5, t0+1s) == 5`; `Update(14, t0+1.2s) == 14`; after `Update(14, t)` then `Update(3, t+0.2s)`, `ReleaseAt == t+1s`; when `lit` equals the peak, `ReleaseAt == null`.
- [ ] **Step 2: Build; expect compile failure** (`LedVuMeterScale` missing).
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `vstest.console.exe IL2-SR-CommonTests/bin/Debug/DCS-SR-CommonTests.dll /Tests:LedVuMeterScaleTests` — expect PASS.
- [ ] **Step 5: Commit.**

### Task 2: LED meter control, picker choice and layout

**Files:**
- Create: `IL2-SR-Client/UI/ClientWindow/LedVuMeter.cs` (add `<Compile Include>`)
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml` — add `clientWindow:LedVuMeter x:Name="Mic_LED"` / `Speaker_LED` beside `Mic_VU` / `Speaker_VU`, `Value="{Binding Value, ElementName=Mic_VU}"` (and Speaker), `Visibility="Collapsed"`, same tooltip as the dial.
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs`
- Modify: `IL2-SR-Client/Localization/{en,de,es,fr,it,ru}.resx` — key `LED` (value `LED` in every language).
- Modify: `CHANGELOG.md` — one line under Unreleased › Added.

**Interfaces:**
- Consumes: `LedVuMeterScale.Thresholds`, `LitSegments`, `ColourOf`, `LedPeakHold.Update`, `ReleaseAt`.
- Produces: `public sealed class LedVuMeter : FrameworkElement` with dependency property `double Value` (default −100, `AffectsRender`); `MainWindow` constant `VuMeterStyleLed = "LED"` and method `void ApplyVuMeterKind()`.

- [ ] **Step 1: `LedVuMeter`** — `MeasureOverride` returns 150 × 28. `OnRender` draws, matching mockup C (`.superpowers/brainstorm/*/content/led-styles.html`): black window with inset edge, 16 segments with a vertical gradient (white-hot top, colour, darker base) and a soft glow when lit, a dark tint of their colour when unlit, the held peak segment lit, a glass reflection over the window, and the six scale labels in 6-pt Consolas under their segments. Brushes frozen and created once. Value changes call `LedPeakHold.Update(LitSegments(Value), DateTime.UtcNow)`; while `ReleaseAt` is set, a one-shot `DispatcherTimer` invalidates the visual at that time (the value may stop changing).
- [ ] **Step 2: Picker and switching** — add `VuMeterStyleLed` to the options in `InitVuMeterStylePicker` and to `CurrentVuMeterStyle`; `ApplyVuMeterKind()` shows `Mic_LED`/`Speaker_LED` and collapses `Mic_VU`/`Speaker_VU` when LED is chosen and the layout isn't Classic, otherwise the reverse; call it after `InitVuMeterStylePicker()` and in `VuMeterStylePicker_SelectionChanged`. `App.ApplyVuMeterStyle` needs no change (LED falls back to the default dial brushes).
- [ ] **Step 3: resx + CHANGELOG**; run `scripts/Validate-ResxTranslations.ps1` — expect "RESX validation passed for 6 file(s)."
- [ ] **Step 4: Build and run all tests** — expect success and every test passing.
- [ ] **Step 5: Run the client** (test settings copy, `-cfg=`): choose LED, run Audio Preview, check the look against mockup C, peak hold going out after preview stops, the panels compacted, switching back to a dial, and Classic with LED saved showing bars. Screenshots.
- [ ] **Step 6: Commit.**
