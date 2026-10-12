import bpy, os, math
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__)); EXP=os.path.normpath(os.path.join(HERE,"..","..","Export"))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(EXP,"chopper.fbx"))
arm=[o for o in bpy.data.objects if o.type=="ARMATURE"][0]
for o in bpy.data.objects:
    if o.name.startswith(("chopper_hair","chopper_plate","chopper_pauldrons")): o.hide_render=True
def att(fn,bone):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=os.path.join(EXP,fn))
    o=[x for x in set(bpy.data.objects)-before if x.type=="MESH"][0]
    for x in set(bpy.data.objects)-before:
        if x!=o: x.hide_render=True
    o.parent=None; o.matrix_world.identity(); return o
axe=att("chopper_axe.fbx","Weapon_R"); sh=att("chopper_shield.fbx","Shield_L")
sc=bpy.context.scene; sc.render.engine="BLENDER_WORKBENCH"; sc.display.shading.color_type="VERTEX" if False else "MATERIAL"
sc.display.shading.show_object_outline=True; sc.render.resolution_x=sc.render.resolution_y=800
cam=bpy.data.objects.new("C",bpy.data.cameras.new("C")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type="ORTHO"; cam.data.ortho_scale=1.0
for an,f in (("chopper_rig|Attack_2",15),("chopper_rig|Attack_1",9)):
    arm.animation_data.action=bpy.data.actions[an]
    a=bpy.data.actions[an]
    if a.slots: arm.animation_data.action_slot=a.slots[0]
    sc.frame_set(f)
    axe.matrix_world=arm.matrix_world@arm.pose.bones["Weapon_R"].matrix
    sh.matrix_world=arm.matrix_world@arm.pose.bones["Shield_L"].matrix
    head=arm.matrix_world@arm.pose.bones["Head"].head
    for tag,d in (("side",Vector((-1,0,0))),("front",Vector((0,-1,0))),("tq",Vector((-0.7,-0.7,0.15)).normalized())):
        cam.location=head+d*5; cam.rotation_euler=(-d).to_track_quat("-Z","Y").to_euler()
        sc.render.filepath=os.path.join(HERE,"probe_%s_f%d_%s.png"%(an.split("|")[1],f,tag)); bpy.ops.render.render(write_still=True)
