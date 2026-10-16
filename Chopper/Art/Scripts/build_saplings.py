import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit

C = 0.08
REPORT = os.path.join(kit.RENDERS, "sapling_check_report.txt")

SOIL = kit.srgb(104, 70, 40)
SOIL_D = kit.srgb(78, 52, 30)

CROWNS = [
    ("neutral", kit.srgb(58, 112, 44), kit.srgb(42, 88, 34), kit.srgb(84, 140, 58)),
    ("water", kit.srgb(44, 120, 128), kit.srgb(30, 92, 104), kit.srgb(92, 170, 176)),
    ("fire", kit.srgb(196, 84, 30), kit.srgb(150, 52, 22), kit.srgb(236, 150, 52)),
    ("earth", kit.srgb(150, 132, 48), kit.srgb(112, 96, 34), kit.srgb(184, 166, 74)),
    ("wind", kit.srgb(150, 196, 150), kit.srgb(118, 164, 124), kit.srgb(206, 228, 204)),
]


def box(out, x0, x1, y0, y1, z0, z1, colour):
    for ix in range(x0, x1):
        for iy in range(y0, y1):
            for iz in range(z0, z1):
                out[(ix, iy, iz)] = colour


def sapling_cells(index):
    _, leaf, leaf_d, leaf_l = CROWNS[index]
    out = {}
    for ix in range(-3, 3):
        for iy in range(-3, 3):
            if abs(ix + 0.5) + abs(iy + 0.5) > 4.5:
                continue
            out[(ix, iy, 0)] = SOIL_D if (ix + iy) % 3 == 0 else SOIL
    box(out, -1, 1, -1, 1, 1, 9, kit.LOG)
    box(out, -1, 0, -1, 1, 3, 5, kit.LOG_DARK)
    tiers = [(6, 4), (9, 3), (12, 2), (15, 1)]
    for z0, half in tiers:
        for ix in range(-half - 1, half + 1):
            for iy in range(-half - 1, half + 1):
                if abs(ix + 0.5) + abs(iy + 0.5) > half + 1.2:
                    continue
                for iz in range(z0, z0 + 2):
                    top = iz == z0 + 1
                    edge = abs(ix + 0.5) + abs(iy + 0.5) > half
                    colour = leaf_l if top else (leaf_d if edge or (ix + iy + iz) % 4 == 0 else leaf)
                    out[(ix, iy, iz)] = colour
    box(out, -1, 1, -1, 1, 17, 18, CROWNS[index][3])
    return out


SPECS = [("prop_sapling_%d_voxel" % i, i, {"h": (1.38, 1.50), "w": (0.75, 0.85), "d": (0.75, 0.85), "tris": 6000}) for i in range(5)]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    built = []
    for name, index, rule in SPECS:
        obj = kit.soften(kit.build(name, sapling_cells(index), mat, size=(C, C, C)), C * 0.2)
        built.append((obj, rule))
        b = kit.bounds(obj)
        print("built %s tris %d size %.3f x %.3f x %.3f" % (
            name, kit.tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))
    kit.REPORT = REPORT
    kit.check(built, [])
    cam = kit.setup_render()
    x = 0.0
    for obj, _ in built:
        b = kit.bounds(obj)
        obj.location.x = x - b[0]
        x += (b[1] - b[0]) + 0.35
    kit.shoot(cam, "sapling_lineup", (x / 2, 0, 0.6), max(2.4, x * 1.2), yaw_deg=-32, pitch_deg=18)
    for obj, _ in built:
        obj.location.x = 0.0
        kit.export_one(obj)


if __name__ == "__main__":
    main()