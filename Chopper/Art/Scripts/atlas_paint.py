"""Pixel-art paint for the Chopper atlas (called once per atlas texel by uv_atlas.bake).

Look (user, W6-03): Minecraft shapes with Valheim-style rendering, so the atlas is a
pixel-art texture. One atlas texel is one painted pixel of texel_m() metres (1.6 cm on the
character, 1.2 cm on the face front and the bear axe). Unity samples it with point filtering.
Every pattern below is counted in whole pixels (floor(coordinate / texel)), so stripes,
stitches and lacing land on the pixel grid instead of being smooth gradients.

detail(name, p, n, rgb, pc): object name, object-space point at the texel centre, face
normal, the interpolated vertex colour (linear, it tells which piece of a mesh this is), and
the centre of the connected part. Returns the linear colour. Colours are written as sRGB
0-255 (picked from concept 1.5) and converted to linear.
"""
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from chopper_lib import PAL  # noqa: E402

TEXEL_M = 0.016          # character pixel (m)
TEXEL_FINE = 0.012       # face front and bear axe
SUPER = 3                # atlas texels per painted pixel, per axis
MOUSTACHE = (0.88, 0.44, 0.13)                       # = build_chopper.MOUSTACHE
BEAD = (0.86, 0.62, 0.28)                            # = build_chopper.BEAD
HELM_BONE = (0.86, 0.80, 0.66)                       # = build_chopper.BONE
BEAR_WOOD, BEAR_CARVE, BEAR_HEAD = (0.72, 0.42, 0.19), (0.42, 0.22, 0.09), (0.80, 0.50, 0.23)  # build_props
SHOULDER_X, SHOULDER_Z = 0.300, 1.27           # build_chopper.SHOULDER after the bulk warp


def texel_m(name, normal, centre):
    """Painted pixel size (m) of an island; uv_atlas sets the UV density from it."""
    if name == "chopper_head" and normal.y < -0.5:
        return TEXEL_FINE
    if name == "chopper_axe_bear":
        return TEXEL_FINE
    return TEXEL_M


# ------------------------------------------------------------------ helpers
def loop_color(me, li):
    attr = me.color_attributes.get("Col")
    if attr is None:
        return (0.6, 0.6, 0.6)
    c = attr.data[li].color
    return (c[0], c[1], c[2])


def S(r, g, b):
    """sRGB 0-255 -> linear."""
    def f(c):
        c = c / 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (f(r), f(g), f(b))


def near(rgb, ref, tol=0.02):
    """rgb is a vertex colour as read back (linear). ref is a PAL-style value as the build scripts write it; the
    BYTE_COLOR attribute stores that as sRGB, so it reads back as its sRGB-to-linear value (0.30 -> 0.073)."""
    ref = S(ref[0] * 255.0, ref[1] * 255.0, ref[2] * 255.0)
    return all(abs(a - b) < tol for a, b in zip(rgb, ref))


def dark(rgb, k):
    return tuple(c * k for c in rgb)


def mix(a, b, k):
    return tuple(a[i] * (1 - k) + b[i] * k for i in range(3))


def h3(i, j, k=0):
    """Deterministic hash of integer cells -> 0..1."""
    v = (i * 73856093) ^ (j * 19349663) ^ (k * 83492791)
    v = (v ^ (v >> 13)) * 1274126177
    return ((v ^ (v >> 16)) & 0xFFFF) / 65535.0


def cell(p, t):
    return (math.floor(p.x / t), math.floor(p.y / t), math.floor(p.z / t))


# All grain at half strength (user, 2026-10-12: smoother textures; the per-pixel noise also shimmered in motion).
GRAIN_SCALE = 0.5


def grain(rgb, p, t, amount=0.07):
    """Per-pixel value jitter (the pixel-art noise of a block texture)."""
    i, j, k = cell(p, t)
    f = 1.0 + amount * GRAIN_SCALE * (2.0 * h3(i, j, k) - 1.0)
    return tuple(min(1.0, c * f) for c in rgb)


def side_coord(p, n):
    """Horizontal coordinate across a vertical face: x on front/back faces, y on the sides."""
    return p.x if abs(n.y) >= abs(n.x) else p.y


def arm_coords(p, n, sx):
    """(t along the arm from the shoulder, coordinate around the arm in the face plane)."""
    d = Vector((sx, -0.20, -1.0)).normalized()
    S0 = Vector((sx * SHOULDER_X, 0.0, SHOULDER_Z))
    t = (p - S0).dot(d)
    across = d.cross(n)
    if across.length < 1e-6:
        across = Vector((0, 1, 0))
    return t, (p - S0).dot(across.normalized())


# ------------------------------------------------------------------ palette (concept 1.5)
RED = S(182, 36, 32)
RED_LINE = S(122, 22, 22)
PLAID_MID = S(92, 16, 18)
PLAID_BLACK = S(30, 10, 12)
LEATHER = [S(124, 74, 38), S(100, 60, 30), S(142, 90, 48)]
PATCH = [S(108, 108, 108), S(88, 90, 92)]
SEAM = S(58, 34, 18)
THREAD = S(214, 190, 146)
STRAP = S(84, 50, 26)
BELT = S(72, 44, 24)
STEEL_LIGHT = S(196, 200, 205)
STEEL_MID = S(132, 136, 142)
STEEL_DARK = S(70, 72, 78)
TROUSERS = S(104, 70, 42)
KNEE_PAD = S(76, 54, 38)
BOOT = S(72, 46, 28)
BOOT_WRAP = S(88, 74, 62)
CORD = S(166, 126, 82)
SOLE = S(46, 30, 20)
SKIN = S(228, 168, 132)
SKIN_SHADE = S(200, 138, 106)
EYE_WHITE = S(236, 232, 222)
IRIS = S(52, 150, 70)
PUPIL = S(24, 30, 26)
GINGER = S(160, 70, 26)          # deeper copper of the model sheet
GINGER_DARK = S(108, 42, 15)
GINGER_LIGHT = S(196, 100, 44)
MOUSTACHE_C = S(180, 86, 34)
WOOD = S(176, 106, 52)
WOOD_DARK = S(136, 80, 38)
WOOD_GROOVE = S(92, 50, 22)
BEAR = S(198, 132, 70)
BEAR_MUZZLE = S(222, 168, 108)
BEAR_DARK = S(40, 24, 14)
WRAP = S(118, 52, 34)
GRIP_WRAP = S(112, 34, 24)


# ------------------------------------------------------------------ parts
def plaid(i, j):
    """Red and black buffalo plaid with a thin line, period 7 px on both axes."""
    a, b = i % 7, j % 7
    da, db = a in (0, 1), b in (0, 1)
    if da and db:
        return PLAID_BLACK
    if da or db:
        return PLAID_MID
    if a == 4 or b == 4:
        return RED_LINE
    return RED


# Base layer atlas (undershirt and braies), baked by uv_atlas.write_under_atlas.
UNDER = False
LINEN = S(92, 92, 94)
LINEN_SEAM = S(64, 64, 66)
SHORTS = S(78, 78, 80)
UNDER_SLEEVE = 0.15        # m along the arm from the shoulder joint
UNDER_BRAIES_Z = 0.56      # shorts hem height (m); bare legs and feet below


DRAUGR = {
    "SKIN": S(140, 152, 142), "SKIN_SHADE": S(108, 120, 112),
    "GINGER": S(150, 148, 140), "GINGER_DARK": S(92, 90, 86), "GINGER_LIGHT": S(198, 196, 188),
    "MOUSTACHE_C": S(172, 170, 162),
    "EYE_WHITE": S(26, 30, 36), "IRIS": S(120, 232, 255), "PUPIL": S(214, 252, 255),
    "RED": S(84, 42, 38), "RED_LINE": S(60, 32, 30), "PLAID_MID": S(46, 28, 28), "PLAID_BLACK": S(22, 16, 16),
    "LEATHER": [S(72, 54, 40), S(58, 44, 32), S(86, 64, 46)], "PATCH": [S(82, 82, 80), S(66, 66, 66)],
    "SEAM": S(34, 26, 20), "THREAD": S(120, 112, 96), "STRAP": S(50, 38, 28), "BELT": S(44, 34, 26),
    "STEEL_LIGHT": S(156, 122, 92), "STEEL_MID": S(112, 84, 62), "STEEL_DARK": S(62, 50, 42),
    "TROUSERS": S(62, 52, 44), "KNEE_PAD": S(50, 42, 36), "BOOT": S(46, 38, 32), "BOOT_WRAP": S(72, 66, 60),
    "SOLE": S(30, 26, 22), "CORD": S(108, 96, 80),
    "WOOD": S(112, 94, 76), "WOOD_DARK": S(84, 70, 56), "WOOD_GROOVE": S(54, 44, 36),
    "WRAP": S(70, 44, 36), "GRIP_WRAP": S(66, 36, 30),
}


def swap_palette(values):
    g = globals()
    old = {k: g[k] for k in values}
    old["VEST_PATCHES_FRONT"], old["VEST_PATCHES_BACK"] = VEST_PATCHES_FRONT, VEST_PATCHES_BACK
    g.update(values)
    pairs = [(old[k][i], g[k][i]) for k in ("PATCH", "LEATHER") if k in values for i in range(len(old[k]))]

    def col(c):
        return next((b for a, b in pairs if a == c), c)
    for name in ("VEST_PATCHES_FRONT", "VEST_PATCHES_BACK"):
        g[name] = [(x0, x1, z0, z1, col(c)) for x0, x1, z0, z1, c in old[name]]
    return old


def linen(p, n, t):
    c = LINEN if math.floor(p.z / t) % 2 else dark(LINEN, 0.96)
    return grain(c, p, t, 0.04)


def paint_under_shirt(p, n, rgb, pc, t):
    if abs(pc.x) > 0.05:
        ta, _ = arm_coords(p, n, 1.0 if pc.x > 0 else -1.0)
        if ta > UNDER_SLEEVE + t:
            return grain(SKIN, p, t, 0.03)
        if ta > UNDER_SLEEVE:
            return LINEN_SEAM
    if near(rgb, PAL["shirt_dark"]):          # collar and hem
        return LINEN_SEAM
    return linen(p, n, t)


def paint_shirt(p, n, rgb, pc, t):
    if UNDER:
        return paint_under_shirt(p, n, rgb, pc, t)
    if abs(pc.x) > 0.05:                      # sleeve: plaid runs along and around the arm
        ta, ac = arm_coords(p, n, 1.0 if pc.x > 0 else -1.0)
        c = plaid(math.floor(ta / t), math.floor(ac / t))
    else:
        c = plaid(math.floor(side_coord(p, n) / t), math.floor(p.z / t))
        if abs(n.z) > 0.7:                    # shoulder tops
            c = plaid(math.floor(p.x / t), math.floor(p.y / t))
    if near(rgb, PAL["shirt_dark"]):          # cuffs, hem and collar a shade darker
        c = dark(c, 0.72)
    return grain(c, p, t, 0.05)


# Model sheet: brown leather with a few patches, each sewn on with a stitched border and riveted corners.
# (x0, x1, z0, z1, colour) in the warped body's metres; front patches face -Y, back patches +Y.
VEST_PATCHES_FRONT = [(-0.40, -0.19, 1.14, 1.32, PATCH[0]), (-0.19, -0.05, 1.04, 1.22, LEATHER[2]),
                      (-0.38, -0.17, 0.90, 1.08, PATCH[1]), (0.05, 0.22, 1.16, 1.32, LEATHER[2]),
                      (0.22, 0.40, 1.08, 1.28, PATCH[1]), (0.06, 0.23, 0.90, 1.07, PATCH[0]),
                      (0.25, 0.40, 0.88, 1.03, LEATHER[2])]
VEST_PATCHES_BACK = [(-0.34, -0.12, 1.12, 1.30, PATCH[0]), (-0.12, 0.10, 0.96, 1.12, LEATHER[2]),
                     (0.10, 0.32, 1.14, 1.30, PATCH[1]), (0.14, 0.34, 0.90, 1.04, PATCH[0])]


def vest_panel(a, z, t, patches):
    """Colour of the vest at (a, z): a patch with its stitched border and corner rivets, or plain leather."""
    for x0, x1, z0, z1, colour in patches:
        if not (x0 <= a < x1 and z0 <= z < z1):
            continue
        ea = min(a - x0, x1 - a)
        ez = min(z - z0, z1 - z)
        if ea < t * 1.5 and ez < t * 1.5:
            return STEEL_MID
        if ea < t or ez < t:
            k = math.floor(a / t) + math.floor(z / t)
            return mix(SEAM, THREAD, 0.3) if k % 3 == 0 else SEAM
        return colour
    return LEATHER[0] if math.floor(z / (14 * t)) % 2 else mix(LEATHER[0], LEATHER[1], 0.35)


def paint_vest(p, n, rgb, pc, t):
    if near(rgb, PAL["leather_dark"]):        # crossbelt
        i, j, k = cell(p, t)
        c = STRAP
        if (i + j + k) % 11 == 0:
            c = mix(STRAP, THREAD, 0.30)      # stitch dots
        return grain(c, p, t, 0.05)
    if near(rgb, PAL["steel"]):               # crossbelt buckle: steel frame, dark centre, prong
        ang = math.atan2(1.30 - 0.905, -0.19 - 0.25)
        dx, dz = p.x - 0.03, p.z - 1.10
        u = dx * math.cos(ang) + dz * math.sin(ang)
        v = -dx * math.sin(ang) + dz * math.cos(ang)
        if max(abs(u), abs(v)) > 0.026:
            return STEEL_LIGHT
        if abs(v) < 0.008:
            return STEEL_MID
        return STRAP
    if abs(n.z) > 0.7:
        return grain(LEATHER[1], p, t)
    a = side_coord(p, n)
    if n.y < -0.3:
        c = vest_panel(a, p.z, t, VEST_PATCHES_FRONT)
    elif n.y > 0.3:
        c = vest_panel(a, p.z, t, VEST_PATCHES_BACK)
    else:
        c = LEATHER[1]
    # lacing up the front: dark slit and an X of light cord, counted in pixels
    if n.y < -0.5 and 0.87 < p.z < 1.32:
        ix = round(p.x / t)
        r = math.floor(p.z / t) % 6
        if abs(ix) <= 3:
            if abs(abs(ix) - abs(3 - r)) < 1:
                return THREAD
            if ix == 0:
                return S(36, 20, 10)
            return dark(LEATHER[1], 0.85)
    return grain(c, p, t)


def paint_belt(p, n, rgb, pc, t):
    if near(rgb, PAL["steel"]):               # big square buckle: steel frame, open centre, prong
        dx, dz = p.x, p.z - 0.85
        if n.y < -0.5:
            if abs(dx) > 0.040 or abs(dz) > 0.035:
                return STEEL_LIGHT
            if abs(dx) < 0.008:
                return STEEL_MID
            return S(50, 30, 16)
        return STEEL_MID
    if near(rgb, PAL["vest"]):                # keeper and pouch tab
        return grain(LEATHER[2], p, t)
    zi = math.floor(p.z / t)
    i, j, k = cell(p, t)
    c = BELT
    if abs(n.z) < 0.5 and (abs(p.z - 0.81) < t * 0.75 or abs(p.z - 0.89) < t * 0.75) and (i + j) % 2 == 0:
        c = mix(BELT, THREAD, 0.5)            # stitch rows along both edges
    return grain(c, p, t, 0.05) if zi else c


def paint_body(p, n, rgb, pc, t):
    if UNDER and near(rgb, PAL["trousers"]):
        if p.z < UNDER_BRAIES_Z - t:
            return grain(SKIN, p, t, 0.03)
        if p.z < UNDER_BRAIES_Z:
            return LINEN_SEAM
        return grain(SHORTS, p, t, 0.04)
    if near(rgb, PAL["trousers"]):
        c = TROUSERS
        if abs(pc.x) > 0.05 and abs(side_coord(p, n) - pc.x) < 0.5 * t and abs(n.x) > 0.7:
            c = dark(TROUSERS, 0.8)           # outer leg seam
        elif abs(pc.x) > 0.05 and n.y < -0.5 and 0.38 < p.z < 0.53 and abs(p.x - pc.x) < 0.075:
            edge = p.z < 0.38 + t or p.z > 0.53 - t or abs(p.x - pc.x) > 0.075 - t
            return SEAM if edge else grain(KNEE_PAD, p, t, 0.06)
        return grain(c, p, t, 0.06)
    return grain(SKIN, p, t, 0.03)


def paint_head(p, n, rgb, pc, t):
    if near(rgb, PAL["ginger_dark"]):         # brow ridge
        return GINGER_DARK if math.floor(p.x / t) % 3 == 0 else GINGER
    x, z = p.x, p.z
    if n.y < -0.5:
        ax = abs(x)
        if p.y < -0.205:                      # nose block front
            return S(214, 132, 104) if z < 1.535 else SKIN
        if 1.576 <= z < 1.598 and 0.046 <= ax < 0.094:
            if 0.058 <= ax < 0.078:
                return PUPIL if ax < 0.068 and z < 1.590 else IRIS
            return EYE_WHITE
        if 1.598 <= z < 1.608 and 0.042 <= ax < 0.098:
            return mix(SKIN_SHADE, GINGER_DARK, 0.45)   # heavy upper lid under the brow
        if 1.608 <= z < 1.622 and 0.036 <= ax < 0.104:
            return SKIN_SHADE                 # shade under the brow
        return grain(SKIN, p, t, 0.03)
    if abs(n.x) > 0.7 and abs(p.y) < 0.05 and 1.52 < z < 1.61:
        return SKIN_SHADE                     # ears
    return grain(SKIN, p, t, 0.03)


def strands(p, n, t, base, darkc, light):
    """Hair or beard pixels: darker strand columns, a few light ones."""
    if abs(n.z) > 0.7:
        col = math.floor(p.x / t)
        row = math.floor(p.y / t)
    else:
        col = math.floor(side_coord(p, n) / t)
        row = math.floor(p.z / t)
    hsh = h3(col, row // 3, 11)
    # Strands are a soft shade of the base, not full dark and light (smoother, and they no longer shimmer in motion).
    if col % 3 == 0 or hsh < 0.18:
        return mix(base, darkc, 0.55)
    if hsh > 0.9:
        return mix(base, light, 0.5)
    return base


def clumps(p, n, t, base, darkc, light):
    """Hair pixels as chunky locks (model sheet): 2x3 texel clumps, each a shade, with a dark lower lip."""
    if abs(n.z) > 0.7:
        col = math.floor(p.x / t)
        row = math.floor(p.y / t)
    else:
        col = math.floor(side_coord(p, n) / t)
        row = math.floor(p.z / t)
    ci = col // 2
    cj = (row + (ci % 2)) // 3
    hsh = h3(ci, cj, 23)
    c = mix(base, darkc, 0.35) if hsh < 0.3 else mix(base, light, 0.35) if hsh > 0.8 else base
    if abs(n.z) <= 0.7 and (row + (ci % 2)) % 3 == 0:
        return mix(c, darkc, 0.6)
    return c


def paint_hair(p, n, rgb, pc, t):
    base = mix(GINGER, GINGER_DARK, 0.35) if near(rgb, PAL["ginger_dark"]) else GINGER
    return clumps(p, n, t, base, GINGER_DARK, GINGER_LIGHT)


def paint_beard(p, n, rgb, pc, t):
    if near(rgb, BEAD):
        k = math.floor(p.z / t)
        return S(120, 78, 36) if k % 2 == 0 else S(196, 142, 64)   # carved amber bead
    if near(rgb, MOUSTACHE):
        return strands(p, n, t, MOUSTACHE_C, GINGER, S(244, 150, 72))
    base = GINGER if near(rgb, PAL["ginger"]) else mix(GINGER, GINGER_DARK, 0.45)
    return clumps(p, n, t, mix(base, GINGER_LIGHT, 0.15), GINGER_DARK, GINGER_LIGHT)


def paint_boots(p, n, rgb, pc, t):
    # Knee/shin wraps and the folded cuff lip stay leather on the base layer too
    # (those bands are part of this mesh; painting them as skin made them look like ridges).
    if near(rgb, PAL["boot_wrap"]) or near(rgb, PAL["leather_dark"]):
        if 0.26 < p.z < 0.38:
            if p.z > 0.362 or abs(p.z - 0.320) < t * 0.6:
                return mix(BOOT_WRAP, CORD, 0.35)
        if near(rgb, PAL["leather_dark"]):
            return grain(BOOT_WRAP, p, t, 0.04)
        return grain(BOOT_WRAP, p, t, 0.05)
    if UNDER:                                 # bare feet (shaft and sole of this mesh)
        if p.z < 0.035 or (p.y < -0.20 and p.z < 0.06 and math.floor(p.x / t) % 3 == 0):
            return SKIN_SHADE
        return grain(SKIN, p, t, 0.03)
    if near(rgb, PAL["sole"]):
        return SOLE
    return grain(BOOT, p, t, 0.06)


BLADE_EDGE = [(0.345, 0.725), (0.375, 0.655), (0.398, 0.555), (0.398, 0.455), (0.380, 0.365),
              (0.350, 0.300), (0.310, 0.262)]
BLADE_KNOTS = [(0.232, 0.540, 0.050), (0.282, 0.500, 0.050)]


def seg_dist(x, z, a, b):
    ax, az = a
    bx, bz = b
    dx, dz = bx - ax, bz - az
    k = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)))
    return math.hypot(x - ax - dx * k, z - az - dz * k)


def blade_engraving(x, z, t):
    if x < 0.12:
        return False
    d = min(seg_dist(x, z, a, b) for a, b in zip(BLADE_EDGE, BLADE_EDGE[1:]))
    if abs(d - 0.050) < 0.6 * t and 0.30 < z < 0.70:
        return True
    return any(abs(abs(x - cx) + abs(z - cz) - r) < 0.6 * t for cx, cz, r in BLADE_KNOTS)


def paint_axe_bear(p, n, rgb, pc, t):
    i, j, k = cell(p, t)
    if near(rgb, PAL["leather_dark"]):        # butt wrap
        return WRAP if (k + i + j) % 3 else mix(WRAP, THREAD, 0.3)
    if near(rgb, PAL["steel"]):               # worn light edge, poll face
        return STEEL_LIGHT if (k + i) % 4 else S(226, 230, 234)
    if near(rgb, PAL["steel_dark"]):          # dark iron face, eye and poll
        if abs(n.y) > 0.7 and blade_engraving(p.x, p.z, t):
            return STEEL_MID
        return grain(STEEL_DARK, p, t, 0.10)
    if near(rgb, BEAR_CARVE):
        if p.z > 0.66:
            return BEAR_DARK                  # bear eyes and nose
        return WOOD_GROOVE if k % 3 else WOOD_DARK  # carved spiral band
    if near(rgb, BEAR_HEAD):
        if n.x > 0.7:                         # bear face toward the blade
            if p.x > 0.11 and abs(p.y) < 0.035 and 0.628 <= p.z < 0.650:
                return BEAR_DARK              # mouth
            if p.x > 0.09:
                return BEAR_MUZZLE
            return BEAR
        if n.z > 0.7:
            return mix(BEAR, WOOD_GROOVE, 0.35) if (i + j) % 3 == 0 else BEAR
        if abs(n.y) > 0.7 and p.z > 0.70:
            return BEAR_DARK if abs(p.y) > 0.055 else BEAR   # ears
        return WOOD_DARK if k % 3 == 0 else BEAR   # carved fur rows
    # haft: grain columns, a knot pattern on the carved section between the spiral turns
    a = math.floor(side_coord(p, n) / t)
    if -0.094 < p.z < 0.215:
        return dark(GRIP_WRAP, 0.5) if (k + a) % 4 == 0 else grain(GRIP_WRAP, p, t, 0.06)
    if 0.24 < p.z < 0.48 and ((a + k) % 4 == 0 or (a - k) % 4 == 0):
        return WOOD_GROOVE
    if h3(a, 0, 7) < 0.3:
        return WOOD_DARK
    return grain(WOOD, p, t, 0.05)


def paint_armour(name, p, n, rgb):
    """Armour and the unused double axe: kept sensible, not painted to the concept yet."""
    x, z = p.x, p.z
    if name == "chopper_chainmail":
        t = TEXEL_M
        if z < 0.575 or abs(n.z) > 0.7:
            return S(40, 40, 44)
        k = math.floor(z / t)
        c = math.floor(side_coord(p, n) / t)
        if k % 2:
            return grain(S(62, 60, 60), p, t, 0.08)
        return S(96, 94, 92) if (c + k // 2) % 2 else S(32, 31, 32)
    elif name in ("chopper_plate", "chopper_pauldrons"):
        if (int(abs(x) * 30) + int(z * 30)) % 8 == 0:
            return dark(rgb, 0.62)
    elif name == "chopper_bracers":
        # Model sheet: thick leather cuffs with a steel band at each end and rivets on the bands.
        t = TEXEL_M
        i, j, k = cell(p, t)
        if near(rgb, PAL["steel_dark"]):
            return STEEL_MID if (i + j + k) % 5 == 0 else grain(STEEL_DARK, p, t, 0.08)
        if (i + k) % 7 == 0:
            return mix(LEATHER[1], THREAD, 0.35)
        return grain(LEATHER[1], p, t, 0.06)
    elif name == "chopper_helmet":
        # Geometry carries the skull, band, rivets and horn steps (build_helmet). Paint adds
        # the teeth on the jaw block, dark sockets, and a little grain.
        t = TEXEL_M
        if near(rgb, HELM_BONE):
            if n.y < -0.5 and z < 1.662 and abs(x) < 0.05:
                return S(40, 34, 28) if math.floor(x / t) % 2 else S(226, 214, 186)
            return grain(S(222, 208, 176), p, t, 0.08)
        if near(rgb, PAL["steel_dark"]):
            return S(26, 26, 30) if z > 1.66 and abs(x) < 0.07 and n.y < -0.5 else STEEL_DARK
        if near(rgb, PAL["chain"]):              # mail neck guard: rings in rows
            i, j, k = cell(p, t)
            return S(92, 92, 94) if (i + j + k + (k % 2)) % 2 else S(50, 50, 54)
        return grain(rgb, p, t, 0.06)
    elif name == "chopper_axe":
        if abs(x) > 0.06 and z > 0.40 and abs(n.y) > 0.5 and (int(x * 40) + int(z * 40)) % 5 == 0:
            return dark(rgb, 0.55)
    elif name == "chopper_shield" and abs(n.y) > 0.5 and rgb[0] > rgb[1] > rgb[2] and rgb[0] > 0.15:
        if int(x * 18) % 3 == 0:
            rgb = dark(rgb, 0.82)
        if abs(x) < 0.28 and -0.42 < z < 0.36:
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


PAINTERS = {"chopper_shirt": paint_shirt, "chopper_vest": paint_vest, "chopper_belt": paint_belt,
            "chopper_body": paint_body, "chopper_head": paint_head, "chopper_hair": paint_hair,
            "chopper_beard": paint_beard, "chopper_boots": paint_boots, "chopper_axe_bear": paint_axe_bear}


def detail(name, p, n, rgb, pc=None):
    pc = pc if pc is not None else Vector()
    fn = PAINTERS.get(name)
    if fn is None:
        return paint_armour(name, p, n, rgb)
    return fn(p, n, rgb, pc, texel_m(name, n, pc))
