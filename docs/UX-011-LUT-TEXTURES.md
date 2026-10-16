# UX-011 — LUT textures

Owning issue: [#19](https://github.com/techrote/RetroBar-Extreme/issues/19).

## Status and acceptance

Implementation candidate on `ux-011-lut-textures`. CI results and the exact portable build
are recorded on the issue/PR. Compilation, automated WPF qualification and code review
are separate from **owner-tested**: the owner's PASS on the exact CI build is required
before merging or closing this visual issue. No automated image claims to be the owner's
CyberGarish theme or a capture of their desktop.

## Using the editor

In **Properties → Colours**, choose a part, then choose **Texture** for a brush property.
The same slots and theme-family limitations as ordinary colours/gradients apply. Start
bitmap recolouring remains the separate UX-010 part; it is not converted into a texture.

Choose `brushed`, `carbon`, `noise`, `scanlines`, `checker` or `diamond`, or a custom PNG.
All six built-ins are original, deterministic procedural patterns. Each is periodic on a
64 × 64 lattice; nothing is downloaded, animated or generated per frame.

The LUT maps black through grey to white using 2–6 evenly spaced colour stops. Each stop
has the existing swatch, hex, copy and paste controls. Remove stops down to two, or add
up to six. Colour alpha is supported. Contrast pivots the input grey level around 0.5;
zero produces the LUT's midpoint, one is unchanged, and four is the maximum. Texture
scale is independently adjustable from 0.25 to 8. Invalid numeric input is highlighted
but never committed; leaving the field restores its last valid value.

**Use palette** is a live reference to all occupied UX-009 slots in slot order, including
up to all twelve, rather than an implicit six-colour truncation. At least two occupied
slots are required. Editing the floating palette or extracting colours from Start updates
the texture; fewer than two colours temporarily falls back to the theme for that property.
The reference is retained so restoring a second palette colour recovers it automatically.

**Ratrod brushed** selects brushed steel with `#2b2f33`, `#5a6066`, `#8a9096`.
**Theme scanlines** selects scanlines with the selected part's original theme colours,
ignoring the overlay. This is how to obtain the real local CyberGarish palette without
inventing or bundling an uncommitted custom theme. A solid theme brush is seeded with its
colour and a lighter variant; a gradient contributes its ordered colours. A seed with
more than six colours is reduced to six positions including both endpoints. These two
shortcuts reset texture scale and contrast to one; they affect only the edited property.

Every edit uses the existing live style save path. Named Save/Load, Import/Export and
restart therefore retain textures. Theme default or Reset part removes that override;
Reset all retains the working palette but removes the texture settings. Applying a
UX-009 generated colourway still intentionally replaces its documented generated parts,
including any textures there, after the existing replacement confirmation. The bar,
Start recolour and palette are not overwritten by that generator.

## JSON contract

Existing solid and gradient representations are unchanged. A texture brush is:

```json
{
  "name": "Ratrod brushed",
  "parts": {
    "bar": {
      "background": {
        "texture": "brushed",
        "lut": ["#2b2f33", "#5a6066", "#8a9096"],
        "scale": 1,
        "contrast": 1
      }
    }
  }
}
```

Replace the LUT array with `"lut": "palette"` for a live reference. The palette remains
the existing top-level array, including null gaps. Explicit arrays require 2–6 colour
strings; palette mode resolves 2–12 nonempty slots. `scale` and `contrast` default to one
when omitted and must be finite and within the editor's ranges. Unknown texture members,
unknown non-PNG pattern names, mixed gradient/texture objects and malformed LUTs are
rejected for that property only, with a warning. Texture parsing performs no asset I/O.
An invalid/missing custom file or unusable palette does not discard the persisted setting.

Interpolation is piecewise linear in straight sRGB bytes, with alpha interpolated
independently. The result alpha is multiplied by the source PNG coverage. Fully transparent
output RGB is zeroed. This is a style colour LUT, not an ICC or cinematic 3D LUT.

## Custom PNGs and safety limits

Put a tile such as `my-steel.png` in `%LOCALAPPDATA%\RetroBar\Textures\` and select its
filename. The folder is created at style-layer initialization. Reopen Texture mode to
refresh the dropdown after adding filenames. Existing selected assets are watched by
the style layer and reloaded after changes settle; reopening/reapplying also reloads them.
A file watcher failure is logged and leaves manual reapplication available.

Only a plain `.png` filename is accepted, never a path, URI, alternate stream or traversal.
Linked files, linked texture folders and reparse-point ancestors are refused. The PNG must
have dimensions from 1–512 pixels on each side and a total file size no greater than 4 MiB.
These bounds and the signature/IHDR dimensions are checked **before** WPF decoding. The
bounded snapshot is decoded with the PNG decoder, with embedded colour profiles ignored.
Every nontransparent pixel must have equal red, green and blue components; native grey
and grey-indexed PNGs are supported. Transparent hidden RGB is ignored. Source alpha is
coverage, not an extra grey channel. PNG DPI metadata is discarded: one source pixel is
one DIP at texture scale one. For clean repeats, the supplied image must itself tile.

Style exports retain the PNG **filename reference**, not embedded image bytes. Copy the
referenced PNG into the receiving machine's Textures directory along with an imported
style. A missing file falls back only for its affected property. No custom theme, shell
hook, startup setting or Windows taskbar restoration behaviour is changed.

The renderer keeps six small immutable greyscale pattern arrays and at most sixteen frozen
coloured bitmaps. Custom bitmap keys include a SHA-256 of the bounded file snapshot, the
resolved LUT colours (including alpha) and contrast. Consequently same-size replacements
with unchanged timestamps still invalidate, deleted files cannot be concealed by a cache
hit, and palette edits cannot reuse an incorrectly coloured image. Scale and rotation
share the immutable source bitmap; the brush supplies those transformations. File reading
and recolouring happen on style rebuilds, not on paint callbacks.

## Scaling and orientation

An absolute WPF viewport tiles in device-independent units (DIPs); it never stretches one
tile to fill the taskbar. In **Native sizing**, increasing the taskbar's size exposes more
pattern, preserving its density. Windows DPI scaling maps those DIPs to device pixels.
Fractional texture/DPI scales are filtered by WPF, not claimed to be pixel-identical to an
integer scale. In legacy **Stretch**, the existing parent transform scales the completed
taskbar, including the texture, once. No new global transform is added.

Existing vertical part mappings rotate the texture by −90 degrees. Classic themes keep
their existing shared brush limitations: bar/tray/Start can share a background and not
all modern per-part controls have an effect. Unused controls remain labelled as before.

WPF basis: [TileBrush overview](https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/tilebrush-overview)
and [setting tile size](https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/how-to-set-the-tile-size-for-a-tilebrush).

## Automated qualification

`tests/RetroBar.Textures.Tests` references the production application assembly and runs on
a real Windows STA/WPF runtime for both `net6.0-windows` and `net10.0-windows`.
`.github/workflows/textures.yml` retains exact-head JSON results and rendered PNG evidence.
The existing UX-010 tests and full portable/installer build matrix also remain enabled.

The texture gate covers JSON and disk round trips, deep clones, part reset, legacy
gradient angles/offsets, generator isolation, explicit/palette LUT bounds, invalid settings,
actual RGBA pixels, zero contrast, deterministic distinct patterns, palette invalidation,
bounded caching, custom grey/indexed PNGs, transparency, corrupt/oversized/missing assets,
same-size/timestamp replacement, all part mappings and property-atomic fallback. Real WPF
raster checks cover horizontal/vertical tile repeats and opaque internal boundaries at
100/125/150/200% DPI, texture scales 0.25/1/1.5, native rectangle resizing and legacy Stretch.
Editor checks use the compiled controls, preserve all four modes during language-resource
reloads, exercise 2–6 LUT editing, reject invalid inputs, accept locale decimal separators,
and verify that palette previews and the parent editor read the current style document.

Tests never start RetroBar's shell integration, display the Properties window, or write the
user's active Style.json. Test files are isolated in a temporary directory. Vivid scanline
PNGs in CI evidence are explicitly synthetic fixtures, not CyberGarish owner acceptance.

## Owner test card

Use the exact portable ZIP and head linked on issue #19. Keep the currently working build
available, and save/export the current style before changing it. Quit the running RetroBar,
extract the new build separately and run its 64-bit executable.

In Colours, edit the bar background in Texture mode. Try **Ratrod brushed**, then select
CyberGarish as the theme and try **Theme scanlines**. Check 100%, 125%, 150% and 200% taskbar
scale in Native sizing; briefly check a vertical edge and legacy Stretch. Try one palette
edit, restart to confirm persistence, and restore Theme default/Reset part.

Good means clean repeating textures without blank tile boundaries, controllable scale and
contrast, immediate colour updates, retained settings after restart, and an exact return
to the original theme when reset. Reply `PASS — <tested head>` or `FAIL — <tested head>`
with the affected pattern/scale and a screenshot. Owner PASS is the remaining visual gate,
not something the CI raster tests can supply.
