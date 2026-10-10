import bpy
src = r"E:\Projects\3D-Game\Chopper\SourceAssets\Motions\new\KayKit_Character_Animations_1.1\Animations\fbx\Rig_Medium\Rig_Medium_Tools.fbx"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = next(o for o in bpy.data.objects if o.type=="ARMATURE")
for b in arm.data.bones:
    parent = b.parent.name if b.parent else "-"
    print(f"{b.name} -> {parent}")
