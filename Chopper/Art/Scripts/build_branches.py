import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_world_kit as kit

C = 0.06
REPORT = os.path.join(kit.RENDERS, "branch_check_report.txt")

KINKS = [
    [(0, 0), (5, 1), (10, 0), (14, -1)],
    [(0, 0), (4, -1), (9, -1), (14, 1)],
    [(0, 0), (6, 1), (11, 2), (15, 2)],
    [(0, 0), (3, 0), (8, -2), (13, -2)],
]
TWIGS = [(6, 1, 1, 4), (8, -1, -1, 3), (5, 1, -1, 4), (10, -2, 1, 3)]


def branch_cells(index):
    out = {}
    pts = KINKS[index]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        for ix in range(x0, x1 + 1):
            t = (ix - x0) / max(1, x1 - x0)
            iy = round(y0 + (y1 - y0) * t)
            for dy in (0, 1):
                for iz in (0, 1):
                    dark = (ix + iz + index) % 4 == 0 or iz == 0
                    out[(ix, iy + dy, iz)] = kit.LOG_DARK if dark else kit.LOG
    end = pts[-1]
    for dy in (0, 1):
        for iz in (0, 1):
            out[(0, pts[0][1] + dy, iz)] = kit.LOG_END
            out[(end[0], end[1] + dy, iz)] = kit.LOG_END
    tx, ty, side, length = TWIGS[index]
    for k in range(1, length + 1):
        out[(tx + k // 2, ty + side * k + (1 if side > 0 else 0), 0 if k < 3 else 1)] = kit.LOG
    return out


SPECS = [("prop_branch_%d_voxel" % i, i, {"h": (0.08, 0.16), "w": (0.75, 1.05), "d": (0.15, 0.5), "tris": 1500}) for i in range(4)]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("M_World")
    mat.diffuse_color = (0.7, 0.7, 0.7, 1.0)
    built = []
    for name, index, rule in SPECS:
        obj = kit.soften(kit.build(name, branch_cells(index), mat, size=(C, C, C)), C * 0.2)
        built.append((obj, rule))
        b = kit.bounds(obj)
        print("built %s tris %d size %.3f x %.3f x %.3f" % (
            name, kit.tris_of(obj), b[1] - b[0], b[3] - b[2], b[5] - b[4]))
    kit.REPORT = REPORT
    kit.check(built, [])
    cam = kit.setup_render()
    y = 0.0
    for obj, _ in built:
        obj.location.y = y
        y += 0.5
    kit.shoot(cam, "branch_lineup", (0.45, y / 2, 0.1), 2.4, yaw_deg=-32, pitch_deg=40)
    for obj, _ in built:
        obj.location.y = 0.0
        kit.export_one(obj)


if __name__ == "__main__":
    main()