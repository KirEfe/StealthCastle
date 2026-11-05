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

def zone_stats(path, sprites, x0, x1, y0, y1, label):
    im = Image.open(path).convert("RGBA")
    print(f"\n=== {label}: x[{x0},{x1}) y[{y0},{y1}) ===")
    rows = []
    for name, x, y, w, h in sprites:
        if x0 <= x < x1 and y0 <= y < y1:
            crop = im.crop((x, y, x+w, y+h))
            px = list(crop.getdata())
            opaque = sum(1 for p in px if p[3] > 0)
            frac = opaque/(w*h)
            r=g=b=0
            for p in px:
                if p[3] > 0:
                    r+=p[0]; g+=p[1]; b+=p[2]
            if opaque>0:
                r//=opaque; g//=opaque; b//=opaque
            rows.append((name, x, y, frac, (r,g,b)))
    rows.sort(key=lambda t: (int(t[4][0]), int(t[4][1]), int(t[4][2]), -t[3]))
    print(f"{'name':20s} {'x':>4} {'y':>4} {'opaque%':>7} {'rgb':>10}")
    for name, x, y, frac, rgb in rows:
        print(f"{name:20s} {x:4d} {y:4d} {frac*100:6.1f}% {rgb}")
    print(f"всего тайлов в зоне: {len(rows)}")

ml = parse_sprites("Assets/RF Castle/Sliced/mainlevbuild.png.meta")
# 37 колонок x 46 строк, тайл 16px
# Верхняя половина: y >= 368 (строки 0..22) -> стены
zone_stats("Assets/RF Castle/Sliced/mainlevbuild.png", ml, 0, 592, 368, 736, "СТЕНЫ (верхняя половина, y 368-736)")
# Нижняя левая четверть: y >= 552, x < 296 -> пол
zone_stats("Assets/RF Castle/Sliced/mainlevbuild.png", ml, 0, 296, 552, 736, "ПОЛ (нижняя левая четверть, y 552-736, x 0-296)")
# Нижняя правая четверть: y >= 552, x >= 296 -> колонны/переходы/лестницы
zone_stats("Assets/RF Castle/Sliced/mainlevbuild.png", ml, 296, 592, 552, 736, "КОЛОННЫ/ПЕРЕХОДЫ (нижняя правая четверть, y 552-736, x 296-592)")
