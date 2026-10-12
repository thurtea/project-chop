"""W2-05 independent probe. Run: blender -b --factory-startup -P probe_fbx.py
Imports Art/Export/chopper.fbx (+ axe, shield) fresh and reports measurements."""
import bpy, bmesh, os, math, re
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))
EXP = os.path.join(ART, "Export")
out = []
def log(s=""):
    print(s); out.append(str(s))

def tris(ob):
    me = ob.data; me.calc_loop_triangles(); return len(me.loop_triangles)

# raw byte scan first
for fn in ("chopper.fbx", "chopper_axe.fbx", "chopper_shield.fbx"):
    data = open(os.path.join(EXP, fn), "rb").read()
    hits = {k: data.count(k) for k in (b"Video", b"Texture", b"concept", b".jpg", b".png", b"Image", b"RelativeFilename", b"Content")}
    log("%s bytes=%d raw-string hits %s" % (fn, len(data), hits))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(EXP, "chopper.fbx"), use_anim=True)
log("images after import: %s" % [i.name for i in bpy.data.images])
log("textures after import: %s" % [t.name for t in bpy.data.textures])
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
log("armature %s bones=%d" % (arm.name, len(arm.data.bones)))
log("bone names: %s" % sorted(b.name for b in arm.data.bones))
leaf = [b.name for b in arm.data.bones if b.name.endswith("_end") or b.name.lower().endswith("end")]
log("leaf/_end bones: %s" % leaf)

# actions / takes
log("actions:")
for a in bpy.data.actions:
    log("  %-40s frames %.0f-%.0f" % (a.name, a.frame_range[0], a.frame_range[1]))

# rest pose: evaluate at frame with no action
if arm.animation_data:
    arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.matrix_basis.identity()
bpy.context.view_layer.update()

def world_bbox(obs):
    dg = bpy.context.evaluated_depsgraph_get()
    mn = Vector((1e9,)*3); mx = Vector((-1e9,)*3)
    for o in obs:
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        for v in me.vertices:
            w = o.matrix_world @ v.co
            for i in range(3):
                mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
        ev.to_mesh_clear()
    return mn, mx

log("")
log("per-mesh tris:")
T = {}
for o in sorted(meshes, key=lambda o: o.name):
    me = o.data
    uvl = len(me.uv_layers)
    us = [l.uv for l in me.uv_layers[0].data] if uvl else []
    umin = min((u.x for u in us), default=-1); umax = max((u.x for u in us), default=-1)
    vmin = min((u.y for u in us), default=-1); vmax = max((u.y for u in us), default=-1)
    T[o.name] = tris(o)
    log("  %-22s tris=%5d uvlayers=%d u %.3f..%.3f v %.3f..%.3f inside=%s mats=%s" % (
        o.name, T[o.name], uvl, umin, umax, vmin, vmax,
        uvl == 1 and 0 <= umin and umax <= 1 and 0 <= vmin and vmax <= 1,
        [m.name for m in me.materials if m]))

def nm(k):
    for n in T:
        if n.startswith(k): return T[n]
    return 0

common = ["chopper_body", "chopper_head", "chopper_beard", "chopper_shirt", "chopper_belt", "chopper_chainmail", "chopper_boots"]

# props
for fn in ("chopper_axe.fbx", "chopper_shield.fbx"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(EXP, fn))
    for o in set(bpy.data.objects) - before:
        if o.type == "MESH":
            T[o.name] = tris(o)
            me = o.data
            us = [l.uv for l in me.uv_layers[0].data] if me.uv_layers else []
            log("  %-22s tris=%5d uvlayers=%d u %.3f..%.3f v %.3f..%.3f (from %s)" % (
                o.name, T[o.name], len(me.uv_layers), min(u.x for u in us), max(u.x for u in us),
                min(u.y for u in us), max(u.y for u in us), fn))
log("images after props import: %s" % [i.name for i in bpy.data.images])
axe = nm("chopper_axe"); sh = nm("chopper_shield")
warrior = sum(nm(k) for k in common) + nm("chopper_vest") + nm("chopper_helmet")
elite = sum(nm(k) for k in common) + nm("chopper_helmet") + nm("chopper_plate") + nm("chopper_pauldrons")
worker = sum(nm(k) for k in common) + nm("chopper_vest") + nm("chopper_hair")
log("")
log("helmet %d (1000-3000) -> %s" % (nm("chopper_helmet"), 1000 <= nm("chopper_helmet") <= 3000))
log("axe %d (<=1000) -> %s" % (axe, axe <= 1000))
log("Warrior body (helmet, vest, no hair) %d; +axe+shield %d (8000-15000) -> %s" % (warrior, warrior + axe + sh, 8000 <= warrior + axe + sh <= 15000))
log("Elite +axe+shield %d" % (elite + axe + sh))
log("Worker (hair, vest, no weapons) %d" % worker)

# height (character only, rest pose, Worker visible set incl hair)
char = [o for o in meshes if o.name.startswith(("chopper_body", "chopper_head", "chopper_hair", "chopper_boots"))]
mn, mx = world_bbox(char)
log("")
log("height (body/head/hair/boots, rest) %.4f m, minZ %.4f maxZ %.4f" % (mx.z - mn.z, mn.z, mx.z))
hm = [o for o in meshes if o.name.startswith("chopper_helmet")]
if hm:
    a, b = world_bbox(hm); log("helmet top Z %.4f" % b.z)

# rest left upper arm angle
def bone(name):
    for b in arm.data.bones:
        if b.name.lower().replace("_", "").replace(".", "") == name: return b
    return None
for cand in ("upperarml", "leftupperarm", "upperarmleft", "larm", "upperarm_l"):
    ub = bone(cand)
    if ub: break
if ub is None:
    ub = [b for b in arm.data.bones if "upper" in b.name.lower() and ("l" == b.name[-1].lower() or "left" in b.name.lower())][0]
pb = arm.pose.bones[ub.name]
h = arm.matrix_world @ pb.head; t = arm.matrix_world @ pb.tail
d = (t - h).normalized()
ang = math.degrees(math.asin(-d.z))
log("rest %s head %s tail %s: %.1f deg below horizontal" % (ub.name, tuple(round(c, 3) for c in h), tuple(round(c, 3) for c in t), ang))
log("armature object rotation %s scale %s" % (tuple(round(math.degrees(r), 2) for r in arm.rotation_euler), tuple(arm.scale)))

# Shield_Back penetration: place shield at Shield_Back socket in rest pose
sb = [b for b in arm.data.bones if "shield_back" in b.name.lower()]
sl = [b for b in arm.data.bones if b.name.lower() in ("shield_l",)]
log("")
log("sockets: %s" % [b.name for b in arm.data.bones if "shield" in b.name.lower() or "weapon" in b.name.lower() or "helmet" in b.name.lower()])
shield_ob = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("chopper_shield")][0]
body_names = ("chopper_body", "chopper_shirt", "chopper_vest", "chopper_chainmail", "chopper_belt", "chopper_hair", "chopper_head", "chopper_beard", "chopper_plate", "chopper_pauldrons", "chopper_helmet")
dg = bpy.context.evaluated_depsgraph_get()
def bvh_of(o):
    ev = o.evaluated_get(dg); me = ev.to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(o.matrix_world)
    t = BVHTree.FromBMesh(bm); bm.free(); ev.to_mesh_clear(); return t, ev
def inside(tree, p):
    # parity along +X and -X ray using ray_cast repeated
    cnt = 0; o = p.copy(); dirv = Vector((0.0123, 1.0, 0.0071)).normalized()
    for _ in range(64):
        hit = tree.ray_cast(o, dirv)
        if hit[0] is None: break
        cnt += 1; o = hit[0] + dirv * 1e-5
    return cnt % 2 == 1
if sb:
    pbs = arm.pose.bones[sb[0].name]
    M = arm.matrix_world @ pbs.matrix
    log("Shield_Back bone world head %s" % (tuple(round(c, 3) for c in (arm.matrix_world @ pbs.head)),))
    # Unity/AttachProp: prop child of socket at identity, FBX root -90X reset. Prop mesh in Blender after import
    # has its own object matrix (FBX -90X conversion). Use prop local mesh coords (Blender Z-up) placed by socket bone matrix
    # with bone Y-up -> treat as Blender: parent to bone with identity local.
    me = shield_ob.data
    loc_vs = [shield_ob.matrix_basis @ v.co for v in me.vertices]
    # try both conventions and report
    for label, conv in (("bone matrix * prop object-space", M), ):
        ws = [conv @ v for v in loc_vs]
        ys = [w.y for w in ws]; zs = [w.z for w in ws]
        log("%s: shield verts world Y %.3f..%.3f mean %.3f, Z %.3f..%.3f" % (label, min(ys), max(ys), sum(ys)/len(ys), min(zs), max(zs)))
        total = 0
        per = {}
        for o in meshes:
            if not o.name.startswith(body_names): continue
            tree, _ = bvh_of(o)
            n = sum(1 for w in ws if inside(tree, w))
            per[o.name] = n; total += n
        log("  penetrating shield verts per body mesh: %s total %d of %d" % (per, total, len(ws)))
        # min distance from body to shield
        dmin = 1e9
        for o in meshes:
            if not o.name.startswith(("chopper_body", "chopper_shirt", "chopper_vest", "chopper_chainmail", "chopper_plate", "chopper_pauldrons")): continue
            tree, _ = bvh_of(o)
            for w in ws:
                r = tree.find_nearest(w)
                if r[0] is not None: dmin = min(dmin, r[3])
        log("  min shield-to-torso distance %.4f m" % dmin)

open(os.path.join(HERE, "probe_fbx_report.txt"), "w").write("\n".join(out) + "\n")
