# RetroBar Extreme quality audit — 4 November 2026

## Scope and source

This pass reviewed the current taskbar, settings, style editor, recovery script,
installer and GitHub Actions paths, then implemented bounded repairs for the
confirmed defects below. The starting point was `master` at
`65b36c0fba6ef8d8e44294b3ea332b03e32f638c`.

The separate manual-update change in [PR #35](https://github.com/techrote/RetroBar-Extreme/pull/35)
was already implemented and awaiting owner interaction acceptance. This branch
does not incorporate, replace or merge that work. Its settings and installer
changes concern different code paths. [Issue #3](https://github.com/techrote/RetroBar-Extreme/issues/3)
also remains an owner comparison of text-rendering prototypes; this audit does
not select a visual preference on the owner's behalf.

Three parallel source reviews covered layout/lifecycle, styles/rendering and
build/recovery. A separate review of the resulting settings and recovery changes
was also performed. Native Windows results and the final source identity are
recorded in the implementation PR and its attached Actions runs.

## Confirmed defects and repairs

| Area | Defect and consequence | Repair | Regression evidence |
| --- | --- | --- | --- |
| Settings startup | Deserializing the valid JSON document `null` replaced defaults with null before migration inspection failed. Startup consumers then dereferenced the missing settings. A failure while checking a migration could similarly publish rejected state. | Deserialize and inspect migration state in a local variable; publish only after both succeed. Reject null manager assignments. Invalid files are not rewritten merely by loading them. | Real `SettingsManager<T>` tests cover missing, malformed, null and wrong-shaped JSON, a migration getter failure, and a successful migration. |
| Settings values | Null theme/path/clock strings and icon lists could erase defaults. Null entries inside reference-type rule/icon arrays were later dereferenced by consumers. Legacy `PinnedNotifyIcons: null` also failed migration. | Preserve usable defaults for null required fields, filter null reference-type elements while preserving valid order, and handle legacy nulls. Value-type icon records retain JSON's document-rejection behavior. Optional fields retain their existing normalization. | Real production JSON setters are exercised for required strings, each list category, valid neighbouring entries and legacy pinned icons. |
| Settings persistence | Every change truncated and rewrote the live settings file. A write interruption could destroy the last complete saved configuration. | Serialize first, write a unique adjacent temporary file, flush it, then replace the existing file or move it into place on first save. Clean up a failed staging file. There is no destructive overwrite fallback. | Sharing-denied replacement preserves the previous bytes; retry, first save, UTF-8 round trip and temporary-file cleanup are checked. |
| Quick Launch shortcuts | Hiding Quick Launch unloads its folder while its hotkey subscription remains. Win+number could dereference `Folder.Files` after that unload. | Ignore hotkeys until the visible toolbar, host and folder are available. | Invoke the real handler with an unavailable toolbar/folder and representative indices. |
| Hotkey lifecycle | Disposing a manager left its singleton settings subscription attached. Later settings changes could reach its destroyed native listener. | Unsubscribe, guard disposal and callbacks, and destroy the native window in a `finally` block. | Verify subscription removal, window destruction and repeated disposal without registering global shortcuts. |
| Task-row alignment | Explicit and relative rules can resolve to the same physical row. Applying both right alignments shifted a 100-pixel button to X=800 in a 500-pixel row. | Resolve alignment once per physical row, using the last matching rule, consistent with existing row-height precedence. App assignment is unchanged. | Real WPF one- and two-row layouts check right/centre/left precedence, bounds and preserved row assignment. |
| Show Desktop lifecycle | Each replaced Quick Launch data item was retained by an anonymous handler on the singleton settings object. Old items continued refreshing after taskbar rebuilds. | Use a named handler through WPF's weak `PropertyChangedEventManager`. | Independent lifecycle/source review and the Windows build; no nondeterministic garbage-collection assertion. |
| Gradient import/rendering | A legal JSON exponent such as `1e999` became infinity, survived import and then failed during automatic JSON save. Large finite angles could overflow the radians calculation. | Reject non-finite imported angles and offsets. Reduce finite angles before addition and conversion to radians. Invalid properties remain isolated from valid neighbours. | Malformed-import isolation, subsequent serialization, extreme finite angles, colour preservation and normal orientation are checked against production code. |
| Part reset | “Reset this part” removed colours or Start recolouring but retained its border multiplier. | Reset the selected part's brushes, borders and Start recolouring together, preserving other parts and both palettes. | Every part, Start, border-only styles, neighbouring state and disk round trips are covered. |
| Border editing | A private 350 ms timer changed whichever document `_style` referred to at tick time. Resetting or switching styles could therefore receive a stale slider value; closing could miss the last edit. | Change the current document immediately and debounce only its save through the existing shared timer and flush path. | The real compiled editor is adjusted offscreen; immediate state and the shared timer are checked, then a detached/reset document is observed past the former delay. |
| Runtime installation | Both .NET prerequisite URLs still used the retired `dotnetcli.azureedge.net` distribution domain. | Switch those URLs to Microsoft's path-compatible `builds.dotnet.microsoft.com`. This is separate from the application updater in PR #35. | Installer compilation, source contract and Microsoft's published migration guidance. |
| Test-build recovery | An incomplete `backup/program` directory was treated as a valid backup. Restore deleted the installed files before copying that unverified backup; settings-copy failures were suppressed and absent files could be resurrected. | Stage and validate snapshots before committing them, verify a file-hash inventory before restore, record settings-file absence, stage the replacement installation, retain rescue copies and roll back failed swaps/restores. Serialize script operations with a file lock. Refuse legacy or incomplete snapshots for automatic restore. | Isolated PowerShell fixtures execute the actual script with fake build/process commands and real temporary filesystem operations, including corruption, failure and rollback cases. |
| Artifact links | A successful older workflow could finish after a newer push and overwrite the PR's download links with stale artifacts. A copied marker could also select a human comment. | Check the live PR source repository, branch and SHA before publication; serialize callbacks; retain newer run/attempt evidence; paginate and update only the Actions bot's comment. Exclude expired artifacts. | Node fixtures execute the actual inline workflow script with controlled API responses for stale heads, mid-read pushes, forks, pagination, markers and reruns. |
| Qualification identity | Contract results called the PR source SHA their head although default checkout tests GitHub's synthetic merge commit. | Retain merge-integration testing and record `Head` (intended source), `TestedCommit` and `TestedTree` separately. Artifact comments explicitly describe merge builds. | Workflow inspection and native result artifacts. |

Microsoft documents both the affected distribution domains and the path-compatible
replacement in [Critical: .NET install links are changing](https://devblogs.microsoft.com/dotnet/critical-dotnet-install-links-are-changing/).
The settings replacement uses the documented [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace)
operation with staging on the same volume. Flushing and replacing greatly reduces
partial-write exposure; it is not a promise of recovery from every filesystem,
storage-device or power failure.

## Verification and safe test execution

The existing Windows jobs remain the qualification surface: the ten publish
targets, installer and portable packaging, plus the two-framework Extras,
Start-colourways and texture suites. The new regressions extend those suites;
they do not add another build matrix. Workflow and recovery-script tests run once,
in the .NET 10 Extras job.

The WPF test runners now refuse an existing `%LOCALAPPDATA%\RetroBar` directory
before constructing controls. Settings initialization can migrate a profile even
when a fixture intends only to read it. Use a clean test account or the normal
GitHub-hosted runners; do not remove an owner's directory to bypass this check.
Settings-manager persistence tests write only to a unique temporary directory.
Layout fixtures suppress settings autosave and restore every setting they change.
The border-editor fixture stops the shared save timer before processing events.
No test starts the taskbar shell or registers global Win+number shortcuts.

On a clean Windows test account, the principal commands are:

```powershell
dotnet run --project tests/RetroBar.Extras.Tests -c Release -f net10.0-windows -- extras-evidence
dotnet run --project tests/RetroBar.StartColourways.Tests -c Release -f net10.0-windows -- start-evidence
dotnet run --project tests/RetroBar.Textures.Tests -c Release -f net10.0-windows -- texture-evidence
node --test tests/workflows/comment-pr-artifacts.test.cjs
pwsh -NoProfile -File tests/install-test-build.Tests.ps1
```

CI also runs the C# suites on `net6.0-windows`. The workflow script fixtures make
no GitHub writes. The PowerShell fixture substitutes build and process operations
and uses a disposable profile root; it does not install RetroBar on the test host.

The development container used for this audit is Linux without a Windows Desktop
runtime. XML parsing, whitespace checks and Node fixtures can run there; they do
not substitute for compilation and execution in the Windows jobs. Similarly,
CI WPF rendering does not establish physical DPI, Explorer, multiple-monitor,
keyboard or owner visual acceptance.

## Recovery-script compatibility

The first verified program snapshot remains the baseline installation. Settings
and Style.json are captured before each test install, preserving the script's
existing “latest pre-test settings” behavior. Older complete settings snapshots
and the displaced program are retained for manual recovery.

Snapshots created by the old script have no trustworthy completion/inventory
record. The new script deliberately refuses to restore them automatically and
leaves them intact. Review and recover such a backup manually, or move the old
backup aside after preserving it before establishing a new verified baseline.
The script does not fabricate a completion marker for an unverified directory.

Neither a successful `Start-Process` nor a green packaging job proves that the
desktop application remained healthy after launch. The interaction check below
is still needed before treating a build as accepted.

## Owner interaction check

Use the portable build from the PR's current successful build run. Record its
source SHA and run link alongside the result. Keep a copy of existing settings
before a normal desktop test.

1. Start the build with your existing theme and settings. Change one setting,
   close normally and reopen; confirm it persists.
2. With Win+number set to Quick Launch, hide Quick Launch and press Win+1. Confirm
   there is no error dialog. Show it again and verify its ordinary shortcut works.
3. On a one-row taskbar, combine an explicit first-row rule and a lowest-row rule.
   Check right, centre and left alignment; buttons should remain visible. Repeat
   after changing the row count.
4. Change the selected part's border, then reset that part. Confirm both colour
   and border return to the theme. Move the border slider and immediately change
   styles or close Properties; the later style should not receive the old value.
5. Rebuild the taskbar by changing scale/theme with Show Desktop inside Quick
   Launch. Confirm its icon and action still work.

## Remaining limits

- **Concentrated row overflow:** assigning more button width to one permitted row
  than that row can contain can still overflow horizontally. The task list's
  current scrolling is vertical. A separate layout change must choose how to
  preserve access: shrink constrained buttons, provide horizontal navigation or
  allow overflow rows. This audit fixes repeated alignment without silently
  changing the owner's app-to-row rules.
- **Settings validation is bounded:** null documents, null fields and null
  collection elements are covered. This is not a new comprehensive schema for
  arbitrary hand-edited dimensions, enum values or every nested object field.
- **Physical platforms:** the supplied evidence does not claim a Windows 7/8,
  ARM64, mixed-DPI or multi-monitor interactive session merely because a target
  compiles. Those need the corresponding machine and an owner check.
- **Other active work:** PR #35's manual-update owner check and issue #3's text
  rendering comparison retain their own acceptance requirements.

No release or default-branch merge is part of this audit branch.
