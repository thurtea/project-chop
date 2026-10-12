"""Per-take prop clipping and ground probe for all 7 takes (Warrior visible set). Run with --factory-startup."""
import bpy, bmesh, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=os.path.dirname(os.path.abspath(__file__)); EXP=os.path.normpath(os.path.join(HERE,"..","..","Export"))
out=[]
def log(s): print(s); out.append(s)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(EXP,"chopper.fbx"))
arm=[o for o in bpy.data.objects if o.type=="ARMATURE"][0]
def imp(fn):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=os.path.join(EXP,fn))
    o=[x for x in set(bpy.data.objects)-before if x.type=="MESH"][0]
    return [o.matrix_basis @ v.co for v in o.data.vertices]
axe_v=imp("chopper_axe.fbx"); sh_v=imp("chopper_shield.fbx")
WARRIOR=("chopper_body","chopper_head","chopper_beard","chopper_shirt","chopper_vest","chopper_belt","chopper_chainmail","chopper_boots","chopper_helmet")
meshes=[o for o in bpy.data.objects if o.type=="MESH" and o.name.startswith(WARRIOR) and o.parent==arm]
def trees():
    dg=bpy.context.evaluated_depsgraph_get(); res=[]; lowest=1e9
    for o in meshes:
        ev=o.evaluated_get(dg); me=ev.to_mesh(); bm=bmesh.new(); bm.from_mesh(me); bm.transform(o.matrix_world)
        bm.normal_update()
        lowest=min(lowest, min(v.co.z for v in bm.verts))
        res.append((o.name,BVHTree.FromBMesh(bm))); bm.free(); ev.to_mesh_clear()
    return res, lowest
def pen(ws, ts, excl_c, excl_r):
    n=0; deep=0.0; who={}
    for w in ws:
        if (w-excl_c).length<excl_r: continue
        for name,t in ts:
            loc,nrm,i,d=t.find_nearest(w)
            if loc is not None and d<0.08 and (w-loc).dot(nrm)<-1e-4:
                n+=1; deep=max(deep,d); who[name]=who.get(name,0)+1; break
    return n,deep,who
acts=sorted(a.name for a in bpy.data.actions)
for an in acts:
    act=bpy.data.actions[an]; arm.animation_data.action=act
    if hasattr(arm.animation_data,"action_slot") and act.slots: arm.animation_data.action_slot=act.slots[0]
    f0,f1=int(act.frame_range[0]),int(act.frame_range[1])
    worst_a=(0,0,0,{}); worst_s=(0,0,0,{}); low=1e9; lowf=None
    for f in range(f0,f1+1,2):
        bpy.context.scene.frame_set(f)
        ts,lo=trees()
        if lo<low: low,lowf=lo,f
        W=arm.matrix_world@arm.pose.bones["Weapon_R"].matrix
        S=arm.matrix_world@arm.pose.bones["Shield_L"].matrix
        aw=[W@v for v in axe_v]; sw=[S@v for v in sh_v]
        a=pen(aw,ts,W.translation,0.11); s=pen(sw,ts,S.translation,0.13)
        if a[0]>worst_a[0]: worst_a=(a[0],a[1],f,a[2])
        if s[0]>worst_s[0]: worst_s=(s[0],s[1],f,s[2])
    log("%-22s frames %d-%d | axe-in-body worst %d verts depth %.1f mm @f%s %s | shield-in-body worst %d verts depth %.1f mm @f%s %s | lowest body vertex Z %.3f @f%s"%(
        an,f0,f1,worst_a[0],worst_a[1]*1000,worst_a[2],worst_a[3],worst_s[0],worst_s[1]*1000,worst_s[2],worst_s[3],low,lowf))
open(os.path.join(HERE,"probe_takes_report.txt"),"w").write("\n".join(out)+"\n")
