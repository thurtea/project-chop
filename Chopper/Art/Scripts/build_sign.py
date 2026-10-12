"""Voxel sign for the tutorial yard, in the concept 2.0 wood: a log post with a plank board in a dark frame.
Uses the world kit's colours, cell builder and export settings (build_world_kit.py).

Run:  blender -b -P build_sign.py
Out:  Art/Export/prop_sign_voxel.fbx
      Art/Renders/sign_check_report.txt, sign_front.png

1 unit = 1 m, 0.125 m cells, base on Z = 0, origin at the post foot. Vertex colour, material M_World.
The board faces -Y, which lands on +Z in Unity (the side TutorialYard turns toward the hut).
Size follows the old primitive sign (TutorialYard.BuildSign): post 1.4 m, board about 0.95 x 0.55 m at 1.35 m.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit  # noqa: E402

C = 0.125
REPORT = os.path.join(kit.RENDERS, "sign_check_report.txt")


def sign_cells():
    out = {}
    # post: 2 x 2 cells, 1.5 m, darker bark every third cell, a pale cut end on top
    for ix in (-1, 0):
        for iy in (-1, 0):
            for iz in range(0, 12):
                out[(ix, iy, iz)] = kit.LOG_DARK if (ix + iy + iz) % 3 == 0 else kit.LOG
            out[(ix, iy, 12)] = kit.LOG_END
    # board: 8 x 5 cells (1.0 x 0.625 m) in front of the post, z 0.875..1.5 m; frame on the rim, planks inside
    for ix in range(-4, 4):
        for iz in range(7, 12):
            rim = ix in (-4, 3) or iz in (7, 11)
            if rim:
                colour = kit.FRAME
            else:
                colour = kit.PLANK if iz % 2 else kit.LOG
            out[(ix, -2, iz)] = colour
    # iron nails, flush in the frame corners
    for ix, iz in ((-4, 7), (3, 7), (-4, 11), (3, 11)):
        out[(ix, -2, iz)] = kit.IRON
    return out


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    obj = kit.build("prop_sign_voxel", sign_cells(), mat, size=(C, C, C))
    # check() writes to kit.REPORT; point it at the sign's own report so the world kit report is left alone
    kit.REPORT = REPORT
    kit.check([(obj, {"h": (1.5, 1.7), "w": (0.95, 1.05), "d": (0.25, 0.5), "tris": 800})], [])

    cam = kit.setup_render()
    kit.shoot(cam, "sign_front", (0, -0.1, 0.8), 3.6, yaw_deg=-25, pitch_deg=12)
    kit.export_one(obj)


if __name__ == "__main__":
    main()
