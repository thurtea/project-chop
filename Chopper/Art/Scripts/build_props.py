"""Build Chopper's props: chopper_axe and chopper_shield (gray box, Week 1).

Run:  blender -b -P build_props.py
Out:  Art/Blender/chopper_props.blend

chopper_axe:    double-headed axe (concept 1.7). Origin = right-hand grip point,
                haft along local +Z, blades along local +/-X, thickness along Y.
chopper_shield: heater/kite shield (concept 1.5). Origin = inner arm-strap centre
                (the forearm axis), face pointing -Y, top at +Z, board back 10 cm in front of it.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chopper_lib as L  # noqa: E402
from chopper_lib import PAL, X, Y, Z  # noqa: E402

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ART, "Blender", "chopper_props.blend")


# ------------------------------------------------------------------ helpers
def plate(mb, outline, y_edge, y_flat, inset, color):
    """Extrude a 2D outline (x, z) into a plate with bevelled (thinner) edges.

    Edge thickness is 2*y_edge, flat centre thickness 2*y_flat.
    """
    bm = mb.bm
    front = [bm.verts.new((x, -y_edge, z)) for x, z in outline]
    back = [bm.verts.new((x, y_edge, z)) for x, z in outline]
    n = len(outline)
    faces = []
    ff = bm.faces.new(front)
    fb = bm.faces.new(list(reversed(back)))
    for j in range(n):
        k = (j + 1) % n
        faces.append(bm.faces.new((front[j], back[j], back[k], front[k])))
    bmesh.ops.recalc_face_normals(bm, faces=faces + [ff, fb])
    for f in (ff, fb):
        res = bmesh.ops.inset_region(bm, faces=[f], thickness=inset, depth=y_flat - y_edge,
                                     use_even_offset=True)
        faces += res["faces"]
    faces += [ff, fb]
    mb.paint(faces, color)
    return faces


def bezier2(p0, p1, p2, n):
    out = []
    for i in range(n + 1):
        t = i / n
        a = (1 - t) ** 2
        b = 2 * (1 - t) * t
        c = t * t
        out.append((a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]))
    return out


# ------------------------------------------------------------------ axe
def blade_outline(s, zc):
    """Crescent blade on side s (+1 = +X). Notched cutting edge."""
    ax = 0.03         # attachment x (inside the head block)
    ah = 0.05         # attachment half height
    tip = (0.175, 0.17)
    # deep concave throat: runs outward first, then curls up into a hooked tip
    upper = bezier2((ax, ah), (0.17, 0.035), tip, 4)
    lower = bezier2((tip[0], -tip[1]), (0.17, -0.035), (ax, -ah), 4)
    # outer convex cutting edge from top tip to bottom tip, with stepped notches
    arc = []
    n = 14
    for i in range(1, n):
        th = math.pi / 2 - math.pi * i / n
        x = tip[0] + 0.075 * math.cos(th)
        z = tip[1] * math.sin(th) * 1.04
        if i % 3 == 2:            # notch: pull the edge in by 1.2 cm
            x -= 0.012
        arc.append((x, z))
    pts = [(ax, -ah)] + upper + arc + lower[:-1]
    return [(s * x, z + zc) for x, z in pts]


def build_axe(mat):
    mb = L.MeshBuilder("chopper_axe")
    # haft: wood below the collar with three raised grip wraps, steel-banded above
    lower = [(-0.14, 0.019), (-0.125, 0.021)]
    for z0 in (-0.095, -0.04, 0.015, 0.07):
        lower += [(z0 - 0.004, 0.021), (z0, 0.024), (z0 + 0.03, 0.024), (z0 + 0.034, 0.021)]
    lower += [(0.145, 0.021)]
    rings = [(Vector((0, 0, z)), X, Y, hw, hw, 0.006) for z, hw in lower]
    mb.tube(rings, cs=1, cap0=True, cap1=False, color=PAL["wood"],
            color_fn=lambda p: PAL["wrap"] if abs(math.hypot(p.x, p.y)) > 0.0225 else None)
    upper = [(0.14, 0.020), (0.30, 0.020), (0.47, 0.020)]
    rings = [(Vector((0, 0, z)), X, Y, hw, hw, 0.006) for z, hw in upper]
    mb.tube(rings, cs=1, cap0=False, cap1=False, color=PAL["wood_dark"])
    # iron end cap, steel collar, upper strap band
    mb.box((0, 0, -0.135), (0.055, 0.055, 0.05), 0.008, color=PAL["steel_dark"])
    mb.box((0, 0, 0.16), (0.054, 0.054, 0.045), 0.008, color=PAL["steel_dark"])
    mb.box((0, 0, 0.32), (0.049, 0.049, 0.16), 0.006, color=PAL["steel_dark"])
    # head block with a short top spike
    zc = 0.52
    mb.box((0, 0, zc), (0.075, 0.06, 0.175), 0.01, color=PAL["steel_dark"])
    mb.box((0, 0, 0.625), (0.04, 0.04, 0.04), 0.008, color=PAL["steel_dark"])
    # crescent blades
    for s in (1, -1):
        plate(mb, blade_outline(s, zc), 0.004, 0.012, 0.008, PAL["steel"])
    return mb.finish(mat, recalc=True)


# ------------------------------------------------------------------ bear-head axe (concept 1.5)
def block(mb, lo, hi, color):
    """Plain axis-aligned box, 12 tris (outward winding)."""
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    c = Vector(((x0 + x1) / 2, (y0 + y1) / 2, z0))
    rings = [(c, X, Y, (x1 - x0) / 2, (y1 - y0) / 2, 0.0),
             (c + Z * (z1 - z0), X, Y, (x1 - x0) / 2, (y1 - y0) / 2, 0.0)]
    return mb.tube(rings, cs=0, color=color)


def bear_blade_outline():
    """Single bearded blade on +X, (x, z). Large head (concept 2.0): from the eye out to a
    high spike, down a long cutting edge to a deep beard, then back to the eye. Kept inside
    the haft length so Z stays the long axis."""
    return [(0.058, 0.615), (0.150, 0.650), (0.250, 0.690), (0.340, 0.728),   # top edge -> spike
            (0.372, 0.655), (0.398, 0.555), (0.400, 0.455), (0.378, 0.365),   # cutting edge
            (0.348, 0.305), (0.265, 0.275), (0.185, 0.295),                     # beard tip
            (0.120, 0.355), (0.078, 0.445), (0.058, 0.535)]                    # beard underside


# warm carved wood (concept 1.5 haft is lighter and warmer than the double axe's)
BEAR_WOOD = (0.72, 0.42, 0.19)
BEAR_CARVE = (0.42, 0.22, 0.09)
BEAR_HEAD = (0.80, 0.50, 0.23)
EDGE_IDX = range(3, 10)   # outline edges j -> j+1 that form the cutting edge (lighter steel)


def build_axe_bear(mat):
    """Bear-head axe, concept 1.5. Same frame as chopper_axe: origin = right-hand grip point,
    haft along local +Z (toward the head), blade on local +X (the side chopper_axe's +X blade
    faces), poll on -X, thickness along Y."""
    mb = L.MeshBuilder("chopper_axe_bear")
    bm = mb.bm
    # haft: octagonal, warm wood, short dark wrap and iron cap at the butt
    # (the grip stays as thin as chopper_axe's inside the fist, z -0.15..0.15)
    haft = [(-0.170, 0.019), (-0.160, 0.023), (-0.100, 0.023), (-0.094, 0.020),
            (0.150, 0.021), (0.200, 0.022), (0.230, 0.033), (0.640, 0.033)]  # 0.200 stays inside the grip-axis probe
    # above the grip the section is a regular octagon (bevel = hw * (2 - sqrt 2)) so the
    # carved spiral stands proud by the same amount all the way round
    rings = [(Vector((0, 0, z)), X, Y, hw, hw, 0.007 if z < 0.2 else hw * (2 - math.sqrt(2)))
             for z, hw in haft]
    mb.tube(rings, cs=1, cap0=True, cap1=True, color=BEAR_WOOD,
            color_fn=lambda p: PAL["leather_dark"] if p.z < -0.095 else None)
    # carved spiral: short separate wraps (one connected helix packs overlapping UVs).
    # Proud of the octagon, clear of the grip (z < 0.15).
    pieces, seg, turns, z0, z1, r = 6, 3, 2.5, 0.22, 0.52, 0.040
    n = pieces * seg
    pts, nrms = [], []
    for i in range(n + 1):
        a = 2 * math.pi * turns * i / n
        pts.append(Vector((r * math.cos(a), r * math.sin(a), z0 + (z1 - z0) * i / n)))
        nrms.append(Vector((math.cos(a), math.sin(a), 0.0)))
    for p in range(pieces):
        srings = []
        i0 = max(0, p * seg - (1 if p else 0))
        for i in range(i0, p * seg + seg + 1):
            t = (pts[min(i + 1, n)] - pts[max(i - 1, 0)]).normalized()
            v = nrms[i]
            v = (v - t * v.dot(t)).normalized()
            u = v.cross(t)
            srings.append((pts[i], u, v, 0.009, 0.0055, 0.0))
        mb.tube(srings, cs=0, cap0=False, cap1=False, color=BEAR_CARVE)
    # iron eye around the haft, squared poll on the back (-X), top cap under the bear
    mb.box((0, 0, 0.56), (0.092, 0.080, 0.13), 0.006, color=PAL["steel_dark"])
    block(mb, (-0.088, -0.030, 0.512), (-0.044, 0.030, 0.608), PAL["steel_dark"])
    block(mb, (-0.098, -0.034, 0.505), (-0.086, 0.034, 0.615), PAL["steel"])   # poll face
    # blade: dark iron face, lighter steel cutting edge (larger single blade)
    outline = bear_blade_outline()
    faces = plate(mb, outline, 0.005, 0.014, 0.016, PAL["steel_dark"])
    segs = [(Vector(outline[j]), Vector(outline[j + 1])) for j in EDGE_IDX]

    def near_edge(f):
        c = f.calc_center_median()
        q = Vector((c.x, c.z))
        for a, b in segs:
            ab = b - a
            t = max(0.0, min(1.0, (q - a).dot(ab) / ab.length_squared))
            if (q - (a + ab * t)).length < 0.016:
                return True
        return False
    mb.paint([f for f in faces if near_edge(f)], PAL["steel"])
    # carved bear head. The ears are the top of the prop and must land on z=0.742
    # (check_rig's imported bounds).
    W, WD = BEAR_HEAD, BEAR_CARVE
    mb.box((0.01, 0, 0.655), (0.168, 0.116, 0.108), 0.010, color=W)              # skull
    mb.box((0.098, 0, 0.642), (0.078, 0.086, 0.062), 0.008, color=W)             # snout
    block(mb, (0.125, -0.016, 0.644), (0.140, 0.016, 0.666), WD)                 # nose
    block(mb, (0.048, -0.046, 0.688), (0.070, -0.016, 0.708), WD)                # eyes
    block(mb, (0.048, 0.016, 0.688), (0.070, 0.046, 0.708), WD)
    block(mb, (0.036, -0.052, 0.704), (0.088, 0.052, 0.722), W)                  # brow shelf
    for s in (-1, 1):
        block(mb, (-0.012, s * 0.048 - 0.012, 0.655), (0.026, s * 0.048 + 0.012, 0.742), W)  # ears
    block(mb, (-0.072, -0.040, 0.600), (-0.054, 0.040, 0.700), WD)              # mane ridge at the back
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return mb.finish(mat)


# ------------------------------------------------------------------ shield
SH_W = 0.65
SH_TOP = 0.40
SH_BOT = -0.55
SH_SIDE = SH_TOP - 0.95 * 0.45      # where the straight sides end
SH_CHAMFER = 0.055
SH_CURVE = 0.04 / (SH_W / 2) ** 2   # edges sit 4 cm behind the centre


def sh_top(x):
    return SH_TOP - max(0.0, abs(x) - (SH_W / 2 - SH_CHAMFER))


def sh_bottom(x):
    hw = SH_W / 2
    D = SH_SIDE - SH_BOT
    c = (D * D - hw * hw) / (2 * hw) if False else (D * D - SH_W * SH_W / 4) / SH_W
    R = hw + c
    return SH_SIDE - math.sqrt(max(0.0, R * R - (abs(x) + c) ** 2))


def curve_y(x):
    return SH_CURVE * x * x


def build_shield(mat):
    mb = L.MeshBuilder("chopper_shield")
    bm = mb.bm
    NC, NR = 15, 7
    xs = [SH_W / 2 * math.sin(math.pi / 2 * (-1 + 2 * i / (NC - 1))) for i in range(NC)]
    # board back 10 cm off the forearm axis: clears an 18 cm forearm (bracer incl.) by 1 cm
    y_front, y_back = -0.13, -0.10
    F, B = [], []
    for x in xs:
        zb, zt = sh_bottom(x), sh_top(x)
        colf, colb = [], []
        for j in range(NR + 1):
            z = zb + (zt - zb) * j / NR
            colf.append(bm.verts.new((x, y_front + curve_y(x), z)))
            colb.append(bm.verts.new((x, y_back + curve_y(x), z)))
        F.append(colf)
        B.append(colb)
    faces = []
    for i in range(NC - 1):
        for j in range(NR):
            faces.append(bm.faces.new((F[i][j], F[i + 1][j], F[i + 1][j + 1], F[i][j + 1])))
            faces.append(bm.faces.new((B[i][j + 1], B[i + 1][j + 1], B[i + 1][j], B[i][j])))
    # perimeter loop (index pairs), bottom left->right, up right side, top right->left, down left
    loop = [(i, 0) for i in range(NC)] + [(NC - 1, j) for j in range(1, NR + 1)] + \
           [(i, NR) for i in range(NC - 2, -1, -1)] + [(0, j) for j in range(NR - 1, 0, -1)]
    for a in range(len(loop)):
        i0, j0 = loop[a]
        i1, j1 = loop[(a + 1) % len(loop)]
        faces.append(bm.faces.new((F[i0][j0], B[i0][j0], B[i1][j1], F[i1][j1])))
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    mb.paint(faces, PAL["wood"])

    # steel rim: separate closed loop of geometry hugging the board edge
    pts2 = []
    for i, j in loop:
        z = sh_bottom(xs[i]) + (sh_top(xs[i]) - sh_bottom(xs[i])) * j / NR
        pts2.append(Vector((xs[i], z)))
    # drop outline points closer than 2 cm to the previous kept one (the 1.1 cm step at the
    # chamfered top corners, which lies on the straight chamfer line); a segment shorter than
    # the rim's 2.6 cm inner lip would fold the rim through itself
    kept = [pts2[0]]
    for p in pts2[1:]:
        if (p - kept[-1]).length >= 0.02:
            kept.append(p)
    if (kept[-1] - kept[0]).length < 0.02:
        kept.pop()
    pts2 = kept
    n = len(pts2)
    sect = [(-0.026, y_front - 0.008), (0.012, y_front - 0.008), (0.012, y_back + 0.008),
            (-0.026, y_back + 0.008)]
    rings = []
    for a in range(n):
        p_prev, p, p_next = pts2[a - 1], pts2[a], pts2[(a + 1) % n]
        t = (p_next - p_prev).normalized()
        nrm = Vector((t.y, -t.x))      # outward for a CCW loop
        ring = []
        for off, y in sect:
            q = p + nrm * off
            ring.append(bm.verts.new((q.x, y + curve_y(q.x), q.y)))
        rings.append(ring)
    rim = []
    for a in range(n):
        r0, r1 = rings[a], rings[(a + 1) % n]
        for k in range(4):
            kk = (k + 1) % 4
            rim.append(bm.faces.new((r0[k], r0[kk], r1[kk], r1[k])))
    bmesh.ops.recalc_face_normals(bm, faces=rim)
    mb.paint(rim, PAL["steel_dark"])

    # central boss (dome along -Y)
    w = -Y
    v = Z
    u = v.cross(w)
    zc = -0.02
    boss = [(y_front + 0.004, 0.085), (y_front - 0.005, 0.085), (y_front - 0.012, 0.075),
            (y_front - 0.030, 0.064), (y_front - 0.045, 0.042), (y_front - 0.051, 0.015)]
    mb.tube([(Vector((0, y, zc)), u, v, r, r, r) for y, r in boss], cs=3, cap0=False,
            color=PAL["steel"])

    # back: arm strap loop around the forearm. The origin is the strap centre, i.e. the
    # forearm axis; the forearm runs along local Z (top of the shield toward the elbow).
    # Inner clearance: x +/-0.097, y -0.10..+0.094 -> fits an 18 cm forearm with bracer.
    for s in (-1, 1):
        mb.box((s * 0.105, 0.0025, 0), (0.016, 0.205, 0.055), 0.004, color=PAL["leather_dark"])
    mb.box((0, 0.10, 0), (0.226, 0.012, 0.055), 0.004, color=PAL["leather_dark"])
    # hand grip on the arm axis, 23.5 cm toward the hand (local -Z), across the fist;
    # its posts stand outside the hand's width
    gz = -0.235
    for s in (-1, 1):
        mb.box((s * 0.10, -0.044, gz), (0.016, 0.112, 0.03), 0.004, color=PAL["wood_dark"])
    mb.box((0, 0.0, gz), (0.216, 0.026, 0.03), 0.006, color=PAL["wood_dark"])
    return mb.finish(mat)


def main():
    L.reset_scene()
    m_gear = L.make_material("M_Chopper_Gear", (0.6, 0.6, 0.6))
    for fn in (build_axe, build_axe_bear, build_shield):
        ob = fn(m_gear)
        print(f"built {ob.name}: {L.tri_count(ob)} tris, dims {tuple(round(d, 3) for d in ob.dimensions)}")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("saved", OUT)


if __name__ == "__main__":
    main()
