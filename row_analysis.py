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

def analyze_rows(path, sprites, label):
    im = Image.open(path).convert("RGBA")
    print(f"\n=== {label} ===")
    # группируем по y
    rows = {}
    for name, x, y, w, h in sprites:
        rows.setdefault(y, []).append((name, x, y, w, h))
    for y in sorted(rows.keys(), reverse=True):
        items = sorted(rows[y], key=lambda t: t[1])
        line = f"y={y:4d}: "
        for name, x, y0, w, h in items:
            crop = im.crop((x, y0, x+w, y0+h))
            px = list(crop.getdata())
            opaque = sum(1 for p in px if p[3] > 0)
            frac = opaque/(w*h)
            r=g=b=0
            for p in px:
                if p[3] > 0:
                    r+=p[0]; g+=p[1]; b+=p[2]
            if opaque>0:
                r//=opaque; g//=opaque; b//=opaque
            line += f"{name}({frac*100:3.0f}%,{r},{g},{b}) "
        print(line)

ml = parse_sprites("Assets/RF Castle/Sliced/mainlevbuild.png.meta")
analyze_rows("Assets/RF Castle/Sliced/mainlevbuild.png", ml, "mainlevbuild (y сверху вниз)")
