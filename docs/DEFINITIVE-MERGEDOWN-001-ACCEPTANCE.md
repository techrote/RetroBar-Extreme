# RETROBAR-EXTREME — DEFINITIVE-MERGEDOWN-001
## Owner Windows acceptance card

**Candidate:** cumulative M004 evaluator, GitHub PR #51. Read source-provenance.json and record the actual source_head, source_tree and workflow_run here. Never substitute an older build. All shell actions are real Windows actions; this is profile isolation, not a VM.

### Preflight (stop on failure)
- [ ] Save ongoing work. Confirm no other RetroBar process is running after graceful exit. Back up the installed profile and note the production settings.json SHA-256 if present. Extract the **entire marked evaluator ZIP** into a fresh folder. Do not overwrite any installation.
- [ ] Verify the ZIP's published SHA-256 and compare Get-FileHash -Algorithm SHA256 .\RetroBar.exe against RetroBar.exe.sha256. Record actual monitor names, primary monitor, resolution and scaling. Ensure the evaluation profile is private and not a junction/link; do not copy production settings.
- [ ] Run RUN-ISOLATED-EVALUATION.cmd, confirm the explicit shell takeover warning, verify settings and logs are in %LOCALAPPDATA%\RetroBar-Evaluation. Confirm startup/Quick Launch location editing is disabled and production settings/Windows pinned shortcut directory are unchanged. Do not deliberately crash or create competing shell owners on a working desktop.

### Layout and preserved baseline
- [ ] With display profiles disabled, test top/bottom/left/right, lock/unlock, auto-hide and Start/Show Desktop/tray/clock/Quick Launch. Confirm no default layout regressions, native and stretch sizing, sharp text, two visually distinct themes, custom Start image, fonts/styles/LUT textures, and manual-only updates.
- [ ] Horizontal rows: begin at 1, then 5, 16, 24 and 32 if screen height permits. Try custom app widths and row height adjustments. Assign explicit and exclusive rows, including row 32 and relative Lowest row. Force overflow; verify whole button faces, left/right page arrows, keyboard focus and last-task reachability. Relax the rules: stale arrows must retract. Reduce rows back to 1; verify tray and usable work area. If height is excessive, reduce rows or scaling, record the failure and stop that case.
- [ ] Side packing: Properties > Start/Quick > Pack beside Start on side taskbars when space allows. With 0/1/4/12 private shortcuts, widen/narrow past fit threshold; complete toolbar packs or stacks without clipping. Reverse order, test all Show Desktop placements, custom Start sizing, drag/drop/reorder and persistence. Test the opt-out and a monitor with Start hidden.

### Three-monitor management
- [ ] Enable display profiles. Example: primary monitor disabled; secondary left dock with all windows and tray/Start; third bottom or right dock filtered to terminals (all monitors). Verify one shell host, tray live and missed balloons, Win+number task and Quick Launch actions, Start menu on intended display, and Explorer work-area bounds. Do not assume DISPLAYn names survive reconnect.
- [ ] Configure Firefox Flyout, terminals Inline, Explorer Separate; try Separate/Affinity/Bundle/Hybrid, groups of 1/2/20 windows, individual activation, right-click native menu, minimise/restore on correct monitor and no duplicate targets. On horizontal profile, force constrained and expanded Inline overflow; collapse with row rules active and then remove rules. On vertical profile, verify no horizontal scroll and no clipping of inline child buttons.
- [ ] Independently vary row counts and edge (e.g. bottom 5/32 and left vertical). Verify icon wrapping/clock/Start/Quick visibility follows each host, not global edge. Move a window between monitors, test local/all-monitor overlap and app-excluded shell host. Disconnect/reconnect a display, verify recovery bar when all configured displays unavailable, verify profile identity and no stranded taskbar.
- [ ] Change settings live, exit normally, relaunch the same marked evaluator. Confirm display profiles, rule assignments, styles and private shortcut order persist. Verify old defaults still work with profiles disabled.

### Closure and recovery
- [ ] Exit normally and confirm Explorer's native taskbar returns; compare production settings hash and pinned shortcut folder against preflight. Capture any errors and relevant evaluator logs (private Logs directory). A crash, shell takeover problem, inaccessible native window, wrong profile mapping or unexplained production write is FAIL; do not install or merge.
- If shell becomes unresponsive: Ctrl+Shift+Esc opens Task Manager; end only the isolated evaluator if necessary; restart Windows Explorer or run explorer.exe. Forced termination bypasses cleanup. Do not relaunch automatically. Preserve logs and screenshots.
- Record each gate PASS / FAIL / NOT RUN, source identifiers, monitor configuration, screenshots and any deviations. Report on issue #48 or PR #51. Plasma and fullscreen dashboard are explicitly out of scope.

**Disposition:** Draft/HOLD. No automated merge. Physical acceptance and separate authorization are required for production integration. No CI test asserts actual owner shell behaviour.
