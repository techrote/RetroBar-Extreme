# UX-SIDE-QL-PACK-001 — Quick Launch packing on side taskbars

## Scope

Optional, adaptive packing of the **entire** Quick Launch toolbar beside the Start/Show Desktop section on left- and right-docked taskbars. The fallback is the existing full-width, vertically stacked Start and Quick Launch arrangement. Top- and bottom-docked taskbars retain the previous sequential horizontal geometry.

**Feature setting:** Properties → Start/Quick → "Pack beside Start on side taskbars when space allows". Disabled by default for source-compatible rollout. No change to shortcut files, application activation, shell ownership, drag-and-drop, tray, task buttons, or existing Quick Launch length and ordering settings.

## Implementation

- `AdaptiveStartQuickPanel` owns the two existing visual children for their entire lifetime. Reordering or switching from packed to stacked does not reparent, unload or recreate the Toolbar.
- Its side-taskbar measure pass first requests natural widths. It chooses packed mode only when both controls are visible and their combined width plus a 4-DIP gutter and 2-DIP rounding reserve fits.
- Packed mode gives Quick Launch a finite remaining width; stacked mode measures against the full sidebar width, retaining existing icon wrapping.
- Layout remains entirely WPF-driven; there is no timer, screen-resolution guess, fixed-width shortcut assumption or OS-level taskbar mutation.
- The full taskbar root remains the height source for custom Start-button layout; the new leading host is not substituted for the root.

## Automated regression coverage

`tests/RetroBar.Extras.Tests/SideQuickLaunchContracts.cs` exercises fit thresholds, resize transitions, stable parentage, reversed order, opt-out, hidden sections, horizontal layout and a changed Start-button width. Run the existing Windows Extras contracts on **net6.0-windows** and **net10.0-windows**, and the normal package/CI workflows.

## Manual Windows acceptance (required before merge)

- Test both side edges and top/bottom non-regression with the actual current theme and a second materially different theme.
- Verify all Show Desktop placements: beside/under Start, beside/inside Quick Launch and taskbar end.
- Resize taskbar width through the threshold while Quick Launch has 0, 1, 4 and 12 shortcuts; verify icons never disappear or overlap task buttons.
- Exercise Quick Launch first, custom length (including live grip resizing), per-icon fill, icon drag/drop and order persistence.
- Verify custom Start PNG, no Start text, hidden Start on secondary display, custom margins, native/stretch scaling and mixed-DPI transitions.
- Test lock/unlock, auto-hide, real Start menu and Show Desktop actions; confirm no repeated load/unload, shortcut folder resets or frozen taskbar.
- Evaluate from the explicitly guarded isolated evaluator (PR #46) if that stack is separately approved; it changes the active shell and is **not** virtualized. Never test over a running installed RetroBar.

**Status:** Draft/HOLD until source-matched Windows CI, independent review and owner physical acceptance. Do not merge, auto-merge, promote to Ready, install or release automatically.
