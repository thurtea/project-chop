"""Export chopper.fbx (rig + skinned modules + takes), chopper_axe.fbx, chopper_shield.fbx.

Run:  blender -b -P export_fbx.py
In:   Art/Blender/chopper_rigged.blend, Art/Blender/chopper_props.blend (read only)
Out:  Art/Export/*.fbx

Settings (shared): Apply Scalings = FBX All, Forward -Z, Up Y, apply unit scale,
no space-transform bake, primary bone axis Y / secondary X (Blender's native bone
frame, so socket bones keep the exact frame of the props: a prop parented to a socket
with identity local transform sits in the hand / on the arm), no leaf bones,
no embedded media, flat (face) smoothing, armature modifier not applied
(skin exported as clusters). Animation: baked, one take per NLA strip
(take names = strip names Idle and Walk), every frame keyed, no simplification.
"""
import os

import bpy
from mathutils import Matrix

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
RIGGED = os.path.join(ART, "Blender", "chopper_rigged.blend")
PROPS = os.path.join(ART, "Blender", "chopper_props.blend")
EXPORT = os.path.join(ART, "Export")

PARTS = ["chopper_body", "chopper_head", "chopper_beard", "chopper_hair", "chopper_shirt",
         "chopper_vest", "chopper_belt", "chopper_chainmail", "chopper_boots", "chopper_helmet",
         "chopper_plate", "chopper_pauldrons"]

COMMON = dict(
    use_selection=True,
    use_visible=False,
    use_active_collection=False,
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    use_space_transform=True,
    bake_space_transform=False,
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=False,
    mesh_smooth_type="FACE",
    use_mesh_edges=False,
    use_tspace=False,
    use_custom_props=False,
    colors_type="SRGB",
    path_mode="AUTO",
    embed_textures=False,
    batch_mode="OFF",
)

CHAR_SETTINGS = dict(
    COMMON,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,     # keep the non-deforming socket bones
    armature_nodetype="NULL",
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
)

PROP_SETTINGS = dict(COMMON, object_types={"MESH"}, bake_anim=False)


def select_only(names):
    bpy.ops.object.select_all(action="DESELECT")
    for n in names:
        ob = bpy.data.objects[n]
        ob.hide_set(False)
        ob.select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects[names[0]]


def main():
    os.makedirs(EXPORT, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=RIGGED)
    rig = bpy.data.objects["chopper_rig"]
    rig.animation_data.action = None
    # Bind-pose node transforms are whatever pose is current when the exporter writes
    # the models, which is after it bakes. Strips start at frame 1; with no hold
    # extrapolation, frame 0 is the rest pose, so the FBX nodes are the A-pose.
    # pose_position stays POSE so the take bake still evaluates the actions.
    rig.data.pose_position = "POSE"
    for tr in rig.animation_data.nla_tracks:
        tr.mute = False
        for strip in tr.strips:
            strip.extrapolation = "NOTHING"
    bpy.context.scene.frame_set(0)
    for pb in rig.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    for tr in rig.animation_data.nla_tracks:
        tr.mute = False
        print("NLA", tr.name, [(s.name, s.action.name, s.frame_start, s.frame_end) for s in tr.strips])
    select_only(["chopper_rig"] + PARTS)
    out = os.path.join(EXPORT, "chopper.fbx")
    bpy.ops.export_scene.fbx(filepath=out, **CHAR_SETTINGS)
    print("exported", out, os.path.getsize(out), "bytes")

    bpy.ops.wm.open_mainfile(filepath=PROPS)
    for name in ("chopper_axe", "chopper_shield"):
        select_only([name])
        out = os.path.join(EXPORT, name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=out, **PROP_SETTINGS)
        print("exported", out, os.path.getsize(out), "bytes")
    print("settings (character):")
    for k in sorted(CHAR_SETTINGS):
        print("  %s = %r" % (k, CHAR_SETTINGS[k]))


if __name__ == "__main__":
    main()
