"""Voxel pine on a dirt pad, from concept 4. Explicit cubes, no reference image in the file.

Run:  blender -b -P build_pine.py
Out:  Art/Blender/prop_pine_voxel.blend, Art/Export/prop_pine_voxel.fbx,
      Art/Renders/pine_check_report.txt, Art/Renders/pine_front.png

1 unit = 1 m. The pad sits on Z = 0. Cube size is 0.25 m so the tree reads next to
the 1.8 m character (about 3.5 m tall). Shared faces between cubes are omitted.
"""
import os

import bpy
from mathutils import Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
BLEND = os.path.join(ART, "Blender", "prop_pine_voxel.blend")
EXPORT = os.path.join(ART, "Export", "prop_pine_voxel.fbx")
REPORT = os.path.join(ART, "Renders", "pine_check_report.txt")
RENDER = os.path.join(ART, "Renders", "pine_front.png")

S = 0.25
LEAF = (0.18, 0.38, 0.14)
LEAF_DARK = (0.12, 0.28, 0.10)
TRUNK = (0.36, 0.22, 0.11)
DIRT = (0.42, 0.26, 0.13)
DIRT_DARK = (0.30, 0.18, 0.09)

# outward faces, CCW when looking at the face from outside
FACE = {
    (1, 0, 0): ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)),
    (-1, 0, 0): ((0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)),
    (0, 1, 0): ((1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)),
    (0, -1, 0): ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),
    (0, 0, 1): ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)),
    (0, 0, -1): ((0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)),
}


def cells():
    """Integer cube coordinates. x, y across the pad, z up. (0,0) is the trunk.

    Four foliage discs with a bare trunk cell between them, widest at the bottom,
    so the tree reads as stacked layers instead of one cone.
    """
    out = {}
    for ix in range(-4, 5):
        for iy in range(-4, 5):
            if ix * ix + iy * iy > 18:
                continue
            if abs(ix) >= 3 and abs(iy) >= 3:
                continue
            out[(ix, iy, 0)] = DIRT_DARK if (ix + iy) % 2 else DIRT
    for iz in range(1, 17):
        out[(0, 0, iz)] = TRUNK
    # (bottom z, top z, radius). A trunk-only slice sits between each pair.
    tiers = ((6, 7, 4), (9, 10, 3), (12, 13, 2), (15, 16, 1))
    for z0, z1, r in tiers:
        for iz in range(z0, z1 + 1):
            for ix in range(-r, r + 1):
                for iy in range(-r, r + 1):
                    if ix * ix + iy * iy > r * r + r // 2:
                        continue
                    if max(abs(ix), abs(iy)) == r and (ix * 3 + iy * 5 + iz) % 3 == 0:
                        continue
                    if iz == z0:
                        out[(ix, iy, iz)] = LEAF_DARK
                    else:
                        out[(ix, iy, iz)] = LEAF_DARK if (ix + iy + iz) % 3 == 0 else LEAF
    return out


def foliage_tiers(occ):
    """How many separated foliage bands sit on the trunk (a gap of at least one cell splits a band)."""
    zs = sorted({iz for (ix, iy, iz) in occ if iz > 0 and (ix, iy) != (0, 0)})
    if not zs:
        return 0
    bands = 1
    for a, b in zip(zs, zs[1:]):
        if b > a + 1:
            bands += 1
    return bands


def build(occ):
    mesh = bpy.data.meshes.new("prop_pine_voxel")
    bm_verts = []
    faces = []
    colors = []
    index = {}

    def vert(ix, iy, iz, corner):
        key = (ix, iy, iz, corner)
        if key in index:
            return index[key]
        cx, cy, cz = corner
        co = ((ix + cx) * S, (iy + cy) * S, (iz + cz) * S)
        index[key] = len(bm_verts)
        bm_verts.append(co)
        return index[key]

    for (ix, iy, iz), color in occ.items():
        for (dx, dy, dz), corners in FACE.items():
            if (ix + dx, iy + dy, iz + dz) in occ:
                continue
            faces.append(tuple(vert(ix, iy, iz, c) for c in corners))
            colors.append(color)
    mesh.from_pydata(bm_verts, [], faces)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for poly, color in zip(mesh.polygons, colors):
        poly.use_smooth = False
        for li in poly.loop_indices:
            attr.data[li].color = (color[0], color[1], color[2], 1.0)
    mesh.color_attributes.active_color = attr
    obj = bpy.data.objects.new("prop_pine_voxel", mesh)
    bpy.context.scene.collection.objects.link(obj)
    mat = bpy.data.materials.new("M_Pine")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    mesh.materials.append(mat)
    return obj


def check(obj, n_cells, occ):
    lines = ["Voxel pine check", "=" * 60]
    ok = True

    def add(label, passed, detail=""):
        nonlocal ok
        ok = ok and passed
        lines.append("  [%s] %s%s" % ("PASS" if passed else "FAIL", label, (" - " + detail) if detail else ""))

    me = obj.data
    me.calc_loop_triangles()
    tris = len(me.loop_triangles)
    loc, rot, scl = obj.location, obj.rotation_euler, obj.scale
    xs, ys, zs = [], [], []
    for v in me.vertices:
        xs.append(v.co.x)
        ys.append(v.co.y)
        zs.append(v.co.z)
    add("name prop_pine_voxel", obj.name == "prop_pine_voxel")
    add("one mesh", len([o for o in bpy.data.objects if o.type == "MESH"]) == 1)
    add("transforms applied", loc.length < 1e-6 and Vector(rot).length < 1e-6 and (scl - Vector((1, 1, 1))).length < 1e-6)
    add("base on Z=0", abs(min(zs)) < 1e-6, "min z %.4f" % min(zs))
    height = max(zs) - min(zs)
    add("height %.2f m in [3.0, 4.5]" % height, 3.0 <= height <= 4.5)
    add("pad wider than 1.2 m", (max(xs) - min(xs)) > 1.2, "width %.2f" % (max(xs) - min(xs)))
    add("flat shaded", not any(p.use_smooth for p in me.polygons))
    add("material M_Pine only", [m.name for m in me.materials if m] == ["M_Pine"])
    imgs = [im.name for im in bpy.data.images if im.name not in ("Render Result", "Viewer Node")]
    add("no images in the file", not imgs, ", ".join(imgs))
    add("cubes %d, triangles %d" % (n_cells, tris), tris < 8000)
    tiers = foliage_tiers(occ)
    add("foliage in %d separated tiers" % tiers, tiers >= 4)
    lines.append("SUMMARY: %s" % ("ALL PASS" if ok else "FAILURES"))
    text = "\n".join(lines) + "\n"
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    if not ok:
        raise SystemExit(1)


def shoot(name, yaw_deg, ortho, pitch_deg=12):
    import math
    sc = bpy.context.scene
    cam = sc.camera
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    look = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    target = Vector((0, 0, 1.5))
    cam.location = target - look * 8
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(ART, "Renders", name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", sc.render.filepath)


def render(obj):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = 1024
    sc.render.resolution_y = 1024
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "VERTEX"
    sh.show_shadows = False
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    # Warm haze, the colour of the concept sky, so the shot is not a hard blue.
    data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    sc.render.image_settings.file_format = "PNG"
    data.clip_end = 100
    sc.world.color = (0.78, 0.70, 0.56)
    shoot("pine_front", 0, 5.2)
    shoot("pine_three_quarter", -40, 5.6)
    copies = []
    for x, y, s in ((-1.6, -2.4, 1.05), (2.6, -1.2, 0.72), (-2.8, 1.8, 0.58), (1.2, 3.6, 0.42)):
        copy = obj.copy()
        copy.data = obj.data
        copy.location = (x, y, 0)
        copy.scale = (s, s, s)
        sc.collection.objects.link(copy)
        copies.append(copy)
    shoot("pine_grove", -32, 11.0, pitch_deg=18)
    for copy in copies:
        bpy.data.objects.remove(copy, do_unlink=True)


def export_fbx():
    os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=EXPORT,
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        embed_textures=False,
        bake_anim=False,
        path_mode="STRIP",
    )
    print("exported", EXPORT, os.path.getsize(EXPORT), "bytes")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    occ = cells()
    obj = build(occ)
    check(obj, len(occ), occ)
    render(obj)
    bpy.context.preferences.filepaths.save_version = 0
    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    export_fbx()
    print("saved", BLEND)


if __name__ == "__main__":
    main()
