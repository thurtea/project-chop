import bpy, os
src = r"E:\Projects\3D-Game\Chopper\SourceAssets\Motions\new\KayKit_Character_Animations_1.1\Animations\fbx\Rig_Medium\Rig_Medium_Tools.fbx"
out = r"E:\Projects\3D-Game\Chopper\SourceAssets\Motions\kaykit-slots"
os.makedirs(out, exist_ok=True)
mapping = {
    "Pickaxing": "mine",
    "Fishing_Cast": "fish_cast",
    "Fishing_Idle": "fish_wait",
    "Fishing_Struggling": "fish_fight",
    "Fishing_Catch": "fish_catch",
}
rename = {
    "hips": "Hips", "spine": "Spine", "chest": "Chest", "head": "Head",
    "upperarm.l": "LeftUpperArm", "lowerarm.l": "LeftLowerArm", "wrist.l": "LeftHand",
    "upperarm.r": "RightUpperArm", "lowerarm.r": "RightLowerArm", "wrist.r": "RightHand",
    "upperleg.l": "LeftUpperLeg", "lowerleg.l": "LeftLowerLeg", "foot.l": "LeftFoot", "toes.l": "LeftToes",
    "upperleg.r": "RightUpperLeg", "lowerleg.r": "RightLowerLeg", "foot.r": "RightFoot", "toes.r": "RightToes",
}

def retarget_curves(act):
    curves = list(getattr(act, "fcurves", []) or [])
    for layer in getattr(act, "layers", []) or []:
        for strip in getattr(layer, "strips", []) or []:
            for bag in getattr(strip, "channelbags", []) or []:
                curves.extend(getattr(bag, "fcurves", []) or [])
    for fc in curves:
        for old, new in rename.items():
            key = f'pose.bones["{old}"]'
            if key in fc.data_path:
                fc.data_path = fc.data_path.replace(key, f'pose.bones["{new}"]')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
for bone in arm.data.bones:
    if bone.name in rename:
        bone.name = rename[bone.name]
for act in bpy.data.actions:
    retarget_curves(act)
for o in list(bpy.data.objects):
    if o.type == "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
by_tail = {a.name.split("|")[-1]: a for a in bpy.data.actions}
for key, slot in mapping.items():
    act = by_tail.get(key)
    if act is None:
        print("MISSING", key)
        continue
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    slots = getattr(act, "slots", None)
    if slots:
        try:
            arm.animation_data.action_slot = slots[0]
        except Exception as e:
            print("SLOT", key, e)
    start, end = act.frame_range
    bpy.context.scene.frame_start = int(round(start))
    bpy.context.scene.frame_end = int(round(end))
    path = os.path.join(out, slot + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={"ARMATURE"},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
    )
    print("WROTE", slot, os.path.getsize(path))
