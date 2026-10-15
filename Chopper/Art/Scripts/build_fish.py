"""Fishing kit in the 2.0 block language: perch, rod butt, rod tip, bobber, aim ring.

Run:  blender -b -P build_fish.py
Out:  Art/Export/prop_<name>_voxel.fbx
      Art/Renders/fish_check_report.txt, fish_lineup.png
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit

C = 0.08
REPORT = os.path.join(kit.RENDERS, "fish_check_report.txt")

TEAL = kit.srgb(36, 110, 88)
TEAL_D = kit.srgb(22, 72, 58)
BELLY = kit.srgb(210, 214, 186)
TAIL = kit.srgb(210, 100, 42)
EYE = kit.srgb(240, 210, 60)
RED = kit.srgb(210, 36, 28)
WHITE = kit.srgb(240, 236, 226)
GOLD = kit.srgb(214, 168, 60)


def box(out, x0, x1, y0, y1, z0, z1, colour):
    for ix in range(x0, x1):
        for iy in range(y0, y1):
            for iz in range(z0, z1):
                out[(ix, iy, iz)] = colour


def fish_cells():
    out = {}
    box(out, -2, 2, -4, 3, 1, 5, TEAL)
    box(out, -2, 2, -3, 2, 0, 1, BELLY)
    box(out, -2, 2, -6, -4, 1, 4, TEAL)
    box(out, -1, 1, -7, -6, 1, 3, TEAL)
    box(out, -1, 1, 3, 5, 2, 4, TEAL)
    box(out, -1, 1, 5, 8, 3, 6, TAIL)
    box(out, -1, 1, 5, 8, 0, 3, TAIL)
    box(out, -1, 1, -1, 2, 5, 7, TEAL_D)
    box(out, -4, -2, -2, 0, 2, 3, TEAL_D)
    box(out, 2, 4, -2, 0, 2, 3, TEAL_D)
    out[(1, -5, 3)] = EYE
    out[(-2, -5, 3)] = EYE
    return out


def rod_butt_cells():
    out = {}
    box(out, -2, 2, -2, 2, 0, 4, kit.LOG_DARK)
    box(out, -1, 1, -1, 1, 4, 13, kit.LOG)
    box(out, -1, 1, -1, 1, 8, 9, kit.ROPE)
    return out


def rod_tip_cells():
    out = {}
    box(out, -1, 1, -1, 1, 0, 8, kit.LOG_END)
    box(out, -1, 1, -1, 1, 8, 10, kit.IRON)
    out[(0, 0, 9)] = kit.IRON_LIGHT
    return out


def bobber_cells():
    out = {}
    box(out, -2, 2, -2, 2, 0, 3, RED)
    box(out, -1, 1, -1, 1, 3, 5, WHITE)
    box(out, 0, 1, 0, 1, 5, 6, kit.IRON)
    return out


def aim_ring_cells():
    out = {}
    for i in range(-6, 6):
        out[(i, -6, 0)] = GOLD
        out[(i, 5, 0)] = GOLD
        out[(-6, i, 0)] = GOLD
        out[(5, i, 0)] = GOLD
    return out


SPECS = [
    ("prop_fish_voxel", fish_cells, {"h": (0.50, 0.64), "w": (0.60, 0.72), "d": (1.12, 1.28), "tris": 800}),
    ("prop_rod_butt_voxel", rod_butt_cells, {"h": (1.00, 1.10), "w": (0.28, 0.40), "d": (0.28, 0.40), "tris": 400}),
    ("prop_rod_tip_voxel", rod_tip_cells, {"h": (0.76, 0.88), "w": (0.12, 0.24), "d": (0.12, 0.24), "tris": 200}),
    ("prop_bobber_voxel", bobber_cells, {"h": (0.44, 0.52), "w": (0.28, 0.40), "d": (0.28, 0.40), "tris": 240}),
    ("prop_aim_ring_voxel", aim_ring_cells, {"h": (0.07, 0.10), "w": (0.88, 1.04), "d": (0.88, 1.04), "tris": 400}),
]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    built = []
    for name, cells, rule in SPECS:
        size = (C, C, C)
        obj = kit.build(name, cells(), mat, size=size)
        built.append((obj, rule))
        b = kit.bounds(obj)
        print("built %s tris %d size %.3f x %.3f x %.3f" % (
            name, kit.tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))
    kit.REPORT = REPORT
    kit.check(built, [])
    cam = kit.setup_render()
    x = 0.0
    for obj, _ in built:
        b = kit.bounds(obj)
        obj.location.x = x - b[0]
        x += (b[1] - b[0]) + 0.4
    kit.shoot(cam, "fish_lineup", (x / 2, 0, 0.55), max(2.4, x * 1.15), yaw_deg=-28, pitch_deg=16)
    for obj, _ in built:
        obj.location.x = 0.0
        kit.export_one(obj)


if __name__ == "__main__":
    main()
