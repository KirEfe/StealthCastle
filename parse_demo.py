import re
from pathlib import Path

text = Path("Assets/RF Castle/Scenes/Demo.unity").read_text(encoding="utf-8", errors="ignore")

# Разбираем по блокам --- !u!1 &fid
blocks = re.findall(r"--- !u!1 &(\d+)\n(.*?)(?=--- !u!1 |\Z)", text, re.S)
print("Всего GameObject:", len(blocks))
for fid, body in blocks:
    nm = re.search(r"m_Name: (.+)", body)
    if not nm:
        continue
    name = nm.group(1).strip()
    has_sprite = "SpriteRenderer:" in body
    has_anim = "Animator:" in body
    print(f"[{fid}] {name} sprite={has_sprite} anim={has_anim}")
    sm = re.search(r"m_Sprite: \{fileID: 21300000, guid: ([a-f0-9]+)", body)
    pm = re.search(r"m_LocalPosition: \{x: ([\d.\-]+), y: ([\d.\-]+), z: ([\d.\-]+)\}", body)
    lm = re.search(r"m_Controller: \{fileID: 9100000, guid: ([a-f0-9]+)", body)
    if sm:
        print(f"   sprite guid: {sm.group(1)}")
    if pm:
        print(f"   pos: ({pm.group(1)}, {pm.group(2)}, {pm.group(3)})")
    if lm:
        print(f"   animator guid: {lm.group(1)}")
