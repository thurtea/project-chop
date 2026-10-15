"""Tree elemental on Chopper's rig: the night forest enemy (Treant's golem look).

Run:  blender -b -P build_elemental.py
In:   Art/Blender/chopper_rigged.blend (rig and takes, read only)
Out:  Art/Blender/chopper_elemental.blend, Art/Export/chopper_elemental.fbx

Bark is the shared wood body (neutral and earth): muscled torso, antlers, root feet,
wooden axe and carved shield. Leaves0 adds the willow crown, blue eyes and leaf skirt.
Leaves3 is a moss crown and amber eyes on that same body. Leaves1, Leaves2 and Leaves4
are full water, fire and wind bodies. Axe boxes sit in the Weapon_R frame and are
weighted to RightHand. Shield boxes sit in the LeftHand frame.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chopper_lib as L
import export_fbx as E
from rig_chopper import attach

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
RIGGED = os.path.join(ART, "Blender", "chopper_rigged.blend")
OUT_BLEND = os.path.join(ART, "Blender", "chopper_elemental.blend")
OUT_FBX = os.path.join(ART, "Export", "chopper_elemental.fbx")


def S(r, g, b):
    return (r / 255.0, g / 255.0, b / 255.0)


WOOD = dict(body=S(118, 76, 44), dark=S(68, 42, 26), light=S(158, 114, 70), muscle=S(140, 98, 58),
            socket=S(24, 14, 8), antler=S(96, 64, 38), thorn=S(82, 54, 32),
            haft=S(96, 62, 36), cap=S(52, 34, 22), axe=S(64, 72, 82),
            blade=S(120, 210, 255), edge=S(210, 245, 255), rune=S(170, 235, 255),
            shield=S(150, 106, 64), carve=S(90, 58, 34), rim=S(62, 40, 24),
            eye=S(80, 210, 255), leaf=S(48, 96, 40), leaf2=S(72, 122, 52), leaf3=S(34, 72, 30))
FIRE = dict(body=S(36, 26, 28), dark=S(16, 12, 14), light=S(62, 40, 34), muscle=S(48, 30, 28),
            socket=S(10, 6, 6), antler=S(28, 16, 14), thorn=S(22, 14, 14),
            haft=S(32, 20, 18), cap=S(18, 12, 12), axe=S(48, 28, 22),
            blade=S(255, 120, 28), edge=S(255, 210, 80), rune=S(255, 70, 16),
            shield=S(32, 24, 26), carve=S(255, 64, 18), rim=S(14, 10, 12),
            eye=S(255, 230, 120), leaf=S(160, 36, 12), leaf2=S(255, 110, 24), leaf3=S(255, 200, 60),
            crack=S(255, 100, 20))
WATER = dict(body=S(120, 184, 220), dark=S(64, 114, 164), light=S(196, 230, 248), muscle=S(150, 204, 232),
             socket=S(36, 64, 100), antler=S(90, 150, 190), thorn=S(80, 140, 186),
             haft=S(78, 138, 178), cap=S(50, 96, 140), axe=S(90, 160, 200),
             blade=S(170, 230, 255), edge=S(230, 250, 255), rune=S(245, 252, 255),
             shield=S(96, 162, 208), carve=S(210, 242, 255), rim=S(52, 100, 150),
             eye=S(230, 252, 255), leaf=S(140, 206, 236), leaf2=S(186, 226, 248), leaf3=S(230, 248, 255),
             crack=S(214, 244, 255))
WIND = dict(body=S(188, 214, 230), dark=S(138, 168, 194), light=S(232, 244, 252), muscle=S(206, 226, 240),
            socket=S(90, 114, 140), antler=S(170, 198, 220), thorn=S(160, 190, 214),
            haft=S(168, 196, 216), cap=S(120, 150, 176), axe=S(176, 206, 224),
            blade=S(214, 236, 250), edge=S(245, 252, 255), rune=S(230, 246, 255),
            shield=S(176, 206, 226), carve=S(230, 244, 255), rim=S(110, 142, 170),
            eye=S(230, 250, 255), leaf=S(176, 208, 226), leaf2=S(206, 228, 240), leaf3=S(240, 248, 255),
            crack=S(220, 240, 252))
MOSS = (S(86, 110, 42), S(120, 132, 52), S(64, 88, 36))
EARTH_EYE = S(255, 196, 70)


def box(mb, c, size, color, bevel=0.01):
    mb.box(c, size, bevel, color=color)


def along(mb, a, b, width, depth, color, extend=0.0, bevel=0.01):
    a, b = Vector(a), Vector(b)
    d = b - a
    if d.length < 1e-4:
        return
    d = d.normalized()
    a, b = a - d * extend, b + d * extend
    u, v, w = L.basis(d, Vector((0, -1, 0)) if abs(d.z) < 0.9 else Vector((1, 0, 0)))
    mb.box((a + b) / 2, (width, depth, (b - a).length), bevel, rot=L.rot_from(u, v, w), color=color)


class Rig:
    def __init__(self, rig):
        self.rig = rig
        mw = rig.matrix_world
        self.head = {b.name: mw @ b.head_local for b in rig.data.bones}
        self.tail = {b.name: mw @ b.tail_local for b in rig.data.bones}
        self.frame = {b.name: mw @ b.matrix_local for b in rig.data.bones}


class Parts:
    def __init__(self, mat):
        self.mat = mat
        self.pending = {}

    def add(self, group, bone, build):
        self.pending.setdefault((group, bone), []).append(build)

    def put(self, group, bone, c, size, color, bevel=0.012):
        self.add(group, bone, lambda mb, c=tuple(c), size=tuple(size), color=color, bevel=bevel:
                 box(mb, c, size, color, bevel))

    def limb(self, group, bone, a, b, w, d, color, extend=0.0):
        self.add(group, bone, lambda mb, a=tuple(a), b=tuple(b), w=w, d=d, color=color, extend=extend:
                 along(mb, a, b, w, d, color, extend))

    def put_local(self, group, bone, frame, center, size, color, bevel=0.008):
        c = frame @ Vector((center[0], center[1], center[2]))
        rot = frame.to_3x3().copy()
        ct = (c.x, c.y, c.z)
        self.add(group, bone, lambda mb, ct=ct, size=tuple(size), color=color, bevel=bevel, rot=rot:
                 mb.box(ct, size, bevel, rot=rot, color=color))

    def join(self, rig):
        grouped = {}
        for (group, bone), builds in self.pending.items():
            mb = L.MeshBuilder("%s_%s" % (group, bone))
            for build in builds:
                build(mb)
            ob = mb.finish(self.mat)
            vg = ob.vertex_groups.new(name=bone)
            vg.add(list(range(len(ob.data.vertices))), 1.0, "REPLACE")
            grouped.setdefault(group, []).append(ob)
        out = []
        for name, obs in grouped.items():
            bpy.ops.object.select_all(action="DESELECT")
            for ob in obs:
                ob.select_set(True)
            bpy.context.view_layer.objects.active = obs[0]
            if len(obs) > 1:
                bpy.ops.object.join()
            ob = bpy.context.view_layer.objects.active
            ob.name = name
            ob.data.name = name
            attach(rig, ob)
            out.append(ob)
        return out


def torso(P, g, pal):
    P.put(g, "Hips", (0, 0.02, 0.78), (0.38, 0.26, 0.18), pal["body"], 0.02)
    P.put(g, "Spine", (0, 0.015, 0.95), (0.32, 0.24, 0.18), pal["dark"], 0.016)
    P.put(g, "Chest", (0, 0.0, 1.12), (0.46, 0.30, 0.20), pal["body"], 0.02)
    P.put(g, "UpperChest", (0, 0.0, 1.30), (0.54, 0.32, 0.16), pal["body"], 0.018)
    P.put(g, "Neck", (0, 0.0, 1.40), (0.15, 0.14, 0.10), pal["dark"], 0.012)
    for s in (-1, 1):
        P.put(g, "Chest", (s * 0.11, -0.15, 1.20), (0.16, 0.08, 0.11), pal["muscle"], 0.012)
        P.put(g, "UpperChest", (s * 0.20, -0.04, 1.32), (0.14, 0.16, 0.10), pal["light"], 0.012)
    for i, z in enumerate((1.02, 0.94, 0.86)):
        for s in (-1, 1):
            P.put(g, "Spine" if z < 1.0 else "Chest", (s * 0.055, -0.125, z), (0.07, 0.045, 0.055), pal["muscle"], 0.006)
    P.put(g, "Chest", (0, -0.145, 1.08), (0.03, 0.03, 0.28), pal["dark"], 0.004)
    P.put(g, "Hips", (0, -0.12, 0.80), (0.10, 0.04, 0.06), pal["light"], 0.008)


def head(P, g, pal, eyes, eye_color):
    P.put(g, "Head", (0, -0.02, 1.58), (0.30, 0.28, 0.34), pal["body"], 0.02)
    P.put(g, "Head", (0, -0.145, 1.70), (0.26, 0.07, 0.05), pal["light"], 0.01)
    P.put(g, "Head", (0, -0.10, 1.46), (0.20, 0.18, 0.12), pal["dark"], 0.012)
    P.put(g, "Head", (0, -0.20, 1.56), (0.055, 0.07, 0.09), pal["light"], 0.008)
    P.put(g, "Head", (0, -0.185, 1.47), (0.11, 0.03, 0.025), pal["socket"], 0.004)
    for s in (-1, 1):
        P.put(g, "Head", (s * 0.075, -0.175, 1.615), (0.07, 0.03, 0.04), pal["socket"], 0.004)
        P.put(g, "Head", (s * 0.11, -0.08, 1.52), (0.05, 0.06, 0.12), pal["light"], 0.008)
        if eyes:
            P.put(g, "Head", (s * 0.075, -0.205, 1.62), (0.06, 0.025, 0.038), eye_color, 0.003)


def antlers(P, g, pal):
    P.limb(g, "Head", (0, -0.02, 1.70), (0, -0.02, 2.02), 0.045, 0.045, pal["antler"])
    P.limb(g, "Head", (0, -0.02, 1.90), (-0.10, -0.06, 2.08), 0.03, 0.03, pal["antler"])
    P.limb(g, "Head", (0, -0.02, 1.90), (0.10, -0.06, 2.08), 0.03, 0.03, pal["antler"])
    for s in (-1, 1):
        P.limb(g, "Head", (s * 0.08, -0.02, 1.66), (s * 0.22, -0.02, 1.98), 0.05, 0.045, pal["antler"])
        P.limb(g, "Head", (s * 0.16, -0.02, 1.84), (s * 0.36, -0.08, 2.02), 0.035, 0.032, pal["light"])
        P.limb(g, "Head", (s * 0.18, -0.02, 1.90), (s * 0.30, 0.08, 1.78), 0.028, 0.028, pal["antler"])
        P.limb(g, "UpperChest", (s * 0.22, -0.02, 1.34), (s * 0.36, -0.16, 1.52), 0.035, 0.03, pal["thorn"])
        P.limb(g, "UpperChest", (s * 0.16, -0.04, 1.26), (s * 0.26, -0.14, 1.40), 0.028, 0.024, pal["thorn"])


def limbs(P, R, g, pal):
    for side, s in (("Left", 1), ("Right", -1)):
        sh = R.head[side + "UpperArm"]
        el = R.head[side + "LowerArm"]
        wr = R.head[side + "Hand"]
        P.put(g, side + "Shoulder", sh + Vector((-s * 0.02, 0.0, 0.02)), (0.16, 0.16, 0.14), pal["body"], 0.016)
        P.limb(g, side + "UpperArm", sh, el, 0.13, 0.14, pal["body"], 0.03)
        mid = sh.lerp(el, 0.45)
        P.put(g, side + "UpperArm", mid + Vector((0, -0.04, 0.02)), (0.08, 0.06, 0.10), pal["muscle"], 0.01)
        P.limb(g, side + "LowerArm", el, wr, 0.11, 0.12, pal["dark"], 0.02)
        P.put(g, side + "LowerArm", el.lerp(wr, 0.55) + Vector((0, -0.03, 0)), (0.07, 0.05, 0.08), pal["light"], 0.008)
        hip, kn = R.head[side + "UpperLeg"], R.head[side + "LowerLeg"]
        an = R.head[side + "Foot"]
        P.limb(g, side + "UpperLeg", hip, kn, 0.16, 0.17, pal["body"], 0.03)
        P.limb(g, side + "LowerLeg", kn, an, 0.13, 0.14, pal["dark"], 0.02)
        P.put(g, side + "LowerLeg", kn + Vector((0, -0.06, 0.02)), (0.10, 0.06, 0.08), pal["light"], 0.008)
        P.put(g, side + "Foot", (an.x, -0.02, 0.08), (0.16, 0.18, 0.10), pal["body"], 0.012)
        for dx, dy in ((-0.10, -0.16), (0.02, -0.22), (0.12, -0.14), (s * 0.10, 0.06)):
            end = (an.x + dx, -0.02 + dy, 0.025)
            P.limb(g, side + "Foot", (an.x, -0.02, 0.07), end, 0.055, 0.04, pal["dark"])


def fists(P, R, g, pal):
    for side in ("Right", "Left"):
        wr, ht = R.head[side + "Hand"], R.tail[side + "Hand"]
        P.put(g, side + "Hand", wr.lerp(ht, 0.35), (0.11, 0.09, 0.10), pal["body"], 0.012)
        P.put(g, side + "Hand", wr.lerp(ht, 0.7), (0.08, 0.07, 0.07), pal["dark"], 0.008)


def cracks(P, g, color, bones=True):
    pts = [(0.0, 1.30), (-0.04, 1.18), (0.05, 1.06), (-0.03, 0.94), (0.02, 0.82)]
    names = ["UpperChest", "Chest", "Chest", "Spine", "Hips"]
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        P.limb(g, names[i], (a[0], -0.17, a[1]), (b[0], -0.17, b[1]), 0.028, 0.016, color)
    if not bones:
        return
    for s in (-1, 1):
        P.limb(g, "Chest", (s * 0.08, -0.16, 1.22), (s * 0.16, -0.15, 1.08), 0.02, 0.012, color)
        P.limb(g, "UpperChest", (s * 0.18, -0.08, 1.28), (s * 0.28, -0.06, 1.16), 0.018, 0.012, color)


def axe(P, frame, g, pal):
    bone = "RightHand"
    P.put_local(g, bone, frame, (0, 0, 0.16), (0.055, 0.055, 0.46), pal["haft"], 0.008)
    P.put_local(g, bone, frame, (0, 0, -0.10), (0.07, 0.07, 0.07), pal["cap"], 0.008)
    P.put_local(g, bone, frame, (0, 0, 0.40), (0.10, 0.08, 0.16), pal["axe"], 0.01)
    P.put_local(g, bone, frame, (0.11, 0, 0.40), (0.14, 0.045, 0.22), pal["blade"], 0.008)
    P.put_local(g, bone, frame, (0.20, 0, 0.38), (0.08, 0.032, 0.14), pal["edge"], 0.006)
    P.put_local(g, bone, frame, (0.16, 0, 0.50), (0.07, 0.03, 0.06), pal["blade"], 0.005)
    P.put_local(g, bone, frame, (-0.08, 0, 0.40), (0.08, 0.055, 0.09), pal["axe"], 0.008)
    P.put_local(g, bone, frame, (0.12, -0.03, 0.40), (0.10, 0.012, 0.012), pal["rune"], 0.002)
    P.put_local(g, bone, frame, (0.14, -0.03, 0.44), (0.012, 0.012, 0.08), pal["rune"], 0.002)
    P.put_local(g, bone, frame, (0.10, -0.03, 0.36), (0.012, 0.012, 0.06), pal["rune"], 0.002)


def shield(P, frame, g, pal, kind):
    bone = "LeftHand"
    P.put_local(g, bone, frame, (0.0, 0.02, 0.14), (0.40, 0.54, 0.07), pal["shield"], 0.012)
    P.put_local(g, bone, frame, (0.0, 0.02, 0.11), (0.44, 0.58, 0.03), pal["rim"], 0.01)
    if kind == "leaf":
        P.put_local(g, bone, frame, (0.0, 0.02, 0.185), (0.03, 0.28, 0.012), pal["carve"], 0.003)
        P.put_local(g, bone, frame, (-0.07, 0.08, 0.185), (0.12, 0.06, 0.012), pal["carve"], 0.003)
        P.put_local(g, bone, frame, (0.07, -0.02, 0.185), (0.12, 0.05, 0.012), pal["carve"], 0.003)
        P.put_local(g, bone, frame, (-0.05, -0.10, 0.185), (0.10, 0.045, 0.012), pal["carve"], 0.003)
    elif kind == "brand":
        P.put_local(g, bone, frame, (0.0, 0.04, 0.185), (0.16, 0.03, 0.012), pal["carve"], 0.003)
        P.put_local(g, bone, frame, (0.0, -0.02, 0.185), (0.03, 0.22, 0.012), pal["carve"], 0.003)
        P.put_local(g, bone, frame, (-0.08, 0.10, 0.185), (0.10, 0.03, 0.012), pal["carve"], 0.003)
        P.put_local(g, bone, frame, (0.08, 0.10, 0.185), (0.10, 0.03, 0.012), pal["carve"], 0.003)
    elif kind == "swirl":
        for k in range(7):
            a = k * 0.85
            r = 0.04 + k * 0.018
            P.put_local(g, bone, frame, (math.cos(a) * r, 0.02 + math.sin(a) * r, 0.185),
                        (0.06, 0.035, 0.012), pal["carve"], 0.003)
    else:
        for k in range(6):
            a = k * 0.95
            r = 0.03 + k * 0.02
            P.put_local(g, bone, frame, (math.cos(a) * r * 0.8, 0.04 + math.sin(a) * r, 0.185),
                        (0.07, 0.04, 0.012), pal["carve"], 0.003)


def gear(P, R, g, pal, kind):
    axe(P, R.frame["Weapon_R"], g, pal)
    shield(P, R.frame["LeftHand"], g, pal, kind)


def hanging(P, g, cols, origin, count, drop, spread):
    ox, oy, oz = origin
    for i in range(count):
        t = i / max(1, count - 1)
        x = ox + spread * (t - 0.5) * 2.0
        y = oy + (0.04 if i % 2 == 0 else -0.02)
        z0 = oz - 0.04 * t
        steps = 6
        for k in range(steps):
            z = z0 - k * (drop / steps)
            xx = x + (0.02 if ox >= 0 else -0.02) * k
            col = cols[(i + k) % len(cols)]
            P.put(g, "Head", (xx, y, z), (0.08, 0.04, drop / steps + 0.03), col, 0.006)


def willow(P, g, cols):
    for s in (-1, 1):
        P.put(g, "Head", (s * 0.28, -0.04, 2.02), (0.16, 0.12, 0.14), cols[1], 0.01)
        P.limb(g, "Head", (s * 0.16, -0.02, 1.86), (s * 0.30, -0.04, 2.00), 0.045, 0.035, cols[2])
        hanging(P, g, cols, (s * 0.32, -0.06, 1.96), 5, 0.88, 0.14)


def flame_crown(P, g, pal):
    cols = (pal["leaf"], pal["leaf2"], pal["leaf3"], pal["edge"])
    willow(P, g, cols)
    P.put(g, "Head", (0, -0.16, 1.72), (0.10, 0.08, 0.16), pal["leaf3"], 0.008)
    for i in range(3):
        P.put(g, "Head", (0, -0.18, 1.78 + i * 0.08), (0.08 - i * 0.015, 0.06, 0.08), cols[min(i + 1, 3)], 0.006)


def water_hair(P, g, pal):
    cols = (pal["leaf"], pal["leaf2"], pal["leaf3"], pal["light"])
    for i in range(6):
        t = i / 5.0
        pts = []
        for k in range(7):
            f = k / 6.0
            x = -0.05 + t * 0.12 + f * (0.15 + t * 0.55)
            y = -0.12 + f * 0.15 + math.sin(f * math.pi) * 0.08
            z = 1.68 + math.sin(f * math.pi) * (0.22 + t * 0.08) - f * 0.16
            pts.append((x, y, z))
        for k in range(6):
            P.limb(g, "Head", pts[k], pts[k + 1], 0.07 - k * 0.006, 0.045, cols[(i + k) % 4])
    for x in (-0.08, -0.03, 0.02, 0.07):
        P.limb(g, "Head", (x, -0.18, 1.46), (x * 1.4, -0.26, 1.08), 0.045, 0.035, pal["light"])
        P.limb(g, "Chest", (x * 1.2, -0.24, 1.10), (x * 1.5, -0.22, 0.90), 0.032, 0.026, pal["leaf2"])


def wind_crown(P, g, pal):
    cols = (pal["leaf3"], pal["light"], pal["leaf2"], pal["edge"])
    P.put(g, "Head", (0, -0.16, 1.72), (0.08, 0.05, 0.10), pal["edge"], 0.008)
    for k in range(18):
        a = k * 0.62
        r = 0.16 + (k % 6) * 0.045
        x = math.cos(a) * r * 1.15
        y = math.sin(a) * r * 0.42
        z = 1.86 + math.sin(a * 1.7) * 0.05
        P.put(g, "Head", (x, y, z), (0.22, 0.12, 0.05), cols[k % 4], 0.006)
    for x, y in ((-0.48, -0.05), (0.55, 0.20)):
        for k in range(4):
            w = 0.11 - k * 0.02
            P.put(g, "Hips", (x, y, 0.28 + k * 0.09), (w, w, 0.07), cols[k % 3], 0.006)


def moss_crown(P, g):
    for x in (-0.16, -0.05, 0.06, 0.16):
        for y, h in ((-0.06, 0.16), (0.06, 0.12), (0.14, 0.10)):
            P.put(g, "Head", (x, y, 1.78 + h * 0.3), (0.12, 0.10, h), MOSS[(abs(int(x * 10)) + int(y * 10)) % 3], 0.01)
    for s in (-1, 1):
        P.put(g, "Head", (s * 0.22, 0.02, 1.84), (0.14, 0.10, 0.12), MOSS[1], 0.01)
        P.put(g, side_bone(s), (s * 0.28, 0.02, 1.36), (0.12, 0.10, 0.06), MOSS[0], 0.008)


def side_bone(s):
    return "LeftShoulder" if s > 0 else "RightShoulder"


def skirt(P, g, cols, kind):
    n = 9 if kind != "cloth" else 6
    for i in range(n):
        a = (i / n) * math.tau
        x = math.cos(a) * 0.20
        y = math.sin(a) * 0.15
        drop = 0.26 if kind != "cloth" else 0.38
        wide = 0.09 if kind != "cloth" else 0.12
        P.limb(g, "Hips", (x, y, 0.82), (x * 1.45, y * 1.35, 0.82 - drop), wide, 0.03, cols[i % len(cols)])
        if kind == "flame":
            P.limb(g, "Hips", (x * 1.2, y * 1.15, 0.70), (x * 1.55, y * 1.4, 0.52), 0.04, 0.02, cols[-1])


def splash(P, R, g, pal):
    for side in ("Left", "Right"):
        an = R.head[side + "Foot"]
        for k in range(6):
            a = k * 1.05
            P.put(g, side + "Foot", (an.x + math.cos(a) * 0.16, -0.06 + math.sin(a) * 0.12, 0.05 + (k % 2) * 0.03),
                  (0.08, 0.06, 0.05), pal["leaf3"] if k % 2 == 0 else pal["light"], 0.006)
        P.limb(g, side + "Foot", (an.x - 0.18, 0.02, 0.04), (an.x + 0.20, -0.20, 0.09), 0.06, 0.035, pal["edge"])


def eyes_only(P, g, color):
    for s in (-1, 1):
        P.put(g, "Head", (s * 0.075, -0.205, 1.62), (0.06, 0.025, 0.038), color, 0.003)


def body(P, R, g, pal, eyes, kind):
    torso(P, g, pal)
    head(P, g, pal, eyes, pal["eye"])
    limbs(P, R, g, pal)
    fists(P, R, g, pal)
    if kind == "wood":
        antlers(P, g, pal)
    elif kind == "fire":
        antlers(P, g, pal)
        cracks(P, g, pal["crack"])
    elif kind == "water":
        cracks(P, g, pal["crack"], bones=False)
        for s in (-1, 1):
            for k in range(4):
                a = k * 0.9
                P.limb(g, "Chest", (s * 0.06, -0.16, 1.22),
                       (s * (0.10 + math.cos(a) * 0.08), -0.16, 1.22 - 0.08 - k * 0.04),
                       0.02, 0.012, pal["crack"])
    else:
        cracks(P, g, pal["crack"], bones=False)


def build_all(P, R):
    body(P, R, "Bark", WOOD, False, "wood")
    gear(P, R, "Bark", WOOD, "leaf")
    eyes_only(P, "Leaves0", WOOD["eye"])
    willow(P, "Leaves0", (WOOD["leaf"], WOOD["leaf2"], WOOD["leaf3"]))
    skirt(P, "Leaves0", (WOOD["leaf"], WOOD["leaf2"], WOOD["leaf3"]), "leaf")
    body(P, R, "Leaves1", WATER, True, "water")
    gear(P, R, "Leaves1", WATER, "swirl")
    water_hair(P, "Leaves1", WATER)
    skirt(P, "Leaves1", (WATER["light"], WATER["leaf2"], WATER["leaf3"]), "cloth")
    splash(P, R, "Leaves1", WATER)
    body(P, R, "Leaves2", FIRE, True, "fire")
    gear(P, R, "Leaves2", FIRE, "brand")
    flame_crown(P, "Leaves2", FIRE)
    skirt(P, "Leaves2", (FIRE["leaf"], FIRE["dark"], FIRE["leaf2"]), "flame")
    eyes_only(P, "Leaves3", EARTH_EYE)
    moss_crown(P, "Leaves3")
    body(P, R, "Leaves4", WIND, True, "wind")
    gear(P, R, "Leaves4", WIND, "wind")
    wind_crown(P, "Leaves4", WIND)
    skirt(P, "Leaves4", (WIND["leaf"], WIND["light"], WIND["leaf3"]), "cloth")


def main():
    bpy.ops.wm.open_mainfile(filepath=RIGGED)
    rig = bpy.data.objects["chopper_rig"]
    for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
        bpy.data.objects.remove(ob, do_unlink=True)
    mat = L.make_material("M_Elemental", (0.6, 0.6, 0.6))
    R = Rig(rig)
    P = Parts(mat)
    build_all(P, R)
    obs = P.join(rig)
    for ob in obs:
        print("built %s: %d tris" % (ob.name, L.tri_count(ob)))
    os.makedirs(os.path.dirname(OUT_BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    rig.animation_data.action = None
    rig.data.pose_position = "POSE"
    for tr in rig.animation_data.nla_tracks:
        tr.mute = False
        for strip in tr.strips:
            strip.extrapolation = "NOTHING"
    bpy.context.scene.frame_set(0)
    for pb in rig.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    E.select_only(["chopper_rig"] + [ob.name for ob in obs])
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, **E.CHAR_SETTINGS)
    print("exported", OUT_FBX, os.path.getsize(OUT_FBX), "bytes")


if __name__ == "__main__":
    main()
