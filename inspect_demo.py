import re
from pathlib import Path
from PIL import Image

print("=== Размеры текстур ===")
for p in ["Assets/RF Castle/mainlevbuild.png", "Assets/RF Castle/decorative.png",
          "Assets/RF Castle/Sliced/mainlevbuild.png", "Assets/RF Castle/Sliced/decorative.png"]:
    try:
        im = Image.open(p)
        print(f"  {p}: {im.size[0]}x{im.size[1]} px, mode={im.mode}")
    except Exception as e:
        print(f"  {p}: ОШИБКА {e}")

print("\n=== Demo.unity: все объекты ===")
text = Path("Assets/RF Castle/Scenes/Demo.unity").read_text(encoding="utf-8", errors="ignore")
blocks = re.findall(r"--- !u!1 &(\d+)\n(.*?)(?=--- !u!1 |\Z)", text, re.S)
for fid, body in blocks:
    nm = re.search(r"m_Name: (.+)", body)
    if not nm:
        continue
    name = nm.group(1).strip()
    pm = re.search(r"m_LocalPosition: \{x: ([\d.\-]+), y: ([\d.\-]+), z: ([\d.\-]+)\}", body)
    sm = re.search(r"m_Sprite: \{fileID: 21300000, guid: ([a-f0-9]+)", body)
    sz = re.search(r"m_Size: \{x: ([\d.\-]+), y: ([\d.\-]+)\}", body)
    col = re.search(r"m_Color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}", body)
    so = re.search(r"m_SortingOrder: (\-?\d+)", body)
    print(f"[{name}] pos=({pm.group(1) if pm else '?'},{pm.group(2) if pm else '?'},{pm.group(3) if pm else '?'}) "
          f"size=({sz.group(1) if sz else '?'},{sz.group(2) if sz else '?'}) "
          f"sprite={sm.group(1) if sm else '-'} color=({col.group(1) if col else '?'},{col.group(2) if col else '?'},{col.group(3) if col else '?'},{col.group(4) if col else '?'}) sort={so.group(1) if so else '0'}")
