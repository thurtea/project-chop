import bpy, os
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__)); EXP=os.path.join(HERE,"..","..","Export")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(EXP,"chopper.fbx"))
out=[]
for o in bpy.data.objects:
    if o.type!="MESH": continue
    ws=[o.matrix_world@v.co for v in o.data.vertices]
    band=[w for w in ws if 0.9<w.z<1.4]
    if band: out.append("%-20s back maxY(z 0.9-1.4) %.3f  minY %.3f"%(o.name,max(w.y for w in band),min(w.y for w in band)))
open(os.path.join(HERE,"probe_back_report.txt"),"w").write("\n".join(out)+"\n")
