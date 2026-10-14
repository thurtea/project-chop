"""Tree elemental on Chopper's rig: the night forest enemy (Treant's golem look).

Run:  blender -b -P build_elemental.py
In:   Art/Blender/chopper_rigged.blend (rig and takes, read only)
Out:  Art/Blender/chopper_elemental.blend, Art/Export/chopper_elemental.fbx

Inspired by the user's 2D tree elemental sheet: a bark trunk with the head sunk into it, long branch arms
ending in claws, root feet, glowing eyes and a crown that tells the element. Every block is weighted to one
bone of chopper_rig, so the Humanoid avatar and Chopper's blocky takes drive it unchanged. Objects:
Bark (shared) and Leaves0..Leaves4 (crown, eyes, shoulder growth and veins per Element: Neutral, Water,
Fire, Earth, Wind). Treant.ShowElementCrown shows only this enemy's Leaves mesh.
"""
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chopper_lib as L  # noqa: E402
import export_fbx as E  # noqa: E402
from rig_chopper import attach  # noqa: E402

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
RIGGED = os.path.join(ART, "Blender", "chopper_rigged.blend")
OUT_BLEND = os.path.join(ART, "Blender", "chopper_elemental.blend")
OUT_FBX = os.path.join(ART, "Export", "chopper_elemental.fbx")


def S(r, g, b):
    return (r / 255.0, g / 255.0, b / 255.0)


BARK = S(96, 62, 36)
BARK_DARK = S(64, 40, 24)
BARK_LIGHT = S(128, 88, 54)
HOLLOW = S(22, 13, 8)

ELEMENTS = [
    dict(style="leafy", cols=[S(46, 88, 40), S(62, 112, 48), S(34, 66, 30)], eye=S(255, 186, 60), vein=None),
    dict(style="ice", cols=[S(150, 212, 236), S(108, 178, 222), S(206, 242, 252)], eye=S(160, 246, 255),
         vein=S(120, 220, 255)),
    dict(style="flame", cols=[S(196, 46, 20), S(236, 104, 24), S(252, 170, 40), S(255, 226, 110)],
         eye=S(255, 240, 150), vein=S(255, 120, 30)),
    dict(style="moss", cols=[S(98, 114, 42), S(132, 130, 48), S(72, 92, 36)], eye=S(255, 204, 70), vein=None,
         stone=[S(122, 118, 108), S(96, 92, 86)]),
    dict(style="wisp", cols=[S(70, 190, 170), S(112, 222, 202), S(40, 140, 130)], eye=S(222, 255, 250),
         vein=S(150, 250, 230)),
]


class Rig:
    def __init__(self, rig):
        self.rig = rig
        mw = rig.matrix_world
        self.head = {b.name: mw @ b.head_local for b in rig.data.bones}
        self.tail = {b.name: mw @ b.tail_local for b in rig.data.bones}


class Parts:
    def __init__(self, mat):
        self.mat = mat
        self.groups = {}

    def add(self, group, bone, build):
        mb = L.MeshBuilder("%s_%d" % (group, len(self.groups.get(group, []))))
        build(mb)
        ob = mb.finish(self.mat)
        vg = ob.vertex_groups.new(name=bone)
        vg.add(list(range(len(ob.data.vertices))), 1.0, "REPLACE")
        self.groups.setdefault(group, []).append(ob)

    def join(self, rig):
        out = []
        for name, obs in self.groups.items():
            bpy.ops.object.select_all(action="DESELECT")
            for ob in obs:
                ob.select_set(True)
            bpy.context.view_layer.objects.active = obs[0]
            bpy.ops.object.join()
            ob = bpy.context.view_layer.objects.active
            ob.name = name
            ob.data.name = name
            attach(rig, ob)
            out.append(ob)
        return out


def along(mb, a, b, width, depth, color, extend=0.0, bevel=0.012):
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    a, b = a - d * extend, b + d * extend
    u, v, w = L.basis(d, Vector((0, -1, 0)) if abs(d.y) < 0.9 else Vector((0, 0, 1)))
    mb.box((a + b) / 2, (width, depth, (b - a).length), bevel, rot=L.rot_from(u, v, w), color=color)


def box(mb, c, size, color, bevel=0.01):
    mb.box(c, size, bevel, color=color)


def build_bark(P, R):
    torso = [("Hips", (0, 0.0, 0.80), (0.50, 0.36, 0.24)), ("Spine", (0, 0.0, 0.97), (0.54, 0.38, 0.22)),
             ("Chest", (0, 0.0, 1.12), (0.64, 0.42, 0.20)), ("UpperChest", (0, 0.01, 1.28), (0.76, 0.46, 0.24))]
    for bone, c, size in torso:
        P.add("Bark", bone, lambda mb, c=c, size=size: box(mb, c, size, BARK, 0.03))
        for k, x in enumerate((-0.22, -0.08, 0.06, 0.19)):
            col = BARK_DARK if k % 2 == 0 else BARK_LIGHT
            for side in (-1, 1):
                y = side * (size[1] / 2 + 0.008)
                P.add("Bark", bone, lambda mb, x=x * size[0] / 0.64, y=y, c=c, size=size, col=col:
                      box(mb, (x, y, c[2]), (0.05, 0.02, size[2] * 0.96), col, 0.004))
    hc = Vector((0, -0.05, 1.52))
    P.add("Bark", "Head", lambda mb: box(mb, hc, (0.42, 0.40, 0.34), BARK, 0.03))
    P.add("Bark", "Head", lambda mb: box(mb, (0, -0.255, 1.605), (0.44, 0.07, 0.07), BARK_DARK, 0.01))
    for s in (-1, 1):
        P.add("Bark", "Head", lambda mb, s=s: box(mb, (s * 0.095, -0.248, 1.555), (0.10, 0.03, 0.06), HOLLOW, 0.004))
    P.add("Bark", "Head", lambda mb: box(mb, (0, -0.27, 1.52), (0.07, 0.06, 0.08), BARK_LIGHT, 0.008))
    P.add("Bark", "Head", lambda mb: box(mb, (0, -0.25, 1.44), (0.22, 0.03, 0.07), HOLLOW, 0.004))
    for x in (-0.07, 0.0, 0.07):
        P.add("Bark", "Head", lambda mb, x=x: box(mb, (x, -0.262, 1.462), (0.03, 0.02, 0.03), BARK_LIGHT, 0.003))
    for s in (-1, 1):
        P.add("Bark", "UpperChest", lambda mb, s=s: along(mb, (s * 0.20, 0.20, 1.30), (s * 0.30, 0.36, 1.52),
                                                          0.07, 0.07, BARK_DARK))
    for side, s in (("Left", 1), ("Right", -1)):
        sh, el = R.head[side + "UpperArm"], R.head[side + "LowerArm"]
        wr, ht = R.head[side + "Hand"], R.tail[side + "Hand"]
        P.add("Bark", side + "Shoulder", lambda mb, sh=sh, s=s: box(mb, sh + Vector((-s * 0.04, 0, 0.03)),
                                                                     (0.26, 0.30, 0.22), BARK, 0.02))
        P.add("Bark", side + "UpperArm", lambda mb, sh=sh, el=el: along(mb, sh, el, 0.20, 0.21, BARK, 0.05))
        P.add("Bark", side + "LowerArm", lambda mb, el=el, wr=wr: along(mb, el, wr, 0.22, 0.23, BARK, 0.04))
        P.add("Bark", side + "LowerArm", lambda mb, el=el, wr=wr: along(mb, el.lerp(wr, 0.3), el.lerp(wr, 0.7),
                                                                        0.25, 0.12, BARK_DARK))
        P.add("Bark", side + "LowerArm", lambda mb, el=el, s=s: along(mb, el, el + Vector((s * 0.08, 0.10, 0.16)),
                                                                      0.045, 0.045, BARK_LIGHT))
        d = (ht - wr).normalized()
        fwd = Vector((0, -1, 0))
        spread = (fwd - d * fwd.dot(d)).normalized()
        inward = d.cross(spread).normalized() * (-s)
        P.add("Bark", side + "Hand", lambda mb, wr=wr, ht=ht: along(mb, wr, wr.lerp(ht, 0.75), 0.24, 0.22, BARK, 0.03))
        for k in (-1, 0, 1):
            base = wr.lerp(ht, 0.65) + spread * (0.07 * k)
            mid = base + d * 0.13 + spread * (0.02 * k)
            tip = mid + d * 0.09 + inward * 0.07
            P.add("Bark", side + "Hand", lambda mb, a=base, b=mid: along(mb, a, b, 0.075, 0.075, BARK_DARK, 0.012))
            P.add("Bark", side + "Hand", lambda mb, a=mid, b=tip: along(mb, a, b, 0.058, 0.058, BARK_LIGHT, 0.01))
        th = wr.lerp(ht, 0.35) + spread * 0.10
        P.add("Bark", side + "Hand", lambda mb, a=th, b=th + spread * 0.08 + d * 0.06:
              along(mb, a, b, 0.05, 0.05, BARK_DARK, 0.01))
        hip, kn = R.head[side + "UpperLeg"], R.head[side + "LowerLeg"]
        an = R.head[side + "Foot"]
        P.add("Bark", side + "UpperLeg", lambda mb, hip=hip, kn=kn: along(mb, hip, kn, 0.24, 0.26, BARK, 0.04))
        P.add("Bark", side + "LowerLeg", lambda mb, kn=kn, an=an: along(mb, kn, an, 0.26, 0.28, BARK, 0.04))
        P.add("Bark", side + "LowerLeg", lambda mb, kn=kn: box(mb, kn + Vector((0, -0.14, 0)), (0.16, 0.04, 0.10),
                                                               BARK_DARK, 0.008))
        foot = Vector((an.x, -0.05, 0.07))
        P.add("Bark", side + "Foot", lambda mb, f=foot: box(mb, f, (0.28, 0.34, 0.14), BARK, 0.02))
        for dx, dy in ((-0.16, -0.20), (0.0, -0.26), (0.16, -0.20), (s * 0.18, 0.06), (0.0, 0.20)):
            end = Vector((an.x + dx, -0.05 + dy, 0.025))
            P.add("Bark", side + "Foot", lambda mb, f=foot, e=end: along(mb, f + Vector((0, 0, -0.02)), e,
                                                                         0.07, 0.05, BARK_DARK, 0.0))


def build_leaves(P, R, i, el):
    g = "Leaves%d" % i
    rnd = random.Random(17 + i)
    cols = el["cols"]
    top = 1.69
    style = el["style"]
    for s in (-1, 1):
        P.add(g, "Head", lambda mb, s=s: box(mb, (s * 0.095, -0.262, 1.555), (0.065, 0.02, 0.038), el["eye"], 0.004))

    def col():
        return cols[rnd.randrange(len(cols))]
    if style in ("leafy", "moss", "ice"):
        for x in (-0.27, -0.135, 0.0, 0.135, 0.27):
            for y in (-0.21, -0.08, 0.05, 0.18, 0.31):
                edge = abs(x) > 0.2 or y < -0.15 or y > 0.25
                h = rnd.uniform(0.10, 0.20) if edge else rnd.uniform(0.20, 0.32)
                c = (x + rnd.uniform(-0.02, 0.02), y, top + h / 2 - (0.08 if edge else 0.03))
                P.add(g, "Head", lambda mb, c=c, h=h, cc=col(): box(mb, c, (0.16, 0.16, h), cc, 0.012))
        for k in range(9):
            ang = k / 9.0
            x = -0.23 + 0.46 * ang
            side_pts = [(x * 1.15, 0.33), (-0.30, -0.15 + 0.45 * ang), (0.30, -0.15 + 0.45 * ang)]
            px, py = side_pts[k % 3]
            ln = rnd.uniform(0.16, 0.42)
            if style == "ice":
                P.add(g, "Head", lambda mb, px=px, py=py, ln=ln, cc=col(): box(
                    mb, (px, py, top - ln / 2), (0.045, 0.045, ln), cc, 0.006))
            else:
                P.add(g, "Head", lambda mb, px=px, py=py, ln=ln, cc=col(): box(
                    mb, (px, py, top - ln / 2), (0.05, 0.03, ln), cc, 0.006))
    elif style == "flame":
        for x, y, h in ((0, -0.02, 0.36), (-0.14, 0.04, 0.26), (0.14, 0.02, 0.28), (-0.06, 0.16, 0.22),
                        (0.08, 0.17, 0.24), (-0.20, -0.12, 0.16), (0.20, -0.12, 0.18), (0.0, -0.17, 0.14)):
            n = 4
            for k in range(n):
                f = k / n
                w = 0.20 * (1.0 - 0.7 * f)
                z = top - 0.03 + h * f + h / n / 2
                P.add(g, "Head", lambda mb, x=x, y=y, z=z, w=w, k=k: box(
                    mb, (x + rnd.uniform(-0.015, 0.015), y, z), (w, w, h / n + 0.01), cols[min(k, 3)], 0.008))
    elif style == "wisp":
        for k in range(10):
            x = rnd.uniform(-0.20, 0.20)
            z = top - 0.05 + rnd.uniform(0.0, 0.14)
            back = 0.10 + (z - top) * 1.6
            P.add(g, "Head", lambda mb, x=x, z=z, back=back, cc=col(): along(
                mb, (x, -0.12, z), (x * 1.2, 0.20 + back, z + 0.04), 0.09, 0.07, cc, 0.0))
        for s in (-1, 1):
            P.add(g, "Head", lambda mb, s=s: along(mb, (s * 0.20, 0.10, 1.62), (s * 0.30, 0.46, 1.70), 0.06, 0.05,
                                                   cols[1]))
    for side, s in (("Left", 1), ("Right", -1)):
        sh = R.head[side + "UpperArm"]
        base = sh + Vector((-s * 0.02, 0.0, 0.14))
        if style == "flame":
            for k in range(3):
                P.add(g, side + "Shoulder", lambda mb, k=k: box(
                    mb, base + Vector((0, 0.03 * k - 0.03, 0.05 * k)), (0.14 - 0.035 * k,) * 2 + (0.07,),
                    cols[min(k + 1, 3)], 0.006))
        elif style == "moss":
            P.add(g, side + "Shoulder", lambda mb: box(mb, base, (0.22, 0.24, 0.06), cols[0], 0.01))
            P.add(g, side + "Shoulder", lambda mb: box(mb, base + Vector((s * 0.03, 0.04, 0.07)), (0.12, 0.12, 0.10),
                                                       el["stone"][0], 0.015))
        elif style == "ice":
            for k in range(3):
                P.add(g, side + "Shoulder", lambda mb, k=k: along(
                    mb, base + Vector((0, 0.06 * k - 0.06, 0)), base + Vector((s * 0.06, 0.06 * k - 0.04, 0.16 + 0.05 * k)),
                    0.05, 0.05, cols[k % 3]))
        elif style == "wisp":
            P.add(g, side + "Shoulder", lambda mb: along(mb, base, base + Vector((s * 0.05, 0.22, 0.08)),
                                                         0.10, 0.06, cols[0]))
        else:
            P.add(g, side + "Shoulder", lambda mb: box(mb, base, (0.20, 0.22, 0.08), cols[1], 0.01))
            P.add(g, side + "Shoulder", lambda mb: box(mb, base + Vector((0, 0.02, 0.06)), (0.12, 0.12, 0.06),
                                                       cols[0], 0.01))
    if el["vein"] is not None:
        pts = [(0.02, 1.38), (-0.06, 1.28), (0.04, 1.18), (-0.05, 1.07), (0.03, 0.96), (-0.02, 0.86)]
        bones = ["UpperChest", "UpperChest", "Chest", "Chest", "Spine", "Hips"]
        for k in range(len(pts) - 1):
            a, b = pts[k], pts[k + 1]
            for y, sx in ((-0.236, 1.0), (0.236, -1.0)):
                ya = y - 0.004 if y < 0 else y + 0.004
                P.add(g, bones[k], lambda mb, a=a, b=b, ya=ya, sx=sx: along(
                    mb, (a[0] * sx, ya, a[1]), (b[0] * sx, ya, b[1]), 0.025, 0.012, el["vein"], 0.006, 0.003))
        for x, z0 in ((-0.22, 1.30), (0.22, 1.22)):
            P.add(g, "Chest" if z0 < 1.25 else "UpperChest", lambda mb, x=x, z0=z0: along(
                mb, (x, -0.236, z0), (x * 0.6, -0.236, z0 - 0.12), 0.022, 0.012, el["vein"], 0.0, 0.003))


def main():
    bpy.ops.wm.open_mainfile(filepath=RIGGED)
    rig = bpy.data.objects["chopper_rig"]
    for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
        bpy.data.objects.remove(ob, do_unlink=True)
    mat = L.make_material("M_Elemental", (0.6, 0.6, 0.6))
    R = Rig(rig)
    P = Parts(mat)
    build_bark(P, R)
    for i, el in enumerate(ELEMENTS):
        build_leaves(P, R, i, el)
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
