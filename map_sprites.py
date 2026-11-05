import re
from pathlib import Path

def get_sprite_ids(meta_path):
    text = Path(meta_path).read_text(encoding="utf-8", errors="ignore")
    names = re.findall(r"name:\s*(\S+)\s*\n\s*rect:", text)
    ids = re.findall(r"spriteID: ([a-f0-9]+)", text)
    return list(zip(names, ids))

ml_ids = get_sprite_ids("Assets/RF Castle/Sliced/mainlevbuild.png.meta")
dec_ids = get_sprite_ids("Assets/RF Castle/Sliced/decorative.png.meta")

demo = {
    "mainlevbuild": "afce676399ec9654e8047ecb87630e3f",
    "decorative1": "9ee2d4d02a04b5e47a8c77766cd22fb1",
    "decorative2": "245c9dd8c2ec1c84a814f4f9618e7c8b",
}

ml_dict = {sid: n for n, sid in ml_ids}
dec_dict = {sid: n for n, sid in dec_ids}

for name, sid in demo.items():
    if sid in ml_dict:
        print(f"{name}: mainlevbuild_{ml_dict[sid]}")
    elif sid in dec_dict:
        print(f"{name}: decorative_{dec_dict[sid]}")
    else:
        print(f"{name}: НЕ НАЙДЕН ({sid})")

print("\n=== unique spriteIDs in mainlevbuild ===")
print("count:", len(set(sid for _, sid in ml_ids)))
print("first 10:", [sid for _, sid in ml_ids][:10])
