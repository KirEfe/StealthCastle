import re
from pathlib import Path
from PIL import Image

def parse_sprites(meta_path):
    text = Path(meta_path).read_text(encoding="utf-8", errors="ignore")
    out = []
    for m in re.finditer(r"name:\s*(\S+)\s*\n\s*rect:\s*\n\s*serializedVersion:\s*\d+\s*\n\s*x:\s*(-?\d+)\s*\n\s*y:\s*(-?\d+)\s*\n\s*width:\s*(-?\d+)\s*\n\s*height:\s*(-?\d+)", text):
        name, x, y, w, h = m.groups()
        out.append((name, int(x), int(y), int(w), int(h)))
    return out

def analyze(path, sprites):
    im = Image.open(path).convert("RGBA")
    print(f"\n=== {path} ({im.size[0]}x{im.size[1]}) ===")
    rows = []
    for name, x, y, w, h in sprites:
        crop = im.crop((x, y, x+w, y+h))
        px = list(crop.getdata())
        opaque = sum(1 for p in px if p[3] > 0)
        total = w*h
        frac = opaque/total
        # avg color of opaque
        r=g=b=0
        for p in px:
            if p[3] > 0:
                r+=p[0]; g+=p[1]; b+=p[2]
        if opaque>0:
            r//=opaque; g//=opaque; b//=opaque
        else:
            r=g=b=0
        rows.append((name, w, h, frac, (r,g,b)))
    # сортировка по % заполнения
    rows.sort(key=lambda t: -t[3])
    print(f"{'name':22s} {'w':>3} {'h':>3} {'opaque%':>7} {'rgb':>10}")
    for name, w, h, frac, rgb in rows[:60]:
        print(f"{name:22s} {w:3d} {h:3d} {frac*100:6.1f}% {rgb}")
    print(f"... всего {len(rows)} тайлов")
    return rows

ml = parse_sprites("Assets/RF Castle/Sliced/mainlevbuild.png.meta")
dec = parse_sprites("Assets/RF Castle/Sliced/decorative.png.meta")
analyze("Assets/RF Castle/Sliced/mainlevbuild.png", ml)
analyze("Assets/RF Castle/Sliced/decorative.png", dec)
