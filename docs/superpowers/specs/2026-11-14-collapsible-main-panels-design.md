# Collapsible panels on the General tab

## Why

Players want to hide clutter on the main window's General tab. Collapsed sections make the General tab shorter; the
other tabs keep their full height.

## What

- A small arrow in the top-right corner of a section collapses or expands it:
  - **Server, Overlays and Combat Box RCI** together, with one arrow on the Overlays header.
  - **Favourites**.
- Collapsed, a section keeps only its header bar (both the Server and Overlays headers stay side by side). The arrow
  points up (▴) when expanded and down (▾) when collapsed. The RCI panel is hidden while its section is collapsed.
- Window height: the player's normal height is the one saved today (`ClientHeight`). On the General tab the window is
  that height minus the height of the collapsed sections; on the other tabs it is the normal height. Resizing while
  sections are collapsed sets the normal height to the new height plus the collapsed sections. The minimum height on
  the General tab drops by the same amount.
- Whether each section is collapsed is saved between runs.
- Classic: only the Server, Overlays and RCI arrow (Favourites has its own tab there). Classic's fixed height shrinks
  the same way on the General tab.
- A maximised window isn't resized.

## Non-goals

- Collapsing the Microphone and Speakers row, or anything on other tabs.
- Animation.

## Approaches

- **Arrow placed over each group box (picked).** Wrap the two sections in a grid and lay a small arrow button over the
  top-right of the header. Code-behind hides the section contents, saves the setting and adjusts the height. It
  works the same in the military themes and Classic, and doesn't touch the shared group box template.
- Restyled `Expander`: its header and arrow sit on the left by default, so it needs a new template for each theme
  family to look like the existing panels.
- An "is collapsible" option in the group box template: changes the shared template that every panel in the client
  uses, and Classic has no group box template of its own to change.

## Left to the builder

- Exact arrow size, colour and position in each theme family.
- Measuring a section's height (layout height before and after hiding it).
