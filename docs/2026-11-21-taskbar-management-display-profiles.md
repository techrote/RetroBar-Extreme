# TASKBAR-MANAGEMENT-001 — Display Profiles (stage 1, Draft/HOLD)

## Scope
Opt-in display profiles select which monitors run RetroBar, each monitor's Left/Top/Right/Bottom edge, row count, vertical width, auto-hide, Start, quick launch, notification icons and clock, the window source (all screens or local), and an include/exclude application list. `terminals` is a built-in convenience family. This PR does **not** implement app affinity or bundling; subsequent stacked stages implement those.

## Backwards compatibility and recovery
- `UseDisplayProfiles` defaults to false; original `ShowMultiMon`, `Edge`, `RowCount`, `TaskbarWidth`, `AutoHide` and `MultiMonMode` stay unchanged.
- Profiles are indexed by Windows `AppBarScreen.DeviceName` and dormant when not connected. **This is not a physical-monitor identity**: if Windows reassigns `DISPLAYn`, restrictive or disabled settings may attach to a different physical screen. After reconnect/re-enumeration, inspect every profile in the Displays editor; stable EDID/connector mapping remains a separate pre-merge hardening requirement if arbitrary topology changes must be supported.
- If none of the configured enabled profiles matches an active screen, **always create a temporary primary-monitor recovery taskbar**. Settings are not erased.
- The selected shell host is an enabled profile with notification area preference, falling back to the primary among enabled screens or the first enabled screen. This host owns missed/live balloons independently of `Screen.Primary`; profile-enabled Start buttons use their own HWND-targeted launch. Primary-disabled scenarios still need physical tray/Start/Win+number qualification.
- Shared notification-area icon filtering now uses a static predicate and reference-counted view ownership; teardown of one monitor does not leave its control captured or clear another active monitor's filter.
- The editor applies changes on explicit Apply, avoiding AppBar rebuilds per text keystroke.

## Windows owner acceptance
1. On a separate evaluation install, turn on Displays profiles and verify unchanged legacy layout.
2. Set main/primary display disabled, monitor 2 Left/all windows, monitor 3 Bottom/terminal-only/all-monitor source; verify the Windows shell tray, Start menu, task buttons, window activation and Win+number.
3. Verify alternating orientation for live screens, row/width edits, taskbar auto-hide, taskbar drag and resize, quick launch and visibility of icons and clock.
4. Move windows between monitors; test all-window and local-window sources independently of app filters.
5. Unplug configured monitors, restart RetroBar, reconnect in different order and test primary-display changes; confirm recovery bar appears when none of the enabled profiles is present.
6. Test 100%/125%/150%/200% mixed DPI, screen-edge AppBar reservations, horizontal multi-row overflow, top/bottom row rules, and restore of Windows Explorer taskbar on exit.
7. Toggle advanced profiles off; ensure legacy settings and taskbar order are unchanged.

**Hold reasons:** No physical Windows/multi-monitor interaction performed by the agent. CI verifies source compilation and model contracts, not shell/AppBar behaviour. Do not merge or install over live RetroBar until acceptance.
