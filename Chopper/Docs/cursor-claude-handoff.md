# Cursor handoff after the Claude Code session

Written 2026-10-11 for the next Claude Code session. Read this with `Docs/TASKBOARD.md` and `Docs/PRODUCTION_BRIEF.md`. Do not redo Week 1. Do not commit, push, delete, or touch the old project at the repo root unless the user says so.

## Where Claude stopped

Week 1 is done and independently reviewed (`W1-06`). The user said "Go" for Week 2. Claude started `W2-01`, `W2-02`, and `W2-03` in parallel, then all three agents died on a session rate limit before they wrote any Week 2 files. The task board still said those three were in progress. Nothing from that attempt is on disk.

Claude's open choices, still in force:

- Do not commit. Branch is `rebuild/chopper-assets`. Everything Week 1 and Week 2 is untracked or uncommitted.
- Capsule radius stays 0.37 m.
- Axe blades stay front and back (concept 1.7), not sideways.
- Do not delete the old Unity project at the repo root.

## What Cursor did after that

Cursor continued as the same manager, in this repo, with Blender 5.2.2 at `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` and Unity `6000.5.10f1` at `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe`.

### W2-01 done

- `chopper_helmet` added in `Art/Scripts/build_chopper.py` (`build_helmet`). Steel cap, nose guard, two horns, cheek plates, rivet band. 1,324 tris. Material `M_Chopper_Gear`. Top of the helmet is below the hair, so the character is still 1.800 m.
- `Art/Scripts/uv_atlas.py` gives every mesh one `UVMap` inside 0–1. Each object has its own atlas tile, so islands from different meshes do not overlap. Layout is in `Art/Textures/atlas_tiles.txt`.
- `Art/Textures/chopper_atlas.png` is 2048×2048. It is not packed into the .blend files (`check_assets.py` still requires no image datablocks in the blends).
- Texel density is reported and is not uniform: about 9,600 to 23,500 px/m, because each mesh has a fixed tile.
- `check_assets.py`: **84 PASS, 0 FAIL**. Warrior (helmet instead of hair, plus axe and shield) is 11,328 tris. Worker plus axe and shield is still 11,984.

### W2-02 done

- `rig_chopper.py` skins the helmet 100% to `Head`. `Shield_Back` origin moved to `(0, 0.46, 1.12)` so the stowed shield clears the torso.
- `animate_chopper.py` authors seven takes. Idle, Walk, and Run loop. Attack_1, Attack_2, HitReact, and Death are one-shots (last frame is not a copy of the first). Run is 2.20 m/s, 20 frames. Attack clips are 24 frames, HitReact 15, Death 36. Walk is still 0.75 m/s.
- `export_fbx.py` exports at frame 0 with NLA strip extrapolation `NOTHING` and `pose_position = POSE`. That makes the FBX node transforms the A-pose bind, while the takes still bake. Do not set `pose_position = REST` during export: Blender then bakes every take as the rest pose.
- Imported rest pose: left upper arm 45.0° below horizontal. Shield_Back: 0 penetrating verts, mean Y 0.534. Run planted slip 0.5 mm/frame. Walk planted slip 0.4 mm/frame.
- `check_rig.py`: **124 PASS, 0 FAIL**. `Art/Export/chopper.fbx` is about 1.9 MB and includes the helmet plus all seven takes. No leaf bones, no embedded images.

### W2-03 done

- `ChopperVertexColorLit.shader` samples `_BaseMap` when `_UseAtlas` is 1. Vertex colour remains the fallback.
- `ChopperImport.cs` copies the atlas to `Assets/Game/Characters/Chopper/Textures/chopper_atlas.png`, assigns it to `M_Chopper` and `M_Chopper_Gear`, imports all seven clips, and builds the controller (Speed for Idle/Walk/Run at 0.1 and 1.5; triggers Attack1, Attack2, Hit, Die). Death does not transition out.
- `Chopper.prefab` is the Warrior: hair renderer off, helmet on, axe on `Weapon_R`, shield on `Shield_L`, both through `AttachProp`.
- `Chopper_Worker.prefab` is hair on, helmet off, no axe, no shield.
- `ChopperCheck.cs` expects 10 skinned meshes, checks the seven clips, and does a real mesh-against-mesh shield test (0 verts inside the body; a shield pushed 8 cm in is caught at 116 verts). It also steps `ChopperTestDriver` the way Play mode does.
- `ChopperTestDriver`: Space cycles Idle, Walk (0.75 m/s), Run (2.2 m/s). Keys 1, 2, H, K fire Attack1, Attack2, Hit, Die.
- Unity `ChopperCheck.RunChecks` with a graphics device: **101 PASS, 0 FAIL**. Screenshots are in `Art/Renders/unity_*.png`. Import log: `Art/Renders/unity_import.log`. Check log: `Art/Renders/unity_check.log`.

### W2-04 in progress after this handoff was first written

Cursor then replaced the tile overlay with object-space paint in `uv_atlas.py` (`detail()`). Review renders:

- `Art/Renders/atlas_char_front.png` — plaid shirt, vest stitches and gray patch, chainmail checker, green eyes
- `Art/Renders/atlas_head.png` — eyes are small green slits
- `Art/Renders/atlas_shield_front.png` — wood planks, a block axe, two green pines
- `Art/Renders/atlas_axe_front.png` — grip bands and a dot pattern on the blades, not carved knotwork

The same PNG was copied into `Assets/Game/Characters/Chopper/Textures/chopper_atlas.png`. UVs did not change, so `chopper.fbx` was not re-exported. W2-04 is still partial. W2-05 has not been run.

Vertex colours in the mesh are linear. A test written against the sRGB palette (`wood` red 0.55) matches nothing; linear wood red is about 0.26.

## How to rerun

From `Chopper/`:

```
blender -b -P Art/Scripts/build_chopper.py
blender -b -P Art/Scripts/build_props.py
blender -b -P Art/Scripts/uv_atlas.py
blender -b -P Art/Scripts/rig_chopper.py
blender -b -P Art/Scripts/animate_chopper.py
blender -b -P Art/Scripts/export_fbx.py
blender -b -P Art/Scripts/check_assets.py
blender -b --factory-startup -P Art/Scripts/check_rig.py
```

`rig_chopper.py` rebuilds `chopper_rigged.blend` from the character file, so it must be followed by `animate_chopper.py` and `export_fbx.py`. `uv_atlas.py` must run before the rig if the character blend's UVs changed.

Unity, one after the other, without `-nographics` on the check (screenshots need a device):

```
Unity.exe -batchmode -nographics -quit -projectPath Chopper -executeMethod Chopper.EditorTools.ChopperImport.ImportAndBuild -logFile Chopper/Art/Renders/unity_import.log
Unity.exe -batchmode -quit -projectPath Chopper -executeMethod Chopper.EditorTools.ChopperCheck.RunChecks -logFile Chopper/Art/Renders/unity_check.log
```

## Do not "fix" these

- The walk slip check expects the planted foot to move backward at the clip speed while the root stays put. A slip of exactly 0.025 m/frame means the take did not evaluate.
- `check_assets.py` fails if an image is packed into a .blend. Keep the atlas as a PNG only.
- Materials in Unity are remapped by name, not extracted from the FBX. Week 1 QA accepted that.
- Axe triangle budget has 14 tris of room (986 / 1,000). Do not add geometry to the axe.

### Voxel pine done

`Art/Scripts/build_pine.py` builds `prop_pine_voxel` from 0.25 m cubes (shared faces omitted). It is a separate file, not part of the character atlas or the four-material character budget. Colour is vertex colour on material `M_Pine`. The check in that script passed: 190 cubes, 768 triangles, height 3.25 m, dirt pad 2.25 m wide, base on Z=0, one mesh, transforms applied, no packed images. Renders: `Art/Renders/pine_front.png`, `pine_three_quarter.png`. Blend: `Art/Blender/prop_pine_voxel.blend`. FBX: `Art/Export/prop_pine_voxel.fbx` (22,732 bytes).

`ChopperImport.ImportAndBuild` now also runs `ImportPine`: copies the FBX to `Assets/Game/Props/prop_pine_voxel.fbx`, makes `M_Pine` with `_UseAtlas` 0 and no `_BaseMap` (so the shader uses vertex colour), saves `Assets/Game/Props/prop_pine_voxel.prefab`, and places one instance in TestScene at (10, 0, 10), behind the character review cameras. Last import log ends with `RESULT: DONE`. `ChopperCheck.RunChecks` after that, with a graphics device: 101 PASS, 0 FAIL. That check does not assert the pine. `AtlasPath` must stay declared in `ChopperImport.cs`; a pine edit dropped it once and the project failed to compile.

## Stop here

Cursor stopped after the pine. The next packages are a character remodel (Elite) and an independent Week 2 review. Both should be a fresh Claude Code session, not a continuation that marks its own review done.

## Still open

- W2-04: the paint now hits the right surfaces, and it is still a procedural stand-in. A tighter shield axe, real knotwork on the axe blades, and a concept side-by-side are open. Do not go back to painting the atlas tile rectangle.
- W2-05: an independent review that reruns the three checks and looks at `Art/Renders/atlas_*.png` against concepts 3, 1.5, and 1.7, plus `pine_front.png` and `pine_three_quarter.png` against concept 4. The reviewer should not trust this file as proof.
- Elite (concept 1.8): steel pauldrons and a plated vest. That changes the character mesh, so UV, rig, animate, and export have to run again, then Unity import and checks.
- Look polish on the task board: beard, box torso, boot rings, axe throat (14 tris of room), shield rim fold.
- The user has not pressed Play in the editor. The batch driver step and the screenshots are the evidence so far.

## User still has to approve

Commit to `rebuild/chopper-assets`. Narrower capsule. Axe blades turned sideways. Deleting the old project at the repo root.
