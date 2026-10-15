"""Hub hall as 0.25 m voxels, matched to chopper-start-menu-background-3D-concept-2.0.jpg
and the three-storey plan on chopper-hut-zone-3D-concept-3.0.jpg.

Stone foundation, log-block walls, pale thatch roof, porch, chunky steps, chimney, gable X.
Same piece-metre footprint as the collision pieces (W 6, D 4, main 2, eave 4.05, ridge 8).
Z up. Under Hut Cabin (scale 1.5) at identity: Blender X = Unity X, Blender Z = Unity Y,
Blender -Y = Unity +Z (the front / door).

Run:  blender -b -P build_hall.py
Out:  Art/Export/hall_voxel.fbx, Art/Renders/hall_check_report.txt
"""
import math
import os

import bpy
from mathutils import Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
EXPORT = os.path.join(ART, "Export")
REPORT = os.path.join(ART, "Renders", "hall_check_report.txt")

S = 0.25
W, D = 6.0, 4.0
MAIN, EAVE, RIDGE = 2.0, 4.05, 8.0
OVER = 0.45

FACE = {
    (1, 0, 0): ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)),
    (-1, 0, 0): ((0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)),
    (0, 1, 0): ((1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)),
    (0, -1, 0): ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),
    (0, 0, 1): ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)),
    (0, 0, -1): ((0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)),
}


def srgb(r, g, b):
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b))


LOG = srgb(118, 76, 44)
LOG_DARK = srgb(86, 54, 30)
LOG_LIGHT = srgb(148, 98, 58)
LOG_END = srgb(188, 142, 90)
STONE = srgb(132, 128, 120)
STONE_DARK = srgb(96, 94, 88)
STONE_LIGHT = srgb(158, 154, 146)
PLANK = srgb(128, 86, 50)
PLANK_DARK = srgb(104, 68, 38)
THATCH = srgb(214, 184, 118)
THATCH_DARK = srgb(176, 144, 86)
THATCH_LIGHT = srgb(232, 206, 142)
FRAME = srgb(62, 38, 22)
POST = srgb(72, 44, 24)
BARGE = srgb(88, 54, 28)


def h3(i, j, k):
    n = (i * 73856093) ^ (j * 19349663) ^ (k * 83492791)
    n = (n ^ (n >> 13)) * 1274126177
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def log_tone(ix, iy, iz):
    a = h3(ix, iy, iz)
    if iz % 2 == 0:
        return LOG_DARK if a < 0.35 else LOG
    return LOG_LIGHT if a > 0.72 else LOG


def thatch_tone(ix, iy, iz):
    a = h3(ix * 3, iy * 5, iz * 7)
    if a < 0.22:
        return THATCH_DARK
    if a > 0.82:
        return THATCH_LIGHT
    return THATCH


def stone_tone(ix, iy, iz):
    a = h3(ix + 2, iy - 1, iz)
    if (ix + iz) % 2 == 0:
        return STONE_DARK if a < 0.4 else STONE
    return STONE_LIGHT if a > 0.7 else STONE


def put(occ, ix, iy, iz, color):
    occ[(ix, iy, iz)] = color


def fill(occ, ix0, ix1, iy0, iy1, iz0, iz1, color_fn):
    for ix in range(ix0, ix1):
        for iy in range(iy0, iy1):
            for iz in range(iz0, iz1):
                put(occ, ix, iy, iz, color_fn(ix, iy, iz))


def in_door(ix, iz):
    return 0 <= ix <= 7 and 8 <= iz <= 15


def in_front_window(ix, iz):
    return 11 <= iz <= 14 and ((-17 <= ix <= -13) or (13 <= ix <= 17))


def in_back_window(ix, iz):
    return 11 <= iz <= 14 and ((-9 <= ix <= -5) or (9 <= ix <= 13))


def in_end_window(iy, iz):
    return 11 <= iz <= 14 and -5 <= iy <= -2


def cells():
    occ = {}
    ix0, ix1 = -25, 25
    iy0, iy1 = -17, 17
    fill(occ, ix0, ix1, -18, -16, 6, 9, stone_tone)
    fill(occ, ix0, ix1, 16, 18, 6, 9, stone_tone)
    fill(occ, -26, -24, iy0, iy1, 6, 9, stone_tone)
    fill(occ, 24, 26, iy0, iy1, 6, 9, stone_tone)

    for iz in range(8, 17):
        for ix in range(ix0, ix1):
            if in_door(ix, iz) or in_front_window(ix, iz):
                continue
            put(occ, ix, -17, iz, log_tone(ix, -17, iz))
            if in_front_window(ix - 1, iz) or in_front_window(ix + 1, iz) or in_front_window(ix, iz - 1) or in_front_window(ix, iz + 1) or in_door(ix - 1, iz) or in_door(ix + 1, iz) or in_door(ix, iz - 1) or in_door(ix, iz + 1):
                put(occ, ix, -17, iz, FRAME)
        for ix in range(ix0, ix1):
            if in_back_window(ix, iz):
                continue
            put(occ, ix, 16, iz, log_tone(ix, 16, iz))
            if in_back_window(ix - 1, iz) or in_back_window(ix + 1, iz) or in_back_window(ix, iz - 1) or in_back_window(ix, iz + 1):
                put(occ, ix, 16, iz, FRAME)
        for iy in range(iy0, iy1):
            if in_end_window(iy, iz):
                continue
            put(occ, -25, iy, iz, log_tone(-25, iy, iz))
            put(occ, 24, iy, iz, log_tone(24, iy, iz))
            if in_end_window(iy - 1, iz) or in_end_window(iy + 1, iz) or in_end_window(iy, iz - 1) or in_end_window(iy, iz + 1):
                put(occ, -25, iy, iz, FRAME)
                put(occ, 24, iy, iz, FRAME)

    for ix, iy in ((-25, -17), (24, -17), (-25, 16), (24, 16)):
        fill(occ, ix, ix + 1, iy, iy + 1, 8, 17, lambda i, j, k: POST)
    fill(occ, ix0, ix1, -18, -16, 16, 17, lambda i, j, k: POST)
    fill(occ, ix0, ix1, 16, 18, 16, 17, lambda i, j, k: POST)
    fill(occ, -26, -24, iy0, iy1, 16, 17, lambda i, j, k: POST)
    fill(occ, 24, 26, iy0, iy1, 16, 17, lambda i, j, k: POST)

    eave_half = D + OVER
    ix_r0, ix_r1 = -27, 27
    iy_r0 = int(math.floor((-eave_half) / S))
    iy_r1 = int(math.ceil(eave_half / S))
    for ix in range(ix_r0, ix_r1):
        for iy in range(iy_r0, iy_r1):
            across = (iy + 0.5) * S
            t = max(0.0, 1.0 - abs(across) / eave_half)
            z = EAVE + (RIDGE - EAVE) * t
            iz_top = int(round(z / S))
            for iz in range(iz_top - 2, iz_top + 1):
                if iz < 15:
                    continue
                put(occ, ix, iy, iz, thatch_tone(ix, iy, iz))
            if t > 0.88:
                put(occ, ix, iy, iz_top + 1, BARGE)

    for side_ix in (-29, -28, 27, 28):
        for t in range(0, 24):
            iz = 16 + t
            put(occ, side_ix, iy_r0 + t, iz, BARGE)
            put(occ, side_ix, iy_r1 - 1 - t, iz, BARGE)
        for t in range(0, 5):
            put(occ, side_ix, -2 + t, 36 + t, LOG_END)
            put(occ, side_ix, 1 - t, 36 + t, LOG_END)
    fill(occ, -30, 30, -2, 2, 33, 35, lambda i, j, k: LOG_END)

    for iz in range(16, 33):
        span = eave_half * (1.0 - (iz * S - EAVE) / (RIDGE - EAVE + 0.05))
        if span < 0.15:
            continue
        iy_a = int(math.floor(-span / S))
        iy_b = int(math.ceil(span / S))
        for iy in range(iy_a, iy_b):
            if (-25, iy, iz) not in occ:
                put(occ, -25, iy, iz, log_tone(-25, iy, iz))
            if (24, iy, iz) not in occ:
                put(occ, 24, iy, iz, log_tone(24, iy, iz))

    fill(occ, -1, 9, -27, -17, 7, 8, lambda i, j, k: PLANK if j % 2 == 0 else PLANK_DARK)
    fill(occ, -1, 9, -27, -17, 6, 7, lambda i, j, k: PLANK_DARK)
    fill(occ, -1, 1, -27, -25, 8, 16, lambda i, j, k: POST)
    fill(occ, 7, 9, -27, -25, 8, 16, lambda i, j, k: POST)
    fill(occ, -1, 9, -27, -25, 16, 17, lambda i, j, k: POST)

    porch_half = 1.5
    porch_eave, porch_ridge = MAIN + 1.55, MAIN + 3.15
    for iy in range(-31, -18):
        for ix in range(-3, 11):
            across = (ix + 0.5) * S - 1.0
            t = max(0.0, 1.0 - abs(across) / porch_half)
            z = porch_eave + (porch_ridge - porch_eave) * t
            iz_top = int(round(z / S))
            for iz in range(max(14, iz_top - 2), iz_top + 1):
                put(occ, ix, iy, iz, THATCH_DARK if (ix + iy + iz) % 3 == 0 else THATCH)
    for face_y in (-32, -31):
        for t in range(0, 8):
            put(occ, -3 + t, face_y, 14 + t, BARGE)
            put(occ, 10 - t, face_y, 14 + t, BARGE)
        for t in range(0, 4):
            put(occ, 3 + t, face_y, 21 + t, LOG_END)
            put(occ, 4 - t, face_y, 21 + t, LOG_END)
    for iz in range(14, 22):
        span = porch_half * max(0.0, 1.0 - (iz * S - porch_eave) / (porch_ridge - porch_eave + 0.05))
        if span < 0.12:
            continue
        ix_a = int(math.floor((1.0 - span) / S))
        ix_b = int(math.ceil((1.0 + span) / S))
        for ix in range(ix_a, ix_b):
            put(occ, ix, -31, iz, log_tone(ix, -31, iz))

    fill(occ, 1, 7, -34, -29, 6, 7, lambda i, j, k: STONE if (i + j) % 2 == 0 else STONE_DARK)
    fill(occ, 1, 7, -29, -26, 7, 8, lambda i, j, k: PLANK if j % 2 == 0 else PLANK_DARK)
    fill(occ, -1, 1, -34, -26, 6, 8, stone_tone)
    fill(occ, 7, 9, -34, -26, 6, 8, stone_tone)

    fill(occ, 25, 29, 4, 9, 8, 38, stone_tone)
    for iz in range(34, 38):
        for ix in range(26, 28):
            for iy in range(5, 8):
                occ.pop((ix, iy, iz), None)
    fill(occ, 24, 30, 3, 10, 37, 39, stone_tone)
    fill(occ, -26, 26, -2, 3, 28, 34, lambda i, j, k: THATCH_DARK)
    return occ


def mesh_from(occ):
    bm_verts = []
    faces = []
    colors = []
    index = {}

    def vert(ix, iy, iz, corner):
        key = (ix, iy, iz, corner)
        if key in index:
            return index[key]
        cx, cy, cz = corner
        index[key] = len(bm_verts)
        bm_verts.append(((ix + cx) * S, (iy + cy) * S, (iz + cz) * S))
        return index[key]

    for (ix, iy, iz), color in occ.items():
        for (dx, dy, dz), corners in FACE.items():
            if (ix + dx, iy + dy, iz + dz) in occ:
                continue
            faces.append(tuple(vert(ix, iy, iz, c) for c in corners))
            colors.append(color)
    return bm_verts, faces, colors


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    occ = cells()
    verts, faces, colors = mesh_from(occ)
    verts = [(x, -y, z) for x, y, z in verts]
    faces = [tuple(reversed(f)) for f in faces]
    mesh = bpy.data.meshes.new("hall_voxel")
    mesh.from_pydata(verts, [], faces)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for poly, color in zip(mesh.polygons, colors):
        poly.use_smooth = False
        for li in poly.loop_indices:
            attr.data[li].color = (color[0], color[1], color[2], 1.0)
    mesh.color_attributes.active_color = attr
    obj = bpy.data.objects.new("hall_voxel", mesh)
    bpy.context.scene.collection.objects.link(obj)
    mat = bpy.data.materials.new("M_World")
    try:
        mat.use_nodes = True
        bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        vc = mat.node_tree.nodes.new("ShaderNodeVertexColor")
        vc.layer_name = "Col"
        mat.node_tree.links.new(vc.outputs["Color"], bsdf.inputs["Base Color"])
    except Exception:
        pass
    mesh.materials.append(mat)
    mesh.calc_loop_triangles()
    xs = [v[0] for v in verts]
    ys = [v[1] for v in verts]
    zs = [v[2] for v in verts]
    tris = len(mesh.loop_triangles)
    os.makedirs(EXPORT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(EXPORT, "hall_voxel.fbx")
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
        add_leaf_bones=False,
    )
    dg = bpy.context.evaluated_depsgraph_get()
    missed = 0
    tried = 0
    for ix in range(-6, 7):
        for iy in range(-4, 5):
            x, y = ix * 0.85, iy * 0.7
            if abs(y) > 3.2 or abs(x) > 5.6:
                continue
            tried += 1
            if not obj.ray_cast(Vector((x, y, 4.55)), Vector((0, 0, 1)), distance=3.5, depsgraph=dg)[0]:
                missed += 1
    for z, y in ((6.55, 0.0), (6.4, 0.35), (6.15, 0.9), (5.85, 1.6), (6.5, -0.4)):
        tried += 1
        if not obj.ray_cast(Vector((0.0, y, z)), Vector((1, 0, 0)), distance=8, depsgraph=dg)[0]:
            missed += 1
    roof_ok = missed == 0
    ok = tris > 1000 and max(zs) > 6.5 and min(zs) > 1.4 and max(ys) > 5.5 and max(xs) > 6.0 and roof_ok
    lines = [
        "Hub hall",
        "=" * 60,
        "  tris %d cells %d" % (tris, len(occ)),
        "  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)),
        "  [%s] loft rays hit the roof (%d/%d)" % ("PASS" if roof_ok else "FAIL", tried - missed, tried),
        "SUMMARY %s" % ("ALL PASS" if ok else "FAILED"),
    ]
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.filepath = os.path.join(ART, "Renders", "hall_three_quarter.png")
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 38
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = (14, 16, 9)
    target = Vector((0, -1, 3.6))
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (0.9, 0.2, 0.6)
    bpy.context.scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("W")
    scene.world = world
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)
    cam.location = (1.0, 20, 5.2)
    target = Vector((1.0, 0.0, 3.4))
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 32
    scene.render.filepath = os.path.join(ART, "Renders", "hall_front.png")
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is None:
        bg = world.node_tree.nodes.new("ShaderNodeBackground")
        world.node_tree.links.new(bg.outputs["Background"], world.node_tree.nodes["World Output"].inputs["Surface"])
    bg.inputs[0].default_value = (0.42, 0.72, 0.98, 1.0)
    bg.inputs[1].default_value = 1.4
    cam.location = (-2.2, 0.35, 5.15)
    target = Vector((4.2, 0.05, 6.45))
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 28
    scene.render.filepath = os.path.join(ART, "Renders", "hut_preview_up.png")
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)


if __name__ == "__main__":
    main()
