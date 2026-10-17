"""Tree elemental on Chopper's rig: the night forest enemy (Treant's golem look).

Run:  blender -b -P build_elemental.py
In:   Art/Blender/chopper_rigged.blend (rig and takes, read only)
Out:  Art/Blender/chopper_elemental.blend, Art/Export/chopper_elemental.fbx

Bark is the shared wood body (neutral and earth): muscled torso, antlers, root feet,
a right arm shaped as an axe, and a carved shield. Leaves0 adds the willow crown, blue
eyes and leaf skirt. Leaves3 is a moss crown and amber eyes on that same body.
Leaves1, Leaves2 and Leaves4 are full water, fire and wind bodies, each with its own
right-arm weapon (spear, sword, axe); water and wind are the same willow tree spirit as the others,
recoloured (teal bark and willow with coral; tan bark and pale green willow), after
chopper-all-3D-concept-3.0 (user, 2026-10-17). Leaves3 is now earth's own stone body; the bark body carries the axe in Leaves0 and the mace in Leaves3. Weapon boxes are weighted to the arm bones.
Shield boxes sit in the LeftHand frame.
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


WOOD = dict(body=S(55, 37, 22), dark=S(31, 21, 13), light=S(80, 60, 37), muscle=S(67, 49, 30),
            socket=S(14, 9, 5), antler=S(58, 41, 24), thorn=S(49, 35, 21),
            haft=S(96, 62, 36), cap=S(52, 34, 22), axe=S(64, 72, 82),
            blade=S(120, 210, 255), edge=S(210, 245, 255), rune=S(170, 235, 255),
            shield=S(79, 51, 30), carve=S(48, 28, 16), rim=S(33, 19, 11),
            eye=S(80, 210, 255), leaf=S(31, 49, 19), leaf2=S(46, 63, 24), leaf3=S(22, 37, 14))
FIRE = dict(body=S(37, 19, 15), dark=S(16, 9, 8), light=S(63, 29, 18), muscle=S(49, 22, 15),
            socket=S(10, 4, 3), antler=S(29, 11, 8), thorn=S(23, 10, 8),
            haft=S(32, 20, 18), cap=S(18, 12, 12), axe=S(48, 28, 22),
            blade=S(255, 120, 28), edge=S(255, 210, 80), rune=S(255, 70, 16),
            shield=S(45, 19, 14), carve=S(255, 64, 18), rim=S(20, 8, 6),
            eye=S(255, 230, 120), leaf=S(164, 25, 6), leaf2=S(255, 76, 11), leaf3=S(255, 139, 28),
            crack=S(255, 100, 20))
WATER = dict(body=S(56, 109, 158), dark=S(30, 68, 118), light=S(91, 137, 178), muscle=S(69, 121, 166),
             socket=S(17, 38, 72), antler=S(42, 89, 136), thorn=S(37, 83, 133),
             haft=S(78, 138, 178), cap=S(50, 96, 140), axe=S(90, 160, 200),
             blade=S(170, 230, 255), edge=S(230, 250, 255), rune=S(245, 252, 255),
             shield=S(44, 84, 126), carve=S(210, 242, 255), rim=S(24, 52, 91),
             eye=S(230, 252, 255), leaf=S(82, 140, 174), leaf2=S(108, 154, 183), leaf3=S(134, 168, 188),
             crack=S(214, 244, 255))
WIND = dict(body=S(123, 184, 208), dark=S(90, 144, 175), light=S(151, 209, 227), muscle=S(134, 194, 217),
            socket=S(59, 98, 126), antler=S(111, 170, 199), thorn=S(104, 163, 193),
            haft=S(168, 196, 216), cap=S(120, 150, 176), axe=S(176, 206, 224),
            blade=S(214, 236, 250), edge=S(245, 252, 255), rune=S(230, 246, 255),
            shield=S(81, 132, 175), carve=S(230, 244, 255), rim=S(51, 91, 132),
            eye=S(230, 250, 255), leaf=S(137, 179, 204), leaf2=S(160, 197, 216), leaf3=S(186, 214, 230),
            crack=S(220, 240, 252))
EARTH = dict(WOOD, body=S(66, 56, 43), dark=S(30, 24, 17), light=S(110, 90, 64), muscle=S(80, 66, 49),
             socket=S(14, 10, 7), antler=S(72, 58, 41), thorn=S(60, 49, 35),
             shield=S(76, 44, 27), carve=S(255, 176, 52), rim=S(38, 27, 19))
WATER_TREE = dict(WATER, body=S(34, 84, 90), dark=S(16, 42, 48), light=S(62, 118, 120), muscle=S(44, 98, 102),
                  socket=S(8, 20, 24), antler=S(34, 40, 42), thorn=S(30, 36, 38),
                  shield=S(58, 44, 32), carve=S(70, 170, 180), rim=S(30, 24, 18), eye=S(120, 235, 255),
                  leaf=S(44, 128, 136), leaf2=S(64, 152, 158), leaf3=S(30, 96, 106))
WIND_TREE = dict(WIND, body=S(132, 114, 78), dark=S(90, 74, 48), light=S(170, 150, 106), muscle=S(146, 126, 86),
                 socket=S(40, 32, 20), antler=S(124, 104, 70), thorn=S(110, 92, 62),
                 shield=S(146, 118, 80), carve=S(96, 76, 50), rim=S(92, 72, 46), eye=S(150, 240, 255),
                 leaf=S(140, 172, 124), leaf2=S(170, 196, 150), leaf3=S(112, 146, 100))
CORAL = (S(240, 120, 110), S(250, 150, 130))
MOSS = (S(50, 65, 20), S(70, 78, 25), S(37, 52, 17))
PLAIN = dict(WOOD, blade=S(150, 156, 164), edge=S(204, 210, 216), rune=S(112, 118, 126))
EARTH_VEIN = S(255, 176, 52)
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


def limbs(P, R, g, pal, weapon="axe"):
    for side, s in (("Left", 1), ("Right", -1)):
        sh = R.head[side + "UpperArm"]
        el = R.head[side + "LowerArm"]
        wr = R.head[side + "Hand"]
        P.put(g, side + "Shoulder", sh + Vector((-s * 0.02, 0.0, 0.02)), (0.16, 0.16, 0.14), pal["body"], 0.016)
        P.limb(g, side + "UpperArm", sh, el, 0.13, 0.14, pal["body"], 0.03)
        mid = sh.lerp(el, 0.45)
        P.put(g, side + "UpperArm", mid + Vector((0, -0.04, 0.02)), (0.08, 0.06, 0.10), pal["muscle"], 0.01)
        lw, ld = 0.11, 0.12
        if side == "Right" and weapon == "sword":
            lw, ld = 0.055, 0.14
        elif side == "Right" and weapon == "spear":
            lw, ld = 0.05, 0.05
        elif side == "Right" and weapon == "mace":
            lw, ld = 0.09, 0.09
        P.limb(g, side + "LowerArm", el, wr, lw, ld, pal["dark"], 0.02)
        if not (side == "Right" and weapon in ("sword", "spear", "mace")):
            P.put(g, side + "LowerArm", el.lerp(wr, 0.55) + Vector((0, -0.03, 0)), (0.07, 0.05, 0.08), pal["light"], 0.008)
        hip, kn = R.head[side + "UpperLeg"], R.head[side + "LowerLeg"]
        an = R.head[side + "Foot"]
        P.limb(g, side + "UpperLeg", hip, kn, 0.18, 0.18, pal["body"], 0.03)
        P.limb(g, side + "LowerLeg", kn, an, 0.16, 0.16, pal["dark"], 0.02)
        P.put(g, side + "LowerLeg", kn + Vector((0, -0.05, 0.02)), (0.11, 0.07, 0.09), pal["light"], 0.008)
        P.put(g, side + "Foot", (an.x, -0.04, 0.10), (0.18, 0.24, 0.14), pal["body"], 0.014)


def fists(P, R, g, pal):
    wr, ht = R.head["LeftHand"], R.tail["LeftHand"]
    P.put(g, "LeftHand", wr.lerp(ht, 0.35), (0.11, 0.09, 0.10), pal["body"], 0.012)
    P.put(g, "LeftHand", wr.lerp(ht, 0.7), (0.08, 0.07, 0.07), pal["dark"], 0.008)


def weapon_arm(P, R, g, pal, kind):
    frame = R.frame["Weapon_R"]
    bone = "RightHand"
    P.put_local(g, bone, frame, (0, 0, -0.04), (0.16, 0.15, 0.26), pal["dark"], 0.012)
    P.put_local(g, bone, frame, (0.03, 0.02, 0.10), (0.11, 0.10, 0.12), pal["body"], 0.01)
    if kind == "axe":
        P.put_local(g, bone, frame, (0, 0, 0.06), (0.08, 0.08, 0.20), pal["haft"], 0.008)
        P.put_local(g, bone, frame, (0.02, 0, 0.26), (0.14, 0.10, 0.16), pal["axe"], 0.01)
        P.put_local(g, bone, frame, (0.16, 0, 0.26), (0.22, 0.055, 0.30), pal["blade"], 0.008)
        P.put_local(g, bone, frame, (0.28, 0, 0.24), (0.10, 0.04, 0.20), pal["edge"], 0.006)
    elif kind == "sword":
        P.put_local(g, bone, frame, (0, 0, 0.08), (0.07, 0.06, 0.16), pal["haft"], 0.006)
        P.put_local(g, bone, frame, (0, 0, 0.48), (0.16, 0.045, 0.72), pal["blade"], 0.006)
        P.put_local(g, bone, frame, (0, 0, 0.86), (0.08, 0.03, 0.18), pal["edge"], 0.004)
    elif kind == "mace":
        P.put_local(g, bone, frame, (0, 0, 0.10), (0.11, 0.11, 0.24), pal["haft"], 0.008)
        P.put_local(g, bone, frame, (0, 0, 0.42), (0.34, 0.32, 0.34), pal["edge"], 0.016)
        P.put_local(g, bone, frame, (0.16, 0, 0.42), (0.20, 0.24, 0.24), pal["light"], 0.012)
        P.put_local(g, bone, frame, (-0.12, 0.08, 0.46), (0.18, 0.20, 0.20), pal["axe"], 0.012)
    else:
        P.put_local(g, bone, frame, (0, 0, 0.08), (0.06, 0.06, 0.18), pal["haft"], 0.006)
        P.put_local(g, bone, frame, (0, 0, 0.62), (0.045, 0.045, 0.95), pal["blade"], 0.005)
        P.put_local(g, bone, frame, (0, 0, 1.12), (0.10, 0.10, 0.16), pal["edge"], 0.006)


def cracks(P, g, color, bones=True):
    pts = [(0.0, 1.30), (-0.04, 1.18), (0.05, 1.06), (-0.03, 0.94), (0.02, 0.82)]
    names = ["UpperChest", "Chest", "Chest", "Spine", "Hips"]
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        P.limb(g, names[i], (a[0], -0.17, a[1]), (b[0], -0.17, b[1]), 0.032, 0.018, color)
    if not bones:
        return
    for s in (-1, 1):
        P.limb(g, "Chest", (s * 0.08, -0.16, 1.22), (s * 0.16, -0.15, 1.08), 0.024, 0.014, color)
        P.limb(g, "UpperChest", (s * 0.18, -0.08, 1.28), (s * 0.28, -0.06, 1.16), 0.022, 0.014, color)
        P.limb(g, "Head", (s * 0.04, -0.18, 1.58), (s * 0.10, -0.16, 1.46), 0.018, 0.012, color)
        P.limb(g, ("Left" if s > 0 else "Right") + "UpperArm",
               (s * 0.30, -0.04, 1.22), (s * 0.38, -0.02, 1.04), 0.018, 0.012, color)
        P.limb(g, ("Left" if s > 0 else "Right") + "UpperLeg",
               (s * 0.12, -0.14, 0.70), (s * 0.16, -0.12, 0.50), 0.018, 0.012, color)


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
        shield_border(P, bone, frame, g, pal["carve"])
        P.put_local(g, bone, frame, (0.0, 0.0, 0.185), (0.05, 0.36, 0.012), pal["carve"], 0.003)
        P.put_local(g, bone, frame, (0.0, 0.0, 0.185), (0.18, 0.04, 0.012), pal["carve"], 0.003)
        for s in (-1, 1):
            for k, (x, y) in enumerate(((0.04, 0.07), (0.08, 0.11), (0.11, 0.15))):
                P.put_local(g, bone, frame, (s * x, 0.02 + y, 0.185), (0.05, 0.05, 0.012), pal["carve"], 0.003)
            for x, y in ((0.04, -0.09), (0.08, -0.13)):
                P.put_local(g, bone, frame, (s * x, 0.02 + y, 0.185), (0.05, 0.05, 0.012), pal["carve"], 0.003)
    elif kind == "tree":
        shield_border(P, bone, frame, g, pal["carve"])
        P.put_local(g, bone, frame, (0.0, -0.02, 0.185), (0.045, 0.30, 0.012), pal["carve"], 0.003)
        P.put_local(g, bone, frame, (0.0, 0.18, 0.185), (0.04, 0.08, 0.012), pal["carve"], 0.003)
        for s in (-1, 1):
            for x, y in ((0.04, 0.06), (0.08, 0.10), (0.11, 0.14), (0.13, 0.18)):
                P.put_local(g, bone, frame, (s * x, 0.02 + y, 0.185), (0.04, 0.04, 0.012), pal["carve"], 0.003)
            for x, y in ((0.04, -0.14), (0.08, -0.17)):
                P.put_local(g, bone, frame, (s * x, 0.02 + y, 0.185), (0.045, 0.03, 0.012), pal["carve"], 0.003)
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


def shield_border(P, bone, frame, g, color):
    P.put_local(g, bone, frame, (0.0, 0.26, 0.185), (0.34, 0.025, 0.012), color, 0.003)
    P.put_local(g, bone, frame, (0.0, -0.22, 0.185), (0.34, 0.025, 0.012), color, 0.003)
    for s in (-1, 1):
        P.put_local(g, bone, frame, (s * 0.165, 0.02, 0.185), (0.025, 0.46, 0.012), color, 0.003)


def gear(P, R, g, pal, kind, weapon, weapon_group=None):
    weapon_arm(P, R, weapon_group or g, pal, weapon)
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
    P.put(g, "Head", (0, 0.10, 1.68), (0.28, 0.14, 0.30), pal["leaf2"], 0.012)
    hanging(P, g, cols, (0.0, 0.08, 1.78), 4, 0.55, 0.12)
    P.put(g, "Head", (0, -0.16, 1.72), (0.10, 0.08, 0.16), pal["leaf3"], 0.008)
    for i in range(3):
        P.put(g, "Head", (0, -0.18, 1.78 + i * 0.08), (0.08 - i * 0.015, 0.06, 0.08), cols[min(i + 1, 3)], 0.006)


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


def coral(P, g):
    for s in (-1, 1):
        P.limb(g, "Head", (s * 0.12, 0.02, 1.86), (s * 0.20, 0.04, 2.02), 0.026, 0.026, CORAL[0])
        P.limb(g, "Head", (s * 0.16, 0.03, 1.94), (s * 0.26, 0.06, 2.04), 0.02, 0.02, CORAL[1])
        P.limb(g, side_bone(s), (s * 0.30, 0.02, 1.40), (s * 0.34, 0.04, 1.52), 0.024, 0.024, CORAL[0])
        P.limb(g, side_bone(s), (s * 0.32, 0.03, 1.46), (s * 0.40, 0.05, 1.54), 0.018, 0.018, CORAL[1])


def eyes_only(P, g, color):
    for s in (-1, 1):
        P.put(g, "Head", (s * 0.075, -0.205, 1.62), (0.06, 0.025, 0.038), color, 0.003)


def body(P, R, g, pal, eyes, kind, weapon):
    torso(P, g, pal)
    head(P, g, pal, eyes, pal["eye"])
    limbs(P, R, g, pal, weapon)
    fists(P, R, g, pal)
    if kind == "wood":
        antlers(P, g, pal)
    elif kind == "fire":
        antlers(P, g, pal)
        cracks(P, g, pal["crack"])
    elif kind == "water":
        for s in (-1, 1):
            P.limb(g, "Head", (s * 0.10, -0.02, 1.68), (s * 0.22, -0.08, 1.98), 0.032, 0.028, WOOD["antler"])
            P.limb(g, "Head", (s * 0.16, -0.06, 1.88), (s * 0.30, -0.12, 2.06), 0.022, 0.02, WOOD["light"])
        cracks(P, g, pal["crack"], bones=False)
        for s in (-1, 1):
            for k in range(4):
                a = k * 0.9
                P.limb(g, "Chest", (s * 0.06, -0.16, 1.22),
                       (s * (0.10 + math.cos(a) * 0.08), -0.16, 1.22 - 0.08 - k * 0.04),
                       0.02, 0.012, pal["crack"])
    else:
        cracks(P, g, pal["crack"], bones=False)


def tongue(P, g, bone, base, lean, height, width, cols):
    steps = 5
    pts = []
    for k in range(steps + 1):
        f = k / float(steps)
        pts.append((base[0] + lean[0] * f * f, base[1] + lean[1] * f * f, base[2] + height * f))
    for k in range(steps):
        w = width * (1.0 - k / float(steps)) + 0.015
        P.limb(g, bone, pts[k], pts[k + 1], w, w * 0.7, cols[min(k, len(cols) - 1)])


def fire_pass(P, g, pal):
    cols = (pal["leaf"], pal["leaf2"], pal["leaf2"], pal["leaf3"], pal["edge"])
    antlers(P, g, pal)
    for i in range(7):
        x = -0.24 + i * 0.08
        tongue(P, g, "Head", (x, -0.02, 1.82), (x * 0.9, 0.10, 0.0), 0.42 + 0.14 * (1 - abs(x) / 0.24), 0.07, cols)
    for s in (-1, 1):
        tongue(P, g, side_bone(s), (s * 0.30, 0.0, 1.40), (s * 0.08, 0.04, 0), 0.26, 0.06, cols)
    for z in (0.86, 1.00, 1.16, 1.30):
        for s in (-1, 1):
            P.limb(g, "Chest" if z > 1.0 else "Spine", (s * 0.04, -0.16, z), (s * 0.16, -0.155, z + 0.07), 0.018, 0.012, pal["crack"])


def build_all(P, R):
    body(P, R, "Bark", WOOD, False, "wood", "axe")
    gear(P, R, "Bark", PLAIN, "leaf", "axe", "Weapon0Axe")
    weapon_arm(P, R, "Weapon0Mace", PLAIN, "mace")
    weapon_arm(P, R, "Weapon0Sword", PLAIN, "sword")
    weapon_arm(P, R, "Weapon0Spear", PLAIN, "spear")
    weapon_arm(P, R, "Leaves3", WOOD, "mace")
    eyes_only(P, "Leaves0", WOOD["eye"])
    willow(P, "Leaves0", (WOOD["leaf"], WOOD["leaf2"], WOOD["leaf3"]))
    skirt(P, "Leaves0", (WOOD["leaf"], WOOD["leaf2"], WOOD["leaf3"]), "leaf")
    body(P, R, "Leaves1", WATER_TREE, True, "wood", "spear")
    gear(P, R, "Leaves1", WATER_TREE, "swirl", "spear")
    willow(P, "Leaves1", (WATER_TREE["leaf"], WATER_TREE["leaf2"], WATER_TREE["leaf3"]))
    skirt(P, "Leaves1", (WATER_TREE["leaf"], WATER_TREE["leaf2"], WATER_TREE["leaf3"]), "leaf")
    coral(P, "Leaves1")
    body(P, R, "Leaves2", FIRE, True, "fire", "sword")
    gear(P, R, "Leaves2", FIRE, "brand", "sword")
    flame_crown(P, "Leaves2", FIRE)
    skirt(P, "Leaves2", (FIRE["leaf"], FIRE["dark"], FIRE["leaf2"]), "flame")
    fire_pass(P, "Leaves2", FIRE)
    body(P, R, "Leaves3", EARTH, False, "wood", "mace")
    shield(P, R.frame["LeftHand"], "Leaves3", EARTH, "tree")
    eyes_only(P, "Leaves3", EARTH_EYE)
    moss_crown(P, "Leaves3")
    willow(P, "Leaves3", MOSS)
    cracks(P, "Leaves3", EARTH_VEIN)
    for x in (-0.12, -0.04, 0.04, 0.12):
        P.put("Leaves3", "Head", (x, -0.10, 1.80 + (0.05 if abs(x) < 0.08 else 0.0)), (0.05, 0.05, 0.12), EARTH_VEIN, 0.006)
    for s in (-1, 1):
        P.put("Leaves3", side_bone(s), (s * 0.30, -0.02, 1.40), (0.14, 0.12, 0.05), EARTH_VEIN, 0.008)
    body(P, R, "Leaves4", WIND_TREE, True, "wood", "axe")
    gear(P, R, "Leaves4", WIND_TREE, "leaf", "axe")
    willow(P, "Leaves4", (WIND_TREE["leaf"], WIND_TREE["leaf2"], WIND_TREE["leaf3"]))
    skirt(P, "Leaves4", (WIND_TREE["leaf"], WIND_TREE["leaf2"], WIND_TREE["leaf3"]), "leaf")


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
