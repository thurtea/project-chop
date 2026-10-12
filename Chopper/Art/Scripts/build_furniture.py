"""Voxel furniture for the hub house and the crypts, in the concept 2.0 wood and stone: storage chest, grand chest,
bed, workbench and fireplace. Uses the world kit's colours, cell builder, checks and export settings
(build_world_kit.py).

Run:  blender -b -P build_furniture.py
Out:  Art/Export/prop_<name>_voxel.fbx
      Art/Renders/furniture_check_report.txt, furniture_lineup.png

1 unit = 1 m, 0.125 m cells, base on Z = 0, origin at the middle of the footprint. Vertex colour, material M_World.
Fronts face -Y, which lands on +Z in Unity. The bed's length runs along Y (Unity Z), the head at +Y (Unity -Z).
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit  # noqa: E402

C = 0.125
REPORT = os.path.join(kit.RENDERS, "furniture_check_report.txt")

GOLD = kit.srgb(214, 168, 60)
GOLD_DARK = kit.srgb(160, 118, 36)
BLANKET = kit.srgb(176, 40, 34)
BLANKET_DARK = kit.srgb(116, 24, 22)
LINEN = kit.srgb(226, 214, 186)
STRAW = kit.srgb(206, 170, 96)
SOOT = kit.srgb(34, 28, 24)


def box(out, x0, x1, y0, y1, z0, z1, colour):
    """Fills cells x0..x1-1, y0..y1-1, z0..z1-1 with colour (a callable gets the cell)."""
    for ix in range(x0, x1):
        for iy in range(y0, y1):
            for iz in range(z0, z1):
                out[(ix, iy, iz)] = colour(ix, iy, iz) if callable(colour) else colour


def plank(ix, iy, iz):
    return kit.PLANK if (ix // 2 + iz) % 2 else kit.LOG


def chest_cells(trim, trim_dark):
    """1.0 x 0.625 x 0.75 m: plank body, a lid one cell proud, two bands over the lid, a lock plate on the front."""
    out = {}
    box(out, -4, 4, -2, 3, 0, 4, lambda x, y, z: kit.PLANK if z % 2 else kit.LOG)       # body
    box(out, -4, 4, -3, 3, 4, 6, lambda x, y, z: kit.LOG_DARK if z == 4 else kit.LOG)    # lid, overhanging the front
    for bx in (-3, 2):                                                                     # bands front, top, back
        for iz in range(0, 6):
            out[(bx, -3 if iz >= 4 else -2, iz)] = trim
        for iy in range(-3, 3):
            out[(bx, iy, 5)] = trim
    box(out, -1, 1, -3, -2, 3, 5, trim_dark)                                               # lock plate
    out[(-1, -3, 3)] = kit.IRON
    return out


def bed_cells():
    """1.125 x 2.25 m, 0.75 m to the top of the headboard: log frame, straw mattress, red blanket, linen pillow."""
    out = {}
    box(out, -4, 5, -9, 9, 0, 2, kit.LOG_DARK)                                             # frame and legs block
    box(out, -4, 5, -9, 9, 2, 3, STRAW)                                                    # mattress
    box(out, -4, 5, -9, 4, 3, 4, lambda x, y, z: BLANKET_DARK if (x + y) % 4 == 0 else BLANKET)  # blanket
    box(out, -3, 4, 5, 8, 3, 4, LINEN)                                                     # pillow
    box(out, -4, 5, 8, 9, 0, 6, kit.LOG)                                                   # headboard
    box(out, -4, 5, -9, -8, 0, 4, kit.LOG)                                                 # footboard
    for ix in (-4, 4):
        out[(ix, 8, 6)] = kit.LOG_END
    return out


def workbench_cells():
    """1.75 x 1.0 m, 0.875 m top: plank top on log legs, a shelf, a vise block and a hammer on the top."""
    out = {}
    box(out, -7, 7, -4, 4, 6, 7, plank)                                                    # top
    for lx, ly in ((-7, -4), (6, -4), (-7, 3), (6, 3)):                                    # legs
        box(out, lx, lx + 1, ly, ly + 1, 0, 6, kit.LOG_DARK)
    box(out, -6, 6, -3, 3, 2, 3, kit.PLANK)                                                # shelf
    box(out, 4, 6, -4, -2, 7, 9, kit.IRON)                                                 # vise
    box(out, -4, 0, -1, 0, 7, 8, kit.LOG)                                                  # hammer handle
    box(out, -5, -4, -2, 1, 7, 9, kit.IRON_LIGHT)                                          # hammer head
    box(out, 1, 3, 1, 3, 7, 8, kit.LOG_END)                                                # offcut block
    return out


def fireplace_cells():
    """1.5 x 0.75 m, 2.25 m with the chimney: stone hearth with an arched opening, embers and logs inside."""
    out = {}

    def stone(ix, iy, iz):
        r = (kit.hsh(ix, iy, iz) % 1000) / 1000.0
        return kit.STONE_DARK if r < 0.3 else (kit.STONE_LIGHT if r > 0.8 else kit.STONE)

    box(out, -6, 6, -3, 3, 0, 10, stone)                                                   # hearth block
    for (ix, iy, iz) in list(out):                                                          # opening, 1 m x 0.75 m
        if -4 <= ix < 4 and iy < 2 and 1 <= iz < 7 and not (iz == 6 and ix in (-4, 3)):
            del out[(ix, iy, iz)]
    box(out, -4, 4, -3, 2, 0, 1, SOOT)                                                     # floor of the fire
    box(out, -3, 3, -1, 1, 1, 2, lambda x, y, z: kit.EMBER_HOT if (x + y) % 2 else kit.EMBER)
    box(out, -3, 3, 1, 2, 1, 3, kit.LOG_DARK)                                              # logs at the back
    box(out, -7, 7, -4, 3, 10, 11, kit.STONE_LIGHT)                                        # mantel
    box(out, -3, 3, -1, 3, 11, 18, stone)                                                  # chimney
    return out


SPECS = [
    ("prop_chest_voxel", lambda: chest_cells(kit.IRON, kit.IRON_LIGHT), {"h": (0.7, 0.8), "w": (0.95, 1.05), "tris": 1200}),
    ("prop_chest_grand_voxel", lambda: chest_cells(GOLD, GOLD_DARK), {"h": (0.7, 0.8), "w": (0.95, 1.05), "tris": 1200}),
    ("prop_bed_voxel", bed_cells, {"h": (0.7, 0.9), "w": (1.05, 1.2), "d": (2.2, 2.3), "tris": 1500}),
    ("prop_workbench_voxel", workbench_cells, {"h": (1.05, 1.2), "w": (1.7, 1.8), "d": (0.95, 1.05), "tris": 1500}),
    ("prop_fireplace_voxel", fireplace_cells, {"h": (2.2, 2.3), "w": (1.7, 1.8), "d": (0.8, 0.9), "tris": 3000}),
]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    built = []
    for name, cells, rule in SPECS:
        obj = kit.build(name, cells(), mat, size=(C, C, C))
        built.append((obj, rule))
        b = kit.bounds(obj)
        print("built %s tris %d size %.3f x %.3f x %.3f" % (name, kit.tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))
    # check() writes to kit.REPORT; point it at this report so the world kit report is left alone
    kit.REPORT = REPORT
    kit.check(built, [])

    cam = kit.setup_render()
    x = 0.0
    for obj, _ in built:
        b = kit.bounds(obj)
        obj.location.x = x - b[0]
        x += (b[1] - b[0]) + 0.5
    kit.shoot(cam, "furniture_lineup", (x / 2, 0, 0.9), x * 1.1, yaw_deg=-25, pitch_deg=18)
    for obj, _ in built:
        obj.location.x = 0.0
        kit.export_one(obj)


if __name__ == "__main__":
    main()
