# RetroBar-Extreme example styles

Fourteen ready-made styles that show what the style system can do. To use one, open
Properties → **Colours** → **Import...** and pick a file. It applies straight away, over
whatever theme you're using. Tweak it from there, and **Save** it under your own name.

![Preview strip of every style](preview.png)

| Style | Shows off |
|---|---|
| **Ratrod** | Steel gradients with one warm accent (the original example) |
| **Neon Night** | Clouds texture bar, neon gradients, palette-driven, thicker tray borders, purple Start recolour |
| **Carbon Racer** | Carbon-fibre texture, racing red, double task button borders |
| **Amber CRT** | Fine scanlines, a 45° scanline active button, borderless buttons; try a monospace font and the pixel-arrow toggle |
| **Green Phosphor** | Noise glow, a tray texture that takes its colours from the palette |
| **Marble Halls** | Light marble with turned veins, gold accents, dark text on a light bar |
| **Ocean Depths** | Three-stop gradient bar, cellular caustics on the tray |
| **Sunset Strip** | A horizontal (0°) gradient across the bar, see-through buttons (`#aarrggbb`) |
| **Diamond Plate** | Diamond-plate texture, hazard-yellow accents, thick bar and tray borders |
| **Paper and Ink** | Light and flat; double-thick bevels everywhere (great on 95-98 / 2000) |
| **Ice Crystal** | Frosty cells, angled clouds on the tray, cool blues |
| **Lava Lamp** | Hot marble turned 30°, dark glass buttons |
| **Midnight Mono** | Minimal and flat: no borders, one white accent; pairs with icon-only task buttons |
| **Brushed Bronze** | Brushed texture turned to vertical strokes, triple-thick Start border |

Parts a style leaves out keep the theme's own look.
- **Classic themes** (95-98, 2000, …) draw the bar, tray and Start button in one colour, so the tray settings only show on XP, Vista, Longhorn and Watercolor.
- **Start recolouring** applies to the image-based Start buttons of XP, Vista and Longhorn.

The file format is plain JSON with comments; open any of these files to see it.
- **Brushes:** colour, gradient or texture.
- **Other settings:** `palette`, `borders` and `start`.

`Broken.json` is a test file: it shows that a bad style is skipped and never stops RetroBar.
