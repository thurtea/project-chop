"""Render review PNGs (Workbench, 1024 px, vertex colors, flat shading).

Run:  blender -b -P render_views.py
Out:  Art/Renders/*.png

The props are appended into a temporary scene for the Warrior lineup; nothing
is saved back to the .blend files.
"""
import math
import os

import bpy
from mathutils import Euler, Matrix, Vector

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
CHAR = os.path.join(ART, "Blender", "chopper_character.blend")
PROPS = os.path.join(ART, "Blender", "chopper_props.blend")
OUT = os.path.join(ART, "Renders")

CHAR_PARTS = ["chopper_body", "chopper_head", "chopper_beard", "chopper_hair", "chopper_shirt",
              "chopper_vest", "chopper_belt", "chopper_chainmail", "chopper_boots"]


def setup_scene(scene):
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "VERTEX"
    sh.show_object_outline = True
    sh.object_outline_color = (0.05, 0.05, 0.05)
    sh.show_cavity = False
    sh.show_shadows = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.color = (0.72, 0.74, 0.77)
    scene.view_settings.view_transform = "Standard"


def camera(scene):
    cam = scene.camera
    if cam is None:
        data = bpy.data.cameras.new("ReviewCam")
        cam = bpy.data.objects.new("ReviewCam", data)
        scene.collection.objects.link(cam)
        scene.camera = cam
    return cam


def shoot(scene, name, target, yaw_deg, pitch_deg, ortho, dist=8.0):
    """yaw 0 = looking from the front (-Y side) toward +Y."""
    cam = camera(scene)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.data.clip_end = 100
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    look = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    target = Vector(target)
    cam.location = target - look * dist
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)


def append_props():
    with bpy.data.libraries.load(PROPS, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in ("chopper_axe", "chopper_shield")]
    objs = {}
    for ob in dst.objects:
        bpy.context.scene.collection.objects.link(ob)
        objs[ob.name] = ob
    return objs


def arm_point(s, t, palm_off=0.0):
    d = Vector((s * 1.0, -0.20, -1.0)).normalized()
    S = Vector((s * 0.30, 0.0, 1.27))
    return S + d * t, d


def main():
    os.makedirs(OUT, exist_ok=True)
    # ---------------- character turnaround
    bpy.ops.wm.open_mainfile(filepath=CHAR)
    sc = bpy.context.scene
    setup_scene(sc)
    for name in ("chopper_plate", "chopper_pauldrons"):
        bpy.data.objects[name].hide_render = True
    tgt = (0, 0, 0.90)
    shoot(sc, "char_front", tgt, 0, 0, 2.05)
    shoot(sc, "char_side", tgt, -90, 0, 2.05)      # from character's right (-X)
    shoot(sc, "char_back", tgt, 180, 0, 2.05)
    shoot(sc, "char_three_quarter", tgt, -35, 8, 2.05)
    # head close-up for beard / hair read
    shoot(sc, "char_head_three_quarter", (0, -0.05, 1.50), -35, 5, 0.75)

    # ---------------- Warrior lineup (props placed, not rigged)
    props = append_props()
    axe, shield = props["chopper_axe"], props["chopper_shield"]
    grip, d = arm_point(-1, 0.70)
    haft = Vector((-0.30, -0.45, 0.84)).normalized()
    q = haft.to_track_quat("Z", "Y")
    axe.matrix_world = Matrix.Translation(grip) @ q.to_matrix().to_4x4()
    # shield origin = strap centre = left forearm axis; local +Y toward the palm side,
    # local +Z toward the elbow (same convention as the rig's Shield_L socket)
    fore, d = arm_point(1, 0.45)
    fwd = Vector((0.0, -1.0, 0.0))
    v = -(fwd - d * fwd.dot(d)).normalized()
    palm = v.cross(d).normalized()
    sz = -d
    sx = palm.cross(sz).normalized()
    rot = Matrix((sx, palm, sz)).transposed().to_4x4()
    shield.matrix_world = Matrix.Translation(fore) @ rot
    shoot(sc, "warrior_lineup_front", (0, 0, 0.90), 0, 0, 2.2)
    shoot(sc, "warrior_lineup_three_quarter", (0, 0, 0.90), -35, 8, 2.2)
    for name in ("chopper_hair", "chopper_vest"):
        bpy.data.objects[name].hide_render = True
    for name in ("chopper_plate", "chopper_pauldrons"):
        bpy.data.objects[name].hide_render = False
    shoot(sc, "elite_lineup_front", (0, 0, 0.90), 0, 0, 2.2)
    shoot(sc, "elite_lineup_three_quarter", (0, 0, 0.90), -35, 8, 2.2)

    # ---------------- props alone
    for ob in list(sc.objects):
        if ob.name in CHAR_PARTS or ob.name in ("chopper_helmet", "chopper_plate", "chopper_pauldrons"):
            ob.hide_render = True
    axe.matrix_world = Matrix.Identity(4)
    shield.hide_render = True
    shoot(sc, "axe_front", (0, 0, 0.26), 0, 0, 1.0)
    shoot(sc, "axe_side", (0, 0, 0.26), -90, 0, 1.0)
    axe.hide_render = True
    shield.hide_render = False
    shield.matrix_world = Matrix.Identity(4)
    shoot(sc, "shield_front", (0, 0, -0.07), 0, 0, 1.1)
    shoot(sc, "shield_side", (0, 0, -0.07), -90, 0, 1.1)
    shoot(sc, "shield_back", (0, 0, -0.07), 180, 0, 1.1)


if __name__ == "__main__":
    main()
