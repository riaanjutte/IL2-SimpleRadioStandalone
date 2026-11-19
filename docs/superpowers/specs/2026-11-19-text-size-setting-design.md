# Text size setting

## Intent

Trial testers on 4K monitors find the client's text too small. They need a setting that makes it up to 30%
larger, with the layout following so nothing is cut off.

## Approaches

1. **Scale the whole UI** with one layout transform per window: text, controls and spacing grow together and
   the layout reflows as if the window were narrower. Chosen (2026-11-19): nothing can clip that does not clip
   at the normal size, and it is a small change.
2. **Scale only the fonts**: about 90 font sizes plus every fixed-size control (Server's 220-unit boxes, the
   Overlays buttons, 28-unit pickers, switches) reworked and checked across twelve panels and ten themes.
   Rejected: several times the work, and it brings back clipping.

## Design

- Global Settings gets **Text size**: 100%, 110%, 120%, 130%. Stored as `TextSize` (percent) in `global.cfg`;
  a hand-edited value is clamped to 100–130 and snapped to a step, anything unreadable is 100.
- `UiScale.Apply(window)` puts a `ScaleTransform` on the window's content as its `LayoutTransform` and scales
  the window's minimum size, and its size, by the same factor.
- Scaled: the main window, layout editor, server settings, input profile dialog, telemetry diagnostics, client
  list and pilot roster, with their dropdowns and tooltips (a tooltip opens in a window of its own, so
  `UiScale` scales each one as it opens). Not scaled: the radio overlay (it scales with its
  own window), window title bars, Windows message boxes.
- The setting applies at once. The main window keeps its size and its minimum size grows; the other open
  windows grow or shrink with it. Classic's fixed-size main window grows with its content.
- The main window's fit-to-tab height mixes window units and content units once the content is scaled;
  `CollapsedPanelsHeight.EmptyHeight(viewport, heightReduction, content, scale)` does the conversion. The
  pilot roster's fit-to-rows height converts the same way.
