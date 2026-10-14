"""Crypt and mine dressing in the voxel style of build_world_kit.py.

Run:  blender -b -P build_cave_kit.py
Out:  Art/Blender/cave_kit.blend
      Art/Export/prop_<piece>_voxel.fbx
      Art/Scripts/cave_kit_manifest.json   (bounds, walking line, slots — for the wiring pass)
      Art/Renders/cave_kit_check_report.txt, cave_kit_*_sheet.png, cave_kit_*_corner.png, cave_kit_<piece>.png

1 unit = 1 m. Floor props sit on Z = 0. Fronts face Blender -Y, which is local +Z after the
FBX export (forward -Z, up Y), same as the furniture kit. Vertex colour, material M_World.

Sized to the block rooms (do not treat these as guesses):

  CryptPlan  RoomSize 15, RoomHeight 6, WallThickness 0.6, CorridorWidth 5, DoorHeight 4.4
  MinePlan   RoomSize 16, RoomHeight 6.5, WallThickness 0.8, GalleryWidth 5.5, GalleryHeight 4.5
             PostSize 0.32, BeamDepth 0.36, LanternHeight 2.3
             VeinMinHeight 0.9, VeinMaxHeight 1.6
             gallery floor length = CellSize 24 - RoomSize 16 - 2*WallThickness 0.8 = 6.4
  Arena      ArenaSize 26, ArenaHeight 9, ArenaSpawnRadius 9 (private on CryptPlan)

Walking line: the door-to-door cross |x| < clear_half and |z| < clear_half
(clear_half = CorridorWidth/2 = 2.5 in a crypt, GalleryWidth/2 = 2.75 in a mine).
The manifest only keeps slots whose solid cells stay off that cross below 2.2 m.
Rails and floor tiles may lie on it; they are not colliders.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit  # noqa: E402

ART = kit.ART
EXPORT = kit.EXPORT
RENDERS = kit.RENDERS
BLEND = os.path.join(ART, "Blender", "cave_kit.blend")
REPORT = os.path.join(RENDERS, "cave_kit_check_report.txt")
MANIFEST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cave_kit_manifest.json")

CRYPT_HALF, CRYPT_CLEAR, CRYPT_H = 7.5, 2.5, 6.0
MINE_HALF, MINE_CLEAR, MINE_H = 8.0, 2.75, 6.5
ARENA_HALF, ARENA_CLEAR, ARENA_H = 13.0, 9.0, 9.0
HEAD = 2.2

STONE = kit.srgb(118, 124, 132)
STONE_D = kit.srgb(72, 76, 84)
STONE_L = kit.srgb(176, 180, 186)
MOSS = kit.srgb(78, 110, 52)
MOSS_D = kit.srgb(46, 72, 34)
BONE = kit.srgb(214, 204, 180)
BONE_D = kit.srgb(160, 140, 112)
WEB = kit.srgb(210, 210, 204)
ROOT = kit.srgb(96, 68, 40)
ROOT_D = kit.srgb(64, 44, 28)
SOOT = kit.srgb(42, 36, 32)
PLASTER = kit.srgb(196, 190, 176)
PLASTER_D = kit.srgb(150, 146, 136)
EMBER = kit.EMBER
EMBER_HOT = kit.EMBER_HOT
IRON = kit.IRON
IRON_L = kit.IRON_LIGHT
LOG = kit.LOG
LOG_D = kit.LOG_DARK
LOG_E = kit.LOG_END
MINE_ROCK = kit.srgb(140, 122, 102)
MINE_ROCK_D = kit.srgb(96, 82, 68)
GOLD = kit.srgb(214, 168, 56)
GOLD_D = kit.srgb(150, 100, 36)
AQUA = kit.srgb(90, 196, 186)
AMBER = kit.srgb(214, 140, 48)


def rock(ix, iy, iz):
    return shade(ix, iy, iz, STONE, STONE_D, STONE_L)


def mine_rock(ix, iy, iz):
    return shade(ix, iy, iz, MINE_ROCK, MINE_ROCK_D, STONE_L)


def wood(ix, iy, iz):
    return LOG_E if (ix + iy + iz) % 5 == 0 else (LOG_D if iz % 2 else LOG)


def shade(ix, iy, iz, base, dark, light):
    n = kit.hsh(ix, iy, iz) % 8
    if n < 2:
        return dark
    if n > 5:
        return light
    return base


def fill(out, x0, x1, y0, y1, z0, z1, col):
    for ix in range(x0, x1):
        for iy in range(y0, y1):
            for iz in range(z0, z1):
                out[(ix, iy, iz)] = col(ix, iy, iz) if callable(col) else col


def put(out, ix, iy, iz, col):
    out[(ix, iy, iz)] = col


# ----------------------------------------------------------------------------------------------- crypt
def panel_cells(style):
    out = {}
    for ix in range(-5, 5):
        for iz in range(0, 14):
            out[(ix, -1, iz)] = rock(ix, -1, iz)
            if ix in (-5, 4) or iz in (0, 1, 12, 13):
                out[(ix, -2, iz)] = STONE_L
    if style == "carved":
        for ix in range(-3, 3):
            for iz in range(4, 11):
                if abs(ix + 0.5) + abs(iz - 7) <= 3.6:
                    out[(ix, -2, iz)] = STONE_D if (ix + iz) % 2 == 0 else STONE
        for ix in range(-3, 3):
            out[(ix, -2, 4)] = STONE_L
            out[(ix, -2, 10)] = STONE_L
        for iz in range(4, 11):
            out[(-3, -2, iz)] = STONE_L
            out[(2, -2, iz)] = STONE_L
    elif style == "moss":
        for ix in range(-5, 5):
            for iz in range(0, 6):
                if kit.hsh(ix, iz, 2) % 3 != 2:
                    out[(ix, -2, iz)] = MOSS if kit.hsh(ix, iz, 3) % 2 == 0 else MOSS_D
    elif style == "crack":
        for iz in range(1, 13):
            ix = (iz % 3) - 2
            out.pop((ix, -1, iz), None)
            out[(ix, -2, iz)] = PLASTER if iz % 2 == 0 else PLASTER_D
            if iz % 4 == 0:
                out.pop((ix + 1, -1, iz), None)
                out[(ix + 1, -2, iz)] = PLASTER_D
    elif style == "scorch":
        for ix in range(-4, 4):
            for iz in range(1, 13):
                if kit.hsh(ix, iz, 4) % 4 == 0:
                    out[(ix, -1, iz)] = SOOT
                if kit.hsh(ix, iz, 5) % 9 == 0:
                    out[(ix, -2, iz)] = EMBER if kit.hsh(ix, iz, 6) % 2 == 0 else EMBER_HOT
    out.pop((-5, -1, 13), None)
    out.pop((-5, -2, 13), None)
    out.pop((4, -1, 0), None)
    return out


def trim_cells():
    out = {}
    for ix in range(-4, 4):
        out[(ix, -1, 0)] = STONE_L if ix % 2 == 0 else STONE
        if ix % 2 == 0:
            out[(ix, -2, 0)] = STONE
            out[(ix, -2, 1)] = STONE_L
    return out


def arch_cells():
    """Legs outside |x| = 3.0 m. Rectangular clear 6.0 m wide and 4.5 m tall, above the 5.0 x 4.4 door."""
    out = {}
    for iz in range(0, 18):
        for ix in list(range(-14, -12)) + list(range(12, 14)):
            for iy in range(-2, 0):
                out[(ix, iy, iz)] = STONE_L if iz % 4 == 0 and iy == -2 else rock(ix, iy, iz)
    for iz, inner in ((18, 10), (19, 8), (20, 6), (21, 3), (22, 0)):
        for ix in range(-14, 14):
            if -inner <= ix < inner:
                continue
            for iy in range(-2, 0):
                keystone = abs(ix) <= 1 and iz >= 21
                out[(ix, iy, iz)] = STONE_L if keystone else rock(ix, iy, iz)
    for iz in range(2, 16, 3):
        for ix in (-14, 13):
            out[(ix, -3, iz)] = STONE_D
    return out


def pillar_cells(top):
    out = {}
    fill(out, -3, 3, -3, 3, 0, 1, rock)
    fill(out, -2, 2, -2, 2, 1, 2, rock)
    shaft = top - 3
    fill(out, -2, 2, -2, 2, 2, shaft, rock)
    for iz in range(4, shaft, 3):
        out[(-1, -3, iz)] = STONE_D
        out[(0, -3, iz)] = STONE_D
    fill(out, -2, 2, -2, 2, shaft, shaft + 1, lambda x, y, z: STONE_L)
    fill(out, -3, 3, -3, 3, shaft + 1, shaft + 2, lambda x, y, z: STONE_L)
    fill(out, -2, 2, -2, 2, shaft + 2, top, rock)
    return out


def broken_pillar_cells():
    out = {}
    fill(out, -3, 3, -3, 3, 0, 1, rock)
    fill(out, -2, 2, -2, 2, 1, 2, rock)
    for ix in range(-2, 2):
        for iy in range(-2, 2):
            height = 6 + kit.hsh(ix, iy, 1) % 5
            for iz in range(2, height):
                out[(ix, iy, iz)] = STONE_D if iz == height - 1 else rock(ix, iy, iz)
    for ix in range(-4, 4):
        for iy in range(-4, 4):
            if max(abs(ix), abs(iy)) <= 2:
                continue
            if kit.hsh(ix, iy, 8) % 3 == 0:
                continue
            for iz in range(kit.hsh(ix, iy, 9) % 3):
                out[(ix, iy, iz)] = rock(ix, iy, iz)
    return out


def sarcophagus_cells():
    out = {}
    fill(out, -5, 5, -2, 2, 0, 3, rock)
    for iz in range(1, 3):
        for ix in range(-3, 3):
            out[(ix, -2, iz)] = STONE_D
            out[(ix, 1, iz)] = STONE_D
    fill(out, -5, 5, -2, 2, 3, 4, lambda x, y, z: STONE_L)
    fill(out, -4, 4, -1, 1, 4, 5, rock)
    for ix in range(-1, 1):
        out[(ix, 0, 4)] = STONE_D
        out[(ix, -1, 4)] = STONE_D
    for iy in range(-1, 1):
        out[(0, iy, 4)] = STONE_D
        out[(-1, iy, 4)] = STONE_D
    return out


def bone_cells():
    out = {}
    fill(out, -1, 2, -1, 2, 1, 4, lambda x, y, z: BONE)
    out[(0, -2, 2)] = BONE_D
    out[(1, -2, 2)] = BONE_D
    bones = (
        (-5, -1, 0, 1, 0, 0, 4),
        (2, 1, 0, 1, 1, 0, 4),
        (-3, 2, 0, 1, -1, 0, 5),
        (0, -4, 0, 0, 1, 0, 3),
        (-2, 0, 1, 1, 0, 1, 3),
        (3, -2, 0, 0, 1, 1, 3),
    )
    for x, y, z, dx, dy, dz, n in bones:
        for k in range(n):
            col = BONE if k % 2 == 0 else BONE_D
            out[(x + dx * k, y + dy * k, z + dz * k)] = col
    return out


def urn_cells():
    out = {}
    layers = ((0, 1, 2), (1, 4, 2), (4, 6, 1), (6, 7, 2))
    for z0, z1, half in layers:
        fill(out, -half, half, -half, half, z0, z1, lambda x, y, z: shade(x, y, z, kit.srgb(120, 86, 62), kit.srgb(78, 54, 40), kit.srgb(156, 116, 82)))
    out[(0, -2, 3)] = STONE_D
    return out


def chain_cells():
    out = {}
    for i in range(18):
        iz = -1 - i
        ox = i % 2
        out[(ox, 0, iz)] = IRON
        out[(ox, 1, iz)] = IRON_L
        if i % 2 == 0:
            out[(ox + 1, 0, iz)] = IRON
    return out


def cobweb_cells():
    """Hangs from Z = 0 toward Blender -X and +Y, which is Unity -X and -Z."""
    out = {}
    for i in range(12):
        out[(-i, 0, -1)] = WEB
        out[(0, i, -1)] = WEB
        if i > 2:
            out[(-i, i, -2)] = WEB
        if i % 3 == 0:
            for k in range(i):
                out[(-k, i - k, -2)] = WEB
    return out


def root_cells():
    out = {}
    arms = (
        (-1, 2, -5, -1, 0, 4),
        (0, 2, -2, 1, 1, 8),
        (-3, 0, -4, -1, 0, 6),
        (1, 3, -4, -2, 2, 7),
        (-2, 1, -1, 1, 3, 5),
    )
    for x0, x1, y0, y1, z0, z1 in arms:
        for ix in range(x0, x1):
            for iy in range(y0, y1):
                for iz in range(z0, z1):
                    out[(ix, iy, iz)] = ROOT_D if iz == z0 else ROOT
    for ix in range(-2, 2):
        for iy in range(-2, 1):
            if kit.hsh(ix, iy, 7) % 2 == 0:
                out[(ix, iy, 0)] = MOSS_D
                out[(ix, iy, 1)] = MOSS
    return out


def rubble_cells(timber=False):
    out = {}
    col = mine_rock if timber else rock
    for ix in range(-3, 4):
        for iy in range(-3, 3):
            if kit.hsh(ix, iy, 11) % 4 == 0:
                continue
            height = 1 + kit.hsh(ix, iy, 12) % 3
            for iz in range(height):
                out[(ix, iy, iz)] = col(ix, iy, iz)
    if timber:
        fill(out, -2, 3, -1, 1, 1, 2, wood)
        out[(3, 0, 2)] = LOG_D
        out[(4, 0, 2)] = LOG_E
    return out


def floor_cells(moss):
    out = {}
    for ix in range(-4, 4):
        for iy in range(-4, 4):
            crack = abs(ix + iy) % 7 == 0 or (ix == 1 and iy > -1)
            if moss and kit.hsh(ix, iy, 6) % 3 == 0:
                out[(ix, iy, 0)] = MOSS if kit.hsh(ix, iy, 7) % 2 == 0 else MOSS_D
            elif crack:
                out[(ix, iy, 0)] = STONE_D
            else:
                out[(ix, iy, 0)] = rock(ix, iy, 0)
    return out


def drip_cells():
    out = {}
    for i, half in enumerate((1, 1, 1, 0, 0, 0, 0, 0)):
        iz = -1 - i
        for ix in range(-half, half + 1):
            for iy in range(-half, half + 1):
                out[(ix, iy, iz)] = STONE_L if i > 5 else STONE
    return out


def flow_cells():
    out = {}
    fill(out, -2, 2, -2, 2, 0, 1, lambda x, y, z: STONE_L)
    fill(out, -1, 1, -1, 1, 1, 3, lambda x, y, z: STONE)
    out[(0, 0, 3)] = STONE_L
    for ix in range(-2, 2):
        out[(ix, -2, 0)] = MOSS
    return out


# ----------------------------------------------------------------------------------------------- mine
def timber_frame_cells():
    """Wall bay, 3.0 m wide and 4.0 m tall, 0.5 m deep. Stands against a solid wall."""
    out = {}
    for x0 in (-6, 4):
        fill(out, x0, x0 + 2, -2, 0, 0, 14, wood)
        for iz in range(0, 14):
            out[(x0 if x0 < 0 else x0 + 1, -1, iz)] = LOG_E
    fill(out, -6, 6, -2, 0, 14, 16, wood)
    for i in range(6):
        out[(-4 + i, -2, 2 + i * 2)] = LOG_D
        out[(-4 + i, -1, 2 + i * 2)] = LOG
    return out


def gallery_frame_cells():
    """Walk-through collar. Clear opening 6.0 m by 4.5 m, outside the 5.5 x 4.5 gallery."""
    out = {}
    for x0 in (-14, 12):
        fill(out, x0, x0 + 2, -2, 0, 0, 18, wood)
    fill(out, -14, 14, -2, 0, 18, 20, wood)
    for ix in (-14, 13):
        for iz in range(0, 18):
            out[(ix, -1, iz)] = LOG_E
    return out


def rail_cells():
    """6.4 m gallery module. Rails centred at x = ±0.35 m, 0.20 m tall. Cell 0.1 m."""
    out = {}
    for iy in range(-32, 32):
        if iy % 6 == 0:
            for ix in range(-5, 5):
                out[(ix, iy, 0)] = LOG_D
        for ix in list(range(-4, -3)) + list(range(3, 4)):
            out[(ix, iy, 1)] = IRON_L if iy % 8 == 0 else IRON
    return out


def cart_cells():
    """1.6 m along Y, 1.0 m wide, about 1.05 m tall. Cell 0.1 m. Park off the centre line."""
    out = {}
    fill(out, -5, 5, -8, 8, 4, 6, wood)
    fill(out, -5, 5, -8, -7, 6, 10, wood)
    fill(out, -5, 5, 7, 8, 6, 10, wood)
    fill(out, -5, -4, -8, 8, 6, 9, wood)
    fill(out, 4, 5, -8, 8, 6, 9, wood)
    for ix in (-5, 4):
        for iy in (-7, 6):
            fill(out, ix, ix + 1, iy, iy + 1, 6, 10, lambda x, y, z: IRON)
    for ix in (-5, 2):
        for iy in (-7, 4):
            fill(out, ix, ix + 3, iy, iy + 3, 0, 3, lambda x, y, z: IRON_L if z == 1 else IRON)
    fill(out, -5, 5, -8, 8, 9, 10, lambda x, y, z: IRON)
    return out


def ore_cells():
    out = {}
    fill(out, -5, 5, -3, 0, -4, 4, mine_rock)
    nuggets = (
        (-3, -4, -1, GOLD), (2, -4, 1, GOLD_D), (0, -4, 0, AQUA),
        (-1, -4, 2, AMBER), (3, -4, -2, GOLD), (-4, -3, 1, AQUA),
    )
    for ix, iy, iz, col in nuggets:
        out[(ix, iy, iz)] = col
    for ix in range(-2, 3):
        out[(ix, -2, 0)] = SOOT
    return out


def lantern_cells():
    """Open cage, flame on the front face. Everything is at or above the mount."""
    out = {}
    for iy in range(-3, 1):
        out[(0, iy, 2)] = IRON_L
    for iz in range(1, 5):
        out[(-1, -4, iz)] = IRON_L
        out[(1, -4, iz)] = IRON_L
        out[(-1, -2, iz)] = IRON
        out[(1, -2, iz)] = IRON
        out[(0, -4, iz)] = EMBER_HOT if iz in (2, 3) else IRON_L
    for ix in (-1, 0, 1):
        out[(ix, -4, 1)] = IRON
        out[(ix, -4, 4)] = IRON_L
    out[(0, -3, 2)] = EMBER
    out[(0, -3, 3)] = EMBER_HOT
    return out


def crate_cells():
    out = {}
    fill(out, -3, 3, -3, 3, 0, 5, wood)
    for ix in range(-3, 3):
        out[(ix, -3, 2)] = LOG_D
        out[(ix, 2, 3)] = LOG_D
    fill(out, -3, 3, -3, 3, 5, 6, lambda x, y, z: LOG_E if (x + y) % 2 == 0 else LOG)
    out.pop((-3, -3, 5), None)
    return out


def barrel_cells():
    out = {}
    for ix in range(-3, 3):
        for iy in range(-3, 3):
            if abs(ix) >= 2 and abs(iy) >= 2:
                continue
            for iz in range(0, 8):
                band = iz in (1, 6) and (abs(ix) >= 1 or abs(iy) >= 1)
                out[(ix, iy, iz)] = IRON if band else wood(ix, iy, iz)
    return out


def crystal_cells():
    out = {}
    shards = ((0, 0, 6, AQUA), (2, -1, 4, AMBER), (-2, 1, 5, kit.srgb(150, 230, 220)))
    for ox, oy, height, col in shards:
        for iz in range(height):
            out[(ox, oy, iz)] = col
            if iz < height - 1:
                out[(ox + 1, oy, iz)] = col
    return out


# ----------------------------------------------------------------------------------------------- catalogue
# kind: obstacle (solid, corner only), skin (wall, beside the door), frame (hollow opening),
# floor (no collider), overhead (ceiling, above 2.2 m), path (rail, no collider), mount (lantern).
Q = 0.25
F = 0.125
T = 0.1

SPECS = [
    ("prop_crypt_wall_panel_voxel", lambda: panel_cells("carved"), (Q, Q, Q), "skin", "crypt", 0.0, 6000),
    ("prop_crypt_wall_moss_voxel", lambda: panel_cells("moss"), (Q, Q, Q), "skin", "crypt", 0.0, 6000),
    ("prop_crypt_wall_crack_voxel", lambda: panel_cells("crack"), (Q, Q, Q), "skin", "crypt", 0.0, 6000),
    ("prop_crypt_wall_scorch_voxel", lambda: panel_cells("scorch"), (Q, Q, Q), "skin", "crypt", 0.0, 6000),
    ("prop_crypt_trim_voxel", trim_cells, (Q, Q, Q), "skin", "crypt", 0.0, 800),
    ("prop_crypt_arch_voxel", arch_cells, (Q, Q, Q), "frame", "crypt", 0.0, 12000),
    ("prop_crypt_pillar_voxel", lambda: pillar_cells(23), (Q, Q, Q), "obstacle", "crypt", 0.0, 12000),
    ("prop_crypt_pillar_broken_voxel", broken_pillar_cells, (Q, Q, Q), "obstacle", "crypt", 0.0, 8000),
    ("prop_crypt_pillar_arena_voxel", lambda: pillar_cells(33), (Q, Q, Q), "obstacle", "arena", 0.0, 14000),
    ("prop_crypt_sarcophagus_voxel", sarcophagus_cells, (Q, Q, Q), "obstacle", "crypt", 0.0, 4000),
    ("prop_crypt_bone_pile_voxel", bone_cells, (F, F, F), "obstacle", "crypt", 0.0, 2000),
    ("prop_crypt_urn_voxel", urn_cells, (F, F, F), "obstacle", "crypt", 0.0, 2000),
    ("prop_crypt_chain_voxel", chain_cells, (F, F, F), "overhead", "crypt", CRYPT_H, 800),
    ("prop_crypt_cobweb_voxel", cobweb_cells, (F, F, F), "overhead", "crypt", CRYPT_H, 800),
    ("prop_crypt_roots_voxel", root_cells, (Q, Q, Q), "obstacle", "crypt", 0.0, 4000),
    ("prop_crypt_rubble_voxel", lambda: rubble_cells(False), (Q, Q, Q), "obstacle", "crypt", 0.0, 4000),
    ("prop_crypt_floor_cracked_voxel", lambda: floor_cells(False), (Q, Q, F), "floor", "crypt", 0.02, 2000),
    ("prop_crypt_floor_moss_voxel", lambda: floor_cells(True), (Q, Q, F), "floor", "crypt", 0.02, 2000),
    ("prop_crypt_dripstone_voxel", drip_cells, (F, F, F), "overhead", "crypt", CRYPT_H, 800),
    ("prop_crypt_flowstone_voxel", flow_cells, (F, F, F), "obstacle", "crypt", 0.0, 1500),
    ("prop_mine_timber_frame_voxel", timber_frame_cells, (Q, Q, Q), "skin", "mine", 0.0, 6000),
    ("prop_mine_gallery_frame_voxel", gallery_frame_cells, (Q, Q, Q), "frame", "mine", 0.0, 8000),
    ("prop_mine_rail_voxel", rail_cells, (T, T, T), "path", "mine", 0.02, 8000),
    ("prop_mine_cart_voxel", cart_cells, (T, T, T), "obstacle", "mine", 0.0, 6000),
    ("prop_mine_ore_voxel", ore_cells, (F, F, F), "skin", "mine", 1.25, 4000),
    ("prop_mine_lantern_voxel", lantern_cells, (F, F, F), "mount", "mine", 2.3, 1500),
    ("prop_mine_crate_voxel", crate_cells, (F, F, F), "obstacle", "mine", 0.0, 2000),
    ("prop_mine_barrel_voxel", barrel_cells, (F, F, F), "obstacle", "mine", 0.0, 2500),
    ("prop_mine_rubble_voxel", lambda: rubble_cells(True), (Q, Q, Q), "obstacle", "mine", 0.0, 4000),
    ("prop_mine_crystal_voxel", crystal_cells, (F, F, F), "obstacle", "mine", 0.0, 1500),
]

OPENING = {
    "prop_crypt_arch_voxel": (-12, 12, 0, 18, 6.0, 4.5, 5.0, 4.4),
    "prop_mine_gallery_frame_voxel": (-12, 12, 0, 18, 6.0, 4.5, 5.5, 4.5),
}


def unity_of(x, y, z):
    return (x, z, -y)


def rotate_xz(x, z, yaw):
    r = math.radians(yaw)
    c, s = math.cos(r), math.sin(r)
    return (x * c + z * s, -x * s + z * c)


def hits_cross(x0, x1, z0, z1, clear, half):
    def ov(a0, a1, b0, b1):
        return a0 < b1 - 1e-4 and a1 > b0 + 1e-4
    along_z = ov(x0, x1, -clear, clear) and ov(z0, z1, -half, half)
    along_x = ov(z0, z1, -clear, clear) and ov(x0, x1, -half, half)
    return along_z or along_x


def cell_blocks(cells, size, pos, yaw, clear, half):
    sx, sy, sz = size
    px, py, pz = pos
    for ix, iy, iz in cells:
        corners = []
        min_y = None
        for cx in (ix * sx, (ix + 1) * sx):
            for cy in (iy * sy, (iy + 1) * sy):
                for cz in (iz * sz, (iz + 1) * sz):
                    ux, uy, uz = unity_of(cx, cy, cz)
                    rx, rz = rotate_xz(ux, uz, yaw)
                    wx, wy, wz = px + rx, py + uy, pz + rz
                    corners.append((wx, wz))
                    min_y = wy if min_y is None else min(min_y, wy)
        if min_y >= HEAD:
            continue
        xs = [p[0] for p in corners]
        zs = [p[1] for p in corners]
        if hits_cross(min(xs), max(xs), min(zs), max(zs), clear, half):
            return True
    return False


def room_of(name_room):
    if name_room == "mine":
        return MINE_HALF, MINE_CLEAR, MINE_H
    if name_room == "arena":
        return ARENA_HALF, ARENA_CLEAR, ARENA_H
    return CRYPT_HALF, CRYPT_CLEAR, CRYPT_H


def unity_bounds(obj):
    xs, ys, zs = [], [], []
    for v in obj.data.vertices:
        ux, uy, uz = unity_of(v.co.x, v.co.y, v.co.z)
        xs.append(ux)
        ys.append(uy)
        zs.append(uz)
    return (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))


def corner_slots(bounds, half, margin, y):
    x0, x1, _, _, z0, z1 = bounds
    slots = []
    for sx in (1, -1):
        for sz in (1, -1):
            px = (half - margin - x1) if sx > 0 else (-(half - margin) - x0)
            pz = (half - margin - z1) if sz > 0 else (-(half - margin) - z0)
            slots.append({"slot": "corner", "pos": [round(px, 3), y, round(pz, 3)], "yaw": 0})
    return slots


def skin_slots(bounds, half, margin, y):
    x0, x1, _, _, z0, z1 = bounds
    # Back face is local z = z0 (Blender y = 0). Front extends toward +Z.
    slots = []
    px = half - z0
    for sign in (1, -1):
        pz = (half - margin - x1) if sign > 0 else (-(half - margin) - x0)
        slots.append({"slot": "wall", "wall": "+x", "pos": [round(px, 3), y, round(pz, 3)], "yaw": -90})
    px = -half - z0
    for sign in (1, -1):
        # yaw 90: world z = pz - local x
        pz = (half - margin + x0) if sign > 0 else (-(half - margin) + x1)
        slots.append({"slot": "wall", "wall": "-x", "pos": [round(px, 3), y, round(pz, 3)], "yaw": 90})
    pz = half - z0
    for sign in (1, -1):
        # yaw 180: world x = px - local x
        px = (half - margin + x0) if sign > 0 else (-(half - margin) + x1)
        slots.append({"slot": "wall", "wall": "+z", "pos": [round(px, 3), y, round(pz, 3)], "yaw": 180})
    pz = -half - z0
    for sign in (1, -1):
        px = (half - margin - x1) if sign > 0 else (-(half - margin) - x0)
        slots.append({"slot": "wall", "wall": "-z", "pos": [round(px, 3), y, round(pz, 3)], "yaw": 0})
    return slots


def frame_slots(half, y):
    return [
        {"slot": "door", "wall": "+x", "pos": [half, y, 0.0], "yaw": -90},
        {"slot": "door", "wall": "-x", "pos": [-half, y, 0.0], "yaw": 90},
        {"slot": "door", "wall": "+z", "pos": [0.0, y, half], "yaw": 180},
        {"slot": "door", "wall": "-z", "pos": [0.0, y, -half], "yaw": 0},
    ]


def propose_slots(kind, room, bounds, mount_y):
    half, _, _ = room_of(room)
    margin = 0.15
    y = mount_y
    if kind in ("obstacle", "overhead"):
        return corner_slots(bounds, half, margin, y)
    if kind == "skin":
        return skin_slots(bounds, half, margin, y)
    if kind == "frame":
        return frame_slots(half, y)
    if kind == "floor":
        slots = [{"slot": "floor", "pos": [0.0, y, 0.0], "yaw": 0}]
        slots += corner_slots(bounds, half, margin, y)
        return slots
    if kind == "path":
        return [
            {"slot": "gallery", "pos": [0.0, y, 0.0], "yaw": 0},
            {"slot": "gallery", "pos": [0.0, y, 0.0], "yaw": 90},
        ]
    if kind == "mount":
        # MinePlan lanterns sit on the chamber posts. PostSize 0.32, LanternHeight 2.3.
        inset = half - 0.32 - 0.05
        return [
            {"slot": "post", "pos": [0.0, y, round(inset, 3)], "yaw": 180},
            {"slot": "post", "pos": [0.0, y, round(-inset, 3)], "yaw": 0},
            {"slot": "post", "pos": [round(inset, 3), y, 0.0], "yaw": -90},
            {"slot": "post", "pos": [round(-inset, 3), y, 0.0], "yaw": 90},
        ]
    return []


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    try:
        mat.use_nodes = True
    except Exception:
        pass

    built = []
    for name, cells_fn, size, kind, room, mount_y, tri_cap in SPECS:
        cells = cells_fn()
        obj = kit.build(name, cells, mat, size=size)
        built.append({
            "name": name, "obj": obj, "cells": cells, "size": size,
            "kind": kind, "room": room, "mount_y": mount_y, "tri_cap": tri_cap,
        })
        b = kit.bounds(obj)
        print("built %s tris %d size %.3f x %.3f x %.3f" % (
            name, kit.tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))

    lines = ["Cave kit check", "=" * 60]
    ok = True

    def add(label, passed, detail=""):
        nonlocal ok
        ok = ok and passed
        lines.append("  [%s] %s%s" % ("PASS" if passed else "FAIL", label, (" - " + detail) if detail else ""))

    meshes = [b["obj"] for b in built]
    add("%d meshes" % len(SPECS), len([o for o in bpy.data.objects if o.type == "MESH"]) == len(SPECS))
    imgs = [im.name for im in bpy.data.images if im.name not in ("Render Result", "Viewer Node")]
    add("no images in the file", not imgs, ", ".join(imgs))
    mats = sorted({m.name for o in meshes for m in o.data.materials if m})
    add("one shared material M_World", mats == ["M_World"], ", ".join(mats))

    pieces = []
    for item in built:
        obj = item["obj"]
        name = item["name"]
        me = obj.data
        tris = kit.tris_of(obj)
        loc, rot, scl = obj.location, obj.rotation_euler, obj.scale
        add("%s transforms applied" % name,
            loc.length < 1e-6 and Vector(rot).length < 1e-6 and (scl - Vector((1, 1, 1))).length < 1e-6)
        add("%s flat shaded" % name, not any(p.use_smooth for p in me.polygons))
        add("%s has vertex colour Col" % name, me.color_attributes.get("Col") is not None)
        add("%s triangles %d <= %d" % (name, tris, item["tri_cap"]), tris <= item["tri_cap"])
        x0, x1, y0, y1, z0, z1 = kit.bounds(obj)
        anchor = "ceiling" if item["kind"] == "overhead" else ("center" if name.endswith("ore_voxel") or item["kind"] == "mount" else "floor")
        if anchor == "floor":
            add("%s base on Z=0" % name, abs(z0) < 1e-4, "min z %.4f" % z0)
        elif anchor == "ceiling":
            add("%s hung from Z=0" % name, abs(z1) < 1e-4, "max z %.4f" % z1)
        ub = unity_bounds(obj)
        half, clear, _ = room_of(item["room"])
        raw_slots = propose_slots(item["kind"], item["room"], ub, item["mount_y"])
        kept = []
        exempt = item["kind"] in ("floor", "path")
        for slot in raw_slots:
            pos = tuple(slot["pos"])
            blocked = False if exempt else cell_blocks(item["cells"], item["size"], pos, slot["yaw"], clear, half)
            slot = dict(slot)
            slot["blocks_walking_line"] = blocked
            if not blocked:
                kept.append(slot)
        add("%s has a clear slot (%d/%d)" % (name, len(kept), len(raw_slots)), len(kept) > 0,
            "every proposed slot meets the walking cross" if not kept else "")
        if name in OPENING:
            ix0, ix1, iz0, iz1, width, height, need_w, need_h = OPENING[name]
            blocked_cells = [c for c in item["cells"] if ix0 <= c[0] < ix1 and iz0 <= c[2] < iz1]
            add("%s opening %.2f x %.2f m clears the door %.2f x %.2f" % (name, width, height, need_w, need_h),
                not blocked_cells and width + 1e-6 >= need_w and height + 1e-6 >= need_h,
                "%d cells in the opening" % len(blocked_cells))
        if name == "prop_mine_rail_voxel":
            length = y1 - y0
            add("rail length %.3f m is the 6.4 m gallery" % length, abs(length - 6.4) < 0.02)
            add("rail gauge centres near ±0.35 m", abs((3.5 * T) - 0.35) < 0.02)
        if name == "prop_mine_cart_voxel":
            add("cart length %.3f m matches MineProp length 1.6" % (y1 - y0), abs((y1 - y0) - 1.6) < 0.02)
        if name == "prop_crypt_pillar_voxel":
            add("room pillar %.3f m under the 6 m ceiling" % (z1 - z0), 5.0 <= (z1 - z0) < CRYPT_H)
        if name == "prop_crypt_pillar_arena_voxel":
            add("arena pillar %.3f m under the 9 m ceiling" % (z1 - z0), 7.5 <= (z1 - z0) < ARENA_H)
        solid = item["kind"] in ("obstacle", "skin")
        piece = {
            "name": name,
            "fbx": "Chopper/Art/Export/%s.fbx" % name,
            "room": item["room"],
            "kind": item["kind"],
            "solid": solid,
            "blocks_walking_line": solid,
            "suggested_slots_keep_the_line_clear": True,
            "collider": "none" if item["kind"] in ("floor", "path", "frame", "overhead", "mount") else "box",
            "anchor": anchor,
            "triangles": tris,
            "unity_bounds_m": {
                "min": [round(ub[0], 3), round(ub[2], 3), round(ub[4], 3)],
                "max": [round(ub[1], 3), round(ub[3], 3), round(ub[5], 3)],
                "size": [round(ub[1] - ub[0], 3), round(ub[3] - ub[2], 3), round(ub[5] - ub[4], 3)],
            },
            "slots": kept,
        }
        if name == "prop_mine_lantern_voxel":
            piece["flame_unity"] = [0.063, 0.313, 0.438]
            piece["note"] = "Origin is the post bracket. Put the point light at flame_unity. No collider; the post is already solid."
        if name == "prop_mine_cart_voxel":
            piece["note"] = "MinePlan currently parks the cart at 1.8 m off the room centre, on the gallery line. Use a corner slot instead."
        if name == "prop_mine_barrel_voxel":
            piece["note"] = "May replace the box barrel. MinePlan corner is sign * (RoomSize/2 - 0.7) = ±7.3 on both axes, which is outside the 2.75 m cross."
        if name == "prop_mine_rail_voxel":
            piece["note"] = "One segment per gallery, centred on the corridor midpoint (MinePlan puts a rail of length CellSize 24 there and it runs through the rooms). Yaw along the link. No collider."
        if item["kind"] == "frame":
            piece["note"] = "Inset about 0.25 m into the room so it sits in front of the block jamb. No collider on the opening."
        if item["kind"] == "floor":
            piece["note"] = "Place 0.02 m above the floor so it does not z-fight the block floor. No collider. Safe on the walking line."
        pieces.append(piece)

    manifest = {
        "units": "Unity metres after FBX axis_forward -Z, axis_up Y. Local +Z is the piece front (Blender -Y).",
        "crypt": {
            "source": "Assets/_Chopper/Scripts/World/Generation/CryptPlan.cs",
            "room_size": 15.0, "room_height": 6.0, "wall_thickness": 0.6,
            "door_width": 5.0, "door_height": 4.4, "cell_size": 20.0,
            "clear_half": 2.5,
            "arena_size": 26.0, "arena_height": 9.0, "arena_spawn_radius": 9.0,
        },
        "mine": {
            "source": "Assets/_Chopper/Scripts/World/Generation/MinePlan.cs",
            "room_size": 16.0, "room_height": 6.5, "wall_thickness": 0.8,
            "gallery_width": 5.5, "gallery_height": 4.5, "cell_size": 24.0,
            "gallery_length": 6.4, "post_size": 0.32, "lantern_height": 2.3,
            "vein_height": [0.9, 1.6], "clear_half": 2.75,
        },
        "walking_line": (
            "Room-local XZ, origin at the room centre. Keep |x| < clear_half and |z| < clear_half "
            "free of solid cells below 2.2 m. That is the centre path and every door-to-door line, "
            "including rooms whose doors are not known yet. Flat floor tiles and the rail may lie on it."
        ),
        "recipe": {
            "crypt": (
                "Every room: one arch on each doorway; one wall panel (rotate carved, moss, crack, scorch by zone) "
                "and a trim on each solid span beside a door, using a wall slot; one floor tile somewhere; "
                "one overhead piece (chain, cobweb, or dripstone) in a ceiling corner; one solid corner piece "
                "(pillar, broken pillar, sarcophagus, urn, bones, roots, rubble, flowstone) in a free corner. "
                "Do not cover a chest (solid-wall centre, 1.1 m in from RoomSize/2) or a guardian point. "
                "Room 0: keep off the stair run (StairWidth 2.6, seven steps of StairRun 0.55). "
                "Arena: pillar_arena and rubble stay outside the 9 m spawn radius."
            ),
            "mine": (
                "Every chamber: a gallery frame on each mouth; one 6.4 m rail centred on that gallery; "
                "a lantern mesh on each existing post; an ore seam on a solid wall using a wall slot "
                "(do not use a vein whose along-wall offset falls inside the gallery); "
                "a timber frame on one solid wall; a barrel or crate or crystal or rubble in a corner. "
                "The cart goes in a corner slot in the deepest chamber, not at centre + 1.8 m."
            ),
        },
        "pieces": pieces,
    }
    os.makedirs(os.path.dirname(MANIFEST), exist_ok=True)
    with open(MANIFEST, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2)
        fh.write("\n")
    print("wrote", MANIFEST)

    lines.append("SUMMARY: %s" % ("ALL PASS" if ok else "FAILURES"))
    text = "\n".join(lines) + "\n"
    os.makedirs(RENDERS, exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    if not ok:
        raise SystemExit(1)

    bpy.context.preferences.filepaths.save_version = 0
    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("saved", BLEND)
    for item in built:
        kit.export_one(item["obj"])

    cam = kit.setup_render()
    render_sheets(cam, built)
    render_each(cam, built)
    render_corners(cam, built, mat)


def layout(items, cols, gap):
    x = y = row_d = 0.0
    n = 0
    max_x = 0.0
    for item in items:
        x0, x1, y0, y1, z0, z1 = kit.bounds(item["obj"])
        w = max(x1 - x0, 0.6)
        d = max(y1 - y0, 0.6)
        if n == cols:
            x = 0.0
            y += row_d + gap
            row_d = 0.0
            n = 0
        tx = x + w * 0.5
        ty = -(y + d * 0.5)
        item["obj"].location = (tx - (x0 + x1) * 0.5, ty - (y0 + y1) * 0.5, -z0)
        x += w + gap
        row_d = max(row_d, d)
        max_x = max(max_x, x)
        n += 1
    return max_x, y + row_d


def render_sheets(cam, built):
    crypt = [b for b in built if b["room"] != "mine"]
    mine = [b for b in built if b["room"] == "mine"]
    saved = {b["name"]: b["obj"].location.copy() for b in built}
    aspect = 1600 / 900
    for group, name in ((crypt, "cave_kit_crypt_sheet"), (mine, "cave_kit_mine_sheet")):
        layout(group, 5, 1.2)
        bpy.context.view_layer.update()
        shown = [b["obj"] for b in group]
        kit.only([b["obj"] for b in built], shown)
        xs, ys, zs = [], [], []
        for ob in shown:
            for v in ob.data.vertices:
                w = ob.matrix_world @ v.co
                xs.append(w.x)
                ys.append(w.y)
                zs.append(w.z)
        span_x = max(xs) - min(xs)
        span_y = max(ys) - min(ys)
        span_z = max(zs) - min(zs)
        ortho = max((span_y + span_z) * 0.95, span_x * 1.15 / aspect, 8)
        target = ((min(xs) + max(xs)) * 0.5, (min(ys) + max(ys)) * 0.5, span_z * 0.35)
        kit.shoot(cam, name, target, ortho, yaw_deg=-32, pitch_deg=32, res=(1600, 900))
        for b in built:
            b["obj"].location = saved[b["name"]]
    kit.only([b["obj"] for b in built], [b["obj"] for b in built])


def render_each(cam, built):
    objs = [b["obj"] for b in built]
    for item in built:
        kit.only(objs, [item["obj"]])
        x0, x1, y0, y1, z0, z1 = kit.bounds(item["obj"])
        ortho = max(max(x1 - x0, y1 - y0) * 1.45, (z1 - z0) * 1.15, 1.2)
        target = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
        kit.shoot(cam, "cave_kit_" + item["name"], target, ortho, res=(960, 540))
    kit.only(objs, objs)


def render_corners(cam, built, mat):
    by = {b["name"]: b["obj"] for b in built}
    temps = []

    def dup(name, loc, yaw=0):
        src = by[name]
        ob = bpy.data.objects.new(name + "_d", src.data)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = loc
        ob.rotation_euler = (0.0, 0.0, math.radians(yaw))
        temps.append(ob)
        return ob

    def reveal(shown):
        pool = temps + [crypt_wall, mine_wall] + [b["obj"] for b in built]
        for ob in pool:
            ob.hide_render = ob not in shown

    crypt_wall = kit.build("_crypt_shell", crypt_shell(), mat, size=(Q, Q, Q))
    mine_wall = kit.build("_mine_shell", mine_shell(), mat, size=(Q, Q, Q))
    show = [
        crypt_wall,
        dup("prop_crypt_wall_panel_voxel", (1.2, 3.9, 0.0)),
        dup("prop_crypt_trim_voxel", (1.2, 3.95, 0.0)),
        dup("prop_crypt_pillar_voxel", (3.2, 3.2, 0.0)),
        dup("prop_crypt_sarcophagus_voxel", (-1.6, 2.6, 0.0), 90),
        dup("prop_crypt_urn_voxel", (2.3, 2.2, 0.0)),
        dup("prop_crypt_bone_pile_voxel", (0.4, 2.4, 0.0)),
        dup("prop_crypt_rubble_voxel", (-2.4, 1.6, 0.0)),
        dup("prop_crypt_roots_voxel", (2.6, 3.4, 0.0)),
        dup("prop_crypt_floor_moss_voxel", (0.2, 1.2, 0.02)),
        dup("prop_crypt_chain_voxel", (1.6, 2.8, 5.6)),
        dup("prop_crypt_cobweb_voxel", (3.7, 3.7, 5.7)),
        dup("prop_crypt_arch_voxel", (-3.2, 0.4, 0.0), 90),
    ]
    reveal(show)
    bpy.context.scene.world.color = (0.22, 0.20, 0.18)
    kit.shoot(cam, "cave_kit_crypt_corner", (0.4, 2.2, 1.6), 9.5, yaw_deg=-38, pitch_deg=16, res=(1600, 900))

    show = [
        mine_wall,
        dup("prop_mine_timber_frame_voxel", (0.6, 3.9, 0.0)),
        dup("prop_mine_ore_voxel", (2.4, 3.85, 1.25)),
        dup("prop_mine_lantern_voxel", (3.4, 3.5, 2.3), 180),
        dup("prop_mine_barrel_voxel", (2.8, 2.6, 0.0)),
        dup("prop_mine_crate_voxel", (1.5, 2.5, 0.0)),
        dup("prop_mine_crystal_voxel", (3.3, 1.8, 0.0)),
        dup("prop_mine_rubble_voxel", (-1.8, 2.8, 0.0)),
        dup("prop_mine_cart_voxel", (-0.4, 1.5, 0.0), 70),
        dup("prop_mine_rail_voxel", (-1.2, -1.6, 0.02), 90),
        dup("prop_mine_gallery_frame_voxel", (-4.6, 0.2, 0.0), 90),
    ]
    reveal(show)
    kit.shoot(cam, "cave_kit_mine_corner", (-0.2, 1.6, 1.4), 11.0, yaw_deg=-36, pitch_deg=16, res=(1600, 900))


def crypt_shell():
    out = {}
    for ix in range(-16, 16):
        for iy in range(-8, 16):
            out[(ix, iy, -1)] = STONE
    for iz in range(0, 16):
        for ix in range(-16, 16):
            out[(ix, 16, iz)] = STONE
        for iy in range(0, 17):
            out[(16, iy, iz)] = STONE
    return out


def mine_shell():
    out = {}
    for ix in range(-20, 18):
        for iy in range(-12, 16):
            out[(ix, iy, -1)] = mine_rock(ix, iy, 0)
    for iz in range(0, 14):
        for ix in range(-20, 18):
            out[(ix, 16, iz)] = mine_rock(ix, 16, iz)
        for iy in range(-4, 17):
            out[(16, iy, iz)] = mine_rock(16, iy, iz)
    return out


if __name__ == "__main__":
    main()
