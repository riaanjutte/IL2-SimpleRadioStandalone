# Panel "Fit to content" and a draggable divider between paired panels

## Intent

A panel alone in a row, or in a 50/50 pair, often has far more width than its controls need. The Server
panel is the example: its controls are 220 px wide and centred, and the rest of the row is empty. The
player wants that space back for the panel beside it.

## Goals

- A per-panel **Fit to content** option: the panel takes only the width its controls need.
- A **draggable divider** between the two panels of a pair, to set any split by hand.
- Both are saved with the layout and survive a restart.

## Non-goals

- Free placement of panels on a snap grid, or of controls inside a panel.
- More than two panels in a row.
- Fitting or resizing a panel's height.
- Layouts that adapt across languages: the player uses one language and accepts that a split set in one
  may not suit another.
- The Classic theme, which keeps its fixed layout.

## Anti-goals

- A divider drag or a fit setting that is lost when the layout editor's OK or Apply is pressed afterwards.
- A split that can make a panel disappear (zero width).

## Approaches

1. **Fit to content per panel.** Width is measured from the controls, so nothing is stored but a flag.
   Follows window width, language and meter style by itself. Cannot give an arbitrary split.
2. **Draggable divider.** Any split, stored as a ratio. Does not adapt to content.
3. **Full snap grid.** Does everything, at many times the cost, and loses the reflow the window relies on.

Chosen: 1 and 2 together. The player picked fit for the Server case and wants the divider as well for
pairs where neither panel should be content-sized.

## Design

**Model (`CustomLayout`)**

- `Fit`: set of panel keys that are fit to content.
- `Splits`: row key (as `RowKey`, e.g. `server+favourites`) to the left panel's share of the row, 0.15 to
  0.85. No entry means 50/50.
- `Normalise` drops unknown panel keys from `Fit`, drops `Splits` entries whose row no longer exists, and
  clamps shares into range. `Default()` has neither, so **Reset layout** clears both.
- An older saved layout without these fields loads as today (no fit, 50/50).

**Row widths (main window, `BuildRow`)**

- A fit panel's column is `Auto`; any other panel's column is star-sized.
- Pair, neither fit: the columns are `share*` and `(1 - share)*`, so the split scales with the window.
- Pair, one fit: the fit panel is content-sized, the other takes the rest. No divider.
- Pair, both fit, or a single fit panel alone in its row: content-sized and left-aligned, the remaining
  width stays empty.
- Fit never hides the collapse arrow; it stays over the right-hand panel's header.

**Divider (main window)**

- Shown only on a pair where neither panel is fit: an invisible strip in the gap between the two panels,
  with a resize cursor on hover. It is always draggable, not only while the editor is open.
- Dragging resizes live; on release the share is clamped, stored in `Splits` and saved. Double-click
  resets the pair to 50/50.
- Like the collapse arrows, a change made while the editor is open is passed to the editor's copy of the
  layout, so its OK or Apply keeps it.

**Editor**

- Under the row buttons: "Fit to content:" followed by one checkbox per panel of the selected row, each
  labelled with its panel's name. Ticking one sets or clears that panel in `Fit`.
- A row's entry in the list marks fit panels, e.g. "Server (fit) + Favourites".
- Unpairing, moving or hiding a panel keeps its fit flag. A pair's split is dropped when the pair is broken
  up (its row key no longer exists).

**Text**

"Fit to content:" and "{0} (fit)" in all six `.resx` files, plus a changelog line.

## My calls (not decisions the player made)

- Fit wins over the divider: a pair with a fit panel has no divider.
- The divider lives in the main window and is always active, with double-click to reset.
- Shares are limited to 15%–85%.
- A fit panel alone in a row is left-aligned with empty space beside it.
- The editor uses checkboxes per panel rather than a button per row.

## Left to the builder

- The divider's exact width and how it sits in the existing gap between panels.
- How a panel with stretching content (Favourites' table, the settings grids, Controls) looks when fit:
  check each panel once and report any that collapses to something unusable.
- Tests: model tests for `Fit`/`Splits` (normalise, clamp, save and load round trip, old layout without the
  fields); a live check in the client for the row widths and the divider.
