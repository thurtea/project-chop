"""Build the Chopper Worker character modules (gray box, Week 1).

Run:  blender -b -P build_chopper.py
Out:  Art/Blender/chopper_character.blend

Hand-placed geometry only. Character faces -Y, soles at Z=0, top of hair Z=1.80,
A-pose (arms ~45 deg below horizontal, slightly forward). Every module is a
separate object with identity transform and origin at the world origin.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chopper_lib as L  # noqa: E402
from chopper_lib import PAL, X, Y, Z  # noqa: E402

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ART, "Blender", "chopper_character.blend")

# ------------------------------------------------------------------ proportions
# Torso / neck profile of the body: (z, half width, half depth)
TORSO = [
    (0.62, 0.205, 0.140),
    (0.66, 0.228, 0.150),
    (0.72, 0.240, 0.155),
    (0.80, 0.245, 0.155),   # hip
    (0.86, 0.235, 0.150),   # waist loops
    (0.90, 0.230, 0.150),
    (0.94, 0.235, 0.155),
    (1.02, 0.250, 0.165),
    (1.12, 0.265, 0.170),   # chest
    (1.20, 0.275, 0.170),
    (1.27, 0.280, 0.165),   # shoulder loops
    (1.33, 0.275, 0.155),
    (1.37, 0.230, 0.130),
    (1.395, 0.100, 0.095),  # neck loops
    (1.43, 0.085, 0.085),
    (1.47, 0.085, 0.085),
    (1.52, 0.085, 0.085),
]

LEG_X = 0.13
# Leg profile: (z, hw, hd)
LEG = [  # bottom -> top so the tube winding faces outward
    (0.05, 0.090, 0.100),
    (0.09, 0.098, 0.108),
    (0.12, 0.098, 0.108),
    (0.15, 0.100, 0.110),   # ankle loops
    (0.24, 0.104, 0.114),
    (0.34, 0.104, 0.114),
    (0.41, 0.100, 0.112),
    (0.44, 0.098, 0.112),
    (0.47, 0.098, 0.112),   # knee loops
    (0.50, 0.100, 0.115),
    (0.57, 0.104, 0.118),
    (0.63, 0.106, 0.120),
    (0.70, 0.108, 0.122),   # hip loops
    (0.78, 0.105, 0.120),
]

SHOULDER = (0.30, 0.0, 1.27)
# Arm profile along the arm axis: (t, hw, hd). Elbow t=0.30, wrist t=0.57.
ARM = [
    (-0.05, 0.072, 0.078),
    (-0.03, 0.080, 0.085),  # shoulder loops
    (0.00, 0.082, 0.086),
    (0.04, 0.080, 0.085),
    (0.15, 0.076, 0.080),
    (0.25, 0.070, 0.074),   # elbow loops
    (0.29, 0.068, 0.072),
    (0.33, 0.068, 0.072),
    (0.42, 0.068, 0.070),
    (0.50, 0.064, 0.066),   # wrist loops
    (0.54, 0.060, 0.062),
    (0.575, 0.058, 0.060),
]
# Hand continues the arm tube: (t, hw [thickness], hd [front-back width], curl)
HAND = [
    (0.60, 0.056, 0.072, 0.0),
    (0.64, 0.062, 0.080, 0.0),
    (0.69, 0.062, 0.080, 0.0),   # knuckle
    (0.72, 0.058, 0.078, 0.006),
    (0.755, 0.054, 0.074, 0.012),
    (0.78, 0.046, 0.066, 0.016),
]


def interp(table, z, col):
    """Linear interpolation in a z-sorted-or-reverse table."""
    rows = sorted(table, key=lambda r: r[0])
    if z <= rows[0][0]:
        return rows[0][col]
    for a, b in zip(rows, rows[1:]):
        if a[0] <= z <= b[0]:
            f = (z - a[0]) / (b[0] - a[0])
            return a[col] + (b[col] - a[col]) * f
    return rows[-1][col]


def arm_frame(s):
    d = Vector((s * 1.0, -0.20, -1.0)).normalized()
    fwd = Vector((0.0, -1.0, 0.0))
    v = -(fwd - d * fwd.dot(d)).normalized()   # v points back, so d=-hd is front
    u = v.cross(d)
    palm = u * s                                # side facing the body
    S = Vector((s * SHOULDER[0], SHOULDER[1], SHOULDER[2]))
    return S, d, u, v, palm


def arm_rings(s, table, off=0.0, bevel=0.022):
    S, d, u, v, palm = arm_frame(s)
    return [(S + d * t, u, v, hw + off, hd + off, bevel) for (t, hw, hd) in table]


# ------------------------------------------------------------------ modules
def build_body(mat):
    mb = L.MeshBuilder("chopper_body")

    def torso_color(p):
        return PAL["trousers"] if p.z < 0.87 else None

    # torso + neck, open at the top (inside the head)
    mb.ztube(0, 0, TORSO, cs=2, bevel=0.035, cap1=False, color=PAL["skin"], color_fn=torso_color)
    # legs (trousers), open at the top (inside the pelvis)
    for s in (-1, 1):
        mb.ztube(s * LEG_X, 0, LEG, cs=2, bevel=0.03, cap1=False, color=PAL["trousers"])
    # arms + hands as one tube each
    for s in (-1, 1):
        S, d, u, v, palm = arm_frame(s)
        rings = arm_rings(s, ARM)
        for (t, hw, hd, curl) in HAND:
            rings.append((S + d * t + palm * curl, u, v, hw, hd, 0.016))
        mb.tube(rings, cs=2, cap0=False, color=PAL["skin"])
        # thumb block on the front edge of the hand
        w = (d * 0.75 - v * 0.45 + palm * 0.35).normalized()
        uu, vv, ww = L.basis(w, palm)
        mb.box(S + d * 0.635 - v * 0.074 + palm * 0.022, (0.052, 0.05, 0.08), 0.01,
               rot=L.rot_from(uu, vv, ww), color=PAL["skin"])
    return mb.finish(mat)


def build_head(mat):
    mb = L.MeshBuilder("chopper_head")
    head = [
        (1.410, 0.165, 0.155, -0.010),
        (1.425, 0.180, 0.170, -0.010),
        (1.480, 0.182, 0.172, -0.010),
        (1.560, 0.182, 0.172, -0.010),
        (1.630, 0.182, 0.172, -0.010),
        (1.705, 0.180, 0.170, -0.010),
        (1.720, 0.165, 0.155, -0.010),
    ]
    mb.ztube(0, 0, head, cs=2, bevel=0.03, color=PAL["skin"])
    # nose block, brow ridge, ears (face detail stays in texture)
    mb.box((0, -0.198, 1.552), (0.066, 0.05, 0.085), 0.01, color=PAL["skin"])
    mb.box((0, -0.186, 1.628), (0.30, 0.03, 0.035), 0.008, color=PAL["ginger_dark"])
    for s in (-1, 1):
        mb.box((s * 0.188, 0.01, 1.565), (0.03, 0.07, 0.09), 0.008, color=PAL["skin"])
    return mb.finish(mat)


def vest_front(z0, z1):
    """Largest front depth of the vest (incl. the 14 mm crossbelt) over z0..z1."""
    zs = [z0 + (z1 - z0) * k / 8 for k in range(9)]
    return max(interp(VEST, z, 2) for z in zs) + 0.014


def build_beard(mat):
    """Voxel V-spade beard (concept 1.5): full at the jaw and cheeks, stepping in to a point at
    mid-chest, with a separate moustache bar and drooping ends proud of it. Every layer below
    the chin keeps its back face BEARD_CLEAR in front of the vest and crossbelt so the beard does
    not touch the chest in rest, Idle and Walk (it is skinned 100% to Head)."""
    mb = L.MeshBuilder("chopper_beard")
    G, D = PAL["ginger"], PAL["ginger_dark"]
    M = MOUSTACHE

    def blk(x0, x1, y0, y1, z0, z1, c, b=0.012):
        mb.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), b, color=c)

    # chin and jaw (over the lower head; the head is behind, so these can be deep)
    blk(-0.20, 0.20, -0.255, -0.13, 1.425, 1.50, G)          # chin plate
    for s in (-1, 1):
        x0, x1 = sorted((s * 0.125, s * 0.205))
        blk(x0, x1, -0.245, -0.02, 1.43, 1.585, G)           # cheek sides rising to the ears
        x0, x1 = sorted((s * 0.178, s * 0.213))
        blk(x0, x1, -0.13, 0.03, 1.52, 1.665, D, 0.01)        # sideburn into the hair
    # the V: layers hang in front of the chest, each narrower than the one above
    front = -0.268
    layers = [(1.375, 1.425, 0.195), (1.325, 1.375, 0.168), (1.275, 1.325, 0.138),
              (1.225, 1.275, 0.108), (1.180, 1.225, 0.078), (1.145, 1.180, 0.050),
              (1.115, 1.145, 0.024)]
    for i, (z0, z1, hw) in enumerate(layers):
        back = -(vest_front(z0 - 0.01, z1 + 0.01) + BEARD_CLEAR)
        blk(-hw, hw, front + 0.002 * i, back, z0, z1 + 0.004, G if i % 2 == 0 else D)
    # darker strand blocks proud of the front, stepping down the V
    chunks = [(-0.14, 1.40, 0.05), (0.0, 1.405, 0.06), (0.14, 1.40, 0.05),
              (-0.075, 1.35, 0.05), (0.075, 1.35, 0.05),
              (-0.10, 1.30, 0.04), (0.035, 1.30, 0.05),
              (-0.035, 1.25, 0.045), (0.065, 1.25, 0.035),
              (0.0, 1.20, 0.04)]
    for i, (x, z, w) in enumerate(chunks):
        mb.box((x, front - 0.008, z), (w, 0.02, 0.04), 0.006, color=D if i % 2 == 0 else G)
    # moustache: separate darker bar over the mouth with ends drooping proud of the beard
    mb.box((0, -0.228, 1.522), (0.25, 0.05, 0.042), 0.01, color=M)
    for s in (-1, 1):
        mb.box((s * 0.105, -0.262, 1.485), (0.058, 0.03, 0.08), 0.008, color=M)
        mb.box((s * 0.125, -0.279, 1.44), (0.04, 0.02, 0.06), 0.006, color=M)
    return mb.finish(mat)


MOUSTACHE = (0.88, 0.44, 0.13)   # lighter ginger so the moustache reads apart from the beard
BEARD_CLEAR = 0.035      # m in front of the vest + crossbelt (head nods up to ~5 deg in Walk)


def build_hair(mat):
    """Swept-back block hair: domed voxel cap, thick sides, long back."""
    mb = L.MeshBuilder("chopper_hair")
    G, D = PAL["ginger"], PAL["ginger_dark"]
    xs = [-0.155, -0.052, 0.052, 0.155]
    ys = [-0.14, -0.04, 0.06, 0.155]
    tops = [[1.789, 1.800, 1.800, 1.789],
            [1.786, 1.798, 1.798, 1.786],
            [1.776, 1.790, 1.790, 1.776],
            [1.762, 1.776, 1.776, 1.762]]
    for j, y in enumerate(ys):
        for i, x in enumerate(xs):
            zt, zb = tops[j][i], 1.695
            mb.box((x, y, (zb + zt) / 2), (0.108, 0.104, zt - zb), 0.011, color=G if (i + j) % 2 == 0 else D)
    # swept front roll over the hairline
    rot = Matrix.Rotation(math.radians(-25.0), 3, "X")
    for x in (-0.13, 0.0, 0.13):
        mb.box((x, -0.198, 1.748), (0.135, 0.05, 0.08), 0.011, rot=rot, color=G)
    for s in (-1, 1):
        for y, zb in ((-0.07, 1.62), (0.03, 1.57), (0.13, 1.50)):
            zt = 1.77
            mb.box((s * 0.205, y, (zb + zt) / 2), (0.045, 0.104, zt - zb), 0.01, color=D if y > 0 else G)
        mb.box((s * 0.232, 0.07, 1.67), (0.022, 0.14, 0.13), 0.008, color=G)
    for i, (x, zb) in enumerate(zip(xs, (1.47, 1.43, 1.435, 1.47))):
        zt = 1.77
        mb.box((x, 0.19, (zb + zt) / 2), (0.108, 0.05, zt - zb), 0.011, color=G if i % 2 else D)
    for x, zb in ((-0.08, 1.56), (0.08, 1.55)):
        mb.box((x, 0.222, (zb + 1.74) / 2), (0.13, 0.02, 1.74 - zb), 0.007, color=G)
    return mb.finish(mat)

SHIRT = [
    (0.515, 0.262, 0.182), (0.53, 0.260, 0.180), (0.60, 0.256, 0.175), (0.70, 0.253, 0.170),
    (0.80, 0.253, 0.163), (0.86, 0.243, 0.158), (0.90, 0.238, 0.158), (0.94, 0.243, 0.163),
    (1.02, 0.258, 0.173), (1.12, 0.273, 0.178), (1.20, 0.283, 0.178), (1.27, 0.288, 0.173),
    (1.33, 0.283, 0.163), (1.37, 0.238, 0.138), (1.395, 0.110, 0.105), (1.43, 0.098, 0.098),
]


def build_shirt(mat):
    mb = L.MeshBuilder("chopper_shirt")
    mb.ztube(0, 0, SHIRT, cs=2, bevel=0.035, cap0=False, cap1=False, color=PAL["shirt"],
             color_fn=lambda p: PAL["shirt_dark"] if p.z < 0.56 or p.z > 1.385 else None)
    for s in (-1, 1):
        S, d, u, v, palm = arm_frame(s)
        # full-length plaid sleeve (concept 1.5): the forearm is covered to the wrist, so the
        # base look without bracers shows no bare forearm and no gap at the hand
        sleeve = [(-0.06, 0.072, 0.078)] + [r for r in ARM if -0.04 < r[0] <= 0.54]
        rings = arm_rings(s, sleeve, off=0.010, bevel=0.025)
        # buttoned cuff at the wrist, ending where the hand starts (t 0.60)
        for t, extra in ((0.556, 0.016), (0.562, 0.020), (0.585, 0.020), (0.592, 0.012)):
            hw = L_interp_arm(t, 1) + extra
            hd = L_interp_arm(t, 2) + extra
            rings.append((S + d * t, u, v, hw, hd, 0.025))
        mb.tube(rings, cs=2, cap0=False, cap1=False, color=PAL["shirt"],
                color_fn=lambda p, S=S, d=d: PAL["shirt_dark"] if (p - S).dot(d) > 0.553 else None)
    return mb.finish(mat)


def L_interp_arm(t, col):
    return interp(ARM, t, col)


VEST = [
    (0.825, 0.258, 0.169), (0.86, 0.251, 0.166), (0.90, 0.246, 0.166), (0.94, 0.251, 0.171),
    (1.02, 0.266, 0.181), (1.12, 0.281, 0.186), (1.20, 0.291, 0.186), (1.27, 0.296, 0.181),
    (1.33, 0.291, 0.171), (1.37, 0.246, 0.146), (1.39, 0.156, 0.121),
]


def build_vest(mat):
    mb = L.MeshBuilder("chopper_vest")

    def vest_color(p):
        # gray patch blocks (placeholder for the patched texture)
        if p.y < 0 and 0.97 < p.z < 1.10 and -0.20 < p.x < -0.05:
            return PAL["patch"]
        if p.y < 0 and 1.15 < p.z < 1.30 and 0.08 < p.x < 0.24:
            return PAL["patch"]
        return None

    mb.ztube(0, 0, VEST, cs=2, bevel=0.04, cap0=False, cap1=False, color=PAL["vest"], color_fn=vest_color)

    # diagonal strap: character's right shoulder (-X) to left hip (+X), front and back
    def surf_y(x, z, side):
        hd = interp(VEST, z, 2)
        return side * (hd + 0.008)

    # three straight runs (front diagonal, over the shoulder, back diagonal); separate
    # tubes so the strap never twists or folds through itself at the shoulder bend
    front = [(0.25, 0.905), (0.10, 1.04), (-0.05, 1.17), (-0.19, 1.30)]
    for side in (-1, 1):
        pts = [(x, surf_y(x, z, side), z) for x, z in front]
        mb.path_strip(pts, [(0, side, 0)] * len(pts), 0.034, 0.006, PAL["leather_dark"], cs=1, bevel=0.003)
    over = [(-0.19, -0.175, 1.36), (-0.19, -0.12, 1.39), (-0.19, 0.0, 1.402), (-0.19, 0.12, 1.39),
            (-0.19, 0.175, 1.36)]
    mb.path_strip(over, [(0, -0.7, 0.7), (0, -0.4, 0.9), (0, 0, 1), (0, 0.4, 0.9), (0, 0.7, 0.7)],
                  0.034, 0.006, PAL["leather_dark"], cs=1, bevel=0.003)
    # strap buckle on the chest
    ang = math.atan2(1.30 - 0.905, -0.19 - 0.25)
    rot = Matrix.Rotation(-ang, 3, "Y")
    mb.box((0.03, surf_y(0.03, 1.10, -1) - 0.01, 1.10), (0.085, 0.022, 0.085), 0.008, rot=rot,
           color=PAL["steel"])
    return mb.finish(mat)


def build_belt(mat):
    mb = L.MeshBuilder("chopper_belt")
    belt = [(0.800, 0.268, 0.180), (0.805, 0.282, 0.194), (0.895, 0.282, 0.194), (0.900, 0.262, 0.180)]
    mb.ztube(0, 0, belt, cs=2, bevel=0.04, cap0=False, cap1=False, color=PAL["leather_dark"])
    # buckle, keeper, hanging tongue
    mb.box((0, -0.204, 0.85), (0.115, 0.026, 0.105), 0.01, color=PAL["steel"])
    mb.box((0.10, -0.200, 0.85), (0.035, 0.02, 0.10), 0.006, color=PAL["vest"])
    mb.box((0.075, -0.205, 0.755), (0.06, 0.016, 0.16), 0.006, color=PAL["leather_dark"])
    # side pouch tab (character left)
    mb.box((0.215, -0.15, 0.80), (0.08, 0.04, 0.10), 0.01, color=PAL["vest"])
    return mb.finish(mat)


def build_bracers(mat):
    """Leather bracers with steel rims on the forearms (armour module, off in the base look).
    They sit over the full-length shirt sleeve (sleeve +10 mm, bracer +15..22 mm)."""
    mb = L.MeshBuilder("chopper_bracers")
    for s in (-1, 1):
        S, d, u, v, palm = arm_frame(s)
        rings = []
        for t, extra in ((0.42, 0.013), (0.425, 0.022), (0.445, 0.022), (0.45, 0.015),
                         (0.525, 0.015), (0.53, 0.022), (0.55, 0.022), (0.555, 0.013)):
            rings.append((S + d * t, u, v, L_interp_arm(t, 1) + extra, L_interp_arm(t, 2) + extra, 0.02))
        mb.tube(rings, cs=2, cap0=False, cap1=False, color=PAL["vest"],
                color_fn=lambda p, S=S, d=d: PAL["steel_dark"]
                if not (0.447 < (p - S).dot(d) < 0.528) else None)
    return mb.finish(mat)


def build_chainmail(mat):
    """Thick split skirt. Rings run bottom -> top (+Z) so the tube winding faces outward;
    the inner shell is reversed to face the legs, and the hem rim and slit sides close it."""
    import bmesh
    mb = L.MeshBuilder("chopper_chainmail")
    outer = [(0.565, 0.283, 0.196), (0.63, 0.277, 0.191), (0.70, 0.272, 0.186),
             (0.80, 0.266, 0.180), (0.87, 0.258, 0.172)]
    split_below = 0.70  # front slit from the hem up to here
    gap = 0.022
    rings_o = [(Vector((0, 0, z)), X, Y, hw, hd, 0.04) for z, hw, hd in outer]
    rings_i = [(Vector((0, 0, z)), X, Y, hw - 0.011, hd - 0.011, 0.03) for z, hw, hd in outer]
    skip = lambda i: outer[i + 1][0] <= split_below + 1e-6  # noqa: E731
    vo, fo = mb.tube(rings_o, cs=2, gap=gap, cap0=False, cap1=False, color=PAL["chain"], skip=skip)
    vi, fi = mb.tube(rings_i, cs=2, gap=gap, cap0=False, cap1=False, color=PAL["chain"], skip=skip)
    bmesh.ops.reverse_faces(mb.bm, faces=fi)      # inner shell faces the legs
    n = len(vo[0])
    extra = []
    # hem rim (ring 0), facing down; skips the slit column
    for j in range(n - 1):
        k = j + 1
        extra.append(mb.bm.faces.new((vo[0][k], vo[0][j], vi[0][j], vi[0][k])))
    # slit side walls
    for i in range(len(outer) - 1):
        if not skip(i):
            continue
        extra.append(mb.bm.faces.new((vo[i][0], vo[i + 1][0], vi[i + 1][0], vi[i][0])))
        extra.append(mb.bm.faces.new((vo[i + 1][n - 1], vo[i][n - 1], vi[i][n - 1], vi[i + 1][n - 1])))
    mb.paint(extra, PAL["steel_dark"])
    return mb.finish(mat, recalc=False)


def build_boots(mat):
    mb = L.MeshBuilder("chopper_boots")
    for s in (-1, 1):
        x0 = s * LEG_X
        # shaft over the shin: leg + 8 mm, two raised wraps and a cuff
        shaft = []
        for z, raise_ in ((0.05, 0.0), (0.13, 0.0), (0.175, 0.0), (0.18, 0.010), (0.225, 0.010),
                          (0.23, 0.0), (0.265, 0.0), (0.27, 0.010), (0.315, 0.010), (0.32, 0.0),
                          (0.355, 0.0), (0.36, 0.020), (0.415, 0.020), (0.42, 0.006)):
            hw = interp(LEG, z, 1) + 0.008 + raise_
            hd = interp(LEG, z, 2) + 0.008 + raise_
            shaft.append((z, hw, hd))
        mb.ztube(x0, 0, shaft, cs=2, bevel=0.03, cap0=False, cap1=False, color=PAL["boot"],
                 color_fn=lambda p: PAL["boot_wrap"] if (0.177 < p.z < 0.228 or 0.267 < p.z < 0.318
                                                         or p.z > 0.357) else None)
        # foot, as a tube running heel -> toe (axis -Y)
        w = -Y
        v = Z
        u = v.cross(w)
        foot = [(0.135, 0.110, 0.025, 0.135), (0.12, 0.122, 0.025, 0.150), (0.0, 0.126, 0.025, 0.155),
                (-0.10, 0.128, 0.025, 0.148), (-0.17, 0.128, 0.025, 0.128), (-0.22, 0.124, 0.025, 0.100),
                (-0.245, 0.114, 0.03, 0.078)]
        rings = []
        for y, hw, z0, z1 in foot:
            rings.append((Vector((x0, y, (z0 + z1) / 2)), u, v, hw, (z1 - z0) / 2, 0.02))
        mb.tube(rings, cs=2, color=PAL["boot"])
        # sole and instep strap
        mb.box((x0, -0.055, 0.0175), (0.272, 0.40, 0.035), 0.008, color=PAL["sole"])
        mb.box((x0, -0.07, 0.15), (0.262, 0.045, 0.02), 0.006, color=PAL["boot_wrap"])
    return mb.finish(mat)


def build_helmet(mat):
    """Steel skull cap, nose guard and two horns. Hair stays the tallest point (1.80 m)."""
    mb = L.MeshBuilder("chopper_helmet")
    cap = [
        (1.48, 0.198, 0.188, -0.02),
        (1.56, 0.208, 0.198, -0.015),
        (1.64, 0.190, 0.180, 0.0),
        (1.71, 0.145, 0.135, 0.015),
        (1.765, 0.075, 0.068, 0.02),
    ]
    mb.ztube(0, 0, cap, cs=2, bevel=0.018, color=PAL["steel"])
    # skull plate and eye sockets (the painted skull lives in the atlas; this is the silhouette)
    mb.box((0, -0.205, 1.58), (0.155, 0.035, 0.16), 0.008, color=PAL["steel_dark"])
    mb.box((0, -0.222, 1.46), (0.038, 0.028, 0.14), 0.006, color=PAL["steel"])
    for s in (-1, 1):
        axis = Vector((s * 0.72, -0.25, 0.65)).normalized()
        mb.cylinder((s * 0.145, -0.04, 1.68), axis, 0.026, 0.20, color=PAL["steel_dark"], cs=3, bevel=0.005)
        mb.box((s * 0.12, -0.17, 1.52), (0.075, 0.04, 0.11), 0.006, color=PAL["steel"])
        mb.box((s * 0.155, -0.02, 1.60), (0.05, 0.055, 0.09), 0.006, color=PAL["steel_dark"])
    for ang in range(-72, 80, 16):
        a = math.radians(ang)
        mb.box((0.195 * math.sin(a), -0.175 * math.cos(a), 1.705), (0.03, 0.022, 0.02), 0.004,
               color=PAL["steel_dark"])
    return mb.finish(mat)


def build_plate(mat):
    """Steel chest shell for the Elite. Sits outside the leather vest; the Elite prefab hides the vest."""
    mb = L.MeshBuilder("chopper_plate")
    plate = [
        (0.90, 0.268, 0.178),
        (0.98, 0.278, 0.184),
        (1.08, 0.292, 0.190),
        (1.16, 0.300, 0.190),
        (1.24, 0.292, 0.180),
        (1.32, 0.260, 0.158),
        (1.36, 0.190, 0.128),
    ]
    mb.ztube(0, 0, plate, cs=2, bevel=0.028, cap0=False, cap1=False, color=PAL["steel"],
             color_fn=lambda p: PAL["steel_dark"] if (0.99 < p.z < 1.05 or 1.17 < p.z < 1.23) else None)
    for z, w, h in ((1.06, 0.22, 0.09), (1.18, 0.24, 0.10)):
        hd = interp(plate, z, 2)
        mb.box((0, -(hd + 0.010), z), (w, 0.016, h), 0.004, color=PAL["steel_dark"])
    def surf_y(z, side):
        return side * (interp(plate, z, 2) + 0.016)
    front = [(0.22, 0.92), (0.08, 1.06), (-0.06, 1.18), (-0.16, 1.30)]
    for side in (-1, 1):
        pts = [(x, surf_y(z, side), z) for x, z in front]
        mb.path_strip(pts, [(0, side, 0)] * len(pts), 0.028, 0.006, PAL["leather_dark"], cs=1, bevel=0.003)
    return mb.finish(mat)


def build_pauldrons(mat):
    """Steel caps on top of the shoulders. Kept off the upper arm so the shield crown stays clear."""
    mb = L.MeshBuilder("chopper_pauldrons")
    for s in (-1, 1):
        c = Vector((s * 0.30, -0.01, 1.40))
        mb.box(c, (0.20, 0.16, 0.055), 0.008, color=PAL["steel"])
        mb.box(c + Vector((0, 0, 0.04)), (0.14, 0.11, 0.035), 0.006, color=PAL["steel_dark"])
    return mb.finish(mat)


def main():
    L.reset_scene()
    m_chop = L.make_material("M_Chopper", (0.8, 0.8, 0.8))
    m_gear = L.make_material("M_Chopper_Gear", (0.6, 0.6, 0.6))
    for fn in (build_body, build_head, build_beard, build_hair, build_shirt, build_vest,
               build_belt, build_bracers, build_chainmail, build_boots):
        ob = fn(m_chop)
        print(f"built {ob.name}: {L.tri_count(ob)} tris")
    for fn in (build_helmet, build_plate, build_pauldrons):
        ob = fn(m_gear)
        print(f"built {ob.name}: {L.tri_count(ob)} tris")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("saved", OUT)


if __name__ == "__main__":
    main()
