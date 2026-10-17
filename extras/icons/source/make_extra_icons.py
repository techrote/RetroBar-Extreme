"""Draws the example Show desktop and notification area icons (Icons\\showdesktop, Icons\\tray).

Original artwork drawn in code, each at 16, 24, 32 and 48 px.  Run: python make_extra_icons.py
(needs `pip install pillow`).  Add your own by adding a function to SHOWDESKTOP or TRAY.
"""
import math
import os
from PIL import Image, ImageDraw

SIZES = (16, 24, 32, 48)
OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BIG = 512  # drawn at 512, scaled down


def rgb(h, a=255):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


def canvas():
    img = Image.new('RGBA', (BIG, BIG), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def u(v):
    """A 0..100 coordinate to canvas pixels."""
    return v * BIG / 100


def box(d, x0, y0, x1, y1, fill=None, outline=None, width=0, r=0):
    d.rounded_rectangle([u(x0), u(y0), u(x1), u(y1)], radius=u(r), fill=fill, outline=outline, width=int(u(width)))


# ---- Show desktop ----------------------------------------------------------------------------
def sd_monitor():
    img, d = canvas()
    box(d, 10, 16, 90, 66, fill=rgb('#2a3442'), outline=rgb('#cfe3ff'), width=5, r=6)
    box(d, 16, 22, 84, 60, fill=rgb('#3d83d6'), r=3)
    box(d, 40, 70, 60, 80, fill=rgb('#cfe3ff'))
    box(d, 28, 80, 72, 87, fill=rgb('#cfe3ff'), r=3)
    return img


def sd_corner():
    img, d = canvas()
    box(d, 8, 8, 92, 92, fill=rgb('#1c2430'), outline=rgb('#8fb8ee'), width=5, r=10)
    d.polygon([(u(50), u(92) - 1), (u(92) - 1, u(92) - 1), (u(92) - 1, u(50))], fill=rgb('#6fb0ff'))
    d.polygon([(u(62), u(92) - 1), (u(92) - 1, u(92) - 1), (u(92) - 1, u(62))], fill=rgb('#e8f3ff'))
    return img


def sd_windows():
    img, d = canvas()
    box(d, 8, 22, 68, 80, fill=rgb('#2a3442'), outline=rgb('#9fc2f2'), width=5, r=5)
    box(d, 32, 10, 92, 68, fill=rgb('#34445a'), outline=rgb('#e8f3ff'), width=5, r=5)
    box(d, 32, 10, 92, 24, fill=rgb('#e8f3ff'), r=5)
    return img


def sd_minus():
    img, d = canvas()
    box(d, 8, 16, 92, 84, fill=rgb('#222a36'), outline=rgb('#cfe3ff'), width=5, r=8)
    box(d, 26, 52, 74, 64, fill=rgb('#ffd166'), r=3)
    return img


def sd_grid():
    img, d = canvas()
    for i, (x, y) in enumerate([(10, 10), (54, 10), (10, 54), (54, 54)]):
        box(d, x, y, x + 36, y + 36, fill=rgb(['#4da3ff', '#7dd87d', '#ffb347', '#ff6b81'][i]), r=6)
    return img


def sd_chevron():
    img, d = canvas()
    box(d, 8, 22, 92, 36, fill=rgb('#cfe3ff'), r=5)
    d.polygon([(u(22), u(50)), (u(36), u(50)), (u(50), u(68)), (u(64), u(50)), (u(78), u(50)), (u(50), u(86))], fill=rgb('#6fb0ff'))
    return img


SHOWDESKTOP = {
    'monitor': sd_monitor, 'corner': sd_corner, 'windows': sd_windows,
    'minimise': sd_minus, 'grid': sd_grid, 'chevron': sd_chevron,
}


# ---- notification area -----------------------------------------------------------------------
def dot(colour, glow):
    def draw():
        img, d = canvas()
        d.ellipse([u(12), u(12), u(88), u(88)], fill=rgb(glow, 120))
        d.ellipse([u(22), u(22), u(78), u(78)], fill=rgb(colour), outline=rgb('#ffffff', 180), width=int(u(3)))
        d.ellipse([u(34), u(30), u(52), u(44)], fill=rgb('#ffffff', 150))
        return img
    return draw


def tr_bell():
    img, d = canvas()
    d.pieslice([u(22), u(14), u(78), u(70)], 180, 360, fill=rgb('#ffcf4d'))
    d.polygon([(u(22), u(42)), (u(78), u(42)), (u(88), u(76)), (u(12), u(76))], fill=rgb('#ffcf4d'))
    box(d, 8, 74, 92, 82, fill=rgb('#e0a21a'), r=3)
    d.ellipse([u(40), u(80), u(60), u(94)], fill=rgb('#a86f00'))
    return img


def tr_shield():
    img, d = canvas()
    d.polygon([(u(50), u(8)), (u(88), u(22)), (u(84), u(58)), (u(50), u(92)), (u(16), u(58)), (u(12), u(22))], fill=rgb('#2f7bd9'), outline=rgb('#cfe3ff'))
    d.polygon([(u(50), u(18)), (u(78), u(28)), (u(75), u(55)), (u(50), u(80))], fill=rgb('#5aa4ff'))
    d.line([(u(34), u(48)), (u(46), u(62)), (u(68), u(34))], fill=rgb('#ffffff'), width=int(u(8)), joint='curve')
    return img


def tr_heart():
    img, d = canvas()
    d.ellipse([u(10), u(14), u(54), u(58)], fill=rgb('#ff4d6d'))
    d.ellipse([u(46), u(14), u(90), u(58)], fill=rgb('#ff4d6d'))
    d.polygon([(u(14), u(46)), (u(86), u(46)), (u(50), u(90))], fill=rgb('#ff4d6d'))
    d.ellipse([u(22), u(22), u(38), u(36)], fill=rgb('#ffb3c1'))
    return img


def tr_bolt():
    img, d = canvas()
    d.polygon([(u(58), u(6)), (u(22), u(54)), (u(46), u(54)), (u(38), u(94)), (u(80), u(40)), (u(55), u(40))], fill=rgb('#ffc533'), outline=rgb('#a86f00'))
    return img


def tr_cloud():
    img, d = canvas()
    for x0, y0, x1, y1 in [(10, 42, 46, 78), (28, 24, 70, 66), (54, 38, 92, 76), (22, 52, 80, 80)]:
        d.ellipse([u(x0), u(y0), u(x1), u(y1)], fill=rgb('#dbe9f9'))
    box(d, 24, 56, 78, 78, fill=rgb('#dbe9f9'), r=10)
    return img


def tr_chip():
    img, d = canvas()
    for i in range(4):
        v = 24 + i * 16
        box(d, v, 6, v + 6, 20, fill=rgb('#c9d1d9'))
        box(d, v, 80, v + 6, 94, fill=rgb('#c9d1d9'))
        box(d, 6, v, 20, v + 6, fill=rgb('#c9d1d9'))
        box(d, 80, v, 94, v + 6, fill=rgb('#c9d1d9'))
    box(d, 18, 18, 82, 82, fill=rgb('#27303b'), outline=rgb('#8fa3b8'), width=4, r=6)
    box(d, 34, 34, 66, 66, fill=rgb('#4da3ff'), r=4)
    return img


TRAY = {
    'dot-green': dot('#3ddc84', '#3ddc84'), 'dot-red': dot('#ff4d4d', '#ff4d4d'), 'dot-amber': dot('#ffb020', '#ffb020'),
    'bell': tr_bell, 'shield': tr_shield, 'heart': tr_heart, 'bolt': tr_bolt, 'cloud': tr_cloud, 'chip': tr_chip,
}


def write(kind, name, draw):
    big = draw()
    for size in SIZES:
        folder = os.path.join(OUT, kind, f'{size}px')
        os.makedirs(folder, exist_ok=True)
        big.resize((size, size), Image.LANCZOS).save(os.path.join(folder, name + '.png'))


def main():
    for name, draw in SHOWDESKTOP.items():
        write('showdesktop', name, draw)
    for name, draw in TRAY.items():
        write('tray', name, draw)
    print(len(SHOWDESKTOP), 'Show desktop icons,', len(TRAY), 'notification area icons x', len(SIZES), 'sizes')


if __name__ == '__main__':
    main()
