# TASKBAR-MANAGEMENT-002 — Application Affinity (stage 2, Draft/HOLD)

Stacks on stage 1 (`taskbar-management-001-display-profiles`), fixing the independently observed layout-notification typo.

## Changes
- Four arrangement modes represented in model: Separate (legacy), Affinity (same-app windows remain separate but adjacent), Bundle, Hybrid.
- Ordered application-family groups, e.g. `terminals` first then `firefox.exe`, `explorer.exe`.
- Application presentation overrides and per-app bundle expansion (Flyout/Inline), globally and per display, with deep-copy persistence.
- Application tab with draggable-like Up/Down family ordering, optional example presets, program suggestions, scoped app policy editor and explicit Apply.
- WPF `ListCollectionView.CustomSort` grouping in non-Separate modes, stable per-window tie-break order, no restart on setting changes; all legacy modes leave original task collection intact.

**Important:** Bundle/Hybrid presentation is modelled here but buttons will not yet combine until stacked stage 3; this PR is **not merge-ready** on its own. Fullscreen display mode excluded.

## Verification
Tests cover opt-in defaults, group keys, per-display Hybrid overrides, deep-copy config, independent exclusion, and stable identity. Windows CI must pass on exact source; verify drag/drop ordering, keyboard Win+number positions, multi-monitor filters, window addition/removal, and Live profile updates physically before approval.
