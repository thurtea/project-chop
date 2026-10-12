"""Compare atlas pixels with the gray-box vertex colours. Opens the blend read-only (never saves)."""
import bpy, os, math
HERE=os.path.dirname(os.path.abspath(__file__)); ART=os.path.normpath(os.path.join(HERE,"..",".."))
out=[]
def l2s(c): return 12.92*c if c<=0.0031308 else 1.055*c**(1/2.4)-0.055
img=bpy.data.images.load(os.path.join(ART,"Textures","chopper_atlas.png"))
W,H=img.size; px=list(img.pixels)   # Blender byte image: pixels are the stored (sRGB-encoded) values /255
out.append("atlas %dx%d colourspace %s"%(W,H,img.colorspace_settings.name))
def sample(u,v):
    x=min(W-1,int(u*W)); y=min(H-1,int(v*H)); i=(y*W+x)*4; return px[i:i+3]
bpy.ops.wm.open_mainfile(filepath=os.path.join(ART,"Blender","chopper_character.blend"))
img=bpy.data.images.load(os.path.join(ART,"Textures","chopper_atlas.png")); px=list(img.pixels)
for name in ("chopper_hair","chopper_beard","chopper_head","chopper_body","chopper_shirt","chopper_vest","chopper_boots","chopper_chainmail","chopper_helmet"):
    ob=bpy.data.objects[name]; me=ob.data; col=me.color_attributes.get("Col"); uv=me.uv_layers[0].data
    errs_lin=[];errs_srgb=[]; ex=None
    for p in list(me.polygons)[::max(1,len(me.polygons)//40)]:
        li=p.loop_indices
        u=sum(uv[i].uv.x for i in li)/len(li); v=sum(uv[i].uv.y for i in li)/len(li)
        vc=[sum(col.data[i].color[k] for i in li)/len(li) for k in range(3)]
        a=sample(u,v)
        errs_lin.append(max(abs(a[k]-vc[k]) for k in range(3)))
        errs_srgb.append(max(abs(a[k]-l2s(vc[k])) for k in range(3)))
        if ex is None: ex=(vc,a)
    out.append("%-18s vc(linear)=%s atlas=%s sRGB(vc)=%s | mean |atlas-vc_lin| %.3f, mean |atlas-sRGB(vc)| %.3f"%(name,
        tuple(round(c,3) for c in ex[0]),tuple(round(c,3) for c in ex[1]),tuple(round(l2s(c),3) for c in ex[0]),
        sum(errs_lin)/len(errs_lin),sum(errs_srgb)/len(errs_srgb)))
# cell sizes
for name,tile in (("chopper_head",(0.68,0.26,0.99,0.49)),("chopper_body",(0.01,0.51,0.38,0.99)),("chopper_shield",(0.76,0.01,0.99,0.24))):
    pass
gut=sum(1 for i in range(0,len(px),4) if abs(px[i]-0.12)<0.004 and abs(px[i+1]-0.10)<0.004 and abs(px[i+2]-0.09)<0.004)
out.append("pixels equal to the gutter fill (0.12,0.10,0.09): %.1f%% of atlas"%(100*gut/(len(px)/4)))
open(os.path.join(HERE,"probe_atlas_report.txt"),"w").write("\n".join(out)+"\n")
