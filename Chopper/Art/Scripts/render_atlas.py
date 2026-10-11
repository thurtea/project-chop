"""Render the character and props with the atlas (not vertex colours). Does not save the blends.

Run:  blender -b -P render_atlas.py
Out:  Art/Renders/atlas_char_front.png, atlas_shield_front.png, atlas_axe_front.png,
      atlas_warrior_front.png
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_views as rv  # noqa: E402

ATLAS = os.path.join(rv.ART, "Textures", "chopper_atlas.png")


def use_atlas():
    img = bpy.data.images.load(ATLAS, check_existing=True)
    img.colorspace_settings.name = "sRGB"
    for mat in bpy.data.materials:
        if mat.node_tree is None:
            continue
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None:
            continue
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = img
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])


def setup(scene):
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = eng
            break
        except TypeError:
            continue
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.color = (0.85, 0.86, 0.88)
    sun = bpy.data.lights.new("ReviewSun", "SUN")
    sun.energy = 5.0
    ob = bpy.data.objects.new("ReviewSun", sun)
    scene.collection.objects.link(ob)
    ob.rotation_euler = (0.85, 0.15, 0.6)


def main():
    os.makedirs(rv.OUT, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=rv.CHAR)
    sc = bpy.context.scene
    setup(sc)
    use_atlas()
    rv.shoot(sc, "atlas_char_front", (0, 0, 0.90), 0, 0, 2.05)
    rv.shoot(sc, "atlas_head", (0, -0.05, 1.55), 0, 0, 0.7)
    props = rv.append_props()
    use_atlas()
    axe, shield = props["chopper_axe"], props["chopper_shield"]
    for ob in list(sc.objects):
        if ob.name in rv.CHAR_PARTS or ob.name == "chopper_helmet":
            ob.hide_render = True
    axe.matrix_world = rv.Matrix.Identity(4)
    shield.hide_render = True
    rv.shoot(sc, "atlas_axe_front", (0, 0, 0.26), 0, 0, 1.0)
    axe.hide_render = True
    shield.hide_render = False
    shield.matrix_world = rv.Matrix.Identity(4)
    rv.shoot(sc, "atlas_shield_front", (0, 0, -0.07), 0, 0, 1.1)
    print("atlas renders written")


if __name__ == "__main__":
    main()
