# UX-010 — Start-button colourways

Issue: [#18](https://github.com/techrote/RetroBar-Extreme/issues/18).
Dependency: UX-009 / PR #20. Implementation baseline: `da45c232b86e5e378bd834798e30ca2aeba52382`.

## Using it

In Properties → Colours, **Colours from Start button** reads the original theme button into the
12-slot working palette and the existing Base/Accent fields. Replacing a non-empty palette asks
for confirmation. This does not apply a scheme: **Apply** remains the existing, separately
confirmed generator action. Extracting again while recoloured still samples the original image.

Select **Start button** in the part selector. Enable **Recolour Start button**, enter/paste a
colour or use **Use colourway base/accent**. All three sprite states change live. Unchecking or
**Reset part** restores the original image without resetting other colours. **Reset all** clears
Start and the other overrides but retains the working palette, as in UX-009. Named styles,
import/export and restarting retain the Start setting. Changing a scheme does not implicitly
replace a separately chosen Start colour; the two explicit Use buttons connect them.

Classic brush-based themes do not have a recolourable background bitmap. Their control is
disabled with an explanation; a saved Start target is retained for switching back to XP/Vista.
Their existing brush-based styling remains unchanged. Missing/broken images fail open per key.

## Persistence

A backward-compatible optional **top-level** entry (not a brush inside `parts`):

```json
{
  "name": "Purple Start",
  "parts": {},
  "start": { "recolour": "#8c24ff" }
}
```

A Start-only object without `parts` is also accepted. Missing/null `recolour` means original
image; a gradient or invalid value is warned about and skipped without losing valid brushes.
Target alpha is ignored/normalised to opaque: each original pixel's alpha is preserved instead.
The writer omits `start` when unset. An otherwise empty Start-only style is not deleted while
its target is set. There is no theme-file mutation and no additional settings-store format.

## Rendering and extraction contract

`StartButtonColourways` works in straight BGRA32 (converts premultiplied sources first), remaps
HSL hue/saturation and preserves per-pixel HSL lightness and alpha. Black/white and unrelated
hues are retained. Non-orb sprites analyse each state's face independently: Royale Noir's
blue-black normal face and green hover/pressed faces must all follow the requested target;
extraction still returns only the normal state's palette. Soft hue/saturation shoulders reduce
hard chromatic-mask transitions. Neutral Longhorn faces receive a saturation tint rather than
a shift of undefined grey hue.

XP's Windows flag is a **separate resource and is never replaced**. Vista has its flag inside the
orb, including a blue pane sharing the background hue. A normalised authored silhouette protects
that flag in each frame; Longhorn protects chromatic flag pixels without leaving a grey rectangle
between them. These spatial masks target the shipped artwork; arbitrary custom embedded logos
are not a universal image-segmentation problem solved by this feature. Owner visual qualification
is still required for edge quality, flags, text and native/stretched display at real DPI.

Overrides cover `StartButtonImage` and all six Vista keys: `StartButtonImageLarge`,
`StartButtonImageLargeScaled`, `StartButtonImageBottom`, `StartButtonImageBottomScaled`,
`StartButtonImageTop`, `StartButtonImageTopScaled`. The orb variants share the complete orb's
reference hue/saturation, avoiding independent remaps at split/scaled transitions. Geometry,
DPI and sprite-state dimensions are unchanged. Existing DynamicResource references update
without replacing templates or affecting click handling.

Extraction samples the normal frame only, skips transparent/low-coverage pixels and the embedded
logo, finds the dominant face hue, then uses deterministic three-centre RGB clustering seeded by
lightness quantiles. Result order is dominant face, highlight, shadow with duplicate centres
removed. Grey faces are supported; absent usable samples leave the palette unchanged. Analysis
samples at most 32,768 pixels and rejects source images larger than 1 megapixel to bound UI work.

Original-resource lookup follows direct-before-merged, reverse-merged WPF precedence while
excluding the style overlay. Weak-key caches release old theme images; each source/kind keeps
only the most recent target/reference output. Mutable image changes invalidate their analysis.
There is no frame-time shader, growing colour-history cache, network call or shell behaviour change.

## Automated qualification

`tests/RetroBar.StartColourways.Tests` is an STA console runner using the **production WPF code**.
The separate `Start colourways contracts` workflow runs it on both supported frameworks. It does
not launch shell integration, show the taskbar, or write active user styles. Tests cover document
compatibility/round-trips, malformed values, original resource precedence, live override/reset,
all shipped Start families and all six Vista variants in all three states, exact alpha, bounded
HSL-lightness rounding, flag-pane samples, all-state Royale Noir face-region hue checks, immutable
inputs, cache reuse/invalidation, size limits, compiled editor XAML, classic fallback, dynamic
localisation and incremental hex typing.

Evidence artifacts contain machine-readable results tagged with the PR head plus original,
purple and orange sprite PNGs. Pixel contracts and sampled flag cores are not a substitute for
perceptual acceptance. CI's normal portable builds remain the owner-test deliverable.

## Owner acceptance card

Download the exact-head **RetroBar Portable 64-bit** artifact linked in issue #18 / the PR.
Exit the running RetroBar before launching the extracted portable build. Existing styles persist;
export the current style first to keep a convenient rollback copy.

Try XP Blue → Olive Green extraction and Apply; recolour XP Blue and Royale Noir; switch live
to Vista Aero and try normal/hover/pressed, top/bottom/vertical taskbars and native/stretch sizing.
Try Reset part, restart, and save/export/import. Good: useful matching green/olive colours,
original flags/text and smooth edges/shading, no split-orb seam, immediate consistent switching,
and exact restoration on reset. Reply **PASS** with the tested build/head and themes, or **FAIL**
with the theme/state/scale and a screenshot. Do not merge or close #18 before that response.

Evidence disposition: implementation/review and automated CI are recorded separately on the PR;
**owner-tested remains pending** until an explicit reply for the exact build. Master verification
is required after the eventual authorised squash merge.
