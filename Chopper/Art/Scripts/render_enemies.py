"""Renders of the enemies built on Chopper's rig, to judge them without Unity.

Run:  blender -b -P render_enemies.py
In:   Art/Blender/chopper_rigged.blend, chopper_props.blend, Art/Textures/chopper_atlas_draugr.png,
      Art/Blender/chopper_elemental.blend
Out:  Art/Renders/draugr_front.png, draugr_three_quarter.png, elemental_<i>.png (i = Element index),
      enemies_sheet.png, draugr_takes_sheet.png and elemental_takes_sheet.png (one row per take and view,
      columns = key frames; the draugr sheet has Chopper's Walk on the draugr as a comparison row)
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


def WALK8(n):
    return tuple(1 + round(m * n) for m in (0.0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 1.0))


DRAUGR_ROWS = (("Draugr_Walk", WALK8(36), -90, 0), ("Draugr_Walk", WALK8(36), 0, 0), ("Walk", WALK8(30), -90, 0),
               ("Draugr_Attack", (1, 4, 12, 17, 19, 20, 24, 31), -35, 6),
               ("Draugr_Attack", (1, 4, 12, 17, 19, 20, 24, 31), -90, 0))
ELEMENTAL_ROWS = (("Elemental_Chop", (1, 10, 16, 17, 21, 29), -70, 4),
                  ("Elemental_Thrust", (1, 7, 13, 14, 17, 23), -70, 4),
                  ("Elemental_Smash", (1, 14, 18, 24, 27, 33), -70, 4),
                  ("Elemental_Stab", (1, 8, 15, 16, 19, 25), -70, 4))


def pose(rig, take="Idle", frame=16):
    for tr in rig.animation_data.nla_tracks:
        tr.mute = tr.name != take
    bpy.context.scene.frame_set(frame)


def take_sheet(sc, rig, rows, prefix, out_path, target):
    import numpy as np
    sc.render.resolution_x, sc.render.resolution_y = 300, 340
    flat = []
    for take, frames, yaw, pitch in rows:
        for fr in frames:
            pose(rig, take, fr)
            bpy.context.view_layer.update()
            name = "%s_%s_%d_%02d" % (prefix, take, yaw, fr)
            rv.shoot(sc, name, target, yaw, pitch, 2.9)
            flat.append(os.path.join(rv.OUT, name + ".png"))
    imgs = [rbl.load_rgb(p) for p in flat]
    h, w = imgs[0].shape[:2]
    cols = max(len(r[1]) for r in rows)
    sheet = np.ones((h * len(rows), w * cols, 3), dtype=np.float32)
    for i, im in enumerate(imgs):
        r, c = divmod(i, cols)
        sheet[r * h:(r + 1) * h, c * w:(c + 1) * w] = im[:h, :w, :3]
    img = bpy.data.images.new(prefix, w * cols, h * len(rows), alpha=False)
    rgba = np.concatenate([sheet[::-1], np.ones((h * len(rows), w * cols, 1), dtype=np.float32)], axis=2)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = out_path
    img.file_format = "PNG"
    img.save()
    for p in flat:
        os.remove(p)
    print("wrote", out_path)


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
    take_sheet(sc, rig, DRAUGR_ROWS, "draugr_takes", os.path.join(rv.OUT, "draugr_takes_sheet.png"), (0, 0, 0.95))


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
            elif ob.name == "Bark":
                ob.hide_render = i != 0
        bpy.context.view_layer.update()
        name = "elemental_%d" % i
        rv.shoot(sc, name, (0, -0.05, 1.0), -35, 8, 2.5)
        names.append(name)
    rv.shoot(sc, "elemental_front", (0, -0.05, 1.0), 0, 0, 2.5)
    for ob in bpy.data.objects:
        if ob.name.startswith("Leaves"):
            ob.hide_render = ob.name != "Leaves0"
        elif ob.name == "Bark":
            ob.hide_render = False
    take_sheet(sc, rig, ELEMENTAL_ROWS, "elemental_takes", os.path.join(rv.OUT, "elemental_takes_sheet.png"),
               (0, 0, 0.95))
    return names


def main():
    draugr()
    names = elementals()
    rbl.sheet([os.path.join(rv.OUT, n + ".png") for n in ["draugr_front", "draugr_three_quarter"] + names],
              os.path.join(rv.OUT, "enemies_sheet.png"), h=420)


if __name__ == "__main__":
    main()
