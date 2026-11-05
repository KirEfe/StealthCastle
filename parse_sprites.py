import re
from pathlib import Path

def parse_sprites(meta_path):
    text = Path(meta_path).read_text(encoding="utf-8", errors="ignore")
    sprites = []
    for m in re.finditer(r"name:\s*(\S+)\s*\n\s*rect:\s*\n\s*serializedVersion:\s*\d+\s*\n\s*x:\s*(-?\d+)\s*\n\s*y:\s*(-?\d+)\s*\n\s*width:\s*(-?\d+)\s*\n\s*height:\s*(-?\d+)", text):
        name, x, y, w, h = m.groups()
        sprites.append((name, int(x), int(y), int(w), int(h)))
    return sprites

ml = parse_sprites("Assets/RF Castle/Sliced/mainlevbuild.png.meta")
dec = parse_sprites("Assets/RF Castle/Sliced/decorative.png.meta")
print(f"mainlevbuild: {len(ml)} спрайтов")
print(f"decorative: {len(dec)} спрайтов")
print("\n=== mainlevbuild (первые 40) ===")
for s in ml[:40]:
    print(f"  {s[0]:20s} x={s[1]:4d} y={s[2]:4d} w={s[3]:3d} h={s[4]:3d}")
print("\n=== decorative (все) ===")
for s in dec:
    print(f"  {s[0]:20s} x={s[1]:4d} y={s[2]:4d} w={s[3]:3d} h={s[4]:3d}")
