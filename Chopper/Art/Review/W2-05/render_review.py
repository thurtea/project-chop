"""W2-05 review renders with the CURRENT atlas. Opens the character blend, never saves it. Output: this folder."""
import os, sys, math, bpy
HERE=os.path.dirname(os.path.abspath(__file__)); ART=os.path.normpath(os.path.join(HERE,"..",".."))
sys.path.insert(0, os.path.join(ART,"Scripts"))
import render_views as rv
rv.OUT = HERE
ATLAS=os.path.join(ART,"Textures","chopper_atlas.png")
def use_atlas(space):
    img=bpy.data.images.load(ATLAS, check_existing=True); img.colorspace_settings.name=space
    for mat in bpy.data.materials:
        if not mat.node_tree: continue
        bsdf=next((n for n in mat.node_tree.nodes if n.type=="BSDF_PRINCIPLED"),None)
        if not bsdf: continue
        tex=next((n for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"),None) or mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image=img; mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bpy.ops.wm.open_mainfile(filepath=rv.CHAR)
sc=bpy.context.scene
for eng in ("BLENDER_EEVEE_NEXT","BLENDER_EEVEE"):
    try: sc.render.engine=eng; break
    except TypeError: pass
sc.view_settings.view_transform="Standard"
sc.world=sc.world or bpy.data.worlds.new("W"); sc.world.color=(0.75,0.78,0.82)
sun=bpy.data.objects.new("S", bpy.data.lights.new("S","SUN")); sun.data.energy=4; sc.collection.objects.link(sun); sun.rotation_euler=(0.85,0.15,-0.5)
props=rv.append_props()
axe,shield=props["chopper_axe"],props["chopper_shield"]
# props beside the character, not rigged (lineup)
from mathutils import Matrix
axe.matrix_world=Matrix.Translation((-0.75,0,0.2)); shield.matrix_world=Matrix.Translation((0.75,0,0.7))
CONF={"worker":["chopper_helmet","chopper_plate","chopper_pauldrons"],
      "warrior":["chopper_hair","chopper_plate","chopper_pauldrons"]}
for space,tag in (("sRGB","asis"),("Linear Rec.709","linfix")):
    use_atlas(space)
    for cname,hide in CONF.items():
        for ob in sc.objects:
            if ob.type=="MESH": ob.hide_render = ob.name in hide
        sc.render.resolution_x=sc.render.resolution_y=1024
        rv.shoot(sc,"rev_%s_%s_front"%(cname,tag),(0,0,0.9),0,0,2.4)
        rv.shoot(sc,"rev_%s_%s_head"%(cname,tag),(0,-0.05,1.55),-20,5,0.75)
        # gameplay size: ~180 px tall character
        sc.render.resolution_x=sc.render.resolution_y=200
        rv.shoot(sc,"rev_%s_%s_gameplay"%(cname,tag),(0,0,0.9),-35,20,2.4)
