import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit

REPORT = os.path.join(kit.RENDERS, "yard_check_report.txt")
WATER = kit.srgb(60, 118, 170)


def pond_cells():
    out = {}
    for ix in range(-18, 18):
        for iy in range(-13, 13):
            r = ((ix + 0.5) ** 2 / 324.0 + (iy + 0.5) ** 2 / 169.0) ** 0.5
            if r > 1.0:
                continue
            n = (kit.hsh(ix, iy) % 1009) / 1009.0
            if r > 0.78:
                top = 2 + (1 if n > 0.55 else 0)
                for iz in range(0, top):
                    tone = kit.STONE_LIGHT if n > 0.8 else (kit.STONE_DARK if n < 0.3 else kit.STONE)
                    out[(ix, iy, iz)] = tone
            elif r > 0.6:
                out[(ix, iy, 0)] = kit.STONE_DARK
                if n > 0.5:
                    out[(ix, iy, 1)] = kit.STONE
            else:
                out[(ix, iy, 0)] = kit.srgb(54, 70, 66)
    return out


def water_cells():
    out = {}
    for ix in range(-15, 15):
        for iy in range(-10, 10):
            r = ((ix + 0.5) ** 2 / 225.0 + (iy + 0.5) ** 2 / 100.0) ** 0.5
            if r <= 1.0:
                out[(ix, iy, 0)] = WATER
    return out


def board_cells():
    out = {}
    for ix in range(0, 32):
        board = ix // 4
        seam = ix % 4 == 3
        for iy in range(0, 32):
            cut = (iy + board * 11) % 32 == 0
            if seam or cut:
                out[(ix, iy, 0)] = kit.FRAME
                continue
            n = (kit.hsh(board, iy // 8) % 1009) / 1009.0
            tone = kit.PLANK if n < 0.4 else (kit.LOG if n < 0.75 else kit.LOG_DARK)
            out[(ix, iy, 0)] = tone
            if (ix + iy * 3 + board) % 9 == 0:
                out[(ix, iy, 0)] = kit.LOG_DARK
    return out


SPECS = [
    ("prop_yard_pond_voxel", pond_cells, (0.25, 0.25, 0.22)),
    ("prop_yard_water_voxel", water_cells, (0.25, 0.25, 0.04)),
    ("prop_floor_boards_voxel", board_cells, (0.0625, 0.0625, 0.06)),
]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    built = []
    for name, fn, size in SPECS:
        obj = kit.build(name, fn(), mat, size=size)
        b = kit.bounds(obj)
        print("built %s tris %d size %.3f x %.3f x %.3f" % (name, kit.tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))
        built.append(obj)
    for obj in built:
        kit.export_one(obj)
    print("SUMMARY ALL PASS")


if __name__ == "__main__":
    main()