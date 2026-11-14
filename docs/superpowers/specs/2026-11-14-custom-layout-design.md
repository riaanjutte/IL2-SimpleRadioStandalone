# Custom main-window layout

## Why

Players want to shape the main window around how they fly: put the panels they use on the tabs they want, give
those tabs their own names, pair panels side by side or give them a full row, and leave out what they never use.

## What players can do

- Add, rename, reorder and delete tabs, including the built-in General, Controls, Settings and Help.
- Put each panel on a tab, order the rows on a tab, and pair two panels side by side or unpair them.
- Leave a panel off every tab (hidden).
- Collapse any row with its arrow.
- Reset everything to the default layout.

## The panels

There are 12 panels (13 counting Combat Box RCI, which travels inside Overlays), each with a stable key used in the
saved layout:

| Key | Panel | Today |
|---|---|---|
| `microphone` | Microphone | General, paired with Speakers |
| `speakers` | Speakers & Optional Mic Output | General |
| `server` | Server | General, paired with Overlays |
| `overlays` | Overlays, with Combat Box RCI underneath | General |
| `favourites` | Favourites | General |
| `controls` | Controls | Controls |
| `globalSettings` | Global Settings | Settings |
| `rciController` | RCI Controller | Settings |
| `audioOptions` | Audio Options | Settings |
| `profile` | Profile | Settings |
| `profileSettings` | Profile Settings | Settings |
| `help` | Help & About | Help |

`overlays` carries the Combat Box RCI panel with it: RCI belongs to that panel's job, and it still appears only
when the server offers RCI. Panels keep any visibility rule they have today (for example RCI Controller staying
hidden without access keys); the layout only decides where a panel sits when it is shown.

## Layout model

- A layout is an ordered list of tabs. A tab has a name and an ordered list of rows. A row holds one panel (full
  width) or two panels (a pair, half width each, left and right).
- Each panel appears at most once in the whole layout. Panels in no row are hidden.
- A built-in tab that hasn't been renamed shows its translated name; a renamed or new tab shows the player's name as
  typed. An empty name isn't allowed.
- At least one tab must exist. A tab may have no rows (it shows nothing).
- Every tab's content scrolls vertically when it is taller than the window.

## The default layout

Exactly today's window: General (Microphone + Speakers, Server + Overlays, Favourites), Controls (Controls),
Settings (Global Settings, RCI Controller, Audio Options, Profile, Profile Settings, each a single row), Help
(Help & About). Players who never open the editor see no change, and Reset layout returns to this.

## The layout editor

- A small ⚙ button at the right end of the tab strip, always visible on every tab, opens it. It is never part of a
  tab, so no layout can hide it.
- A small window: a list of tabs (add, rename, delete, move up/down) and, for the selected tab, its rows. Each row
  shows its panel or pair with: move up/down, move to another tab, Pair with the row below / Unpair, Hide. A
  "Hidden panels" list lets a panel be added back to the selected tab.
- Deleting a tab hides its panels (they appear in "Hidden panels"); the editor says so before deleting.
- Changes apply to the main window when the player presses OK or Apply; Cancel leaves the layout as it was.
  **Reset layout** asks first.
- The editor works with the keyboard, and its controls have accessible names.
- Its text is translated like the rest of the client (all six `Localization/*.resx`).

## Collapse arrows

- Every row has a collapse arrow on the top-right of its right-hand panel (the only panel, for a single row),
  including every row of the default layout. It collapses the whole row to its header bars, as the current
  Server/Overlays arrow does.
- Collapsed rows are saved with the layout, keyed by the panels in the row, so a re-paired row starts expanded.
- The window shrinks on whichever tab is shown by the empty space collapsing leaves at the bottom of that tab, never
  more: a tab whose content still overflows after collapsing doesn't shrink at all, and the window never shrinks
  below what the tab's content needs. It returns to the player's normal height on a tab with nothing collapsed.
  *(My call: the reviewer found that taking off the full collapsed height would shrink the always-scrolling
  Settings tab, and the Controls and Help panels that fill their tab, towards zero height.)* The existing rules stay:
  the saved height is the normal height, maximised and snapped windows aren't resized.
- Reset layout also expands every row.
- The two existing settings (`ServerOverlaysCollapsed`, `FavouritesCollapsed`) are read once to seed the collapsed
  state of the matching default rows. After that the custom layout only uses its own saved state (see Classic for
  `ServerOverlaysCollapsed`).
- Hidden panels stay inside the window, out of sight, so their bindings and features keep working.

## Classic

Classic keeps its fixed 1.0.2.0 layout (Favourites on its own tab, stacked panels) and ignores the custom layout.
The ⚙ button doesn't appear in Classic. A saved custom layout is kept, so switching back to another theme restores
it. Classic keeps its Server/Overlays arrow exactly as today, saved in `ServerOverlaysCollapsed`; that setting stays
Classic's and is never written by the custom layout. *(My call: Classic is frozen, and the custom layout may no longer
have a Server + Overlays row to share the state with.)*

## Saving

The layout is stored per player in `global.cfg` (not per settings profile) as one JSON setting: the tabs with their
rows and collapsed rows, plus the list of panels the player hid.

- A layout that can't be read at all (corrupt JSON, no tabs) falls back to the default and logs a warning.
- Unknown panel keys are dropped, and a second appearance of a panel is dropped (the first wins).
- A panel that is in neither a row nor the hidden list (for example one added in a later version) is added as a
  single row at the end of the first tab, so players see it.

## Non-goals

- Drag and drop on the main window.
- More than two panels in a row, or adjustable widths in a pair.
- A panel on more than one tab.
- Per-profile layouts, or sharing layouts between players.
- Changing what any panel contains.

## Anti-goals (would count as failure even if it works)

- A player can't get back to the editor, or to Settings, without editing files.
- Existing players' windows look different after updating before they touch the editor. Two agreed exceptions: the
  ⚙ button, and the collapse arrows that every row now has.
- A hidden panel stops its features working (its hotkeys, overlays, connection and auto-connect keep working
  whether its panel is shown or not).

## Constraints

- WPF on .NET Framework 4.8.1, C# 7.3; old-style csproj files with explicit `<Compile Include>` entries.
- Panels are the existing `GroupBox`es in `MainWindow.xaml`; moving them between tabs at runtime is already done by
  Classic (`ApplyClassicThemeLayout`). The code-behind refers to controls inside them by name, which keeps working
  when a panel moves.
- Pairing two wide panels (for example Global Settings with Audio Options) is allowed even though it is cramped at
  the current minimum window width.

## Approaches

- **A layout editor window (picked).** Quick to build, works with the keyboard, hard to get into a broken state.
- Drag and drop on the main window in an "edit layout" mode: more direct, but several times the work and fiddly with
  the collapse arrows and Classic.

## Left to the builder

- The editor window's exact look, within the theme's existing controls and styles.
- How the main window builds tabs and rows from the layout (re-parenting the existing GroupBoxes).
- The JSON shape, as long as it stores tabs (built-in id or custom name), rows (one or two panel keys), collapsed
  rows and hidden panels.
