# UX-SIDE-QL-PACK-001 — Quick Launch packing on side taskbars

## Scope

Optional, adaptive packing of the **entire** Quick Launch toolbar beside the Start/Show Desktop section on left- and right-docked taskbars. The fallback is the existing full-width, vertically stacked Start and Quick Launch arrangement. Top- and bottom-docked taskbars retain the previous sequential horizontal geometry.

**Feature setting:** Properties → Start/Quick → "Pack beside Start on side taskbars when space allows". Disabled by default for source-compatible rollout. No change to shortcut files, application activation, shell ownership, drag-and-drop, tray, task buttons, or existing Quick Launch length and ordering settings.

## Implementation

- `AdaptiveStartQuickPanel` owns the two existing visual children for their entire lifetime. Reordering or switching from packed to stacked does not reparent, unload or recreate the Toolbar.
- When packing is enabled and both controls are visible, the side-taskbar measure pass requests natural widths. It chooses packed mode only when both sections have nonzero content and their complete combined width plus a 4-DIP gutter and 2-DIP rounding reserve fits, and the row fits the available height.
- Packed mode gives Quick Launch a finite remaining width. Horizontal and stacked modes preserve the old DockPanel's finite remaining-space constraints, in visual order; stacked mode gives Quick Launch the full sidebar width for wrapping. Opt-out skips the natural-width probe entirely.
- Layout remains entirely WPF-driven; there is no timer, screen-resolution guess, fixed-width shortcut assumption or OS-level taskbar mutation.
- The full taskbar root remains the height source for custom Start-button layout; the new leading host is not substituted for the root.

## Automated regression coverage

`tests/RetroBar.Extras.Tests/SideQuickLaunchContracts.cs` exercises fit thresholds, resize transitions, stable parentage, reversed order, opt-out, hidden sections, horizontal layout and a changed Start-button width. Independent review follow-up adds constrained DockPanel-equivalence cases, actual QuickLaunchPanel wrapping, insufficient available height and the visible-but-empty StartArea case. Run the existing Windows Extras contracts on **net6.0-windows** and **net10.0-windows**, and the normal package/CI workflows.

## Manual Windows acceptance (required before merge)

- Test both side edges and top/bottom non-regression with the actual current theme and a second materially different theme.
- Verify all Show Desktop placements: beside/under Start, beside/inside Quick Launch and taskbar end.
- Resize taskbar width through the threshold while Quick Launch has 0, 1, 4 and 12 shortcuts; verify icons never disappear or overlap task buttons.
- Exercise Quick Launch first, custom length (including live grip resizing), per-icon fill, icon drag/drop and order persistence.
- Verify custom Start PNG, no Start text, hidden Start on secondary display, custom margins, native/stretch scaling and mixed-DPI transitions.
- Test lock/unlock, auto-hide, real Start menu and Show Desktop actions; confirm no repeated load/unload, shortcut folder resets or frozen taskbar.
- Evaluate from the explicitly guarded isolated evaluator (PR #46) if that stack is separately approved; it changes the active shell and is **not** virtualized. Never test over a running installed RetroBar.

**Status:** Draft/HOLD until source-matched Windows CI, independent review and owner physical acceptance. Do not merge, auto-merge, promote to Ready, install or release automatically.
