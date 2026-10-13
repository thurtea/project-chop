"""Validate the Chopper .blend files and write Art/Renders/check_report.txt.

Run:  blender -b -P check_assets.py
"""
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from atlas_paint import SUPER  # noqa: E402

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
FILES = [os.path.join(ART, "Blender", "chopper_character.blend"),
         os.path.join(ART, "Blender", "chopper_props.blend")]
REPORT = os.path.join(ART, "Renders", "check_report.txt")

CHAR = ["chopper_body", "chopper_head", "chopper_beard", "chopper_hair", "chopper_shirt",
        "chopper_vest", "chopper_belt", "chopper_bracers", "chopper_chainmail", "chopper_boots",
        "chopper_helmet", "chopper_plate", "chopper_pauldrons"]
ELITE_ONLY = ["chopper_plate", "chopper_pauldrons"]
PROPS = ["chopper_axe", "chopper_axe_bear", "chopper_shield"]
LOADOUT = ["chopper_axe", "chopper_shield"]          # Worker / Warrior / Elite lineups as before
# Base look (W6-02, concept 1.5 without armour): no helmet, chainmail, bracers, plate, pauldrons
ARMOUR = ["chopper_helmet", "chopper_chainmail", "chopper_bracers", "chopper_plate", "chopper_pauldrons"]
BASE = [n for n in CHAR if n not in ARMOUR]
ALLOWED_MATS = {"chopper_character.blend": {"M_Chopper", "M_Chopper_Gear"},
                 "chopper_props.blend": {"M_Chopper_Gear"}}

BUDGETS = [
    ("body+head+shirt+vest+belt+bracers+chainmail+boots",
     ["chopper_body", "chopper_head", "chopper_shirt", "chopper_vest", "chopper_belt", "chopper_bracers",
      "chopper_chainmail", "chopper_boots"], 4000, 8000),
    ("beard", ["chopper_beard"], 1000, 3000),
    ("hair", ["chopper_hair"], 1000, 3000),
    ("axe", ["chopper_axe"], 300, 1000),
    ("bear-head axe", ["chopper_axe_bear"], 300, 1000),
    ("bracers", ["chopper_bracers"], 50, 600),
    ("shield", ["chopper_shield"], 500, 1500),
    ("helmet", ["chopper_helmet"], 1000, 3000),
    ("Worker + axe + shield",
     [n for n in CHAR if n not in ("chopper_helmet", *ELITE_ONLY)] + LOADOUT, 8000, 15000),
    ("Warrior (helmet, no hair) + axe + shield",
     [n for n in CHAR if n not in ("chopper_hair", *ELITE_ONLY)] + LOADOUT, 8000, 15000),
    ("Elite (plate and pauldrons, no leather vest, no hair) + axe + shield",
     [n for n in CHAR if n not in ("chopper_hair", "chopper_vest")] + LOADOUT, 8000, 15000),
    ("Base look (no armour) + bear-head axe: %s" % "+".join(n[8:] for n in BASE),
     BASE + ["chopper_axe_bear"], 6000, 15000),
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


UV_TRIS = {}      # object -> [(uv triangle 3x2, frozenset of its vertex indices)]
DENSITY = {}


def uv_stats(ob, uv):
    """Collect UV triangles for the atlas-wide overlap test; report islands and texel density.

    An island = faces joined by an edge whose two UV endpoints are the same on both faces."""
    me = ob.data
    me.calc_loop_triangles()
    world = uva = 0.0
    tris = []
    for t in me.loop_triangles:
        vs = [me.vertices[i].co for i in t.vertices]
        world += (vs[1] - vs[0]).cross(vs[2] - vs[0]).length * 0.5
        u = [tuple(uv.data[li].uv) for li in t.loops]
        uva += abs((u[1][0] - u[0][0]) * (u[2][1] - u[0][1]) - (u[2][0] - u[0][0]) * (u[1][1] - u[0][1])) * 0.5
        tris.append((u, frozenset(t.vertices)))
    UV_TRIS[ob.name] = tris
    parent = list(range(len(me.polygons)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    seen = {}
    for p in me.polygons:
        lis = list(p.loop_indices)
        for k in range(len(lis)):
            l0, l1 = lis[k], lis[(k + 1) % len(lis)]
            v0, v1 = me.loops[l0].vertex_index, me.loops[l1].vertex_index
            key = (min(v0, v1), max(v0, v1))
            uvk = {v0: tuple(round(c, 6) for c in uv.data[l0].uv), v1: tuple(round(c, 6) for c in uv.data[l1].uv)}
            if key in seen:
                q, other = seen[key]
                if other == uvk:
                    ra, rb = find(p.index), find(q)
                    if ra != rb:
                        parent[ra] = rb
            else:
                seen[key] = (p.index, uvk)
    n_isl = len({find(i) for i in range(len(me.polygons))})
    dens = 2048 * (uva / world) ** 0.5 if world > 0 else 0.0
    DENSITY[ob.name] = dens / SUPER        # painted pixels per metre
    out(f"    uv layout  : {n_isl} islands for {len(me.polygons)} faces "
        f"({len(me.polygons) / max(1, n_isl):.1f} faces per island), texel density {dens:.0f} px/m")
    check(f"{ob.name} UVs are connected islands, not one cell per face ({len(me.polygons) / max(1, n_isl):.1f} faces per island)",
          len(me.polygons) / max(1, n_isl) >= 2.0)


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
        uv = ob.data.uv_layers.get("UVMap") if ob.data.uv_layers else None
        if uv is None:
            check(f"{name} has one UV layer inside 0-1", False, "no UVMap")
        else:
            us, vs = [], []
            for d in uv.data:
                us.append(d.uv.x)
                vs.append(d.uv.y)
            inside = min(us) >= -1e-4 and min(vs) >= -1e-4 and max(us) <= 1.001 and max(vs) <= 1.001
            check(f"{name} has one UV layer inside 0-1",
                  len(ob.data.uv_layers) == 1 and inside,
                  "layers=%d u %.3f..%.3f v %.3f..%.3f" % (len(ob.data.uv_layers), min(us), max(us), min(vs), max(vs)))
            uv_stats(ob, uv)
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
    check(f"character height {zmax - zmin:.4f} m in [1.95, 2.15] (horns above the hair)", 1.95 <= zmax - zmin <= 2.15)
    hair_top = bounds["chopper_hair"][1].z
    check(f"top of hair {hair_top:.4f} m stays at 1.80", 1.79 <= hair_top <= 1.81)
    helm_top = bounds["chopper_helmet"][1].z
    check(f"helmet horns {helm_top:.4f} m are the highest point, above the hair",
          abs(helm_top - zmax) < 1e-4 and helm_top > hair_top + 0.12)
    bz = bounds["chopper_boots"][0].z
    check(f"lowest Z of boots {bz:.4f} within 0.005 of 0", abs(bz) <= 0.005)
    check(f"boots are the lowest geometry (min z {zmin:.4f})", abs(zmin - bz) < 1e-6)
if "chopper_axe" in bounds:
    lo, hi = bounds["chopper_axe"]
    check(f"axe length {hi.z - lo.z:.3f} m about 0.85 (0.80-0.90)", 0.80 <= hi.z - lo.z <= 0.90)
    check("axe origin on the haft (grip point inside haft range)", lo.z < 0 < hi.z)
if "chopper_axe_bear" in bounds:
    lo, hi = bounds["chopper_axe_bear"]
    dims = hi - lo
    check(f"bear axe length {dims.z:.3f} m in 0.85-0.95", 0.85 <= dims.z <= 0.95)
    # grip convention = chopper_axe: origin at the grip on the haft, haft along +Z (head up),
    # blade on +X (the side of chopper_axe's +X blade), poll on -X, thickness along Y
    check("bear axe origin on the haft (grip point inside haft range)", lo.z < 0 < hi.z)
    check(f"bear axe: haft along Z (longest), thickness along Y (shortest): dims {dims.x:.3f} x {dims.y:.3f} x {dims.z:.3f}",
          dims.z > dims.x > dims.y)
    check(f"bear axe head at +Z (top {hi.z:.3f} > {-lo.z:.3f} below the grip)", hi.z > 2 * -lo.z)
    check(f"bear axe single blade on +X (x {lo.x:.3f}..{hi.x:.3f}: blade reach > 0.15, poll < 0.10)",
          hi.x > 0.15 and -0.10 < lo.x < 0 and hi.x > 2 * -lo.x)
    if "chopper_axe" in bounds:
        check("chopper_axe has a +X blade (the side the bear axe's blade matches)", bounds["chopper_axe"][1].x > 0.15)

    def grip_section(name):
        bpy.ops.wm.open_mainfile(filepath=FILES[1])
        # cross-section of the haft at the grip point (z = 0): rays from four sides toward the Z axis
        me = bpy.data.objects[name].data
        tree = BVHTree.FromPolygons([v.co for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
        hit = {}
        for k, d in (("x+", Vector((1, 0, 0))), ("x-", Vector((-1, 0, 0))), ("y+", Vector((0, 1, 0))),
                     ("y-", Vector((0, -1, 0)))):
            loc = tree.ray_cast(d * 0.2, -d)[0]
            hit[k] = loc.dot(d) * (1 if k[1] == "+" else -1) if loc is not None else float("nan")
        return ((hit["x+"] + hit["x-"]) / 2, (hit["y+"] + hit["y-"]) / 2,
                (hit["x+"] - hit["x-"]) / 2, (hit["y+"] - hit["y-"]) / 2)
    ga, gb = grip_section("chopper_axe"), grip_section("chopper_axe_bear")
    check("bear axe haft section at the grip point (z 0) centred on the Z axis like chopper_axe: centre (%.4f, %.4f) "
          "vs (%.4f, %.4f), half size %.4f x %.4f vs %.4f x %.4f" % (gb[0], gb[1], ga[0], ga[1], gb[2], gb[3], ga[2], ga[3]),
          abs(gb[0]) < 1e-3 and abs(gb[1]) < 1e-3 and abs(gb[2] - ga[2]) < 0.005 and abs(gb[3] - ga[3]) < 0.005)
if "chopper_shield" in bounds:
    lo, hi = bounds["chopper_shield"]
    d = hi - lo
    check(f"shield height {d.z:.3f} m about 0.95 (0.90-1.00)", 0.90 <= d.z <= 1.00)
    check(f"shield width {d.x:.3f} m about 0.65 (0.62-0.70, incl. rim)", 0.62 <= d.x <= 0.70)

out("")
out("UV ATLAS (all objects of both files share one 2048 atlas)")
out("-" * 72)


def atlas_overlaps(size=2048):
    """Rasterise every UV triangle at atlas resolution; count pixel centres strictly inside
    triangles of two objects, or of one object where the two triangles share no edge."""
    import numpy as np
    owner = np.full(size * size, -1, dtype=np.int64)
    tri_sets = {}
    cover = np.zeros(size * size, dtype=bool)
    cross = inner = 0
    names = sorted(UV_TRIS)
    for oi, name in enumerate(names):
        for ti, (u, vset) in enumerate(UV_TRIS[name]):
            key = oi * 10_000_000 + ti
            tri_sets[key] = vset
            a = np.array(u) * size
            x0, y0 = np.maximum(np.floor(a.min(axis=0)).astype(int), 0)
            x1, y1 = np.minimum(np.ceil(a.max(axis=0)).astype(int), size - 1)
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, ay), (bx, by), (cx, cy) = a
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-12:
                continue
            w0 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
            w1 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
            w2 = 1 - w0 - w1
            ins = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
            st = (w0 > 2e-3) & (w1 > 2e-3) & (w2 > 2e-3)
            ys, xs = np.nonzero(ins)
            cover[(y0 + ys) * size + x0 + xs] = True
            ys, xs = np.nonzero(st)
            px = (y0 + ys) * size + x0 + xs
            prev = owner[px]
            for pv in np.unique(prev[prev >= 0]):
                if pv // 10_000_000 != oi:
                    cross += int((prev == pv).sum())
                elif len(tri_sets[int(pv)] & vset) < 2:
                    inner += int((prev == pv).sum())
            owner[px] = key
    return cross, inner, cover


cross, inner, cover = atlas_overlaps()
check(f"atlas: no UV overlap between objects ({cross} px)", cross == 0)
check(f"atlas: no UV overlap inside an object ({inner} px)", inner == 0)
for name in sorted(DENSITY, key=lambda n: -DENSITY[n]):
    out(f"  density {name:18s} {DENSITY[name]:6.1f} px/m")
# pixel art (W6-03): one atlas texel = one painted pixel, the same size on every part
for name in sorted(DENSITY, key=lambda n: -DENSITY[n]):
    out(f"  texel {name:18s} {100.0 / DENSITY[name]:5.2f} cm per painted pixel")
base_parts = [n for n in BASE if n in DENSITY and n != "chopper_head"]
cm = {n: 100.0 / DENSITY[n] for n in base_parts}
check("pixel size uniform on the base look (excluding the face): %.2f..%.2f cm, required 1.5-2.0 cm and "
      "within 10 %%" % (min(cm.values()), max(cm.values())),
      1.5 <= min(cm.values()) and max(cm.values()) <= 2.0 and max(cm.values()) <= 1.10 * min(cm.values()))
check("face pixels as fine or finer than the body (head %.2f cm, body %.2f cm)"
      % (100.0 / DENSITY["chopper_head"], 100.0 / DENSITY["chopper_body"]),
      DENSITY["chopper_head"] >= DENSITY["chopper_body"])

atlas = os.path.join(ART, "Textures", "chopper_atlas.png")
if os.path.isfile(atlas):
    import numpy as np
    img = bpy.data.images.load(atlas)
    img.colorspace_settings.name = "Non-Color"            # raw 8-bit values
    px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)[:, :3]
    sz = img.size[0]
    bpy.data.images.remove(img)
    m = cover.reshape(2048, 2048)
    ring = m.copy()
    for _ in range(8):                                     # 8 px around every island
        g = ring.copy()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)):
            g |= np.roll(np.roll(ring, dy, 0), dx, 1)
        ring = g
    ring &= ~m
    gutter = np.array([0.0134, 0.0100, 0.0086])
    gutter = np.where(gutter <= 0.0031308, gutter * 12.92, 1.055 * gutter ** (1 / 2.4) - 0.055)
    is_gutter = (np.abs(px - gutter).max(axis=1) < 1.5 / 255).reshape(sz, sz)
    bad = int((is_gutter & ring).sum())
    check(f"atlas: 8 px edge padding around every island carries the island colour, not the gutter "
          f"({bad} of {int(ring.sum())} padding px are gutter colour)", sz == 2048 and bad <= 0.001 * ring.sum())
check("atlas PNG exists at Art/Textures/chopper_atlas.png", os.path.isfile(atlas),
      "%d bytes" % (os.path.getsize(atlas) if os.path.isfile(atlas) else 0))

out("")
n_fail = sum(1 for _, ok in results if not ok)
out("=" * 72)
out(f"SUMMARY: {len(results) - n_fail} PASS, {n_fail} FAIL -> {'ALL PASS' if n_fail == 0 else 'FAILURES PRESENT'}")
os.makedirs(os.path.dirname(REPORT), exist_ok=True)
with open(REPORT, "w", encoding="utf-8") as fh:
    fh.write("\n".join(lines) + "\n")
print("report written to", REPORT)
