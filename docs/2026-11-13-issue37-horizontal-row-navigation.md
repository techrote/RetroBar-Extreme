# Issue #37 — constrained task-row horizontal navigation

**Scope:** [Issue #37](https://github.com/techrote/RetroBar-Extreme/issues/37) · [Draft PR #38](https://github.com/techrote/RetroBar-Extreme/pull/38)

**Status:** Draft/HOLD. Physical Windows taskbar acceptance has not been performed. Do not merge or replace an owner's installed program.

## Access policy

With horizontal taskbars and active row rules, **keep the configured physical rows and intra-row order** and extend the scroll *extent* when a constrained row overflows. Expose small Webdings left/right page-navigation buttons, only when computed horizontal overflow exists; do not invent extra rows, relocate a matched app, or add settings. Both directions are bound to the owning ScrollViewer instead of global keyboard focus. Widths and row packing use a finite viewport, never the growing ItemsControl extent.

The pre-existing rule conflict when **every** row is marked exclusive but an unmatched app exists cannot satisfy all of "all apps visible", "exclusive rows", and "no extra rows" simultaneously. This PR preserves the legacy deterministic fallback for that impossible configuration rather than silently introducing a new policy. The owner should avoid configuring all available rows as exclusive when unmatched apps are running.

Without active rules, the native WrapPanel remains responsible for ordinary horizontal wrapping. Vertical taskbars retain vertical scroll navigation. With horizontal row rules, vertical scrolling may also appear if explicit row-height adjustments exceed the available height.

## Root cause and fix

Before the change, TaskRowPanel disabled rule packing if WPF ScrollContentPresenter measured its content with infinite width. TaskList also used ItemsControl.ActualWidth, which can grow with the row extent, as a feedback input to per-task widths.

TaskRowPanel now separates a finite, bindable ScrollViewer viewport from the full content extent. TaskList uses the finite outer scroll host width for stable button sizing; the theme's horizontal page buttons occupy space only on overflow. Settings, row-count and DPI-related updates invalidate layout, and bubbling keyboard-focus events request WPF BringIntoView on the focused descendant.

The red-phase WPF failure was captured independently of the fix at `4f25f9b088594f007bb38a05941d114f0ce7d7b9`, with the same specific exclusive-row assertion failing on both .NET 6 and 10 in [run 43454969964](https://github.com/techrote/RetroBar-Extreme/actions/runs/43454969964).

## Automated qualification

Run the standard pull-request workflow matrix (full Windows build/package, Extras for net6.0-windows and net10.0-windows, Start colourways and LUT textures). Extras is an actual [STAThread] WPF contract executable: it calls native Measure/Arrange/ScrollViewer, uses harmless synthetic ApplicationWindow identity data, and checks scrollbar offsets, row assignments, per-app sizing and focus-scrolling routes. The profile guard and Settings reflection scope prevent test writes to any owner installation.

WPF scenarios include infinite-width measure, one/multiple over-capacity rows, explicit/relative/exclusive/end-app rules, one-row mode, right/centre alignment, no-rule WrapPanel, vertical layout, adjustable row heights, per-app width/icon/label rules, narrow widths, resize, simulated fractional DPI scaling, keyboard-focus routing and theme navigation markup.

Each build output contains `source-provenance.json` with actual PR head/tree, tested checkout commit/tree and workflow run/attempt. A pull-request merge checkout can have a different commit SHA from its PR head; verify source and tested *tree* separately. Exact run links, final source SHA/tree, and portable artifact details belong in the PR description.

## Physical Windows x64 acceptance — owner checklist

1. Download the **RetroBar Portable 64-bit** artifact from the final-source RetroBar workflow; unpack in a separate folder. Do not run an installer or overwrite an existing installation.
2. On Windows 10/11, test a horizontal two-row taskbar. Reserve one explicit row for an app, set another app to Lowest Row, and overflow both rows with real windows. All matched apps must remain in their assigned physical rows.
3. Check exclusive-row rules and end-apps-at-bottom behaviour. Inspect ordering before and after scrolling; no extra rows or unexpected reassignment.
4. Verify left/right arrows appear *only* when overflow exists. Click and hold to reach first and last buttons; every button must be clickable, including while an arrow is visible.
5. Use Tab/Shift+Tab to reach off-screen buttons. Focus should bring them into view without activating a window until the expected activation gesture.
6. Check left/right context menus, drag/drop reordering and task activation. Verify configured task-button mouse-wheel actions and intentional DoNothing mode.
7. Exercise one-row mode, icon-only, long labels, per-app narrow/wide buttons, centre/right row alignment and a very narrow task list.
8. Resize the taskbar/Quick Launch grip, change row count and update/delete rules while applications remain open. Check no oscillation, stale positions, negative offsets or stuck navigation arrows.
9. Switch between horizontal and vertical taskbars, then disable all row rules. Verify legacy WrapPanel wrapping and vertical scroll behaviour.
10. Repeat at another display scaling factor (including non-integer effective DPI) and, if available, across two monitors; restart the portable candidate and recheck saved rules.

**GO/HOLD:** Automated success supports source and regression qualification only. Physical keyboard, pointer, visual, monitor/DPI, drag/drop and actual task activation acceptance remain **UNVERIFIED** until the owner performs the checklist. Do not merge on the basis of CI alone.
