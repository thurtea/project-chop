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


LOG = srgb(112, 72, 40)
LOG_DARK = srgb(70, 44, 24)
LOG_LIGHT = srgb(148, 98, 58)
LOG_END = srgb(188, 142, 90)
STONE = srgb(132, 128, 120)
STONE_DARK = srgb(96, 94, 88)
STONE_LIGHT = srgb(158, 154, 146)
PLANK = srgb(128, 86, 50)
PLANK_DARK = srgb(104, 68, 38)
THATCH = srgb(196, 146, 82)
THATCH_DARK = srgb(146, 100, 54)
THATCH_LIGHT = srgb(222, 176, 106)
FRAME = srgb(62, 38, 22)
POST = srgb(72, 44, 24)
BARGE = srgb(88, 54, 28)


def log_tone(ix, iy, iz):
    if iz % 2 == 0:
        return LOG_DARK
    return LOG_LIGHT if ((ix // 4) + (iy // 4)) % 2 else LOG


def thatch_tone(ix, iy, iz):
    band = iz % 3
    if band == 0:
        return THATCH_DARK if (ix // 2) % 4 == 0 else THATCH
    if band == 1:
        return THATCH_LIGHT
    return THATCH


def h3(i, j, k):
    n = (i * 73856093) ^ (j * 19349663) ^ (k * 83492791)
    n = (n ^ (n >> 13)) * 1274126177
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


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

    for iz in range(8, 17, 2):
        for ix, iy, dx, dy in ((-25, -17, -1, -1), (24, -17, 1, -1), (-25, 16, -1, 1), (24, 16, 1, 1)):
            occ.setdefault((ix + dx, iy, iz), LOG_END)
            occ.setdefault((ix, iy + dy, iz), LOG_END)
            occ.setdefault((ix + 2 * dx, iy, iz), LOG_END)
    for ix in (-21, -9, 10, 21):
        fill(occ, ix, ix + 1, -18, -17, 8, 16, lambda i, j, k: POST)
    for ix in (-17, -1, 17):
        fill(occ, ix, ix + 1, 17, 18, 8, 16, lambda i, j, k: POST)
    for lo, hi in ((-17, -13), (13, 17)):
        fill(occ, lo - 1, hi + 2, -18, -17, 10, 11, lambda i, j, k: FRAME)
        fill(occ, lo - 1, hi + 2, -18, -17, 15, 16, lambda i, j, k: FRAME)
        fill(occ, lo - 2, lo - 1, -18, -17, 11, 15, lambda i, j, k: BARGE)
        fill(occ, hi + 2, hi + 3, -18, -17, 11, 15, lambda i, j, k: BARGE)
    for lo, hi in ((-9, -5), (9, 13)):
        fill(occ, lo - 1, hi + 2, 17, 18, 10, 11, lambda i, j, k: FRAME)
        fill(occ, lo - 1, hi + 2, 17, 18, 15, 16, lambda i, j, k: FRAME)
    fill(occ, -1, 9, -18, -17, 16, 17, lambda i, j, k: FRAME)
    fill(occ, -1, 0, -18, -17, 8, 16, lambda i, j, k: POST)
    fill(occ, 8, 9, -18, -17, 8, 16, lambda i, j, k: POST)
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
                if iz == iz_top - 2:
                    put(occ, ix, iy, iz, LOG_DARK if iy % 2 else LOG_LIGHT)
                else:
                    put(occ, ix, iy, iz, thatch_tone(ix, iy, iz))
            if t > 0.88:
                put(occ, ix, iy, iz_top + 1, BARGE)
            if t < 0.06:
                put(occ, ix, iy, iz_top - 3, THATCH_DARK if (ix % 3) else THATCH)
            elif int(t * 40) % 6 == 0 and iz_top + 1 >= 15:
                occ.setdefault((ix, iy, iz_top + 1), THATCH_LIGHT if (ix + iy) % 2 else THATCH_DARK)

    for iz in range(9, 16, 2):
        for ix in range(ix0, ix1):
            if not (in_door(ix, iz) or in_front_window(ix, iz) or in_door(ix, iz - 1) or in_door(ix, iz + 1) or in_front_window(ix, iz - 1) or in_front_window(ix, iz + 1)):
                occ.setdefault((ix, -18, iz), LOG_LIGHT)
            if not (in_back_window(ix, iz) or in_back_window(ix, iz - 1) or in_back_window(ix, iz + 1)):
                occ.setdefault((ix, 17, iz), LOG_LIGHT)
        for iy in range(iy0, iy1):
            if not (in_end_window(iy, iz) or in_end_window(iy, iz - 1) or in_end_window(iy, iz + 1)):
                occ.setdefault((-26, iy, iz), LOG_LIGHT)
                occ.setdefault((25, iy, iz), LOG_LIGHT)

    def roof_z(iy):
        across = (iy + 0.5) * S
        t = max(0.0, 1.0 - abs(across) / eave_half)
        return int(round((EAVE + (RIDGE - EAVE) * t) / S))

    dx0, dx1, dfront = -15, -5, iy_r0 + 6
    dtop = roof_z(dfront) + 6
    for ix in range(dx0, dx1):
        for iz in range(roof_z(dfront) - 1, dtop):
            win = dx0 + 3 <= ix <= dx1 - 4 and roof_z(dfront) + 1 <= iz <= dtop - 2
            put(occ, ix, dfront, iz, srgb(54, 66, 78) if win else (FRAME if ix in (dx0, dx1 - 1) else log_tone(ix, dfront, iz)))
        for iy in range(dfront, dfront + 8):
            for iz in range(roof_z(iy), dtop):
                if ix in (dx0, dx1 - 1):
                    occ.setdefault((ix, iy, iz), log_tone(ix, iy, iz))
    mid = (dx0 + dx1 - 1) / 2.0
    for ix in range(dx0 - 1, dx1 + 1):
        h = dtop + int(round((dx1 - dx0) / 2.0 - abs(ix - mid)))
        for iy in range(dfront - 1, dfront + 9):
            if h >= roof_z(iy):
                put(occ, ix, iy, h, thatch_tone(ix, iy, h))
                put(occ, ix, iy, h - 1, THATCH_DARK)
        for iz in range(dtop, h - 1):
            occ.setdefault((ix, dfront, iz), log_tone(ix, dfront, iz))
    for k in range(0, 4):
        put(occ, int(mid) - 3 + k, dfront - 1, dtop + 1 + k, BARGE)
        put(occ, int(mid) + 4 - k, dfront - 1, dtop + 1 + k, BARGE)

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

    def roof_under(iy):
        t = max(0.0, 1.0 - abs((iy + 0.5) * S) / eave_half)
        return int(round((EAVE + (RIDGE - EAVE) * t) / S)) - 2

    ring = [(ix, -17) for ix in range(ix0, ix1)] + [(ix, 16) for ix in range(ix0, ix1)]
    ring += [(-25, iy) for iy in range(iy0, iy1)] + [(24, iy) for iy in range(iy0, iy1)]
    for ix, iy in ring:
        for iz in range(8, roof_under(iy) + 1):
            if (ix, iy, iz) in occ:
                continue
            if iy == -17 and (in_door(ix, iz) or in_front_window(ix, iz)):
                continue
            if iy == 16 and in_back_window(ix, iz):
                continue
            if ix in (-25, 24) and in_end_window(iy, iz):
                continue
            put(occ, ix, iy, iz, log_tone(ix, iy, iz))
    for ix in range(-24, 24, 6):
        for iy in range(iy0, iy1):
            top = roof_under(iy)
            if top >= 16:
                occ.setdefault((ix, iy, top), BARGE)
    for iy in (-9, 8):
        for ix in range(-24, 24):
            top = roof_under(iy)
            if top >= 16:
                occ.setdefault((ix, iy, top), FRAME)
    glass = srgb(54, 66, 78)
    for iz in range(8, 17):
        for ix in range(ix0, ix1):
            if in_front_window(ix, iz):
                put(occ, ix, -17, iz, glass)
            if in_back_window(ix, iz):
                put(occ, ix, 16, iz, glass)
        for iy in range(iy0, iy1):
            if in_end_window(iy, iz):
                put(occ, -25, iy, iz, glass)
                put(occ, 24, iy, iz, glass)
    fill(occ, -1, 9, -27, -17, 7, 8, lambda i, j, k: PLANK if j % 2 == 0 else PLANK_DARK)
    fill(occ, -1, 9, -27, -17, 6, 7, lambda i, j, k: PLANK_DARK)
    fill(occ, -1, 1, -27, -25, 8, 16, lambda i, j, k: POST)
    fill(occ, 7, 9, -27, -25, 8, 16, lambda i, j, k: POST)
    fill(occ, -1, 9, -27, -25, 16, 17, lambda i, j, k: POST)

    porch_half = 1.75
    porch_ix0, porch_ix1 = -4, 12
    porch_iy0, porch_iy1 = -32, -18

    def porch_underside(ix, iy):
        across = abs((ix + 0.5) * S - 1.0) / porch_half
        along = (-18 - iy) / 14.0
        rise = max(0.0, 1.0 - across) * max(0.0, min(1.0, along))
        return 15 + int(round(4 * rise))

    for iy in range(porch_iy0 + 1, porch_iy1):
        for ix in range(porch_ix0, porch_ix1):
            if abs((ix + 0.5) * S - 1.0) > porch_half:
                continue
            iz0 = porch_underside(ix, iy)
            for iz in range(iz0, iz0 + 3):
                put(occ, ix, iy, iz, thatch_tone(ix, iy, iz))
    face_y = porch_iy0
    for t in range(0, 11):
        for dy in (0, -1):
            put(occ, -4 + t, face_y + dy, 15 + t, BARGE)
            put(occ, 11 - t, face_y + dy, 15 + t, BARGE)
            put(occ, -4 + t, face_y + dy, 16 + t, BARGE)
            put(occ, 11 - t, face_y + dy, 16 + t, BARGE)
    for t in range(0, 10):
        iz = 15 + int(round(t * 6 / 9.0))
        put(occ, -1 + t, face_y, iz, LOG_END)
        put(occ, 8 - t, face_y, iz, LOG_END)

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
    thatch = {THATCH, THATCH_DARK, THATCH_LIGHT}
    bottoms = {}
    for (ix, iy, iz), color in occ.items():
        if color not in thatch:
            continue
        prev = bottoms.get((ix, iy))
        if prev is None or iz < prev:
            bottoms[(ix, iy)] = iz

    def vert(ix, iy, iz, corner):
        key = (ix, iy, iz, corner)
        if key in index:
            return index[key]
        cx, cy, cz = corner
        index[key] = len(bm_verts)
        bm_verts.append(((ix + cx) * S, (iy + cy) * S, (iz + cz) * S))
        return index[key]

    def abs_vert(x, y, z):
        key = ("s", x, y, z)
        if key in index:
            return index[key]
        index[key] = len(bm_verts)
        bm_verts.append((x * S, y * S, z * S))
        return index[key]

    def riser(ix, iy, iz, dx, dy):
        own = bottoms.get((ix, iy))
        nbr = bottoms.get((ix + dx, iy + dy))
        if own is None or nbr is None or nbr <= own:
            return False
        return own <= iz < nbr

    for (ix, iy, iz), color in occ.items():
        for (dx, dy, dz), corners in FACE.items():
            if (ix + dx, iy + dy, iz + dz) in occ:
                continue
            if dz == -1 and bottoms.get((ix, iy)) == iz and color in thatch:
                continue
            if dz == 0 and riser(ix, iy, iz, dx, dy):
                continue
            faces.append(tuple(vert(ix, iy, iz, c) for c in corners))
            colors.append(color)

    for (ix, iy), iz in bottoms.items():
        z00 = iz
        z10 = bottoms.get((ix + 1, iy), iz)
        z01 = bottoms.get((ix, iy + 1), iz)
        z11 = bottoms.get((ix + 1, iy + 1), z10 if (ix + 1, iy) in bottoms else z01)
        if (ix + 1, iy) not in bottoms:
            z10 = z00
        if (ix, iy + 1) not in bottoms:
            z01 = z00
        if (ix + 1, iy + 1) not in bottoms:
            z11 = z10 if (ix + 1, iy) in bottoms else z01
        faces.append((
            abs_vert(ix, iy + 1, z01),
            abs_vert(ix + 1, iy + 1, z11),
            abs_vert(ix + 1, iy, z10),
            abs_vert(ix, iy, z00),
        ))
        colors.append(THATCH)
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
