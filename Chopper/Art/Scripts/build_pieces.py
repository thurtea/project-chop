"""Hammer pieces in the concept 2.0 cabin style: log walls, a doorway, a window, thatch, log stairs, a gable cross.

Run:  blender -b -P build_pieces.py
Out:  Art/Export/piece_*_voxel.fbx, Art/Renders/piece_check_report.txt

1 unit = 1 m. Authored Z-up, the same way as the pine, so the Unity prefab wrapper stands them up.
Each piece's pivot is the bottom center of its 2 m cell. Walls run along X and are thin on Y.
The roof and the stairs rise toward -Y, which lands as +Z in Unity (the high edge of the old roof piece).
"""
import os

import bpy
from mathutils import Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
EXPORT = os.path.join(ART, "Export")
REPORT = os.path.join(ART, "Renders", "piece_check_report.txt")

S = 0.25


def srgb(r, g, b):
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b))


LOG = srgb(150, 96, 52)
LOG_DARK = srgb(118, 72, 38)
LOG_END = srgb(196, 150, 96)
FRAME = srgb(82, 50, 28)
PLANK = srgb(134, 92, 56)
PLANK_DARK = srgb(112, 74, 42)
THATCH = srgb(214, 168, 84)
THATCH_DARK = srgb(170, 124, 56)

FACE = {
    (1, 0, 0): ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)),
    (-1, 0, 0): ((0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)),
    (0, 1, 0): ((1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)),
    (0, -1, 0): ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),
    (0, 0, 1): ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)),
    (0, 0, -1): ((0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)),
}


def mesh_from(name, occ, shift=(0.0, 0.0, 0.0)):
    mesh = bpy.data.meshes.new(name)
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
        bm_verts.append(((ix + cx) * S + shift[0], (iy + cy) * S + shift[1], (iz + cz) * S + shift[2]))
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
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    mesh.materials.append(mat)
    return obj


def floor_cells():
    out = {}
    for ix in range(-4, 4):
        for iy in range(-4, 4):
            out[(ix, iy, 0)] = PLANK if (ix + iy) % 2 == 0 else PLANK_DARK
    return out


def wall_cells(hole):
    """hole(ix, iz) is true where the logs are cut away."""
    out = {}
    for ix in range(-4, 4):
        for iz in range(8):
            if hole(ix, iz):
                continue
            color = LOG_DARK if iz % 2 else LOG
            if hole(ix - 1, iz) or hole(ix + 1, iz) or hole(ix, iz - 1) or hole(ix, iz + 1):
                color = FRAME
            out[(ix, 0, iz)] = color
    return out


def door_hole(ix, iz):
    return -2 <= ix <= 1 and 0 <= iz <= 6


def window_hole(ix, iz):
    return -1 <= ix <= 0 and 4 <= iz <= 5


def roof_cells():
    """Thatch on the slope. Blender -Y is the high edge (Unity +Z)."""
    out = {}
    for ix in range(-4, 4):
        for iy in range(-4, 4):
            y1 = (iy + 1) * S
            base = int(round((1.0 - y1) / S))
            color = LOG if ix in (-4, 3) else (THATCH_DARK if (ix + iy) % 2 else THATCH)
            out[(ix, iy, base)] = color
    return out


def stair_cells():
    out = {}
    for ix in range(-4, 4):
        for iy in range(-4, 4):
            y0 = iy * S
            tread = int(round((1.0 - y0) / S))
            color = LOG_DARK if ix in (-4, 3) else (LOG if iy % 2 == 0 else PLANK)
            for iz in range(max(tread, 1)):
                out[(ix, iy, iz)] = color
    return out


def gable_cells():
    """Log triangle above a wall, with the crossed beam ends from concept 2.0."""
    out = {}
    for iz in range(8):
        half = 1.0 - iz * S
        for ix in range(-4, 4):
            if abs((ix + 0.5) * S) <= half + 1e-6:
                out[(ix, 0, iz)] = LOG_DARK if iz % 2 else LOG
    for t in range(5):
        out[(-2 + t, 0, 3 + t)] = LOG_END
        out[(1 - t, 0, 3 + t)] = LOG_END
    return out


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
        add_leaf_bones=False,
    )
    return path


def bounds(obj):
    xs, ys, zs = [], [], []
    for v in obj.data.vertices:
        xs.append(v.co.x)
        ys.append(v.co.y)
        zs.append(v.co.z)
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Walls are one cube thick. Shift them so that thickness is centered on the piece, matching the 0.3 m collider.
    wall_shift = (0.0, -0.125, 0.0)
    pieces = [
        ("piece_floor_voxel", floor_cells(), (0.0, 0.0, 0.0)),
        ("piece_wall_voxel", wall_cells(lambda ix, iz: False), wall_shift),
        ("piece_doorway_voxel", wall_cells(door_hole), wall_shift),
        ("piece_window_voxel", wall_cells(window_hole), wall_shift),
        ("piece_roof_voxel", roof_cells(), (0.0, 0.0, 0.0)),
        ("piece_stairs_voxel", stair_cells(), (0.0, 0.0, 0.0)),
        ("piece_gable_voxel", gable_cells(), wall_shift),
    ]
    lines = ["Concept 2.0 hammer pieces", "=" * 60]
    ok = True
    for name, occ, shift in pieces:
        obj = mesh_from(name, occ, shift)
        obj.data.calc_loop_triangles()
        tris = len(obj.data.loop_triangles)
        x0, x1, y0, y1, z0, z1 = bounds(obj)
        path = export_one(obj)
        passed = tris > 0 and abs(z0) < 1e-4 and x1 - x0 <= 2.01 and y1 - y0 <= 2.01
        ok = ok and passed
        lines.append("  [%s] %s  tris %d  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f  %s" % (
            "PASS" if passed else "FAIL", name, tris, x0, x1, y0, y1, z0, z1, os.path.basename(path)))
    # The doorway is a hole: the middle of the bottom course is gone.
    door = pieces[2][1]
    hole = (0, 0, 0) not in door and (-4, 0, 0) in door and (0, 0, 7) in door
    ok = ok and hole
    lines.append("  [%s] doorway is open at the bottom and closed at the lintel" % ("PASS" if hole else "FAIL"))
    lines.append("SUMMARY %s" % ("ALL PASS" if ok else "FAILED"))
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
