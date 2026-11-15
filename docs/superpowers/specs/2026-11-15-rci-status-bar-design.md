# RCI status bar (short design)

**Intent:** the Combat Box RCI status sat in a "Combat Box RCI" box under the Overlays panel, so it vanished when
Overlays was collapsed, hidden or moved to another tab, and it made the Overlays panel change height. The player
wants it in a second status bar at the bottom of the main window, visible on every tab.

**Design (approved 2026-11-15):**
- A thin bar docked directly above the existing status bar, in the same style (well background, top border).
  Left: the coloured RCI state box (same texts and colours as before, from `RciDisplayState`); next to it the
  controller-on-duty text, only when someone is on duty.
- Shown only while connected to `srs.combatbox.net` (any port), the existing `ShouldShowRciStatus` rule. There is no
  Korea RCI yet; when there is, its host is added to that rule.
- The "Combat Box RCI" box under Overlays is removed, with the window re-measuring it caused and Classic's
  header-less styling of it. Classic gets the same bar in its own colours.
- Unchanged: the radio overlay's RCI indicator, and the RCI Controller switch and access code in Settings.
- The bar sits outside the tabs, so the window grows by its height (about 25 px) while it shows: the fitted height is re-measured when the bar appears or goes.

**Approaches considered:** only one fits. Keeping the box inside a panel can't make it visible on every tab; a
separate floating window would be more to manage than the status line the player asked for.
