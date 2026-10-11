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

### Elite done

`chopper_plate` (384 tris, steel shell with two dark bands, two front plates, a leather strap) and `chopper_pauldrons` (240 tris, a cap on each shoulder) are on the same rig. Material `M_Chopper_Gear`. The Elite prefab hides the leather vest and the hair, and shows the plate, pauldrons, helmet, axe, and shield. Worker and Warrior hide the plate and pauldrons. Pauldrons sit on top of the shoulders so the shield crown stays clear (an earlier wrap down the arm failed the shield clip check).

Budgets: Elite (no hair, no leather vest) plus axe and shield 11,456 tris. Height still 1.800 m. `check_assets.py` 95 PASS, 0 FAIL. `check_rig.py` 128 PASS, 0 FAIL. Unity capsule stays 0.37 m: the fit measured 0.363 m once plate and pauldrons were left out of the torso sample. `ChopperCheck` after import: 101 PASS, 0 FAIL, 12 skinned meshes. Prefab: `Assets/Game/Characters/Chopper/Chopper_Elite.prefab`. Renders: `Art/Renders/elite_lineup_front.png`, `elite_lineup_three_quarter.png`.

### Meadow kit done

`Art/Scripts/build_meadow.py` builds a rock (1.00 m, 292 tris), a stump (0.75 m, 76 tris), and a 2 m log (144 tris) from the same 0.25 m cubes as the pine. Vertex colour on `M_World`. Check ALL PASS. Render: `Art/Renders/meadow_kit.png`. Unity prefabs under `Assets/Game/Props/`, placed in TestScene at (6, 0, 8), (8, 0, 8), and (10.5, 0, 8). The live world uses the rock and the pine. Felled trees use the stump. The log is on the campfire.

## Do not restart the character

A generic “concept to Blender to Unity” pipeline was pasted after the character was already built. It matches `PRODUCTION_BRIEF.md`. Do not remodel Chopper from the images, do not generate an FBX from the concepts, and do not rename the modules.

Keep these choices where they already differ from that write-up:

- Two materials, `M_Chopper` and `M_Chopper_Gear`, plus `M_Pine` and `M_World` for props. Not four character materials.
- Axe stays about 0.85 m (concept 1.7), with 14 tris of room. Not 1.1–1.3 m.
- `Shield_L` stays on the forearm. `Shield_Back` stays on the upper chest.
- Capsule radius stays 0.37 m. Do not switch the prefab to a CharacterController.
- No Block or EquipShield clip until combat asks for one.
- Mesh read/write stays on. The Unity checks sample vertices.

### Live world uses the pine and rock

`ChopperMeadowSwap.Apply` retargets `Assets/_Chopper/Config/SO_WorldAssets.asset`. Every tree slot is the voxel pine (natural height 3.25 m) with a `_BaseColor` tint per old species, so water stays blue, fire red and orange, earth dark and yellow, wind light green, and the hub stays the authored green. All three rock slots are `prop_rock_voxel`. The campfire prefab was rebuilt with that rock and `prop_log_voxel`. Shader: `Assets/_Chopper/Shaders/ChopperVertexColorLit.shader`. Meshes: `Assets/_Chopper/Art/Meadow/`. A later Rebuild Network Scenes calls `Retarget` again, so it will not put the pixel trees back.

### Live player uses the Chopper body

`NetworkScenesBuilder.UseChopperPlayer` rebuilt `Assets/_Chopper/Prefabs/Player/Player.prefab` from `Assets/_Chopper/Art/Character/chopper.fbx`. The avatar is humanoid, so the existing combat, fishing, mining, and locomotion clips still play. Scale is 1 (the mesh is already 1.8 m). CharacterController height is 1.8 m, radius stays 0.5 m. Hair is on. Helmet, plate, and pauldrons are off. The outfit catalog is not used. The gameplay axe and torch parent to `RightHand` / `Weapon_R` and `LeftHand`, because this rig has no finger bones. The atlas is `chopper_atlas.png` on `M_Chopper` and `M_Chopper_Gear` with `_UseAtlas` 1.

### Felled trees leave the voxel stump

`TreeManager.CreateStump` instantiates `prop_stump_voxel` when `_stumpPrefab` is set. The mesh is 0.75 m wide and 0.75 m tall with its base on the ground, scaled so its width matches the trunk diameter (0.6 m, 0.9 m, 1.3 m for small, medium, large). A capsule collider of that same size blocks the player. `ChopperMeadowSwap.WireStump` wrote the prefab onto `Assets/_Chopper/Prefabs/World/TreeManager.prefab`. A later Rebuild Network Scenes keeps the reference, because `BuildTreeManagerPrefab` assigns it when the FBX is present. The old cylinder remains only if the prefab is missing.

### Foliage leans when you walk through it

The grass carpet (`GrassCarpet`, shader `Chopper/Grass`) already sways in the wind and bends away from `GrassPushers`. The player push radius is 1.25 m and `_PushStrength` on `M_GrassCarpet` is 1.15, so the part is wider than the old 0.9 m / 0.75 setting. `HerbManager` also places 700 decorative bushes (same model as the fiber ferns, not pickable) and puts every herb, fern, and bush under one `GrassField`. That component leans the meshes away from the same pushers and springs them back. Picked plants are hidden and are skipped while inactive.

## Stop here

The live world grows the voxel pine, places the voxel rock, leaves the voxel stump when a tree falls, and parts grass and bushes around the player. The player wears the Chopper body. The held axe is still the old woodsman axe, not `chopper_axe`. The shield is not on the player. Look polish is optional. W2-05 still needs a different reviewer. Nobody has pressed Play on this foliage pass. The other two GitHub repos named as MVPs (`thurtea/3D-Game`, `thurtea/Chopper`) are private and were not readable. [project-chop](https://github.com/thurtea/project-chop) is this repo. The current agent prompt is `Chopper/Docs/agent-prompt.md`.

## Still open

- W2-04: the paint now hits the right surfaces, and it is still a procedural stand-in. A tighter shield axe, real knotwork on the axe blades, and a concept side-by-side are open. Do not go back to painting the atlas tile rectangle. The user has accepted this look for now.
- W2-05: an independent review that reruns the three checks and looks at `Art/Renders/atlas_*.png` against concepts 3, 1.5, and 1.7, `elite_lineup_*.png` against concept 1.8, and `pine_three_quarter.png` against concept 4. The reviewer should not trust this file as proof.
- Look polish on the task board (optional; the user accepted the current models): beard, box torso, boot rings, axe throat (14 tris of room), shield rim fold.
- The user has not pressed Play in the editor. The batch driver step and the screenshots are the evidence so far.

## User still has to approve

Commit to `rebuild/chopper-assets`. Narrower capsule. Axe blades turned sideways. Deleting the old project at the repo root.
