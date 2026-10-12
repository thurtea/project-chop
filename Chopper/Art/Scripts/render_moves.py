"""Frame sheet of Chopper's movement takes (Idle, Walk, Run, Sprint, crouch, air, landing, roll, swim, death), side view,
so the blocky Minecraft/ARPG style (rigid torso, pendulum limbs, planted feet) can be judged without Unity.
Base look with the bear-head axe on Weapon_R, as render_attacks.py.

Run:  blender -b -P render_moves.py
In:   Art/Blender/chopper_rigged.blend (after animate_chopper.py), chopper_props.blend, Art/Textures/chopper_atlas.png
Out:  Art/Renders/moves_sheet.png (one row per take, columns = eight moments of the take)
"""
import os
import sys

import bpy
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_base_look as rbl  # noqa: E402
import render_views as rv  # noqa: E402

# take: (length, view yaw)
TAKES = {"Idle": (60, -90), "Walk": (30, -90), "Run": (18, -90), "Sprint": (16, -90), "CrouchIdle": (40, -90),
         "CrouchWalk": (24, -90), "Air": (20, -90), "LandHard": (18, -90), "Roll": (24, -90), "SwimIdle": (40, -35),
         "Swim": (24, -90), "Death": (30, 0)}
MOMENTS = (0.0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 1.0)


def main():
    bpy.ops.wm.open_mainfile(filepath=rbl.RIGGED)
    sc = bpy.context.scene
    rbl.setup_eevee(sc)
    rig = bpy.data.objects["chopper_rig"]
    for name in rbl.ARMOUR:
        bpy.data.objects[name].hide_render = True
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
    sc.render.resolution_x, sc.render.resolution_y = 300, 300

    flat = []
    for take, (length, yaw) in TAKES.items():
        for tr in rig.animation_data.nla_tracks:
            tr.mute = tr.name != take
        for m in MOMENTS:
            sc.frame_set(1 + round(m * length))
            bpy.context.view_layer.update()
            name = "move_%s_%02d" % (take, round(m * 100))
            rv.shoot(sc, name, (0, 0, 0.9), yaw, 4, 2.6)
            flat.append(os.path.join(rv.OUT, name + ".png"))
    import numpy as np
    imgs = [rbl.load_rgb(p) for p in flat]
    h, w = imgs[0].shape[:2]
    cols = len(MOMENTS)
    rows = len(TAKES)
    sheet = np.ones((h * rows, w * cols, 3), dtype=np.float32)
    for i, im in enumerate(imgs):
        r, c = divmod(i, cols)
        sheet[r * h:(r + 1) * h, c * w:(c + 1) * w] = im[:h, :w, :3]
    out = bpy.data.images.new("moves", w * cols, h * rows, alpha=False)
    rgba = np.concatenate([sheet[::-1], np.ones((h * rows, w * cols, 1), dtype=np.float32)], axis=2)
    out.pixels.foreach_set(rgba.ravel())
    out.filepath_raw = os.path.join(rv.OUT, "moves_sheet.png")
    out.file_format = "PNG"
    out.save()
    for p in flat:
        os.remove(p)
    print("wrote", out.filepath_raw)


if __name__ == "__main__":
    main()
