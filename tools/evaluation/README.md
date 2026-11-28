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
  Automatic restart is suppressed, preventing a silent switch into default
  settings on process recreation.
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
4. Run `RUN-ISOLATED-EVALUATION.cmd` or the marked `RetroBar.exe` directly.
5. Confirm the evaluator uses private settings. Follow the current milestone-specific acceptance checklist;
   capture PASS/FAIL, screenshots and logs.
6. Exit normally, verify Explorer's native taskbar returns, and confirm
   `%LOCALAPPDATA%\RetroBar\settings.json` is unchanged.

## Recovery

If the desktop becomes unresponsive, use **Ctrl+Shift+Esc** to open Task Manager,
end the isolated `RetroBar.exe` if necessary, then restart Windows Explorer or
use Task Manager's **Run new task** to start `explorer.exe` if the native
taskbar has not returned. Ending the process forcibly bypasses graceful
shutdown, so recovery may require manual action.

## Limits

Not approved for production use. The standalone M001 candidate is based on
master; #38, #44, #47 and #39 are not incorporated. Fullscreen is out of scope.
Draft/HOLD, no merge. See issue #48 for the integrated acceptance gates.
