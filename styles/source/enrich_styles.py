"""Gives every example style an 8-colour palette and ten textures in its texture palette.

    python enrich_styles.py            (from this folder; edits ../*.json in place, safe to run again)

* Palette: the style's existing slots stay where they are; empty slots are filled from the colours the
  style itself uses (most used first, no near-duplicates) until eight are set; if the style uses
  fewer than eight distinct colours, complementary / analogous variants of its colours fill the rest.
* Texture palette: the textures the style already uses come first, then built-in patterns coloured
  with three colours picked from the palette (dark to light), ten in all. Slots 11-20 stay empty.

The style files keep their comments: only the palette and texturePalette lines are rewritten.
"""
import colorsys
import glob
import json
import os
import re
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
STYLES = os.path.dirname(HERE)
SKIP = {'Broken.json'}

# A pattern and the settings that make it look good, drawn from the built-in presets.
PATTERNS = [
    ('brushed', {'strength': 0.6, 'contrast': 1.2}),
    ('carbon', {}),
    ('diamond', {}),
    ('clouds', {'octaves': 4, 'scale': 2}),
    ('cells', {}),
    ('marble', {'frequency': 3, 'strength': 1.5}),
    ('scanlines', {'frequency': 32}),
    ('weave', {}),
    ('checker', {'frequency': 16, 'contrast': 0.6}),
    ('waves', {'frequency': 3, 'octaves': 3}),
    ('bricks', {'frequency': 12}),
    ('dots', {'frequency': 12}),
    ('noise', {'strength': 0.6}),
]


def parse_colour(text):
    m = re.fullmatch(r'#([0-9a-fA-F]{3,8})', text.strip())
    if not m:
        return None
    h = m.group(1)
    if len(h) in (3, 4):
        h = ''.join(c * 2 for c in h)
    if len(h) == 8:
        h = h[2:]  # drop alpha
    if len(h) != 6:
        return None
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def hexs(c):
    return '#%02x%02x%02x' % tuple(int(round(max(0, min(255, v)))) for v in c)


def luminance(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def distance(a, b):
    return sum((x - y) ** 2 for x, y in zip(a, b)) ** 0.5


def variants(c):
    """Complementary, analogous and tonal relatives of a colour, nearest relatives first."""
    h, l, s = colorsys.rgb_to_hls(*(v / 255 for v in c))
    out = []
    for dh, dl, ds in [(0.5, 0, 1), (0.083, 0, 1), (-0.083, 0, 1), (0.333, 0, 1), (-0.333, 0, 1), (0, 0.18, 1), (0, -0.18, 1),
                       (0.5, 0.12, 0.8), (0.5, -0.12, 0.8), (0.083, 0.15, 0.7), (-0.083, -0.15, 0.7), (0, 0.3, 0.5)]:
        r, g, b = colorsys.hls_to_rgb((h + dh) % 1.0, max(0.05, min(0.95, l + dl)), max(0, min(1, s * ds)))
        out.append((r * 255, g * 255, b * 255))
    return out


def collect(node, found):
    if isinstance(node, str):
        c = parse_colour(node)
        if c:
            found.append(c)
    elif isinstance(node, list):
        for item in node:
            collect(item, found)
    elif isinstance(node, dict):
        for key, value in node.items():
            if key == 'palette':
                continue
            collect(value, found)


def load_text(text):
    body = '\n'.join(line for line in text.split('\n') if not line.lstrip().startswith('//'))
    body = re.sub(r',(\s*[}\]])', r'\1', body)  # trailing commas
    return json.loads(body)


def load(path):
    text = open(path, encoding='utf-8').read()
    return text, load_text(text)



def build_palette(existing, used):
    slots = list(existing) + [None] * (12 - len(existing))
    slots = slots[:12]
    have = [parse_colour(s) for s in slots if s]
    counts = Counter(used)
    candidates = [c for c, _ in counts.most_common()]
    for c in candidates:
        if sum(1 for s in slots if s) >= 8:
            break
        if all(distance(c, h) > 28 for h in have if h):
            i = slots.index(None)
            slots[i] = hexs(c)
            have.append(c)
    # Not enough distinct colours: complementary and analogous relatives of what is there.
    seeds = [h for h in have if h]
    k = 0
    while sum(1 for s in slots if s) < 8 and seeds and k < 400:
        seed = seeds[k % len(seeds)]
        step = k // len(seeds)
        k += 1
        cand = variants(seed)[step % 12]
        if all(distance(cand, h) > 24 for h in have):
            slots[slots.index(None)] = hexs(cand)
            have.append(cand)
    return slots


def texture_object(name, params, lut):
    obj = {'texture': name, 'lut': lut, 'scale': params.get('scale', 1), 'contrast': params.get('contrast', 1)}
    for key in ('frequency', 'octaves', 'strength'):
        if key in params:
            obj[key] = params[key]
    return obj


def build_textures(style, palette, rng_seed):
    result = []
    seen = set()

    def add(obj):
        key = json.dumps(obj, sort_keys=True)
        if key not in seen:
            seen.add(key)
            result.append(obj)

    # The style's own textures first.
    def walk(node):
        if isinstance(node, dict):
            if 'texture' in node and 'lut' in node and isinstance(node['lut'], list):
                add({k: v for k, v in node.items()})
            for v in node.values():
                walk(v)
        elif isinstance(node, list):
            for v in node:
                walk(v)
    walk(style.get('parts', {}))
    result = result[:4]

    colours = sorted([parse_colour(c) for c in palette if c], key=luminance)
    n = len(colours)
    i = 0
    while len(result) < 10:
        name, params = PATTERNS[(rng_seed + i) % len(PATTERNS)]
        # three colours, dark to light, sliding through the palette so each texture differs
        start = i % max(1, n - 2)
        picks = [colours[min(n - 1, start + j * max(1, (n - 1 - start) // 2))] for j in range(3)] if n >= 3 else colours
        if len(picks) < 2:
            picks = [(0, 0, 0), (255, 255, 255)]
        if i % 3 == 2:
            picks = list(reversed(picks))  # some light-on-dark
        add(texture_object(name, params, [hexs(c) for c in picks]))
        i += 1
        if i > 200:
            break
    return result[:10]


def compact(obj):
    return json.dumps(obj, separators=(', ', ': '))


def render(palette, textures):
    pal = '  "palette": [ ' + ', '.join(json.dumps(p) for p in palette) + ' ],'
    tex = ['  "texturePalette": [']
    entries = [compact(t) for t in textures] + ['null'] * (20 - len(textures))
    tex += ['    ' + e + (',' if i < len(entries) - 1 else '') for i, e in enumerate(entries)]
    tex.append('  ]')
    return pal, '\n'.join(tex)


def bake_palette_luts(text, style):
    """A texture whose lut is "palette" uses every occupied slot, so it can't share a palette with
    other colour picks. Give it the explicit colours it was using."""
    colours = [c for c in style.get('palette', []) if c]
    if '"lut": "palette"' not in text or not colours:
        return text
    return text.replace('"lut": "palette"', '"lut": [ ' + ', '.join(json.dumps(c) for c in colours) + ' ]')


def rewrite(path):
    text, style = load(path)
    text = bake_palette_luts(text, style)
    style = load_text(text)
    used = []
    collect(style.get('parts', {}), used)
    collect(style.get('start', {}), used)
    palette = build_palette(style.get('palette', []), used)
    textures = build_textures(style, palette, sum(ord(c) for c in os.path.basename(path)))
    pal_line, tex_block = render(palette, textures)

    # drop old palette / texturePalette blocks
    text = re.sub(r'(?m)^\s*"palette": \[[^\]]*\],?\n', '', text)
    text = re.sub(r'(?ms)^\s*"texturePalette": \[.*?\n  \],?\n', '', text)
    # insert before the closing brace of the file
    end = text.rstrip().rfind('}')
    head = text[:end].rstrip()
    if not head.endswith(','):
        head += ','
    text = head + '\n' + pal_line + '\n' + tex_block + '\n}\n'
    # tidy: a trailing comma before the closing brace is invalid in strict JSON
    open(path, 'w', encoding='utf-8', newline='\n').write(text)
    return sum(1 for p in palette if p), len(textures)


def main():
    for path in sorted(glob.glob(os.path.join(STYLES, '*.json'))):
        if os.path.basename(path) in SKIP:
            continue
        colours, textures = rewrite(path)
        print(f'{os.path.basename(path):28} {colours} colours, {textures} textures')


if __name__ == '__main__':
    main()
