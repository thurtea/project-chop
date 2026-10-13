"""UV-unwrap every Chopper mesh into one shared 2048 atlas and bake a base colour PNG.

Run:  blender -b -P uv_atlas.py
In:   Art/Blender/chopper_character.blend, chopper_props.blend
Out:  those files (one UVMap layer each, no image packed into the blend),
      Art/Textures/chopper_atlas.png, Art/Textures/atlas_tiles.txt (UV report)

Layout (W6-03, replaces the per-polygon grid cells of W2-01):
  * islands: every connected mesh part is split into sides by growing regions of faces whose
    normals stay within ISLAND_ANGLE of the seed face (front, back and sides of a tube, the
    faces of a block). Each island is projected flat onto the plane of its mean normal, so an
    island is connected and keeps its proportions.
  * density: pixel art. One atlas texel is one painted pixel of atlas_paint.texel_m() metres
    (1.6 cm on the character, 1.2 cm on the face front and the bear axe), the same on every
    part, so it reads crisply with point filtering. The atlas keeps its 2048 size; most of it
    is free room for later gear.
  * packing: the islands of one part are packed together with a small inner gap, the parts
    are skyline-packed with a PART_GAP gutter. All objects of both files share the one atlas,
    so islands of different objects never overlap.
  * edges: texels cut by an island edge are painted from their own triangle (conservative
    raster), then every island is grown outward by DILATE px by copying its edge texels, so
    neither point sampling nor mip levels pull in the dark gutter.
Paint: atlas_paint.detail(), in object space. The PNG is linear paint encoded to sRGB
(Blender and Unity read 8-bit PNGs as sRGB).
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from atlas_paint import SUPER, TEXEL_M, detail, loop_color, texel_m  # noqa: E402

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
CHAR = os.path.join(ART, "Blender", "chopper_character.blend")
PROPS = os.path.join(ART, "Blender", "chopper_props.blend")
TEX = os.path.join(ART, "Textures")
ATLAS = os.path.join(TEX, "chopper_atlas.png")
TILES_TXT = os.path.join(TEX, "atlas_tiles.txt")
SIZE = 2048
MARGIN = 10              # px kept free at the atlas border
PART_GAP = 16            # px between parts (each side grows DILATE px into it)
INNER_GAP = 4            # px between the islands of one part (same surface, they pad into each other)
DILATE = 8               # px of edge padding grown out of every island
ISLAND_ANGLE = 50.0      # deg from the seed face normal
ISLAND_COS = math.cos(math.radians(ISLAND_ANGLE))

CHAR_OBJECTS = ["chopper_body", "chopper_head", "chopper_beard", "chopper_hair", "chopper_shirt",
                "chopper_vest", "chopper_belt", "chopper_bracers", "chopper_chainmail", "chopper_boots",
                "chopper_helmet", "chopper_plate", "chopper_pauldrons"]
PROP_OBJECTS = ["chopper_axe", "chopper_axe_bear", "chopper_shield"]
OBJ_INDEX = {n: i for i, n in enumerate(CHAR_OBJECTS + PROP_OBJECTS)}


def density_mult(name, normal, centre):
    """Island density relative to D = 1 / TEXEL_M: one atlas texel = one painted pixel of
    texel_m() metres (pixel art, read with point filtering)."""
    return TEXEL_M / texel_m(name, normal, centre)


# ------------------------------------------------------------------ islands
def island_normal(polys, isl):
    n = Vector()
    for f in isl:
        n += polys[f].normal * polys[f].area
    return n.normalized() if n.length > 1e-12 else polys[isl[0]].normal.copy()


def mesh_islands(ob):
    """Per connected part: [(faces, mean normal, centre)] for each side island."""
    me = ob.data
    polys = me.polygons
    edge_faces = {}
    for p in polys:
        for ek in p.edge_keys:
            edge_faces.setdefault(ek, []).append(p.index)
    nbrs = [set() for _ in polys]
    for fl in edge_faces.values():
        for a in fl:
            for b in fl:
                if a != b:
                    nbrs[a].add(b)
    part_of = [-1] * len(polys)
    parts = []
    for f in range(len(polys)):
        if part_of[f] >= 0:
            continue
        stack, comp = [f], []
        part_of[f] = len(parts)
        while stack:
            a = stack.pop()
            comp.append(a)
            for b in nbrs[a]:
                if part_of[b] < 0:
                    part_of[b] = len(parts)
                    stack.append(b)
        parts.append(comp)
    out = []
    for comp in parts:
        left = set(comp)
        islands = []
        for seed in sorted(comp, key=lambda f: (-polys[f].area, f)):
            if seed not in left:
                continue
            n0 = polys[seed].normal.copy()
            isl, stack = [seed], [seed]
            left.discard(seed)
            while stack:
                a = stack.pop()
                for b in sorted(nbrs[a]):
                    if b in left and polys[b].normal.dot(n0) > ISLAND_COS:
                        left.discard(b)
                        isl.append(b)
                        stack.append(b)
            islands.append(isl)
        # fold tiny leftovers (block corner triangles) into a touching island they face
        merged = True
        while merged:
            merged = False
            for k, isl in enumerate(islands):
                if len(islands) < 2 or len(isl) > 2 or sum(polys[f].area for f in isl) > 2e-4:
                    continue
                best, bd = None, 0.3
                for j, other in enumerate(islands):
                    if j == k or not any(b in other for f in isl for b in nbrs[f]):
                        continue
                    no = island_normal(polys, other)
                    d = min(polys[f].normal.dot(no) for f in isl)
                    if d > bd:
                        best, bd = j, d
                if best is not None:
                    islands[best] = islands[best] + isl
                    islands.pop(k)
                    merged = True
                    break
        out.append([(isl, island_normal(polys, isl),
                     sum((polys[f].center for f in isl), Vector()) / len(isl)) for isl in islands])
    return out


def project(ob, isl, n):
    """Loop index -> (x, y) in metres on the island plane, longest axis along x, min at 0."""
    me = ob.data
    up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((0, 1, 0))
    u = up.cross(n).normalized()
    v = n.cross(u).normalized()
    pts = {}
    for f in isl:
        for li in me.polygons[f].loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            pts[li] = (co.dot(u), co.dot(v))
    xs = np.array(list(pts.values()))
    c = xs.mean(axis=0)
    ca, sa = 1.0, 0.0
    if len(xs) > 2:
        # keep the texel grid on world up / horizontal (painted rows stay level); only a long
        # strip running diagonally (the A-pose arms) is turned to run along its own axis
        w, vecs = np.linalg.eigh(np.cov((xs - c).T))
        ax = vecs[:, int(np.argmax(w))]
        ang = math.degrees(math.atan2(ax[1], ax[0])) % 90.0
        off = min(ang, 90.0 - ang)
        if off > 20.0 and max(w) > 4.0 * max(min(w), 1e-12):
            ca, sa = ax[0], ax[1]
    out = {li: ((x - c[0]) * ca + (y - c[1]) * sa, -(x - c[0]) * sa + (y - c[1]) * ca)
           for li, (x, y) in pts.items()}
    mnx = min(p[0] for p in out.values())
    mny = min(p[1] for p in out.values())
    return {li: (x - mnx, y - mny) for li, (x, y) in out.items()}


def collect(path, names, layout):
    bpy.ops.wm.open_mainfile(filepath=path)
    for name in names:
        ob = bpy.data.objects[name]
        for part in mesh_islands(ob):
            isls = []
            for isl, n, c in part:
                uv = project(ob, isl, n)
                isls.append(dict(faces=isl, uv=uv, w=max(p[0] for p in uv.values()),
                                 h=max(p[1] for p in uv.values()), mult=density_mult(name, n, c)))
            layout.append(dict(obj=name, islands=isls))


# ------------------------------------------------------------------ packing
def skyline(rects, width, gap):
    """Bottom-left skyline packing of rects [(w, h)] px into `width` (tallest first).
    Returns positions and the height used. Each rect reserves `gap` px to its right and top."""
    order = sorted(range(len(rects)), key=lambda i: (-rects[i][1], -rects[i][0], i))
    sky = [(0, 0, width + gap)]               # segments (x, y, w) along the top of the packed area
    pos = [None] * len(rects)
    top = 0
    for i in order:
        w, h = rects[i][0] + gap, rects[i][1] + gap
        best = None
        for k in range(len(sky)):
            x = sky[k][0]
            if x + w > width + gap:
                break
            y, span, j = 0, 0, k
            while span < w and j < len(sky):
                y = max(y, sky[j][1])
                span = sky[j][0] + sky[j][2] - x
                j += 1
            if span < w:
                continue
            if best is None or (y + h, x) < (best[0] + h, best[1]):
                best = (y, x)
        if best is None:
            return None, 1e9
        y, x = best
        pos[i] = (x, y)
        top = max(top, y + h - gap)
        new = []
        for sx, sy, sw in sky:
            e = sx + sw
            if e <= x or sx >= x + w:
                new.append((sx, sy, sw))
                continue
            if sx < x:
                new.append((sx, sy, x - sx))
            if e > x + w:
                new.append((x + w, sy, e - x - w))
        new.append((x, y + h, w))
        new.sort()
        merged = []
        for seg in new:
            if merged and merged[-1][1] == seg[1] and merged[-1][0] + merged[-1][2] == seg[0]:
                merged[-1] = (merged[-1][0], seg[1], merged[-1][2] + seg[2])
            else:
                merged.append(seg)
        sky = merged
    return pos, top


def pack(layout, D):
    """Place every island at density D; returns the atlas height used (px), None if too wide."""
    usable = SIZE - 2 * MARGIN
    for part in layout:
        rects = [(max(1, math.ceil(i["w"] * D * i["mult"])), max(1, math.ceil(i["h"] * D * i["mult"])))
                 for i in part["islands"]]
        area = sum((w + INNER_GAP) * (h + INNER_GAP) for w, h in rects)
        width = max(max(w for w, h in rects), int(math.sqrt(area) * 1.15))
        if width > usable:
            return None
        pos, hgt = skyline(rects, width, INNER_GAP)
        if pos is None:
            return None
        part["sub"] = pos
        part["rect"] = (max(p[0] + r[0] for p, r in zip(pos, rects)), hgt)
    pos, total = skyline([p["rect"] for p in layout], usable, PART_GAP)
    if pos is None:
        return None
    for part, (x, y) in zip(layout, pos):
        part["at"] = (MARGIN + x, MARGIN + y)
    return total


def fit(layout):
    """Fixed density: SUPER texels per TEXEL_M metres (one painted pixel is SUPER x SUPER texels). Fails if it does not fit."""
    D = SUPER / TEXEL_M
    t = pack(layout, D)
    if t is None or t > SIZE - 2 * MARGIN:
        raise SystemExit("atlas does not fit at %.1f cm per texel" % (100 * TEXEL_M))
    return D


PART_CENTRE = {}     # (object, polygon index) -> centre of its connected part


def apply_uvs(names, layout, D):
    for name in names:
        me = bpy.data.objects[name].data
        while len(me.uv_layers):
            me.uv_layers.remove(me.uv_layers[0])
        me.uv_layers.new(name="UVMap")
        me.uv_layers.active = me.uv_layers["UVMap"]
    for part in layout:
        if part["obj"] not in names:
            continue
        uvl = bpy.data.objects[part["obj"]].data.uv_layers["UVMap"].data
        ox, oy = part["at"]
        me = bpy.data.objects[part["obj"]].data
        fs = [f for isl in part["islands"] for f in isl["faces"]]
        pc = sum((me.polygons[f].center for f in fs), Vector()) / len(fs)
        for f in fs:
            PART_CENTRE[(part["obj"], f)] = pc
        for isl, (sx, sy) in zip(part["islands"], part["sub"]):
            k = D * isl["mult"]
            for li, (x, y) in isl["uv"].items():
                uvl[li].uv = ((ox + sx + x * k) / SIZE, (oy + sy + y * k) / SIZE)


# ------------------------------------------------------------------ bake
TRI_VERTS = {}
TRI_OWNER = np.full(SIZE * SIZE, -1, dtype=np.int64)
CLAIM = np.zeros(SIZE * SIZE, dtype=np.int8)     # 2 = texel centre inside a triangle, 1 = edge texel


def shares_edge(k1, k2):
    """Two triangles of one object that share an edge (a split quad or neighbours)."""
    if k1 // 10_000_000 != k2 // 10_000_000:
        return False
    return len(TRI_VERTS.get(k1, set()) & TRI_VERTS.get(k2, set())) >= 2


def bake(obs, buf, island_px, overlaps):
    """Raster vertex colour + object-space paint into buf (linear RGBA rows, one per pixel).

    A pixel centre strictly inside triangles of two objects, or inside two triangles of one
    object that do not share an edge, is recorded as an overlap."""
    tri_owner = TRI_OWNER        # shared by both files, so props are tested against the character
    for ob in obs:
        oid = OBJ_INDEX[ob.name]
        me = ob.data
        uv = me.uv_layers.active.data
        me.calc_loop_triangles()
        for tri in me.loop_triangles:
            loops = list(tri.loops)
            uvs = np.array([tuple(uv[li].uv) for li in loops]) * SIZE
            key = oid * 10_000_000 + tri.index
            TRI_VERTS[key] = set(tri.vertices)
            x0, y0 = np.floor(uvs.min(axis=0)).astype(int)
            x1, y1 = np.ceil(uvs.max(axis=0)).astype(int)
            x0, y0, x1, y1 = max(0, x0), max(0, y0), min(SIZE - 1, x1), min(SIZE - 1, y1)
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, ay), (bx, by), (cx, cy) = uvs
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-12:
                continue
            w0 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
            w1 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
            w2 = 1.0 - w0 - w1
            # conservative: every texel whose centre is within 0.75 px of the triangle is painted
            # from the triangle (clamped barycentrics), so slivers and texels cut by an island
            # edge carry their own colour under point filtering; centre-inside texels win
            area2 = abs(den)
            hts = [area2 / max(1e-9, math.dist(uvs[1], uvs[2])), area2 / max(1e-9, math.dist(uvs[0], uvs[2])),
                   area2 / max(1e-9, math.dist(uvs[0], uvs[1]))]
            dist_out = np.minimum(np.minimum(w0 * hts[0], w1 * hts[1]), w2 * hts[2])
            inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
            near = dist_out >= -0.75
            strict = (w0 > 2e-3) & (w1 > 2e-3) & (w2 > 2e-3)
            cols = [loop_color(me, li) for li in loops]
            cos = [me.vertices[me.loops[li].vertex_index].co for li in loops]
            nrm = tri.normal
            pc = PART_CENTRE.get((ob.name, tri.polygon_index), Vector())
            for yy, xx in zip(*np.nonzero(near)):
                px = (y0 + yy) * SIZE + (x0 + xx)
                a, b, c = max(0.0, float(w0[yy, xx])), max(0.0, float(w1[yy, xx])), max(0.0, float(w2[yy, xx]))
                t = a + b + c
                a, b, c = a / t, b / t, c / t
                level = 2 if inside[yy, xx] else 1
                if level < CLAIM[px] or (level == 1 and CLAIM[px] == 1):
                    continue
                CLAIM[px] = level
                if strict[yy, xx]:
                    prev = tri_owner[px]
                    if prev >= 0 and prev != key and not shares_edge(int(prev), key):
                        overlaps.append((int(prev) // 10_000_000, oid))
                    tri_owner[px] = key
                island_px[px] = 1
                rgb = tuple(a * cols[0][i] + b * cols[1][i] + c * cols[2][i] for i in range(3))
                p = cos[0] * a + cos[1] * b + cos[2] * c
                r, g, bl = detail(ob.name, p, nrm, rgb, pc)
                buf[px, 0], buf[px, 1], buf[px, 2] = r, g, bl


def dilate(buf, filled, steps):
    """Grow every filled region outward by `steps` px, copying one neighbouring texel (edge
    neighbours first, then diagonals), so the padding repeats the edge pixels exactly."""
    img = buf.reshape(SIZE, SIZE, 4)
    m = filled.reshape(SIZE, SIZE).astype(bool)
    for _ in range(steps):
        new = m.copy()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)):
            sm = np.roll(np.roll(m, dy, 0), dx, 1)
            take = sm & ~new
            if take.any():
                img[take] = np.roll(np.roll(img, dy, 0), dx, 1)[take]
                new |= take
        m = new
    return m


def fill_gutter(buf, filled):
    """Fills every texel outside the padded islands with the average colour of the nearest islands (push-pull: average
    the filled texels down a pyramid, then fill the gaps back up from the coarser levels). Mip levels then blend island
    colours with each other instead of with a dark gutter, which showed as flickering dark edges on the character."""
    img = buf.reshape(SIZE, SIZE, 4)
    w = filled.reshape(SIZE, SIZE).astype(np.float64)
    colours, weights = [img[:, :, :3] * w[:, :, None]], [w]
    while colours[-1].shape[0] > 1:
        c, k = colours[-1], weights[-1]
        n = c.shape[0] // 2
        colours.append(c.reshape(n, 2, n, 2, 3).sum(axis=(1, 3)))
        weights.append(k.reshape(n, 2, n, 2).sum(axis=(1, 3)))
    # Down the pyramid from the coarsest level: a level's empty texels take the colour of the level above.
    fill = colours[-1] / np.maximum(weights[-1], 1e-9)[:, :, None]
    for c, k in zip(reversed(colours[:-1]), reversed(weights[:-1])):
        up = np.repeat(np.repeat(fill, 2, axis=0), 2, axis=1)
        own = c / np.maximum(k, 1e-9)[:, :, None]
        fill = np.where((k > 0)[:, :, None], own, up)
    empty = w == 0
    img[:, :, :3][empty] = fill[empty]


def texel_density(ob):
    """Mean texel density in px per metre: SIZE * sqrt(UV area / surface area)."""
    me = ob.data
    uv = me.uv_layers.active.data
    me.calc_loop_triangles()
    world = uv_area = 0.0
    for tri in me.loop_triangles:
        vs = [me.vertices[i].co for i in tri.vertices]
        world += (vs[1] - vs[0]).cross(vs[2] - vs[0]).length * 0.5
        uvs = [uv[li].uv for li in tri.loops]
        uv_area += abs((uvs[1].x - uvs[0].x) * (uvs[2].y - uvs[0].y) -
                       (uvs[2].x - uvs[0].x) * (uvs[1].y - uvs[0].y)) * 0.5
    return SIZE * math.sqrt(uv_area / world) if world > 0 else 0.0


def process(path, names, layout, D, buf, island_px, overlaps, report):
    bpy.ops.wm.open_mainfile(filepath=path)
    for img in list(bpy.data.images):
        if img.name not in ("Render Result", "Viewer Node"):
            bpy.data.images.remove(img)
    apply_uvs(names, layout, D)
    obs = [bpy.data.objects[n] for n in names]
    for ob in obs:
        parts = [p for p in layout if p["obj"] == ob.name]
        d = texel_density(ob)
        report.append("%-18s faces=%4d parts=%3d islands=%4d density=%5.1f px/m = %.2f cm per painted pixel"
                      % (ob.name, len(ob.data.polygons), len(parts),
                         sum(len(p["islands"]) for p in parts), d, 100.0 * SUPER / d))
    bake(obs, buf, island_px, overlaps)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=path)


def linear_to_srgb(a):
    a = np.clip(a, 0.0, 1.0)
    return np.where(a <= 0.0031308, a * 12.92, 1.055 * np.power(a, 1.0 / 2.4) - 0.055)


UNDER_ATLAS = os.path.join(TEX, "chopper_atlas_under.png")
UNDER_OBJECTS = ["chopper_shirt", "chopper_body"]


def write_under_atlas(buf, layout, D):
    """Same atlas with the shirt and body painted as the base layer (atlas_paint.UNDER)."""
    import atlas_paint
    global TRI_OWNER, CLAIM
    under = buf.copy()
    TRI_OWNER = np.full(SIZE * SIZE, -1, dtype=np.int64)
    CLAIM = np.zeros(SIZE * SIZE, dtype=np.int8)
    island_px = np.zeros(SIZE * SIZE, dtype=np.int8)
    bpy.ops.wm.open_mainfile(filepath=CHAR)
    atlas_paint.UNDER = True
    try:
        bake([bpy.data.objects[n] for n in UNDER_OBJECTS], under, island_px, [])
    finally:
        atlas_paint.UNDER = False
    dilate(under, island_px, DILATE)
    rgb = linear_to_srgb(under[:, :3])
    out = np.concatenate([rgb, np.ones((SIZE * SIZE, 1))], axis=1).astype(np.float32)
    img = bpy.data.images.new("chopper_atlas_under", SIZE, SIZE, alpha=False)
    img.pixels.foreach_set(out.ravel())
    img.filepath_raw = UNDER_ATLAS
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def main():
    os.makedirs(TEX, exist_ok=True)
    layout = []
    collect(CHAR, CHAR_OBJECTS, layout)
    collect(PROPS, PROP_OBJECTS, layout)
    D = fit(layout)
    report = ["Chopper atlas UV report",
              "size %d, pixel art: one painted pixel = %.1f cm = %d x %d texels (%.1f texels/m), finer on the face front and the "
              "bear axe (atlas_paint.texel_m); part gap %d px, inner gap %d px, edge padding %d px (copied edge texels)"
              % (SIZE, 100 * TEXEL_M, SUPER, SUPER, D, PART_GAP, INNER_GAP, DILATE)]
    # the edge padding copies the edge texels; fill_gutter then fills the rest from the nearest islands
    buf = np.tile(np.array([0.0134, 0.0100, 0.0086, 1.0]), (SIZE * SIZE, 1))
    island_px = np.zeros(SIZE * SIZE, dtype=np.int8)
    overlaps = []
    process(CHAR, CHAR_OBJECTS, layout, D, buf, island_px, overlaps, report)
    process(PROPS, PROP_OBJECTS, layout, D, buf, island_px, overlaps, report)
    padded = dilate(buf, island_px, DILATE)
    fill_gutter(buf, padded)
    report.append("coverage: islands %.1f %%, islands plus padding %.1f %%"
                  % (100.0 * island_px.mean(), 100.0 * padded.mean()))
    cross = sum(1 for a, b in overlaps if a != b)
    inner = sum(1 for a, b in overlaps if a == b)
    report.append("overlapping pixels: between objects %d, inside one object %d" % (cross, inner))
    rgb = linear_to_srgb(buf[:, :3])
    out = np.concatenate([rgb, np.ones((SIZE * SIZE, 1))], axis=1).astype(np.float32)
    img = bpy.data.images.new("chopper_atlas", SIZE, SIZE, alpha=False)
    img.pixels.foreach_set(out.ravel())
    img.filepath_raw = ATLAS
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    report.append("wrote " + ATLAS)
    write_under_atlas(buf, layout, D)
    report.append("wrote " + UNDER_ATLAS)
    text = "\n".join(report) + "\n"
    with open(TILES_TXT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    if cross or inner:
        raise SystemExit("UV islands overlap")


if __name__ == "__main__":
    main()
