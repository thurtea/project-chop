"""Chopper's own weapons: sword, mace and spear, in the frame of chopper_axe.

Run:  blender -b -P build_weapons.py
Out:  Art/Export/chopper_sword.fbx, chopper_mace.fbx, chopper_spear.fbx

Same frame as chopper_axe: origin = right-hand grip point, haft along local +Z, blade or head
width along local X, thickness along Y. Vertex colours only.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chopper_lib as L  # noqa: E402
from chopper_lib import PAL  # noqa: E402
import export_fbx as E  # noqa: E402

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
EXPORT = os.path.join(ART, "Export")
RENDERS = os.path.join(ART, "Renders")
REPORT = os.path.join(RENDERS, "weapons_check_report.txt")


def grip(mb, z0, z1):
    z = z0
    while z < z1:
        mb.box((0, 0, z + 0.02), (0.044, 0.044, 0.04), 0.004, color=PAL["wood"])
        mb.box((0, 0, z + 0.045), (0.05, 0.05, 0.012), 0.003, color=PAL["wood_dark"])
        z += 0.055


def build_sword(mat):
    mb = L.MeshBuilder("chopper_sword")
    mb.box((0, 0, -0.13), (0.06, 0.06, 0.05), 0.008, color=PAL["steel_dark"])
    grip(mb, -0.1, 0.09)
    mb.box((0, 0, 0.12), (0.26, 0.06, 0.04), 0.006, color=PAL["steel_dark"])
    mb.box((0, 0, 0.47), (0.07, 0.022, 0.64), 0.004, color=PAL["steel"])
    mb.box((0, 0, 0.47), (0.016, 0.026, 0.6), 0.002, color=PAL["steel_dark"])
    mb.box((0, 0, 0.82), (0.045, 0.02, 0.06), 0.004, color=PAL["steel"])
    mb.box((0, 0, 0.87), (0.02, 0.016, 0.04), 0.003, color=PAL["steel"])
    return mb.finish(mat, recalc=True)


def build_mace(mat):
    mb = L.MeshBuilder("chopper_mace")
    mb.box((0, 0, -0.14), (0.055, 0.055, 0.05), 0.008, color=PAL["steel_dark"])
    grip(mb, -0.11, 0.1)
    mb.box((0, 0, 0.3), (0.04, 0.04, 0.4), 0.006, color=PAL["wood_dark"])
    mb.box((0, 0, 0.2), (0.05, 0.05, 0.03), 0.004, color=PAL["steel_dark"])
    mb.box((0, 0, 0.6), (0.17, 0.17, 0.2), 0.012, color=PAL["steel_dark"])
    for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        mb.box((sx * 0.105, sy * 0.105, 0.6), (0.05 if sx else 0.16, 0.05 if sy else 0.16, 0.16), 0.006, color=PAL["steel"])
    mb.box((0, 0, 0.73), (0.06, 0.06, 0.06), 0.006, color=PAL["steel"])
    return mb.finish(mat, recalc=True)


def build_spear(mat):
    mb = L.MeshBuilder("chopper_spear")
    mb.box((0, 0, -0.62), (0.05, 0.05, 0.05), 0.008, color=PAL["steel_dark"])
    mb.box((0, 0, 0.2), (0.036, 0.036, 1.6), 0.005, color=PAL["wood"])
    grip(mb, -0.1, 0.1)
    mb.box((0, 0, 1.02), (0.05, 0.05, 0.06), 0.006, color=PAL["steel_dark"])
    mb.box((0, 0, 1.13), (0.09, 0.02, 0.16), 0.004, color=PAL["steel"])
    mb.box((0, 0, 1.25), (0.055, 0.018, 0.1), 0.003, color=PAL["steel"])
    mb.box((0, 0, 1.33), (0.022, 0.014, 0.07), 0.002, color=PAL["steel"])
    return mb.finish(mat, recalc=True)


def check(objs):
    lines, fails = [], 0
    want = {"chopper_sword": (0.8, 1.2), "chopper_mace": (0.75, 1.0), "chopper_spear": (1.8, 2.2)}
    for ob in objs:
        h = ob.dimensions.z
        lo, hi = want[ob.name]
        tris = L.tri_count(ob)
        ok = lo <= h <= hi and tris < 2000
        fails += 0 if ok else 1
        lines.append(f"{'PASS' if ok else 'FAIL'} {ob.name}: length {h:.2f} m (want {lo}-{hi}), {tris} tris")
    lines.append(f"SUMMARY: {len(objs) - fails} PASS, {fails} FAIL")
    os.makedirs(RENDERS, exist_ok=True)
    with open(REPORT, "w") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))


def main():
    L.reset_scene()
    mat = L.make_material("M_Chopper_Gear", (0.6, 0.6, 0.6))
    objs = [build_sword(mat), build_mace(mat), build_spear(mat)]
    for i, ob in enumerate(objs):
        ob.location.x = 0.0
    check(objs)
    os.makedirs(EXPORT, exist_ok=True)
    for ob in objs:
        for o in bpy.data.objects:
            o.select_set(False)
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        out = os.path.join(EXPORT, ob.name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=out, **E.PROP_SETTINGS)
        print("exported", out, os.path.getsize(out), "bytes")


main()
