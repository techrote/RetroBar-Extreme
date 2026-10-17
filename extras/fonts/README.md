# RetroBar-Extreme fonts

Twenty-four original typefaces from [font-payload-vault](https://github.com/techrote/font-payload-vault)
(commit `65619a9`), reworked for the taskbar. They're built into RetroBar, so you don't need to install
them. They're listed first in Properties → **Style** → **Font**, and in **Layout** → Clock → **Font**.

They're here too, so you can install them in Windows for other apps: right-click a `.ttf` →
**Install**. Licence: SIL Open Font License 1.1 (`OFL.txt`); no Reserved Font Names.

## Small-size clarity and space efficiency

| Font | Best for | Notes |
|---|---|---|
| **Knurled Swarf** | Everything: a true pixel font | One hand-tuned face per size, 6–16 px. RetroBar picks the face that matches your text, so it stays pixel-sharp at any scale. |
| **Rivet Condensed** | Narrow task buttons, titles | Bold and very compact |
| **Tallow Condensed** | Narrow task buttons | Medium weight; its Bold is Rivet Condensed, so the two work as a pair |
| **Ferrule** | General UI text | Round, open shapes |
| **Bushing Sans** | General UI text | Neutral and tidy at 11 px |
| **Collet Sans** | Clock, counters | Compact, tabular digits |
| **Latchline Mono** | Clock, tray | Slashed zero; distinct I / l / 1 |
| **Railkey Mono** | Clock, tray | |
| **Pinfeed Grid** | Retro labels | Dot matrix, crisp at 11 px |
| **Flypress** | Narrow buttons | Heavy condensed |
| **Microslab** | Labels | Monospaced slab |
| **Pebble** | Active-window titles | Bold, soft ends |

## Style-led, still legible

| Font | Best for | Notes |
|---|---|---|
| **Null Cartridge** | Bold looks, pressed buttons | Inverse-video character cells |
| **Phase Drift** | Clock, signage | Dot-matrix capitals (caps only) |
| **Quantum Step** | Bitmap look | Stair-stepped round letters |
| **Pixel Cell** | Bitmap look | Pixelated slab |
| **Cache Rattle** | Glitch looks | Pixel monospace with a tear |
| **Torque Plate** | Heavy display | Pixel-flavoured |
| **Diecast Relay** | 90s control panel | Small caps; best at 12 px and up |
| **Raster Forge** | Wide screen type | Best at 16 px and up |
| **Camber Script** | The Start button | Looped cursive |
| **Plinth Mono** | Typewriter look | Slab-serif monospace |
| **Quoin Serif** | Document look | Classic text serif |
| **Sling Arc** | Speed looks | Heavy oblique |

## What the rework changed

The vault faces are outline proofs: polygon outlines with no hinting and no Windows testing.
`source/cut.py` turns them into the RetroBar cut (version 1.100).

- **Legibility at 11–13 px**, from renders of each face as the taskbar draws it:
  - **"a" read as "o"** ("Task Manager" read "Tosk Monoger") in Rivet, Tallow, Torque Plate, Microslab
    and Ferrule. Fixed with a foot on the stem.
  - **Microslab's "l"** matched its "L". The foot now runs both ways.
  - **Broken "ß"** (a "B" with a stray stroke) in Bushing, Microslab, Torque Plate, Sling Arc and
    Raster Forge. Redrawn.
  - **"·" drawn as an asterisk** in five faces. Rebuilt from the full stop.
  - **Rivet's "…"** had merged into one bar. It's three dots again.
- **True Bold faces:** every Regular-only face has a real Bold, made from its own outlines. The taskbar
  draws some text bold, such as the "start" label, and Windows would otherwise smear a fake bold over
  these designs.
  - Null Cartridge's letters are holes in filled cells, so its Bold is the same as its Regular.
- **Hinting:** each face is autohinted (ttfautohint), so stems land on whole pixels. The dot-matrix
  faces are left unhinted so their dots keep their shape: Pinfeed Grid, Phase Drift, Null Cartridge,
  Cache Rattle and Raster Forge.
- **Knurled Swarf:**
  - ©, ® and ™ are added at every size (`source/knurled-swarf-marks.patch`).
  - RetroBar uses the per-size faces, because Windows' text renderer ignores the bitmaps embedded in
    the universal TTF.
- **Windows naming and metrics:**
  - style-linked family names, so Bold and Regular group correctly;
  - weight and width classes;
  - monospace flags;
  - clipping-safe line metrics;
  - licence fields.

### Rebuilding

```
pip install fonttools shapely ttfautohint-py
python -I source/cut.py <vault faces> <knurled per-size faces> <out>
```

Before running it, apply `knurled-swarf-marks.patch` in the vault and run Knurled Swarf's own
`build.py` and `verify.py`. The verifier confirms every glyph is still pixel-exact.
