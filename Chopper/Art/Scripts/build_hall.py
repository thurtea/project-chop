"""The hub hall as one mesh, matched to chopper-3D-concept-2.0.jpg.

The hammer pieces stay in the hut for floors, walls and stairs (the player still collides with
them). This mesh is only the look: stone foundation, round logs, a closed shingle roof, gable
crosses, and a porch roof over the door.

Piece-metres, Z up, same export as the pieces. Under Hut's Cabin (scale 1.5) at identity:
Blender X = Unity X, Blender Z = Unity Y, Blender -Y = Unity +Z (the front, where the door is).

Run:  blender -b -P build_hall.py
Out:  Art/Export/hall_voxel.fbx, Art/Renders/hall_check_report.txt
"""
import math
import os

import bpy
from mathutils import Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
EXPORT = os.path.join(ART, "Export")
REPORT = os.path.join(ART, "Renders", "hall_check_report.txt")

W, D = 6.0, 4.0
MAIN, EAVE, RIDGE = 2.0, 4.05, 6.75
OVER = 0.45
LOG_R = 0.115
SIDES = 8


def srgb(r, g, b):
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b))


LOG = srgb(156, 102, 56)
LOG_DARK = srgb(118, 74, 40)
LOG_LIGHT = srgb(176, 118, 66)
LOG_END = srgb(206, 160, 104)
STONE = srgb(128, 124, 118)
STONE_DARK = srgb(96, 94, 90)
PLANK = srgb(134, 92, 56)
PLANK_DARK = srgb(112, 74, 42)
THATCH = srgb(206, 156, 80)
THATCH_DARK = srgb(168, 122, 58)
THATCH_LIGHT = srgb(228, 186, 108)
FRAME = srgb(72, 44, 26)
POST = srgb(96, 58, 32)
CHINK = srgb(62, 40, 24)


class Mesh:
    def __init__(self):
        self.verts = []
        self.faces = []
        self.colors = []

    def add(self, verts, faces, color):
        base = len(self.verts)
        self.verts.extend(verts)
        for f in faces:
            self.faces.append(tuple(base + i for i in f))
            self.colors.append(color)

    def prism(self, a, b, radius, color, end=LOG_END, sides=SIDES):
        a, b = (a[0], a[1], a[2]), (b[0], b[1], b[2])
        axis = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
        length = math.sqrt(sum(c * c for c in axis)) or 1.0
        w = tuple(c / length for c in axis)
        hint = (0.0, 0.0, 1.0) if abs(w[2]) < 0.9 else (0.0, 1.0, 0.0)
        v = (hint[0] - w[0] * sum(hint[i] * w[i] for i in range(3)),
             hint[1] - w[1] * sum(hint[i] * w[i] for i in range(3)),
             hint[2] - w[2] * sum(hint[i] * w[i] for i in range(3)))
        vl = math.sqrt(sum(c * c for c in v)) or 1.0
        v = tuple(c / vl for c in v)
        u = (v[1] * w[2] - v[2] * w[1], v[2] * w[0] - v[0] * w[2], v[0] * w[1] - v[1] * w[0])
        ring0, ring1 = [], []
        for k in range(sides):
            ang = (k + 0.5) * 2 * math.pi / sides
            off = (u[0] * math.cos(ang) + v[0] * math.sin(ang),
                   u[1] * math.cos(ang) + v[1] * math.sin(ang),
                   u[2] * math.cos(ang) + v[2] * math.sin(ang))
            ring0.append(tuple(a[i] + radius * off[i] for i in range(3)))
            ring1.append(tuple(b[i] + radius * off[i] for i in range(3)))
        verts = ring0 + ring1
        faces = []
        shade = LOG_DARK if color == LOG else color
        side_colors = []
        for k in range(sides):
            n = (k + 1) % sides
            faces.append((k, n, sides + n, sides + k))
            # the underside of a log is darker, so the courses separate
            side_colors.append(shade if math.sin((k + 0.5) * 2 * math.pi / sides) < -0.2 else color)
        faces.append(tuple(range(sides - 1, -1, -1)))
        faces.append(tuple(range(sides, 2 * sides)))
        base = len(self.verts)
        self.verts.extend(verts)
        for i, f in enumerate(faces):
            self.faces.append(tuple(base + j for j in f))
            self.colors.append(end if i >= sides else side_colors[i])

    def box(self, x0, x1, y0, y1, z0, z1, color):
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        verts = [(x, y, z) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
        # 0-3 bottom (z0), 4-7 top. Within a layer: (x0,y0) (x1,y0) (x0,y1) (x1,y1) — wait
        # loop y then x: index = z*4 + y*2 + x
        # Outward faces: bottom, top, -Y, +Y, -X, +X.
        faces = [
            (0, 2, 3, 1), (4, 5, 7, 6),
            (0, 1, 5, 4), (2, 6, 7, 3),
            (0, 4, 6, 2), (1, 3, 7, 5),
        ]
        self.add(verts, faces, color)

    def quad_up(self, a, b, c, d, color, back=None):
        """One quad, flipped when the normal points down. back adds the other side."""
        ab = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
        ad = (d[0] - a[0], d[1] - a[1], d[2] - a[2])
        nz = ab[0] * ad[1] - ab[1] * ad[0]
        if nz < 0:
            self.add([a, b, c, d], [(0, 3, 2, 1)], color)
            if back is not None:
                self.add([a, b, c, d], [(0, 1, 2, 3)], back)
        else:
            self.add([a, b, c, d], [(0, 1, 2, 3)], color)
            if back is not None:
                self.add([a, b, c, d], [(0, 3, 2, 1)], back)


def shade_log(row):
    return (LOG_DARK, LOG, LOG_LIGHT, LOG, LOG_DARK)[row % 5]


BOARD = (THATCH, THATCH_LIGHT, srgb(190, 140, 70), srgb(218, 170, 96))
BUTT = srgb(122, 82, 40)         # the shadowed lower edge of each shingle course
BARGE = srgb(92, 56, 30)         # barge boards, posts and crossed gable ends (concept 2.0)
GABLE = (srgb(126, 84, 46), srgb(140, 94, 52), srgb(112, 74, 40))


def _sheet(m, a, b, c, d, color):
    """Both windings of one quad, so a grazing view cannot cull a hole."""
    m.add([a, b, c, d], [(0, 1, 2, 3), (0, 3, 2, 1)], color)


def gable_roof(m, along, c_across, half, l0, l1, eave, ridge, rows=9, board=0.55, thick=0.07,
               cross_ends=(True, True), cross_ext=0.95, beam=0.11):
    """Two roof slopes. along = 'x' or 'y' is the ridge direction.

    Each course is a slab parallel to the slope and runs up under the next course, so the rows
    overlap in plan instead of meeting at a corner. A continuous ceiling under the courses is
    what the loft looks up at. A ridge plug closes the knife edge where the two slopes meet.
    Thick barge boards run up both rakes and carry on past the peak into a crossed X.
    """
    def pt(t, u, side, lift=0.0):
        a = c_across + side * half * (1.0 - t)
        z = eave + (ridge - eave) * t + lift
        return (u, a, z) if along == "x" else (a, u, z)

    def slab(side, t0, t1, u0, u1, lift0, lift1, color, butt=None):
        """Solid slice of one slope. butt colours the downhill end."""
        def p(t, u, lift):
            return pt(t, u, side, lift)
        _sheet(m, p(t0, u0, lift1), p(t0, u1, lift1), p(t1, u1, lift1), p(t1, u0, lift1), color)
        _sheet(m, p(t0, u0, lift0), p(t0, u1, lift0), p(t1, u1, lift0), p(t1, u0, lift0), color)
        _sheet(m, p(t0, u0, lift0), p(t0, u1, lift0), p(t0, u1, lift1), p(t0, u0, lift1), butt or color)
        _sheet(m, p(t1, u0, lift0), p(t1, u1, lift0), p(t1, u1, lift1), p(t1, u0, lift1), color)
        _sheet(m, p(t0, u0, lift0), p(t1, u0, lift0), p(t1, u0, lift1), p(t0, u0, lift1), color)
        _sheet(m, p(t0, u1, lift0), p(t1, u1, lift0), p(t1, u1, lift1), p(t0, u1, lift1), color)

    length = l1 - l0
    lap = 0.72
    step = 0.016
    for side in (-1, 1):
        # Ceiling: one slab for the whole slope, past the ridge so the two sides cross at the peak.
        slab(side, 0.0, 1.06, l0, l1, -0.02, 0.02, THATCH_DARK)
        for row in range(rows):
            t0 = row / rows
            t1 = (row + 1) / rows
            t_hi = 1.0 if row == rows - 1 else min(1.0, t1 + lap / rows)
            lift1 = 0.045 + thick + row * step
            lift0 = lift1 - thick
            n = max(1, int(round(length / board)))
            stagger = 0.5 if row % 2 else 0.0
            cuts = {l0 + (k + stagger) * length / n for k in range(n + 1)}
            edges = sorted({l0, l1} | {c for c in cuts if l0 < c < l1})
            gap = 0.02
            for k, (u0, u1) in enumerate(zip(edges, edges[1:])):
                color = BOARD[(k * 2 + row * 3 + (k * k) % 3) % len(BOARD)]
                a0 = u0 + (gap if u0 > l0 else 0.0)
                a1 = u1 - (gap if u1 < l1 else 0.0)
                if a1 - a0 < 0.05:
                    continue
                slab(side, t0, t_hi, a0, a1, lift0, lift1, color, butt=BUTT)
    # Plug the peak. The ceiling slabs cross above it; this fills the loft side of the knife edge.
    plug = 0.22
    if along == "x":
        m.box(l0 - 0.02, l1 + 0.02, c_across - plug, c_across + plug, ridge - 0.2, ridge + 0.05, THATCH_DARK)
    else:
        m.box(c_across - plug, c_across + plug, l0 - 0.02, l1 + 0.02, ridge - 0.2, ridge + 0.05, THATCH_DARK)
    # ridge cap on top of the courses
    a, b = pt(1.0, l0 - 0.05, 1, thick + 0.08), pt(1.0, l1 + 0.05, 1, thick + 0.08)
    m.prism(a, b, beam * 0.9, BARGE, end=BARGE, sides=4)
    # barge boards up both rakes; past the peak they cross into an X
    for u, show in ((l0 - 0.06, cross_ends[0]), (l1 + 0.06, cross_ends[1])):
        for side in (-1, 1):
            s0 = pt(0.0, u, side, thick + 0.06)
            s1 = pt(1.0, u, side, thick + 0.06)
            d = tuple(s1[i] - s0[i] for i in range(3))
            dl = math.sqrt(sum(c * c for c in d)) or 1.0
            ext = cross_ext if show else beam
            e = tuple(s1[i] + d[i] / dl * ext for i in range(3))
            m.prism(s0, e, beam, BARGE, end=LOG_END if show else BARGE, sides=4)


def gable_planks(m, along, c_across, half, at, eave, ridge, width=0.38, depth=0.08, z_from=None):
    """Vertical boards filling a gable triangle (in the plane along = at).

    A solid triangle sits in the boards so the peak and the joints between boards cannot open
    onto the sky. The peak runs into the roof instead of stopping short of it.
    """
    z0 = eave - 0.05 if z_from is None else z_from
    peak_z = ridge + 0.03
    span = 0.06

    def corner(across, z, sign):
        if along == "x":
            return (at + sign, across, z)
        return (across, at + sign, z)

    d = 0.02
    front = [
        corner(c_across - half, z0, d), corner(c_across + half, z0, d),
        corner(c_across + span, peak_z, d), corner(c_across - span, peak_z, d),
    ]
    back = [
        corner(c_across - half, z0, -d), corner(c_across + half, z0, -d),
        corner(c_across + span, peak_z, -d), corner(c_across - span, peak_z, -d),
    ]
    _sheet(m, front[0], front[1], front[2], front[3], GABLE[1])
    _sheet(m, back[0], back[1], back[2], back[3], GABLE[1])
    _sheet(m, front[0], back[0], back[1], front[1], GABLE[0])
    _sheet(m, front[0], front[3], back[3], back[0], GABLE[0])
    _sheet(m, front[1], back[1], back[2], front[2], GABLE[0])
    _sheet(m, front[3], front[2], back[2], back[3], GABLE[0])

    n = max(1, int(math.ceil((2 * half) / width - 1e-9)))
    for k in range(n):
        a0 = c_across - half + k * (2 * half) / n
        a1 = c_across - half + (k + 1) * (2 * half) / n
        mid = abs((a0 + a1) / 2 - c_across)
        top = eave + (ridge - eave) * max(0.0, 1.0 - mid / half) + 0.02
        if top <= z0 + 0.02:
            continue
        col = GABLE[k % len(GABLE)]
        if along == "x":
            m.box(at - depth / 2, at + depth / 2, a0, a1, z0, top, col)
        else:
            m.box(a0, a1, at - depth / 2, at + depth / 2, z0, top, col)


def build():
    m = Mesh()
    # Stone foundation: two courses of blocks in two greys, joints offset per course; a ring, so the
    # basement stays open.
    for course, z0 in ((0, 1.58), (1, 1.88)):
        z1 = z0 + 0.30
        off = 0.0 if course == 0 else 0.4
        k = 0
        x = -W - 0.22 - off
        while x < W + 0.22:
            xa, xb = max(x, -W - 0.22), min(x + 0.8, W + 0.22)
            col = STONE if (k + course) % 2 else STONE_DARK
            other = STONE_DARK if col == STONE else STONE
            m.box(xa, xb, -D - 0.22, -D + 0.16, z0, z1, col)
            m.box(xa, xb, D - 0.16, D + 0.22, z0, z1, other)
            x += 0.8
            k += 1
        y = -D - off
        k = 0
        while y < D:
            ya, yb = max(y, -D), min(y + 0.8, D)
            col = STONE if (k + course) % 2 else STONE_DARK
            other = STONE_DARK if col == STONE else STONE
            m.box(-W - 0.22, -W + 0.16, ya, yb, z0, z1, col)
            m.box(W - 0.16, W + 0.22, ya, yb, z0, z1, other)
            y += 0.8
            k += 1

    zs = [2.28 + i * 0.23 for i in range(8)]
    door = (0.15, 1.85, 2.12, 3.92)
    front_windows = [(-4.15, -3.15, 2.85, 3.55), (3.3, 4.3, 2.85, 3.55)]
    back_windows = [(-2.2, -1.2, 2.85, 3.55), (2.4, 3.4, 2.85, 3.55)]

    def blocked(x, z, holes):
        for x0, x1, z0, z1 in holes:
            if x0 - 0.05 < x < x1 + 0.05 and z0 < z < z1:
                return True
        return False

    # Long walls: continuous logs, split only where a hole cuts them.
    for i, z in enumerate(zs):
        color = shade_log(i)
        for y, holes in ((-D, [door] + front_windows), (D, back_windows)):
            x = -W
            while x < W - 0.05:
                if blocked(x + 0.05, z, holes):
                    x += 0.15
                    continue
                x1 = x
                while x1 < W and not blocked(x1 + 0.1, z, holes):
                    x1 += 0.15
                m.prism((x, y, z), (min(x1, W), y, z), LOG_R, color)
                # Chinking inside the course seals the slit between the flat log faces (W11-14 flicker).
                m.box(x + 0.02, min(x1, W) - 0.02, y - 0.05, y + 0.05, z - LOG_R, z + LOG_R, CHINK)
                x = x1 + 0.15
    # End walls: logs along Y, one window each.
    ew = (-1.1, -0.2, 2.85, 3.55)

    def in_end_window(y, z):
        return ew[2] < z < ew[3] and ew[0] - 0.05 < y < ew[1] + 0.05

    for i, z in enumerate(zs):
        color = shade_log(i + 1)
        for x in (-W, W):
            y = -D + 0.2
            while y < D - 0.25:
                if in_end_window(y, z):
                    y += 0.15
                    continue
                y1 = y
                while y1 < D - 0.2 and not in_end_window(y1 + 0.1, z):
                    y1 += 0.15
                m.prism((x, y, z), (x, min(y1, D - 0.2), z), LOG_R, color)
                m.box(x - 0.05, x + 0.05, y + 0.02, min(y1, D - 0.2) - 0.02, z - LOG_R, z + LOG_R, CHINK)
                y = y1 + 0.15

    # Heavy square posts split the walls into bays (concept 2.0), standing on the stones, up to a top
    # plate that closes the wall to the underside of the roof (the old walls stopped 0.3 short of it).
    roof_at_wall = EAVE + (RIDGE - EAVE) * (1.0 - D / (D + OVER))
    post_top = roof_at_wall + 0.02
    ph = 0.16
    front_posts = (-W, -4.6, -2.6, -0.17, 2.12, 4.75, W)
    back_posts = (-W, -4.0, -2.6, -0.4, 1.95, 3.85, W)
    for y, xs in ((-D, front_posts), (D, back_posts)):
        for x in xs:
            m.box(x - ph, x + ph, y - ph - 0.04, y + ph + 0.04, 2.18, post_top, BARGE)
        m.box(-W - 0.2, W + 0.2, y - 0.17, y + 0.17, zs[-1] + 0.08, post_top, POST)
    for x in (-W, W):
        for y in (-2.0, 1.6):
            m.box(x - ph - 0.04, x + ph + 0.04, y - ph, y + ph, 2.18, EAVE, BARGE)
        m.box(x - 0.17, x + 0.17, -D - 0.2, D + 0.2, zs[-1] + 0.08, EAVE + 0.02, POST)

    # Frames, with a lattice cross in each window.
    def frame(x0, x1, y, z0, z1, lattice=True):
        m.box(x0 - 0.07, x0, y - 0.09, y + 0.09, z0, z1, FRAME)
        m.box(x1, x1 + 0.07, y - 0.09, y + 0.09, z0, z1, FRAME)
        m.box(x0 - 0.07, x1 + 0.07, y - 0.09, y + 0.09, z1, z1 + 0.09, FRAME)
        m.box(x0 - 0.07, x1 + 0.07, y - 0.09, y + 0.09, z0 - 0.07, z0, FRAME)
        if lattice:
            xm, zm = (x0 + x1) / 2, (z0 + z1) / 2
            m.box(xm - 0.025, xm + 0.025, y - 0.03, y + 0.03, z0, z1, FRAME)
            m.box(x0, x1, y - 0.03, y + 0.03, zm - 0.025, zm + 0.025, FRAME)

    frame(door[0], door[1], -D, door[2], door[3], lattice=False)
    for x0, x1, z0, z1 in front_windows:
        frame(x0, x1, -D, z0, z1)
    for x0, x1, z0, z1 in back_windows:
        frame(x0, x1, D, z0, z1)
    for x in (-W, W):
        y0, y1, z0, z1 = ew
        m.box(x - 0.09, x + 0.09, y0 - 0.07, y0, z0, z1, FRAME)
        m.box(x - 0.09, x + 0.09, y1, y1 + 0.07, z0, z1, FRAME)
        m.box(x - 0.09, x + 0.09, y0 - 0.07, y1 + 0.07, z1, z1 + 0.09, FRAME)
        m.box(x - 0.09, x + 0.09, y0 - 0.07, y1 + 0.07, z0 - 0.07, z0, FRAME)
        m.box(x - 0.03, x + 0.03, (y0 + y1) / 2 - 0.025, (y0 + y1) / 2 + 0.025, z0, z1, FRAME)
        m.box(x - 0.03, x + 0.03, y0, y1, (z0 + z1) / 2 - 0.025, (z0 + z1) / 2 + 0.025, FRAME)

    # Main roof: plank courses, barge boards, big crossed ends at both gables; plank gables.
    eave_y = D + OVER
    gable_roof(m, "x", 0.0, eave_y, -W - 0.4, W + 0.4, EAVE, RIDGE, rows=11, board=0.6, thick=0.08,
               cross_ext=1.1, beam=0.13)
    for x in (-W + 0.02, W - 0.02):
        gable_planks(m, "x", 0.0, eave_y, x, EAVE, RIDGE, z_from=EAVE - 0.08)

    # Dormer on the ridge, left of centre like the concept, its gable to the front, with its own crosses.
    dx0, dx1 = -3.2, -1.8
    d_eave, d_ridge = RIDGE + 0.35, RIDGE + 1.0
    m.box(dx0 + 0.08, dx1 - 0.08, -0.85, 0.85, RIDGE - 0.6, d_eave, GABLE[0])
    for x in (dx0 + 0.08, dx1 - 0.08):
        m.box(x - 0.09, x + 0.09, -0.9, -0.72, RIDGE - 0.6, d_eave, BARGE)
        m.box(x - 0.09, x + 0.09, 0.72, 0.9, RIDGE - 0.6, d_eave, BARGE)
    gable_roof(m, "y", (dx0 + dx1) / 2, (dx1 - dx0) / 2 + 0.2, -1.05, 1.05, d_eave, d_ridge, rows=4,
               board=0.45, thick=0.06, cross_ext=0.55, beam=0.08)
    for y in (-0.86, 0.86):
        gable_planks(m, "y", (dx0 + dx1) / 2, (dx1 - dx0) / 2 + 0.2, y, d_eave, d_ridge, width=0.24, depth=0.06)

    # Porch: deck in front of the door (the pieces collide with it), square posts on a beam, and a
    # cross-gable whose ridge runs out from the main roof, with plank gable and crossed ends at the front.
    deck_y0, deck_y1 = -D - 2.15, -D
    m.box(-0.15, 2.15, deck_y0, deck_y1, MAIN - 0.12, MAIN, PLANK)
    m.box(-0.15, 2.15, deck_y0, deck_y1, MAIN - 0.18, MAIN - 0.12, PLANK_DARK)
    for k in range(6):
        y = deck_y0 + 0.3 + k * 0.33
        m.box(-0.15, 2.15, y, y + 0.03, MAIN - 0.005, MAIN + 0.005, PLANK_DARK)
    pc, phalf = 1.0, 1.55
    p_eave, p_ridge = MAIN + 1.95, MAIN + 3.05
    for x in (pc - phalf + 0.2, pc + phalf - 0.2):
        m.box(x - 0.13, x + 0.13, deck_y0 + 0.05, deck_y0 + 0.31, MAIN, p_eave + 0.05, BARGE)
        m.box(x - 0.11, x + 0.11, deck_y0 + 0.05, -D + 0.1, p_eave - 0.12, p_eave + 0.06, POST)
    m.box(pc - phalf + 0.07, pc + phalf - 0.07, deck_y0 + 0.05, deck_y0 + 0.31, p_eave - 0.12,
          p_eave + 0.06, POST)
    gable_roof(m, "y", pc, phalf + 0.15, deck_y0 - 0.35, -D + 0.9, p_eave, p_ridge, rows=6, board=0.5,
               thick=0.07, cross_ends=(True, False), cross_ext=0.8, beam=0.1)
    gable_planks(m, "y", pc, phalf + 0.15, deck_y0 + 0.18, p_eave + 0.06, p_ridge, width=0.3, depth=0.06)
    # four treads from the terrace (1.67) up to the deck
    for i in range(4):
        z0 = 1.67 + i * 0.08
        y1 = deck_y0 - 0.12 - (3 - i) * 0.42
        m.box(0.15, 1.85, y1 - 0.4, y1, z0, z0 + 0.08, PLANK if i % 2 == 0 else PLANK_DARK)
    return m


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    built = build()
    # Blender +Y lands as Unity +Z with this project's axis bake. The front (door, porch) is +Y.
    built.verts = [(x, -y, z) for x, y, z in built.verts]
    built.faces = [tuple(reversed(f)) for f in built.faces]
    mesh = bpy.data.meshes.new("hall_voxel")
    mesh.from_pydata(built.verts, [], built.faces)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for poly, color in zip(mesh.polygons, built.colors):
        poly.use_smooth = False
        for li in poly.loop_indices:
            attr.data[li].color = (color[0], color[1], color[2], 1.0)
    mesh.color_attributes.active_color = attr
    obj = bpy.data.objects.new("hall_voxel", mesh)
    bpy.context.scene.collection.objects.link(obj)
    mat = bpy.data.materials.new("M_World")
    try:
        mat.use_nodes = True
        bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        vc = mat.node_tree.nodes.new("ShaderNodeVertexColor")
        vc.layer_name = "Col"
        mat.node_tree.links.new(vc.outputs["Color"], bsdf.inputs["Base Color"])
    except Exception:
        pass
    mesh.materials.append(mat)
    mesh.calc_loop_triangles()
    xs = [v[0] for v in built.verts]
    ys = [v[1] for v in built.verts]
    zs = [v[2] for v in built.verts]
    tris = len(mesh.loop_triangles)
    os.makedirs(EXPORT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(EXPORT, "hall_voxel.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        embed_textures=False,
        bake_anim=False,
        path_mode="STRIP",
        add_leaf_bones=False,
    )
    # Front is +Y after the flip. Rays from the loft must hit the roof, including along the ridge
    # toward the gable peak (the old courses let those through).
    dg = bpy.context.evaluated_depsgraph_get()
    missed = 0
    tried = 0
    for ix in range(-6, 7):
        for iy in range(-4, 5):
            x, y = ix * 0.85, iy * 0.7
            if abs(y) > 3.2 or abs(x) > 5.6:
                continue
            tried += 1
            if not obj.ray_cast(Vector((x, y, 4.55)), Vector((0, 0, 1)), distance=3.5, depsgraph=dg)[0]:
                missed += 1
    for z, y in ((6.55, 0.0), (6.4, 0.35), (6.15, 0.9), (5.85, 1.6), (6.5, -0.4)):
        tried += 1
        if not obj.ray_cast(Vector((0.0, y, z)), Vector((1, 0, 0)), distance=8, depsgraph=dg)[0]:
            missed += 1
    roof_ok = missed == 0
    # Front is -Y before the flip; after it the roof ridge is the highest point, the foundation the lowest.
    ok = tris > 1000 and max(zs) > 6.5 and min(zs) > 1.5 and max(ys) > 5.5 and max(xs) > 6.0 and roof_ok
    lines = [
        "Hub hall",
        "=" * 60,
        "  tris %d" % tris,
        "  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)),
        "  [%s] loft rays hit the roof (%d/%d)" % ("PASS" if roof_ok else "FAIL", tried - missed, tried),
        "SUMMARY %s" % ("ALL PASS" if ok else "FAILED"),
    ]
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.filepath = os.path.join(ART, "Renders", "hall_three_quarter.png")
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 38
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = (14, 16, 9)
    target = Vector((0, -1, 3.6))
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (0.9, 0.2, 0.6)
    bpy.context.scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("W")
    scene.world = world
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)
    # Loft, looking along the roof toward the gable peak, under a bright sky.
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is None:
        bg = world.node_tree.nodes.new("ShaderNodeBackground")
        world.node_tree.links.new(bg.outputs["Background"], world.node_tree.nodes["World Output"].inputs["Surface"])
    bg.inputs[0].default_value = (0.42, 0.72, 0.98, 1.0)
    bg.inputs[1].default_value = 1.4
    cam.location = (-2.2, 0.35, 5.15)
    target = Vector((4.2, 0.05, 6.45))
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 28
    scene.render.filepath = os.path.join(ART, "Renders", "hut_preview_up.png")
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)


if __name__ == "__main__":
    main()
