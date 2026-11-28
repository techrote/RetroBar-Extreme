# DEFINITIVE-MERGEDOWN-001 — M001 evaluator checkpoint

Tracked by #48. Base master b13e7895809b41f2230f3758eec80caf98981d0c.
Extracted only the #46 delta relative to #43/#44 (377610e0 vs 9b7d7a5c);
no display, bundle, 32-row, packing or Plasma changes admitted.

- Preserve production paths and normal startup without the marker/explicit flag.
- Refuse a competing shell owner, unsafe/inaccessible profile roots and nested reparse points.
- Keep Quick Launch private, including setup and shortcut writes.
- Record exact source/tested-tree provenance and hash the evaluator executable.
- Shell-free Windows tests cover private profile initialization and a real nested junction.
- Review remains incomplete: explicit export dialogs, hardlinks, concurrent filesystem manipulation,
  other native shell actions, physical Windows startup/recovery and full integration are outside this checkpoint's proof.
- M001 remains Draft/HOLD until new CI and independent safety review; owner physical acceptance is required before production integration.

## M002 cumulative layout checkpoint

- #38 (655b30e55f8e2745111b976f2af650c84fe46929) admitted via a real second-parent integration commit 8694409d393172aec15f5edbe2ac506f04992bc8. Retains rule-constrained row navigation, finite viewport, hysteresis prevention, fixed widths, compatibility and 32-row contracts. The earlier #38 provenance workflow is superseded by M001's stricter provenance/package checks.
- #47 (5ccac3cdc0818d7b2cb9a04ac88838965dac01de) admitted via a separate second-parent integration commit. Retains actual wrapping panel tests, opt-in side packing, stable visual parenting and horizontal layout behaviour.
- M001 settings, private Quick Launch and evaluator tests retained; shared Settings, LayoutStrings and Extras registration reconciled without dropping source contracts.
- Integrated Windows qualification and owner acceptance remain HOLD; this candidate is still single global taskbar configuration, not per-display profiles. #44 and #39 are not included.

## M003 taskbar-management convergence — source candidate

- Admits #44's combined head (9b7d7a5ceab5fa86d524137853bedf6d40f8a3ee) with complete ancestry and retains M002 (#38/#47) ancestry. Prior stage descriptions referencing a five-row limit are historical, not the cumulative candidate's envelope.
- Preserve #38's navigation template, fixed task widths, and arrow-hysteresis algorithm; both ordinary and bundle panels bind finite and outer viewports and host row count. Horizontal access is admitted for rule-constrained rows OR expanded Inline, only on horizontal hosts.
- Independent display profiles clamp to 1–32 rows. Actual host orientation drives wrapping and icon columns; Quick Launch hotkeys follow the designated shell host.
- Preserve M001 evaluator safeguards, #47 stable Start/Quick panel, task affinity, bundle rendering and per-window rectangle ownership. Added mixed-edge converter, profile capacity and projection transition contracts; run full Windows CI before physical owner testing.
- Draft/HOLD. No production merge, release, or shell launch. #39 and #45 remain excluded.
