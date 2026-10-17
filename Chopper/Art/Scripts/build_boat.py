"""Little rowboat for the ocean: plank hull with a dark keel and rim, two thwarts (the oars are separate, moved in game).

Run:  blender -b -P build_boat.py
Out:  Art/Export/prop_boat_voxel.fbx

1 unit = 1 m, 0.125 m cells, keel on Z = 0, bow toward -Y (Unity +Z).
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit  # noqa: E402

C = 0.125
LEN = 22
REPORT = os.path.join(kit.RENDERS, "boat_check_report.txt")


def half_width(iy, iz):
    t = abs(iy - LEN / 2.0 + 0.5) / (LEN / 2.0)
    base = 4.6 * (1.0 - t ** 2.2)
    return base * (0.55 + 0.45 * min(1.0, iz / 3.0))


def boat_cells():
    out = {}
    for iy in range(LEN):
        for iz in range(0, 5):
            w = half_width(iy, iz)
            n = int(round(w))
            if n < 1:
                if iz >= 1:
                    out[(0, iy, iz)] = kit.FRAME
                continue
            for ix in range(-n, n):
                rim = ix in (-n, n - 1)
                if iz == 0:
                    out[(ix, iy, iz)] = kit.LOG_DARK if ix in (-1, 0) else kit.LOG_DARK
                elif rim:
                    out[(ix, iy, iz)] = kit.FRAME if iz == 4 else (kit.PLANK if iz % 2 else kit.LOG)
                elif iz == 1:
                    out[(ix, iy, iz)] = kit.PLANK
    for iy in (7, 14):
        n = int(round(half_width(iy, 3)))
        for ix in range(-n + 1, n - 1):
            out[(ix, iy, 3)] = kit.LOG
    return out


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    obj = kit.build("prop_boat_voxel", boat_cells(), mat, size=(C, C, C))
    kit.REPORT = REPORT
    kit.check([(obj, {"h": (0.5, 0.8), "w": (1.0, 1.8), "d": (2.5, 3.0), "tris": 6000})], [])
    cam = kit.setup_render()
    kit.shoot(cam, "boat_three_quarter", (0, 1.4, 0.3), 5.0, yaw_deg=-35, pitch_deg=22)
    kit.export_one(obj)


main()