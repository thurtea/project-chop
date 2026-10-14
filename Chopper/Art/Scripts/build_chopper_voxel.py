"""Build Chopper as a voxel model (model sheet, concept 2.0): every part is filled with small cubes.

Run:  blender -b -P build_chopper_voxel.py
In:   Art/Scripts/chopper_joints.json (rest joints of chopper_rig)
Out:  Art/Blender/chopper_character.blend

Character faces -Y, soles at Z=0, top of hair Z=1.80, A-pose. Each module is a separate object with identity
transform. Colours are per voxel in the "Col" corner attribute (sRGB); the base-layer colours are in the UV maps
"UnderRG" and "UnderB". Vertex groups are written here; rig_chopper.py adds the armature from the same joints.
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_chopper as BC  # noqa: E402
import chopper_lib as L  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, ".."))
OUT = os.path.join(ART, "Blender", "chopper_character.blend")
JOINTS = json.load(open(os.path.join(HERE, "chopper_joints.json")))

V_BODY = 0.02
V_HAIR = 0.016
V_HEAD = 0.0125


def J(name, end="head"):
    return Vector(JOINTS[name][end])


def rgb(r, g, b):
    return (r / 255.0, g / 255.0, b / 255.0)


def mix(a, b, k):
    return tuple(x + (y - x) * k for x, y in zip(a, b))


def shade(c, k):
    return tuple(min(1.0, x * k) for x in c)


def h3(i, j, k):
    n = (i * 73856093) ^ (j * 19349663) ^ (k * 83492791)
    n = (n ^ (n >> 13)) * 1274126177
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def grain(c, i, j, k, amount=0.06):
    return shade(c, 1.0 + (h3(i, j, k) - 0.5) * 2 * amount)


RED = rgb(176, 38, 34)
RED_LINE = rgb(122, 24, 22)
PLAID_MID = rgb(96, 18, 20)
PLAID_BLACK = rgb(34, 12, 14)
LEATHER = [rgb(124, 76, 40), rgb(102, 62, 32), rgb(144, 92, 50)]
PATCH = [rgb(112, 112, 112), rgb(90, 92, 96)]
SEAM = rgb(60, 36, 20)
THREAD = rgb(212, 188, 146)
STRAP = rgb(62, 36, 20)
BELT = rgb(74, 46, 26)
STEEL_LIGHT = rgb(196, 200, 205)
STEEL_MID = rgb(132, 136, 142)
STEEL_DARK = rgb(72, 74, 80)
MAIL = [rgb(96, 94, 92), rgb(60, 58, 58), rgb(34, 33, 34)]
TROUSERS = rgb(92, 62, 38)
TROUSERS_DARK = rgb(70, 46, 30)
KNEE_PAD = rgb(110, 74, 46)
BOOT = rgb(88, 58, 36)
BOOT_DARK = rgb(64, 42, 28)
WRAP = rgb(74, 52, 36)
SOLE = rgb(46, 30, 20)
SKIN = rgb(232, 172, 136)
SKIN_SHADE = rgb(204, 142, 110)
SKIN_DARK = rgb(170, 110, 84)
LIP = rgb(150, 82, 66)
EYE_WHITE = rgb(236, 232, 222)
IRIS = rgb(52, 140, 70)
PUPIL = rgb(24, 30, 26)
GINGER = rgb(170, 76, 28)
GINGER_DARK = rgb(112, 44, 16)
GINGER_LIGHT = rgb(204, 108, 46)
MOUSTACHE = rgb(186, 90, 36)
BEAD = rgb(208, 156, 72)
LINEN = rgb(92, 92, 94)
LINEN_DARK = rgb(68, 68, 70)
SHORTS = rgb(78, 78, 80)


def plaid(a, b):
    a, b = a % 6, b % 6
    da, db = a < 2, b < 2
    if da and db:
        return PLAID_BLACK
    if da or db:
        return PLAID_MID
    if a == 4 or b == 4:
        return RED_LINE
    return RED


def interp(table, z, col):
    rows = sorted(table, key=lambda r: r[0])
    if z <= rows[0][0]:
        return rows[0][col]
    for a, b in zip(rows, rows[1:]):
        if a[0] <= z <= b[0]:
            f = (z - a[0]) / (b[0] - a[0])
            return a[col] + (b[col] - a[col]) * f
    return rows[-1][col]


def superellipse(x, y, hw, hd, n=4.0):
    if hw <= 0 or hd <= 0:
        return 2.0
    return (abs(x) / hw) ** n + (abs(y) / hd) ** n


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


# ------------------------------------------------------------------ frames and parts
class Frame:
    """Voxel grid frame: origin, orthonormal axes (columns of R), voxel size."""

    def __init__(self, origin, R, v):
        self.o = Vector(origin)
        self.R = R
        self.v = v

    def world(self, i, j, k):
        return self.o + self.R @ Vector(((i + 0.5) * self.v, (j + 0.5) * self.v, (k + 0.5) * self.v))

    def local(self, p):
        return self.R.transposed() @ (Vector(p) - self.o)


WORLD = Matrix.Identity(3)


def bone_frame(bone, v, start=None):
    a, b = J(bone), J(bone, "tail")
    d = (b - a).normalized()
    fwd = Vector((0.0, -1.0, 0.0))
    x = fwd.cross(d).normalized()
    y = d.cross(x).normalized()
    R = Matrix((x, y, d)).transposed()
    return Frame(start if start is not None else a, R, v)


class Part:
    def __init__(self, obj, frame, inside, color, weights, bounds):
        self.obj = obj
        self.frame = frame
        self.inside = inside
        self.color = color
        self.weights = weights
        self.bounds = bounds
        self.cells = set()

    def fill(self):
        (x0, y0, z0), (x1, y1, z1) = self.bounds
        v = self.frame.v
        for i in range(math.floor(x0 / v), math.ceil(x1 / v)):
            for j in range(math.floor(y0 / v), math.ceil(y1 / v)):
                for k in range(math.floor(z0 / v), math.ceil(z1 / v)):
                    lp = Vector(((i + 0.5) * v, (j + 0.5) * v, (k + 0.5) * v))
                    if self.inside(lp, self.frame.o + self.frame.R @ lp):
                        self.cells.add((i, j, k))
        return self


FACES = [((1, 0, 0), [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)]),
         ((-1, 0, 0), [(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)]),
         ((0, 1, 0), [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)]),
         ((0, -1, 0), [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)]),
         ((0, 0, 1), [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]),
         ((0, 0, -1), [(0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)])]


def build_object(name, parts, mat, bones):
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    bm = bmesh.new()
    col = bm.loops.layers.color.new("Col")
    uv_rg = bm.loops.layers.uv.new("UnderRG")
    uv_b = bm.loops.layers.uv.new("UnderB")
    dl = bm.verts.layers.deform.verify()
    groups = {b: i for i, b in enumerate(bones)}
    shared = {}
    for part in parts:
        key = (tuple(part.frame.o), tuple(tuple(r) for r in part.frame.R), part.frame.v)
        shared.setdefault(key, set()).update(part.cells)
    for part in parts:
        f = part.frame
        v = f.v
        cells = part.cells
        solid = shared[(tuple(f.o), tuple(tuple(r) for r in f.R), v)]
        planes = {}
        for (i, j, k) in cells:
            centre = f.world(i, j, k)
            w = part.weights(centre)
            items = sorted(((b, x) for b, x in w.items() if x > 1e-4), key=lambda bx: -bx[1])[:4]
            tot = sum(x for _, x in items) or 1.0
            q = [(b, round(x / tot * 20)) for b, x in items if round(x / tot * 20) > 0] or [(items[0][0], 20)]
            qs = sum(x for _, x in q)
            wkey = tuple((b, x / qs) for b, x in q)
            cell = (i, j, k)
            for n, _ in FACES:
                if (i + n[0], j + n[1], k + n[2]) in solid:
                    continue
                a = 0 if n[0] else 1 if n[1] else 2
                s = n[a]
                nw = f.R @ Vector(n)
                c, cu = part.color(i, j, k, Vector(n), nw, centre)
                key = (tuple(round(x, 3) for x in c), tuple(round(x, 3) for x in cu), wkey)
                plane = cell[a] + (1 if s > 0 else 0)
                planes.setdefault((a, s, plane), {})[(cell[(a + 1) % 3], cell[(a + 2) % 3])] = key
        for (a, s, plane), grid in planes.items():
            used = set()
            for (u, w0) in sorted(grid):
                if (u, w0) in used:
                    continue
                key = grid[(u, w0)]
                u1 = u
                while (u1 + 1, w0) in grid and (u1 + 1, w0) not in used and grid[(u1 + 1, w0)] == key:
                    u1 += 1
                w1 = w0
                while all((uu, w1 + 1) in grid and (uu, w1 + 1) not in used and grid[(uu, w1 + 1)] == key for uu in range(u, u1 + 1)):
                    w1 += 1
                for uu in range(u, u1 + 1):
                    for ww in range(w0, w1 + 1):
                        used.add((uu, ww))
                corners = [(u, w0), (u1 + 1, w0), (u1 + 1, w1 + 1), (u, w1 + 1)]
                if s < 0:
                    corners.reverse()
                c, cu, wkey = key
                verts = []
                for cu_, cw_ in corners:
                    lp = [0.0, 0.0, 0.0]
                    lp[a] = plane * v
                    lp[(a + 1) % 3] = cu_ * v
                    lp[(a + 2) % 3] = cw_ * v
                    vt = bm.verts.new(f.o + f.R @ Vector(lp))
                    for b, x in wkey:
                        vt[dl][groups[b]] = x
                    verts.append(vt)
                face = bm.faces.new(verts)
                for lp in face.loops:
                    lp[col] = (c[0], c[1], c[2], 1.0)
                    lp[uv_rg].uv = (cu[0], cu[1])
                    lp[uv_b].uv = (cu[2], 0.0)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(me)
    bm.free()
    for b in bones:
        ob.vertex_groups.new(name=b)
    for poly in me.polygons:
        poly.use_smooth = False
    me.color_attributes.active_color = me.color_attributes["Col"]
    return ob


# ------------------------------------------------------------------ weights
def ramp(x, a, b):
    return min(1.0, max(0.0, (x - a) / (b - a)))


def spine(z):
    chain = [("Hips", 0.76, 0.90), ("Spine", 0.90, 1.05), ("Chest", 1.05, 1.20), ("UpperChest", 1.20, 1.37)]
    w = {}
    for idx, (b, z0, z1) in enumerate(chain):
        lo = ramp(z, z0 - 0.04, z0 + 0.04) if idx else 1.0
        hi = 1.0 - ramp(z, z1 - 0.04, z1 + 0.04) if idx < len(chain) - 1 else 1.0
        if lo * hi > 0:
            w[b] = lo * hi
    if z > 1.33:
        w = {k: x * (1 - ramp(z, 1.33, 1.40)) for k, x in w.items()}
        w["Neck"] = ramp(z, 1.33, 1.40)
    return w or {"Hips": 1.0}


def torso_w(p):
    w = spine(p.z)
    if p.z < 0.80:
        a = 0.85 * ramp(p.z, 0.80, 0.70)
        sl = ramp(p.x, -0.17, 0.17)
        w = {k: x * (1 - a) for k, x in w.items()}
        w["LeftUpperLeg"] = w.get("LeftUpperLeg", 0) + a * sl
        w["RightUpperLeg"] = w.get("RightUpperLeg", 0) + a * (1 - sl)
    return w


def head_w(p):
    return {"Head": 1.0}


def arm_w(side):
    S, E, W = J(side + "UpperArm"), J(side + "LowerArm"), J(side + "Hand")

    def w(p):
        d = (E - S).normalized()
        t = (p - S).dot(d)
        de = (W - E).normalized()
        te = (p - E).dot(de)
        el = ramp(t, 0.27, 0.33)
        wr = ramp(te, 0.25, 0.29)
        sh = 0.5 * ramp(t, 0.04, -0.04)
        return {side + "Shoulder": sh, side + "UpperArm": (1 - sh) * (1 - el),
                side + "LowerArm": (1 - sh) * el * (1 - wr), side + "Hand": (1 - sh) * el * wr}
    return w


def leg_w(side):
    def w(p):
        kn = ramp(p.z, 0.48, 0.43)
        an = ramp(p.z, 0.17, 0.13)
        toe = ramp(p.y, -0.10, -0.15) if p.z < 0.165 else 0.0
        return {side + "UpperLeg": 1 - kn, side + "LowerLeg": kn * (1 - an),
                side + "Foot": kn * an * (1 - toe), side + "Toes": kn * an * toe}
    return w


# ------------------------------------------------------------------ torso layers
SHIRT = [(0.53, 0.312, 0.216), (0.60, 0.306, 0.206), (0.80, 0.292, 0.186), (0.90, 0.270, 0.180),
         (1.00, 0.316, 0.212), (1.10, 0.346, 0.232), (1.20, 0.360, 0.238), (1.27, 0.356, 0.226),
         (1.33, 0.318, 0.200), (1.37, 0.250, 0.165), (1.40, 0.150, 0.130)]
SHIRT_TOP = 1.40


def shirt_in(p):
    if not 0.53 <= p.z <= SHIRT_TOP:
        return False
    return superellipse(p.x, p.y - 0.005, interp(SHIRT, p.z, 1), interp(SHIRT, p.z, 2)) <= 1.0


def grown(p, d, top=SHIRT_TOP):
    if not 0.53 <= p.z <= top:
        return False
    return superellipse(p.x, p.y - 0.005, interp(SHIRT, p.z, 1) + d, interp(SHIRT, p.z, 2) + d) <= 1.0


def face_axes(n):
    if abs(n.z) > 0.5:
        return 0, 1
    if abs(n.y) > 0.5:
        return 0, 2
    return 1, 2


def shirt_color(i, j, k, n, nw, c):
    a, b = face_axes(nw)
    idx = (i, j, k)
    col = plaid(idx[a], idx[b])
    if c.z < 0.57 or c.z > 1.37:
        col = shade(col, 0.72)
    under = LINEN_DARK if c.z < 0.57 or c.z > 1.37 else LINEN
    return col, under


VEST_FRONT = [(-0.30, -0.14, 1.14, 1.32, PATCH[0]), (-0.14, -0.03, 1.02, 1.20, LEATHER[2]),
              (-0.28, -0.12, 0.90, 1.06, PATCH[1]), (0.03, 0.16, 1.18, 1.32, LEATHER[2]),
              (0.16, 0.30, 1.06, 1.24, PATCH[1]), (0.05, 0.18, 0.90, 1.05, PATCH[0])]
VEST_BACK = [(-0.34, -0.12, 1.12, 1.30, PATCH[0]), (-0.12, 0.10, 0.96, 1.12, LEATHER[2]),
             (0.10, 0.32, 1.14, 1.30, PATCH[1]), (0.14, 0.34, 0.90, 1.04, PATCH[0])]


def vest_in(p):
    if not 0.84 <= p.z <= 1.37 or not grown(p, 0.022) or shirt_in(p):
        return False
    if p.y < 0 and abs(p.x) < 0.012:
        return False
    if p.z > 1.20 and abs(p.x) > 0.30:
        return False
    return True


def strap_dist(p):
    a = Vector((0.27, 0.0, 0.88))
    b = Vector((-0.30, 0.0, 1.36))
    q = Vector((p.x, 0.0, p.z))
    ab = b - a
    t = max(0.0, min(1.0, (q - a).dot(ab) / ab.length_squared))
    return (q - (a + ab * t)).length


def panel(a, z, patches, v):
    for x0, x1, z0, z1, colour in patches:
        if x0 <= a < x1 and z0 <= z < z1:
            ea, ez = min(a - x0, x1 - a), min(z - z0, z1 - z)
            if ea < v and ez < v:
                return STEEL_MID
            if ea < v or ez < v:
                return SEAM
            return colour
    return LEATHER[0] if math.floor(z / 0.12) % 2 else mix(LEATHER[0], LEATHER[1], 0.4)


def vest_color(i, j, k, n, nw, c):
    if abs(nw.z) > 0.7:
        col = LEATHER[1]
    elif nw.y < -0.3:
        col = panel(c.x, c.z, VEST_FRONT, V_BODY)
        if abs(c.x) < 0.04 and 0.90 < c.z < 1.32:
            col = THREAD if (k + (1 if c.x > 0 else 0)) % 3 == 0 else shade(LEATHER[1], 0.8)
    elif nw.y > 0.3:
        col = panel(c.x, c.z, VEST_BACK, V_BODY)
    else:
        col = LEATHER[1]
    return grain(col, i // 3, j // 3, k // 3, 0.05), (0, 0, 0)


def strap_in(p):
    if not 0.84 <= p.z <= 1.40 or not grown(p, 0.044, 1.42) or grown(p, 0.022, 1.42):
        return False
    return strap_dist(p) < 0.042 and abs(p.y) > 0.05


def strap_color(i, j, k, n, nw, c):
    bx, bz = 0.02, 1.10
    if abs(c.x - bx) < 0.05 and abs(c.z - bz) < 0.05 and nw.y < -0.3:
        edge = max(abs(c.x - bx), abs(c.z - bz)) > 0.025
        return (STEEL_LIGHT if edge else shade(STRAP, 0.7)), (0, 0, 0)
    col = STRAP if (i + k) % 7 else mix(STRAP, THREAD, 0.3)
    return col, (0, 0, 0)


def belt_in(p):
    return 0.80 <= p.z <= 0.90 and grown(p, 0.044, 1.42) and not grown(p, 0.022, 1.42)


def belt_color(i, j, k, n, nw, c):
    if nw.y < -0.3 and abs(c.x) < 0.06:
        edge = abs(c.x) > 0.035 or abs(c.z - 0.85) > 0.03
        return (STEEL_LIGHT if edge else rgb(50, 30, 16)), (0, 0, 0)
    if abs(c.z - 0.81) < 0.011 or abs(c.z - 0.89) < 0.011:
        return (mix(BELT, THREAD, 0.4) if (i + j) % 2 else BELT), (0, 0, 0)
    return BELT, (0, 0, 0)


def pouch_in(p):
    return 0.70 <= p.z <= 0.84 and 0.17 <= p.x <= 0.27 and -0.27 <= p.y <= -0.17 and not grown(p, 0.022, 1.42)


def pouch_color(i, j, k, n, nw, c):
    col = LEATHER[2] if c.z > 0.79 else LEATHER[0]
    return grain(col, i, j, k), (0, 0, 0)


def tongue_in(p):
    return 0.70 <= p.z <= 0.80 and 0.06 <= p.x <= 0.11 and grown(p, 0.044) and not grown(p, 0.022)


def mail_in(p):
    if not 0.55 <= p.z <= 0.80:
        return False
    if not grown(p, 0.045) or shirt_in(p):
        return False
    return not (p.y < 0 and abs(p.x) < 0.015 and p.z < 0.70)


def mail_color(i, j, k, n, nw, c):
    if c.z < 0.57 or abs(nw.z) > 0.7:
        return STEEL_DARK, (0, 0, 0)
    a, b = face_axes(nw)
    idx = (i, j, k)
    if idx[b] % 2:
        return MAIL[1], (0, 0, 0)
    return (MAIL[0] if (idx[a] + idx[b] // 2) % 2 else MAIL[2]), (0, 0, 0)


# ------------------------------------------------------------------ arms
def limb_in(half_w, half_d, t0, t1, n=4.0):
    def f(lp, wp):
        if not t0 <= lp.z <= t1:
            return False
        hw = half_w(lp.z) if callable(half_w) else half_w
        hd = half_d(lp.z) if callable(half_d) else half_d
        return superellipse(lp.x, lp.y, hw, hd, n) <= 1.0
    return f


def sleeve_color(i, j, k, n, nw, c, t_of):
    t = t_of(c)
    col = plaid(i if abs(n.x) < 0.5 else j, k)
    if t > 0.255:
        col = shade(col, 0.74)
    under = LINEN if t < 0.15 else SKIN
    return col, under


def skin_color(i, j, k, n, nw, c):
    return SKIN, SKIN


def fist_in(lp, wp):
    x, y, z = lp.x, lp.y, lp.z
    if -0.01 <= z <= 0.20 and abs(x) <= 0.058 and abs(y) <= 0.072:
        return not (z > 0.17 and abs(y) > 0.05)
    return False


def thumb_in(side):
    def f(lp, wp):
        return 0.02 <= lp.z <= 0.10 and -0.10 <= lp.y <= -0.06 and abs(lp.x) <= 0.035
    return f


def fist_color(i, j, k, n, nw, c):
    col = SKIN
    if n.y < -0.5 or n.x != 0:
        if k % 4 == 0 and k > 1:
            col = SKIN_DARK
    return col, col


def bracer_in(lp, wp):
    t = lp.z
    if not 0.10 <= t <= 0.25:
        return False
    hw = 0.084 - 0.02 * t + (0.012 if t < 0.125 or t > 0.225 else 0.0)
    return superellipse(lp.x, lp.y, hw, hw * 1.05) <= 1.0 and superellipse(lp.x, lp.y, hw - 0.022, hw * 1.05 - 0.022) > 1.0


def bracer_color(i, j, k, n, nw, c, t_of):
    t = t_of(c)
    if t < 0.125 or t > 0.225:
        return (STEEL_LIGHT if (i + j) % 4 == 0 else grain(STEEL_DARK, i, j, k, 0.08)), (0, 0, 0)
    return (mix(LEATHER[1], THREAD, 0.35) if (i + k) % 6 == 0 else grain(LEATHER[1], i, j, k)), (0, 0, 0)


# ------------------------------------------------------------------ legs and boots
def leg_in(side):
    x0 = 0.165 if side == "Left" else -0.165

    def f(wp):
        z = wp.z
        if not 0.10 <= z <= 0.80:
            return False
        hw = 0.100 if z > 0.46 else 0.096
        hd = 0.112 if z > 0.46 else 0.106
        if z > 0.62:
            hw, hd = 0.094, 0.100
        return superellipse(wp.x - x0, wp.y, hw, hd) <= 1.0
    return f


def leg_color(i, j, k, n, nw, c):
    col = TROUSERS
    side_x = 0.165 if c.x > 0 else -0.165
    if nw.y < -0.5 and 0.38 < c.z < 0.53 and abs(c.x - side_x) < 0.075:
        edge = c.z < 0.40 or c.z > 0.51 or abs(c.x - side_x) > 0.055
        col = SEAM if edge else KNEE_PAD
    elif abs(nw.x) > 0.5 and (c.x - side_x) * nw.x > 0 and i % 5 == 0:
        col = TROUSERS_DARK
    under = SHORTS if c.z > 0.56 else SKIN
    return grain(col, i // 3, j // 3, k // 3, 0.05), under


def boot_in(side):
    x0 = 0.165 if side == "Left" else -0.165

    def f(wp):
        x, y, z = wp.x - x0, wp.y, wp.z
        if 0.0 <= z <= 0.20 and superellipse(x, y, 0.118, 0.128) <= 1.0:
            return True
        if 0.0 <= z <= 0.12 and abs(x) <= 0.128 and -0.27 <= y <= 0.0:
            return not (z > 0.08 and y < -0.20)
        if 0.12 <= z <= 0.20 and superellipse(x, y, 0.134, 0.130) <= 1.0:
            return True
        if 0.21 <= z <= 0.245 and superellipse(x, y, 0.118, 0.126) <= 1.0:
            return True
        if 0.26 <= z <= 0.38 and superellipse(x, y, 0.126, 0.134) <= 1.0:
            return True
        return False
    return f


def boot_color(i, j, k, n, nw, c):
    z = c.z
    under = SKIN
    if z < 0.025:
        return SOLE, SKIN_SHADE
    if 0.26 <= z <= 0.38:
        col = mix(WRAP, THREAD, 0.3) if z > 0.36 or abs(z - 0.32) < 0.01 else WRAP
        return col, col
    if 0.21 <= z <= 0.245:
        return WRAP, WRAP
    if 0.17 <= z <= 0.20:
        return BOOT_DARK, under
    if c.y < -0.20 and z < 0.12:
        return BOOT_DARK, under
    return grain(BOOT, i // 3, j // 3, k // 3, 0.05), under


# ------------------------------------------------------------------ head, hair, beard
HEAD_X, HEAD_Y0, HEAD_Y1, HEAD_Z0, HEAD_Z1 = 0.165, -0.18, 0.16, 1.42, 1.72


def head_in(lp, wp):
    x, y, z = wp
    if HEAD_Z0 <= z <= HEAD_Z1 and abs(x) <= HEAD_X and HEAD_Y0 <= y <= HEAD_Y1:
        r = 0.03
        cx = max(0.0, abs(x) - (HEAD_X - r))
        cy = max(0.0, max(HEAD_Y0 + r - y, y - (HEAD_Y1 - r)))
        cz = max(0.0, max(HEAD_Z0 + r - z, z - (HEAD_Z1 - r)))
        if cx * cx + cy * cy + cz * cz > r * r:
            return False
        return True
    if abs(x) <= 0.045 and -0.235 <= y <= HEAD_Y0 and 1.495 <= z <= 1.60:
        return not (y < -0.215 and z > 1.575) and not (abs(x) > 0.03 and y < -0.22)
    if abs(x) <= 0.13 and abs(abs(x) - 0.075) <= 0.055 and -0.205 <= y <= HEAD_Y0 and 1.615 <= z <= 1.645 + 0.25 * max(0.0, abs(x) - 0.04):
        return True
    if HEAD_X <= abs(x) <= HEAD_X + 0.02 and -0.02 <= y <= 0.05 and 1.52 <= z <= 1.61:
        return True
    return False


def head_color(i, j, k, n, nw, c):
    x, y, z = c
    if y < HEAD_Y0 - 0.002 and z > 1.612 and abs(x) > 0.02:
        return grain(GINGER_DARK, i, j, k, 0.1), (0, 0, 0)
    if nw.y < -0.5 and y < HEAD_Y0 + V_HEAD * 1.5:
        ax = abs(x)
        if 1.578 <= z <= 1.607 and 0.045 <= ax <= 0.105:
            if 0.062 <= ax <= 0.088 and z < 1.600:
                return (PUPIL if 0.070 <= ax <= 0.080 else IRIS), (0, 0, 0)
            if z > 1.596:
                return SKIN_DARK, (0, 0, 0)
            return EYE_WHITE, (0, 0, 0)
        if 1.60 <= z <= 1.615 and 0.04 <= ax <= 0.11:
            return SKIN_SHADE, (0, 0, 0)
    if abs(x) <= 0.046 and y < HEAD_Y0:
        return (SKIN_DARK if nw.z < -0.5 else SKIN_SHADE if abs(nw.x) > 0.5 else SKIN), (0, 0, 0)
    if nw.y < -0.5 and abs(x) > 0.11 and 1.50 <= z <= 1.57:
        return SKIN_SHADE, (0, 0, 0)
    return SKIN, (0, 0, 0)


def hair_in(lp, wp):
    x, y, z = wp
    if head_in(lp, wp):
        return False
    lump = (h3(math.floor(x / 0.05), math.floor(y / 0.05), math.floor(z / 0.05)) - 0.5) * 0.03
    cz = 1.655
    dome = (abs(x) / (0.205 + lump)) ** 2.6 + (abs(y + 0.005) / (0.205 + lump)) ** 2.6 + (max(0.0, z - cz) / 0.145) ** 2.6
    if z >= cz - 0.01 and dome <= 1.0:
        if y < -0.16 and z < 1.70 and abs(x) < 0.15:
            return False
        return z <= 1.80
    if 1.44 <= z < cz and y > -0.07 and abs(x) <= 0.205 + lump and y <= 0.20 + lump:
        if abs(x) <= HEAD_X - 0.01 and y < HEAD_Y1 - 0.01:
            return False
        bottom = 1.47 - 0.05 * smoothstep(-0.05, 0.12, y) - 0.02 * h3(math.floor(x / 0.04), 0, 7)
        return z >= bottom
    return False


def hair_color(i, j, k, n, nw, c):
    a = h3(i // 2, j // 2, k // 3)
    col = GINGER_DARK if a < 0.22 else GINGER_LIGHT if a > 0.82 else GINGER
    if nw.z < -0.5:
        col = GINGER_DARK
    return grain(col, i, j, k, 0.07), (0, 0, 0)


def vest_front_y(z):
    return -(interp(SHIRT, z, 2) + 0.026)


def beard_in(lp, wp):
    x, y, z = wp
    if 1.452 <= z <= 1.497 and abs(x) <= 0.125 and -0.236 <= y <= -0.18:
        return True
    if not 1.13 <= z <= 1.54:
        return False
    t = (1.54 - z) / 0.41
    half = 0.175 - 0.115 * t * t
    lump = (h3(math.floor(x / 0.04), 7, math.floor(z / 0.04)) - 0.5) * 0.03
    if abs(x) > half + lump:
        return False
    back = vest_front_y(z) - 0.05 if z < 1.42 else -0.12
    front = min(back - 0.08, -0.215 - 0.11 * min(1.0, t * 2.2)) + lump
    if z > 1.465 and abs(x) < 0.105 + 0.6 * max(0.0, 1.50 - z) and y < -0.17:
        return False
    if z > 1.42 and abs(x) <= HEAD_X - 0.005 and y > HEAD_Y0:
        return False
    return front <= y <= back


def braid_in(lp, wp):
    x, y, z = wp
    yb = vest_front_y(1.10) - 0.05
    return 1.04 <= z <= 1.15 and abs(x) <= 0.026 and yb - 0.04 <= y <= yb


def beard_color(i, j, k, n, nw, c):
    if 1.45 <= c.z <= 1.50 and c.y < -0.17 and abs(c.x) <= 0.125:
        return grain(MOUSTACHE, i, j, k, 0.06), (0, 0, 0)
    if c.z < 1.07:
        return BEAD, (0, 0, 0)
    a = h3(i // 2, j // 2, k // 3)
    col = GINGER_DARK if a < 0.25 else GINGER_LIGHT if a > 0.85 else GINGER
    return grain(col, i, j, k, 0.07), (0, 0, 0)


# ------------------------------------------------------------------ assembly
ALL_BONES = [n for n, b in JOINTS.items() if b["deform"]]


def arm_parts(side):
    s = 1.0 if side == "Left" else -1.0
    S = J(side + "UpperArm")
    up = bone_frame(side + "UpperArm", V_BODY)
    lo = bone_frame(side + "LowerArm", V_BODY)
    hand = bone_frame(side + "Hand", 0.0125)
    d = (J(side + "LowerArm") - S).normalized()

    def t_of(p):
        return (p - S).dot(d)

    def sleeve_w(z):
        return 0.098 if z < 0.255 else 0.112

    sleeve = Part("chopper_shirt", up, limb_in(sleeve_w, sleeve_w, -0.08, 0.33), lambda i, j, k, n, nw, c: sleeve_color(i, j, k, n, nw, c, t_of),
                  arm_w(side), ((-0.13, -0.13, -0.08), (0.13, 0.13, 0.33)))
    fore = Part("chopper_body", lo, limb_in(lambda z: 0.072 - 0.03 * z, lambda z: 0.074 - 0.03 * z, -0.02, 0.28), skin_color,
                arm_w(side), ((-0.09, -0.09, -0.02), (0.09, 0.09, 0.28)))
    fist = Part("chopper_body", hand, fist_in, fist_color, arm_w(side), ((-0.07, -0.08, -0.02), (0.07, 0.08, 0.21)))
    thumb = Part("chopper_body", hand, thumb_in(side), fist_color, arm_w(side), ((-0.04, -0.11, 0.0), (0.04, -0.05, 0.11)))
    bracer = Part("chopper_bracers", lo, bracer_in, lambda i, j, k, n, nw, c: bracer_color(i, j, k, n, nw, c, lambda p: (p - J(side + "LowerArm")).dot((J(side + "Hand") - J(side + "LowerArm")).normalized())),
                  arm_w(side), ((-0.11, -0.11, 0.09), (0.11, 0.11, 0.26)))
    return [sleeve, fore, fist, thumb, bracer]


def world_part(obj, v, inside, color, weights, lo, hi):
    return Part(obj, Frame((0, 0, 0), WORLD, v), lambda lp, wp: inside(wp), color, weights, (lo, hi))


def main():
    L.reset_scene()
    mat = L.make_material("M_Chopper", (0.8, 0.8, 0.8))
    m_gear = L.make_material("M_Chopper_Gear", (0.6, 0.6, 0.6))
    body_lo, body_hi = (-0.45, -0.32, 0.50), (0.45, 0.32, 1.45)
    parts = {
        "chopper_shirt": [world_part("chopper_shirt", V_BODY, shirt_in, shirt_color, torso_w, body_lo, body_hi)],
        "chopper_vest": [world_part("chopper_vest", V_BODY, vest_in, vest_color, torso_w, body_lo, body_hi),
                         world_part("chopper_vest", V_BODY, strap_in, strap_color, torso_w, body_lo, (0.45, 0.32, 1.46))],
        "chopper_belt": [world_part("chopper_belt", V_BODY, belt_in, belt_color, torso_w, body_lo, body_hi),
                         world_part("chopper_belt", V_BODY, pouch_in, pouch_color, torso_w, body_lo, body_hi),
                         world_part("chopper_belt", V_BODY, tongue_in, pouch_color, torso_w, body_lo, body_hi)],
        "chopper_chainmail": [world_part("chopper_chainmail", V_BODY, mail_in, mail_color, torso_w, body_lo, body_hi)],
        "chopper_body": [world_part("chopper_body", V_BODY, lambda p: 1.36 <= p.z <= 1.46 and p.x * p.x + p.y * p.y <= 0.095 ** 2, skin_color,
                                    lambda p: {"Neck": 1.0}, (-0.1, -0.1, 1.36), (0.1, 0.1, 1.46))],
        "chopper_boots": [],
        "chopper_bracers": [],
        "chopper_head": [Part("chopper_head", Frame((0, 0, 0), WORLD, V_HEAD), head_in, head_color, head_w,
                              ((-0.19, -0.23, 1.42), (0.19, 0.17, 1.73)))],
        "chopper_hair": [Part("chopper_hair", Frame((0, 0, 0), WORLD, V_HAIR), hair_in, hair_color, head_w,
                              ((-0.24, -0.24, 1.40), (0.24, 0.24, 1.81)))],
        "chopper_beard": [Part("chopper_beard", Frame((0, 0, 0), WORLD, V_HAIR), beard_in, beard_color, head_w,
                               ((-0.2, -0.42, 1.12), (0.2, -0.1, 1.55))),
                          Part("chopper_beard", Frame((0, 0, 0), WORLD, V_HAIR), braid_in, beard_color, head_w,
                               ((-0.04, -0.42, 1.03), (0.04, -0.2, 1.16)))],
    }
    for side in ("Left", "Right"):
        for p in arm_parts(side):
            parts[p.obj].append(p)
        parts["chopper_body"].append(world_part("chopper_body", V_BODY, leg_in(side), leg_color, leg_w(side),
                                                (-0.3, -0.15, 0.09), (0.3, 0.15, 0.81)))
        parts["chopper_boots"].append(world_part("chopper_boots", V_BODY, boot_in(side), boot_color, leg_w(side),
                                                 (-0.32, -0.29, 0.0), (0.32, 0.15, 0.39)))
    for name, plist in parts.items():
        for p in plist:
            p.fill()
        ob = build_object(name, plist, mat, ALL_BONES)
        print(f"built {name}: {len(ob.data.polygons) * 2} tris")
    for fn in (BC.build_helmet, BC.build_plate, BC.build_pauldrons):
        ob = fn(m_gear)
        if ob.name != "chopper_helmet":
            for vert in ob.data.vertices:
                vert.co = BC.bulk_point(vert.co)
            ob.data.update()
        print(f"built {ob.name}: {L.tri_count(ob)} tris")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("saved", OUT)


if __name__ == "__main__":
    main()
