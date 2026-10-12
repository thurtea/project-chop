"""Verify the exported Chopper FBX files and render the rig review images.

Run:  blender -b --factory-startup -P check_rig.py
In:   Art/Export/chopper.fbx, chopper_axe.fbx, chopper_axe_bear.fbx, chopper_shield.fbx (imported, nothing else read)
Out:  Art/Renders/rig_check_report.txt and Art/Renders/rig_*.png

Everything is measured on the IMPORTED data in an empty scene (what a game engine sees),
not on the authoring .blend.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
EXPORT = os.path.join(ART, "Export")
RENDERS = os.path.join(ART, "Renders")
CHAR_FBX = os.path.join(EXPORT, "chopper.fbx")
AXE_FBX = os.path.join(EXPORT, "chopper_axe.fbx")
BEAR_FBX = os.path.join(EXPORT, "chopper_axe_bear.fbx")
SHIELD_FBX = os.path.join(EXPORT, "chopper_shield.fbx")
REPORT = os.path.join(RENDERS, "rig_check_report.txt")

PARTS = ["chopper_body", "chopper_head", "chopper_beard", "chopper_hair", "chopper_shirt",
         "chopper_vest", "chopper_belt", "chopper_bracers", "chopper_chainmail", "chopper_boots",
         "chopper_helmet",
         "chopper_plate", "chopper_pauldrons"]
HUMANOID = ["Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
            "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes"]
SOCKETS = ["Weapon_R", "Shield_L", "Shield_Back"]
TAKES = {"Idle": 60, "Walk": 30, "Run": 18, "Sprint": 16, "CrouchIdle": 40, "CrouchWalk": 24, "Air": 20, "LandHard": 18,
         "Roll": 24, "SwimIdle": 40, "Swim": 24, "Attack_1": 22, "Attack_2": 22, "Attack_3": 28, "HitReact": 15,
         "Death": 30}
LOOP_TAKES = {"Idle", "Walk", "Run", "Sprint", "CrouchIdle", "CrouchWalk", "Air", "SwimIdle", "Swim"}
# Ground speed of each in-place gait (= animate_chopper GAITS), for the planted-foot slip check.
GAIT_SPEEDS = {"Run": 4.10, "Sprint": 6.50, "CrouchWalk": 1.60}
# Whole-body moves that must not sink into the ground (lowest vertex of every mesh).
GROUND_TAKES = {"Roll", "Death", "LandHard", "CrouchIdle"}
FPS = 30
WALK_SPEED = 0.75                          # m/s, = animate_chopper.WALK_SPEED (ground speed for the slip check)
FBX_KTIME = 46186158000

IMPORT = dict(use_anim=True, anim_offset=1.0, ignore_leaf_bones=False, automatic_bone_orientation=False,
              primary_bone_axis="Y", secondary_bone_axis="X", axis_forward="-Z", axis_up="Y",
              use_manual_orientation=False, global_scale=1.0, bake_space_transform=False,
              use_custom_normals=True, use_image_search=False)

lines = []
results = []


def log(s=""):
    print(s)
    lines.append(s)


def check(ok, msg):
    results.append(bool(ok))
    log("  [%s] %s" % ("PASS" if ok else "FAIL", msg))
    return ok


# ------------------------------------------------------------------ raw FBX parsing
def fbx_scan(path):
    from io_scene_fbx import parse_fbx
    root, version = parse_fbx.parse(path)
    out = {"version": version, "takes": {}, "limbs": [], "meshes": [], "global": {}, "videos": 0}

    def props70(elem):
        d = {}
        for p in elem.elems:
            if p.id == b"Properties70":
                for pp in p.elems:
                    d[pp.props[0].decode()] = pp.props[4:] if len(pp.props) > 4 else ()
        return d

    for e in root.elems:
        if e.id == b"GlobalSettings":
            out["global"] = props70(e)
        if e.id != b"Objects":
            continue
        for o in e.elems:
            name = o.props[1].split(b"\x00\x01")[0].decode() if len(o.props) > 1 else ""
            if o.id == b"AnimationStack":
                p = props70(o)
                out["takes"][name] = (p.get("LocalStart", (0,))[0], p.get("LocalStop", (0,))[0])
            elif o.id == b"Model" and len(o.props) > 2 and o.props[2] == b"LimbNode":
                out["limbs"].append(name)
            elif o.id == b"Model" and len(o.props) > 2 and o.props[2] == b"Mesh":
                out["meshes"].append(name)
            elif o.id == b"Video":
                out["videos"] += 1
    return out


# ------------------------------------------------------------------ helpers
def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, **IMPORT)
    return [o for o in bpy.data.objects if o not in before]


def eval_points(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    oe = ob.evaluated_get(dg)
    me = oe.to_mesh()
    mw = oe.matrix_world.copy()
    pts = [mw @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    oe.to_mesh_clear()
    return pts, polys


def dominant_group(ob, v):
    best, bw = None, -1
    for g in v.groups:
        if g.weight > bw:
            best, bw = ob.vertex_groups[g.group].name, g.weight
    return best


def set_action(rig, take):
    ad = rig.animation_data or rig.animation_data_create()
    act = next((a for a in bpy.data.actions if a.name.split("|")[-1] == take), None)
    ad.action = act
    if act is not None and hasattr(ad, "action_slot") and len(act.slots):
        ad.action_slot = act.slots[0]
    return act


def socket_world(rig, name):
    return rig.matrix_world @ rig.pose.bones[name].matrix


def bbox(pts):
    return (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
            Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))


def inside(p, lo, hi, eps=0.0):
    return all(lo[i] - eps <= p[i] <= hi[i] + eps for i in range(3))


def attach(ob, rig, bone):
    """Identity local transform relative to the socket bone head (what Unity does)."""
    ob.parent = rig
    ob.parent_type = "BONE"
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Translation((0, -rig.data.bones[bone].length, 0))
    ob.matrix_basis = Matrix.Identity(4)


DIAG = {}


SDIAG = {}


def penetration(prop_pts, meshes, skip=None, depth=0.004, local=None, allow=None, diag=None):
    """Count prop vertices that are inside (behind the nearest face) any of the meshes."""
    worst, count = 0.0, 0
    for ob in meshes:
        pts, polys = eval_points(ob)
        tree = BVHTree.FromPolygons(pts, polys)
        for i, p in enumerate(prop_pts):
            if skip and skip(i):
                continue
            hit = tree.find_nearest(p, 0.10)
            if hit[0] is None:
                continue
            loc, nrm, fi, dist = hit
            s = (p - loc).dot(nrm)
            # behind the face AND the nearest point is on the face interior (offset mostly
            # along the normal); near edges/corners the face-normal sign is meaningless
            if s < 0 and -s < 0.8 * dist:
                continue
            if s < -depth and allow is not None and allow(ob, fi, i):
                continue
            if s < -depth:
                count += 1
                worst = max(worst, -s)
                if local is not None:
                    d = (DIAG if diag is None else diag).setdefault(ob.name, [1e9, -1e9, 0])
                    d[0] = min(d[0], local[i].z)
                    d[1] = max(d[1], local[i].z)
                    d[2] += 1
    return count, worst


# ------------------------------------------------------------------ main checks
def main():
    os.makedirs(RENDERS, exist_ok=True)
    log("Chopper rig / FBX check report")
    log("=" * 72)
    for p in (CHAR_FBX, AXE_FBX, BEAR_FBX, SHIELD_FBX):
        check(os.path.isfile(p), "file exists: %s (%d bytes)" % (os.path.relpath(p, ART),
                                                                os.path.getsize(p) if os.path.isfile(p) else 0))

    # ---------------- raw FBX content
    log("")
    log("RAW FBX: chopper.fbx")
    log("-" * 72)
    raw = fbx_scan(CHAR_FBX)
    g = raw["global"]
    log("  FBX version %s, UpAxis=%s FrontAxis=%s CoordAxis=%s UnitScaleFactor=%s" % (
        raw["version"], g.get("UpAxis"), g.get("FrontAxis"), g.get("CoordAxis"), g.get("UnitScaleFactor")))
    log("  bones in file (%d): %s" % (len(raw["limbs"]), ", ".join(raw["limbs"])))
    check(not [n for n in raw["limbs"] if n.lower().endswith("_end")], "no leaf bones ('_end') in the FBX")
    check(raw["videos"] == 0, "no embedded media (Video objects: %d)" % raw["videos"])
    check(sorted(raw["meshes"]) == sorted(PARTS), "mesh models are exactly the modules: %s" % sorted(raw["meshes"]))
    for take, length in TAKES.items():
        if take not in raw["takes"]:
            check(False, "take %s present" % take)
            continue
        a, b = raw["takes"][take]
        fa, fb = a / FBX_KTIME * FPS, b / FBX_KTIME * FPS
        check(abs(fa - 0) < 1e-3 and abs(fb - length) < 1e-3,
              "take %s present, frames %.2f-%.2f (expected 0-%d = %.2f s at %d fps)"
              % (take, fa, fb, length, length / FPS, FPS))
    check(sorted(raw["takes"]) == sorted(TAKES), "takes are exactly %s (found %s)" % (sorted(TAKES), sorted(raw["takes"])))

    # ---------------- import character
    fresh()
    new = import_fbx(CHAR_FBX)
    rigs = [o for o in new if o.type == "ARMATURE"]
    meshes = {o.name: o for o in new if o.type == "MESH"}
    log("")
    log("IMPORTED chopper.fbx")
    log("-" * 72)
    check(len(rigs) == 1 and rigs[0].name == "chopper_rig", "one armature named chopper_rig (%s)"
          % [r.name for r in rigs])
    rig = rigs[0]
    log("  armature transform: loc %s rot %s scale %s" % (tuple(round(x, 4) for x in rig.location),
                                                          tuple(round(math.degrees(x), 3) for x in rig.rotation_euler),
                                                          tuple(round(x, 4) for x in rig.scale)))
    check(rig.matrix_world.to_translation().length < 1e-4 and
          max(abs(x - 1) for x in rig.matrix_world.to_scale()) < 1e-4,
          "armature at origin, scale 1 after import")
    names = [b.name for b in rig.data.bones]
    missing = [n for n in HUMANOID + SOCKETS if n not in names]
    check(not missing, "all %d humanoid bones + 3 sockets present (missing: %s)" % (len(HUMANOID), missing or "none"))
    check(not [n for n in names if n.lower().endswith("_end")], "no bones ending in _end after import")
    hips = rig.data.bones.get("Hips")
    check(hips is not None and hips.parent is None, "Hips is the root bone")
    for s, par in (("Weapon_R", "RightHand"), ("Shield_L", "LeftLowerArm"), ("Shield_Back", "UpperChest")):
        b = rig.data.bones.get(s)
        check(b is not None and b.parent is not None and b.parent.name == par,
              "socket %s is a child of %s" % (s, par))
    expected_parent = {"Spine": "Hips", "Chest": "Spine", "UpperChest": "Chest", "Neck": "UpperChest",
                       "Head": "Neck"}
    for sd in ("Left", "Right"):
        expected_parent.update({sd + "Shoulder": "UpperChest", sd + "UpperArm": sd + "Shoulder",
                                sd + "LowerArm": sd + "UpperArm", sd + "Hand": sd + "LowerArm",
                                sd + "UpperLeg": "Hips", sd + "LowerLeg": sd + "UpperLeg",
                                sd + "Foot": sd + "LowerLeg", sd + "Toes": sd + "Foot"})
    wrong = [n for n, par in expected_parent.items()
             if n in rig.data.bones and (rig.data.bones[n].parent is None or rig.data.bones[n].parent.name != par)]
    check(not wrong, "humanoid hierarchy (Hips > Spine > Chest > UpperChest > Neck > Head, shoulders under "
          "UpperChest, legs under Hips) (wrong: %s)" % (wrong or "none"))
    skinned = set()
    for ob in meshes.values():
        for v in ob.data.vertices:
            for gg in v.groups:
                if gg.weight > 0:
                    skinned.add(ob.vertex_groups[gg.group].name)
    check(not (skinned & set(SOCKETS)) and skinned <= set(HUMANOID),
          "skin clusters only on humanoid bones (%d bones carry weights), none on the sockets" % len(skinned))
    # mirrored rest frames: right = mirror of left (Y and Z mirrored, X flipped), i.e. rolls mirrored
    Mx = Matrix.Diagonal((-1.0, 1.0, 1.0))
    worst = 0.0
    for n in HUMANOID:
        if not n.startswith("Left"):
            continue
        L = rig.data.bones[n].matrix_local
        Rb = rig.data.bones["Right" + n[4:]].matrix_local
        Lr, Rr = L.to_3x3(), Rb.to_3x3()
        exp = [-(Mx @ Lr.col[0]), Mx @ Lr.col[1], Mx @ Lr.col[2]]
        for k in range(3):
            worst = max(worst, (Vector(Rr.col[k]) - exp[k]).length)
        worst = max(worst, (Rb.translation - Mx @ L.translation).length)
    check(worst < 1e-3, "left/right rest frames mirror-symmetric (positions and rolls), max error %.1e" % worst)
    check(sorted(meshes) == sorted(PARTS), "character meshes imported: %s" % sorted(meshes))
    for n, ob in sorted(meshes.items()):
        arm_mod = [m for m in ob.modifiers if m.type == "ARMATURE" and m.object == rig]
        check(ob.parent == rig and arm_mod, "%s parented to chopper_rig with an Armature modifier" % n)

    # weights
    log("")
    log("SKIN WEIGHTS (imported)")
    deform = set(names) - set(SOCKETS)
    for n, ob in sorted(meshes.items()):
        unweighted, over4, badsum, maxinf = 0, 0, 0, 0
        for v in ob.data.vertices:
            ws = [g.weight for g in v.groups if ob.vertex_groups[g.group].name in deform and g.weight > 0]
            maxinf = max(maxinf, len(ws))
            if not ws:
                unweighted += 1
            if len(ws) > 4:
                over4 += 1
            if ws and abs(sum(ws) - 1) > 0.01:
                badsum += 1
        check(unweighted == 0 and over4 == 0 and badsum == 0,
              "%-18s %4d verts: unweighted %d, >4 influences %d, not normalized %d (max influences %d)"
              % (n, len(ob.data.vertices), unweighted, over4, badsum, maxinf))
    for n in ("chopper_head", "chopper_hair", "chopper_beard", "chopper_helmet"):
        ob = meshes[n]
        only_head = all(len(v.groups) == 1 and ob.vertex_groups[v.groups[0].group].name == "Head"
                        and abs(v.groups[0].weight - 1) < 1e-4 for v in ob.data.vertices)
        check(only_head, "%s is 100%% Head" % n)

    # rest pose dimensions
    log("")
    log("REST POSE (imported)")
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    allpts = []
    for ob in meshes.values():
        allpts += eval_points(ob)[0]
    lo, hi = bbox(allpts)
    h = hi.z - lo.z
    log("  bounds x %.3f..%.3f  y %.3f..%.3f  z %.3f..%.3f" % (lo.x, hi.x, lo.y, hi.y, lo.z, hi.z))
    check(1.95 <= h <= 2.15, "character height %.4f m in [1.95, 2.15] (horns above the hair)" % h)
    check(abs(lo.z) < 0.005, "soles at Z=0 in rest (min z %.4f)" % lo.z)
    front = sum(p.y for p in allpts if p.z > 1.4) / max(1, len([p for p in allpts if p.z > 1.4]))
    check(min(p.y for p in eval_points(meshes["chopper_head"])[0]) < -0.15, "character faces -Y (nose at y < -0.15)")
    ua = rig.data.bones["LeftUpperArm"]
    direction = (ua.tail_local - ua.head_local).normalized()
    drop = math.degrees(math.atan2(-direction.z, abs(direction.x)))
    check(35.0 <= drop <= 55.0, "rest pose is an A-pose: left upper arm %.1f deg below horizontal" % drop)

    # hand region for the weapon socket
    body = meshes["chopper_body"]
    rhand = [v.index for v in body.data.vertices if dominant_group(body, v) == "RightHand"]
    lhand = [v.index for v in body.data.vertices if dominant_group(body, v) == "LeftHand"]

    def hand_box(idx):
        pts = eval_points(body)[0]
        return bbox([pts[i] for i in idx])

    lo_h, hi_h = hand_box(rhand)
    sp = socket_world(rig, "Weapon_R").translation
    check(inside(sp, lo_h, hi_h), "rest: Weapon_R (%.3f %.3f %.3f) inside right-hand bounds %s..%s"
          % (*sp, tuple(round(x, 3) for x in lo_h), tuple(round(x, 3) for x in hi_h)))
    rig.data.pose_position = "POSE"

    # ---------------- animation checks
    boots = meshes["chopper_boots"]
    side_of = {}
    for v in boots.data.vertices:
        gname = dominant_group(boots, v) or ""
        side_of[v.index] = "Left" if gname.startswith("Left") else "Right"
    sc = bpy.context.scene
    for take, length in TAKES.items():
        log("")
        log("TAKE %s (imported)" % take)
        act = set_action(rig, take)
        if not check(act is not None, "action for take %s found (%s)" % (take, act.name if act else None)):
            continue
        fr = act.frame_range
        check(abs(fr[0] - 1) < 1e-3 and abs(fr[1] - (length + 1)) < 1e-3,
              "imported frame range %.0f-%.0f (expected 1-%d)" % (fr[0], fr[1], length + 1))
        # loop seam only for clips that are authored to repeat
        if take in LOOP_TAKES:
            sc.frame_set(1)
            first = {n: eval_points(ob)[0] for n, ob in meshes.items()}
            bones_first = {pb.name: pb.matrix.copy() for pb in rig.pose.bones}
            sc.frame_set(length + 1)
            last = {n: eval_points(ob)[0] for n, ob in meshes.items()}
            dv = max((a - b).length for n in meshes for a, b in zip(first[n], last[n]))
            db = max(max(abs(x) for row in (pb.matrix - bones_first[pb.name]) for x in row) for pb in rig.pose.bones)
            check(dv < 5e-4 and db < 5e-4, "first and last frame identical: max vertex delta %.2e m, max bone matrix delta %.2e"
                  % (dv, db))
        else:
            sc.frame_set(1)
            a0 = eval_points(meshes["chopper_body"])[0]
            sc.frame_set(max(2, length // 2))
            a1 = eval_points(meshes["chopper_body"])[0]
            moved = max((a - b).length for a, b in zip(a0, a1))
            check(moved > 0.02, "%s moves (body vertex delta mid-clip %.3f m)" % (take, moved))
            low = 0.0
            for f in range(1, length + 2, 2):
                sc.frame_set(f)
                pts = eval_points(boots)[0]
                low = min(low, min(p.z for p in pts))
            check(low >= -0.02, "%s: feet stay above Z=-0.02 (lowest %.4f)" % (take, low))
        if take == "Idle":
            outside = []
            for f in range(1, length + 2):
                sc.frame_set(f)
                lo_h, hi_h = hand_box(rhand)
                sp = socket_world(rig, "Weapon_R").translation
                if not inside(sp, lo_h, hi_h):
                    outside.append(f)
            check(not outside, "Idle: Weapon_R inside right-hand bounds on all %d frames (outside: %s)"
                  % (length + 1, outside or "none"))
            # feet stay planted in idle
            worst = 0.0
            sc.frame_set(1)
            p1 = eval_points(boots)[0]
            for f in range(1, length + 2):
                sc.frame_set(f)
                pts = eval_points(boots)[0]
                worst = max(worst, max((a - b).length for a, b in zip(pts, p1) if b.z < 0.03))
            check(worst < 0.005, "Idle: soles stay planted (max sole vertex drift %.4f m)" % worst)
        if take == "Walk":
            log("  frame  minZ_L   minZ_R   planted  slip(m/frame vs ground %.2f m/s)" % WALK_SPEED)
            bad, sink, slips = [], [], []
            prev = None
            hip_track = []
            for f in range(1, length + 2):
                sc.frame_set(f)
                pts = eval_points(boots)[0]
                mz = {"Left": 1e9, "Right": 1e9}
                for i, p in enumerate(pts):
                    s = side_of[i]
                    mz[s] = min(mz[s], p.z)
                planted = min(mz, key=mz.get)
                if not (-0.01 <= mz[planted] <= 0.03):
                    bad.append(f)
                if min(mz.values()) < -0.01:
                    sink.append(f)
                slip = 0.0
                if prev is not None:
                    gstep = WALK_SPEED / FPS
                    for i, (a, b) in enumerate(zip(prev, pts)):
                        if a.z < 0.004 and b.z < 0.004:
                            d = (b - a) - Vector((0, gstep, 0))
                            slip = max(slip, Vector((d.x, d.y)).length)
                    slips.append(slip)
                prev = pts
                hip_track.append(rig.pose.bones["Hips"].head.z)
                log("  %5d  %+.4f  %+.4f  %-6s  %.4f" % (f, mz["Left"], mz["Right"], planted, slip))
            check(not bad, "Walk: planted foot lowest vertex within -0.01..0.03 m of Z=0 on all frames (bad: %s)"
                  % (bad or "none"))
            check(not sink, "Walk: no foot below Z=-0.01 on any frame (bad: %s)" % (sink or "none"))
            check(max(slips) < 0.005, "Walk: planted contact vertices move with the ground (in-place %.2f m/s), "
                  "max slip %.4f m/frame" % (WALK_SPEED, max(slips)))
            log("  hips height range %.3f..%.3f m (down/up bob %.1f cm)" % (min(hip_track), max(hip_track),
                                                                         100 * (max(hip_track) - min(hip_track))))
        if take in GROUND_TAKES:
            low, at = 1e9, 0
            for f in range(1, length + 2):
                sc.frame_set(f)
                z = min(p.z for ob in meshes.values() for p in eval_points(ob)[0])
                if z < low:
                    low, at = z, f
            check(low >= -0.06, "%s: whole body stays above Z=-0.06 (lowest %.3f m at frame %d)" % (take, low, at))
        if take in GAIT_SPEEDS:
            gait_speed = GAIT_SPEEDS[take]
            bad, sink, slips = [], [], []
            prev = None
            for f in range(1, length + 2):
                sc.frame_set(f)
                pts = eval_points(boots)[0]
                mz = {"Left": 1e9, "Right": 1e9}
                for i, p in enumerate(pts):
                    mz[side_of[i]] = min(mz[side_of[i]], p.z)
                planted = min(mz, key=mz.get)
                # a run has a flight phase: only a foot near the ground counts as planted
                if mz[planted] < 0.04 and mz[planted] < -0.01:
                    bad.append(f)
                if min(mz.values()) < -0.01:
                    sink.append(f)
                slip = 0.0
                if prev is not None:
                    gstep = gait_speed / FPS
                    for i, (a, b) in enumerate(zip(prev, pts)):
                        if a.z < 0.004 and b.z < 0.004:
                            d = (b - a) - Vector((0, gstep, 0))
                            slip = max(slip, Vector((d.x, d.y)).length)
                    slips.append(slip)
                prev = pts
            check(not bad, "%s: planted foot not below -0.01 m (bad: %s)" % (take, bad or "none"))
            check(not sink, "%s: no foot below Z=-0.01 (bad: %s)" % (take, sink or "none"))
            check(slips and max(slips) < 0.008 + 0.001 * gait_speed, "%s: planted slip max %.4f m/frame at %.2f m/s" % (
                take, max(slips) if slips else -1, gait_speed))

    # ---------------- props
    log("")
    log("PROPS")
    log("-" * 72)
    expected = {"chopper_axe": (AXE_FBX, (-0.160, 0.692)), "chopper_axe_bear": (BEAR_FBX, (-0.170, 0.742)),
                "chopper_shield": (SHIELD_FBX, (-0.562, 0.412))}
    prop_objs = {}
    for name, (path, zr) in expected.items():
        before = set(bpy.data.objects)
        objs = import_fbx(path)
        ms = [o for o in objs if o.type == "MESH"]
        ok = len(objs) == 1 and len(ms) == 1 and ms[0].name == name
        check(ok, "%s imports as exactly one mesh named %s (got %s)" % (os.path.basename(path), name,
                                                                         [(o.name, o.type) for o in objs]))
        if ms:
            ob = ms[0]
            pts = [ob.matrix_world @ v.co for v in ob.data.vertices]
            lo, hi = bbox(pts)
            ob.data.calc_loop_triangles()
            log("  %s: %d tris, transform loc %s rot %s scale %s, bounds z %.3f..%.3f"
                % (name, len(ob.data.loop_triangles), tuple(round(x, 4) for x in ob.location),
                   tuple(round(math.degrees(x), 2) for x in ob.rotation_euler), tuple(round(x, 4) for x in ob.scale),
                   lo.z, hi.z))
            check(abs(lo.z - zr[0]) < 0.002 and abs(hi.z - zr[1]) < 0.002 and ob.matrix_world.to_translation().length < 1e-4,
                  "%s origin preserved (world bounds z %.3f..%.3f, expected %.3f..%.3f)" % (name, lo.z, hi.z, *zr))
            prop_objs[name] = ob
    axe, shield = prop_objs.get("chopper_axe"), prop_objs.get("chopper_shield")
    bear = prop_objs.get("chopper_axe_bear")

    # ---------------- props on sockets (identity local transform), clipping
    log("")
    log("PROPS ON SOCKETS (identity local transform relative to the socket bone)")
    attach(axe, rig, "Weapon_R")
    attach(bear, rig, "Weapon_R")
    attach(shield, rig, "Shield_L")
    # grip convention (W6-01): on the same socket, the bear axe's haft and blade must point
    # where chopper_axe's haft and +X ("front", outward) blade point
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()

    def head_blade(ob):
        mw = ob.matrix_world
        pts = [mw @ v.co for v in ob.data.vertices]
        grip = mw.to_translation()
        hz = mw.to_3x3() @ Vector((0, 0, 1))
        hx = mw.to_3x3() @ Vector((1, 0, 0))
        # haft centre line: centroid of the haft vertices 0.15..0.40 above the grip (axis, not the head)
        loc = [mw.inverted() @ p for p in pts]
        hv = [p for p, q in zip(pts, loc) if 0.15 <= q.z <= 0.40 and abs(q.x) < 0.03 and abs(q.y) < 0.03]
        hc = sum(hv, Vector()) / max(1, len(hv))
        blade = [p for p in pts if (p - grip).dot(hx) > 0.10]
        bc = sum(blade, Vector()) / max(1, len(blade))
        return grip, hz, (hc - grip).normalized(), (bc - grip).normalized(), len(blade)
    g_a, z_a, head_a, _, _ = head_blade(axe)
    g_b, z_b, head_b, blade_b, nb = head_blade(bear)
    x_a = axe.matrix_world.to_3x3() @ Vector((1, 0, 0))
    perp = (blade_b - z_a * blade_b.dot(z_a)).normalized()
    check((g_a - g_b).length < 1e-4 and head_a.dot(head_b) > 0.999 and perp.dot(x_a) > 0.9 and nb > 0,
          "rest: chopper_axe_bear grips like chopper_axe on Weapon_R: grip offset %.1f mm, haft direction "
          "dot %.4f, blade (perpendicular to the haft) vs chopper_axe +X blade dot %.3f, world blade dir "
          "(%.2f %.2f %.2f)" % (1000 * (g_a - g_b).length, head_a.dot(head_b), perp.dot(x_a), *perp))
    rig.data.pose_position = "POSE"
    body_like = [meshes[n] for n in PARTS]
    sh_local = [v.co.copy() for v in shield.data.vertices]
    # the shield hand grip sits inside the closed left hand: allowed only against the left
    # hand's own faces and only for the grip region (local z -0.25..-0.22, |x|<0.12, y -0.10..0.015)
    lhand_set = set(lhand)
    lhand_faces = {p.index for p in body.data.polygons if all(vi in lhand_set for vi in p.vertices)}
    in_grip = lambda i: (-0.25 <= sh_local[i].z <= -0.22 and abs(sh_local[i].x) < 0.12  # noqa: E731
                         and -0.10 <= sh_local[i].y <= 0.015)
    strap = None
    sh_allow = lambda ob, fi, i: ob == body and fi in lhand_faces and in_grip(i)  # noqa: E731
    # board back = board/rim faces facing the arm (+Y local), excluding strap and grip parts
    # sampled on the surface (6x6 per face): the board grid rows are ~14 cm apart, too coarse
    board_back = [tuple(p.vertices) for p in shield.data.polygons if p.normal.y > 0.9 and p.center.y < -0.06]
    # forearm + bracer surface (left): body faces dominated by LeftLowerArm, plus the left bracer
    lfore_set = {v.index for v in body.data.vertices if dominant_group(body, v) == "LeftLowerArm"}
    fore_faces = [p.index for p in body.data.polygons if all(vi in lfore_set for vi in p.vertices)]
    belt = meshes["chopper_bracers"]
    bracer_faces = [p.index for p in belt.data.polygons
                    if all(dominant_group(belt, belt.data.vertices[vi]) in ("LeftLowerArm", "LeftHand")
                           for vi in p.vertices)]
    grip_centre = Vector((0.0, 0.0, -0.235))

    grip_stats = []

    def grip_through_fist():
        """In the LeftHand bone frame (oriented; a world AABB is too loose for the tilted hand):
        bar centre inside the hand box and the bar spanning >= 90 % of the hand width along it."""
        inv = (rig.matrix_world @ rig.pose.bones["LeftHand"].matrix).inverted()
        bp = eval_points(body)[0]
        lo, hi = bbox([inv @ bp[i] for i in lhand])
        mw = shield.matrix_world
        c = inv @ (mw @ grip_centre)
        a, b = inv @ (mw @ Vector((-0.108, 0, -0.235))), inv @ (mw @ Vector((0.108, 0, -0.235)))
        k = max(range(3), key=lambda j: abs(b[j] - a[j]))           # axis the bar runs along
        span = (max(a[k], b[k]) - min(a[k], b[k]))
        cover = (min(max(a[k], b[k]), hi[k]) - max(min(a[k], b[k]), lo[k])) / (hi[k] - lo[k])
        grip_stats.append((c.copy(), cover, lo[k], hi[k], span))
        return inside(c, lo, hi) and cover >= 0.9

    def board_gap():
        bp, bpolys = eval_points(body)
        tp, tpolys = eval_points(belt)
        verts = bp + tp
        off = len(bp)
        polys = [bpolys[i] for i in fore_faces] + [tuple(off + k for k in tpolys[i]) for i in bracer_faces]
        tree = BVHTree.FromPolygons(verts, polys)
        mw = shield.matrix_world
        wpts = [mw @ c for c in sh_local]
        best = 1e9
        for p in surface_samples(wpts, board_back, lambda poly: True, n=6):
            hit = tree.find_nearest(p, 0.5)
            if hit[0] is not None:
                best = min(best, hit[3])
        return best
    ax_local = [v.co.copy() for v in axe.data.vertices]
    # the haft passes through the closed right hand: allowed only for faces of the right
    # hand itself and only for the grip section of the haft
    hand_set = set(rhand)
    hand_faces = {p.index for p in body.data.polygons if all(vi in hand_set for vi in p.vertices)}
    in_fist = lambda ob, fi, i: (ob == body and fi in hand_faces  # noqa: E731
                                 and -0.15 < ax_local[i].z < 0.15)
    bear_local = [v.co.copy() for v in bear.data.vertices]
    in_fist_b = lambda ob, fi, i: (ob == body and fi in hand_faces  # noqa: E731
                                   and -0.15 < bear_local[i].z < 0.15)

    def prop_pass(label, frames, take):
        if take:
            set_action(rig, take)
        else:
            rig.data.pose_position = "REST"
        worst_s, worst_a, cnt_s, cnt_a, axe_hand = 0.0, 0.0, 0, 0, True
        worst_b, cnt_b = 0.0, 0
        gaps, grip_in = [], True
        for f in frames:
            sc.frame_set(f)
            bpy.context.view_layer.update()
            sp = [shield.matrix_world @ c for c in sh_local]
            ap = [axe.matrix_world @ c for c in ax_local]
            c, w = penetration(sp, body_like, allow=sh_allow, local=sh_local, diag=SDIAG)
            gaps.append(board_gap())
            grip_in &= grip_through_fist()
            cnt_s += c
            worst_s = max(worst_s, w)
            c, w = penetration(ap, body_like, local=ax_local, allow=in_fist)
            cnt_a += c
            worst_a = max(worst_a, w)
            bp = [bear.matrix_world @ c for c in bear_local]
            c, w = penetration(bp, body_like, allow=in_fist_b)
            cnt_b += c
            worst_b = max(worst_b, w)
            lo_h, hi_h = hand_box(rhand)
            grip = axe.matrix_world.to_translation()
            axe_hand &= inside(grip, lo_h, hi_h)
        rig.data.pose_position = "POSE"
        check(cnt_s == 0, "%s: shield clear of body, arm and clothing (penetrating verts %d, deepest %.1f mm)"
              % (label, cnt_s, 1000 * worst_s))
        check(0.005 <= min(gaps) and max(gaps) <= 0.03,
              "%s: shield board back not floating: gap to forearm/bracer surface %.1f..%.1f mm "
              "(min over the board, per sampled frame; required 5-30 mm)"
              % (label, 1000 * min(gaps), 1000 * max(gaps)))
        # the grip bar must pass through the fist: centre inside the hand, both bar ends outside it
        c, cover, lo_k, hi_k, span = min(grip_stats, key=lambda g: g[1])
        check(grip_in, "%s: shield grip bar passes through the left fist (bar centre inside the hand, bar "
              "covers >= 90%% of the hand width; worst frame: centre in hand frame (%.3f %.3f %.3f), "
              "covers %.0f%% of hand width %.3f..%.3f, bar %.3f m)"
              % (label, c.x, c.y, c.z, 100 * cover, lo_k, hi_k, span))
        grip_stats.clear()
        for k, (z0, z1, n) in sorted(SDIAG.items()):
            log("    shield contact with %s: %d vertex hits, shield local z %.3f..%.3f" % (k, n, z0, z1))
        SDIAG.clear()
        check(cnt_a == 0, "%s: axe clear of body and clothing outside the fist (penetrating verts %d, deepest %.1f mm)"
              % (label, cnt_a, 1000 * worst_a))
        check(axe_hand, "%s: axe grip point inside the right-hand bounds" % label)
        check(cnt_b == 0, "%s: bear axe clear of body and clothing outside the fist (penetrating verts %d, "
              "deepest %.1f mm)" % (label, cnt_b, 1000 * worst_b))
        for k, (z0, z1, n) in sorted(DIAG.items()):
            log("    axe contact with %s: %d vertex hits, axe local z %.3f..%.3f" % (k, n, z0, z1))
        DIAG.clear()

    # detector self-test: a shield pushed 6 cm into the chest and an axe through the thigh must be caught
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    probe_s = [Matrix.Translation((0.0, -0.13, 1.10)) @ c for c in sh_local]
    probe_a = [Matrix.Translation((0.13, 0.0, 0.40)) @ c for c in ax_local]
    cs, ws = penetration(probe_s, body_like)
    ca, wa = penetration(probe_a, body_like)
    rig.data.pose_position = "POSE"
    check(cs > 0 and ca > 0, "clip detector self-test: shield sunk into the chest -> %d verts (%.0f mm), "
          "axe through the leg -> %d verts (%.0f mm)" % (cs, 1000 * ws, ca, 1000 * wa))
    prop_pass("rest", [1], None)
    prop_pass("Idle (every 4th frame)", list(range(1, 62, 4)), "Idle")
    prop_pass("Walk (every 2nd frame)", list(range(1, 32, 2)), "Walk")

    attach(shield, rig, "Shield_Back")
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    sp = [shield.matrix_world @ c for c in sh_local]
    c_back, w_back = penetration(sp, body_like, allow=sh_allow, local=sh_local)
    mean_y = sum(p.y for p in sp) / len(sp)
    check(c_back == 0 and mean_y > 0.15,
          "Shield_Back rest: shield clear of the body and sitting on the back "
          "(penetrating %d, deepest %.1f mm, mean Y %.3f)" % (c_back, 1000 * w_back, mean_y))
    attach(shield, rig, "Shield_L")
    rig.data.pose_position = "POSE"

    # ---------------- clothing layering on the imported animation
    log("")
    log("CLOTHING LAYERS (surface samples of the inner layer vs the outer garment)")
    layering_checks(rig, meshes)

    # ---------------- beard against the chest (W6-03: the V spade hangs in front of the vest)
    log("")
    log("BEARD CLEARANCE (beard vertices vs vest, shirt and body)")
    beard_clearance(rig, meshes)

    # ---------------- renders
    bear.hide_render = True          # the rig review renders keep the double axe
    passing = render_all(rig, meshes, axe, shield)
    log("")
    log("RENDERS (Workbench 1024 px, from the imported FBX): %s" % ", ".join(sorted(passing)))

    log("")
    log("=" * 72)
    n_pass = sum(results)
    n_fail = len(results) - n_pass
    log("SUMMARY: %d PASS, %d FAIL -> %s" % (n_pass, n_fail, "ALL PASS" if n_fail == 0 else "FAILURES"))
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")
    print("wrote", REPORT)


# ------------------------------------------------------------------ clothing layers
def surface_samples(pts, polys, keep, n=4):
    out = []
    for poly in polys:
        if not keep(poly):
            continue
        vs = [pts[i] for i in poly]
        if len(vs) == 4:
            for a in range(n):
                for b in range(n):
                    u, v = (a + 0.5) / n, (b + 0.5) / n
                    out.append(vs[0].lerp(vs[1], u).lerp(vs[3].lerp(vs[2], u), v))
        else:
            out.append(sum(vs, Vector()) / len(vs))
    return out


def main_tube_faces(ob):
    """Faces of the largest centred island (the torso tube of a garment / of the body)."""
    me = ob.data
    parent = list(range(len(me.vertices)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for e in me.edges:
        ra, rb = find(e.vertices[0]), find(e.vertices[1])
        if ra != rb:
            parent[ra] = rb
    groups = {}
    for v in me.vertices:
        groups.setdefault(find(v.index), []).append(v.index)
    best = None
    for root, idx in groups.items():
        cx = sum(me.vertices[i].co.x for i in idx) / len(idx)
        if abs(cx) < 0.02 and (best is None or len(idx) > len(groups[best])):
            best = root
    return {p.index for p in me.polygons if find(p.vertices[0]) == best}


def layer_violations(inner_ob, inner_keep, outer_ob, outer_inner_faces, tol=0.002, outer_faces=None):
    """Inner-layer surface samples that are outside the outer garment.

    outer_inner_faces: set of outer-mesh face indices that form its inside wall (double shells);
    for single-shell garments pass an empty set. Returns (count, worst depth in m)."""
    ip, ipolys = eval_points(inner_ob)
    op, opolys = eval_points(outer_ob)
    samples = surface_samples(ip, ipolys, lambda poly: inner_keep(poly, [inner_ob.data.vertices[i].co for i in poly]))
    if outer_faces is not None:
        order = sorted(outer_faces)
        sub = [opolys[i] for i in order]
        tree = BVHTree.FromPolygons(op, sub)
        remap = order
    else:
        tree = BVHTree.FromPolygons(op, opolys)
        remap = None
    bad, worst = 0, 0.0
    for p in samples:
        loc, nrm, fi, dist = tree.find_nearest(p, 0.06)
        if loc is None:
            continue
        if remap is not None:
            fi = remap[fi]
        sgn = (p - loc).dot(nrm)
        if abs(sgn) < 0.8 * dist:      # nearest point on an edge/corner: not a reliable side test
            continue
        if fi in outer_inner_faces:
            if sgn < -tol:             # beyond the inside wall, i.e. inside the shell or through it
                bad += 1
                worst = max(worst, -sgn)
        elif sgn > tol:                # in front of the outer surface: poking through
            bad += 1
            worst = max(worst, sgn)
    return bad, worst


def inner_wall_faces(ob):
    me = ob.data
    out = set()
    for p in me.polygons:
        radial = Vector((p.center.x, p.center.y, 0.0))
        if radial.length > 1e-6 and p.normal.dot(radial.normalized()) < -0.3:
            out.add(p.index)
    return out


def layering_checks(rig, meshes):
    sc = bpy.context.scene
    shirt, chain, vest, body = (meshes[n] for n in ("chopper_shirt", "chopper_chainmail", "chopper_vest",
                                                      "chopper_body"))
    chain_inner = inner_wall_faces(chain)
    tube = {ob.name: main_tube_faces(ob) for ob in (shirt, vest, body)}
    leg_faces = set()
    for poly in body.data.polygons:
        cos = [body.data.vertices[i].co for i in poly.vertices]
        if abs(sum(c.x for c in cos) / len(cos)) > 0.05 and max(c.z for c in cos) <= 0.80:
            leg_faces.add(poly.index)
    tube["legs"] = leg_faces
    # map polygon vertex tuples back to face indices of the inner mesh
    polykey = {ob.name: {tuple(p.vertices): p.index for p in ob.data.polygons} for ob in (shirt, body)}

    def band(ob, lo, hi, key=None):
        faces = tube[key or ob.name]
        return lambda poly, cos: (polykey[ob.name].get(tuple(poly)) in faces
                                  and all(lo <= c.z <= hi for c in cos))
    pairs = [("shirt under chainmail", shirt, band(shirt, 0.57, 0.86), chain, chain_inner, None),
             ("shirt under vest", shirt, band(shirt, 0.83, 1.33), vest, set(), tube["chopper_vest"]),
             ("trousers/torso under shirt", body, band(body, 0.62, 1.33), shirt, set(), tube["chopper_shirt"]),
             ("thighs inside the shirt skirt", body, band(body, 0.52, 0.80, "legs"), shirt, set(),
              tube["chopper_shirt"]),
             ("thighs inside the chainmail", body, band(body, 0.57, 0.80, "legs"), chain, chain_inner, None)]
    for take, frames in (("rest", [1]), ("Idle", range(1, 62, 3)), ("Walk", range(1, 32))):
        if take == "rest":
            rig.data.pose_position = "REST"
        else:
            rig.data.pose_position = "POSE"
            set_action(rig, take)
        for label, inner, keep, outer, walls, ofaces in pairs:
            total, worst, worst_f = 0, 0.0, None
            for f in frames:
                sc.frame_set(f)
                n, w = layer_violations(inner, keep, outer, walls, outer_faces=ofaces)
                total += n
                if w > worst:
                    worst, worst_f = w, f
            check(total == 0, "%s: %s on %d frames (surface samples outside: %d, worst %.1f mm at frame %s)"
                  % (take, label, len(frames), total, 1000 * worst, worst_f))


def beard_clearance(rig, meshes):
    """Signed distance of every beard vertex to the nearest vest/shirt/body face (negative =
    inside). Required: nothing inside and at least 3 mm of air on every sampled frame."""
    sc = bpy.context.scene
    beard = meshes["chopper_beard"]
    others = [meshes[n] for n in ("chopper_vest", "chopper_shirt", "chopper_body")]
    for take, frames in (("rest", [1]), ("Idle", range(1, 62, 3)), ("Walk", range(1, 32)),
                         ("Run (info)", range(1, 22, 2))):
        if take == "rest":
            rig.data.pose_position = "REST"
        else:
            rig.data.pose_position = "POSE"
            set_action(rig, take.split()[0])
        worst, worst_f, worst_o = 1e9, None, None
        for f in frames:
            sc.frame_set(f)
            bp = eval_points(beard)[0]
            for ob in others:
                pts, polys = eval_points(ob)
                tree = BVHTree.FromPolygons(pts, polys)
                for p in bp:
                    hit = tree.find_nearest(p, 0.08)
                    if hit[0] is None:
                        continue
                    loc, nrm, fi, dist = hit
                    sgn = (p - loc).dot(nrm)
                    d = dist if sgn >= 0 or -sgn < 0.8 * dist else -dist
                    if d < worst:
                        worst, worst_f, worst_o = d, f, ob.name
        rig.data.pose_position = "POSE"
        msg = ("%s: beard clear of vest, shirt and body on %d frames (closest %.1f mm to %s at frame %s)"
               % (take, len(frames), 1000 * worst, worst_o, worst_f))
        if take.endswith("(info)"):
            log("  [INFO] " + msg)
        else:
            check(worst >= 0.003, msg)


# ------------------------------------------------------------------ renders
def bone_sticks(rig):
    mb = bmesh.new()
    col = mb.loops.layers.color.new("Col")
    for b in rig.data.bones:
        m = rig.matrix_world @ b.matrix_local
        L = b.length
        r = 0.03 if b.use_deform else 0.02
        if b.name in SOCKETS:
            c = (0.95, 0.1, 0.85, 1)
        elif b.name.startswith("Left"):
            c = (0.15, 0.85, 0.2, 1)
        elif b.name.startswith("Right"):
            c = (0.15, 0.4, 1.0, 1)
        else:
            c = (1.0, 0.85, 0.1, 1)
        # octahedral bone: head, 4 ring points at 10%, tail
        pts = [Vector((0, 0, 0)), Vector((r, 0.1 * L, 0)), Vector((0, 0.1 * L, r)),
               Vector((-r, 0.1 * L, 0)), Vector((0, 0.1 * L, -r)), Vector((0, L, 0))]
        vs = [mb.verts.new(m @ p) for p in pts]
        faces = []
        for k in range(4):
            a, bb = vs[1 + k], vs[1 + (k + 1) % 4]
            faces.append(mb.faces.new((vs[0], bb, a)))
            faces.append(mb.faces.new((vs[5], a, bb)))
        for f in faces:
            for lp in f.loops:
                lp[col] = c
    me = bpy.data.meshes.new("bone_sticks")
    mb.to_mesh(me)
    mb.free()
    if "Col" in me.color_attributes:
        me.color_attributes.active_color = me.color_attributes["Col"]
        me.color_attributes.render_color_index = me.color_attributes.active_color_index
    ob = bpy.data.objects.new("bone_sticks", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def setup_render(sc):
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = 1024
    sc.render.resolution_y = 1024
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "VERTEX"
    sh.show_object_outline = True
    sh.object_outline_color = (0.05, 0.05, 0.05)
    sh.show_shadows = False
    sh.show_xray = False
    sh.show_backface_culling = True       # like Unity's default Cull Back: inverted faces would show
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    sc.world.color = (0.72, 0.74, 0.77)
    sc.view_settings.view_transform = "Standard"
    cd = bpy.data.cameras.new("ReviewCam")
    cam = bpy.data.objects.new("ReviewCam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    # ground grid: thin dark plane slightly below zero so contact reads
    me = bpy.data.meshes.new("ground")
    bm = bmesh.new()
    c = bm.loops.layers.color.new("Col")
    for i in range(-4, 4):
        for j in range(-4, 4):
            vs = [bm.verts.new((x * 0.25, y * 0.25, -0.0005)) for x, y in
                  ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))]
            f = bm.faces.new(vs)
            g = 0.45 if (i + j) % 2 else 0.55
            for lp in f.loops:
                lp[c] = (g, g, g, 1)
    bm.to_mesh(me)
    bm.free()
    ground = bpy.data.objects.new("ground", me)
    sc.collection.objects.link(ground)
    return cam, ground


def shoot(sc, cam, name, target, yaw_deg, pitch_deg, ortho):
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    look = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.data.clip_end = 100
    cam.location = Vector(target) - look * 8
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(RENDERS, name + ".png")
    bpy.ops.render.render(write_still=True)
    return name + ".png"


def render_all(rig, meshes, axe, shield):
    sc = bpy.context.scene
    cam, ground = setup_render(sc)
    out = []
    axe.hide_render = shield.hide_render = True
    for extra in ("chopper_plate", "chopper_pauldrons"):
        if extra in meshes:
            meshes[extra].hide_render = True
    # rest with bones (x-ray so the bones show through the meshes)
    rig.data.pose_position = "REST"
    sc.frame_set(1)
    sticks = bone_sticks(rig)
    # pass 1: ghosted character; pass 2: opaque bones on a transparent film; alpha-over
    ground.hide_render = True
    sticks.hide_render = True
    sc.display.shading.show_xray = True
    sc.display.shading.xray_alpha = 0.45
    shoot(sc, cam, "_tmp_rest_char", (0, 0, 0.9), 0, 0, 2.05)
    sc.display.shading.show_xray = False
    sticks.hide_render = False
    for ob in meshes.values():
        ob.hide_render = True
    sc.render.film_transparent = True
    shoot(sc, cam, "_tmp_rest_bones", (0, 0, 0.9), 0, 0, 2.05)
    sc.render.film_transparent = False
    for ob in meshes.values():
        ob.hide_render = False
    sticks.hide_render = True
    ground.hide_render = False
    composite(os.path.join(RENDERS, "_tmp_rest_char.png"), os.path.join(RENDERS, "_tmp_rest_bones.png"),
              os.path.join(RENDERS, "rig_rest_front.png"))
    out.append("rig_rest_front.png")
    rig.data.pose_position = "POSE"
    # idle
    set_action(rig, "Idle")
    sc.frame_set(16)
    out.append(shoot(sc, cam, "rig_idle_front", (0, 0, 0.9), 0, 0, 2.05))
    # walk
    set_action(rig, "Walk")
    sc.frame_set(1)
    out.append(shoot(sc, cam, "rig_walk_contact_side", (0, 0, 0.9), -90, 0, 2.05))
    out.append(shoot(sc, cam, "rig_walk_contact_threequarter", (0, 0, 0.9), -35, 8, 2.05))
    pf = passing_frame(rig, meshes["chopper_boots"])
    sc.frame_set(pf)
    out.append(shoot(sc, cam, "rig_walk_passing_side", (0, 0, 0.9), -90, 0, 2.05))
    log("  passing frame used for rig_walk_passing_side: %d" % pf)
    # warrior idle with props on sockets
    axe.hide_render = shield.hide_render = False
    set_action(rig, "Idle")
    sc.frame_set(16)
    out.append(shoot(sc, cam, "rig_warrior_idle", (0, -0.1, 0.95), -30, 8, 2.3))
    return out


def composite(base_path, over_path, out_path):
    import numpy as np
    a = bpy.data.images.load(base_path)
    b = bpy.data.images.load(over_path)
    pa = np.array(a.pixels[:], dtype=np.float32).reshape(-1, 4)
    pb = np.array(b.pixels[:], dtype=np.float32).reshape(-1, 4)
    al = pb[:, 3:4]
    res = pa * (1 - al) + pb * al
    res[:, 3] = 1.0
    a.pixels[:] = res.ravel()
    a.filepath_raw = out_path
    a.file_format = "PNG"
    a.save()
    for im in (a, b):
        bpy.data.images.remove(im)
    for p in (base_path, over_path):
        os.remove(p)


def passing_frame(rig, boots):
    """Frame (first half cycle) where the swinging right foot passes the planted left foot."""
    sc = bpy.context.scene
    best, bf = 1e9, 1
    for f in range(1, 16):
        sc.frame_set(f)
        pts = eval_points(boots)[0]
        yl = [p.y for p in pts if p.x > 0]
        yr = [p.y for p in pts if p.x < 0]
        d = abs(sum(yl) / len(yl) - sum(yr) / len(yr))
        if d < best:
            best, bf = d, f
    return bf


if __name__ == "__main__":
    main()
