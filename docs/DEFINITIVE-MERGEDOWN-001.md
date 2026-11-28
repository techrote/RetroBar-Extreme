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
