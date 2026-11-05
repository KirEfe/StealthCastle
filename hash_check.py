import hashlib

targets = {
    "mainlevbuild": "afce676399ec9654e8047ecb87630e3f",
    "decorative1": "9ee2d4d02a04b5e47a8c77766cd22fb1",
    "decorative2": "245c9dd8c2ec1c84a814f4f9618e7c8b",
}

ml_path = "Assets/RF Castle/Sliced/mainlevbuild.png"
dec_path = "Assets/RF Castle/Sliced/decorative.png"

formats = [
    "{p}::{n}",
    "{p}:{n}",
    "{n}",
    "Sprites/{n}",
    "{p}#{n}",
    "{p}|{n}",
    "{p}/{n}",
    "Assets/RF Castle/Sliced/{n}",
]

all_names = []
for i in range(957):
    all_names.append(f"mainlevbuild_{i}")
for i in range(324):
    all_names.append(f"decorative_{i}")

for fmt in formats:
    for name in all_names:
        p = ml_path if name.startswith("mainlevbuild") else dec_path
        h = hashlib.md5(fmt.format(p=p, n=name).encode()).hexdigest()
        for tname, tgt in targets.items():
            if h == tgt:
                print(f"MATCH {tname}={tgt} via fmt={fmt} name={name}")
