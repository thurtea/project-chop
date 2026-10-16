import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit

# Furniture and stores for the three storey hut (hut-three-story-concept.jpg), 1/16 m cells, real size.
G = 0.0625
WOOD = kit.srgb(128, 84, 48)
WOOD_DARK = kit.srgb(92, 58, 32)
WOOD_LIGHT = kit.srgb(160, 112, 66)
PAPER = kit.srgb(228, 214, 178)
PAPER_DARK = kit.srgb(196, 178, 140)
PIN = kit.srgb(170, 50, 40)
CLOTH = kit.srgb(196, 170, 120)
CLOTH_DARK = kit.srgb(160, 134, 92)
RED = kit.srgb(150, 62, 48)
GREEN = kit.srgb(92, 122, 70)
WHITE = kit.srgb(226, 222, 210)
IRON = kit.IRON
IRON_LIGHT = kit.IRON_LIGHT
ROPE = kit.ROPE


def box(o, x0, x1, y0, y1, z0, z1, c):
    for x in range(x0, x1):
        for y in range(y0, y1):
            for z in range(z0, z1):
                o[(x, y, z)] = c(x, y, z) if callable(c) else c


def grain(x, y, z):
    return WOOD_DARK if (x + 2 * y) % 7 == 0 else (WOOD_LIGHT if (x * 3 + y) % 11 == 0 else WOOD)


def table():
    o = {}
    box(o, -13, 13, -7, 7, 11, 13, lambda x, y, z: WOOD_DARK if y % 4 == 0 else grain(x, y, z))
    for x in (-12, 10):
        for y in (-6, 4):
            box(o, x, x + 2, y, y + 2, 0, 11, WOOD_DARK)
    box(o, -11, 11, -1, 1, 3, 4, WOOD_DARK)
    return o


def stool():
    o = {}
    for x in range(-4, 4):
        for y in range(-4, 4):
            if (x + 0.5) ** 2 + (y + 0.5) ** 2 <= 16:
                o[(x, y, 7)] = grain(x, y, 7)
    for x, y in ((-3, -3), (2, -3), (0, 2)):
        box(o, x, x + 1, y, y + 1, 0, 7, WOOD_DARK)
    return o


def notice_board():
    o = {}
    box(o, -10, 10, 0, 1, 16, 31, lambda x, y, z: WOOD_DARK if x in (-10, 9) or z in (16, 30) else WOOD)
    for (x0, z0, w, h) in ((-8, 23, 5, 6), (-2, 25, 4, 4), (3, 22, 5, 7), (-7, 18, 4, 4), (0, 18, 6, 5)):
        box(o, x0, x0 + w, -1, 0, z0, z0 + h, lambda x, y, z: PAPER_DARK if (z - z0) % 2 == 1 and x > x0 else PAPER)
        o[(x0 + w // 2, -2, z0 + h - 1)] = PIN
    return o


def bunk():
    o = {}
    for x in (-8, 7):
        for y in (-17, 16):
            box(o, x, x + 1, y, y + 1, 0, 29, WOOD_DARK)
    for z, top in ((5, RED), (19, GREEN)):
        box(o, -8, 8, -17, 17, z, z + 1, WOOD)
        box(o, -7, 7, -16, 16, z + 1, z + 3, top)
        box(o, -7, 7, 12, 16, z + 3, z + 4, WHITE)
        for x in range(-8, 8):
            o[(x, -17, z + 2)] = WOOD_DARK
            o[(x, 16, z + 2)] = WOOD_DARK
    for z in range(8, 19, 3):
        for x in range(4, 7):
            o[(x, -18, z)] = WOOD_LIGHT
    return o


def tool_wall():
    o = {}
    box(o, -13, 13, 0, 1, 14, 31, lambda x, y, z: WOOD_DARK if x % 4 == 0 else WOOD)
    for x in range(-11, -6):
        o[(x, -1, 26)] = IRON_LIGHT
    for x, top in ((-5, 28), (-2, 27), (1, 28), (4, 26)):
        for z in range(20, top):
            o[(x, -1, z)] = WOOD_LIGHT
        o[(x, -1, top)] = IRON
        o[(x - 1, -1, top)] = IRON
    for x in range(7, 12):
        for z in range(18, 28):
            if x - 7 <= (z - 18) // 2:
                o[(x, -1, z)] = IRON_LIGHT
    o[(9, -1, 28)] = WOOD
    return o


def vise():
    o = {}
    box(o, -3, 3, -2, 2, 0, 2, IRON)
    box(o, -3, -1, -2, 2, 2, 6, IRON)
    box(o, 1, 3, -2, 2, 2, 6, IRON)
    for y in range(-5, 5):
        o[(0, y, 4)] = IRON_LIGHT
    return o


def spinning_wheel():
    o = {}
    box(o, -10, 10, -2, 2, 4, 6, WOOD)
    for x, y in ((-9, -3), (-9, 2), (8, 0)):
        box(o, x, x + 1, y, y + 1, 0, 4, WOOD_DARK)
    cx, cz, r = 3, 15, 7
    for x in range(cx - r - 1, cx + r + 2):
        for z in range(cz - r - 1, cz + r + 2):
            d = math.hypot(x - cx, z - cz)
            if r - 1 <= d <= r + 0.5 or (d < r and (x == cx or z == cz)):
                o[(x, 0, z)] = WOOD_DARK if d > r - 1 else WOOD_LIGHT
    box(o, cx, cx + 1, -1, 2, 6, cz, WOOD)
    box(o, -8, -7, -1, 1, 6, 16, WOOD)
    box(o, -9, -6, -1, 1, 16, 18, CLOTH)
    return o


def sack():
    o = {}
    for x in range(-4, 4):
        for y in range(-4, 4):
            for z in range(0, 10):
                rr = 4.2 - max(0, z - 6) * 0.9
                if (x + 0.5) ** 2 + (y + 0.5) ** 2 <= rr * rr:
                    o[(x, y, z)] = CLOTH_DARK if (x + z) % 5 == 0 else CLOTH
    for z in (10, 11):
        for x in range(-1, 1):
            for y in range(-1, 1):
                o[(x, y, z)] = CLOTH
    for x in range(-2, 2):
        o[(x, -2, 9)] = ROPE
    return o


SPECS = [
    ("prop_hut_table_voxel", table), ("prop_hut_stool_voxel", stool), ("prop_hut_notice_voxel", notice_board),
    ("prop_hut_bunk_voxel", bunk), ("prop_hut_toolwall_voxel", tool_wall), ("prop_hut_vise_voxel", vise),
    ("prop_hut_wheel_voxel", spinning_wheel), ("prop_hut_sack_voxel", sack),
]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    for name, fn in SPECS:
        obj = kit.build(name, fn(), mat, size=(G, G, G))
        b = kit.bounds(obj)
        print("built %s tris %d size %.2f x %.2f x %.2f" % (name, kit.tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))
        kit.export_one(obj)
    print("SUMMARY ALL PASS")


if __name__ == "__main__":
    main()
