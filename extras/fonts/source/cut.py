"""RetroBar cut of the 24 chosen font-payload-vault faces.

The vault fonts are polygon-only outline proofs (no curves, no hinting). This makes them fit a
Windows taskbar drawn by WPF:
- glyph fixes for shapes that fail at 11-13 px ("a" reading as "o", "·" drawn as "*", a broken
  "ß", Microslab "l" identical to "L", Rivet "…" merged into one bar);
- Windows naming (RIBBI), weight / width classes, monospace flags, version and licence fields;
- autohinting (ttfautohint), so stems land on whole pixels when the taskbar draws aliased text;
- extra families: Tallow Condensed gets Rivet as its true Bold.

usage: python -I cut.py <orig dir> <knurled per-size dir> <out dir>
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pylib"))

import copy
import io
from pathlib import Path

from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from shapely import affinity
from shapely.geometry import LineString, MultiPolygon, Point, Polygon, box
from shapely.geometry.polygon import orient
from shapely.ops import polygonize, unary_union
import ttfautohint

VERSION = "1.100"
VENDOR = "TCHR"
LICENSE = ("This Font Software is licensed under the SIL Open Font License, Version 1.1. "
           "This license is available with a FAQ at: https://openfontlicense.org")
LICENSE_URL = "https://openfontlicense.org"

# file -> (family, style, weight, width class, mono)
FACES = {
    "RivetCondensed-Regular.ttf": ("Rivet Condensed", "Bold", 700, 3, False),
    "TallowCondensed-Regular.ttf": ("Tallow Condensed", "Regular", 500, 3, False),
    "FerruleRegular-Regular.ttf": ("Ferrule", "Regular", 400, 5, False),
    "BushingSans-Regular.ttf": ("Bushing Sans", "Regular", 400, 5, False),
    "ColletSans-Regular.ttf": ("Collet Sans", "Regular", 400, 5, False),
    "LatchlineMono-Regular.ttf": ("Latchline Mono", "Regular", 400, 5, True),
    "RailkeyMono-Regular.ttf": ("Railkey Mono", "Regular", 400, 5, True),
    "PinfeedGrid-Regular.ttf": ("Pinfeed Grid", "Regular", 400, 5, True),
    "Flypress-Regular.ttf": ("Flypress", "Regular", 700, 3, False),
    "Microslab-Regular.ttf": ("Microslab", "Regular", 400, 5, True),
    "PebbleBold-Regular.ttf": ("Pebble", "Bold", 700, 5, False),
    "NullCartridge-Regular.ttf": ("Null Cartridge", "Regular", 400, 5, True),
    "PhaseDrift-Regular.ttf": ("Phase Drift", "Regular", 400, 5, True),
    "QuantumStep-Regular.ttf": ("Quantum Step", "Regular", 400, 5, False),
    "PixelCell-Regular.ttf": ("Pixel Cell", "Regular", 400, 5, False),
    "CacheRattle-Regular.ttf": ("Cache Rattle", "Regular", 400, 5, True),
    "TorquePlate-Regular.ttf": ("Torque Plate", "Regular", 800, 5, False),
    "DiecastRelay-Regular.ttf": ("Diecast Relay", "Regular", 400, 5, False),
    "RasterForge-Regular.ttf": ("Raster Forge", "Regular", 400, 7, True),
    "CamberScript-Oblique.ttf": ("Camber Script", "Italic", 400, 5, False),
    "PlinthMono-Regular.ttf": ("Plinth Mono", "Regular", 400, 5, True),
    "QuoinSerif-Regular.ttf": ("Quoin Serif", "Regular", 400, 5, False),
    "SlingArc-Oblique.ttf": ("Sling Arc", "Italic", 700, 5, False),
}
# Fixes chosen from 11-13 px WPF renders of the originals ("Task Manager" read as "Tosk Monoger").
FIX_A = {"Rivet Condensed", "Tallow Condensed", "Torque Plate", "Microslab", "Ferrule"}
FIX_L = {"Microslab"}
FIX_SS = {"Bushing Sans", "Microslab", "Torque Plate", "Sling Arc", "Raster Forge"}
NO_FIX = {"Null Cartridge"}  # inverse-video cells: every glyph is a filled cell with holes
# Faces whose letters already sit on a coarse pixel/dot grid: hinting would bend the dots.
NO_HINT = {"Phase Drift", "Pinfeed Grid", "Null Cartridge", "Cache Rattle", "Raster Forge"}


# ----------------------------------------------------------------- outline <-> polygons

def contours(font, glyph_name):
    pen = RecordingPen()
    font.getGlyphSet()[glyph_name].draw(pen)
    out, cur = [], []
    for op, args in pen.value:
        if op == "moveTo":
            cur = [args[0]]
        elif op == "lineTo":
            cur.append(args[0])
        elif op in ("qCurveTo", "curveTo"):
            cur.extend(args)  # none expected in these fonts; keep the points
        elif op in ("closePath", "endPath"):
            if len(cur) >= 3:
                out.append(cur)
            cur = []
    return out


def to_shape(font, glyph_name):
    """Exact nonzero-winding area of a glyph as a shapely geometry."""
    rings = contours(font, glyph_name)
    if not rings:
        return Polygon()
    lines = unary_union([LineString(r + [r[0]]) for r in rings])
    kept = []
    for face in polygonize(lines):
        p = face.representative_point()
        winding = 0
        for r in rings:
            winding += _winding(p.x, p.y, r)
        if winding != 0:
            kept.append(face)
    return unary_union(kept).buffer(0)


def _winding(x, y, ring):
    w = 0
    n = len(ring)
    for i in range(n):
        x0, y0 = ring[i]
        x1, y1 = ring[(i + 1) % n]
        if y0 <= y < y1 and (x1 - x0) * (y - y0) - (x - x0) * (y1 - y0) > 0:
            w += 1
        elif y1 <= y < y0 and (x1 - x0) * (y - y0) - (x - x0) * (y1 - y0) < 0:
            w -= 1
    return w


def polys(geom):
    if geom.is_empty:
        return []
    if isinstance(geom, Polygon):
        return [geom]
    if isinstance(geom, MultiPolygon):
        return list(geom.geoms)
    return [g for g in getattr(geom, "geoms", []) if isinstance(g, Polygon)]


def set_shape(font, glyph_name, geom, advance=None):
    pen = TTGlyphPen(None)
    for p in polys(geom.buffer(0)):
        p = orient(p, sign=-1.0)  # TrueType: outer rings clockwise
        for ring in [p.exterior] + list(p.interiors):
            pts = [(round(x), round(y)) for x, y in list(ring.coords)[:-1]]
            pts = [pt for i, pt in enumerate(pts) if pt != pts[i - 1]]
            if len(pts) < 3:
                continue
            pen.moveTo(pts[0])
            for pt in pts[1:]:
                pen.lineTo(pt)
            pen.closePath()
    glyph = pen.glyph()
    font["glyf"][glyph_name] = glyph
    glyph.recalcBounds(font["glyf"])
    adv = advance if advance is not None else font["hmtx"][glyph_name][0]
    font["hmtx"][glyph_name] = (adv, getattr(glyph, "xMin", 0))


def gname(font, ch):
    return font.getBestCmap().get(ord(ch))


def stem_width(font):
    """Width of the "l" stem halfway up the x-height (the face's main stem)."""
    shape = to_shape(font, gname(font, "l"))
    xh = font["OS/2"].sxHeight or 500
    band = shape.intersection(box(-2000, xh * 0.45, 4000, xh * 0.55))
    widths = sorted(p.bounds[2] - p.bounds[0] for p in polys(band))
    return widths[0] if widths else 100


# ----------------------------------------------------------------- glyph fixes

def similarity(a, b):
    """IoU of two shapes after moving both to their own bounding-box origin."""
    if a.is_empty or b.is_empty:
        return 0
    a = affinity.translate(a, -a.bounds[0], -a.bounds[1])
    b = affinity.translate(b, -b.bounds[0], -b.bounds[1])
    return a.intersection(b).area / a.union(b).area


def fix_a(font, log, mono):
    """Single-storey "a" that reads as "o": add a foot spur to the stem."""
    ga, go = gname(font, "a"), gname(font, "o")
    a, o = to_shape(font, ga), to_shape(font, go)
    sim = similarity(a, o)

    upm = font["head"].unitsPerEm
    stem = stem_width(font)
    spur = max(1.1 * stem, 0.13 * upm)
    x0, y0, x1, y1 = a.bounds
    angle = font["post"].italicAngle
    foot = box(x1 - stem, y0, x1 + spur, y0 + max(stem, 0.10 * upm))
    if angle:
        foot = affinity.skew(foot, xs=-angle, origin=(0, 0))
    new = unary_union([a, foot])
    adv = font["hmtx"][ga][0]
    if mono:
        new = affinity.translate(new, -spur / 2, 0)
    else:
        adv = round(adv + spur)
    set_shape(font, ga, new, adv)
    log.append(f'"a" looked like "o" (overlap {sim:.2f}): foot spur added')


def fix_l(font, log):
    """Microslab-style "l" with a one-sided foot, identical to "L": make the foot two-sided."""
    gl, gL = gname(font, "l"), gname(font, "L")
    l, L = to_shape(font, gl), to_shape(font, gL)
    stem = stem_width(font)
    x0, y0, x1, y1 = l.bounds
    # the stem's centre, from the slice halfway up
    mid = l.intersection(box(-2000, (y0 + y1) / 2 - 5, 4000, (y0 + y1) / 2 + 5))
    cx = (mid.bounds[0] + mid.bounds[2]) / 2
    reach = max(x1 - cx, cx - x0)
    foot = box(cx - reach, y0, cx + reach, y0 + stem)
    # top: keep the left serif only (as drawn), and strip any right foot overhang into a symmetric slab
    new = unary_union([l.intersection(box(-2000, y0 + stem, 4000, y1 + 10)), foot])
    shift = max(0, -(cx - reach))
    set_shape(font, gl, affinity.translate(new, shift, 0), font["hmtx"][gl][0])
    log.append('"l" looked like "L": foot made two-sided')


def fix_periodcentered(font, log):
    gp, gc = gname(font, "."), gname(font, "·")
    p, c = to_shape(font, gp), to_shape(font, gc)
    if p.is_empty or c.is_empty:
        return
    pw, ph = p.bounds[2] - p.bounds[0], p.bounds[3] - p.bounds[1]
    cw, ch = c.bounds[2] - c.bounds[0], c.bounds[3] - c.bounds[1]
    if cw < 1.6 * pw and ch < 1.6 * ph:
        return
    xh = font["OS/2"].sxHeight or 500
    adv = font["hmtx"][gc][0]
    dot = affinity.translate(p, (adv - pw) / 2 - p.bounds[0], xh / 2 - (p.bounds[1] + p.bounds[3]) / 2)
    angle = font["post"].italicAngle
    if angle:
        dot = affinity.translate(dot, -xh / 2 * __import__("math").tan(__import__("math").radians(angle)), 0)
    set_shape(font, gc, dot, adv)
    log.append('"·" was drawn as an asterisk: rebuilt from "."')


def fix_ellipsis(font, log, mono):
    gp, ge = gname(font, "."), gname(font, "…")
    p, e = to_shape(font, gp), to_shape(font, ge)
    if len(polys(e)) >= 3 or p.is_empty:
        return
    pw = p.bounds[2] - p.bounds[0]
    adv = font["hmtx"][ge][0]
    gap = max(pw * 0.8, (adv - 3 * pw) / 3) if mono else pw * 1.1
    dots = [affinity.translate(p, -p.bounds[0] + gap / 2 + i * (pw + gap), 0) for i in range(3)]
    width = 3 * pw + 3 * gap
    shape = unary_union(dots)
    if mono:
        scale = min(1.0, adv / width)
        shape = affinity.scale(shape, scale, 1, origin=(0, 0))
    else:
        adv = round(width)
    set_shape(font, ge, shape, adv)
    log.append('"…" had merged dots: rebuilt from three "."')


def fix_germandbls(font, log):
    """A broken "ß" (B-like with a stray inner stroke): redraw it as a stroked skeleton."""
    g = gname(font, "ß")
    if not g:
        return
    s = to_shape(font, g)
    B = to_shape(font, gname(font, "B"))
    stem = stem_width(font)
    xh = font["OS/2"].sxHeight or 500
    asc = max(font["OS/2"].sCapHeight or 700, to_shape(font, gname(font, "l")).bounds[3])
    adv = font["hmtx"][g][0]
    lsb = to_shape(font, gname(font, "n")).bounds[0]
    x0 = lsb + stem / 2
    w = adv - 2 * lsb - stem
    top = asc - stem / 2
    r = w * 0.5
    # left stem up, a cap over to the right, the upper bowl in to the middle, the lower bowl
    # out and back to the baseline.
    mid = xh * 0.85
    pts = [
        (x0, stem / 2 - stem / 2), (x0, top - r * 0.6), (x0 + r * 0.3, top), (x0 + w * 0.65, top),
        (x0 + w * 0.92, top - r * 0.35), (x0 + w * 0.92, mid + (top - mid) * 0.35), (x0 + w * 0.62, mid),
        (x0 + w * 0.45, mid), (x0 + w * 0.62, mid), (x0 + w, mid - xh * 0.25), (x0 + w, xh * 0.25),
        (x0 + w * 0.75, stem / 2), (x0 + w * 0.35, stem / 2),
    ]
    angle = font["post"].italicAngle
    path = LineString(pts).buffer(stem / 2, cap_style="flat", join_style="mitre", mitre_limit=2.0)
    path = path.intersection(box(-2000, 0, 4000, asc))
    if angle:
        path = affinity.skew(path, xs=-angle, origin=(0, 0))
    set_shape(font, g, path, adv)
    log.append('"ß" was a broken B shape: redrawn')


# ----------------------------------------------------------------- true bold

# Regular-only faces get a real Bold, so the taskbar's bold text (the "start" label, some themes'
# active buttons) isn't WPF's simulated smear. Null Cartridge's "Bold" is its Regular: its letters
# are holes in filled cells, which emboldening would only shrink.
BOLD_SAME = {"Null Cartridge"}


def embolden(font, amount, mono):
    glyf, hmtx = font["glyf"], font["hmtx"]
    for name in font.getGlyphOrder():
        g = glyf[name]
        if g.isComposite():
            continue
        adv = hmtx[name][0]
        if g.numberOfContours <= 0:
            if not mono and adv:
                hmtx[name] = (round(adv + 2 * amount), 0)
            continue
        shape = to_shape(font, name)
        if shape.is_empty:
            continue
        bold = shape.buffer(amount, join_style="mitre", mitre_limit=2.0)
        if mono:
            set_shape(font, name, bold, adv)
        else:
            set_shape(font, name, affinity.translate(bold, amount, 0), round(adv + 2 * amount))
    # composites: their components moved with the base glyphs; widen proportional advances to match
    if not mono:
        for name in font.getGlyphOrder():
            if glyf[name].isComposite():
                adv, lsb = hmtx[name]
                hmtx[name] = (round(adv + 2 * amount), lsb)


# ----------------------------------------------------------------- naming and tables

def set_names(font, family, style, mono, weight, width, note):
    name = font["name"]
    keep = {0: name.getDebugName(0) or "Copyright (c) 2026 techrote."}
    name.names = []
    ps_family = family.replace(" ", "")
    ribbi = style if style in ("Regular", "Bold", "Italic", "Bold Italic") else "Regular"
    legacy_family = family
    typo_style = None
    if weight == 500 and style == "Regular":
        typo_style = "Medium"
    if weight == 800:
        typo_style = "ExtraBold"
    full = family if ribbi == "Regular" and not typo_style else f"{family} {typo_style or ribbi}"
    records = {
        0: keep[0],
        1: legacy_family,
        2: ribbi,
        3: f"{VERSION};{VENDOR};{ps_family}-{(typo_style or ribbi).replace(' ', '')}",
        4: full,
        5: f"Version {VERSION}; RetroBar cut",
        6: f"{ps_family}-{(typo_style or ribbi).replace(' ', '')}",
        8: "techrote",
        9: "techrote",
        10: note,
        11: "https://github.com/techrote/font-payload-vault",
        13: LICENSE,
        14: LICENSE_URL,
    }
    if typo_style:
        records[16] = family
        records[17] = typo_style
    for nid, text in records.items():
        name.setName(text, nid, 3, 1, 0x409)
        name.setName(text, nid, 1, 0, 0)


def set_tables(font, style, weight, width, mono):
    os2, head, post, hhea = font["OS/2"], font["head"], font["post"], font["hhea"]
    os2.usWeightClass = weight
    os2.usWidthClass = width
    os2.achVendID = VENDOR
    os2.fsType = 0
    italic = "Italic" in style
    bold = style.startswith("Bold")
    sel = 0
    if italic:
        sel |= 1 << 0
    if bold:
        sel |= 1 << 5
    if not italic and not bold:
        sel |= 1 << 6
    sel |= 1 << 7  # USE_TYPO_METRICS
    if italic and font["post"].italicAngle:
        sel |= 1 << 9  # OBLIQUE: slanted roman, not a cursive italic (Camber is a script: below)
    os2.fsSelection = sel
    head.macStyle = (1 if bold else 0) | (2 if italic else 0)
    head.fontRevision = float(VERSION)
    post.isFixedPitch = 1 if mono else 0
    if mono:
        os2.panose.bFamilyType = 2
        os2.panose.bProportion = 9
    widths = [a for a, _ in font["hmtx"].metrics.values() if a > 0]
    os2.xAvgCharWidth = round(sum(widths) / len(widths))
    # line metrics: keep the vault's 1.25 em line, and make sure nothing is clipped by GDI
    ymin = min(getattr(g, "yMin", 0) for g in (font["glyf"][n] for n in font.getGlyphOrder()))
    ymax = max(getattr(g, "yMax", 0) for g in (font["glyf"][n] for n in font.getGlyphOrder()))
    os2.usWinAscent = max(os2.usWinAscent, ymax)
    os2.usWinDescent = max(os2.usWinDescent, -ymin)
    hhea.ascent, hhea.descent, hhea.lineGap = os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap
    if os2.version < 4:
        os2.version = 4


def autohint(font, family):
    if family in NO_HINT:
        return font, False
    buf = io.BytesIO()
    font.save(buf)
    hinted = ttfautohint.ttfautohint(in_buffer=buf.getvalue(), hinting_range_min=8, hinting_range_max=50,
                                     increase_x_height=14, windows_compatibility=True,
                                     default_script="latn",
                                     no_info=True, dehint=False)
    return TTFont(io.BytesIO(hinted)), True


def finish(font, out_dir, family, style, mono, weight, width, note, log, hint=True):
    set_names(font, family, style, mono, weight, width, note)
    set_tables(font, style, weight, width, mono)
    hinted = False
    if hint:
        font, hinted = autohint(font, family)
    if family in NO_HINT:
        # dot-matrix faces: smooth, no grid-fitting (dots stay round instead of bending)
        from fontTools.ttLib import newTable
        gasp = newTable("gasp")
        gasp.version = 1
        gasp.gaspRange = {0xFFFF: 0x000A}
        font["gasp"] = gasp
    file = f"{family.replace(' ', '')}-{style.replace(' ', '')}.ttf"
    font.save(Path(out_dir) / file)
    return file, hinted


def main(orig, knurled, out):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    report = []
    for file, (family, style, weight, width, mono) in FACES.items():
        font = TTFont(Path(orig) / file)
        log = []
        if family in FIX_A:
            fix_a(font, log, mono)
        if family in FIX_L:
            fix_l(font, log)
        if family in FIX_SS:
            fix_germandbls(font, log)
        if family not in NO_FIX:
            fix_periodcentered(font, log)
            fix_ellipsis(font, log, mono)
        note = "RetroBar cut of a font-payload-vault face." + (" " + "; ".join(log) + "." if log else "")
        name, hinted = finish(font, out, family, style, mono, weight, width, note, log)
        report.append((name, hinted, log))
        if weight < 600 and family not in ("Tallow Condensed",):
            bold = TTFont(Path(orig) / file)
            for fix, on in ((fix_a, family in FIX_A), (fix_l, family in FIX_L), (fix_germandbls, family in FIX_SS)):
                if on:
                    fix(bold, [], mono) if fix is fix_a else fix(bold, [])
            if family not in NO_FIX:
                fix_periodcentered(bold, [])
                fix_ellipsis(bold, [], mono)
            if family not in BOLD_SAME:
                embolden(bold, max(0.25 * stem_width(bold), 0.045 * bold["head"].unitsPerEm), mono)
            bstyle = "Bold Italic" if "Italic" in style else "Bold"
            bname, bh = finish(bold, out, family, bstyle, mono, 700, width,
                               "RetroBar cut: Bold made from the Regular outlines.", [])
            report.append((bname, bh, ["Bold generated"]))
        if family == "Rivet Condensed":
            # Tallow Condensed's true Bold: the same Rivet outlines under Tallow's family name.
            bold = TTFont(out / name)
            bname, _ = finish(bold, out, "Tallow Condensed", "Bold", False, 700, 3,
                              "RetroBar cut: Rivet Condensed as the Bold of Tallow Condensed.", [], hint=False)
            report.append((bname, True, ["Rivet Condensed outlines as Tallow Condensed Bold"]))

    # Knurled Swarf: one pixel-exact font per size; RetroBar picks the size that matches the text.
    for f in sorted(Path(knurled).glob("KnurledSwarf*px-*.ttf")):
        font = TTFont(f)
        n = f.stem.split("px")[0].replace("KnurledSwarf", "")
        style = f.stem.split("-")[1]
        family = f"Knurled Swarf {n}px"
        set_names(font, family, style, False, 700 if style == "Bold" else 400, 5,
                  f"RetroBar cut: Knurled Swarf drawn for exactly {n} px (and {n}x2, {n}x3...).")
        font["OS/2"].achVendID = VENDOR
        font["head"].fontRevision = float(VERSION)
        font.save(out / f"KnurledSwarf{n}px-{style}.ttf")
        report.append((f.name, False, ["pixel-exact per-size face, (c) (R) TM added"]))

    for name, hinted, log in report:
        print(f"{name:34} {'hinted ' if hinted else 'unhinted'} {'; '.join(log)}")


if __name__ == "__main__":
    main(*sys.argv[1:4])
