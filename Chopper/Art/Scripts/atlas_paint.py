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
MOUSTACHE = (0.88, 0.44, 0.13)                       # = build_chopper.MOUSTACHE
BEAR_WOOD, BEAR_CARVE, BEAR_HEAD = (0.72, 0.42, 0.19), (0.42, 0.22, 0.09), (0.80, 0.50, 0.23)  # build_props
SHOULDER_X, SHOULDER_Z = 0.30, 1.27                  # = build_chopper.SHOULDER


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


def grain(rgb, p, t, amount=0.07):
    """Per-pixel value jitter (the pixel-art noise of a block texture)."""
    i, j, k = cell(p, t)
    f = 1.0 + amount * (2.0 * h3(i, j, k) - 1.0)
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
PATCH = [S(126, 122, 116), S(102, 100, 96)]
SEAM = S(58, 34, 18)
THREAD = S(214, 190, 146)
STRAP = S(84, 50, 26)
BELT = S(72, 44, 24)
STEEL_LIGHT = S(196, 200, 205)
STEEL_MID = S(132, 136, 142)
STEEL_DARK = S(70, 72, 78)
TROUSERS = S(70, 46, 30)
BOOT = S(118, 72, 40)
BOOT_WRAP = S(82, 50, 28)
CORD = S(166, 126, 82)
SOLE = S(46, 30, 20)
SKIN = S(228, 168, 132)
SKIN_SHADE = S(200, 138, 106)
CHEEK = S(222, 128, 108)
EYE_WHITE = S(236, 232, 222)
IRIS = S(52, 150, 70)
PUPIL = S(24, 30, 26)
GINGER = S(204, 94, 36)
GINGER_DARK = S(150, 62, 24)
GINGER_LIGHT = S(232, 126, 52)
MOUSTACHE_C = S(226, 116, 46)
WOOD = S(176, 106, 52)
WOOD_DARK = S(136, 80, 38)
WOOD_GROOVE = S(92, 50, 22)
BEAR = S(198, 132, 70)
BEAR_MUZZLE = S(222, 168, 108)
BEAR_DARK = S(40, 24, 14)
WRAP = S(60, 38, 22)


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


def paint_shirt(p, n, rgb, pc, t):
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


def vest_panel(a, z, t):
    """Patchwork panel colour and whether this pixel lies on a panel seam (stitched)."""
    # Concept 2.0: three or four big patches across the front, a few of them grey, sewn with sparse stitches.
    row = math.floor(z / (10 * t))
    shift = 5 if row % 2 else 0
    col = math.floor((math.floor(a / t) + shift) / 11)
    pa = (math.floor(a / t) + shift) % 11
    pz = math.floor(z / t) % 10
    hsh = h3(col, row, 5)
    if hsh < 0.28:
        base = PATCH[0] if hsh < 0.14 else PATCH[1]
    else:
        base = LEATHER[int(hsh * 97) % 3]
    seam = pa == 0 or pz == 0
    return base, seam, (pa if pz == 0 else pz) % 3 == 1


def paint_vest(p, n, rgb, pc, t):
    if near(rgb, PAL["leather_dark"]):        # crossbelt
        i, j, k = cell(p, t)
        c = STRAP
        if (i + j + k) % 5 == 0:
            c = mix(STRAP, THREAD, 0.45)      # stitch dots
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
    base, seam, dot = vest_panel(a, p.z, t)
    c = base
    if seam:
        c = mix(SEAM, THREAD, 0.6) if dot else SEAM
    # lacing up the front: dark slit and an X of light cord, counted in pixels
    if n.y < -0.5 and 0.87 < p.z < 1.32:
        ix = round(p.x / t)
        r = math.floor(p.z / t) % 4
        if abs(ix) <= 2:
            if abs(ix) == abs(2 - r):
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
    if near(rgb, PAL["trousers"]):
        c = TROUSERS
        if abs(pc.x) > 0.05 and abs(side_coord(p, n) - pc.x) < 0.5 * t and abs(n.x) > 0.7:
            c = dark(TROUSERS, 0.8)           # outer leg seam
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
        if 1.570 <= z < 1.606 and 0.040 <= ax < 0.100:
            if 0.052 <= ax < 0.076:
                return PUPIL if ax < 0.064 and z < 1.594 else IRIS
            return EYE_WHITE
        if 1.606 <= z < 1.618 and 0.036 <= ax < 0.104:
            return SKIN_SHADE                 # shade under the brow
        if 1.520 <= z < 1.560 and 0.064 <= ax < 0.124:
            return CHEEK
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
    if col % 3 == 0 or hsh < 0.18:
        return darkc
    if hsh > 0.9:
        return light
    return base


def paint_hair(p, n, rgb, pc, t):
    return strands(p, n, t, GINGER, GINGER_DARK, GINGER_LIGHT)


def paint_beard(p, n, rgb, pc, t):
    if near(rgb, MOUSTACHE):
        return strands(p, n, t, MOUSTACHE_C, GINGER, S(244, 150, 72))
    base = GINGER if near(rgb, PAL["ginger"]) else mix(GINGER, GINGER_DARK, 0.45)
    return strands(p, n, t, base, GINGER_DARK, GINGER_LIGHT)


def paint_boots(p, n, rgb, pc, t):
    if near(rgb, PAL["sole"]):
        return SOLE
    i, j, k = cell(p, t)
    if near(rgb, PAL["boot_wrap"]):
        a = math.floor(side_coord(p, n) / t)
        if (a + k) % 7 == 0 or (a - k) % 7 == 0:
            return mix(BOOT_WRAP, CORD, 0.6)  # crossed binding on the wrap bands
        return grain(BOOT_WRAP, p, t)
    return grain(BOOT, p, t, 0.06)


def paint_axe_bear(p, n, rgb, pc, t):
    i, j, k = cell(p, t)
    if near(rgb, PAL["leather_dark"]):        # butt wrap
        return WRAP if (k + i + j) % 3 else mix(WRAP, THREAD, 0.3)
    if near(rgb, PAL["steel"]):               # worn light edge, poll face
        return STEEL_LIGHT if (k + i) % 4 else S(226, 230, 234)
    if near(rgb, PAL["steel_dark"]):          # dark iron face, eye and poll
        return grain(STEEL_DARK, p, t, 0.10)
    if near(rgb, BEAR_CARVE):
        if p.z > 0.66:
            return BEAR_DARK                  # bear eyes and nose
        return WOOD_GROOVE if k % 3 else WOOD_DARK  # carved spiral band
    if near(rgb, BEAR_HEAD):
        if n.x > 0.7:                         # bear face toward the blade
            if p.x > 0.075 and abs(p.y) < 0.03 and 0.648 <= p.z < 0.660:
                return BEAR_DARK              # mouth
            if p.x > 0.075:
                return BEAR_MUZZLE
            return BEAR
        if n.z > 0.7:
            return mix(BEAR, WOOD_GROOVE, 0.35) if (i + j) % 3 == 0 else BEAR
        return WOOD_DARK if k % 3 == 0 else BEAR   # carved fur rows
    # haft: grain columns, a knot pattern on the carved section between the spiral turns
    a = math.floor(side_coord(p, n) / t)
    if 0.24 < p.z < 0.48 and ((a + k) % 4 == 0 or (a - k) % 4 == 0):
        return WOOD_GROOVE
    if h3(a, 0, 7) < 0.3:
        return WOOD_DARK
    return grain(WOOD, p, t, 0.05)


def paint_armour(name, p, n, rgb):
    """Armour and the unused double axe: kept sensible, not painted to the concept yet."""
    x, z = p.x, p.z
    if name == "chopper_chainmail":
        if (int(x * 45) + int(z * 45)) % 2 == 0:
            return dark(rgb, 0.72)
    elif name in ("chopper_plate", "chopper_pauldrons"):
        if (int(abs(x) * 30) + int(z * 30)) % 8 == 0:
            return dark(rgb, 0.62)
    elif name == "chopper_bracers":
        if abs((z * 36) - round(z * 36)) < 0.08:
            return dark(rgb, 0.62)
    elif name == "chopper_helmet" and n.y < -0.35 and abs(x) < 0.12 and 1.48 < z < 1.66:
        if 1.56 < z < 1.64 and 0.03 < abs(x) < 0.08:
            return (0.12, 0.12, 0.13)
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
