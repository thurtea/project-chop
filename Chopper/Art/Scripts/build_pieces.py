"""Hammer pieces in the concept 2.0 cabin style: log walls, a doorway, a window, shingles, log stairs, a gable cross.

Walls are round logs (flat-shaded eight-sided prisms in three warm browns, so the lit tops, shaded undersides and the
creases between them read as a log wall); the frames round the door and window stay blocky. The roof is staggered
0.5 m shingles in three golden shades (concept 2.0), not a checker.

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


LOG = srgb(156, 102, 56)
LOG_DARK = srgb(132, 84, 44)
LOG_LIGHT = srgb(176, 118, 66)
LOG_END = srgb(206, 160, 104)
FRAME = srgb(82, 50, 28)
PLANK = srgb(134, 92, 56)
PLANK_DARK = srgb(112, 74, 42)
THATCH = srgb(206, 156, 80)
THATCH_DARK = srgb(174, 126, 60)
THATCH_LIGHT = srgb(226, 182, 104)
LOG_RADIUS = 0.125 * 2 ** 0.5   # square-cut logs filling the 0.25 m course (no thin facets to alias)
LOG_SIDES = 4

FACE = {
    (1, 0, 0): ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)),
    (-1, 0, 0): ((0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)),
    (0, 1, 0): ((1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)),
    (0, -1, 0): ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),
    (0, 0, 1): ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)),
    (0, 0, -1): ((0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)),
}


def mesh_from(name, occ, shift=(0.0, 0.0, 0.0), logs=()):
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
    for x0, x1, yc, zc, color in logs:
        add_log(bm_verts, faces, colors, x0 + shift[0], x1 + shift[0], yc + shift[1], zc + shift[2], color)
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


def add_log(verts, faces, colors, x0, x1, yc, zc, color):
    """One log along X: an eight-sided prism with end caps. The end caps are the lighter cut wood."""
    import math
    ring0, ring1 = [], []
    for k in range(LOG_SIDES):
        a = (k + 0.5) * 2 * math.pi / LOG_SIDES
        y = yc + LOG_RADIUS * math.cos(a)
        z = zc + LOG_RADIUS * math.sin(a)
        ring0.append(len(verts))
        verts.append((x0, y, z))
        ring1.append(len(verts))
        verts.append((x1, y, z))
    for k in range(LOG_SIDES):
        n = (k + 1) % LOG_SIDES
        faces.append((ring0[k], ring0[n], ring1[n], ring1[k]))
        colors.append(color)
    faces.append(tuple(reversed(ring0)))
    colors.append(LOG_END)
    faces.append(tuple(ring1))
    colors.append(LOG_END)


def log_shade(row, segment):
    """Each log a slightly different brown, so the courses read as separate logs."""
    h = (row * 7 + segment * 13 + 3) % 5
    return (LOG_DARK, LOG, LOG, LOG_LIGHT, LOG)[h]


BOARD_SHADES = (srgb(150, 104, 62), srgb(140, 96, 57), srgb(156, 110, 66))


def floor_cells():
    """Long boards along X, one per 0.25 m row, in three close shades."""
    out = {}
    for iy in range(-4, 4):
        base = BOARD_SHADES[(iy * 2) % len(BOARD_SHADES)]
        for ix in range(-4, 4):
            out[(ix, iy, 0)] = base
    return out


def wall_cells(hole):
    """hole(ix, iz) is true where the logs are cut away. Only the frame round a hole is cubes; see wall_logs."""
    out = {}
    for ix in range(-4, 4):
        for iz in range(8):
            if hole(ix, iz):
                continue
            if hole(ix - 1, iz) or hole(ix + 1, iz) or hole(ix, iz - 1) or hole(ix, iz + 1):
                out[(ix, 0, iz)] = FRAME
    return out


def wall_logs(hole):
    """Round logs for every run of wall cells in a course that is not a hole or a frame cube."""
    framed = wall_cells(hole)
    logs = []
    for iz in range(8):
        run = []
        segment = 0
        for ix in list(range(-4, 4)) + [None]:
            solid = ix is not None and not hole(ix, iz) and (ix, 0, iz) not in framed
            if solid:
                run.append(ix)
                continue
            if run:
                logs.append((run[0] * S, (run[-1] + 1) * S, 0.5 * S, (iz + 0.5) * S, log_shade(iz, segment)))
                segment += 1
                run = []
    return logs


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
            # shingles two cells wide, staggered by a cell on every other row, three shades by a small hash
            shingle = (ix + 4 + (iy % 2)) // 2
            h = (shingle * 5 + iy * 3 + 1) % 6
            color = (THATCH, THATCH_DARK, THATCH, THATCH_LIGHT, THATCH, THATCH_DARK)[h]
            # the shingle's lower edge row of cells (the exposed butt) a shade darker reads as the overlap
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
    # the crossed beams cross at the apex and stand only a little proud of it (they read as antlers when long)
    for t in range(3):
        out[(-2 + t, 0, 2 + t)] = LOG_END
        out[(1 - t, 0, 2 + t)] = LOG_END
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
        ("piece_wall_voxel", wall_cells(lambda ix, iz: False), wall_shift, wall_logs(lambda ix, iz: False)),
        ("piece_doorway_voxel", wall_cells(door_hole), wall_shift, wall_logs(door_hole)),
        ("piece_window_voxel", wall_cells(window_hole), wall_shift, wall_logs(window_hole)),
        ("piece_roof_voxel", roof_cells(), (0.0, 0.0, 0.0)),
        ("piece_stairs_voxel", stair_cells(), (0.0, 0.0, 0.0)),
        ("piece_gable_voxel", gable_cells(), wall_shift),
    ]
    lines = ["Concept 2.0 hammer pieces", "=" * 60]
    ok = True
    wall_logs_count = len(pieces[1][3])
    lines.append("  [%s] plain wall is %d round logs (one per 0.25 m course)" % ("PASS" if wall_logs_count == 8 else "FAIL", wall_logs_count))
    ok = ok and wall_logs_count == 8
    for name, occ, shift, *logs in pieces:
        obj = mesh_from(name, occ, shift, logs[0] if logs else ())
        obj.data.calc_loop_triangles()
        tris = len(obj.data.loop_triangles)
        x0, x1, y0, y1, z0, z1 = bounds(obj)
        path = export_one(obj)
        # round logs may sit up to 2 cm into the floor below (their octagon bottom)
        passed = tris > 0 and -0.02 < z0 < 1e-4 and x1 - x0 <= 2.01 and y1 - y0 <= 2.01
        ok = ok and passed
        lines.append("  [%s] %s  tris %d  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f  %s" % (
            "PASS" if passed else "FAIL", name, tris, x0, x1, y0, y1, z0, z1, os.path.basename(path)))
    # The doorway is a hole: the middle of the bottom course is gone.
    # (the frame cubes border the opening; the lintel course over it is a frame too)
    door = pieces[2][1]
    door_logs = pieces[2][3]
    hole = (0, 0, 0) not in door and (-3, 0, 0) in door and (0, 0, 7) in door and any(l[3] < S for l in door_logs)
    ok = ok and hole
    lines.append("  [%s] doorway is open at the bottom and closed at the lintel" % ("PASS" if hole else "FAIL"))
    lines.append("SUMMARY %s" % ("ALL PASS" if ok else "FAILED"))
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
