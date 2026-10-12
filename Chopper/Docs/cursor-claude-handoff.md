# Chopper handoff (2026-10-11, after the rate limit)

The live game and the art project are both on `main`. There is no `rebuild/chopper-assets` branch. Do not open another branch. Do not commit or push unless asked. Do not remodel the character from the concept images. Do not delete the Unity project at the repo root.

Unity: `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe`. Live project: `E:\Projects\project-chop`. Art project: `E:\Projects\project-chop\Chopper`. Blender: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`. One Unity project at a time. Import may use `-nographics`. Screenshot checks must not. Trust `Exiting batchmode successfully` in the log. Mesh read/write stays on. Art-project capsule radius stays 0.37 m.

## What the game is

Co-op survival, Valheim leaning, with Minecraft shapes. Concept art wins over the current cabin, the smooth mountains, and old saves. Gameplay systems stay (gather, build, fight, fish, network). Their look and the terrain shape change.

Visual target: block shapes, then crisp pixel textures (nearest filter, one texel size), Valheim lighting (fog, warm sun, cool shadows, bloom, grading), and motion (wind, fire flicker). That look pass is Week 7 and has not started.

## Already in the live game

- Player body is `chopper.fbx`, humanoid, scale 1, controller height 1.8 m, radius 0.5 m. Hair on. Helmet, plate, and pauldrons off. Existing combat, fishing, and mining clips stay on that avatar. The held axe is still the old woodsman axe. `chopper_axe_bear.fbx` exists and is not on the player yet.
- Trees and rocks are the voxel pine and rock. Felled trees leave `prop_stump_voxel`, scaled to the trunk. Campfire uses the voxel rock and log.
- Props stand upright. The prefab root is a plain object and the FBX (which imports at +90 X) is a child. `CheckOrientation` after that fix: `RESULT PASS (0 props not upright)`.
- Lighting pass is in: warmer sun, cooler ambient, thicker hub fog, bloom 0.30, saturation -12, SSAO radius 0.6. Hub frame time went from 5.6 ms to 5.1 ms at 2560x1440.
- Grass carpet bends away from the player (push radius 1.25 m, `_PushStrength` 1.15). Herbs, ferns, and 700 decorative bushes lean through `GrassField`.

## What Claude finished before the limit

- World audit: `Logs/chopper_play/world_audit/W5-01_audit.md`, 14 shots, 0 errors. Every voxel prop was lying on its side. That orientation bug is fixed (above).
- Bear-head axe and the unarmoured base look (bracers split out of the belt). `check_assets` 115 PASS, 0 FAIL. `check_rig` 137 PASS, 0 FAIL. Renders: `Chopper/Art/Renders/base_look_*.png`, `axe_bear_*.png`. Unity's `ChopperCheck` still expects 12 skinned meshes and the FBX now has 13, so that check was not rerun.
- World kit, Blender, `check` SUMMARY ALL PASS. Exports in `Chopper/Art/Export/`: longhouse, torch, grass tufts (and carpet variants), bushes, clouds, sun, path stone, mountain chunks, backdrop mountain, `terrain_blocks_voxel.fbx`, `terrain_palette.txt`. These files are not in the live scene yet.
- Voxel terrain design, approved, half-block steps (0.5 m) so the player can walk them: `Logs/chopper_play/world_audit/W5-08_voxel_terrain_design.md`.
- `WorldGround` facade started. Callers were only partly moved, and two of the edits did not compile (`BranchManager` and `SmokeTest` still named a `terrain` variable that no longer existed).

## What was finished after the limit

Step 1: height, normal, steepness, the minimap, the grass carpet, building placement, the motor, and the dev tours read `WorldGround`, which still reads the Unity Terrain collider. Compile log: `Logs/chopper_play/world_audit/W5-08_step1_compile.log`.

Steps 2 to 4: `VoxelColumns` (640 x 640 columns, 0.5 m steps), `BlockPalette` from `Chopper/Art/Export/terrain_palette.txt`, and `VoxelWorld` chunk meshes with shader `Chopper/Block`. On world build the terrain **paint** is hidden and the **collider stays**, so walking still uses the smooth ground. Batch check `Chopper.Editor.VoxelChecks.RunColumns`: CRC `D467EE29` matched on three runs, first build 89 ms, 382,074 triangles, palette file loaded. Log: `Logs/chopper_play/world_audit/W5-08_step2.log`, `RESULT PASS`, then `Exiting batchmode successfully`. Nobody has pressed Play to look at it.

## Do not do next

- Do not remove the Terrain collider yet. Step 5 is the switch, after a Play look at the blocks.
- Do not start Week 7 (pixel textures, extra fog, wind on pines) before the Terrain collider is replaced.
- Do not mark W2-05 done. A different reviewer still has to look at the renders.
- The character paint (W6-03: V-spade beard, chunkier bear axe, painted atlas) was in progress and is not finished. Do not restart it from scratch. Look at `Chopper/Art/Scripts/` and the latest renders before editing.

## Website pictures

`Website/images/*.jpg` were replaced from a `-chopperSite` tour of a development player (1600 by 900). The shots show the Chopper body, the voxel pines, and the block ground. The block shader needed a small fill light or flat tops sat in black shadow; chunks do not cast shadows. The character creator is gone from the page. The logo and favicons were left as they are. The page was not published.

## Next safe step

Pines are planted 0.40 m (times tree scale) below the ground so the dirt pad is buried and the trunk comes out of the grass (`TreeManager.PineBuryMeters`). The hub cabin's walls and roof are hidden and `prop_longhouse_voxel` is parented on the hut (door toward +Z, footprint 12 x 9 m). Bed, chest, and fireplace stay. The rest of the world kit is not in the scene.

W5-08 step 5: point `WorldGround` at the columns, add chunk colliders, hide the Terrain, and raise the player step offset to 0.55. The collider is still the old terrain, so feet can sit slightly off a step. Then NavMesh.

## Still open, not this step

- Live player still holds the woodsman axe. Shield is not equipped.
- W5-06 longhouse is in. Torches, grass tufts, clouds, and the block sun are still the old props.
- W6-04 puts the base look and the bear axe on the live player after W6-03.
- Look polish and W2-05 stay optional and separate.
- Paid voxel plugins need an explicit yes. The design says not to use them.
