import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit

C = 0.08
REPORT = os.path.join(kit.RENDERS, "ground_gear_check_report.txt")

PLAID = kit.srgb(176, 36, 32)
PLAID_D = kit.srgb(36, 16, 14)
VEST = kit.srgb(118, 74, 42)
VEST_D = kit.srgb(86, 52, 30)
BELT = kit.srgb(62, 38, 22)


def box(out, x0, x1, y0, y1, z0, z1, colour):
    for ix in range(x0, x1):
        for iy in range(y0, y1):
            for iz in range(z0, z1):
                out[(ix, iy, iz)] = colour


def axe_cells():
    out = {}
    box(out, -1, 1, -7, 6, 0, 2, kit.LOG)
    box(out, -1, 1, -2, 1, 0, 2, kit.LOG_DARK)
    box(out, -1, 1, -7, -6, 0, 2, kit.LOG_END)
    box(out, -1, 1, 5, 7, 0, 2, kit.IRON)
    box(out, -4, -1, 4, 8, 0, 2, kit.IRON_LIGHT)
    box(out, -5, -4, 5, 8, 0, 2, kit.IRON_LIGHT)
    box(out, -6, -5, 5, 7, 0, 2, kit.IRON)
    box(out, 1, 4, 4, 8, 0, 2, kit.IRON_LIGHT)
    box(out, 4, 5, 5, 8, 0, 2, kit.IRON_LIGHT)
    box(out, 5, 6, 5, 7, 0, 2, kit.IRON)
    return out


def outfit_cells():
    out = {}
    for ix in range(-4, 4):
        for iy in range(-3, 3):
            for iz in range(0, 2):
                out[(ix, iy, iz)] = PLAID if ((ix // 2) + (iy // 2)) % 2 == 0 else PLAID_D
    for ix in range(-3, 3):
        for iy in range(-2, 2):
            for iz in range(2, 3):
                out[(ix, iy, iz)] = VEST_D if (ix + iy) % 3 == 0 else VEST
    box(out, -4, 4, -1, 1, 3, 4, BELT)
    return out


SPECS = [
    ("prop_ground_axe_voxel", axe_cells, {"h": (0.14, 0.20), "w": (0.92, 1.00), "d": (1.16, 1.24), "tris": 600}),
    ("prop_ground_outfit_voxel", outfit_cells, {"h": (0.30, 0.36), "w": (0.60, 0.68), "d": (0.44, 0.52), "tris": 400}),
]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    built = []
    for name, cells, rule in SPECS:
        obj = kit.build(name, cells(), mat, size=(C, C, C))
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
    kit.shoot(cam, "ground_gear_lineup", (x / 2, 0, 0.28), max(1.8, x * 1.2), yaw_deg=-32, pitch_deg=22)
    for obj, _ in built:
        obj.location.x = 0.0
        kit.export_one(obj)


if __name__ == "__main__":
    main()
