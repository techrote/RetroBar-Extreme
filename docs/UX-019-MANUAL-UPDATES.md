# UX-019 — Manual updates for RetroBar-Extreme

Tracking: [issue #34](https://github.com/techrote/RetroBar-Extreme/issues/34).

## Problem and behavior

The inherited updater contacted the stock RetroBar version feed ten seconds after startup
and then daily. It could direct an Extreme user to stock builds that lack the fork's features.
The installer independently contacted the same feed before showing its wizard.

Extreme now uses manual application updates. The existing `Updater` interface remains so
App, WindowManager and Taskbar need no lifecycle changes, but it creates no HTTP client,
timer or settings subscription. It never reports an available update. The Properties
window offers a user-activated link to this fork's releases instead of an automatic-check
checkbox; the installer uses the same fork URL for its update metadata and has no version probe.

The old `CheckForUpdates` JSON property remains readable and writable for compatibility.
Missing, false and true values all produce false; an old true value cannot enable anything.
Ignoring this flag does not force a file rewrite. The next ordinary settings save writes false.
Other settings and saved styles keep their existing format.

The installer still offers a missing Microsoft .NET Desktop Runtime as a separately
disclosed prerequisite. This change removes application-update polling, not all network use
by an installer or a browser the user explicitly opens.

## Qualification

The existing **Extras contracts** workflow runs the new checks on both `net6.0-windows`
and `net10.0-windows`: real settings deserialization/serialization for the legacy flag,
updater construction and repeated disposal, HTTP request observation beyond the former
ten-second startup delay, and the Properties/installer manual-update contract. The normal
**RetroBar** workflow compiles all application targets, the portable archive and the installer.

The settings fixture requires a clean test profile and refuses to run if a real RetroBar
`settings.json` already exists, before loading the settings type. The HTTP observer is
checked against an in-memory handler first; that positive control opens no network connection.
The manual-update row itself is instantiated in WPF to validate the typed navigation URI.

These checks establish the software contract. They do not establish owner acceptance of
the changed Properties row. Keep the PR draft until the owner checks a final-head portable
CI build and records the result on issue #34. The issue/PR carry the exact commit, run and
artifact links, so historical release assets are not mistaken for the repaired build.

## Owner check to attach to the final build

- **Download:** the issue's exact-head `RetroBar Portable 64-bit` CI artifact; normal Windows
  11 builds need the matching .NET 10 Desktop Runtime if it is not already installed.
- **Try:** quit the current RetroBar, run the extracted build, open Properties → Taskbar,
  follow **RetroBar-Extreme releases**, then close Properties and quit RetroBar.
- **Good:** the row says updates are manual; the link opens `techrote/RetroBar-Extreme`
  releases; no automatic-check checkbox or stock-update prompt appears; quitting restores
  the normal Windows taskbar. Existing styles and layout are retained.
- **Reply:** PASS with the tested commit, or which step failed.
