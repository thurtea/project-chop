"""Validate the Chopper .blend files and write Art/Renders/check_report.txt.

Run:  blender -b -P check_assets.py
"""
import os

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
FILES = [os.path.join(ART, "Blender", "chopper_character.blend"),
         os.path.join(ART, "Blender", "chopper_props.blend")]
REPORT = os.path.join(ART, "Renders", "check_report.txt")

CHAR = ["chopper_body", "chopper_head", "chopper_beard", "chopper_hair", "chopper_shirt",
        "chopper_vest", "chopper_belt", "chopper_chainmail", "chopper_boots"]
PROPS = ["chopper_axe", "chopper_shield"]
ALLOWED_MATS = {"chopper_character.blend": {"M_Chopper"}, "chopper_props.blend": {"M_Chopper_Gear"}}

BUDGETS = [
    ("body+head+shirt+vest+belt+chainmail+boots",
     ["chopper_body", "chopper_head", "chopper_shirt", "chopper_vest", "chopper_belt",
      "chopper_chainmail", "chopper_boots"], 4000, 8000),
    ("beard", ["chopper_beard"], 1000, 3000),
    ("hair", ["chopper_hair"], 1000, 3000),
    ("axe", ["chopper_axe"], 300, 1000),
    ("shield", ["chopper_shield"], 500, 1500),
    ("Worker + axe + shield", CHAR + PROPS, 8000, 15000),
]

lines = []
results = []   # (label, ok)


def out(s=""):
    print(s)
    lines.append(s)


def check(label, ok, detail=""):
    results.append((label, ok))
    out(f"  [{'PASS' if ok else 'FAIL'}] {label}{(' - ' + detail) if detail else ''}")


def tris(ob):
    me = ob.data
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def world_bounds(obs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for ob in obs:
        for v in ob.data.vertices:
            p = ob.matrix_world @ v.co
            lo = Vector(map(min, lo, p))
            hi = Vector(map(max, hi, p))
    return lo, hi


def image_usage():
    found = []
    for img in bpy.data.images:
        if img.name in ("Render Result", "Viewer Node"):
            continue
        found.append(f"image datablock '{img.name}'")
    for mat in bpy.data.materials:
        if mat.node_tree:
            for n in mat.node_tree.nodes:
                if n.type in ("TEX_IMAGE", "TEX_ENVIRONMENT"):
                    found.append(f"image node in material '{mat.name}'")
    for ob in bpy.data.objects:
        if ob.type == "EMPTY" and ob.empty_display_type == "IMAGE":
            found.append(f"reference image empty '{ob.name}'")
        if ob.type == "GPENCIL" or ob.type == "GREASEPENCIL":
            found.append(f"grease pencil '{ob.name}'")
    for cam in bpy.data.cameras:
        if len(cam.background_images):
            found.append(f"camera background image on '{cam.name}'")
    return found


def winding_report(ob):
    """Per object: inconsistent-winding edges, inside-out islands, inward-facing faces.

    * inconsistent edge: a manifold edge whose two faces walk it in the same direction.
    * island signed volume: summed tetrahedra about the island centroid (same measure the
      rigger's MESH FIX uses); negative = the island is inside out.
    * inward face (ray parity): a ray cast from the face centre along its normal is counted
      against the face's own island; an odd number of hits means the normal points into
      the island's solid (toward its interior / into the body).
    """
    me = ob.data
    me.calc_loop_triangles()
    verts = [v.co.copy() for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    # islands via shared edges
    edge_faces = {}
    for pi, p in enumerate(me.polygons):
        for ek in p.edge_keys:
            edge_faces.setdefault(ek, []).append(pi)
    parent = list(range(len(polys)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for fl in edge_faces.values():
        for f in fl[1:]:
            ra, rb = find(fl[0]), find(f)
            if ra != rb:
                parent[ra] = rb
    islands = {}
    for pi in range(len(polys)):
        islands.setdefault(find(pi), []).append(pi)

    # inconsistent winding edges
    bad_edges = 0
    for (a, b), fl in edge_faces.items():
        if len(fl) != 2:
            continue
        dirs = []
        for f in fl:
            vs = polys[f]
            n = len(vs)
            for i in range(n):
                if {vs[i], vs[(i + 1) % n]} == {a, b}:
                    dirs.append(vs[i] == a)
                    break
        if len(dirs) == 2 and dirs[0] == dirs[1]:
            bad_edges += 1

    neg_islands, inward = 0, 0
    jitter = Vector((0.0123, -0.0071, 0.0049))
    for faces in islands.values():
        c = sum((me.polygons[f].center for f in faces), Vector()) / len(faces)
        vol = 0.0
        for f in faces:
            vs = [verts[i] - c for i in polys[f]]
            for i in range(1, len(vs) - 1):
                vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
        if vol < 0:
            neg_islands += 1
        # ray parity on the actual render triangles (non-planar quads would otherwise
        # start the ray behind a neighbouring triangle)
        fset = set(faces)
        tris_i = [t for t in me.loop_triangles if t.polygon_index in fset]
        bvh = BVHTree.FromPolygons(verts, [tuple(t.vertices) for t in tris_i], epsilon=0.0)
        bad_polys = set()
        for t in tris_i:
            d = (t.normal + jitter).normalized()
            o = t.center + d * 1e-5
            hits = 0
            for _ in range(64):
                loc, nrm, idx, dist = bvh.ray_cast(o, d)
                if loc is None:
                    break
                hits += 1
                o = loc + d * 1e-5
            if hits % 2 == 1:
                bad_polys.add(t.polygon_index)
        inward += len(bad_polys)
    return len(islands), bad_edges, neg_islands, inward


tri_by_name = {}
bounds = {}

out("Chopper asset check report")
out("=" * 72)
for path in FILES:
    base = os.path.basename(path)
    out("")
    out(f"FILE {base}")
    out("-" * 72)
    if not os.path.exists(path):
        check(f"{base} exists", False)
        continue
    bpy.ops.wm.open_mainfile(filepath=path)
    expected = CHAR if base.startswith("chopper_character") else PROPS
    meshes = [ob for ob in bpy.data.objects if ob.type == "MESH"]
    names = sorted(ob.name for ob in meshes)
    check("expected objects present, exact names",
          sorted(expected) == names, f"found {names}")
    all_mats = set()
    for name in expected:
        ob = bpy.data.objects.get(name)
        if ob is None:
            continue
        t = tris(ob)
        tri_by_name[name] = t
        lo, hi = world_bounds([ob])
        bounds[name] = (lo, hi)
        dims = hi - lo
        mats = [m.name for m in ob.data.materials if m]
        all_mats.update(mats)
        loc, rot, scl = ob.location, ob.rotation_euler, ob.scale
        out(f"{name}")
        out(f"    triangles  : {t}")
        out(f"    dimensions : {dims.x:.3f} x {dims.y:.3f} x {dims.z:.3f} m  (bounds z {lo.z:.3f}..{hi.z:.3f})")
        out(f"    location   : ({loc.x:.4f}, {loc.y:.4f}, {loc.z:.4f})")
        out(f"    rotation   : ({rot.x:.4f}, {rot.y:.4f}, {rot.z:.4f})")
        out(f"    scale      : ({scl.x:.4f}, {scl.y:.4f}, {scl.z:.4f})")
        out(f"    materials  : {len(mats)} {mats}")
        out(f"    shading    : {'flat' if not any(p.use_smooth for p in ob.data.polygons) else 'SMOOTH faces present'}")
        ok_t = loc.length < 1e-6 and Vector(rot).length < 1e-6 and (scl - Vector((1, 1, 1))).length < 1e-6
        check(f"{name} transforms applied (loc 0, rot 0, scale 1)", ok_t)
        check(f"{name} materials within {sorted(ALLOWED_MATS[base])}",
              len(mats) >= 1 and set(mats) <= ALLOWED_MATS[base], str(mats))
        check(f"{name} flat shaded", not any(p.use_smooth for p in ob.data.polygons))
        n_isl, bad_e, neg_i, inward = winding_report(ob)
        out(f"    winding    : {n_isl} islands, {bad_e} inconsistent edges, {neg_i} inside-out islands, "
            f"{inward} of {len(ob.data.polygons)} faces facing inward (ray parity)")
        check(f"{name} winding outward (0 inconsistent edges, 0 inside-out islands, 0 inward faces)",
              bad_e == 0 and neg_i == 0 and inward == 0,
              f"{bad_e} / {neg_i} / {inward}")
    if base.startswith("chopper_character") and "chopper_body" in bpy.data.objects:
        # detector self-test: a scratch copy of the body with the trouser legs turned inside
        # out (the W1-03 defect) must be caught; the copy is discarded, nothing is saved
        me = bpy.data.objects["chopper_body"].data.copy()
        tmp = bpy.data.objects.new("_winding_selftest", me)
        bm = bmesh.new()
        bm.from_mesh(me)
        legs = [f for f in bm.faces if f.calc_center_median().z < 0.6]
        bmesh.ops.reverse_faces(bm, faces=legs)
        bm.to_mesh(me)
        bm.free()
        r = winding_report(tmp)
        bpy.data.objects.remove(tmp)
        bpy.data.meshes.remove(me)
        check(f"winding detector self-test: legs reversed on a scratch copy -> {r[1]} inconsistent edges, "
              f"{r[2]} inside-out islands, {r[3]} inward faces", r[2] > 0 and r[3] > 0)
    imgs = image_usage()
    check(f"{base}: no image textures / reference images", not imgs, "; ".join(imgs) if imgs else "none found")
    check(f"{base}: materials used {sorted(all_mats)}", all_mats <= ALLOWED_MATS[base])

out("")
out("BUDGETS (triangles after triangulation)")
out("-" * 72)
for label, objs, lo, hi in BUDGETS:
    total = sum(tri_by_name.get(n, 0) for n in objs)
    check(f"{label}: {total} in [{lo}, {hi}]", lo <= total <= hi and all(n in tri_by_name for n in objs))

out("")
out("CHARACTER DIMENSIONS")
out("-" * 72)
char_b = [bounds[n] for n in CHAR if n in bounds]
if char_b:
    zmin = min(b[0].z for b in char_b)
    zmax = max(b[1].z for b in char_b)
    xmin = min(b[0].x for b in char_b)
    xmax = max(b[1].x for b in char_b)
    out(f"  overall bounds: x {xmin:.3f}..{xmax:.3f}, z {zmin:.3f}..{zmax:.3f}")
    check(f"character height {zmax - zmin:.4f} m in [1.78, 1.82]", 1.78 <= zmax - zmin <= 1.82)
    hair_top = bounds["chopper_hair"][1].z
    check(f"top of hair {hair_top:.4f} m is the highest point", abs(hair_top - zmax) < 1e-6)
    bz = bounds["chopper_boots"][0].z
    check(f"lowest Z of boots {bz:.4f} within 0.005 of 0", abs(bz) <= 0.005)
    check(f"boots are the lowest geometry (min z {zmin:.4f})", abs(zmin - bz) < 1e-6)
if "chopper_axe" in bounds:
    lo, hi = bounds["chopper_axe"]
    check(f"axe length {hi.z - lo.z:.3f} m about 0.85 (0.80-0.90)", 0.80 <= hi.z - lo.z <= 0.90)
    check("axe origin on the haft (grip point inside haft range)", lo.z < 0 < hi.z)
if "chopper_shield" in bounds:
    lo, hi = bounds["chopper_shield"]
    d = hi - lo
    check(f"shield height {d.z:.3f} m about 0.95 (0.90-1.00)", 0.90 <= d.z <= 1.00)
    check(f"shield width {d.x:.3f} m about 0.65 (0.62-0.70, incl. rim)", 0.62 <= d.x <= 0.70)

out("")
n_fail = sum(1 for _, ok in results if not ok)
out("=" * 72)
out(f"SUMMARY: {len(results) - n_fail} PASS, {n_fail} FAIL -> {'ALL PASS' if n_fail == 0 else 'FAILURES PRESENT'}")
os.makedirs(os.path.dirname(REPORT), exist_ok=True)
with open(REPORT, "w", encoding="utf-8") as fh:
    fh.write("\n".join(lines) + "\n")
print("report written to", REPORT)
