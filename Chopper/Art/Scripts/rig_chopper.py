"""Build chopper_rig (humanoid armature + sockets) and skin the Worker modules plus the helmet.

Run:  blender -b -P rig_chopper.py
In:   Art/Blender/chopper_character.blend   (read only)
Out:  Art/Blender/chopper_rigged.blend      (meshes + chopper_rig, no actions yet)

Joint positions are measured from the body mesh (tube ring centroids), not typed in:
  * arm axis = least-squares line through the upper-arm/forearm ring centroids,
    shoulder = centroid of the t=0 ring, elbow/wrist at 0.30/0.57 m along that axis
  * leg axis = leg ring centroids, knee = midpoint of the two knee loops
Weights are assigned explicitly from one smooth weight field per body region
(torso, arm, leg, head). Clothing islands use the same field as the body part they
cover, so the layers stay apart when the joints bend. No automatic weights.
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SRC = os.path.join(ART, "Blender", "chopper_character.blend")
OUT = os.path.join(ART, "Blender", "chopper_rigged.blend")

PARTS = ["chopper_body", "chopper_head", "chopper_beard", "chopper_hair", "chopper_shirt",
         "chopper_vest", "chopper_belt", "chopper_bracers", "chopper_chainmail", "chopper_boots",
         "chopper_helmet",
         "chopper_plate", "chopper_pauldrons"]
RIG = "chopper_rig"
SOCKETS = ("Weapon_R", "Shield_L", "Shield_Back")

FWD = Vector((0.0, -1.0, 0.0))
UP = Vector((0.0, 0.0, 1.0))


# ------------------------------------------------------------------ helpers
def clamp01(x):
    return 0.0 if x < 0.0 else 1.0 if x > 1.0 else x


def ramp(x, a, b):
    """0 at a, 1 at b (linear, clamped). a may be > b."""
    return clamp01((x - a) / (b - a))


def smooth(x):
    x = clamp01(x)
    return x * x * (3.0 - 2.0 * x)


def islands(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    seen, out = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            a = stack.pop()
            comp.append(a.index)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index)
                    stack.append(b)
        out.append(sorted(comp))
    bm.free()
    return out


def ring_centroids(me, idx, n=12):
    vs = [me.vertices[i].co for i in idx]
    return [sum(vs[k:k + n], Vector()) / n for k in range(0, len(vs) - n + 1, n)]


def fit_line(pts):
    c = sum(pts, Vector()) / len(pts)
    # power iteration on the covariance matrix
    cov = Matrix(((0.0,) * 3,) * 3)
    for p in pts:
        d = p - c
        for i in range(3):
            for j in range(3):
                cov[i][j] += d[i] * d[j]
    v = (pts[-1] - pts[0]).normalized()
    for _ in range(50):
        v = (cov @ v).normalized()
    if v.dot(pts[-1] - pts[0]) < 0:
        v = -v
    return c, v


# ------------------------------------------------------------------ measurement
def measure(body):
    me = body.data
    isl = islands(me)
    info = {}
    arms = [i for i in isl if len(i) == 216]
    legs = [i for i in isl if len(i) == 168]
    assert len(arms) == 2 and len(legs) == 2, "unexpected body island layout"
    for idx in arms:
        rc = ring_centroids(me, idx)
        arm_rings = rc[:12]                     # ARM table rows (t = -0.05 .. 0.575)
        c, d = fit_line(arm_rings)
        S = arm_rings[2]                        # t = 0.00 ring = shoulder joint
        side = "Left" if S.x > 0 else "Right"
        hand_tip = rc[-1]
        info[side + "Arm"] = dict(S=S, d=d, tip=(hand_tip - S).dot(d))
    for idx in legs:
        rc = ring_centroids(me, idx)
        side = "Left" if rc[0].x > 0 else "Right"
        zs = [p.z for p in rc]
        knee = (rc[zs.index(min(zs, key=lambda z: abs(z - 0.47)))] +
                rc[zs.index(min(zs, key=lambda z: abs(z - 0.44)))]) / 2
        info[side + "Leg"] = dict(x=rc[0].x, y=rc[0].y, top=max(zs), knee=knee)
    return info


# ------------------------------------------------------------------ armature
def bone_specs(info):
    """Return list of (name, head, tail, roll_ref, parent, deform)."""
    sp = []
    # spine chain (centre line, Z axis of every bone toward the front)
    chain = [("Hips", 0.760, 0.900, None), ("Spine", 0.900, 1.050, "Hips"),
             ("Chest", 1.050, 1.200, "Spine"), ("UpperChest", 1.200, 1.370, "Chest"),
             ("Neck", 1.370, 1.450, "UpperChest"), ("Head", 1.450, 1.720, "Neck")]
    for n, z0, z1, par in chain:
        sp.append((n, Vector((0, 0, z0)), Vector((0, 0, z1)), FWD, par, True))
    for side in ("Left", "Right"):
        a = info[side + "Arm"]
        S, d = a["S"], a["d"]
        s = 1.0 if side == "Left" else -1.0
        sp.append((side + "Shoulder", Vector((s * 0.05, 0.0, 1.32)), S.copy(), FWD, "UpperChest", True))
        sp.append((side + "UpperArm", S.copy(), S + d * 0.30, FWD, side + "Shoulder", True))
        sp.append((side + "LowerArm", S + d * 0.30, S + d * 0.57, FWD, side + "UpperArm", True))
        sp.append((side + "Hand", S + d * 0.57, S + d * min(0.76, a["tip"]), FWD, side + "LowerArm", True))
        lg = info[side + "Leg"]
        x, y = lg["x"], lg["y"]
        hip = Vector((x, y, 0.74))
        knee = Vector((x, y - 0.012, lg["knee"].z))    # knee a hair forward: IK bend plane
        ankle = Vector((x, y + 0.01, 0.10))
        ball = Vector((x, -0.13, 0.035))
        toe = Vector((x, -0.245, 0.035))
        sp.append((side + "UpperLeg", hip, knee, FWD, "Hips", True))
        sp.append((side + "LowerLeg", knee, ankle, FWD, side + "UpperLeg", True))
        sp.append((side + "Foot", ankle, ball, UP, side + "LowerLeg", True))
        sp.append((side + "Toes", ball, toe, UP, side + "Foot", True))
    return sp


def arm_frame(info, side):
    a = info[side + "Arm"]
    S, d = a["S"], a["d"]
    v = -(FWD - d * FWD.dot(d)).normalized()     # back, perpendicular to the arm
    u = v.cross(d)
    s = 1.0 if side == "Left" else -1.0
    palm = u * s                                 # toward the body
    return S, d, v, palm


def socket_matrices(info):
    """Socket bone matrices. A socket bone's local axes equal the prop's local axes,
    so a prop parented to the socket with an identity local transform sits correctly."""
    out = {}
    # Weapon_R: grip centre of the right fist. Axe: +Z haft (toward the head), X blades, Y thickness.
    S, d, v, palm = arm_frame(info, "Right")
    grip = S + d * 0.685 + palm * 0.006
    # haft runs across the fist (thumb side = front), cocked 30 deg up toward the elbow;
    # the "up" part leans 30 deg outward (wrist extension) so the head clears the hip.
    up = (-d * math.cos(math.radians(30)) - palm * math.sin(math.radians(30))).normalized()
    haft = (-v * math.cos(math.radians(30)) + up * math.sin(math.radians(30))).normalized()
    Z = haft
    Y = (palm - Z * palm.dot(Z)).normalized()
    X = Y.cross(Z).normalized()
    out["Weapon_R"] = ("RightHand", Matrix((X, Y, Z)).transposed().to_4x4(), grip)
    # Shield_L: outer left forearm. Shield: -Y face (outward), +Z top (toward the elbow).
    S, d, v, palm = arm_frame(info, "Left")
    # shield origin = its arm-strap centre ON the forearm axis (board back 10 cm off the axis).
    # t = 0.50 puts the grip bar (local z -0.235) at t = 0.735, inside the fist (hand t 0.60-0.78),
    # and keeps the shield's top edge clear of the shoulder when the arm swings in the walk.
    origin = S + d * 0.50
    Y = palm.normalized()
    Z = (-d).normalized()
    X = Y.cross(Z).normalized()
    out["Shield_L"] = ("LeftLowerArm", Matrix((X, Y, Z)).transposed().to_4x4(), origin)
    # Shield_Back: stowed on the upper back. The shield's +Y local points at the body
    # when this bone's Y axis points forward, so the origin sits far enough back that
    # the strap and grip clear the torso.
    Y = Vector((0, -1, 0))
    Z = Vector((0, 0, 1))
    X = Y.cross(Z)
    out["Shield_Back"] = ("UpperChest", Matrix((X, Y, Z)).transposed().to_4x4(), Vector((0.0, 0.46, 1.12)))
    return out


def build_armature(info):
    arm_data = bpy.data.armatures.new(RIG)
    arm_data.display_type = "OCTAHEDRAL"
    rig = bpy.data.objects.new(RIG, arm_data)
    bpy.context.scene.collection.objects.link(rig)
    rig.show_in_front = True
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    for name, head, tail, ref, parent, deform in bone_specs(info):
        b = eb.new(name)
        b.head, b.tail = head, tail
        b.align_roll(ref)
        b.use_deform = deform
        if parent:
            b.parent = eb[parent]
            b.use_connect = (parent and (eb[parent].tail - head).length < 1e-6
                             and name not in ("LeftUpperLeg", "RightUpperLeg"))
    for name, (parent, rot, pos) in socket_matrices(info).items():
        b = eb.new(name)
        b.head = (0, 0, 0)
        b.tail = (0, 0.08, 0)
        m = rot.copy()
        m.translation = pos
        b.matrix = m
        b.length = 0.08
        b.use_deform = False
        b.parent = eb[parent]
        b.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return rig


# ------------------------------------------------------------------ weight fields
SPINE = [  # (bone, z where it becomes fully active going up), blends between neighbours
    ("Hips", None), ("Spine", (0.890, 0.950)), ("Chest", (1.020, 1.080)),
    ("UpperChest", (1.170, 1.230)), ("Neck", (1.370, 1.410)), ("Head", (1.440, 1.480)),
]


def spine_weights(z):
    w = {"Hips": 1.0}
    for name, rng in SPINE[1:]:
        f = ramp(z, rng[0], rng[1])
        if f <= 0:
            break
        # move share f from everything below to this bone
        for k in w:
            w[k] *= (1.0 - f)
        w[name] = f
    return w


SKIRT_MAX = 0.80     # thigh share of the skirt below SKIRT_Z1 (0.75-0.85 all clean in the sweep)
SKIRT_Z0 = 0.80      # belt line: pure Hips above
SKIRT_Z1 = 0.70
SKIRT_HALF = 0.17    # half width of the left/right blend across the skirt panels


def torso_field(p):
    w = spine_weights(p.z)
    # skirt / crotch: below the belt the cloth follows the thighs, split left / right.
    # The ramp spans 0.80 -> 0.70 only, where shirt, chainmail and body all have rings at
    # the same heights; below 0.70 the share is constant, so the layered skirts get the
    # same blend (same transform) and cannot pass through each other.
    a = SKIRT_MAX * ramp(p.z, SKIRT_Z0, SKIRT_Z1)
    if a > 0:
        # linear across the front/back panel; every corner (|x| >= 0.17) is fully one side.
        # Garment panels are single quads corner to corner, so a linear split keeps the
        # chainmail's slit vertices on the same blend as the shirt panel underneath.
        sl = ramp(p.x, -SKIRT_HALF, SKIRT_HALF)
        w = {k: v * (1 - a) for k, v in w.items()}
        w["LeftUpperLeg"] = w.get("LeftUpperLeg", 0) + a * sl
        w["RightUpperLeg"] = w.get("RightUpperLeg", 0) + a * (1 - sl)
    # shoulder tops follow the clavicle bones
    c = 0.6 * ramp(abs(p.x), 0.15, 0.28) * ramp(p.z, 1.15, 1.27)
    if c > 0:
        w = {k: v * (1 - c) for k, v in w.items()}
        key = "LeftShoulder" if p.x > 0 else "RightShoulder"
        w[key] = w.get(key, 0) + c
    return w


def arm_field(p, side, info):
    S, d = info[side + "Arm"]["S"], info[side + "Arm"]["d"]
    t = (p - S).dot(d)
    sh = 0.5 * ramp(t, 0.05, -0.05)
    el = ramp(t, 0.27, 0.33)
    wr = ramp(t, 0.555, 0.60)
    w = {side + "Shoulder": sh,
         side + "UpperArm": (1 - sh) * (1 - el),
         side + "LowerArm": (1 - sh) * el * (1 - wr),
         side + "Hand": (1 - sh) * el * wr}
    return w


def leg_field(p, side):
    kn = ramp(p.z, 0.485, 0.425)       # 0 above the knee blend, 1 below
    an = ramp(p.z, 0.175, 0.130)       # 1 = foot
    toe = ramp(p.y, -0.10, -0.15) if p.z < 0.165 else 0.0
    w = {side + "UpperLeg": 1 - kn,
         side + "LowerLeg": kn * (1 - an),
         side + "Foot": kn * an * (1 - toe),
         side + "Toes": kn * an * toe}
    return w


def classify(obj_name, centroid, bbox_min, bbox_max):
    """Region of a mesh island: 'torso', 'head', ('arm', side), ('leg', side)."""
    if obj_name in ("chopper_head", "chopper_hair", "chopper_beard", "chopper_helmet"):
        return "head"
    side = "Left" if centroid.x > 0 else "Right"
    if obj_name == "chopper_boots":
        return ("leg", side)
    if obj_name == "chopper_body":
        if abs(centroid.x) < 0.02:
            return "torso"
        if bbox_max.z < 0.80 and abs(centroid.x) < 0.2:
            return ("leg", side)
        return ("arm", side)
    if obj_name == "chopper_shirt":
        return "torso" if abs(centroid.x) < 0.02 else ("arm", side)
    if obj_name == "chopper_bracers":
        return ("arm", side)
    if obj_name == "chopper_belt":
        return ("arm", side) if abs(centroid.x) > 0.4 else "torso"
    if obj_name == "chopper_pauldrons":
        return ("arm", side)
    return "torso"   # vest, strap, buckle, chainmail, plate


def skin(rig, info, log):
    deform = [b.name for b in rig.data.bones if b.use_deform]
    stats = {}
    for name in PARTS:
        ob = bpy.data.objects[name]
        me = ob.data
        ob.vertex_groups.clear()
        groups = {b: ob.vertex_groups.new(name=b) for b in deform}
        per_vert = {}
        regions = {}
        for idx in islands(me):
            pts = [me.vertices[i].co for i in idx]
            c = sum(pts, Vector()) / len(pts)
            mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
            mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
            reg = classify(name, c, mn, mx)
            regions[str(reg)] = regions.get(str(reg), 0) + 1
            for i in idx:
                p = me.vertices[i].co
                if reg == "head":
                    w = {"Head": 1.0}
                elif reg == "torso":
                    w = torso_field(p)
                elif reg[0] == "arm":
                    w = arm_field(p, reg[1], info)
                else:
                    w = leg_field(p, reg[1])
                per_vert[i] = w
        maxinf = 0
        for i, w in per_vert.items():
            items = sorted(((k, v) for k, v in w.items() if v > 1e-4), key=lambda kv: -kv[1])[:4]
            tot = sum(v for _, v in items)
            items = [(k, v / tot) for k, v in items]
            maxinf = max(maxinf, len(items))
            for k, v in items:
                groups[k].add([i], v, "REPLACE")
        # drop empty groups
        used = set()
        for v in me.vertices:
            for g in v.groups:
                used.add(ob.vertex_groups[g.group].name)
        for gname in list(groups):
            if gname not in used:
                ob.vertex_groups.remove(ob.vertex_groups[gname])
        unweighted = sum(1 for v in me.vertices if not v.groups)
        stats[name] = (len(me.vertices), unweighted, maxinf, regions)
        log.append(f"  {name:18s} verts={len(me.vertices):5d} unweighted={unweighted} "
                   f"max_influences={maxinf} islands={regions}")
        # parent + Armature modifier, keep identity transform
        ob.parent = rig
        ob.parent_type = "OBJECT"
        ob.matrix_parent_inverse = Matrix.Identity(4)
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        mod.use_vertex_groups = True
        mod.use_deform_preserve_volume = False
    return stats


def _island_stats(comp):
    c = sum((f.calc_center_median() for f in comp), Vector()) / len(comp)
    vol = 0.0
    for f in comp:
        vs = [lp.vert.co - c for lp in f.loops]
        for i in range(1, len(vs) - 1):
            vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
    fs = set(comp)
    bad = 0
    for f in comp:
        for lp in f.loops:
            e = lp.edge
            if len(e.link_faces) != 2:
                continue
            other = [g for g in e.link_faces if g is not f][0]
            if other not in fs:
                continue
            # consistent winding: the neighbour walks the shared edge the other way
            for lq in other.loops:
                if lq.edge is e and lq.vert is lp.vert:
                    bad += 1
    return c, vol, bad // 2


def fix_inverted_islands(log):
    """Repair face islands whose winding is inside-out or internally inconsistent.

    Reported in the build log; the source .blend and the modeler's build scripts are untouched.
    In W1-03 this hits the two trouser-leg tubes of chopper_body (LEG table runs top->down,
    so the tube faces inward) and chopper_chainmail (both shells inward, hem rim outward),
    which Unity's default back-face culling would draw inside-out.
    """
    for name in PARTS:
        me = bpy.data.objects[name].data
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        seen, fixed = set(), []
        for f0 in bm.faces:
            if f0.index in seen:
                continue
            stack, comp = [f0], []
            seen.add(f0.index)
            while stack:
                f = stack.pop()
                comp.append(f)
                for e in f.edges:
                    for g in e.link_faces:
                        if g.index not in seen:
                            seen.add(g.index)
                            stack.append(g)
            c, vol, bad = _island_stats(comp)
            if vol < 0 or bad:
                bmesh.ops.recalc_face_normals(bm, faces=comp)
                c2, vol2, bad2 = _island_stats(comp)
                if vol2 < 0:
                    bmesh.ops.reverse_faces(bm, faces=comp)
                    c2, vol2, bad2 = _island_stats(comp)
                fixed.append("%d faces at (x %.2f, z %.2f): volume %+.5f -> %+.5f, inconsistent edges %d -> %d"
                             % (len(comp), c.x, c.z, vol, vol2, bad, bad2))
        if fixed:
            bm.to_mesh(me)
            me.update()
            for fx in fixed:
                log.append("  MESH FIX %s: %s" % (name, fx))
        bm.free()


def main():
    bpy.ops.wm.open_mainfile(filepath=SRC)
    scene = bpy.context.scene
    scene.render.fps = 30
    body = bpy.data.objects["chopper_body"]
    info = measure(body)
    log = ["chopper_rig build log", "mesh winding:"]
    fix_inverted_islands(log)
    log.append("measured joints:")
    for k, v in info.items():
        log.append("  %s: %s" % (k, {kk: (tuple(round(x, 4) for x in vv) if hasattr(vv, "__len__") else round(vv, 4))
                                     for kk, vv in v.items()}))
    rig = build_armature(info)
    log.append("bones:")
    for b in rig.data.bones:
        log.append("  %-14s parent=%-13s deform=%d head=(%.3f %.3f %.3f) tail=(%.3f %.3f %.3f)" % (
            b.name, b.parent.name if b.parent else "-", b.use_deform, *b.head_local, *b.tail_local))
    log.append("skin:")
    skin(rig, info, log)
    print("\n".join(log))
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("saved", OUT)


if __name__ == "__main__":
    main()
