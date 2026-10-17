"""Concept-match review render of Chopper as the game shows him: the baked atlas only, no vertex colours.

Run:  blender -b -P render_review.py -- [atlas.png] [name]
In:   Art/Blender/chopper_rigged.blend, chopper_props.blend, the atlas (default Art/Textures/chopper_atlas.png)
Out:  Art/Renders/<name>.png (default review_chopper_three_quarter), same camera and pose as base_look_three_quarter
"""
import os
import sys

import bpy
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_base_look as rbl  # noqa: E402
import render_views as rv  # noqa: E402


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args:
        rbl.ATLAS = os.path.abspath(args[0])
    name = args[1] if len(args) > 1 else "review_chopper_three_quarter"
    bpy.ops.wm.open_mainfile(filepath=rbl.RIGGED)
    sc = bpy.context.scene
    rbl.setup_eevee(sc)
    rig = bpy.data.objects["chopper_rig"]
    for tr in rig.animation_data.nla_tracks:
        tr.mute = tr.name != rbl.POSE_TAKE
    sc.frame_set(rbl.POSE_FRAME)
    for part in rbl.ARMOUR:
        bpy.data.objects[part].hide_render = True
    with bpy.data.libraries.load(rv.PROPS, link=False) as (src, dst):
        dst.objects = ["chopper_axe_bear"]
    axe = dst.objects[0]
    sc.collection.objects.link(axe)
    axe.parent = rig
    axe.parent_type = "BONE"
    axe.parent_bone = "Weapon_R"
    axe.matrix_parent_inverse = Matrix.Translation((0, -rig.data.bones["Weapon_R"].length, 0))
    axe.matrix_basis = Matrix.Identity(4)
    rbl.atlas_materials()
    bpy.context.view_layer.update()
    rv.shoot(sc, name, (0, -0.05, 0.92), -35, 8, 2.15)


if __name__ == "__main__":
    main()
