"""Draws the example Start button pictures in ../ (and the preview / reference sheets).

Every button is original artwork drawn here in code, three states stacked top to bottom
(normal, hover, pressed) at 96 x 28 px per state, so each PNG is 96 x 84.  Run:

    pip install pillow
    python make_buttons.py

Each style is a small dict, so adding your own is a matter of copying one and changing colours.
"""
import math
import os
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps

W, H, SS = 96, 28, 4            # one state: width, height, supersampling factor
OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# --- body shapes (masks at supersampled size) ---------------------------------------------------
def mask(shape, w, h, inset=0):
    m = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(m)
    i = inset * SS
    box = [i, i, w - 1 - i, h - 1 - i]
    r = (h - 2 * i) // 2
    if shape == 'pill':
        d.rounded_rectangle(box, radius=r, fill=255)
    elif shape == 'round':
        d.rounded_rectangle(box, radius=5 * SS, fill=255)
    elif shape == 'rect':
        d.rectangle(box, fill=255)
    elif shape == 'tab':      # square left, rounded right (a classic Start tab)
        d.rounded_rectangle(box, radius=r, fill=255)
        d.rectangle([box[0], box[1], box[0] + r, box[3]], fill=255)
    elif shape == 'chamfer':
        c = 6 * SS
        d.polygon([(box[0] + c, box[1]), (box[2] - c, box[1]), (box[2], box[1] + c), (box[2], box[3] - c),
                   (box[2] - c, box[3]), (box[0] + c, box[3]), (box[0], box[3] - c), (box[0], box[1] + c)], fill=255)
    elif shape == 'slant':
        s = 8 * SS
        d.polygon([(box[0] + s, box[1]), (box[2], box[1]), (box[2] - s, box[3]), (box[0], box[3])], fill=255)
    elif shape == 'notch':
        c = 7 * SS
        d.polygon([(box[0], box[1]), (box[2] - c, box[1]), (box[2], box[1] + c), (box[2], box[3]),
                   (box[0] + c, box[3]), (box[0], box[3] - c)], fill=255)
    return m


def vgrad(w, h, stops):
    """Vertical gradient through (position 0..1, colour) stops."""
    g = Image.new('RGB', (1, h))
    px = g.load()
    for y in range(h):
        t = y / max(1, h - 1)
        for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
            if p0 <= t <= p1:
                k = 0 if p1 == p0 else (t - p0) / (p1 - p0)
                px[0, y] = tuple(int(c0[j] + (c1[j] - c0[j]) * k) for j in range(3))
                break
    return g.resize((w, h))


def hgrad(w, h, stops):
    return vgrad(h, w, stops).transpose(Image.ROTATE_90).transpose(Image.FLIP_TOP_BOTTOM).resize((w, h)) \
        if False else _hgrad(w, h, stops)


def _hgrad(w, h, stops):
    g = Image.new('RGB', (w, 1))
    px = g.load()
    for x in range(w):
        t = x / max(1, w - 1)
        for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
            if p0 <= t <= p1:
                k = 0 if p1 == p0 else (t - p0) / (p1 - p0)
                px[x, 0] = tuple(int(c0[j] + (c1[j] - c0[j]) * k) for j in range(3))
                break
    return g.resize((w, h))


# --- glyphs -------------------------------------------------------------------------------------
def glyph_mask(kind, w, h, cx, size):
    m = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(m)
    cy = h / 2
    r = size / 2
    if kind == 'star':
        pts = []
        for i in range(10):
            a = -math.pi / 2 + i * math.pi / 5
            rr = r if i % 2 == 0 else r * 0.45
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        d.polygon(pts, fill=255)
    elif kind == 'diamond':
        d.polygon([(cx, cy - r), (cx + r * 0.8, cy), (cx, cy + r), (cx - r * 0.8, cy)], fill=255)
    elif kind == 'bolt':
        d.polygon([(cx + r * 0.2, cy - r), (cx - r * 0.7, cy + r * 0.15), (cx - r * 0.05, cy + r * 0.15),
                   (cx - r * 0.25, cy + r), (cx + r * 0.75, cy - r * 0.2), (cx + r * 0.05, cy - r * 0.2)], fill=255)
    elif kind == 'chevron':
        t = r * 0.5
        d.polygon([(cx - r * 0.6, cy - r), (cx - r * 0.6 + t, cy - r), (cx + r * 0.7, cy), (cx - r * 0.6 + t, cy + r),
                   (cx - r * 0.6, cy + r), (cx + r * 0.7 - t, cy)], fill=255)
    elif kind == 'play':
        d.polygon([(cx - r * 0.6, cy - r), (cx + r * 0.9, cy), (cx - r * 0.6, cy + r)], fill=255)
    elif kind == 'circle':
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    elif kind == 'ring':
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
        d.ellipse([cx - r * 0.55, cy - r * 0.55, cx + r * 0.55, cy + r * 0.55], fill=0)
    elif kind == 'hex':
        pts = [(cx + r * math.cos(math.pi / 6 + i * math.pi / 3), cy + r * math.sin(math.pi / 6 + i * math.pi / 3)) for i in range(6)]
        d.polygon(pts, fill=255)
    elif kind == 'prompt':    # > _
        t = r * 0.4
        d.polygon([(cx - r, cy - r * 0.8), (cx - r + t, cy - r * 0.8), (cx + r * 0.2, cy), (cx - r + t, cy + r * 0.8),
                   (cx - r, cy + r * 0.8), (cx + r * 0.2 - t, cy)], fill=255)
        d.rectangle([cx + r * 0.3, cy + r * 0.5, cx + r * 1.0, cy + r * 0.8], fill=255)
    elif kind == 'cross':
        t = r * 0.38
        d.rectangle([cx - t, cy - r, cx + t, cy + r], fill=255)
        d.rectangle([cx - r, cy - t, cx + r, cy + t], fill=255)
    elif kind == 'pixel':     # 8-bit heart-ish diamond made of squares
        u = size / 5
        for dy, row in enumerate(['01010', '11111', '11111', '01110', '00100']):
            for dx, c in enumerate(row):
                if c == '1':
                    d.rectangle([cx - 2.5 * u + dx * u, cy - 2.5 * u + dy * u, cx - 2.5 * u + (dx + 1) * u - 1, cy - 2.5 * u + (dy + 1) * u - 1], fill=255)
    return m


# --- one state ----------------------------------------------------------------------------------
def draw_state(style, state, with_glyph=False):
    w, h = W * SS, H * SS
    shape = style['shape']
    pressed, hover = state == 2, state == 1
    body = mask(shape, w, h)
    kind = style['kind']
    canvas = Image.new('RGBA', (w, h), (0, 0, 0, 0))

    # fill
    stops = style['fill']
    if pressed:
        stops = list(reversed([(1 - p, c) for p, c in stops]))
    fill = vgrad(w, h, stops) if style.get('dir', 'v') == 'v' else _hgrad(w, h, stops)
    if hover:
        fill = ImageEnhance.Brightness(fill).enhance(1.18)
    elif pressed:
        fill = ImageEnhance.Brightness(fill).enhance(0.82)
    if kind == 'outline':
        # dark body with a coloured neon edge
        inner = mask(shape, w, h, inset=2)
        edge = ImageChops.subtract(body, inner)
        canvas.paste(fill.convert('RGBA'), (0, 0), body)
        glow_col = rgb(style['accent'])
        glow = Image.new('RGBA', (w, h), glow_col + (0,))
        glow.putalpha(edge.filter(ImageFilter.GaussianBlur((5 if hover else 3) * SS * 0.5)).point(lambda v: min(255, int(v * (1.6 if hover else 1.1)))))
        canvas = Image.alpha_composite(canvas, glow)
        line = Image.new('RGBA', (w, h), glow_col + (255,))
        line.putalpha(edge)
        canvas = Image.alpha_composite(canvas, line)
    else:
        canvas.paste(fill.convert('RGBA'), (0, 0), body)
        # border
        if style.get('border'):
            inner = mask(shape, w, h, inset=1)
            edge = ImageChops.subtract(body, inner)
            b = Image.new('RGBA', (w, h), rgb(style['border']) + (255,))
            b.putalpha(edge)
            canvas = Image.alpha_composite(canvas, b)

    # gloss / bevel
    gloss = style.get('gloss', 0)
    if gloss:
        hi = Image.new('RGBA', (w, h), (255, 255, 255, 0))
        a = Image.new('L', (w, h), 0)
        ImageDraw.Draw(a).rectangle([0, 0, w, h // 2 - SS], fill=int(255 * gloss * (0.4 if pressed else 1)))
        a = ImageChops.multiply(a, mask(shape, w, h, inset=2))
        hi.putalpha(a)
        canvas = Image.alpha_composite(canvas, hi)
    if style.get('bevel'):
        light, dark = (255, 255, 255, 190), (0, 0, 0, 150)
        if pressed:
            light, dark = dark, light
        d = ImageDraw.Draw(canvas)
        d.line([(2 * SS, 2 * SS), (w - 3 * SS, 2 * SS)], fill=light, width=SS)
        d.line([(2 * SS, 2 * SS), (2 * SS, h - 3 * SS)], fill=light, width=SS)
        d.line([(2 * SS, h - 3 * SS), (w - 3 * SS, h - 3 * SS)], fill=dark, width=SS)
        d.line([(w - 3 * SS, 2 * SS), (w - 3 * SS, h - 3 * SS)], fill=dark, width=SS)
    for band in style.get('bands', []):    # horizontal scan bands (retro styles), kept inside the body
        layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        for y in range(int(h * 0.55), h, 4 * SS):
            d.rectangle([0, y, w, y + SS], fill=rgb(band) + (70,))
        layer.putalpha(ImageChops.multiply(layer.getchannel('A'), mask(shape, w, h, inset=2)))
        canvas = Image.alpha_composite(canvas, layer)

    # glyph (only for the preview composite; the shipped buttons carry no symbol of their own)
    if with_glyph and style.get('glyph'):
        gx = style.get('gx', 16) * SS
        size = style.get('gsize', 12) * SS
        gm = glyph_mask(style['glyph'], w, h, gx, size)
        if pressed:
            gm = ImageChops.offset(gm, 0, SS)
        shadow = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        shadow.putalpha(ImageChops.offset(gm, 0, SS).filter(ImageFilter.GaussianBlur(SS * 0.6)).point(lambda v: v // 2))
        canvas = Image.alpha_composite(canvas, shadow)
        gcol = rgb(style['gcolor'])
        if hover:
            gcol = tuple(min(255, int(c * 1.15 + 12)) for c in gcol)
        g = Image.new('RGBA', (w, h), gcol + (255,))
        g.putalpha(gm)
        canvas = Image.alpha_composite(canvas, g)

    if pressed and not style.get('flat_pressed'):
        canvas = ImageChops.offset(canvas, 0, 0)
    return canvas.resize((W, H), Image.LANCZOS)


def sheet(style, with_glyph=False):
    img = Image.new('RGBA', (W, H * 3), (0, 0, 0, 0))
    for s in range(3):
        img.paste(draw_state(style, s, with_glyph), (0, H * s))
    return img


def glyph_icon(style, size):
    """The button's symbol on its own, as a start icon of size x size px."""
    big = size * 8
    m = glyph_mask(style['glyph'], big, big, big / 2, big * 0.78)
    g = Image.new('RGBA', (big, big), rgb(style['gcolor']) + (255,))
    g.putalpha(m)
    return g.resize((size, size), Image.LANCZOS)


def P(*pairs):
    return [(p, rgb(c)) for p, c in pairs]


STYLES = {
    # --- solid, glossy -------------------------------------------------------------------------
    'amber-pill': dict(shape='pill', kind='solid', fill=P((0, '#ffd36a'), (.5, '#f5a21b'), (1, '#c46a00')), border='#6b3a00',
                       gloss=.35, glyph='star', gcolor='#5a2d00'),
    'ruby-pill': dict(shape='pill', kind='solid', fill=P((0, '#ff7a82'), (.5, '#e0243a'), (1, '#8e0f22')), border='#4a0612',
                      gloss=.4, glyph='star', gcolor='#ffffff'),
    'sapphire-pill': dict(shape='pill', kind='solid', fill=P((0, '#8cc4ff'), (.5, '#2a78e0'), (1, '#123f94')), border='#0a2352',
                          gloss=.4, glyph='circle', gcolor='#ffffff'),
    'emerald-pill': dict(shape='pill', kind='solid', fill=P((0, '#9af0a4'), (.5, '#2fb250'), (1, '#12702c')), border='#0a3d18',
                         gloss=.4, glyph='diamond', gcolor='#ffffff'),
    'chrome-pill': dict(shape='pill', kind='solid', fill=P((0, '#ffffff'), (.45, '#cfd6de'), (.5, '#aeb7c2'), (1, '#e8edf2')), border='#59616b',
                        gloss=.2, glyph='ring', gcolor='#2b3138'),
    'gunmetal-pill': dict(shape='pill', kind='solid', fill=P((0, '#59616b'), (.5, '#2e343b'), (1, '#16191d')), border='#08090b',
                          gloss=.18, glyph='bolt', gcolor='#ff9a1f'),
    'copper-round': dict(shape='round', kind='solid', fill=P((0, '#f3b48a'), (.5, '#c06a3a'), (1, '#7a3a1a')), border='#3d1a08',
                         gloss=.22, bevel=True, glyph='hex', gcolor='#3d1a08'),
    'gold-chamfer': dict(shape='chamfer', kind='solid', fill=P((0, '#fff0a8'), (.5, '#e0b32a'), (1, '#9a7208')), border='#523d00',
                         gloss=.3, bevel=True, glyph='star', gcolor='#523d00'),
    'ice-pill': dict(shape='pill', kind='solid', fill=P((0, '#ffffff'), (.5, '#bfe9ff'), (1, '#6fb7e8')), border='#3b7ba6',
                     gloss=.5, glyph='diamond', gcolor='#1c5a86'),
    'lava-round': dict(shape='round', kind='solid', fill=P((0, '#ffb347'), (.5, '#e8461a'), (1, '#4a0a05')), border='#240300',
                       gloss=.25, glyph='bolt', gcolor='#fff2c4'),
    # --- neon outlines -------------------------------------------------------------------------
    'neon-orange': dict(shape='pill', kind='outline', fill=P((0, '#1d1209'), (1, '#0d0804')), accent='#ff8a1f', glyph='bolt', gcolor='#ffb35c'),
    'neon-cyan': dict(shape='pill', kind='outline', fill=P((0, '#08171d'), (1, '#040c10')), accent='#27e1ff', glyph='diamond', gcolor='#8df0ff'),
    'neon-magenta': dict(shape='pill', kind='outline', fill=P((0, '#1d0a1a'), (1, '#0e040c')), accent='#ff3fd0', glyph='chevron', gcolor='#ff97e6'),
    'neon-green': dict(shape='pill', kind='outline', fill=P((0, '#09190c'), (1, '#040d06')), accent='#35ff6a', glyph='play', gcolor='#9dffb5'),
    'neon-duotone-slant': dict(shape='slant', kind='outline', fill=P((0, '#14082a'), (1, '#080414')), accent='#b05cff', glyph='star', gcolor='#ff6fd8'),
    # --- classic / flat -------------------------------------------------------------------------
    'classic-bevel': dict(shape='rect', kind='solid', fill=P((0, '#d4d0c8'), (1, '#d4d0c8')), border='#404040', bevel=True,
                          glyph='play', gcolor='#202020'),
    'flat-dark': dict(shape='round', kind='solid', fill=P((0, '#2b2f36'), (1, '#2b2f36')), border='#14161a', glyph='cross', gcolor='#4dd0ff'),
    'flat-light': dict(shape='round', kind='solid', fill=P((0, '#f0f2f5'), (1, '#f0f2f5')), border='#aab2bd', glyph='cross', gcolor='#2f6fe0'),
    'green-tab': dict(shape='tab', kind='solid', fill=P((0, '#66d46c'), (.5, '#2f9e38'), (1, '#1b6422')), border='#0f3d14',
                      gloss=.3, glyph='circle', gcolor='#ffffff'),
    # --- themed ---------------------------------------------------------------------------------
    'synthwave': dict(shape='notch', kind='solid', fill=P((0, '#ff5fc8'), (.5, '#a02be0'), (1, '#2a1285')), border='#12063d',
                      gloss=.2, bands=['#ffffff'], glyph='chevron', gcolor='#fff3a0'),
    'terminal': dict(shape='rect', kind='outline', fill=P((0, '#021006'), (1, '#021006')), accent='#27e05a', bands=['#27e05a'],
                     glyph='prompt', gcolor='#5dff8a'),
    'pixel-block': dict(shape='rect', kind='solid', fill=P((0, '#3a3f6b'), (1, '#3a3f6b')), border='#0d0f22', bevel=True,
                        glyph='pixel', gcolor='#ff3b5c', gsize=14),
    'sunset-pill': dict(shape='pill', kind='solid', fill=P((0, '#ffe08a'), (.35, '#ff8a4c'), (.7, '#d8306e'), (1, '#5a1a7a')), border='#2a0a3a',
                        gloss=.25, glyph='circle', gcolor='#fff6d6'),
    'slate-notch': dict(shape='notch', kind='solid', fill=P((0, '#8693a3'), (.5, '#4d5a6a'), (1, '#262e39')), border='#0e1218',
                        gloss=.2, bevel=True, glyph='hex', gcolor='#e8f2ff'),
    'outline-minimal': dict(shape='pill', kind='outline', fill=P((0, '#101114'), (1, '#101114')), accent='#e8eaed', glyph='circle', gcolor='#e8eaed', gsize=8),
}


def main():
    names = list(STYLES)
    for folder in ('3state', '1state'):
        os.makedirs(os.path.join(OUT, folder), exist_ok=True)
        for old in os.listdir(os.path.join(OUT, folder)):
            os.remove(os.path.join(OUT, folder, old))
    for name in names:
        full = sheet(STYLES[name])
        full.save(os.path.join(OUT, '3state', name + '.png'))
        full.crop((0, 0, W, H)).save(os.path.join(OUT, '1state', name + '.png'))
        for size in (16, 24, 32, 48):
            folder = os.path.join(os.path.dirname(OUT), 'icons', 'start', f'{size}px')
            os.makedirs(folder, exist_ok=True)
            glyph_icon(STYLES[name], size).save(os.path.join(folder, f'btn-{name}.png'))
    for stale in [f for f in os.listdir(OUT) if f.endswith('.png') and f not in ('preview.png',) and not f.startswith('template-')]:
        os.remove(os.path.join(OUT, stale))
    print(len(names), 'buttons (tristate + single) and', len(names) * 4, 'start icons written')
    make_preview(names)
    make_reference()


def make_preview(names):
    cols = 4
    rows = math.ceil(len(names) / cols)
    cw, ch = W + 40, H * 3 + 40
    sheet_img = Image.new('RGBA', (cols * cw, rows * ch), (28, 30, 34, 255))
    d = ImageDraw.Draw(sheet_img)
    for i, name in enumerate(names):
        x, y = (i % cols) * cw + 20, (i // cols) * ch + 10
        s = sheet(STYLES[name], with_glyph=True)
        for st in range(3):
            sheet_img.alpha_composite(s.crop((0, st * H, W, (st + 1) * H)), (x, y + st * (H + 2)))
        d.text((x, y + 3 * (H + 2) - 2), name, fill=(190, 195, 205, 255))
    sheet_img = sheet_img.resize((sheet_img.width * 2, sheet_img.height * 2), Image.NEAREST)
    sheet_img.convert('RGB').save(os.path.join(OUT, 'preview.png'))


def make_reference():
    """template-3state.png: the layout every 3-state button follows, with the sizes marked."""
    scale = 4
    img = Image.new('RGBA', (W * scale + 160, H * 3 * scale + 20), (36, 38, 44, 255))
    d = ImageDraw.Draw(img)
    for st, label in enumerate(['1 normal', '2 hover', '3 pressed']):
        y = 10 + st * H * scale
        d.rectangle([0, y, W * scale - 1, y + H * scale - 1], outline=(255, 190, 60, 255), width=2)
        d.rectangle([4, y + 4, W * scale - 5, y + H * scale - 5], outline=(120, 124, 132, 255))
        d.text((W * scale + 10, y + H * scale // 2 - 5), f'{label}\n{W} x {H} px', fill=(230, 232, 236, 255))
    img.convert('RGB').save(os.path.join(OUT, 'template-3state.png'))
    # a clean transparent template to paint over
    tpl = Image.new('RGBA', (W, H * 3), (0, 0, 0, 0))
    td = ImageDraw.Draw(tpl)
    for st in range(3):
        td.rectangle([0, st * H, W - 1, st * H + H - 1], outline=(255, 190, 60, 255))
    tpl.save(os.path.join(OUT, 'template-blank.png'))
    one = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(one).rectangle([0, 0, W - 1, H - 1], outline=(255, 190, 60, 255))
    one.save(os.path.join(OUT, 'template-single-blank.png'))


if __name__ == '__main__':
    main()
