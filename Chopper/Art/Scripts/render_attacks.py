"""Frame sheets of the three axe chops (Attack_1, Attack_2, Attack_3) and HitReact, front three-quarter and side, so
the swing can be judged without Unity. Columns are key frames: rest, wind-up, cock, strike, impact, settle, end. Base look with the bear-head axe on Weapon_R, as render_base_look.py.

Run:  blender -b -P render_attacks.py
In:   Art/Blender/chopper_rigged.blend (after animate_chopper.py), chopper_props.blend, Art/Textures/chopper_atlas.png
Out:  Art/Renders/attacks_sheet.png (one row per take and view, columns = moments of the swing)
"""
import os
import sys

import bpy
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_base_look as rbl  # noqa: E402
import render_views as rv  # noqa: E402

TAKES = {"Attack_1": (1, 4, 7, 10, 12, 13, 16, 23), "Attack_2": (1, 4, 7, 10, 12, 13, 16, 23),
         "Attack_3": (1, 6, 10, 14, 16, 18, 21, 29), "HitReact": (1, 2, 3, 5, 7, 9, 12, 16)}
VIEWS = (("front", -35, 6), ("side", -90, 0))


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
    sc.render.resolution_x, sc.render.resolution_y = 360, 420

    rows = []
    for take, frames in TAKES.items():
        for tr in rig.animation_data.nla_tracks:
            tr.mute = tr.name != take
        for view, yaw, pitch in VIEWS:
            row = []
            for fr in frames:
                sc.frame_set(fr)
                bpy.context.view_layer.update()
                name = "attack_%s_%s_%02d" % (take, view, fr)
                rv.shoot(sc, name, (0, 0, 1.05), yaw, pitch, 2.9)
                row.append(os.path.join(rv.OUT, name + ".png"))
            rows.append(row)
    flat = [p for row in rows for p in row]
    # one sheet: rows of len(MOMENTS)
    import numpy as np
    imgs = [rbl.load_rgb(p) for p in flat]
    h, w = imgs[0].shape[:2]
    cols = max(len(f) for f in TAKES.values())
    sheet = np.ones((h * len(rows), w * cols, 3), dtype=np.float32)
    for i, im in enumerate(imgs):
        r, c = divmod(i, cols)
        sheet[r * h:(r + 1) * h, c * w:(c + 1) * w] = im[:h, :w, :3]
    out = bpy.data.images.new("attacks", w * cols, h * len(rows), alpha=False)
    rgba = np.concatenate([sheet[::-1], np.ones((h * len(rows), w * cols, 1), dtype=np.float32)], axis=2)
    out.pixels.foreach_set(rgba.ravel())
    out.filepath_raw = os.path.join(rv.OUT, "attacks_sheet.png")
    out.file_format = "PNG"
    out.save()
    for p in flat:
        os.remove(p)
    print("wrote", out.filepath_raw)


if __name__ == "__main__":
    main()
