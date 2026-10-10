# Converts Mixamo FBX files for Chopper: bones renamed to the Universal Animation Library names the game code uses,
# textures saved at 2048 next to the character, animation files saved without the mesh.
# Usage: blender -b --python mixamo_convert.py -- <source folder> <output folder>
import bpy, sys, os

src, out = sys.argv[sys.argv.index('--') + 1:]
os.makedirs(out, exist_ok=True)
anim_out = os.path.join(out, 'Animations')
os.makedirs(anim_out, exist_ok=True)

BASE = {
    'Hips': 'pelvis', 'Spine': 'spine_01', 'Spine1': 'spine_02', 'Spine2': 'spine_03', 'Neck': 'neck_01', 'Head': 'Head',
    'HeadTop_End': 'Head_leaf',
}
SIDE = {
    'Shoulder': 'clavicle', 'Arm': 'upperarm', 'ForeArm': 'lowerarm', 'Hand': 'hand',
    'UpLeg': 'thigh', 'Leg': 'calf', 'Foot': 'foot', 'ToeBase': 'ball', 'Toe_End': 'ball_leaf',
}
FINGERS = {'Thumb': 'thumb', 'Index': 'index', 'Middle': 'middle', 'Ring': 'ring', 'Pinky': 'pinky'}


def new_name(name):
    n = name.split(':')[-1]
    if n in BASE:
        return BASE[n]
    for side, s in (('Left', 'l'), ('Right', 'r')):
        if not n.startswith(side):
            continue
        rest = n[len(side):]
        if rest in SIDE:
            return f'{SIDE[rest]}_{s}'
        if rest.startswith('Hand'):
            f = rest[4:]
            for key, ual in FINGERS.items():
                if f.startswith(key):
                    i = int(f[len(key):])
                    return f'{ual}_04_leaf_{s}' if i == 4 else f'{ual}_0{i}_{s}'
    return n


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    renames = {}
    for b in arm.data.bones:
        renames[b.name] = new_name(b.name)
    for old, new in renames.items():
        arm.data.bones[old].name = new
    # Vertex groups follow bone renames automatically in Blender; actions keep data paths by name, so fix them.
    for act in bpy.data.actions:
        for fc in getattr(act, 'fcurves', []):
            for old, new in renames.items():
                key = f'pose.bones["{old}"]'
                if key in fc.data_path:
                    fc.data_path = fc.data_path.replace(key, f'pose.bones["{new}"]')
        for layer in getattr(act, 'layers', []):
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for fc in bag.fcurves:
                        for old, new in renames.items():
                            key = f'pose.bones["{old}"]'
                            if key in fc.data_path:
                                fc.data_path = fc.data_path.replace(key, f'pose.bones["{new}"]')
    # The scene keeps Blender's default 250 frames otherwise, and the export bakes the clip out to that length.
    if bpy.data.actions:
        start, end = bpy.data.actions[0].frame_range
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = int(round(start)), int(round(end))
    unmapped = [n for n in renames.values() if n.startswith('mixamorig') or ':' in n]
    print('RENAMED', len(renames), 'unmapped', unmapped)
    return arm


def export(path, with_mesh):
    if not with_mesh:
        for o in list(bpy.data.objects):
            if o.type == 'MESH':
                bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False, object_types={'ARMATURE', 'MESH'},
                             add_leaf_bones=False, bake_anim=not with_mesh, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=False, apply_scale_options='FBX_SCALE_ALL',
                             path_mode='RELATIVE', mesh_smooth_type='FACE')


# The character, with its textures at 2048 (skipped when the folder only has motions).
if os.path.exists(os.path.join(src, 'character.fbx')):
    load(os.path.join(src, 'character.fbx'))
    for img in list(bpy.data.images):
        if img.size[0] == 0:
            continue
        kind = img.filepath.replace('\\', '/').split('/')[-1].split('_')[-1].split('.')[0]  # Diffuse, Normal, ...
        name = f'Chopper_{kind}.png'
        img.scale(min(2048, img.size[0]), min(2048, img.size[1]))
        img.filepath_raw = os.path.join(out, name)
        img.file_format = 'PNG'
        img.save()
        print('TEXTURE', name, img.size[:])
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.name = 'Chopper_Body'
    export(os.path.join(out, 'Chopper.fbx'), True)
    print('CHARACTER done')

for f in sorted(os.listdir(src)):
    if not f.lower().endswith('.fbx') or f == 'character.fbx':
        continue
    load(os.path.join(src, f))
    name = f[:-4].replace(' ', '_').replace('(', '').replace(')', '')
    export(os.path.join(anim_out, f'Mixamo_{name}.fbx'), False)
    print('ANIM', name)
