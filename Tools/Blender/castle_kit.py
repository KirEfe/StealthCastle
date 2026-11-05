"""
Набор деталей замка для StealthCastle: стилизованный low-poly, метры, пивот — центр основания.

Запуск (Blender в фоне; в Unity — StealthCastle → 3D → Art → Rebuild Models in Blender + Apply to Kit):
    blender --background --python Tools/Blender/castle_kit.py -- <папка FBX> [превью.png]

Каждая модель — отдельный FBX в <папка FBX>; текстуры — PNG в соседней папке Textures.
    Пропы:   Crate 1×1×1, Barrel Ø0.8×1.2, Chest 1×0.7×0.7 (крышка — отдельный объект с пивотом на петлях),
             Sack, Keg, Table 1.6×0.8; колонна Pillar 1.2×2.6×1.2 (высота стен).
    Логово:  HubMapTable, HubWorkbench, HubCounter, HubTrophyShelf, HubWallTorch (крепится к стене +Y), HubBedroll.
Материалы называются Gen_*: в Unity они подменяются ассетами Assets/Art/Generated/Materials/Gen_*.mat.
Развёртка кубическая, 1 повтор текстуры на 2 м — так же, как у процедурной кладки стен в Unity.
"""

import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector

WALL_HEIGHT = 2.6  # PieceAuthoring3D.WallHeight

PALETTE = {
    "Gen_Wood": (0.55, 0.37, 0.20),
    "Gen_WoodDark": (0.32, 0.20, 0.11),
    "Gen_Metal": (0.26, 0.27, 0.29),
    "Gen_Gold": (0.92, 0.70, 0.22),
    "Gen_Stone": (0.58, 0.56, 0.52),
    "Gen_StoneDark": (0.42, 0.40, 0.37),
    "Gen_Mortar": (0.25, 0.24, 0.22),
    "Gen_Cloth": (0.66, 0.57, 0.41),
    "Gen_Rope": (0.50, 0.40, 0.25),
    "Gen_Blanket": (0.35, 0.42, 0.32),
    "Gen_Red": (0.55, 0.16, 0.14),
    "Gen_Parchment": (0.86, 0.78, 0.58),
    "Gen_Wax": (0.92, 0.88, 0.75),
    "Gen_Flame": (1.00, 0.60, 0.20),
    "Gen_GemBlue": (0.25, 0.55, 1.00),
    "Gen_GemPurple": (0.65, 0.30, 0.95),
}


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        color = (*PALETTE[name], 1.0)
        mat.diffuse_color = color  # Workbench-превью
        if mat.node_tree is None:
            mat.use_nodes = True  # Blender 4.x; в 5.x дерево узлов есть сразу
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.85
    return mat


class Part:
    """Меш из примитивов: боксы, тела вращения, икосферы; у каждой грани — свой материал"""

    def __init__(self, name, seed=1):
        self.name = name
        self.bm = bmesh.new()
        self.materials = []
        self.rnd = random.Random(seed)

    def _mat_index(self, mat_name):
        if mat_name not in self.materials:
            self.materials.append(mat_name)
        return self.materials.index(mat_name)

    def _assign(self, verts, mat_name):
        index = self._mat_index(mat_name)
        for face in {f for v in verts for f in v.link_faces}:
            face.material_index = index

    def box(self, size, center, mat_name, rotation=(0.0, 0.0, 0.0)):
        verts = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        matrix = Matrix.Translation(Vector(center)) @ Euler(rotation).to_matrix().to_4x4() @ Matrix.Diagonal((*size, 1.0))
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self._assign(verts, mat_name)

    def gem(self, radius, center, mat_name, stretch=1.0):
        verts = bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=radius)["verts"]
        matrix = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((1.0, 1.0, stretch, 1.0))
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self._assign(verts, mat_name)

    def lathe(self, profile, mat_name, segments=12, center=(0.0, 0.0), cap_mat=None, closed_loop=False, jitter=0.0):
        """Тело вращения вокруг вертикали по профилю [(радиус, высота), ...] снизу вверх.
        closed_loop — профиль замкнут сам на себя (обруч), иначе закрываются торцы; jitter — неровность (ткань)"""
        rings = []
        for radius, z in profile:
            ring = []
            for s in range(segments):
                a = 2.0 * math.pi * s / segments
                r = radius * (1.0 + self.rnd.uniform(-jitter, jitter))
                ring.append(self.bm.verts.new((center[0] + r * math.cos(a), center[1] + r * math.sin(a), z)))
            rings.append(ring)

        index = self._mat_index(mat_name)
        pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed_loop else [])
        for lower, upper in pairs:
            for s in range(segments):
                n = (s + 1) % segments
                self.bm.faces.new((lower[s], lower[n], upper[n], upper[s])).material_index = index
        if not closed_loop:
            cap_index = self._mat_index(cap_mat or mat_name)
            self.bm.faces.new(list(reversed(rings[0]))).material_index = cap_index
            self.bm.faces.new(rings[-1]).material_index = cap_index

    def _box_uvs(self):
        """Кубическая развёртка по доминирующей оси нормали: 1 повтор текстуры на 2 м"""
        self.bm.normal_update()
        uv = self.bm.loops.layers.uv.new("UVMap")
        for face in self.bm.faces:
            n = face.normal
            axis = max(range(3), key=lambda i: abs(n[i]))
            for loop in face.loops:
                co = loop.vert.co
                u, v = (co.y, co.z) if axis == 0 else (co.x, co.z) if axis == 1 else (co.x, co.y)
                loop[uv].uv = (u * 0.5, v * 0.5)

    def build(self, bevel=0.015, location=(0.0, 0.0, 0.0)):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self._box_uvs()
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for mat_name in self.materials:
            mesh.materials.append(material(mat_name))
        obj.location = location

        if bevel > 0.0:
            mod = obj.modifiers.new("Bevel", "BEVEL")
            mod.width = bevel
            mod.segments = 1
            mod.limit_method = "ANGLE"
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier=mod.name)
        for poly in mesh.polygons:
            poly.use_smooth = False  # Плоское затенение — стилизованный low-poly
        return obj


def table_frame(p, width, depth, height, top_mat="Gen_Wood", leg_mat="Gen_WoodDark", leg=0.08, top=0.07):
    """Столешница + ножки + царги: общая основа столов"""
    p.box((width, depth, top), (0, 0, height - top / 2), top_mat)
    lx, ly = width / 2 - leg, depth / 2 - leg
    for x in (-lx, lx):
        for y in (-ly, ly):
            p.box((leg, leg, height - top), (x, y, (height - top) / 2), leg_mat)
    p.box((width - leg * 3, 0.04, 0.1), (0, -ly, height - top - 0.05), leg_mat)
    p.box((width - leg * 3, 0.04, 0.1), (0, ly, height - top - 0.05), leg_mat)
    p.box((width - leg * 3, 0.04, 0.05), (0, 0, 0.18), leg_mat)  # Перекладина


# --- Пропы ----------------------------------------------------------------------------

def crate():
    p = Part("Crate")
    p.box((0.9, 0.9, 0.9), (0, 0, 0.5), "Gen_Wood")
    h, t = 0.44, 0.12
    for x in (-h, h):
        for y in (-h, h):
            p.box((t, t, 1.0), (x, y, 0.5), "Gen_WoodDark")
    for z in (0.06, 0.94):
        for y in (-h, h):
            p.box((1.0, t, t), (0, y, z), "Gen_WoodDark")
        for x in (-h, h):
            p.box((t, 1.0, t), (x, 0, z), "Gen_WoodDark")
    d = math.radians(45)
    for y in (-0.465, 0.465):
        p.box((1.08, 0.05, 0.1), (0, y, 0.5), "Gen_WoodDark", rotation=(0, d if y > 0 else -d, 0))
    for x in (-0.465, 0.465):
        p.box((0.05, 1.08, 0.1), (x, 0, 0.5), "Gen_WoodDark", rotation=(d if x > 0 else -d, 0, 0))
    return [p.build()]


def barrel():
    p = Part("Barrel")
    profile = [(0.33, 0.0), (0.37, 0.12), (0.4, 0.35), (0.41, 0.6), (0.4, 0.85), (0.37, 1.08), (0.33, 1.2)]
    p.lathe(profile, "Gen_Wood", segments=14, cap_mat="Gen_WoodDark")
    for z, r in ((0.17, 0.385), (1.03, 0.385), (0.6, 0.415)):
        p.lathe([(r - 0.01, z - 0.035), (r + 0.02, z - 0.035), (r + 0.02, z + 0.035), (r - 0.01, z + 0.035)],
                "Gen_Metal", segments=14, closed_loop=True)
    return [p.build(bevel=0.0)]


def chest():
    w, d = 0.96, 0.66
    body = Part("Chest_Body")
    body.box((w, d, 0.42), (0, 0, 0.21), "Gen_WoodDark")
    body.box((w + 0.03, d + 0.03, 0.06), (0, 0, 0.03), "Gen_Metal")
    for x in (-0.33, 0.33):
        body.box((0.08, d + 0.02, 0.43), (x, 0, 0.215), "Gen_Metal")
    body.box((0.16, 0.05, 0.14), (0, -d / 2 - 0.01, 0.37), "Gen_Gold")

    # Крышка: пивот на петлях (задний верхний край), откидывается вокруг X
    hinge = Vector((0, d / 2, 0.42))
    lid = Part("Chest_Lid")
    for size_y, height, z in ((d, 0.1, 0.05), (d - 0.16, 0.08, 0.13), (d - 0.36, 0.05, 0.19)):
        lid.box((w, size_y, height), (0, -d / 2, z), "Gen_WoodDark")
    for x in (-0.33, 0.33):
        lid.box((0.08, d + 0.02, 0.11), (x, -d / 2, 0.05), "Gen_Metal")
        lid.box((0.08, d - 0.14, 0.09), (x, -d / 2, 0.13), "Gen_Metal")
    lid.box((0.1, 0.04, 0.08), (0, -d - 0.01, 0.04), "Gen_Gold")
    return [body.build(), lid.build(location=hinge)]


def pillar():
    p = Part("Pillar")
    h = WALL_HEIGHT
    p.box((1.2, 1.2, 0.18), (0, 0, 0.09), "Gen_StoneDark")
    p.box((1.0, 1.0, 0.1), (0, 0, 0.23), "Gen_Stone")
    r = 0.42 / math.cos(math.pi / 8)
    p.lathe([(r, 0.28), (r * 0.94, h - 0.22)], "Gen_Stone", segments=8)
    p.box((1.0, 1.0, 0.1), (0, 0, h - 0.17), "Gen_Stone")
    p.box((1.2, 1.2, 0.12), (0, 0, h - 0.06), "Gen_StoneDark")
    return [p.build()]


def sack():
    p = Part("Sack", seed=3)
    # Приземистый, с перетянутой горловиной и мятыми боками
    profile = [(0.3, 0.0), (0.39, 0.05), (0.42, 0.18), (0.4, 0.36), (0.32, 0.5), (0.17, 0.58), (0.1, 0.62), (0.16, 0.7), (0.06, 0.74)]
    p.lathe(profile, "Gen_Cloth", segments=9, jitter=0.09)
    p.lathe([(0.09, 0.6), (0.125, 0.6), (0.125, 0.64), (0.09, 0.64)], "Gen_Rope", segments=9, closed_loop=True)
    return [p.build(bevel=0.0)]


def keg():
    p = Part("Keg")
    profile = [(0.25, 0.0), (0.28, 0.1), (0.31, 0.37), (0.28, 0.64), (0.25, 0.74)]
    p.lathe(profile, "Gen_Wood", segments=12, cap_mat="Gen_WoodDark")
    for z in (0.12, 0.62):
        p.lathe([(0.28, z - 0.03), (0.3, z - 0.03), (0.3, z + 0.03), (0.28, z + 0.03)], "Gen_Metal", segments=12, closed_loop=True)
    p.box((0.06, 0.14, 0.06), (0, -0.34, 0.2), "Gen_Metal")  # Кран
    return [p.build(bevel=0.0)]


def table():
    p = Part("Table")
    table_frame(p, 1.6, 0.8, 0.8)
    p.lathe([(0.05, 0.8), (0.045, 0.92)], "Gen_Metal", segments=8, center=(0.45, 0.12))  # Кружка
    p.box((0.3, 0.2, 0.08), (-0.4, -0.1, 0.84), "Gen_Wax")                               # Сыр
    return [p.build()]


# --- Логово ---------------------------------------------------------------------------

def hub_map_table():
    p = Part("HubMapTable")
    table_frame(p, 2.6, 1.5, 0.9, leg=0.14, top=0.08)
    p.box((2.1, 1.05, 0.012), (0, 0, 0.906), "Gen_Parchment")
    for x, y, w in ((-0.4, 0.1, 0.7), (0.3, -0.2, 0.5), (0.1, 0.3, 0.3)):  # Чернила: стены на плане
        p.box((w, 0.02, 0.004), (x, y, 0.914), "Gen_WoodDark")
    p.lathe([(0.05, 0.9), (0.05, 1.04)], "Gen_Wax", segments=8, center=(1.05, 0.5))
    p.gem(0.04, (1.05, 0.5, 1.08), "Gen_Flame", stretch=1.6)
    for i, mat in enumerate(("Gen_Red", "Gen_Blanket", "Gen_WoodDark")):  # Стопка книг
        p.box((0.36, 0.26, 0.06), (-1.05, -0.5, 0.93 + i * 0.06), mat, rotation=(0, 0, 0.15 * i))
    return [p.build()]


def hub_workbench():
    p = Part("HubWorkbench")
    table_frame(p, 1.4, 3.2, 0.9, leg=0.12, top=0.1)
    p.box((1.25, 3.0, 0.05), (0, 0, 0.3), "Gen_WoodDark")      # Нижняя полка
    p.box((0.22, 0.3, 0.16), (0.45, 1.25, 0.98), "Gen_Metal")  # Тиски
    p.gem(0.16, (0.0, -0.9, 1.05), "Gen_GemBlue")
    p.gem(0.16, (0.1, 0.1, 1.05), "Gen_GemPurple")
    p.box((0.26, 0.26, 0.2), (-0.1, 1.0, 1.0), "Gen_Gold")
    p.box((0.05, 0.4, 0.04), (-0.4, -0.3, 0.92), "Gen_WoodDark")  # Молоток
    p.box((0.12, 0.06, 0.06), (-0.4, -0.52, 0.94), "Gen_Metal")
    return [p.build()]


def hub_counter():
    p = Part("HubCounter")
    p.box((1.3, 3.1, 0.92), (0, 0, 0.46), "Gen_WoodDark")
    p.box((1.4, 3.2, 0.08), (0, 0, 0.96), "Gen_Wood")
    for y in (-1.0, 0.0, 1.0):  # Филёнки со стороны комнаты (-X)
        p.box((0.04, 0.8, 0.6), (-0.66, y, 0.48), "Gen_Wood")
    p.box((0.6, 0.8, 0.45), (0.1, 0.9, 1.22), "Gen_WoodDark")    # Сундучок с деньгами
    p.box((0.62, 0.1, 0.47), (0.1, 0.65, 1.22), "Gen_Gold")
    p.box((0.62, 0.1, 0.47), (0.1, 1.15, 1.22), "Gen_Gold")
    for (x, y), h in (((0.1, -0.4), 0.08), ((-0.2, -0.9), 0.12), ((0.25, -1.1), 0.05)):  # Стопки монет
        p.lathe([(0.12, 1.0), (0.12, 1.0 + h)], "Gen_Gold", segments=10, center=(x, y))
    return [p.build()]


def hub_trophy_shelf():
    p = Part("HubTrophyShelf")
    w, d, h = 3.4, 0.6, 1.5
    for x in (-w / 2 + 0.04, w / 2 - 0.04):
        p.box((0.08, d, h), (x, 0, h / 2), "Gen_WoodDark")
    for z in (0.03, 0.75, h - 0.03):
        p.box((w, d, 0.06), (0, 0, z), "Gen_Wood")
    p.box((w, 0.04, h), (0, -d / 2 + 0.02, h / 2), "Gen_WoodDark")  # Задняя стенка (к стене, -Y)
    for x in (-1.0, 0.0, 1.0):  # Кубки наверху
        p.lathe([(0.1, h), (0.04, h + 0.04), (0.03, h + 0.12), (0.12, h + 0.2), (0.14, h + 0.32), (0.11, h + 0.34)],
                "Gen_Gold", segments=10, center=(x, 0))
    p.gem(0.1, (-0.8, 0.05, 0.88), "Gen_GemBlue")
    p.box((0.5, 0.3, 0.2), (0.6, 0.05, 0.88), "Gen_Gold")  # Слиток
    return [p.build()]


def hub_wall_torch():
    """Начало координат — точка крепления к стене; факел торчит в -Y"""
    p = Part("HubWallTorch")
    p.box((0.12, 0.04, 0.2), (0, -0.02, 0), "Gen_Metal")
    p.box((0.04, 0.22, 0.04), (0, -0.13, -0.04), "Gen_Metal")
    p.lathe([(0.035, -0.2), (0.04, 0.22)], "Gen_WoodDark", segments=6, center=(0, -0.24))
    p.lathe([(0.05, 0.12), (0.07, 0.22)], "Gen_Metal", segments=8, center=(0, -0.24))
    p.gem(0.08, (0, -0.24, 0.3), "Gen_Flame", stretch=1.5)
    return [p.build(bevel=0.0)]


def hub_bedroll():
    p = Part("HubBedroll")
    p.box((1.2, 2.1, 0.12), (0, 0, 0.06), "Gen_Blanket")
    p.box((1.24, 0.5, 0.18), (0, -0.55, 0.09), "Gen_Blanket")  # Откинутое одеяло
    p.box((0.8, 0.4, 0.14), (0, 0.78, 0.19), "Gen_Cloth")      # Подушка
    return [p.build(bevel=0.03)]


ASSETS = {
    "Crate": crate,
    "Barrel": barrel,
    "Chest": chest,
    "Pillar": pillar,
    "Sack": sack,
    "Keg": keg,
    "Table": table,
    "HubMapTable": hub_map_table,
    "HubWorkbench": hub_workbench,
    "HubCounter": hub_counter,
    "HubTrophyShelf": hub_trophy_shelf,
    "HubWallTorch": hub_wall_torch,
    "HubBedroll": hub_bedroll,
}


# --- Текстуры ---------------------------------------------------------------------------
# Бесшовные (решётка шума замкнута по модулю), серые: в Unity умножаются на цвет материала

def value_noise(size, cells, rng):
    grid = rng.random((cells, cells))
    t = np.arange(size) * cells / size
    i0 = np.floor(t).astype(int)
    f = t - i0
    f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    a, b = grid[i0][:, i0], grid[i0][:, i1]
    c, d = grid[i1][:, i0], grid[i1][:, i1]
    fx, fy = f[None, :], f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(size, cells, octaves, rng, persistence=0.5):
    total, amplitude, norm = np.zeros((size, size)), 1.0, 0.0
    for octave in range(octaves):
        total += amplitude * value_noise(size, cells * 2 ** octave, rng)
        norm += amplitude
        amplitude *= persistence
    return total / norm


def texture_stone(size, rng):
    v = 0.8 + 0.25 * (fbm(size, 4, 5, rng) - 0.5) * 2
    speckles = fbm(size, 64, 2, rng)
    v -= 0.1 * (speckles > 0.68)
    return np.clip(v, 0.55, 1.0)


def texture_wood(size, rng):
    y = np.arange(size)[:, None] / size
    warp = fbm(size, 4, 3, rng)
    grain = 0.5 + 0.5 * np.sin(2 * np.pi * (y * 12 + warp * 2.5))
    fine = fbm(size, 32, 2, rng)
    return np.clip(0.78 + 0.12 * grain + 0.1 * (fine - 0.5), 0.55, 1.0)


def texture_floor(size, rng, stones=2):
    """Плиты: stones×stones на текстуру (1 м при повторе на 2 м), тёмные швы, у каждой плиты свой тон"""
    cell = size // stones
    idx = np.arange(size)
    local = idx % cell
    edge = np.minimum(local, cell - 1 - local)
    dist = np.minimum(edge[:, None], edge[None, :]).astype(float)
    tones = 0.82 + 0.18 * rng.random((stones, stones))
    tone = tones[(idx // cell)[:, None], (idx // cell)[None, :]]
    v = tone * (0.9 + 0.2 * (fbm(size, 8, 4, rng) - 0.5))
    v *= 0.88 + 0.12 * np.clip(dist / (size * 0.04), 0, 1)  # Скос к краям плиты
    v[dist < size * 0.012] = 0.42                            # Шов
    return np.clip(v, 0.3, 1.0)


def texture_cloth(size, rng):
    x = np.arange(size)[None, :] / size
    y = np.arange(size)[:, None] / size
    weave = 0.5 + 0.25 * (np.sin(2 * np.pi * x * 96) + np.sin(2 * np.pi * y * 96))
    return np.clip(0.85 + 0.08 * (weave - 0.5) + 0.12 * (fbm(size, 8, 3, rng) - 0.5), 0.6, 1.0)


TEXTURES = {
    "T_Stone": texture_stone,
    "T_Wood": texture_wood,
    "T_Floor": texture_floor,
    "T_Cloth": texture_cloth,
}


def save_texture(name, values, folder):
    size = values.shape[0]
    image = bpy.data.images.new(name, size, size, alpha=False)
    rgba = np.ones((size, size, 4), dtype=np.float32)
    rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = values
    image.pixels.foreach_set(rgba.ravel())
    image.filepath_raw = os.path.join(folder, name + ".png")
    image.file_format = "PNG"
    image.save()


# --- Экспорт и превью ------------------------------------------------------------------

def export_fbx(objects, path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",  # 1 м в Blender = 1 юнит в Unity
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        bake_space_transform=True,  # Поворот осей Z-up → Y-up запекается в геометрию: в Unity узел без поворота
        add_leaf_bones=False,
        bake_anim=False,
    )


def render_preview(groups, path):
    """Детали в два ряда (пропы, логово), изометрическая камера, Workbench с цветами материалов"""
    columns, spacing = 7, 3.6
    for i, objects in enumerate(groups):
        offset = Vector(((i % columns) * spacing, -(i // columns) * spacing * 1.3, 0))
        for obj in objects:
            obj.location += offset

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 1800, 760
    scene.render.filepath = path

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = columns * spacing * 1.02
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    center = Vector(((columns - 1) * spacing / 2, -spacing * 0.65, 0.8))
    cam.location = center + Vector((3, -12, 9))
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = argv[0] if argv else os.path.join(os.getcwd(), "Assets", "Art", "Generated", "Models")
    preview = argv[1] if len(argv) > 1 else None
    tex_dir = os.path.join(os.path.dirname(os.path.normpath(out_dir)), "Textures")
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(tex_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name, make in TEXTURES.items():
        save_texture(name, make(512, np.random.default_rng(sum(map(ord, name)))), tex_dir)
        print(f"[castle_kit] texture {name}")

    groups = []
    for name, build in ASSETS.items():
        objects = build()
        export_fbx(objects, os.path.join(out_dir, name + ".fbx"))
        groups.append(objects)
        print(f"[castle_kit] {name}: {', '.join(o.name for o in objects)}")

    if preview:
        render_preview(groups, preview)
        print(f"[castle_kit] preview: {preview}")


main()
