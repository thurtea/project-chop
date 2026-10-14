"""Renders of the enemies built on Chopper's rig, to judge them without Unity.

Run:  blender -b -P render_enemies.py
In:   Art/Blender/chopper_rigged.blend, chopper_props.blend, Art/Textures/chopper_atlas_draugr.png,
      Art/Blender/chopper_elemental.blend
Out:  Art/Renders/draugr_front.png, draugr_three_quarter.png, elemental_<i>.png (i = Element index),
      enemies_sheet.png
"""
import os
import sys

import bpy
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_base_look as rbl  # noqa: E402
import render_views as rv  # noqa: E402

ART = rv.ART
ELEMENTAL = os.path.join(ART, "Blender", "chopper_elemental.blend")
DRAUGR_ATLAS = os.path.join(ART, "Textures", "chopper_atlas_draugr.png")
DRAUGR_HIDDEN = ("chopper_hair", "chopper_plate", "chopper_pauldrons")


def pose(rig, take="Idle", frame=16):
    for tr in rig.animation_data.nla_tracks:
        tr.mute = tr.name != take
    bpy.context.scene.frame_set(frame)


def draugr():
    bpy.ops.wm.open_mainfile(filepath=rbl.RIGGED)
    sc = bpy.context.scene
    rbl.setup_eevee(sc)
    rig = bpy.data.objects["chopper_rig"]
    pose(rig)
    for name in DRAUGR_HIDDEN:
        bpy.data.objects[name].hide_render = True
    with bpy.data.libraries.load(rv.PROPS, link=False) as (src, dst):
        dst.objects = ["chopper_axe"]
    axe = dst.objects[0]
    sc.collection.objects.link(axe)
    axe.parent = rig
    axe.parent_type = "BONE"
    axe.parent_bone = "Weapon_R"
    axe.matrix_parent_inverse = Matrix.Translation((0, -rig.data.bones["Weapon_R"].length, 0))
    axe.matrix_basis = Matrix.Identity(4)
    rbl.ATLAS = DRAUGR_ATLAS
    rbl.atlas_materials()
    bpy.context.view_layer.update()
    tgt = (0, -0.05, 0.98)
    rv.shoot(sc, "draugr_front", tgt, 0, 0, 2.3)
    rv.shoot(sc, "draugr_three_quarter", tgt, -35, 8, 2.3)


def elementals():
    bpy.ops.wm.open_mainfile(filepath=ELEMENTAL)
    sc = bpy.context.scene
    rbl.setup_eevee(sc)
    rig = bpy.data.objects["chopper_rig"]
    pose(rig)
    names = []
    for i in range(5):
        for ob in bpy.data.objects:
            if ob.name.startswith("Leaves"):
                ob.hide_render = ob.name != "Leaves%d" % i
        bpy.context.view_layer.update()
        name = "elemental_%d" % i
        rv.shoot(sc, name, (0, -0.05, 1.0), -35, 8, 2.5)
        names.append(name)
    rv.shoot(sc, "elemental_front", (0, -0.05, 1.0), 0, 0, 2.5)
    return names


def main():
    draugr()
    names = elementals()
    rbl.sheet([os.path.join(rv.OUT, n + ".png") for n in ["draugr_front", "draugr_three_quarter"] + names],
              os.path.join(rv.OUT, "enemies_sheet.png"), h=420)


if __name__ == "__main__":
    main()
