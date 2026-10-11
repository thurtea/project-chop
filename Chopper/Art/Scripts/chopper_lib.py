"""Shared hand-modeling helpers for the Chopper gray-box build scripts.

Everything here places explicit vertices and faces with bmesh. No image data,
no reference planes, no generated/AI geometry.

Conventions: 1 unit = 1 m, character faces -Y, Z up.
Tube rings are rounded rectangles in a (u, v) frame with the tube axis
w = u x v. The profile coordinate d = -hd is the "front" side of the ring.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

X = Vector((1.0, 0.0, 0.0))
Y = Vector((0.0, 1.0, 0.0))
Z = Vector((0.0, 0.0, 1.0))

# Placeholder palette (vertex colors only, linear-ish sRGB values)
PAL = {
    "skin": (0.93, 0.66, 0.52),
    "trousers": (0.30, 0.20, 0.12),
    "shirt": (0.70, 0.10, 0.08),
    "shirt_dark": (0.50, 0.06, 0.05),
    "vest": (0.52, 0.33, 0.18),
    "patch": (0.55, 0.55, 0.55),
    "leather_dark": (0.33, 0.20, 0.11),
    "steel": (0.62, 0.64, 0.66),
    "steel_dark": (0.32, 0.33, 0.35),
    "chain": (0.45, 0.46, 0.48),
    "boot": (0.42, 0.26, 0.14),
    "boot_wrap": (0.30, 0.18, 0.10),
    "sole": (0.20, 0.13, 0.08),
    "ginger": (0.80, 0.36, 0.10),
    "ginger_dark": (0.62, 0.26, 0.07),
    "wood": (0.55, 0.33, 0.16),
    "wood_dark": (0.40, 0.23, 0.11),
    "wrap": (0.36, 0.22, 0.12),
}


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_material(name, rgb):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    mat.diffuse_color = (rgb[0], rgb[1], rgb[2], 1.0)
    nt = mat.node_tree
    if nt is not None:
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None:
            ca = nt.nodes.new("ShaderNodeVertexColor")
            ca.layer_name = "Col"
            nt.links.new(ca.outputs["Color"], bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = 0.8
    return mat


def rrect(hw, hd, b, cs, gap=None):
    """Rounded/chamfered rectangle profile, CCW, in (a, d) coords."""
    b = max(0.0015, min(b, hw * 0.9, hd * 0.9))
    pts = []
    if gap is not None:
        pts.append((gap, -hd))
    corners = [((hw - b, -hd + b), -90.0), ((hw - b, hd - b), 0.0),
               ((-hw + b, hd - b), 90.0), ((-hw + b, -hd + b), 180.0)]
    for (cx, cy), a0 in corners:
        if cs == 0:
            pts.append((math.copysign(hw, cx), math.copysign(hd, cy)))
            continue
        for k in range(cs + 1):
            ang = math.radians(a0 + 90.0 * k / cs)
            pts.append((cx + b * math.cos(ang), cy + b * math.sin(ang)))
    if gap is not None:
        pts.append((-gap, -hd))
    return pts


def basis(w, up_hint=Z):
    """Right-handed (u, v, w) with v as close to up_hint as possible."""
    w = Vector(w).normalized()
    v = Vector(up_hint) - w * Vector(up_hint).dot(w)
    if v.length < 1e-6:
        v = Y - w * Y.dot(w)
    v.normalize()
    u = v.cross(w)
    return u, v, w


def rot_from(u, v, w):
    m = Matrix((u, v, w)).transposed()
    return m


class MeshBuilder:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")

    # ----------------------------------------------------------- utils
    def paint(self, faces, rgb):
        c = (rgb[0], rgb[1], rgb[2], 1.0)
        for f in faces:
            for lp in f.loops:
                lp[self.col] = c

    def _face(self, verts):
        return self.bm.faces.new(verts)

    # ----------------------------------------------------------- tubes
    def tube(self, rings, cs=1, gap=None, cap0=True, cap1=True, color=(1, 1, 1),
             skip=None, color_fn=None):
        """rings: list of (center, u, v, hw, hd, bevel).

        skip(i) -> True removes the front-centre face between ring i and i+1
        (only meaningful with gap set), used for slits.
        """
        bm = self.bm
        vr = []
        for (c, u, v, hw, hd, b) in rings:
            c = Vector(c)
            vr.append([bm.verts.new(c + u * a + v * d) for a, d in rrect(hw, hd, b, cs, gap)])
        n = len(vr[0])
        faces = []
        for i in range(len(vr) - 1):
            r0, r1 = vr[i], vr[i + 1]
            for j in range(n):
                k = (j + 1) % n
                if j == n - 1 and skip is not None and skip(i):
                    continue
                faces.append(self._face((r0[j], r0[k], r1[k], r1[j])))
        if cap0:
            faces.append(self._face(list(reversed(vr[0]))))
        if cap1:
            faces.append(self._face(vr[-1]))
        self.paint(faces, color)
        if color_fn is not None:
            for f in faces:
                cc = color_fn(f.calc_center_median())
                if cc is not None:
                    self.paint([f], cc)
        return vr, faces

    def ztube(self, x, y, table, cs=1, bevel=0.02, off=0.0, **kw):
        """Vertical tube. table rows: (z, hw, hd) or (z, hw, hd, dy).

        Rows must run bottom -> top: the tube axis is w = u x v = +Z, and faces
        built along +w face outward. A descending table would turn it inside out.
        """
        zs = [row[0] for row in table]
        if any(b <= a for a, b in zip(zs, zs[1:])):
            raise ValueError("ztube table must be strictly ascending in z (outward winding): %s" % zs)
        rings = []
        for row in table:
            z, hw, hd = row[0], row[1], row[2]
            dy = row[3] if len(row) > 3 else 0.0
            rings.append((Vector((x, y + dy, z)), X, Y, hw + off, hd + off, bevel))
        return self.tube(rings, cs=cs, **kw)

    def box(self, center, size, bevel=0.01, rot=None, color=(1, 1, 1)):
        """Box with chamfered edges (60 tris). size = (sx, sy, sz) in local u,v,w."""
        if rot is None:
            rot = Matrix.Identity(3)
        u, v, w = rot @ X, rot @ Y, rot @ Z
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        b = max(0.001, min(bevel, hx * 0.45, hy * 0.45, hz * 0.45))
        c = Vector(center)
        rings = [
            (c - w * hz, u, v, hx - b, hy - b, b * 0.6),
            (c - w * (hz - b), u, v, hx, hy, b),
            (c + w * (hz - b), u, v, hx, hy, b),
            (c + w * hz, u, v, hx - b, hy - b, b * 0.6),
        ]
        return self.tube(rings, cs=1, color=color)

    def cylinder(self, center, axis, radius, length, color=(1, 1, 1), cs=3, bevel=0.0):
        u, v, w = basis(axis)
        c = Vector(center)
        r = radius
        if bevel > 0:
            rings = [(c - w * length / 2, u, v, r - bevel, r - bevel, r - bevel),
                     (c - w * (length / 2 - bevel), u, v, r, r, r),
                     (c + w * (length / 2 - bevel), u, v, r, r, r),
                     (c + w * length / 2, u, v, r - bevel, r - bevel, r - bevel)]
        else:
            rings = [(c - w * length / 2, u, v, r, r, r), (c + w * length / 2, u, v, r, r, r)]
        return self.tube(rings, cs=cs, color=color)

    def path_strip(self, points, normals, hw, hd, color, cs=1, bevel=0.003):
        """Flat strap following a polyline; normals give the strap's outward side."""
        rings = []
        n = len(points)
        for i in range(n):
            p = Vector(points[i])
            t = (Vector(points[min(i + 1, n - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
            nv = Vector(normals[i])
            nv = (nv - t * nv.dot(t)).normalized()
            v = nv
            u = v.cross(t)
            rings.append((p, u, v, hw, hd, bevel))
        return self.tube(rings, cs=cs, color=color)

    # ----------------------------------------------------------- output
    def finish(self, mat, recalc=False):
        bm = self.bm
        if recalc:
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = False
        me.materials.append(mat)
        if "Col" in me.color_attributes:
            me.color_attributes.active_color = me.color_attributes["Col"]
            try:
                me.color_attributes.render_color_index = me.color_attributes.active_color_index
            except Exception:
                pass
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def tri_count(ob):
    me = ob.data
    me.calc_loop_triangles()
    return len(me.loop_triangles)
