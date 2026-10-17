import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit

# The mine portal from cave-chopper-concept-3.0: two dark round log posts and a lintel with braces, set in a
# stepped ring of dark rock, cyan crystal clusters at its feet. 0.25 m cells; x across, y depth (front at y<0), z up.
ROCK = kit.srgb(92, 104, 118)
ROCK_DARK = kit.srgb(66, 76, 90)
ROCK_LIGHT = kit.srgb(126, 138, 152)
LOG = kit.srgb(84, 56, 34)
LOG_DARK = kit.srgb(58, 38, 24)
LOG_END = kit.srgb(132, 96, 60)
CYAN = kit.srgb(70, 224, 236)
CYAN_HOT = kit.srgb(170, 252, 255)
MOSS = kit.srgb(84, 118, 60)
MOSS_D = kit.srgb(60, 92, 46)
OPEN = 6          # opening |x| < 6 cells (3 m)
TOP = 12          # opening height 3 m


def h01(*v):
    return (kit.hsh(*v) % 1009) / 1009.0


def portal_cells():
    out = {}
    # stepped rock surround: a broad arch of dark grey rock that steps down in 2-cell terraces to each side,
    # big enough to hide the generated stone behind it
    for ix in range(-30, 30):
        cx = abs(ix + 0.5)
        step = int(cx // 3)
        crown = int(26 * max(0.0, 1 - (cx / 30.0) ** 2) ** 0.5 / 2) * 2 + 2 + int(h01(ix // 2, 7) * 7) - 3 - (4 if h01(ix // 4, 3) > 0.8 else 0)
        if crown <= 0:
            continue
        for iz in range(0, crown):
            if cx < OPEN + 3 and iz < TOP + 4:
                continue
            n = h01(ix, iz)
            edge = iz >= crown - 1 or cx > 28.5 - n
            depth = 4 + (2 if not edge else 0) + (1 if n > 0.6 else 0)
            if cx < OPEN + 7:
                depth = 9
            for iy in range(0, depth):
                if iy == depth - 1 and edge and n < 0.3:
                    continue
                if n > 0.86 and iy == 0:
                    tone = ROCK_LIGHT
                elif h01(ix // 3, iz // 3, 5) < 0.4 or n < 0.15:
                    tone = ROCK_DARK
                else:
                    tone = ROCK
                out[(ix, iy, iz)] = tone
            if iz == crown - 1 and h01(ix, 41) < 0.7:
                out[(ix, 0, iz + 1)] = MOSS if h01(ix, 42) < 0.6 else MOSS_D
                if h01(ix, 43) < 0.5:
                    out[(ix, 1, iz + 1)] = MOSS_D
            elif cx > OPEN + 3 and iz % 6 == 5 and h01(ix, iz, 44) < 0.45:
                out[(ix, depth, iz)] = MOSS if h01(ix, iz, 45) < 0.5 else MOSS_D
    # posts: round logs, 3x3 cells with corners shaved
    for s in (-1, 1):
        x0 = OPEN if s > 0 else -OPEN - 3
        for ix in range(x0, x0 + 3):
            for iy in range(6, 9):
                if (ix - x0 - 1) ** 2 + (iy - 7) ** 2 > 1:
                    continue
                for iz in range(0, TOP + 1):
                    out[(ix, iy, iz)] = LOG_DARK if (iz + ix) % 5 == 0 else LOG
    # lintel log across, ends proud
    for ix in range(-OPEN - 5, OPEN + 5):
        for iy in range(6, 9):
            for iz in range(TOP + 1, TOP + 4):
                if (iy - 7) ** 2 + (iz - TOP - 2) ** 2 > 1:
                    continue
                end = ix in (-OPEN - 5, OPEN + 4)
                out[(ix, iy, iz)] = LOG_END if end else (LOG_DARK if ix % 6 == 0 else LOG)
    # braces
    for s in (-1, 1):
        for k in range(0, 4):
            ix = (OPEN - 1 - k) if s > 0 else (-OPEN + k)
            out[(ix, 7, TOP - 3 + k)] = LOG_DARK
            out[(ix, 7, TOP - 4 + k)] = LOG
    # chunky crystal clusters at the feet: tapered 3x3 spires leaning out, proud of the rock
    for s in (-1, 1):
        for c, (bx, h, by) in enumerate(((OPEN + 5, 9, 9), (OPEN + 8, 6, 10), (OPEN + 11, 7, 9), (OPEN + 3, 4, 10), (OPEN + 14, 4, 10))):
            for iz in range(0, h):
                w = 1 if iz < h * 0.6 else 0
                lean = iz // 3
                for dx in range(-w, w + 1):
                    for dy in range(-w, w + 1):
                        ix = s * (bx + lean) + dx
                        out[(ix, by + dy, iz)] = CYAN_HOT if (iz >= h - 2 or (dx == 0 and dy == -w)) else CYAN
    return out


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    obj = kit.build("prop_mine_portal_voxel", portal_cells(), mat)
    b = kit.bounds(obj)
    print("built prop_mine_portal_voxel tris %d size %.2f x %.2f x %.2f" % (kit.tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))
    print("bounds", [round(v, 2) for v in b])
    kit.export_one(obj)
    print("SUMMARY ALL PASS")


if __name__ == "__main__":
    main()
