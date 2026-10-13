"""World kit in the voxel style of concept 1: hub longhouse, torch post, grass tufts, bushes, clouds, sun,
path stones, mountain chunks, a backdrop mountain and the terrain block reference set. Same pattern as
build_pine.py and build_meadow.py.

Run:  blender -b -P build_world_kit.py
Out:  Art/Blender/world_kit.blend
      Art/Export/<prop>.fbx (one per prop)
      Art/Export/prop_grass_tuft_voxel_<a|b|c>_carpet.fbx (same tufts, vertex colour re-coded for Chopper/Grass)
      Art/Export/terrain_palette.txt (exact face colours of terrain_blocks_voxel, for the Unity terrain mesher)
      Art/Renders/world_kit_check_report.txt, world_kit_<prop>.png, world_kit_lineup*.png

1 unit = 1 m. Every prop sits on Z = 0 with its origin at the world origin. Vertex colour only, one shared
material M_World. No images in the file. Shared faces between cells are omitted.

Cell sizes: 0.25 m cubes (house, bushes, terrain block sub-cells), 0.125 m (torch, so the iron basket is not
0.75 m wide), 0.0625 m columns (grass blades), 0.25 x 0.25 x 0.125 (path slabs), 1 m (mountains, sun), 2 m (clouds).

Facing: the longhouse door faces -Y, the way the character faces, so it lands where the character's front
lands in Unity (+Z there).

Longhouse: drawn from concept 1 (log walls, steep golden thatch with the ridge along the length, crossed beam
ends on both gables, the door on the long side under a cross gable porch). The old hut
(Assets/_Chopper/Scripts/Editor/MeadowBuilder.cs BuildHouse, WorldGen.HutHalfWidth 4.5 / HutHalfDepth 6:
9 x 12 m on the wall centre lines, 2.8 m walls, 1.5 x 2.25 m doorway) was used as a size guide only: the new
one is 12 x 9 m on the wall centre lines (turned so the long side carries the door), walls 2.75 m.
"""
import math
import os

import bpy
from mathutils import Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
BLEND = os.path.join(ART, "Blender", "world_kit.blend")
EXPORT = os.path.join(ART, "Export")
RENDERS = os.path.join(ART, "Renders")
REPORT = os.path.join(RENDERS, "world_kit_check_report.txt")
PALETTE = os.path.join(EXPORT, "terrain_palette.txt")
CHARACTER = os.path.join(ART, "Export", "chopper.fbx")

S = 0.25


def srgb(r, g, b):
    """8-bit sRGB to the linear floats that attr.color takes."""
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b))


# colours, written as 8-bit sRGB
LOG = srgb(150, 96, 52)
LOG_DARK = srgb(118, 72, 38)
LOG_END = srgb(196, 150, 96)
FRAME = srgb(82, 50, 28)
PLANK = srgb(134, 92, 56)
THATCH = srgb(214, 168, 84)
THATCH_LIGHT = srgb(232, 192, 108)
THATCH_DARK = srgb(170, 124, 56)
STONE = srgb(128, 126, 120)
STONE_DARK = srgb(98, 96, 92)
STONE_LIGHT = srgb(160, 158, 152)
WINDOW = srgb(36, 26, 20)
IRON = srgb(58, 58, 62)
IRON_LIGHT = srgb(92, 92, 98)
EMBER = srgb(242, 120, 30)
EMBER_HOT = srgb(255, 200, 80)
ROPE = srgb(190, 160, 110)
GRASS_DARK = srgb(62, 102, 34)
GRASS_MID = srgb(88, 132, 44)
GRASS_LIGHT = srgb(112, 156, 56)
GRASS_TIP = srgb(214, 204, 96)
LEAF = srgb(70, 120, 48)
LEAF_DARK = srgb(52, 96, 38)
LEAF_LIGHT = srgb(96, 146, 64)
FLOWER = srgb(232, 214, 120)
FLOWER_CENTER = srgb(214, 150, 48)
CLOUD = srgb(250, 250, 252)
CLOUD_SHADE = srgb(214, 220, 230)
SUN = srgb(255, 232, 140)
SUN_CORE = srgb(255, 246, 196)
SNOW = srgb(240, 244, 250)
SNOW_SHADE = srgb(214, 222, 234)
ROCK = srgb(146, 148, 152)
ROCK_DARK = srgb(116, 118, 124)
ROCK_LIGHT = srgb(172, 174, 178)
FAR_ROCK = srgb(156, 164, 176)
FAR_ROCK_DARK = srgb(128, 136, 150)

TORCH_FLAME_ANCHOR_Z = 2.125  # top of the ember bed inside the basket; an empty of this name is in the .blend

FACE = {
    (1, 0, 0): ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)),
    (-1, 0, 0): ((0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)),
    (0, 1, 0): ((1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)),
    (0, -1, 0): ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),
    (0, 0, 1): ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)),
    (0, 0, -1): ((0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)),
}


def hsh(*v):
    h = 2166136261
    for x in v:
        h = ((h ^ (x & 0xFFFFFFFF)) * 16777619) & 0xFFFFFFFF
    return h


def disc(r, bites=()):
    cells = []
    for ix in range(-r, r + 1):
        for iy in range(-r, r + 1):
            if ix * ix + iy * iy > r * r + r * 0.6:
                continue
            if (ix, iy) in bites:
                continue
            cells.append((ix, iy))
    return cells


# ----------------------------------------------------------------------------------------------- longhouse
# Long axis X. Walls: x cells -25,-24 and 23,24 (x = -6.25..-5.75, 5.75..6.25, centre lines +-6);
# y cells -19,-18 (front, door) and 17,18 (back) (y = -4.75..-4.25, 4.25..4.75, centre lines +-4.5).
# z = 0 is the stone plinth and the plank floor (floor top 0.25 m); walls up to z cell 10 (2.75 m).
HX0, HX1 = -25, 24
HY0, HY1 = -19, 18
WALL_TOP = 10
DOOR_X = range(-3, 3)  # x -0.75..0.75, 1.5 m
DOOR_Z = range(1, 10)  # 0.25..2.5 m, 2.25 m clear above the floor
ROOF_X0, ROOF_X1 = -27, 26  # 0.5 m past the gables
ROOF_Y0, ROOF_Y1 = -21, 20  # 0.5 m past the eaves
PITCH = 0.9
# cross gable porch over the door
PX0, PX1 = -8, 7  # porch roof x cells (4 m)
PY0 = -30  # porch roof front (y = -7.5)
POST_Y = -28


def roof_base(iy):
    return WALL_TOP + int(min(iy - ROOF_Y0, ROOF_Y1 - iy) * PITCH)


def porch_base(ix):
    return WALL_TOP + min(ix - PX0, PX1 - ix)


def longhouse_cells():
    out = {}

    def log(ix, iy, iz):
        return LOG_DARK if iz % 2 else LOG

    # walls: z 0 is stone, logs above; alternate rows run past the corners (notched corners)
    for iz in range(0, WALL_TOP + 1):
        x_long = iz % 2 == 0 and iz > 0
        y_long = iz % 2 == 1
        for ix in range(HX0 - x_long, HX1 + 1 + x_long):
            for iy in (HY0, HY0 + 1, HY1 - 1, HY1):
                if iz == 0:
                    c = STONE_DARK if hsh(ix, iy) % 3 == 0 else STONE
                else:
                    c = LOG_END if ix < HX0 or ix > HX1 else log(ix, iy, iz)
                out[(ix, iy, iz)] = c
        for iy in range(HY0 - y_long, HY1 + 1 + y_long):
            for ix in (HX0, HX0 + 1, HX1 - 1, HX1):
                if iz == 0:
                    c = STONE_DARK if hsh(ix, iy) % 3 == 0 else STONE
                else:
                    c = LOG_END if iy < HY0 or iy > HY1 else log(ix, iy, iz)
                out[(ix, iy, iz)] = c
    # plank floor
    for ix in range(HX0 + 2, HX1 - 1):
        for iy in range(HY0 + 2, HY1 - 1):
            out[(ix, iy, 0)] = PLANK if ix % 4 else FRAME

    # long walls up to the roof, gable triangles at both ends
    for ix in range(HX0, HX1 + 1):
        for iy in (HY0, HY0 + 1, HY1 - 1, HY1):
            for iz in range(WALL_TOP + 1, roof_base(iy)):
                out[(ix, iy, iz)] = log(ix, iy, iz)
    for iy in range(HY0, HY1 + 1):
        for ix in (HX0, HX0 + 1, HX1 - 1, HX1):
            for iz in range(WALL_TOP + 1, roof_base(iy)):
                out[(ix, iy, iz)] = log(ix, iy, iz)

    # main thatch: two cells thick, stepped, ridge along X, ridge cap on top
    top_base = max(roof_base(j) for j in range(ROOF_Y0, ROOF_Y1 + 1))
    ridge_rows = [iy for iy in range(ROOF_Y0, ROOF_Y1 + 1) if roof_base(iy) == top_base]
    for iy in range(ROOF_Y0, ROOF_Y1 + 1):
        z0 = roof_base(iy)
        for ix in range(ROOF_X0, ROOF_X1 + 1):
            for k, iz in enumerate((z0, z0 + 1)):
                if k == 0 and (ix in (ROOF_X0, ROOF_X1) or iy in (ROOF_Y0, ROOF_Y1)):
                    c = THATCH_DARK
                else:
                    n = hsh(ix, iy, iz) % 10
                    c = THATCH_LIGHT if n < 2 else (THATCH_DARK if n == 9 else THATCH)
                out[(ix, iy, iz)] = c
    for iy in ridge_rows:
        for ix in range(ROOF_X0, ROOF_X1 + 1):
            out[(ix, iy, roof_base(iy) + 2)] = THATCH_DARK

    def barge(place, lo, hi, base, pitch, past):
        """Two boards along a gable edge, each running `past` cells beyond the ridge, so they cross in an X.
        place(u, z, colour) sets a cell; u runs across the gable from lo to hi."""
        mid = (lo + hi) / 2.0
        for u in range(lo, int(mid) + past + 1):  # rising from the lo eave
            z = base + int((u - lo) * pitch) + 2
            place(u, z, FRAME)
            place(u, z - 1, FRAME)
        for u in range(int(math.ceil(mid)) - past, hi + 1):  # rising from the hi eave
            z = base + int((hi - u) * pitch) + 2
            place(u, z, FRAME)
            place(u, z - 1, FRAME)
        u = int(mid) + past + 1
        place(u, base + int((u - lo) * pitch) + 2, LOG_END)
        u = int(math.ceil(mid)) - past - 1
        place(u, base + int((hi - u) * pitch) + 2, LOG_END)

    for ix in (ROOF_X0 - 1, ROOF_X1 + 1):
        def put(u, z, c, ix=ix):
            out[(ix, u, z)] = c
        barge(put, ROOF_Y0, ROOF_Y1, WALL_TOP, PITCH, 3)

    # windows: dark recess with a frame
    def window(cells_out, cells_in, frame_cells):
        for k in cells_out:
            out.pop(k, None)
        for k in cells_in:
            out[k] = WINDOW
        for k in frame_cells:
            out[k] = FRAME
    for x0 in (-18, -12, 10, 16):  # front, either side of the porch, and the back
        for iy_out, iy_in in ((HY0, HY0 + 1), (HY1, HY1 - 1)):
            if iy_out == HY0 and x0 in (-12, 10):
                continue
            window([(ix, iy_out, iz) for ix in range(x0, x0 + 3) for iz in (5, 6, 7)],
                   [(ix, iy_in, iz) for ix in range(x0, x0 + 3) for iz in (5, 6, 7)],
                   [(ix, iy_out, iz) for ix in range(x0 - 1, x0 + 4) for iz in (4, 8)])
    for ix_out, ix_in in ((HX0, HX0 + 1), (HX1, HX1 - 1)):
        window([(ix_out, iy, iz) for iy in range(-1, 2) for iz in (5, 6, 7)],
               [(ix_in, iy, iz) for iy in range(-1, 2) for iz in (5, 6, 7)],
               [(ix_out, iy, iz) for iy in range(-2, 3) for iz in (4, 8)])

    # cross gable porch: thatch running from the porch front back into the main roof
    for ix in range(PX0, PX1 + 1):
        zp = porch_base(ix)
        for iy in range(PY0, 0):
            if iy > HY0 + 1 and roof_base(iy) > zp + 1:
                break
            for k, iz in enumerate((zp, zp + 1)):
                if k == 0 and (iy == PY0 or ix in (PX0, PX1)):
                    c = THATCH_DARK
                else:
                    n = hsh(ix, iy, iz, 7) % 10
                    c = THATCH_LIGHT if n < 2 else (THATCH_DARK if n == 9 else THATCH)
                out[(ix, iy, iz)] = c
            if ix in (-1, 0):
                out[(ix, iy, zp + 2)] = THATCH_DARK
        # the main eave in front of the wall goes where the porch roof covers it
        for iy in range(ROOF_Y0, HY0 + 2):
            for iz in range(WALL_TOP, porch_base(ix)):
                if out.get((ix, iy, iz)) in (THATCH, THATCH_LIGHT, THATCH_DARK):
                    del out[(ix, iy, iz)]
        if PX0 < ix < PX1:
            # front wall carried up under the porch roof
            for iy in (HY0, HY0 + 1):
                for iz in range(WALL_TOP + 1, porch_base(ix)):
                    out[(ix, iy, iz)] = log(ix, iy, iz)
            # porch gable triangle over the post beam
            for iz in range(WALL_TOP, porch_base(ix)):
                out[(ix, POST_Y, iz)] = log(ix, POST_Y, iz) if iz > WALL_TOP else FRAME

    def put_p(u, z, c):
        out[(u, PY0 - 1, z)] = c
    barge(put_p, PX0, PX1, WALL_TOP, 1.0, 2)
    for ix in (PX0 + 1, PX1 - 1):  # posts on the deck
        for iz in range(1, WALL_TOP):
            out[(ix, POST_Y, iz)] = LOG
    for ix in range(PX0 + 1, PX1):  # beam over the posts
        out[(ix, POST_Y, WALL_TOP - 1)] = FRAME
    # stone deck under the porch
    for ix in range(PX0 + 1, PX1):
        for iy in range(POST_Y - 1, HY0):
            out.setdefault((ix, iy, 0), STONE_LIGHT if hsh(ix, iy, 3) % 3 == 0 else STONE)

    # doorway last, so nothing fills it back in; dark frame round it
    for ix in DOOR_X:
        for iz in DOOR_Z:
            for iy in (HY0, HY0 + 1):
                out.pop((ix, iy, iz), None)
    for iz in range(1, DOOR_Z[-1] + 2):
        for ix in (DOOR_X[0] - 1, DOOR_X[-1] + 1):
            out[(ix, HY0, iz)] = FRAME
    for ix in range(DOOR_X[0] - 1, DOOR_X[-1] + 2):
        out[(ix, HY0, DOOR_Z[-1] + 1)] = FRAME
    return out


# ----------------------------------------------------------------------------------------------- torch (0.125 m)
def torch_cells():
    out = {}
    for ix in (-1, 0):
        for iy in (-1, 0):
            for iz in range(0, 14):
                out[(ix, iy, iz)] = ROPE if iz in (11, 12) else (LOG_DARK if (ix + iy + iz) % 3 == 0 else LOG)
    for ix, iy in ((-2, -1), (-2, 0), (1, -1), (1, 0), (-1, -2), (0, -2), (-1, 1), (0, 1)):
        out[(ix, iy, 0)] = LOG_DARK
    for ix in range(-2, 2):
        for iy in range(-2, 2):
            out[(ix, iy, 14)] = IRON
    for iz in (15, 16):
        for ix in range(-2, 2):
            for iy in range(-2, 2):
                if ix in (-2, 1) or iy in (-2, 1):
                    out[(ix, iy, iz)] = IRON_LIGHT if iz == 16 and (ix + iy) % 2 else IRON
                else:
                    out[(ix, iy, iz)] = EMBER if iz == 15 else EMBER_HOT
    for ix, iy in ((-2, -2), (1, -2), (-2, 1), (1, 1)):
        out[(ix, iy, 17)] = IRON
    return out


# ----------------------------------------------------------------------------------------------- grass tufts
# Isolated blade columns on a 0.0625 m grid, no bottom faces. A blade is one box (10 tris) or a stem plus a
# yellow tip box (18 tris). A one-box blade can still carry a yellow top face (cap) for free.
# (ix, iy, width_cells, [(height_cells, colour), ...], cap colour or None)
GS = 0.0625
TUFTS = {
    "a": [
        (0, 0, 1, [(6, GRASS_MID), (2, GRASS_TIP)], None),
        (2, 1, 1, [(6, GRASS_DARK)], GRASS_TIP),
        (-2, 1, 1, [(5, GRASS_LIGHT)], None),
        (1, -2, 1, [(4, GRASS_MID)], GRASS_TIP),
        (-1, -1, 1, [(7, GRASS_DARK)], None),
    ],
    "b": [
        (0, 0, 1, [(5, GRASS_DARK)], GRASS_TIP),
        (2, -1, 1, [(4, GRASS_MID)], None),
        (-1, 2, 1, [(6, GRASS_LIGHT)], GRASS_TIP),
        (-3, -1, 1, [(4, GRASS_MID)], None),
        (1, 2, 1, [(5, GRASS_DARK)], None),
        (-1, -2, 1, [(7, GRASS_MID)], GRASS_TIP),
    ],
    "c": [
        (0, 0, 2, [(7, GRASS_MID), (2, GRASS_TIP)], None),
        (3, 1, 1, [(6, GRASS_DARK)], GRASS_TIP),
        (-2, 2, 1, [(5, GRASS_LIGHT)], None),
        (-1, -2, 1, [(4, GRASS_DARK)], GRASS_TIP),
    ],
}


def build_tuft(name, blades, mat, carpet=False):
    """carpet=True writes the Chopper/Grass channels instead of the look: R = height along the blade
    (0 root .. 1 at the tallest tip of the tuft), G = a fixed random per blade, B = 0."""
    verts, faces, info = [], [], []
    total_h = max(sum(h for h, _ in segs) for _, _, _, segs, _ in blades) * GS
    for bi, (ix, iy, w, segs, cap) in enumerate(blades):
        x0, y0 = ix * GS, iy * GS
        x1, y1 = x0 + w * GS, y0 + w * GS
        z = 0.0
        rnd = (bi * 0.618034 + 0.31) % 1.0
        for si, (h, color) in enumerate(segs):
            z1 = z + h * GS
            top = si == len(segs) - 1
            base = len(verts)
            verts.extend([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z),
                          (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)])
            quads = [((0, 1, 5, 4), color), ((1, 2, 6, 5), color), ((2, 3, 7, 6), color), ((3, 0, 4, 7), color)]
            if top:
                quads.append(((4, 5, 6, 7), cap or color))
            for q, c in quads:
                faces.append(tuple(base + i for i in q))
                info.append((c, rnd))
            z = z1
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for poly, (color, rnd) in zip(mesh.polygons, info):
        poly.use_smooth = False
        for li in poly.loop_indices:
            if carpet:
                vz = mesh.vertices[mesh.loops[li].vertex_index].co.z
                attr.data[li].color_srgb = (vz / total_h, rnd, 0.0, 1.0)  # raw channel values, no colour transform
            else:
                attr.data[li].color = (color[0], color[1], color[2], 1.0)
    mesh.color_attributes.active_color = attr
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(mat)
    return obj


# ----------------------------------------------------------------------------------------------- healing herb
# A short leafy plant with a yellow flower, so it reads apart from the grass tufts and the old red bush.
def herb_cells():
    out = {}
    for ix, iy in ((0, 0), (1, -1), (-1, 1)):
        for iz in range(0, 3):
            out[(ix, iy, iz)] = GRASS_DARK if iz == 0 else GRASS_MID
    for ix, iy, iz in ((1, 0, 1), (-1, 0, 2), (0, 1, 1), (0, -1, 2), (2, -1, 1), (-1, -1, 1)):
        out[(ix, iy, iz)] = LEAF_LIGHT if (ix + iy) % 2 == 0 else LEAF
    out[(0, 0, 3)] = FLOWER_CENTER
    out[(0, 0, 4)] = FLOWER
    out[(1, -1, 3)] = FLOWER
    out[(-1, 1, 3)] = FLOWER
    return out


# ----------------------------------------------------------------------------------------------- bushes
def bush_cells(variant):
    out = {}
    if variant == "a":
        layers = ((0, 2, ((2, 2), (-2, -2))), (1, 3, ((3, 1), (-3, -1), (1, -3))), (2, 3, ((3, -1), (-2, 3), (2, 2))),
                  (3, 2, ((2, 1), (-2, -1))))
        for iz, r, bites in layers:
            for ix, iy in disc(r, bites):
                out[(ix, iy, iz)] = None
        for ix, iy in ((0, 0), (1, 0), (0, -1)):
            out[(ix, iy, 4)] = None
    else:
        for cx, cy, r, h in ((-1, 0, 3, 3), (2, 1, 2, 4), (0, -1, 2, 2)):
            for iz in range(h):
                rr = r if iz < h - 1 else max(1, r - 1)
                for ix, iy in disc(rr):
                    out[(cx + ix, cy + iy, iz)] = None
    for key in list(out):
        ix, iy, iz = key
        n = hsh(ix, iy, iz) % 7
        if (ix, iy, iz + 1) not in out:
            out[key] = LEAF_LIGHT if n in (0, 3) else LEAF
        else:
            out[key] = LEAF_DARK if n in (1, 4, 6) else LEAF
    return out


# ----------------------------------------------------------------------------------------------- clouds and sun
def cloud_cells(variant):
    shapes = {
        "a": [(0, (-3, 3, -1, 1)), (0, (-2, 2, -2, 2)), (1, (-1, 1, -1, 0))],
        "b": [(0, (-4, 2, -1, 1)), (0, (-2, 3, 0, 2)), (1, (-2, 0, 0, 1)), (1, (1, 2, 1, 1))],
        "c": [(0, (-2, 2, -1, 0)), (0, (-1, 1, -2, 1)), (1, (0, 1, -1, 0))],
    }
    out = {}
    for iz, (xa, xb, ya, yb) in shapes[variant]:
        for ix in range(xa, xb + 1):
            for iy in range(ya, yb + 1):
                out[(ix, iy, iz)] = CLOUD
    return out


def sun_cells():
    """A flat disc standing in the X-Z plane (faces -Y), 1 m cells, base on Z = 0."""
    out = {}
    r = 4
    for ix in range(-r, r + 1):
        for iz in range(-r, r + 1):
            if ix * ix + iz * iz > r * r + 2:
                continue
            out[(ix, 0, iz + r)] = SUN_CORE if ix * ix + iz * iz <= 5 else SUN
    return out


# ----------------------------------------------------------------------------------------------- path stones
def path_stone_cells():
    """A short run of separate flagstones (concept 2.0): grass gaps, chipped corners, two of them a step higher.
    Cells are 0.25 x 0.25 x 0.125. Light tops, darker sides."""
    out = {}
    # xa, ya, xb, yb, height in cells. Gaps on X so the slabs do not fuse into one pad.
    slabs = (
        (-6, -1, -5, 1, 1),
        (-3, -2, -2, 0, 2),
        (0, -1, 1, 1, 1),
        (3, 0, 4, 2, 1),
        (6, -1, 6, 1, 2),
    )
    for si, (xa, ya, xb, yb, h) in enumerate(slabs):
        for ix in range(xa, xb + 1):
            for iy in range(ya, yb + 1):
                if (ix in (xa, xb)) and (iy in (ya, yb)) and (ix + iy) % 2 == 0:
                    continue
                for iz in range(h):
                    out[(ix, iy, iz)] = si
    tops = (
        (214 / 255, 210 / 255, 198 / 255),
        (204 / 255, 200 / 255, 188 / 255),
        (222 / 255, 218 / 255, 206 / 255),
        (196 / 255, 192 / 255, 180 / 255),
        (208 / 255, 204 / 255, 192 / 255),
    )
    side = (150 / 255, 146 / 255, 136 / 255)
    side_dark = (118 / 255, 114 / 255, 106 / 255)

    def color(ix, iy, iz, d, si):
        if d == (0, 0, 1):
            return tops[si] if hsh(ix, iy, si) % 4 else (188 / 255, 184 / 255, 172 / 255)
        return side_dark if iz == 0 else side
    return {k: (lambda ix, iy, iz, d, si=si: color(ix, iy, iz, d, si)) for k, si in out.items()}


# ----------------------------------------------------------------------------------------------- mountains (1 m)
def rock_face(ix, iy, iz, d, snow_from):
    """Stone with a little per-block variation, snow on exposed tops from a height on, and on the sides of the
    highest blocks."""
    n = hsh(ix, iy, iz) % 6
    if d == (0, 0, 1) and iz >= snow_from:
        return SNOW if n else SNOW_SHADE
    if iz >= snow_from + 2:
        return SNOW_SHADE if n < 4 else ROCK_LIGHT
    return ROCK_DARK if n == 0 else (ROCK_LIGHT if n == 5 else ROCK)


def mountain_chunk_cells(variant):
    """8 x 8 m chunks (x and y cells -4..3) that tile side by side and stack:
    a  'foot'    up to 4 m: full 8x8 bottom layer, stepping down toward -Y (the front edge of a range)
    b  'block'   4 m: a full 8x8x4 slab with notched sides and a full flat top, so any chunk stacks on it
    c  'peak'    8 m: full 8x8 bottom layer rising to a snow-capped peak
    """
    occ = {}
    for ix in range(-4, 4):
        for iy in range(-4, 4):
            if variant == "a":
                h = 1 + min(3, (iy + 4) // 2 + (1 if hsh(ix, iy) % 5 == 0 else 0))
            elif variant == "b":
                h = 4
            else:
                d = max(abs(ix + 0.5), abs(iy + 0.5))
                h = max(1, 8 - int(d * 1.6) - (hsh(ix, iy) % 2))
            for iz in range(h):
                occ[(ix, iy, iz)] = None
    if variant == "b":  # notches in the side faces, never at the corners, the bottom or the top layer
        for (ix, iy, iz) in list(occ):
            edge = ix in (-4, 3) or iy in (-4, 3)
            corner = ix in (-4, 3) and iy in (-4, 3)
            if edge and not corner and 1 <= iz <= 2 and hsh(ix, iy, iz, 5) % 4 == 0:
                del occ[(ix, iy, iz)]
    snow_from = {"a": 9, "b": 3, "c": 4}[variant]
    return {k: (lambda ix, iy, iz, d, s=snow_from: rock_face(ix, iy, iz, d, s)) for k in occ}


def mountain_backdrop_cells():
    """A distant range, 64 x 36 m footprint, two main peaks of about 36 and 28 m, stepped and jagged, falling to
    the ground inside the footprint on every side."""
    occ = {}
    peaks = ((-10, 0, 36.0, 1.9), (14, 2, 28.0, 1.9), (-26, -3, 15.0, 1.6), (26, 4, 14.0, 1.6))
    for ix in range(-32, 32):
        for iy in range(-18, 18):
            h = 0.0
            for px, py, ph, fall in peaks:
                dist = math.hypot(ix - px + 0.5, (iy - py + 0.5) * 1.15)
                h = max(h, ph - dist * fall)
            h = int(h - hsh(ix, iy) % 3 - (2 if hsh(ix // 3, iy // 3, 9) % 4 == 0 else 0))
            for iz in range(max(0, h)):
                occ[(ix, iy, iz)] = None

    def color(ix, iy, iz, d):
        n = hsh(ix, iy, iz) % 6
        if iz >= 22 or (iz >= 15 and d == (0, 0, 1)):
            return SNOW if n else SNOW_SHADE
        return FAR_ROCK_DARK if n < 2 else FAR_ROCK
    return {k: color for k in occ}


# ----------------------------------------------------------------------------------------------- terrain blocks
# 1 m sample blocks of 4x4x4 sub-cells (0.25 m). Each face of a block uses a three-shade palette; the shade of a
# 0.25 m sub-face is picked by hsh(sub-cell, face) % 8: 0-1 dark, 2-5 base, 6-7 light. All values 8-bit sRGB.
TERRAIN = {
    "grass": {
        "top": ((76, 118, 40), (92, 140, 48), (110, 160, 58)),
        "side_band": ((76, 118, 40), (92, 140, 48), (110, 160, 58)),
        "side": ((104, 70, 42), (128, 86, 52), (148, 104, 64)),
        "bottom": ((104, 70, 42), (128, 86, 52), (148, 104, 64)),
    },
    "dirt": {
        "top": ((110, 74, 44), (134, 92, 56), (156, 110, 68)),
        "side": ((104, 70, 42), (128, 86, 52), (148, 104, 64)),
        "bottom": ((104, 70, 42), (128, 86, 52), (148, 104, 64)),
    },
    "stone": {
        "top": ((108, 108, 106), (130, 130, 126), (152, 152, 148)),
        "side": ((96, 96, 94), (120, 120, 116), (142, 142, 138)),
        "bottom": ((96, 96, 94), (120, 120, 116), (142, 142, 138)),
    },
    "sand": {
        "top": ((198, 178, 124), (216, 198, 142), (230, 214, 164)),
        "side": ((188, 166, 112), (206, 186, 130), (222, 204, 152)),
        "bottom": ((188, 166, 112), (206, 186, 130), (222, 204, 152)),
    },
    "snow": {
        "top": ((222, 228, 238), (238, 242, 248), (250, 252, 255)),
        "side": ((206, 214, 228), (226, 232, 242), (242, 246, 252)),
        "bottom": ((206, 214, 228), (226, 232, 242), (242, 246, 252)),
    },
    "water": {
        "top": ((52, 104, 168), (66, 124, 188), (96, 154, 210)),
        "side": ((40, 84, 144), (52, 104, 168), (66, 124, 188)),
        "bottom": ((40, 84, 144), (52, 104, 168), (66, 124, 188)),
    },
}
TERRAIN_ORDER = ("grass", "dirt", "stone", "sand", "snow", "water")


def shade_index(ix, iy, iz, d):
    n = hsh(ix, iy, iz, d[0] + 2, d[1] + 4, d[2] + 8) % 8
    return 0 if n < 2 else (1 if n < 6 else 2)


def terrain_blocks_cells():
    """The six sample blocks in a row along X, 1.5 m apart (grass at the -X end), each 1 m on a side."""
    out = {}
    for k, kind in enumerate(TERRAIN_ORDER):
        ox = int(round(((k - 2.5) * 1.5 - 0.5) / S))
        pal = TERRAIN[kind]

        def color(ix, iy, iz, d, pal=pal, ox=ox):
            if d == (0, 0, 1):
                key = "top"
            elif d == (0, 0, -1):
                key = "bottom"
            elif iz == 3 and "side_band" in pal:
                key = "side_band"
            else:
                key = "side"
            return srgb(*pal[key][shade_index(ix - ox, iy, iz, d)])
        for ix in range(4):
            for iy in range(-2, 2):
                for iz in range(4):
                    out[(ox + ix, iy, iz)] = color
    return out


def write_palette():
    lines = [
        "Chopper terrain palette (terrain_blocks_voxel, build_world_kit.py)",
        "=" * 70,
        "Blocks are 1 m, each face drawn as 4 x 4 sub-faces of 0.25 m. Each face has three shades.",
        "Shade per 0.25 m sub-face: n = FNV-1a(sub-cell x, y, z, face) % 8 -> 0-1 dark, 2-5 base, 6-7 light",
        "(25% dark, 50% base, 25% light; any even noise with those shares gives the same look).",
        "Values are 8-bit sRGB as stored in the FBX vertex colours; linear floats in brackets.",
        "Grass sides: the top 0.25 m row of every side face uses side_band (the green lip), the rest uses side.",
        "",
    ]
    for kind in TERRAIN_ORDER:
        lines.append("[%s]" % kind)
        for face, shades in TERRAIN[kind].items():
            parts = []
            for name, c in zip(("dark", "base", "light"), shades):
                lin = srgb(*c)
                parts.append("%s %3d %3d %3d (%.4f %.4f %.4f)" % (name, c[0], c[1], c[2], lin[0], lin[1], lin[2]))
            lines.append("  %-9s %s" % (face, " | ".join(parts)))
        lines.append("")
    os.makedirs(EXPORT, exist_ok=True)
    with open(PALETTE, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))
    print("wrote", PALETTE)


# ----------------------------------------------------------------------------------------------- mesh building
def build(name, occ, mat, size=(S, S, S), shade_bottom=None):
    """occ maps cell -> colour tuple, or a callable(ix, iy, iz, face_dir) giving the colour of that face."""
    mesh = bpy.data.meshes.new(name)
    verts, faces, colors = [], [], []
    index = {}
    sx, sy, sz = size

    def vert(ix, iy, iz, corner):
        key = (ix + corner[0], iy + corner[1], iz + corner[2])
        if key in index:
            return index[key]
        index[key] = len(verts)
        verts.append((key[0] * sx, key[1] * sy, key[2] * sz))
        return index[key]

    for (ix, iy, iz), color in occ.items():
        for d, corners in FACE.items():
            if (ix + d[0], iy + d[1], iz + d[2]) in occ:
                continue
            faces.append(tuple(vert(ix, iy, iz, c) for c in corners))
            c = color(ix, iy, iz, d) if callable(color) else color
            colors.append(shade_bottom if (shade_bottom and d[2] == -1) else c)
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


def tris_of(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


# ----------------------------------------------------------------------------------------------- check
def check(specs, extra):
    lines = ["World kit check", "=" * 60]
    ok = True

    def add(label, passed, detail=""):
        nonlocal ok
        ok = ok and passed
        lines.append("  [%s] %s%s" % ("PASS" if passed else "FAIL", label, (" - " + detail) if detail else ""))

    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    add("%d meshes" % len(specs), len(meshes) == len(specs), "found %d" % len(meshes))
    imgs = [im.name for im in bpy.data.images if im.name not in ("Render Result", "Viewer Node")]
    add("no images in the file", not imgs, ", ".join(imgs))
    mats = sorted({m.name for o in meshes for m in o.data.materials if m})
    add("one shared material M_World", mats == ["M_World"], ", ".join(mats))
    for obj, rule in specs:
        me = obj.data
        tris = tris_of(obj)
        x0, x1, y0, y1, z0, z1 = bounds(obj)
        loc, rot, scl = obj.location, obj.rotation_euler, obj.scale
        n = obj.name
        add("%s transforms applied" % n, loc.length < 1e-6 and Vector(rot).length < 1e-6 and (scl - Vector((1, 1, 1))).length < 1e-6)
        add("%s base on Z=0" % n, abs(z0) < 1e-6, "min z %.4f" % z0)
        h = z1 - z0
        add("%s height %.3f m in [%.2f, %.2f]" % (n, h, rule["h"][0], rule["h"][1]), rule["h"][0] - 1e-6 <= h <= rule["h"][1] + 1e-6)
        if "w" in rule:
            add("%s width x %.3f m in [%.2f, %.2f]" % (n, x1 - x0, rule["w"][0], rule["w"][1]), rule["w"][0] - 1e-6 <= x1 - x0 <= rule["w"][1] + 1e-6)
        if "d" in rule:
            add("%s depth y %.3f m in [%.2f, %.2f]" % (n, y1 - y0, rule["d"][0], rule["d"][1]), rule["d"][0] - 1e-6 <= y1 - y0 <= rule["d"][1] + 1e-6)
        add("%s flat shaded" % n, not any(p.use_smooth for p in me.polygons))
        add("%s has vertex colour Col" % n, me.color_attributes.get("Col") is not None)
        add("%s triangles %d <= %d" % (n, tris, rule["tris"]), tris <= rule["tris"])
    for label, passed, detail in extra:
        add(label, passed, detail)
    lines.append("SUMMARY: %s" % ("ALL PASS" if ok else "FAILURES"))
    text = "\n".join(lines) + "\n"
    os.makedirs(RENDERS, exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    if not ok:
        raise SystemExit(1)


def house_extra(house):
    """Footprint of the walls and the doorway, measured on the mesh itself (below the eaves and the porch roof)."""
    out = []
    me = house.data
    xs = [v.co.x for v in me.vertices if 0.3 < v.co.z < 2.45 and abs(v.co.y) < 4.0]
    ys = [v.co.y for v in me.vertices if 0.3 < v.co.z < 2.45 and abs(v.co.x) < 5.5 and abs(v.co.y) < 5.0]
    out.append(("longhouse wall outer faces x %.2f..%.2f (12 m on the centre lines)" % (min(xs), max(xs)),
                abs(min(xs) + 6.25) < 1e-4 and abs(max(xs) - 6.25) < 1e-4, ""))
    out.append(("longhouse wall outer faces y %.2f..%.2f (9 m on the centre lines)" % (min(ys), max(ys)),
                abs(min(ys) + 4.75) < 1e-4 and abs(max(ys) - 4.75) < 1e-4, ""))
    dg = bpy.context.evaluated_depsgraph_get()

    def ray(x, z):
        return house.ray_cast(Vector((x, -5.1, z)), Vector((0, 1, 0)), distance=0.8, depsgraph=dg)[0]
    clear = not any(ray(x, z) for x in (-0.6, 0.0, 0.6) for z in (0.4, 1.2, 2.1, 2.45))
    out.append(("longhouse doorway 1.5 m wide, open 0.25..2.5 m, in the front wall (-Y) at x = 0", clear, ""))
    out.append(("longhouse lintel above the doorway", ray(0.0, 2.6), ""))
    out.append(("longhouse wall beside the doorway is solid", ray(-1.5, 1.0), ""))
    floor = house.ray_cast(Vector((0, 0, 1.0)), Vector((0, 0, -1)), distance=2.0, depsgraph=dg)
    out.append(("longhouse floor top at 0.25 m", floor[0] and abs(floor[1].z - 0.25) < 1e-4, ""))
    return out


# ----------------------------------------------------------------------------------------------- render
def setup_render():
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.image_settings.file_format = "PNG"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "VERTEX"
    sh.show_shadows = False
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    sc.world.color = (0.62, 0.74, 0.88)
    data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    data.type = "ORTHO"
    data.clip_end = 3000
    return cam


def shoot(cam, name, target, ortho, yaw_deg=-35, pitch_deg=20, res=(1280, 720), dist=400):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    cam.data.ortho_scale = ortho
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    look = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    cam.location = Vector(target) - look * dist
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(RENDERS, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", sc.render.filepath)


def only(objs, keep):
    for ob in objs:
        ob.hide_render = ob not in keep


def render_each(cam, objs):
    for ob in objs:
        only(objs, [ob])
        x0, x1, y0, y1, z0, z1 = bounds(ob)
        ortho = max(max(x1 - x0, y1 - y0) * 1.35, (z1 - z0) * 2.4)
        target = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
        if ob.name == "prop_sun_voxel":
            shoot(cam, "world_kit_" + ob.name, target, ortho, yaw_deg=-15, pitch_deg=5)
        else:
            shoot(cam, "world_kit_" + ob.name, target, ortho)
    house = bpy.data.objects["prop_longhouse_voxel"]
    only(objs, [house])
    shoot(cam, "world_kit_prop_longhouse_voxel_back", (0, 0, 3.6), 21, yaw_deg=145, pitch_deg=22)
    shoot(cam, "world_kit_prop_longhouse_voxel_front", (0, -1, 3.6), 17, yaw_deg=0, pitch_deg=8)
    only(objs, objs)


def render_mountain_stack(cam, objs):
    """Copies of the three chunks put together as a small range, to show they tile and stack."""
    a, b, c = (bpy.data.objects["prop_mountain_chunk_voxel_" + v] for v in "abc")
    only(objs, [])
    temp = []
    layout = (
        (a, (-8, -8, 0)), (a, (0, -8, 0)), (a, (8, -8, 0)),
        (b, (-8, 0, 0)), (b, (0, 0, 0)), (b, (8, 0, 0)),
        (b, (-8, 8, 0)), (b, (0, 8, 0)), (b, (8, 8, 0)),
        (c, (-8, 0, 4)), (a, (0, 0, 4)), (b, (0, 8, 4)), (c, (0, 8, 8)), (c, (8, 8, 4)),
    )
    for src, at in layout:
        ob = bpy.data.objects.new(src.name + "_copy", src.data)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = at
        temp.append(ob)
    shoot(cam, "world_kit_mountain_chunks_stacked", (0, 2, 7), 38, yaw_deg=-30, pitch_deg=22)
    for ob in temp:
        bpy.data.objects.remove(ob, do_unlink=True)
    only(objs, objs)


def lineup(cam, objs):
    """Imports the character read-only, lays everything out, renders, then removes the character again."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=CHARACTER)
    char = [o for o in bpy.data.objects if o not in before]
    for o in char:  # the clips would drive the root and the pose; show the rest pose where we put it
        o.animation_data_clear()
    roots = [o for o in char if o.parent is None]
    saved = {ob.name: ob.location.copy() for ob in objs}
    place = {
        "prop_longhouse_voxel": (-6.0, 4.0, 0),
        "prop_torch_voxel": (-1.2, -6.2, 0),
        "prop_bush_voxel_a": (3.6, -3.6, 0),
        "prop_bush_voxel_b": (5.8, -2.6, 0),
        "prop_herb_voxel": (7.4, -3.2, 0),
        "prop_grass_tuft_voxel_a": (0.6, -6.6, 0),
        "prop_grass_tuft_voxel_b": (1.5, -7.0, 0),
        "prop_grass_tuft_voxel_c": (-0.2, -7.3, 0),
        "prop_path_stone_voxel": (-1.6, -2.4, 0),
        "terrain_blocks_voxel": (10.0, -5.0, 0),
        "prop_mountain_chunk_voxel_a": (22.0, 16.0, 0),
        "prop_mountain_chunk_voxel_b": (30.0, 16.0, 0),
        "prop_mountain_chunk_voxel_c": (30.0, 24.0, 0),
        "prop_mountain_backdrop_voxel": (0.0, 90.0, 0),
        "prop_cloud_voxel_a": (-14.0, 50.0, 26.0),
        "prop_cloud_voxel_b": (14.0, 56.0, 32.0),
        "prop_cloud_voxel_c": (30.0, 48.0, 22.0),
        "prop_sun_voxel": (-26.0, 70.0, 34.0),
    }
    for ob in objs:
        ob.location = place.get(ob.name, (0, 0, 0))
    for r in roots:
        r.location = (1.4, -5.6, 0)
    shoot(cam, "world_kit_lineup", (2.0, 30.0, 20.0), 104, yaw_deg=-12, pitch_deg=8, res=(1600, 900))
    shoot(cam, "world_kit_lineup_close", (1.5, -5.5, 1.0), 9, yaw_deg=-25, pitch_deg=14, res=(1600, 900))
    shoot(cam, "world_kit_terrain_blocks_close", (10.0, -5.0, 0.6), 10, yaw_deg=-30, pitch_deg=28, res=(1600, 900))
    shoot(cam, "world_kit_lineup_concept", (-2.0, 0.0, 3.5), 26, yaw_deg=-38, pitch_deg=9, res=(1600, 900))
    for ob in objs:
        ob.location = saved[ob.name]
    for o in char:
        bpy.data.objects.remove(o, do_unlink=True)


# ----------------------------------------------------------------------------------------------- export
def export_one(obj, path=None):
    os.makedirs(EXPORT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = path or os.path.join(EXPORT, obj.name + ".fbx")
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

    built, rules = [], []

    def add(obj, rule):
        built.append(obj)
        rules.append((obj, rule))
        b = bounds(obj)
        print("built %s tris %d size %.3f x %.3f x %.3f" % (obj.name, tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))

    add(build("prop_longhouse_voxel", longhouse_cells(), mat),
        {"h": (6.0, 9.5), "w": (13.0, 14.5), "d": (12.0, 14.0), "tris": 45000})
    torch = build("prop_torch_voxel", torch_cells(), mat, size=(0.125, 0.125, 0.125))
    add(torch, {"h": (2.15, 2.3), "w": (0.4, 0.6), "tris": 600})
    for v in ("a", "b", "c"):
        add(build_tuft("prop_grass_tuft_voxel_" + v, TUFTS[v], mat), {"h": (0.25, 0.6), "w": (0.1, 0.6), "tris": 60})
    for v in ("a", "b"):
        add(build("prop_bush_voxel_" + v, bush_cells(v), mat), {"h": (0.75, 1.3), "w": (1.2, 2.4), "tris": 1500})
    add(build("prop_herb_voxel", herb_cells(), mat, size=(0.125, 0.125, 0.125)),
        {"h": (0.5, 0.75), "w": (0.3, 0.8), "tris": 400})
    for v in ("a", "b", "c"):
        add(build("prop_cloud_voxel_" + v, cloud_cells(v), mat, size=(2.0, 2.0, 2.0), shade_bottom=CLOUD_SHADE),
            {"h": (2.0, 4.0), "w": (8.0, 16.0), "tris": 400})
    add(build("prop_sun_voxel", sun_cells(), mat, size=(1.0, 1.0, 1.0)), {"h": (9.0, 9.0), "w": (9.0, 9.0), "tris": 400})
    add(build("prop_path_stone_voxel", path_stone_cells(), mat, size=(S, S, 0.125)),
        {"h": (0.125, 0.25), "w": (2.5, 3.5), "tris": 600})
    for v, h in (("a", (2.0, 4.0)), ("b", (4.0, 4.0)), ("c", (6.0, 8.0))):
        add(build("prop_mountain_chunk_voxel_" + v, mountain_chunk_cells(v), mat, size=(1.0, 1.0, 1.0)),
            {"h": h, "w": (8.0, 8.0), "d": (8.0, 8.0), "tris": 2000})
    add(build("prop_mountain_backdrop_voxel", mountain_backdrop_cells(), mat, size=(1.0, 1.0, 1.0)),
        {"h": (30.0, 40.0), "w": (50.0, 64.0), "d": (25.0, 36.0), "tris": 30000})
    add(build("terrain_blocks_voxel", terrain_blocks_cells(), mat), {"h": (1.0, 1.0), "w": (8.0, 9.0), "tris": 1200})

    anchor = bpy.data.objects.new("prop_torch_voxel_FlameAnchor", None)
    anchor.empty_display_type = "SPHERE"
    anchor.empty_display_size = 0.1
    anchor.location = (0, 0, TORCH_FLAME_ANCHOR_Z)
    bpy.context.scene.collection.objects.link(anchor)
    anchor.parent = torch

    extra = house_extra(built[0])
    tz = bounds(torch)
    extra.append(("torch flame anchor %.3f m inside the basket (1.875..%.3f)" % (TORCH_FLAME_ANCHOR_Z, tz[5]),
                  1.875 <= TORCH_FLAME_ANCHOR_Z <= tz[5], ""))
    # chunks tile: the bottom layer fills the 8 x 8 footprint; block b's top layer fills it too (stackable)
    for v in "abc":
        ob = bpy.data.objects["prop_mountain_chunk_voxel_" + v]
        bottom = {(math.floor(p.center.x), math.floor(p.center.y)) for p in ob.data.polygons if p.normal.z < -0.5 and p.center.z < 1e-4}
        extra.append(("mountain chunk %s bottom fills 8x8 (%d cells)" % (v, len(bottom)), len(bottom) == 64, ""))
    ob = bpy.data.objects["prop_mountain_chunk_voxel_b"]
    top = {(math.floor(p.center.x), math.floor(p.center.y)) for p in ob.data.polygons if p.normal.z > 0.5 and abs(p.center.z - 4) < 1e-4}
    extra.append(("mountain chunk b top fills 8x8 at 4 m (%d cells)" % len(top), len(top) == 64, ""))
    check(rules, extra)
    write_palette()

    cam = setup_render()
    render_each(cam, built)
    render_mountain_stack(cam, built)

    bpy.context.preferences.filepaths.save_version = 0
    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("saved", BLEND)
    for ob in built:
        export_one(ob)
    for v in ("a", "b", "c"):
        ob = build_tuft("prop_grass_tuft_voxel_%s_carpet" % v, TUFTS[v], mat, carpet=True)
        export_one(ob)
        bpy.data.objects.remove(ob, do_unlink=True)

    if os.path.exists(CHARACTER):
        lineup(cam, built)
    else:
        print("no character FBX, lineup skipped")


def export_path_stone():
    """Build and export only the path stones. The rest of the kit (hut, bushes) stays as it is."""
    import sys
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    obj = build("prop_path_stone_voxel", path_stone_cells(), mat, size=(S, S, 0.125))
    x0, x1, y0, y1, z0, z1 = bounds(obj)
    tris = tris_of(obj)
    width = x1 - x0
    height = z1 - z0
    ok = abs(z0) < 1e-6 and 2.5 - 1e-6 <= width <= 3.5 + 1e-6 and 0.125 - 1e-6 <= height <= 0.25 + 1e-6 and tris <= 600
    # Separate slabs: more than one island of top faces.
    tops = [p for p in obj.data.polygons if p.normal.z > 0.5]
    islands = 0
    seen = set()
    for poly in tops:
        key = (round(poly.center.x * 4), round(poly.center.y * 4))
        if key in seen:
            continue
        # flood through 4-connected top cells
        stack = [key]
        seen.add(key)
        islands += 1
        while stack:
            cx, cy = stack.pop()
            for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                if (nx, ny) in seen:
                    continue
                if any(round(p.center.x * 4) == nx and round(p.center.y * 4) == ny for p in tops):
                    seen.add((nx, ny))
                    stack.append((nx, ny))
    ok = ok and islands >= 4
    lines = [
        "Path stone check",
        "=" * 60,
        "  [%s] base on Z=0 (%.4f)" % ("PASS" if abs(z0) < 1e-6 else "FAIL", z0),
        "  [%s] width %.3f m in [2.50, 3.50]" % ("PASS" if 2.5 - 1e-6 <= width <= 3.5 + 1e-6 else "FAIL", width),
        "  [%s] height %.3f m in [0.125, 0.250]" % ("PASS" if 0.125 - 1e-6 <= height <= 0.25 + 1e-6 else "FAIL", height),
        "  [%s] triangles %d <= 600" % ("PASS" if tris <= 600 else "FAIL", tris),
        "  [%s] %d separate slabs" % ("PASS" if islands >= 4 else "FAIL", islands),
        "SUMMARY: %s" % ("ALL PASS" if ok else "FAILURES"),
    ]
    text = "\n".join(lines) + "\n"
    print(text)
    os.makedirs(RENDERS, exist_ok=True)
    with open(os.path.join(RENDERS, "path_stone_check_report.txt"), "w", encoding="utf-8") as fh:
        fh.write(text)
    if not ok:
        raise SystemExit(1)
    cam = setup_render()
    if bpy.context.scene.world:
        bpy.context.scene.world.color = (0.78, 0.70, 0.56)
    shoot(cam, "path_stone", ((x0 + x1) * 0.5, (y0 + y1) * 0.5, height * 0.5), 4.2, yaw_deg=-40, pitch_deg=28, res=(1024, 1024), dist=12)
    export_one(obj)
    print("exported path stone only")
    sys.stdout.flush()


if __name__ == "__main__":
    import sys
    if "--path" in sys.argv:
        export_path_stone()
    else:
        main()
