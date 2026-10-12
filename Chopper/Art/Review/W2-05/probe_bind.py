import bpy, os
from mathutils import Matrix
HERE=os.path.dirname(os.path.abspath(__file__)); EXP=os.path.normpath(os.path.join(HERE,"..","..","Export"))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(EXP,"chopper.fbx"), use_anim=False)
arm=[o for o in bpy.data.objects if o.type=="ARMATURE"][0]
I=Matrix.Identity(4); worst=(0,"")
for pb in arm.pose.bones:
    d=max(abs(pb.matrix_basis[i][j]-I[i][j]) for i in range(4) for j in range(4))
    if d>worst[0]: worst=(d,pb.name)
open(os.path.join(HERE,"probe_bind_report.txt"),"w").write("node transform vs bind (no anim import): max |matrix_basis - I| = %.2e at %s; action=%s\n"%(worst[0],worst[1],arm.animation_data.action if arm.animation_data else None))
