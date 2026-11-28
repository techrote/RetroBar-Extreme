# TASKBAR-MANAGEMENT-003 — Bundle/Hybrid renderer (stage 3; Draft/HOLD)

## Implemented
- Existing WPF `TaskList` remains the default renderer for Separate/Affinity; task windows are **not** destructively combined.
- Bundle/Hybrid builds a display-filtered projection of windows with one bundle button for each eligible application that has multiple windows. A singleton stays an ordinary TaskButton.
- Bundle click opens a flyout window-selection menu, or expands a list of original task buttons **inline**, controlled by app/policy and display scope.
- Same application HWNDs join a stable bundle on creation; closing a member rebuilds safely. Active inline keys persist across source collection refreshes.
- Win+number in Bundle/Hybrid uses displayed item positions, not the old full window list.
- New deterministic affinity comparer observes explicit family order, keeps unlisted families in first-seen order, honours Hybrid Separate presentation and returns 0 for identical windows.
- Uses legacy per-window `TaskButton` controls for singleton and inline entries, preserving their context menus and standard button functionality.

## Windows acceptance gates
1. Test Separate and Affinity first: original buttons, drag/drop, running state, Win+number, Ctrl/Shift-modified taskbar operations.
2. In Bundle mode, launch 1, 2, 20 Firefox windows; verify count, single-bundle presentation, flyout options, last-window transition, closing a focused flyout member and no ghost buttons.
3. Change Firefox to Inline, expand/collapse with 2, 20 windows; verify labels, taskbar scroll position, scroll wheel, context menus and active-window indication.
4. In Hybrid, Firefox Flyout, PowerShell Inline, Explorer Separate; ensure groups and windows are not unexpectedly combined across executable identity.
5. Repeat on Left/Right and Top/Bottom taskbars, 1–5 rows for this candidate, with per-monitor app allow/deny rules, global/monitor policy overrides and various DPI. Exercise 6–32 rows only after deliberate integration of separate #38.
6. Verify native tray/Start/accessibility/keyboard focus with primary monitor's taskbar disabled and another display as shell host; unplug and reconnect monitors mid-session.
7. Verify theme scaling, button widths, hover previews and context menus against the owner baseline. Source-matched CI is necessary but not sufficient.

## Known limitations / HOLD
- Flyout uses a standard WPF context-menu chooser; unlike an inline expanded TaskButton, its entries don't expose full live thumbnail previews or native per-window context menus.
- Inline children on horizontal taskbars use compact left/right paging; **production** TaskList/TaskRowPanel/TaskBundleEntryView WPF regressions cover 20 windows and explicit/exclusive row ownership. Visible bundle headers own minimize/restore rectangles for all their members, and vertical Inline children reserve the 8-DIP indentation. Physical owner sizing, pointer, shell rectangle animations and overflow acceptance are still required.
- The combined #44 branch currently inherits the five-row cap from master. The separate, unmerged #38 implements 32 rows. The 1–32-row acceptance matrix must wait for an explicit integration; this candidate is limited to the existing 1–5 rows.
- Display profile identity is `AppBarScreen.DeviceName`, with a primary-screen recovery fallback, pending EDID/connector identity hardening.
- No fullscreen taskbar/dashboard. Explicitly deferred.
- No physical Windows testing by the agent. Keep all three stacked PRs Draft/HOLD and do not merge until review and owner acceptance.
