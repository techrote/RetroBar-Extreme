# RetroBar — Isolated Windows evaluator (Draft/HOLD)

This is an opt-in, **special Windows CI artifact**, not the generic portable
package. No changes to ordinary RetroBar startup defaults are required.

## Safeguards

- The package includes `RetroBar-Evaluation.marker`. The EXE detects it
  **before** Settings or ManagedShell initializes; even double-clicking the EXE
  directly selects the isolated profile. `RUN-ISOLATED-EVALUATION.cmd` supplies
  `--isolated-evaluator` explicitly.
- Evaluator settings, saved styles, user images and other `InLocalAppData`
  paths use `%LOCALAPPDATA%\RetroBar-Evaluation\`, separate from
  `%LOCALAPPDATA%\RetroBar\settings.json`. Do not manually copy installed
  settings into the evaluator unless you intend to test their content.
- Quick Launch uses `%LOCALAPPDATA%\RetroBar-Evaluation\QuickLaunch\`.
  Selecting other folders or dropping shortcuts to other toolbar destinations
  is blocked; installed Windows pinned shortcuts remain untouched.
- Windows auto-start registry modification is disabled for the evaluator.
  Automatic restart is suppressed. The evaluator's unhandled dispatcher
  exception path attempts Explorer restoration and WPF shutdown without
  invoking the production automatic-restart/FailFast path.
- The existing named mutex `RetroBar` prevents coexistence with installed or
  generic-portable RetroBar; the evaluator checks known RetroBar process names
  as well. It **refuses immediately before shell initialization** if another
  RetroBar instance is present. It does not kill, replace, or interrupt that process.
- Ordinary installed/unmarked portable builds with no arguments keep the
  original `%LOCALAPPDATA%\RetroBar\` path and previous startup semantics.

**Disk profile isolation is not desktop isolation.** Even in evaluator mode,
RetroBar takes over shell taskbar responsibilities and may hide the native
Explorer taskbar. Do not launch until other important Windows work is finished.

The startup guard rejects junctions, symlinks, inaccessible entries and other
reparse points anywhere in the existing evaluator profile; private path access
also checks the existing path components. Do not redirect or modify the private
profile while the evaluator is running. This does not defend against a hostile
local account, hardlinks, or concurrent filesystem modification.

The marked artifact includes source-provenance.json (source head/tree, tested
checkout/tree, workflow run/attempt) and RetroBar.exe.sha256. Check these against
the acceptance record; use the current exact-tree package only.

## When ready for Windows acceptance

1. Finish ongoing work and save documents. Do not test while Windows
   responsiveness is needed by other processes.
2. Verify that **no other RetroBar is running**. Exit it gracefully, and keep
   existing settings backed up. Do not kill a production RetroBar just to
   circumvent the evaluator launch guard.
3. Extract the entire **RetroBar Isolated Evaluation 64-bit** artifact into a
   fresh folder, including `RetroBar-Evaluation.marker`.
4. Run `RUN-ISOLATED-EVALUATION.cmd` (recommended). Do not launch an EXE
   separated from its marker. The launcher supplies the explicit isolation flag.
5. Confirm the evaluator uses private settings. Follow
   `ACCEPTANCE-MERGEDOWN-001.md` in this package; capture PASS/FAIL,
   screenshots and logs. An error-triggered exit is a failed acceptance test,
   even if Explorer recovery succeeds.
6. Exit normally, verify Explorer's native taskbar returns, and confirm
   `%LOCALAPPDATA%\RetroBar\settings.json` is unchanged.

## Recovery

If the desktop becomes unresponsive, use **Ctrl+Shift+Esc** to open Task Manager,
end the isolated `RetroBar.exe` if necessary, then restart Windows Explorer or
use Task Manager's **Run new task** to start `explorer.exe` if the native
taskbar has not returned. Ending the process forcibly bypasses graceful
shutdown, so recovery may require manual action.

## Limits

Not approved for production use. This cumulative candidate includes the
evaluator, #38, #47 and #41–44 taskbar-management stack. Plasma (#39)
and fullscreen (#45) remain excluded. Display names (DISPLAYn) can be
renumbered after monitor topology changes; verify mappings after reconnect.
32 rows is a configured capacity, not a guarantee that every scale and screen
height can display all rows comfortably. Draft/HOLD until explicit owner
acceptance and integration authorization. See mission issue #48.
