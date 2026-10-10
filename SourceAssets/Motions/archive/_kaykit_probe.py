import bpy, os
root = r"E:\Projects\3D-Game\Chopper\SourceAssets\Motions\new\KayKit_Character_Animations_1.1\Animations\fbx\Rig_Medium"
for name in sorted(os.listdir(root)):
    if not name.endswith(".fbx"):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(root, name))
    print("FILE", name)
    arm = next((o for o in bpy.data.objects if o.type=="ARMATURE"), None)
    if arm:
        bones = [b.name for b in arm.data.bones]
        print("BONES", len(bones), ",".join(bones[:12]))
    for a in bpy.data.actions:
        tail = a.name.split("|")[-1]
        print(f"  {tail}|{a.frame_range[0]:.0f}|{a.frame_range[1]:.0f}")
