# PLASMA-M0 — opt-in animated taskbar background

**Draft / HOLD.** This is a bounded proof of concept, not acceptance of
PlasmaTerm as a complete texture system. Do not merge/release or overwrite
a live taskbar until exact-source Windows visual acceptance.

## Scope and provenance
- Wave source: https://github.com/techrote/PlasmaTerm/blob/main/plasma.py
  The four sine waves, phase, quantization and procedural 256-colour palette
  are implemented in native C# rather than by running Python/Windows Terminal.
- RetroBar's WPF taskbar gains a non-hit-testable Border overlay **behind**
  its real buttons/tray controls. Original resource brushes are untouched.
  Disable the feature to remove the overlay and show the original theme.
- Properties > Taskbar has an off-by-default checkbox, FPS slider (5–60,
  default 20) and resolution divisor slider (2–8, default 4), persisted in
  settings.json. Live adjustment does not re-layout the taskbar.
- The per-window renderer reuses one BGRA buffer and WriteableBitmap until
  the window dimensions/resolution change; 262,144-pixel/1024-side caps.
  Animation stops on hide/unload/close and does not accumulate missed frames.

## Known limitations
- Plasma fills the background only. Theme-filled task buttons/tray may
  obscure it. No integration with static Style/LUT texture configs yet.
- Per-window elapsed time can differ across monitors. No coordinated
  cross-monitor field, independent per-region backgrounds, alpha or blend
  modes, rotation, user palettes, or dynamic performance feedback in M0.
- CPU sampled at 20 FPS by default. No claim of low overhead on old GPUs
  or very tall taskbars until measured on physical Windows hardware.
- This does NOT include the guarded 32-row changes in Draft PR #38.

## Qualification and owner acceptance
Automated texture contracts compare BGRA pixels against independent
PlasmaTerm reference outputs at t=0 and t=1.25; check deterministic frames,
invalid arguments, bound and aspect-preserving allocation. .NET 6/10
Windows builds, texture suite and regression suites must pass exact-head CI.

Physical Windows owner checks still required:
1. Test the portable artifact in isolation with settings.json backup.
2. With plasma disabled, verify original themes/PNG textures, Start,
   task buttons, quick launch, tray, clock and hotkeys unchanged.
3. Enable background; verify smooth animation, click-through controls,
   FPS/resolution changes, immediate off switch and restart persistence.
4. Exercise horizontal/vertical edge, auto-hide, fullscreen, resize,
   DPI changes and multiple monitors; compare CPU/GPU and input latency
   off/on, including old GPU if available.
5. After PR #38 is separately merged, test 1, 16, 32 rows at 1280x1024
   and resize back down. No implied acceptance of PR #38 from this PR.

## Later milestones, outside this PR
M1: Style/LUT/texture editor, save/export/import and colour controls.
M2: per-region and per-state animated brush assignment and blending.
M3: time/phase synchronisation, shared frame generation and measured
performance improvements where profiling demonstrates a need.

**Owner gate:** Do not merge automatically. Keep both PRs separately
reviewable until their acceptance criteria are met.
