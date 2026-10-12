"""The hub hall as one mesh, matched to chopper-3D-concept-2.0.jpg.

The hammer pieces stay in the hut for floors, walls and stairs (the player still collides with
them). This mesh is only the look: stone foundation, round logs, a closed shingle roof, gable
crosses, and a porch roof over the door.

Piece-metres, Z up, same export as the pieces. Under Hut's Cabin (scale 1.5) at identity:
Blender X = Unity X, Blender Z = Unity Y, Blender -Y = Unity +Z (the front, where the door is).

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

W, D = 6.0, 4.0
MAIN, EAVE, RIDGE = 2.0, 4.05, 6.75
OVER = 0.45
LOG_R = 0.115
SIDES = 8


def srgb(r, g, b):
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b))


LOG = srgb(156, 102, 56)
LOG_DARK = srgb(118, 74, 40)
LOG_LIGHT = srgb(176, 118, 66)
LOG_END = srgb(206, 160, 104)
STONE = srgb(128, 124, 118)
STONE_DARK = srgb(96, 94, 90)
PLANK = srgb(134, 92, 56)
PLANK_DARK = srgb(112, 74, 42)
THATCH = srgb(206, 156, 80)
THATCH_DARK = srgb(168, 122, 58)
THATCH_LIGHT = srgb(228, 186, 108)
FRAME = srgb(72, 44, 26)
POST = srgb(96, 58, 32)


class Mesh:
    def __init__(self):
        self.verts = []
        self.faces = []
        self.colors = []

    def add(self, verts, faces, color):
        base = len(self.verts)
        self.verts.extend(verts)
        for f in faces:
            self.faces.append(tuple(base + i for i in f))
            self.colors.append(color)

    def prism(self, a, b, radius, color, end=LOG_END, sides=SIDES):
        a, b = (a[0], a[1], a[2]), (b[0], b[1], b[2])
        axis = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
        length = math.sqrt(sum(c * c for c in axis)) or 1.0
        w = tuple(c / length for c in axis)
        hint = (0.0, 0.0, 1.0) if abs(w[2]) < 0.9 else (0.0, 1.0, 0.0)
        v = (hint[0] - w[0] * sum(hint[i] * w[i] for i in range(3)),
             hint[1] - w[1] * sum(hint[i] * w[i] for i in range(3)),
             hint[2] - w[2] * sum(hint[i] * w[i] for i in range(3)))
        vl = math.sqrt(sum(c * c for c in v)) or 1.0
        v = tuple(c / vl for c in v)
        u = (v[1] * w[2] - v[2] * w[1], v[2] * w[0] - v[0] * w[2], v[0] * w[1] - v[1] * w[0])
        ring0, ring1 = [], []
        for k in range(sides):
            ang = (k + 0.5) * 2 * math.pi / sides
            off = (u[0] * math.cos(ang) + v[0] * math.sin(ang),
                   u[1] * math.cos(ang) + v[1] * math.sin(ang),
                   u[2] * math.cos(ang) + v[2] * math.sin(ang))
            ring0.append(tuple(a[i] + radius * off[i] for i in range(3)))
            ring1.append(tuple(b[i] + radius * off[i] for i in range(3)))
        verts = ring0 + ring1
        faces = []
        shade = LOG_DARK if color == LOG else color
        side_colors = []
        for k in range(sides):
            n = (k + 1) % sides
            faces.append((k, n, sides + n, sides + k))
            # the underside of a log is darker, so the courses separate
            side_colors.append(shade if math.sin((k + 0.5) * 2 * math.pi / sides) < -0.2 else color)
        faces.append(tuple(range(sides - 1, -1, -1)))
        faces.append(tuple(range(sides, 2 * sides)))
        base = len(self.verts)
        self.verts.extend(verts)
        for i, f in enumerate(faces):
            self.faces.append(tuple(base + j for j in f))
            self.colors.append(end if i >= sides else side_colors[i])

    def box(self, x0, x1, y0, y1, z0, z1, color):
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        verts = [(x, y, z) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
        # 0-3 bottom (z0), 4-7 top. Within a layer: (x0,y0) (x1,y0) (x0,y1) (x1,y1) — wait
        # loop y then x: index = z*4 + y*2 + x
        # Outward faces: bottom, top, -Y, +Y, -X, +X.
        faces = [
            (0, 2, 3, 1), (4, 5, 7, 6),
            (0, 1, 5, 4), (2, 6, 7, 3),
            (0, 4, 6, 2), (1, 3, 7, 5),
        ]
        self.add(verts, faces, color)

    def quad_up(self, a, b, c, d, color, back=None):
        """One quad, flipped when the normal points down. back adds the other side."""
        ab = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
        ad = (d[0] - a[0], d[1] - a[1], d[2] - a[2])
        nz = ab[0] * ad[1] - ab[1] * ad[0]
        if nz < 0:
            self.add([a, b, c, d], [(0, 3, 2, 1)], color)
            if back is not None:
                self.add([a, b, c, d], [(0, 1, 2, 3)], back)
        else:
            self.add([a, b, c, d], [(0, 1, 2, 3)], color)
            if back is not None:
                self.add([a, b, c, d], [(0, 3, 2, 1)], back)


def shade_log(row):
    return (LOG_DARK, LOG, LOG_LIGHT, LOG, LOG_DARK)[row % 5]


def build():
    m = Mesh()
    # Stone foundation, two courses, a ring so the basement stays open.
    for course, z0, color in ((0, 1.58, STONE_DARK), (1, 1.88, STONE)):
        z1 = z0 + 0.30
        m.box(-W - 0.18, W + 0.18, -D - 0.22, -D + 0.16, z0, z1, color)
        m.box(-W - 0.18, W + 0.18, D - 0.16, D + 0.22, z0, z1, color)
        m.box(-W - 0.22, -W + 0.16, -D, D, z0, z1, color)
        m.box(W - 0.16, W + 0.22, -D, D, z0, z1, color)

    def log_run(x0, x1, y, z, color):
        m.prism((x0, y, z), (x1, y, z), LOG_R, color)

    def courses(z_of):
        return [2.28 + i * 0.23 for i in range(8)]

    zs = courses(None)
    # Front and back walls. Door and windows are gaps; a dark frame closes the hole.
    door = (0.15, 1.85, 2.12, 3.92)
    windows = [(-4.15, -3.15, 2.85, 3.55), (3.3, 4.3, 2.85, 3.55),
               (-2.2, -1.2, 2.85, 3.55), (2.4, 3.4, 2.85, 3.55)]

    def blocked(x, z, holes):
        for x0, x1, z0, z1 in holes:
            if x0 - 0.05 < x < x1 + 0.05 and z0 < z < z1:
                return True
        return False

    front_holes = [door] + windows[:2]
    back_holes = windows[2:]
    for i, z in enumerate(zs):
        color = shade_log(i)
        for x0, x1 in ((-W, -4.2), (-4.05, -3.25), (-3.05, 0.05), (1.95, 3.2), (4.4, W),
                       (-W, W)):
            pass
        # continuous logs, split only where a hole cuts them
        for y, holes in ((-D, front_holes), (D, back_holes)):
            x = -W
            while x < W - 0.05:
                if blocked(x + 0.05, z, holes):
                    x += 0.15
                    continue
                x1 = x
                while x1 < W and not blocked(x1 + 0.1, z, holes):
                    x1 += 0.15
                m.prism((x, y, z), (min(x1, W), y, z), LOG_R, color)
                x = x1 + 0.15
    # end walls, logs along Y, one window each
    end_windows = [(-1.1, -0.2, 2.85, 3.55)]
    for i, z in enumerate(zs):
        color = shade_log(i + 1)
        for x in (-W, W):
            y = -D + 0.2
            while y < D - 0.25:
                if blocked(0, z, end_windows) and abs(y) < 1.15:
                    y += 0.15
                    continue
                y1 = y
                while y1 < D - 0.2 and not (blocked(0, z, end_windows) and abs(y1) < 1.15):
                    y1 += 0.15
                m.prism((x, y, z), (x, min(y1, D - 0.2), z), LOG_R, color)
                y = y1 + 0.15
    # frames
    for x0, x1, z0, z1 in [door] + windows + [(-W, -W, -1.1, -0.2)] :
        pass
    def frame_rect(x0, x1, y, z0, z1):
        m.box(x0 - 0.06, x0, y - 0.08, y + 0.08, z0, z1, FRAME)
        m.box(x1, x1 + 0.06, y - 0.08, y + 0.08, z0, z1, FRAME)
        m.box(x0, x1, y - 0.08, y + 0.08, z1, z1 + 0.08, FRAME)
        m.box(x0, x1, y - 0.08, y + 0.08, z0 - 0.06, z0, FRAME)

    frame_rect(*door[:2], -D, door[2], door[3])
    for x0, x1, z0, z1 in windows:
        y = -D if x0 < 0 and x1 < 0 or x0 > 2 else D
        if x0 < -3 or (x0 > 3):
            y = -D
        else:
            y = D
        frame_rect(x0, x1, y, z0, z1)
    for x in (-W, W):
        m.box(x - 0.08, x + 0.08, -1.15, -1.05, 2.8, 3.6, FRAME)
        m.box(x - 0.08, x + 0.08, -0.15, -0.05, 2.8, 3.6, FRAME)
        m.box(x - 0.08, x + 0.08, -1.15, -0.05, 3.55, 3.65, FRAME)
        m.box(x - 0.08, x + 0.08, -1.15, -0.05, 2.75, 2.85, FRAME)

    # Roof: two slopes, shingles with a lip, closed over the gable ends.
    span_x0, span_x1 = -W - 0.35, W + 0.35
    eave_y = D + OVER
    rows, cols = 14, 18
    for side in (-1, 1):
        for row in range(rows):
            for col in range(cols):
                stagger = 0.5 if row % 2 else 0.0
                t0 = row / rows
                t1 = (row + 1) / rows
                u0 = (col + stagger) / cols
                u1 = (col + 1 + stagger) / cols
                if u1 > 1.02:
                    continue

                def point(t, u, lift=0.0):
                    x = span_x0 + (span_x1 - span_x0) * u
                    y = -side * eave_y * (1.0 - t)
                    z = EAVE + (RIDGE - EAVE) * t + lift
                    return (x, y, z)

                lip = 0.05 if row < rows - 1 else 0.0
                a = point(t0, u0, 0.02)
                b = point(t0, min(u1, 1.0), 0.02)
                c = point(t1, min(u1, 1.0), 0.02 + lip)
                d = point(t1, u0, 0.02 + lip)
                color = (THATCH, THATCH_DARK, THATCH_LIGHT)[(row + col) % 3]
                m.quad_up(a, b, c, d, color)
                e = point(t0, u0, 0.0)
                f = point(t0, min(u1, 1.0), 0.0)
                m.quad_up(e, f, b, a, THATCH_DARK)

    # Gable fill: short logs across each end, under the roof.
    for x, sign in ((-W + 0.02, -1), (W - 0.02, 1)):
        for i in range(16):
            z = EAVE + 0.06 + (RIDGE - EAVE - 0.2) * i / 15.0
            half = eave_y * (1.0 - (z - EAVE) / (RIDGE - EAVE)) * 0.96
            m.prism((x, -half, z), (x, half, z), 0.095, shade_log(i))
        # crossed beams, standing a little past the peak
        peak = (x + sign * 0.08, 0.0, RIDGE + 0.28)
        m.prism((x, -1.15, EAVE + 0.55), peak, 0.055, LOG_END)
        m.prism((x, 1.15, EAVE + 0.55), peak, 0.055, LOG_END)

    # Porch: deck in front of the door, two posts, a small cross-gable.
    deck_y0, deck_y1 = -D - 2.15, -D
    m.box(-0.15, 2.15, deck_y0, deck_y1, MAIN - 0.12, MAIN, PLANK)
    m.box(-0.15, 2.15, deck_y0, deck_y1, MAIN - 0.18, MAIN - 0.12, PLANK_DARK)
    for x, y in ((-0.05, deck_y0 + 0.12), (2.05, deck_y0 + 0.12)):
        m.prism((x, y, MAIN), (x, y, MAIN + 1.72), 0.09, POST)
    # porch roof, ridge running out from the wall
    porch_eave = MAIN + 1.72
    porch_ridge = porch_eave + 0.85
    for side in (-1, 1):
        for row in range(6):
            for col in range(5):
                t0, t1 = row / 6.0, (row + 1) / 6.0
                u0, u1 = col / 5.0, (col + 1) / 5.0

                def pp(t, u, lift=0.04):
                    y = deck_y1 + (deck_y0 - deck_y1) * t
                    x = 1.0 + side * 1.25 * (1.0 - u)
                    z = porch_eave + (porch_ridge - porch_eave) * u + lift
                    return (x, y, z)

                color = (THATCH_LIGHT, THATCH, THATCH_DARK)[(row + col) % 3]
                m.quad_up(pp(t0, u0), pp(t1, u0), pp(t1, u1), pp(t0, u1), color)
    # porch gable at the front, with the same crossed beams
    gy = deck_y0 + 0.05
    for i in range(6):
        z = porch_eave + 0.06 + (porch_ridge - porch_eave - 0.12) * i / 5.0
        half = 1.15 * (1.0 - i / 5.0)
        m.prism((1.0 - half, gy, z), (1.0 + half, gy, z), 0.055, shade_log(i))
    tip = (1.0, gy - 0.06, porch_ridge + 0.18)
    m.prism((1.0 - 0.7, gy, porch_eave + 0.25), tip, 0.045, LOG_END)
    m.prism((1.0 + 0.7, gy, porch_eave + 0.25), tip, 0.045, LOG_END)
    # four treads from the terrace (1.67) up to the deck
    for i in range(4):
        z0 = 1.67 + i * 0.08
        y1 = deck_y0 - 0.12 - (3 - i) * 0.42
        m.box(0.15, 1.85, y1 - 0.4, y1, z0, z0 + 0.08, PLANK if i % 2 == 0 else PLANK_DARK)
    return m


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    built = build()
    # Blender +Y lands as Unity +Z with this project's axis bake. The front (door, porch) is +Y.
    built.verts = [(x, -y, z) for x, y, z in built.verts]
    built.faces = [tuple(reversed(f)) for f in built.faces]
    mesh = bpy.data.meshes.new("hall_voxel")
    mesh.from_pydata(built.verts, [], built.faces)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for poly, color in zip(mesh.polygons, built.colors):
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
    xs = [v[0] for v in built.verts]
    ys = [v[1] for v in built.verts]
    zs = [v[2] for v in built.verts]
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
    # Front is -Y, the roof ridge is the highest point, the foundation is the lowest.
    ok = tris > 1000 and max(zs) > 6.5 and min(zs) > 1.5 and max(ys) > 5.5 and max(xs) > 6.0
    lines = [
        "Hub hall",
        "=" * 60,
        "  tris %d" % tris,
        "  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)),
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


if __name__ == "__main__":
    main()
