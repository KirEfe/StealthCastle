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

def pick(path, sprites, label, rgb_target, tol=22, min_opaque=0.9):
    im = Image.open(path).convert("RGBA")
    res = []
    for name, x, y, w, h in sprites:
        crop = im.crop((x, y, x+w, y+h))
        px = list(crop.getdata())
        opaque = sum(1 for p in px if p[3] > 0)
        frac = opaque/(w*h)
        if frac < min_opaque:
            continue
        r=g=b=0
        for p in px:
            if p[3] > 0:
                r+=p[0]; g+=p[1]; b+=p[2]
        r//=opaque; g//=opaque; b//=opaque
        if abs(r-rgb_target[0])<=tol and abs(g-rgb_target[1])<=tol and abs(b-rgb_target[2])<=tol:
            res.append((name, (r,g,b), frac))
    print(f"\n{label} (target={rgb_target}, tol={tol}): {len(res)} тайлов")
    print("  " + ", ".join(n for n,_,_ in res))
    return res

ml = parse_sprites("Assets/RF Castle/Sliced/mainlevbuild.png.meta")
# Каменный пол: коричневый ~ (42,36,33)
pick("Assets/RF Castle/Sliced/mainlevbuild.png", ml, "ПОЛ (коричневый камень)", (42,36,33), tol=12)
# Стены: серо-коричневый ~ (45,40,37)
pick("Assets/RF Castle/Sliced/mainlevbuild.png", ml, "СТЕНЫ (серо-коричневые)", (45,40,37), tol=12)
# Проёмы/двери: тёмный ~ (27,20,18)
pick("Assets/RF Castle/Sliced/mainlevbuild.png", ml, "ПРОЁМЫ/ДВЕРИ (тёмные)", (27,20,18), tol=8)
# Мох/трава: зелёный ~ (51,47,40)
pick("Assets/RF Castle/Sliced/mainlevbuild.png", ml, "МОХ/ТРАВА (зелёные)", (51,47,40), tol=10)
# Платформы тёмные (7,7,7)
pick("Assets/RF Castle/Sliced/mainlevbuild.png", ml, "ПЕЩЕРА/ТЁМНЫЕ (7,7,7)", (7,7,7), tol=4)
