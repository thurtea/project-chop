# Chopper handoff (2026-10-12, cabin, foliage, stop)

The live game and the art project are both on `main`. Do not open another branch. Do not commit or push unless asked. Do not remodel the character from the concept images. Do not delete the Unity project at the repo root. Do not generate final meshes with AI image-to-3D. Hand-author them in Blender.

Unity: `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe`. Live project: `E:\Projects\project-chop`. Art project: `E:\Projects\project-chop\Chopper`. Blender: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`. One Unity project at a time. Import may use `-nographics`. Screenshot checks must not. Mesh read/write stays on. Art-project capsule radius stays 0.37 m.

A batchmode Unity often stays alive after the method has already logged success, stuck in an asset-import worker. If the log already has the RESULT line and a new batch says another instance is running, stop that batchmode `Unity.exe` (the one whose command line is `-executeMethod`). Do not stop an editor the user opened themselves.

## What the picture requires

`chopper-3D-concept-2.0.jpg` is the look for the world, the character, and the objects. The home in that picture is a small log cabin: horizontal logs, a steep thatch roof, crossed beams at the gable peak, the door on the gable end, one small window. It is not the 12 × 9 m concept-1 longhouse.

The player must be able to build that cabin. The old pixel hammer pieces are not the goal.

## Hammer pieces (done this pass)

`Chopper/Art/Scripts/build_pieces.py` builds the meshes. Check: `Chopper/Art/Renders/piece_check_report.txt`, SUMMARY ALL PASS. Exports: `Chopper/Art/Export/piece_*_voxel.fbx`.

`Chopper.Editor.ChopperPieceDress.Apply` copied them into `Assets/_Chopper/Art/Building/`, hid the old renderers, and parented each mesh under a child named `Voxel`. Colliders were left as they were. Log: `Logs/chopper_play/world_audit/piece_dress2.log`.

```
[ChopperPieces] Piece_Wall height 2.00 base 0.00
[ChopperPieces] Piece_Roof rises toward +Z True
[ChopperPieces] RESULT PASS
```

The process was stopped after that line. The log does not contain `Exiting batchmode successfully`.

| Piece | What it is | Catalog |
|---|---|---|
| Floor, wall, doorway, roof, stairs | Old prefabs, new meshes. Floor is 2×2 planks. Wall, doorway, and window are horizontal logs, 2 m × 2 m, 0.25 m thick. Door opening is 1 m × 1.75 m. Roof is thatch on the old 45° slope (low at local −Z, high at +Z). Stairs are log treads on that same slope. | Indexes unchanged |
| Window | New. Same snap points as a wall. | Appended |
| Gable | New. Log triangle with the pale crossed beams. Snap points are only the bottom corners, so it sits on a wall top. | Appended last |

Do not run `PieceBuilder.BuildAll`. It rebuilds the pieces from the pixel pack and can drop Window and Gable, because those two are not in its spec list. New pieces go on the end of `SO_PieceCatalog` so saved indexes stay valid.

Workbench, standing torch, and campfire pieces are still the old models.

Play has placed them (`cabin_play3.log`, RESULT PASS). The menu icons for window and gable reuse `piece-doorway` and `piece-roof`; the SVG pack is not in the repo.

## Hub hut (not the concept cabin)

The hub is the concept 2.0 cabin built from the hammer pieces (door and gable on +Z, 2 m × 4 m). `Hut.DressCabin` hides the old pixel house. Bed, chest, and fireplace are moved inside. Repair is unchanged.

## Ground and trees

Pines are sunk `TreeManager.PineBuryMeters` (0.40 m times scale) so the dirt pad is buried. Stumps stay on the surface.

Block columns, the palette, and the chunk mesh are in. On world build the terrain paint hides and `Chopper/Block` draws the steps. The Terrain collider is still what the player walks on. `WorldGround` still reads that Terrain. Batch check: `Logs/chopper_play/world_audit/W5-08_step2.log`, RESULT PASS, CRC `D467EE29`, 382,074 triangles. Chunks do not cast shadows (flat tops went black when they did).

## Still open, in this order

1. Animations read "weird and flaily" (user, 2026-10-12). Not fixed. The live player plays the Quaternius and Mixamo clips through the auto-built humanoid avatar. `-chopperAnimProbe` (`Logs/chopper_play/anim_base.log`): idle arms out 6-8 deg; walking the upper arms reach 81 deg (left) and 118 deg (right, the axe arm) out to the side and swing -92..46 deg. A hand-built T-pose avatar (as the art project does) made it worse (arms straight up, `anim_after.log`) and was reverted, with `chopper.fbx.meta` restored. Frame strips `Logs/chopper_play/anim_strip/` look like a normal stride at that size; which motion looks flaily (walk, run, chop, idle) still needs pinning down with the user.
2. Character polish toward concept 2.0: chunkier bear axe, chainmail reads flat blue-grey, the helmet is a plain steel cap with stub horns (concept 2.0 has the skull helmet; W2-05 noted it too). Torso is still a straight box.
3. The house: porch cross-gable over the door and the stone/grass terrace of concept 2.0 are not built; basement walls are log pieces tinted grey. `MeadowBuilder`/`PieceBuilder` rebuilds would undo the voxel dresses; rerun `ChopperPropDress.Apply` after them.
4. W5-08 step 5: chunk colliders, `WorldGround` reads the columns, drop the Terrain collider, then snap tree Y onto the block top (step 7). The player still walks on the smooth Terrain collider under the stepped blocks. `stepOffset` is already 0.55.
5. Clouds and the block sun. Healing herbs are still the old red flowers. The campfire piece is not dressed. The held torch is the old model.
6. Water to match concept 2.0 (user, 2026-10-12).
7. Website pictures last, once the above is in (user, 2026-10-12). The night picture is weak.
8. Art-project Unity checks (`ChopperCheck`, 12 skinned meshes expected, the FBX has 13) were not rerun. Do not mark W2-05 done.

## Armour, house, furniture, stairs, flicker, axe edge (2026-10-12, latest)

Decisions (user): everyone starts unarmoured (no helmet, chainmail, bracers or shield; axe and tools unchanged). Each felled boss tree gives every player an armour kit; using it from the bag puts on the armoured look for good. The house is a wide, low log house for two with an upstairs and a basement, two workbenches and two beds, matched to concept 2.0.

- Armour: `PlayerUpgrades` keeps `ArmourKits` and `Armoured` (synced, saved in `PlayerRecord`, 10% less damage). `EnemyDirector.ServerOnBossKilled` grants a kit. Bag entry "Armour kit" with "Put on". `PlayerOutfit` shows helmet, chainmail, bracers and the shield and hides the hair. Tour `-chopperArmour`: `Logs/chopper_play/armour.log` RESULT PASS (starts unarmoured, kit after the boss, worn after use, reduction 0.00 -> 0.10, a second kit refused). Shots `armour/`.
- House: `Hut.DressCabin`, 12 x 8 piece-metres scaled 1.5 = 18 m x 12 m. Basement (stone-tinted log walls), main floor, loft in the roof, ridge along X, gables with crossed beams at both ends, door and log steps on the long +Z side. Tour `-chopperHouse`: `Logs/chopper_play/house5.log` RESULT PASS (127 pieces, 2 beds and 2 workbenches inside; outside steps to 3.38 m, loft stairs to 6.38 m, across the loft at 6.66 m, basement stairs to 3.38 m). Shots `house5/`. `WorldGen.HutHalfWidth/Depth` 9.5/6.5; yard campfires moved behind the house (165 and 195 deg, 10 m).
- Furniture: `Chopper/Art/Scripts/build_furniture.py` (chest, grand chest, bed, workbench, fireplace; `furniture_check_report.txt` ALL PASS, `furniture_lineup.png`). `ChopperPropDress.Apply` (`world_audit/prop_dress4.log` RESULT PASS) dresses the house bed, chest and fireplace (fire and light kept), the yard workbench and the workbench piece, and points `SO_WorldAssets` crypt chests at the voxel chests.
- Stairs and steps: the doorway's 0.25 m sill collider was removed (it stopped the player at the door). The stairs piece's 45 deg ramp collider is off; eight 0.25 m tread boxes instead. Player `stepOffset` 0.55 (`NetworkScenesBuilder.PlayerStepOffset`).
- Flicker: `Chopper/Block` fades its 6 cm jitter and grid lines by pixel footprint (`fwidth`) and hashes jitter in 3D (no streaks on cliffs). The character atlas gutter is filled by push-pull (`uv_atlas.fill_gutter`; near-black texels 0.01%), so mip levels no longer pull dark gutter onto the edges. Not measured in Play beyond the tour pictures.
- Axe edge: `CharacterSetup.AttachChopperGear` set the edge to the positive blade axis; the bear axe's blade is on the negative side, so the carry hung the edge up. Now the side the blade reaches farther. `Logs/chopper_play/axe_gear.log`, 0 errors; `axe_gear/gear_close_right.png` edge toward the ground.

## Character pass and double jump (2026-10-12, later)

Double jump: `PlayerMotor` gives one air jump (same height and stamina as the first; back on landing). Tour `-chopperDoubleJump`, `Logs/chopper_play/double_jump.log` RESULT PASS: single peak 1.17 m, double 2.27 m (fall damage starts at 4 m), a third press in the air does nothing.

The live player had never received the W6-03 work: it still used the 2026-10-11 18:13 `chopper.fbx` and the per-face atlas, which is where the dark grid lines came from. Also, every colour test in `atlas_paint.py` failed: the BYTE_COLOR vertex colours read back as linear (trousers 0.074, not 0.30), so legs painted as skin and buckles, crossbelt, soles, moustache and axe parts fell through. `near()` now converts the PAL value first. Vest patches are bigger with sparse stitches; boot cords are calmer; the beard is a compact spade ending at the collarbone (`build_chopper.build_beard`). Pipeline rerun: build_chopper, uv_atlas, rig_chopper, animate_chopper, export_fbx; `check_assets` 136 PASS 0 FAIL, `check_rig` 140 PASS 0 FAIL. Renders `base_look_*.png`. Live: `chopper.fbx` and `chopper_atlas.png` copied to `Assets/_Chopper/Art/Character/`, `ChopperAxePath` is now `chopper_axe_bear.fbx`, `UseChopperPlayer` (`Logs/chopper_play/w6_04_builder.log`, `Exiting batchmode successfully`). Gear tour `w6_04_gear.log`: bear axe on Weapon_R (0.0 cm, 0.0 deg), shield 2.0 cm off the back, 0 errors. The other concept JPGs at the repo root were deleted outside this work (only 2.0 remains); `render_base_look.py`'s side-by-side sheet needs `chopper-3D-concept-1.5.jpg` and fails without it.

## Block ground, voxel torches and signs, website pictures (2026-10-12, later)

The block ground never drew in a player: `Chopper/Block` named a variable `line`, a reserved HLSL word, so the shader failed and URP drew nothing. The "black ground" in every earlier picture was the water and sea-floor planes under the island. Proof: dev tour `-chopperGroundProbe` (new in `ShotCapture.cs`) logged the chunk under the player with the grass colour and `Chopper/Block`; hiding `VoxelWorld` did not change the picture (`Logs/chopper_play/world_audit/ground_probe/`). After the rename, `VoxelChecks.CheckBlockShader` gives `SHADER PASS` (4 passes, no error; `block_shader_fixed.log`), and the probe shows green blocks (`ground_fix/probe_0_all.png`). DepthOnly and DepthNormals passes were added for SSAO and the depth texture. The "old shader" log `block_shader_old.log` is not evidence: the restored file was empty.

`ChopperPropDress.Apply` (`prop_dress.log`, `RESULT PASS`, `Exiting batchmode successfully`): six yard torches in `Hut.prefab` and `Piece_Torch.prefab` show `prop_torch_voxel` (2.25 m; flame and light unchanged), the hub notice board in `World.unity` shows `prop_sign_voxel` at 1.3x, and `Resources/Props/prop_sign_voxel.prefab` is what `TutorialYard` now builds its signs from (old primitives as fallback). Old renderers are hidden, not deleted. `MeadowBuilder` and `PieceBuilder` rebuilds would bring the old looks back; rerun `ChopperPropDress.Apply` after them. The sign mesh is `Chopper/Art/Scripts/build_sign.py` (uses the world kit palette; `sign_check_report.txt` ALL PASS, 396 tris, 1.625 m). Tutorial tour: `world_audit/tutorial_signs.log` `[Tutorial] RESULT PASS`, 8 signs, the welcome sign reads.

The site tour needs a development player: `Builds/Site` was a release build, so `-chopperSite` loaded and sat there. Rebuilt with `BuildDevPlayer -buildOutput Builds/Site`. Launch it from PowerShell `Start-Process`; from Git Bash it quit during world load. The hut and night cameras stood on the yard torch ring (14 m) and were moved in. Website pictures: `site_tour7.log`, 10 shots, `Logs/chopper_play/site_shots7/`, cropped to 2480 x 1395 (drops the Development Build mark) and saved at 1600 x 900 under the old names. `creator.jpg` is a crop of the character shot. Not published.

## Cabin and foliage (2026-10-12)

Play tour `-chopperCabin`: `Logs/chopper_play/world_audit/cabin_play3.log` `[Cabin] RESULT PASS`. Shots in `Logs/chopper_play/world_audit/cabin3/` (`cabin_front.png`, `hub_front.png`). Hub mesh bounds 2.30 × 4.22 × 4.30 m. The longhouse is gone. Bed, chest, fireplace, and repair stay. The carry cap is 40 wood, so the tour refills before the gable and the roofs.

`ChopperFoliageSwap.Apply` (`foliage_swap.log`, RESULT PASS) copies the world-kit tufts and bushes in. The grass carpet instances `prop_grass_tuft_voxel_a_carpet` up close and `prop_grass_tuft_voxel_c_carpet` farther out. Ferns and the decorative bushes are `prop_bush_voxel_a` and `prop_bush_voxel_b`. Foliage walk: `foliage_walk3.log` `[Foliage] RESULT PASS` (lean 27.3 deg mid-walk, 0 after). Shot `foliage3/foliage_b_mid_walk.png`.

The build menu has no new silhouettes: the game-icons SVG pack is not in the repo. Window uses `piece-doorway`, gable uses `piece-roof`.

## Do not

- Do not remodel Chopper or replace the live Animator controller.
- Do not turn chunk shadow casting back on without a bias fix.
- Do not use a paid voxel plugin without an explicit yes.
