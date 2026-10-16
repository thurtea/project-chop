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
WOOD = kit.srgb(150, 96, 52)
WOOD_D = kit.srgb(118, 72, 38)
RIM = kit.srgb(58, 58, 62)
BOSS = kit.srgb(168, 174, 185)
PINE = kit.srgb(48, 110, 42)
IRON_EDGE = kit.srgb(196, 200, 206)


def box(out, x0, x1, y0, y1, z0, z1, colour):
    for ix in range(x0, x1):
        for iy in range(y0, y1):
            for iz in range(z0, z1):
                out[(ix, iy, iz)] = colour


def axe_cells():
    out = {}
    box(out, -1, 1, -8, 6, 0, 2, kit.LOG)
    box(out, -1, 1, -8, -4, 0, 2, BELT)
    for iy in (-7, -5):
        box(out, -1, 1, iy, iy + 1, 0, 2, kit.LOG_END)
    box(out, -1, 1, 6, 8, 0, 2, kit.LOG_DARK)
    box(out, -1, 1, 4, 7, 0, 2, kit.IRON)
    for side in (-1, 1):
        for dx in range(1, 6):
            half = 1 + dx // 2
            for iy in range(5 - half, 6 + half):
                ix = dx if side > 0 else -dx - 1
                edge = dx == 5 or iy in (5 - half, 5 + half)
                colour = IRON_EDGE if dx == 5 else (kit.IRON if edge else kit.IRON_LIGHT)
                box(out, ix, ix + 1, iy, iy + 1, 0, 2, colour)
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


def shield_cells():
    out = {}
    for iy in range(-6, 7):
        half = 4 if iy >= 1 else max(1, (iy + 8) // 2)
        for ix in range(-half, half + 1):
            rim = abs(ix) == half or iy == -6 or iy == 6
            colour = RIM if rim else (WOOD if (ix // 2) % 2 == 0 else WOOD_D)
            box(out, ix, ix + 1, iy, iy + 1, 0, 2, colour)
    box(out, 0, 1, -3, 5, 1, 2, WOOD_D)
    box(out, -2, 0, 3, 5, 1, 2, BOSS)
    box(out, 1, 3, 3, 5, 1, 2, BOSS)
    box(out, -1, 2, -1, 2, 1, 2, RIM)
    box(out, 0, 1, 0, 1, 1, 2, BOSS)
    for px in (-2, 2):
        box(out, px, px + 1, -3, 1, 1, 2, PINE)
        box(out, px - 1 if px < 0 else px, px + 1 if px < 0 else px + 2, -2, -1, 1, 2, PINE)
    return out

SPECS = [
    ("prop_ground_axe_voxel", axe_cells, {"h": (0.14, 0.20), "w": (0.92, 1.00), "d": (1.24, 1.36), "tris": 3000}),
    ("prop_ground_outfit_voxel", outfit_cells, {"h": (0.30, 0.36), "w": (0.60, 0.68), "d": (0.44, 0.52), "tris": 1600}),
    ("prop_ground_shield_voxel", shield_cells, {"h": (0.14, 0.20), "w": (0.64, 0.80), "d": (0.96, 1.12), "tris": 3600}),
]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    built = []
    for name, cells, rule in SPECS:
        obj = kit.soften(kit.build(name, cells(), mat, size=(C, C, C)), C * 0.2)
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
