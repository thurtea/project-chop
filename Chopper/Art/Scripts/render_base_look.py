"""Render the starting Chopper (base look, no armour) holding the bear-head axe, textured
with the atlas, and a side-by-side sheet against concept 1.5.

Run:  blender -b -P render_base_look.py
In:   Art/Blender/chopper_rigged.blend, chopper_props.blend, Art/Textures/chopper_atlas.png,
      ../chopper-3D-concept-1.5.jpg (read only, nothing is saved)
Out:  Art/Renders/base_look_front.png, base_look_three_quarter.png, base_look_gameplay.png,
      axe_bear_front.png, axe_bear_three_quarter.png, base_look_vs_concept.png

Base look (concept 1.5, user decision 2026-10-11): hair, beard, head, body, shirt, vest,
belt, boots. Armour hidden: helmet, chainmail, bracers, plate, pauldrons. The axe hangs on
Weapon_R with an identity local transform (what Unity does).
Shading: EEVEE, both materials sample the atlas PNG with Closest interpolation (the point
filtering Unity uses for the pixel-art atlas), one warm sun plus a cool sky fill. Back faces
are culled like Unity's default.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_views as rv  # noqa: E402

ART = rv.ART
RIGGED = os.path.join(ART, "Blender", "chopper_rigged.blend")
ATLAS = os.path.join(ART, "Textures", "chopper_atlas.png")
CONCEPT = os.path.normpath(os.path.join(ART, "..", "..", "chopper-3D-concept-1.5.jpg"))
OUT = rv.OUT
ARMOUR = ("chopper_helmet", "chopper_chainmail", "chopper_bracers", "chopper_plate", "chopper_pauldrons")
POSE_TAKE, POSE_FRAME = "Idle", 16
BG = (0.80, 0.81, 0.83)


def atlas_materials():
    img = bpy.data.images.load(ATLAS)
    img.colorspace_settings.name = "sRGB"
    for mat in bpy.data.materials:
        if mat.name not in ("M_Chopper", "M_Chopper_Gear"):
            continue
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.interpolation = "Closest"
        uvn = nt.nodes.new("ShaderNodeUVMap")
        uvn.uv_map = "UVMap"
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = 0.85
        outn = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(uvn.outputs["UV"], tex.inputs["Vector"])
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        nt.links.new(bsdf.outputs["BSDF"], outn.inputs["Surface"])
        mat.use_backface_culling = True


def setup_eevee(sc):
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x = sc.render.resolution_y = 1024
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    sc.world.use_nodes = True
    bgn = sc.world.node_tree.nodes.get("Background")
    bgn.inputs["Color"].default_value = (*[c ** 2.2 for c in BG], 1.0)
    bgn.inputs["Strength"].default_value = 1.0
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.2
    sun.color = (1.0, 0.94, 0.84)
    try:
        sun.angle = math.radians(8)
    except AttributeError:
        pass
    so = bpy.data.objects.new("Sun", sun)
    sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(50), 0, math.radians(-35))
    for ob in sc.objects:
        if ob.type == "LIGHT" and ob is not so:
            ob.hide_render = True


def shoot_persp(sc, name, target, yaw_deg, pitch_deg, dist, lens, res):
    cam = rv.camera(sc)
    cam.data.type = "PERSP"
    cam.data.lens = lens
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    look = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    cam.location = Vector(target) - look * dist
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    old = (sc.render.resolution_x, sc.render.resolution_y)
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    sc.render.resolution_x, sc.render.resolution_y = old
    print("rendered", sc.render.filepath)


def load_rgb(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]   # top row first
    bpy.data.images.remove(img)
    return a


def resize_nearest(a, h):
    sh, sw = a.shape[:2]
    w = max(1, round(sw * h / sh))
    ys = (np.arange(h) * sh / h).astype(int)
    xs = (np.arange(w) * sw / w).astype(int)
    return a[ys][:, xs]


def sheet(paths, out_path, h=640, gap=12):
    """Concept and renders side by side at one height (nearest resampling keeps the pixels)."""
    tiles = [resize_nearest(load_rgb(p), h) for p in paths]
    w = sum(t.shape[1] for t in tiles) + gap * (len(tiles) + 1)
    canvas = np.ones((h + 2 * gap, w, 3), dtype=np.float32) * 0.15
    x = gap
    for t in tiles:
        canvas[gap:gap + h, x:x + t.shape[1]] = t
        x += t.shape[1] + gap
    img = bpy.data.images.new("sheet", w, h + 2 * gap, alpha=False)
    rgba = np.concatenate([canvas[::-1], np.ones(canvas.shape[:2] + (1,), dtype=np.float32)], axis=2)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = out_path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("wrote", out_path)


def main():
    bpy.ops.wm.open_mainfile(filepath=RIGGED)
    sc = bpy.context.scene
    setup_eevee(sc)
    rig = bpy.data.objects["chopper_rig"]
    for tr in rig.animation_data.nla_tracks:
        tr.mute = tr.name != POSE_TAKE
    sc.frame_set(POSE_FRAME)
    for name in ARMOUR:
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
    atlas_materials()
    bpy.context.view_layer.update()

    tgt = (0, -0.05, 0.92)
    rv.shoot(sc, "base_look_front", tgt, 0, 0, 2.15)
    rv.shoot(sc, "base_look_three_quarter", tgt, -35, 8, 2.15)
    # gameplay size: third-person camera about 4.5 m away and above, character ~200 px tall
    shoot_persp(sc, "base_look_gameplay", (0, 0, 0.95), -30, 18, 5.0, 50, (320, 260))

    # the axe on its own, standing at its grip origin beside the character (character's right)
    with bpy.data.libraries.load(rv.PROPS, link=False) as (src, dst):
        dst.objects = ["chopper_axe_bear"]
    solo = dst.objects[0]
    solo.name = "chopper_axe_bear_solo"
    sc.collection.objects.link(solo)
    solo.matrix_world = Matrix.Translation((-0.95, -0.05, 0.17))
    tgt = (-0.40, -0.05, 0.92)
    rv.shoot(sc, "axe_bear_front", tgt, 0, 0, 2.35)
    rv.shoot(sc, "axe_bear_three_quarter", tgt, -35, 8, 2.35)

    sheet([CONCEPT] + [os.path.join(OUT, n + ".png") for n in
                       ("base_look_front", "base_look_three_quarter", "base_look_gameplay")],
          os.path.join(OUT, "base_look_vs_concept.png"))


if __name__ == "__main__":
    main()
