"""Meadow kit in the same 0.25 m cubes as the voxel pine.

Run:  blender -b -P build_meadow.py
Out:  Art/Blender/meadow_kit.blend
      Art/Export/prop_rock_voxel.fbx, prop_stump_voxel.fbx, prop_log_voxel.fbx
      Art/Renders/meadow_check_report.txt, meadow_kit.png

1 unit = 1 m. Every prop sits on Z = 0 with its origin at the world origin.
Vertex colour only, one shared material M_World. No images in the file.
"""
import math
import os

import bpy
from mathutils import Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
BLEND = os.path.join(ART, "Blender", "meadow_kit.blend")
EXPORT = os.path.join(ART, "Export")
REPORT = os.path.join(ART, "Renders", "meadow_check_report.txt")

S = 0.25
STONE = (0.30, 0.29, 0.27)
STONE_DARK = (0.21, 0.20, 0.19)
MOSS = (0.20, 0.28, 0.11)
MOSS_DARK = (0.15, 0.22, 0.08)
BARK = (0.34, 0.20, 0.10)
BARK_DARK = (0.24, 0.14, 0.07)
WOOD = (0.55, 0.36, 0.16)

FACE = {
    (1, 0, 0): ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)),
    (-1, 0, 0): ((0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)),
    (0, 1, 0): ((1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)),
    (0, -1, 0): ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),
    (0, 0, 1): ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)),
    (0, 0, -1): ((0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)),
}


def disc(r, bites=()):
    cells = []
    for ix in range(-r, r + 1):
        for iy in range(-r, r + 1):
            if ix * ix + iy * iy > r * r:
                continue
            if (ix, iy) in bites:
                continue
            cells.append((ix, iy))
    return cells


def rock_cells():
    out = {}
    layers = (
        (0, 3, ((3, 1), (-3, -1), (2, 3))),
        (1, 3, ((-3, 2), (3, -2))),
        (2, 2, ((2, 2),)),
        (3, 1, ()),
    )
    for iz, r, bites in layers:
        for ix, iy in disc(r, bites):
            out[(ix, iy, iz)] = STONE_DARK if (ix + iy + iz) % 3 == 0 else STONE
    for (ix, iy, iz) in list(out):
        if (ix, iy, iz + 1) not in out and (ix * 3 + iy * 5) % 4 != 0:
            out[(ix, iy, iz)] = MOSS if (ix + iy) % 2 else MOSS_DARK
    return out


def stump_cells():
    out = {}
    for iz in (0, 1):
        for ix, iy in disc(1):
            out[(ix, iy, iz)] = BARK_DARK if iz == 0 else BARK
    out[(0, 0, 2)] = WOOD
    return out


def log_cells():
    """2 m log on its side, axis along X, bottom on Z = 0."""
    out = {}
    for ix in range(-4, 4):
        for iy in (0, 1):
            for iz in (0, 1):
                end = ix in (-4, 3)
                out[(ix, iy, iz)] = WOOD if end else (BARK_DARK if (ix + iz) % 2 else BARK)
    return out


def build(name, occ, mat):
    mesh = bpy.data.meshes.new(name)
    verts = []
    faces = []
    colors = []
    index = {}

    def vert(ix, iy, iz, corner):
        key = (ix, iy, iz, corner)
        if key in index:
            return index[key]
        cx, cy, cz = corner
        index[key] = len(verts)
        verts.append(((ix + cx) * S, (iy + cy) * S, (iz + cz) * S))
        return index[key]

    for (ix, iy, iz), color in occ.items():
        for (dx, dy, dz), corners in FACE.items():
            if (ix + dx, iy + dy, iz + dz) in occ:
                continue
            faces.append(tuple(vert(ix, iy, iz, c) for c in corners))
            colors.append(color)
    mesh.from_pydata(verts, [], faces)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for poly, color in zip(mesh.polygons, colors):
        poly.use_smooth = False
        for li in poly.loop_indices:
            attr.data[li].color = (color[0], color[1], color[2], 1.0)
    mesh.color_attributes.active_color = attr
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(mat)
    return obj


def bounds(obj):
    xs, ys, zs = [], [], []
    for v in obj.data.vertices:
        xs.append(v.co.x)
        ys.append(v.co.y)
        zs.append(v.co.z)
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)


def check(specs):
    lines = ["Meadow kit check", "=" * 60]
    ok = True

    def add(label, passed, detail=""):
        nonlocal ok
        ok = ok and passed
        lines.append("  [%s] %s%s" % ("PASS" if passed else "FAIL", label, (" - " + detail) if detail else ""))

    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    add("three meshes", len(meshes) == 3, "found %s" % [o.name for o in meshes])
    imgs = [im.name for im in bpy.data.images if im.name not in ("Render Result", "Viewer Node")]
    add("no images in the file", not imgs, ", ".join(imgs))
    for obj, rule in specs:
        me = obj.data
        me.calc_loop_triangles()
        tris = len(me.loop_triangles)
        x0, x1, y0, y1, z0, z1 = bounds(obj)
        loc, rot, scl = obj.location, obj.rotation_euler, obj.scale
        add("%s name" % obj.name, obj.name == rule["name"])
        add("%s transforms applied" % obj.name, loc.length < 1e-6 and Vector(rot).length < 1e-6 and (scl - Vector((1, 1, 1))).length < 1e-6)
        add("%s base on Z=0" % obj.name, abs(z0) < 1e-6, "min z %.4f" % z0)
        height = z1 - z0
        add("%s height %.2f m" % (obj.name, height), rule["h"][0] <= height <= rule["h"][1])
        add("%s width %.2f m" % (obj.name, x1 - x0), rule["w"][0] <= (x1 - x0) <= rule["w"][1], "width %.2f" % (x1 - x0))
        add("%s flat shaded" % obj.name, not any(p.use_smooth for p in me.polygons))
        add("%s material M_World" % obj.name, [m.name for m in me.materials if m] == ["M_World"])
        add("%s triangles %d" % (obj.name, tris), tris < 2000)
    lines.append("SUMMARY: %s" % ("ALL PASS" if ok else "FAILURES"))
    text = "\n".join(lines) + "\n"
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    if not ok:
        raise SystemExit(1)


def render(objs):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = 1280
    sc.render.resolution_y = 720
    sc.render.image_settings.file_format = "PNG"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "VERTEX"
    sh.show_shadows = False
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    sc.world.color = (0.85, 0.86, 0.88)
    data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    data.type = "ORTHO"
    data.ortho_scale = 5.2
    data.clip_end = 100
    saved = {ob.name: ob.location.copy() for ob in objs}
    for ob, x in zip(objs, (-2.4, 0.15, 2.3)):
        ob.location = (x, 0, 0)
    yaw, pitch = math.radians(-35), math.radians(18)
    look = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    target = Vector((0.2, 0, 0.45))
    cam.location = target - look * 8
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(ART, "Renders", "meadow_kit.png")
    bpy.ops.render.render(write_still=True)
    for ob in objs:
        ob.location = saved[ob.name]
    print("rendered", sc.render.filepath)


def export_one(obj):
    os.makedirs(EXPORT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(EXPORT, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
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
    print("exported", path, os.path.getsize(path), "bytes")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    specs_src = (
        ("prop_rock_voxel", rock_cells(), {"name": "prop_rock_voxel", "h": (0.75, 1.5), "w": (1.0, 2.2)}),
        ("prop_stump_voxel", stump_cells(), {"name": "prop_stump_voxel", "h": (0.4, 0.9), "w": (0.5, 1.2)}),
        ("prop_log_voxel", log_cells(), {"name": "prop_log_voxel", "h": (0.4, 0.8), "w": (1.8, 2.4)}),
    )
    built = []
    rules = []
    for name, occ, rule in specs_src:
        ob = build(name, occ, mat)
        built.append(ob)
        rules.append((ob, rule))
        print("built", name, "cubes", len(occ))
    check(rules)
    render(built)
    bpy.context.preferences.filepaths.save_version = 0
    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    for ob in built:
        export_one(ob)
    print("saved", BLEND)


if __name__ == "__main__":
    main()
