"""UV-unwrap every Chopper mesh into one shared 2048 atlas and bake a base colour PNG.

Run:  blender -b -P uv_atlas.py
In:   Art/Blender/chopper_character.blend, chopper_props.blend
Out:  those files (one UVMap layer each, no image packed into the blend),
      Art/Textures/chopper_atlas.png, Art/Textures/atlas_tiles.txt

Each mesh occupies its own tile so islands from different objects cannot overlap.
Inside a tile, faces are packed on a grid (deterministic, no operator dependency).
The PNG is the gray-box vertex colours, then a light detail pass per tile
(plaid, stitching, chainmail, wood, steel). Geometry is unchanged.
"""
import math
import os

import bpy
import bmesh

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
CHAR = os.path.join(ART, "Blender", "chopper_character.blend")
PROPS = os.path.join(ART, "Blender", "chopper_props.blend")
TEX = os.path.join(ART, "Textures")
ATLAS = os.path.join(TEX, "chopper_atlas.png")
TILES_TXT = os.path.join(TEX, "atlas_tiles.txt")
SIZE = 2048

# (u0, v0, u1, v1) in 0-1. Margins between tiles are empty.
TILES = {
    "chopper_body": (0.01, 0.51, 0.49, 0.99),
    "chopper_shirt": (0.51, 0.67, 0.74, 0.99),
    "chopper_vest": (0.76, 0.67, 0.99, 0.99),
    "chopper_boots": (0.51, 0.51, 0.74, 0.65),
    "chopper_beard": (0.01, 0.26, 0.33, 0.49),
    "chopper_hair": (0.35, 0.26, 0.66, 0.49),
    "chopper_head": (0.68, 0.26, 0.99, 0.49),
    "chopper_chainmail": (0.01, 0.01, 0.33, 0.24),
    "chopper_belt": (0.35, 0.01, 0.54, 0.24),
    "chopper_helmet": (0.56, 0.01, 0.74, 0.24),
    "chopper_axe": (0.76, 0.51, 0.99, 0.65),
    "chopper_shield": (0.76, 0.01, 0.99, 0.24),
}

# Detail is painted in object space inside bake(), not as a tile overlay.



def unwrap(ob):
    me = ob.data
    tile = TILES[ob.name]
    u0, v0, u1, v1 = tile
    if "UVMap" in me.uv_layers:
        me.uv_layers.remove(me.uv_layers["UVMap"])
    uv_layer = me.uv_layers.new(name="UVMap")
    polys = list(me.polygons)
    n = max(1, len(polys))
    cols = max(1, math.ceil(math.sqrt(n)))
    rows = max(1, math.ceil(n / cols))
    du = (u1 - u0) / cols
    dv = (v1 - v0) / rows
    inset = 0.12
    for i, p in enumerate(polys):
        c, r = i % cols, i // cols
        a = u0 + c * du + du * inset
        b = v0 + r * dv + dv * inset
        c2 = u0 + (c + 1) * du - du * inset
        d2 = v0 + (r + 1) * dv - dv * inset
        center = p.center
        # order loops around the face so the quad stays non-overlapping
        loops = list(p.loop_indices)
        coords = []
        for li in loops:
            vi = me.loops[li].vertex_index
            co = me.vertices[vi].co
            coords.append((li, co.x - center.x, co.y - center.y, co.z - center.z))
        # project onto the face plane using the two largest axes of the normal
        nx, ny, nz = abs(p.normal.x), abs(p.normal.y), abs(p.normal.z)
        if nx >= ny and nx >= nz:
            ax, ay = 1, 2
        elif ny >= nz:
            ax, ay = 0, 2
        else:
            ax, ay = 0, 1
        pts = [(t[1 + ax], t[1 + ay], t[0]) for t in coords]
        xs = [p2[0] for p2 in pts]
        ys = [p2[1] for p2 in pts]
        minx, maxx = min(xs), max(xs)
        miny, maxy = min(ys), max(ys)
        sx = max(maxx - minx, 1e-8)
        sy = max(maxy - miny, 1e-8)
        for x, y, li in pts:
            u = a + (x - minx) / sx * (c2 - a)
            v = b + (y - miny) / sy * (d2 - b)
            uv_layer.data[li].uv = (u, v)
    me.uv_layers.active = uv_layer
    return n


def loop_color(me, li):
    attr = me.color_attributes.get("Col")
    if attr is None:
        return (0.6, 0.6, 0.6)
    c = attr.data[li].color
    return (c[0], c[1], c[2])


def bary(px, py, a, b, c):
    den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
    if abs(den) < 1e-12:
        return None
    w0 = ((b[1] - c[1]) * (px - c[0]) + (c[0] - b[0]) * (py - c[1])) / den
    w1 = ((c[1] - a[1]) * (px - c[0]) + (a[0] - c[0]) * (py - c[1])) / den
    w2 = 1.0 - w0 - w1
    if w0 < -0.001 or w1 < -0.001 or w2 < -0.001:
        return None
    return w0, w1, w2


def mix(rgb, tint, k):
    return tuple(rgb[i] * (1 - k) + tint[i] * k for i in range(3))


def dark(rgb, k):
    return tuple(c * k for c in rgb)


def detail(name, p, n, rgb):
    """Paint in object space so the colour lands on the face that owns this point.

    Vertex colours in the mesh are linear. Wood is redder than it is green; steel is gray.
    """
    x, y, z = p.x, p.y, p.z
    if name == "chopper_shirt":
        if int(x * 22) % 4 == 0 or int(z * 22) % 4 == 0:
            return dark(rgb, 0.45)
        if int(x * 22) % 4 == 2:
            return (min(1, rgb[0] * 1.05), rgb[1] * 0.55, rgb[2] * 0.4)
    elif name in ("chopper_vest", "chopper_belt", "chopper_boots"):
        if abs((z * 36) - round(z * 36)) < 0.08:
            return dark(rgb, 0.62)
        # a gray patch on the vest's front
        if name == "chopper_vest" and n.y < -0.4 and -0.06 < x < 0.08 and 1.05 < z < 1.18:
            return (0.55, 0.55, 0.55)
    elif name == "chopper_chainmail":
        if (int(x * 45) + int(z * 45)) % 2 == 0:
            return dark(rgb, 0.72)
    elif name == "chopper_head" and n.y < -0.35:
        eye = 1.54 < z < 1.62 and (0.045 < abs(x) < 0.11)
        brow = 1.63 < z < 1.68 and abs(x) < 0.14
        if eye:
            return (0.12, 0.42, 0.16)
        if brow:
            return dark(rgb, 0.35)
    elif name == "chopper_helmet" and n.y < -0.35 and abs(x) < 0.12 and 1.48 < z < 1.66:
        # skull sockets on the front plate
        socket = 1.56 < z < 1.64 and 0.03 < abs(x) < 0.08
        if socket:
            return (0.12, 0.12, 0.13)
    elif name == "chopper_axe":
        on_blade = abs(x) > 0.06 and z > 0.40 and abs(n.y) > 0.5
        if on_blade and (int(x * 40) + int(z * 40)) % 5 == 0:
            return dark(rgb, 0.55)
        if abs(x) < 0.03 and z < 0.15 and int(z * 30) % 3 == 0:
            return dark(rgb, 0.7)  # grip wraps
    elif name == "chopper_shield" and abs(n.y) > 0.5 and rgb[0] > rgb[1] > rgb[2] and rgb[0] > 0.15:
        if int(x * 18) % 3 == 0:
            rgb = dark(rgb, 0.82)  # plank seams
        if abs(x) < 0.28 and -0.42 < z < 0.36:
            # axe above the boss, two pines below it (concept 1.5)
            if abs(x) < 0.015 and 0.08 < z < 0.30:
                return (0.72, 0.74, 0.76)
            if 0.16 < z < 0.26 and 0.015 < abs(x) < 0.09:
                return (0.62, 0.64, 0.66)
            trunk = abs(abs(x) - 0.12) < 0.012 and -0.34 < z < -0.22
            def pine(cx):
                half = (-0.08 - z) * 0.45
                return -0.32 < z < -0.08 and abs(x - cx) < half
            if trunk or pine(-0.12) or pine(0.12):
                return (0.12, 0.45, 0.14)
        return rgb
    elif name in ("chopper_body", "chopper_hair", "chopper_beard"):
        return rgb
    return rgb


def bake(obs, buf, owners):
    """Raster vertex colours, then world-space detail. owners[pixel] records the mesh."""
    overlaps = []
    for ob in obs:
        me = ob.data
        uv = me.uv_layers.active.data
        me.calc_loop_triangles()
        for tri in me.loop_triangles:
            loops = list(tri.loops)
            uvs = [tuple(uv[li].uv) for li in loops]
            cols = [loop_color(me, li) for li in loops]
            cos = [me.vertices[me.loops[li].vertex_index].co for li in loops]
            xs = [u[0] * (SIZE - 1) for u in uvs]
            ys = [u[1] * (SIZE - 1) for u in uvs]
            minx, maxx = int(math.floor(min(xs))), int(math.ceil(max(xs)))
            miny, maxy = int(math.floor(min(ys))), int(math.ceil(max(ys)))
            minx, maxx = max(0, minx), min(SIZE - 1, maxx)
            miny, maxy = max(0, miny), min(SIZE - 1, maxy)
            nrm = tri.normal
            for y in range(miny, maxy + 1):
                for x in range(minx, maxx + 1):
                    w = bary(x + 0.5, y + 0.5, (xs[0], ys[0]), (xs[1], ys[1]), (xs[2], ys[2]))
                    if w is None:
                        continue
                    i = (y * SIZE + x) * 4
                    prev = owners[y * SIZE + x]
                    if prev and prev != ob.name:
                        overlaps.append((ob.name, prev))
                    owners[y * SIZE + x] = ob.name
                    r = w[0] * cols[0][0] + w[1] * cols[1][0] + w[2] * cols[2][0]
                    g = w[0] * cols[0][1] + w[1] * cols[1][1] + w[2] * cols[2][1]
                    b = w[0] * cols[0][2] + w[1] * cols[1][2] + w[2] * cols[2][2]
                    p = w[0] * cos[0] + w[1] * cos[1] + w[2] * cos[2]
                    r, g, b = detail(ob.name, p, nrm, (r, g, b))
                    buf[i] = r
                    buf[i + 1] = g
                    buf[i + 2] = b
                    buf[i + 3] = 1.0
    return overlaps


def texel_density(ob):
    me = ob.data
    uv = me.uv_layers.active.data
    me.calc_loop_triangles()
    world = 0.0
    uv_area = 0.0
    for tri in me.loop_triangles:
        vs = [me.vertices[i].co for i in tri.vertices]
        world += (vs[1] - vs[0]).cross(vs[2] - vs[0]).length * 0.5
        uvs = [uv[li].uv for li in tri.loops]
        uv_area += abs((uvs[1].x - uvs[0].x) * (uvs[2].y - uvs[0].y) -
                       (uvs[2].x - uvs[0].x) * (uvs[1].y - uvs[0].y)) * 0.5
    if uv_area < 1e-12:
        return 0.0
    return math.sqrt(world / uv_area) * SIZE


def process(path, names, buf, owners, report):
    bpy.ops.wm.open_mainfile(filepath=path)
    for img in list(bpy.data.images):
        if img.name not in ("Render Result", "Viewer Node"):
            bpy.data.images.remove(img)
    densities = []
    obs = []
    for name in names:
        ob = bpy.data.objects[name]
        n = unwrap(ob)
        densities.append((name, texel_density(ob), n))
        obs.append(ob)
        report.append("%s faces=%d density=%.1f px/m tile=%s" % (name, n, densities[-1][1], TILES[name]))
    overlaps = bake(obs, buf, owners)
    report.append("%s overlaps=%d" % (os.path.basename(path), len(set(overlaps))))
    # drop any image the bake did not create; UVs stay on the meshes
    for img in list(bpy.data.images):
        if img.name not in ("Render Result", "Viewer Node"):
            bpy.data.images.remove(img)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=path)
    return densities


def main():
    os.makedirs(TEX, exist_ok=True)
    buf = [0.12, 0.10, 0.09, 1.0] * (SIZE * SIZE)
    owners = [""] * (SIZE * SIZE)
    report = ["Chopper atlas UV report", "size %d" % SIZE]
    char = ["chopper_body", "chopper_head", "chopper_beard", "chopper_hair", "chopper_shirt",
            "chopper_vest", "chopper_belt", "chopper_chainmail", "chopper_boots", "chopper_helmet"]
    process(CHAR, char, buf, owners, report)
    process(PROPS, ["chopper_axe", "chopper_shield"], buf, owners, report)
    img = bpy.data.images.new("chopper_atlas", SIZE, SIZE, alpha=False)
    img.pixels.foreach_set(buf) if False else img.pixels.foreach_set([float(x) for x in buf])
    img.filepath_raw = ATLAS
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    dens = [line for line in report if "density=" in line]
    report.append("wrote " + ATLAS)
    text = "\n".join(report) + "\n"
    with open(TILES_TXT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    if any("overlaps=" in line and not line.endswith("overlaps=0") for line in report):
        raise SystemExit("UV tiles overlap")
    print("densities", len(dens))


if __name__ == "__main__":
    main()
