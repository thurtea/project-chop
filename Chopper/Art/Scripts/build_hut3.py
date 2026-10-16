import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_hall as hall
from build_hall import srgb, put, fill, stone_tone, POST, FRAME, BARGE, LOG, LOG_DARK, LOG_LIGHT, LOG_END, PLANK, PLANK_DARK, STONE, STONE_DARK, STONE_LIGHT
import build_world_kit as kit

# The three storey hut of hut-three-story-concept.jpg in the hall's 0.25 piece-metre cells (Hut scales the root by
# 1.8): basement under the terrace (pieces), ground floor hub iz 8-16, dormitory 16-24, workshop 24-32, a steep plank
# shingle gable (ridge along x) from iz 32 to 54, heavy timber posts, floor beams and braces over plank walls, two stone
# chimney stacks on the back wall, a ladder up the +x end, the porch at the door, all on a grassy rock outcrop.
# Blender y < 0 is the front (the door); build_hall.main flips y on export.
X0, X1 = -25, 24          # end walls (inclusive)
Y0, Y1 = -17, 16          # front and back walls (inclusive)
FLOORS = (8, 16, 24)
EAVE, RIDGE = 32, 54
SHINGLE = srgb(84, 56, 36)
SHINGLE_DARK = srgb(62, 40, 26)
SHINGLE_LIGHT = srgb(110, 76, 48)
GLASS = srgb(54, 66, 78)
GRASS = kit.GRASS_MID
GRASS_DARK = kit.GRASS_DARK
ROCK = srgb(112, 108, 102)
ROCK_DARK = srgb(84, 82, 78)


def h01(*v):
    return (kit.hsh(*v) % 1009) / 1009.0


def plank(a, iz, storey):
    n = h01(a // 2, storey, 3)
    if a % 2 == 0 and (a // 2) % 3 == 0:
        return PLANK_DARK
    return PLANK if n < 0.45 else (LOG if n < 0.8 else PLANK_DARK)


def door(ix, iz):
    return 0 <= ix <= 7 and 8 <= iz <= 15


def window(face, a, iz, storey):
    z0 = FLOORS[storey]
    if not (z0 + 3 <= iz <= z0 + 6):
        return False
    if face == "front":
        return (-17 <= a <= -13) or (13 <= a <= 17) or (storey > 0 and 1 <= a <= 5)
    if face == "back":
        return (-5 <= a <= -1) or (storey == 2 and 5 <= a <= 8)
    return storey > 0 and -6 <= a <= -2


def hut_cells():
    occ = {}
    # stone foundation course
    for (a0, a1, b0, b1) in ((X0 - 1, X1 + 2, Y0 - 1, Y0 + 1), (X0 - 1, X1 + 2, Y1, Y1 + 2), (X0 - 1, X0 + 1, Y0, Y1 + 1), (X1, X1 + 2, Y0, Y1 + 1)):
        fill(occ, a0, a1, b0, b1, 5, 8, stone_tone)
    # grassy rock outcrop around the base, stepping down outward (open at the porch)
    for ix in range(X0 - 7, X1 + 8):
        for iy in range(Y0 - 7, Y1 + 8):
            dx = max(X0 - 1 - ix, ix - X1 - 1, 0)
            dy = max(Y0 - 1 - iy, iy - Y1 - 1, 0)
            d = max(dx, dy)
            if d == 0 or d > 6:
                continue
            if iy < Y0 and -6 <= ix <= 13:
                continue
            n = h01(ix, iy)
            top = 8 - d + (1 if n > 0.6 else 0) - (1 if n < 0.15 else 0)
            for iz in range(max(0, top - 4), top):
                tone = ROCK if h01(ix // 2, iy // 2, iz) > 0.4 else ROCK_DARK
                put(occ, ix, iy, iz, tone)
            if top > 0:
                put(occ, ix, iy, top, GRASS if n > 0.3 else GRASS_DARK)
    # plank walls, three storeys
    for s, z0 in enumerate(FLOORS):
        for iz in range(z0, z0 + 8):
            for ix in range(X0, X1 + 1):
                if not (s == 0 and door(ix, iz)) and not window("front", ix, iz, s):
                    put(occ, ix, Y0, iz, plank(ix, iz, s))
                elif window("front", ix, iz, s):
                    put(occ, ix, Y0, iz, GLASS)
                if not window("back", ix, iz, s):
                    put(occ, ix, Y1, iz, plank(ix + 50, iz, s))
                else:
                    put(occ, ix, Y1, iz, GLASS)
            for iy in range(Y0, Y1 + 1):
                for ex in (X0, X1):
                    put(occ, ex, iy, iz, GLASS if window("end", iy, iz, s) else plank(iy + ex, iz, s))
        # window frames, proud by one cell
        for face, y, out in (("front", Y0, -1), ("back", Y1, 1)):
            for ix in range(X0, X1 + 1):
                for iz in range(z0 + 2, z0 + 8):
                    if not window(face, ix, iz, s) and (window(face, ix - 1, iz, s) or window(face, ix + 1, iz, s) or window(face, ix, iz - 1, s) or window(face, ix, iz + 1, s)):
                        put(occ, ix, y + out, iz, FRAME)
        for ex, out in ((X0, -1), (X1, 1)):
            for iy in range(Y0, Y1 + 1):
                for iz in range(z0 + 2, z0 + 8):
                    if not window("end", iy, iz, s) and (window("end", iy - 1, iz, s) or window("end", iy + 1, iz, s) or window("end", iy, iz - 1, s) or window("end", iy, iz + 1, s)):
                        put(occ, ex + out, iy, iz, FRAME)
        # floor beam band, proud
        for ix in range(X0 - 1, X1 + 2):
            put(occ, ix, Y0 - 1, z0, POST)
            put(occ, ix, Y1 + 1, z0, POST)
        for iy in range(Y0 - 1, Y1 + 2):
            put(occ, X0 - 1, iy, z0, POST)
            put(occ, X1 + 1, iy, z0, POST)
        # posts between panels and braces
        for px in (-21, -11, 9, 21):
            for iz in range(z0, z0 + 8):
                put(occ, px, Y0 - 1, iz, POST)
                put(occ, px, Y1 + 1, iz, POST)
        for py in (-9, 8):
            for iz in range(z0, z0 + 8):
                put(occ, X0 - 1, py, iz, POST)
                put(occ, X1 + 1, py, iz, POST)
        if s > 0:
            for k in range(0, 7):
                occ.setdefault((X0 - 1, -8 + k, z0 + 1 + k), BARGE)
                occ.setdefault((X1 + 1, 7 - k, z0 + 1 + k), BARGE)
                occ.setdefault((-20 + k, Y1 + 1, z0 + 1 + k), BARGE)
                occ.setdefault((20 - k, Y1 + 1, z0 + 1 + k), BARGE)
    # corner posts, full height, 2x2
    for cx, cy in ((X0 - 1, Y0 - 1), (X1, Y0 - 1), (X0 - 1, Y1), (X1, Y1)):
        fill(occ, cx, cx + 2, cy, cy + 2, 5, EAVE + 1, lambda i, j, k: POST)
    # top plate
    for ix in range(X0 - 1, X1 + 2):
        put(occ, ix, Y0 - 1, EAVE, FRAME)
        put(occ, ix, Y1 + 1, EAVE, FRAME)
    # steep gable roof, ridge along x, plank shingles in courses
    half = (Y1 - Y0) / 2.0 + 3.0
    cy = (Y0 + Y1) / 2.0
    for ix in range(X0 - 3, X1 + 4):
        for iy in range(int(cy - half) - 1, int(cy + half) + 2):
            t = max(0.0, 1.0 - abs(iy + 0.5 - cy - 0.5) / half)
            z = int(round(EAVE - 2 + (RIDGE - EAVE + 2) * t))
            for iz in range(z - 1, z + 1):
                course = (iz // 2 + (ix // 3) % 2) % 3
                tone = SHINGLE_DARK if course == 0 else (SHINGLE_LIGHT if h01(ix, iy, 9) > 0.85 else SHINGLE)
                put(occ, ix, iy, iz, tone)
            if ix in (X0 - 3, X1 + 3):
                put(occ, ix, iy, z + 1, BARGE)
    for ix in range(X0 - 3, X1 + 4):
        for iz in range(RIDGE, RIDGE + 2):
            put(occ, ix, int(cy), iz, LOG_DARK)
            put(occ, ix, int(cy) + 1, iz, LOG_DARK)
    # gable ends: planks up to the roof, a small window each
    for ex in (X0, X1):
        for iy in range(Y0, Y1 + 1):
            t = max(0.0, 1.0 - abs(iy + 0.5 - cy - 0.5) / half)
            ztop = int(round(EAVE - 2 + (RIDGE - EAVE + 2) * t)) - 2
            for iz in range(EAVE, ztop + 1):
                win = -2 <= iy <= 1 and EAVE + 5 <= iz <= EAVE + 9
                put(occ, ex, iy, iz, GLASS if win else plank(iy + 7, iz, 3))
            occ.setdefault((ex + (1 if ex == X1 else -1), iy, ztop + 1), BARGE)
    # two stone chimney stacks on the back wall (ground fireplace at x -3 m, dormitory hearth at x +3 m)
    for (a0, a1, z0, z1) in ((-14, -9, 5, RIDGE + 6), (10, 15, 13, RIDGE + 4)):
        fill(occ, a0, a1, Y1 + 1, Y1 + 5, z0, z1, stone_tone)
        fill(occ, a0 - 1, a1 + 1, Y1, Y1 + 6, z1 - 2, z1 - 1, lambda i, j, k: STONE_DARK)
        for ix in range(a0 + 1, a1 - 1):
            for iy in range(Y1 + 2, Y1 + 4):
                occ.pop((ix, iy, z1 - 1), None)
                put(occ, ix, iy, z1 - 2, srgb(30, 28, 26))
    # ladder up the +x end wall, front half, from the outcrop to the gable window
    for iz in range(6, EAVE + 8):
        put(occ, X1 + 3, -13, iz, LOG)
        put(occ, X1 + 3, -9, iz, LOG)
        if iz % 3 == 0:
            for iy in range(-12, -9):
                put(occ, X1 + 3, iy, iz, LOG_LIGHT)
    for iz in range(6, EAVE + 8, 8):
        put(occ, X1 + 2, -13, iz, FRAME)
        put(occ, X1 + 2, -9, iz, FRAME)
    # porch: deck, steps, posts and a small shed roof over the door
    fill(occ, -1, 9, -27, -17, 7, 8, lambda i, j, k: PLANK if j % 2 == 0 else PLANK_DARK)
    fill(occ, -1, 9, -27, -17, 6, 7, lambda i, j, k: PLANK_DARK)
    fill(occ, -1, 1, -27, -25, 8, 16, lambda i, j, k: POST)
    fill(occ, 7, 9, -27, -25, 8, 16, lambda i, j, k: POST)
    for iy in range(-28, -17):
        z = 16 + (iy + 28) // 3
        for ix in range(-2, 10):
            put(occ, ix, iy, z, SHINGLE if (iy + ix) % 4 else SHINGLE_DARK)
    fill(occ, 1, 7, -34, -29, 6, 7, lambda i, j, k: STONE if (i + j) % 2 == 0 else STONE_DARK)
    fill(occ, 1, 7, -29, -26, 7, 8, lambda i, j, k: PLANK if j % 2 == 0 else PLANK_DARK)
    fill(occ, -1, 1, -34, -26, 6, 8, stone_tone)
    fill(occ, 7, 9, -34, -26, 6, 8, stone_tone)
    return occ


if __name__ == "__main__":
    hall.cells = hut_cells
    hall.main()
