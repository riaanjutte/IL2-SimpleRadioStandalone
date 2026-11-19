# Panel "Fit to content", draggable dividers, and three panels per row

## Intent

A panel alone in a row, or in a 50/50 pair, often has far more width than its controls need. The Server
panel is the example: its controls are 220 px wide and centred, and the rest of the row is empty. The
player wants that space back for the panel beside it.

## Goals

- A per-panel **Fit to content** option: the panel takes only the width its controls need.
- A **draggable divider** between neighbouring panels of a row, to set any split by hand.
- Up to **three panels per row** instead of two.
- Both are saved with the layout and survive a restart.

## Non-goals

- Free placement of panels on a snap grid, or of controls inside a panel.
- More than three panels in a row.
- Fitting or resizing height, and stacking panels inside a row: height stays as it is, each row as tall as
  its tallest panel (decided 2026-11-19).
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

Chosen: 1 and 2 together, plus a third panel per row. The player picked fit for the Server case and wants
the divider as well for rows where the panels should not be content-sized.

## Design

**Model (`CustomLayout`)**

- `Fit`: set of panel keys that are fit to content.
- A row holds one to three panels (`Normalise` keeps the first three of a longer row, as it keeps two today).
- `Splits`: row key (as `RowKey`, e.g. `server+favourites`) to one share per panel of that row, summing to 1,
  each at least 0.15. No entry means equal shares.
- `Normalise` drops unknown panel keys from `Fit`, drops `Splits` entries whose row no longer exists or whose
  count does not match the row, and clamps shares into range. `Default()` has neither, so **Reset layout** clears both.
- An older saved layout without these fields loads as today (no fit, 50/50).

**Row widths (main window, `BuildRow`)**

- A fit panel's column is `Auto`; any other panel's column is star-sized.
- Star columns use the row's shares as their weights, so a split scales with the window. A panel that is
  fit, or hidden by its own rule (RCI Controller without access keys), simply does not use its share.
- A row with fit and other panels: the fit ones are content-sized, the others divide the rest.
- A row whose panels are all fit: content-sized and left-aligned, the remaining width stays empty.
- Fit never hides the collapse arrow; it stays over the right-hand panel's header.

**Divider (main window)**

- Shown between two neighbouring panels when neither is fit: an invisible strip in the gap between them,
  with a resize cursor on hover. A three-panel row has up to two. It is always draggable, not only while
  the editor is open.
- Dragging moves width between the two panels beside the divider only; on release the shares are clamped,
  stored in `Splits` and saved. Double-click resets the whole row to equal shares.
- Like the collapse arrows, a change made while the editor is open is passed to the editor's copy of the
  layout, so its OK or Apply keeps it.

**Editor**

- Under the row buttons: "Fit to content:" followed by one checkbox per panel of the selected row, each
  labelled with its panel's name. Ticking one sets or clears that panel in `Fit`.
- A row's entry in the list marks fit panels, e.g. "Server (fit) + Favourites".
- **Join with row below** (today "Pair with row below") is enabled when this row and the next row the player
  can see hold three panels or fewer together; the lower row's panels are added on the right.
- **Split row** (today "Unpair") is its own button now, enabled on a row of two or three: it moves the
  right-hand panel to a new row directly below.
- Splitting, moving or hiding a panel keeps its fit flag. A row's split is dropped when its panels change
  (its row key no longer exists).

**Text**

"Fit to content:", "{0} (fit)", "Join with row below" and "Split row" in all six `.resx` files, replacing
"Pair with row below" and "Unpair"; plus a changelog line.

## My calls (not decisions the player made)

- Fit wins over the divider: there is no divider beside a fit panel. With a fit panel in the middle of
  three, the outer two have no divider and share the rest by their stored shares (equal by default).
- The divider lives in the main window and is always active, with double-click to reset.
- No panel's share goes below 15%.
- Join and Split are two buttons, and Split takes off one panel at a time.
- A row of only fit panels is left-aligned with empty space beside it.
- The editor uses checkboxes per panel rather than a button per row.

## Left to the builder

- The divider's exact width and how it sits in the existing gap between panels.
- How a panel with stretching content (Favourites' table, the settings grids, Controls) looks when fit:
  check each panel once and report any that collapses to something unusable.
- Whether three panels fit at the window's 400 px minimum width; the player accepts clipping there.
- Tests: model tests for `Fit`/`Splits` and three-panel rows (normalise, clamp, join and split, save and load
  round trip, old layout without the fields); a live check in the client for the row widths and the dividers.
