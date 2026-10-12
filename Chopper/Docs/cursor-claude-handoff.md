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

1. Double jump, asked for on 2026-10-12. Not started. Add it on the live player motor. Do not rewrite the rest of movement.
2. W5-08 step 5: `WorldGround` reads the columns, chunk colliders, drop the Terrain collider, `stepOffset` 0.55, slide only when pushing into a riser taller than 1 m. Design: `Logs/chopper_play/world_audit/W5-08_voxel_terrain_design.md`. Then snap tree Y onto the block top (step 7), or the buried pines will float off the new surface.
3. Workbench, torch, and campfire piece meshes. A voxel torch already exists at `Chopper/Art/Export/prop_torch_voxel.fbx`. The standing torches in the yard are still the old posts.
4. Clouds and the block sun. Healing herbs are still the old red flowers; there is no voxel herb in the world kit.
5. The bear axe on the live player. Do not restart W6-03 from scratch. Do not mark W2-05 done.
6. Website pictures of the hut, fishing, and the world. The page still has the old JPGs. A release player was built at `Builds/Site/Chopper.exe` and a `-chopperSite` run was started, then stopped before any shot was written. The hut and night cameras in that tour now aim at the new cabin. Do not publish.

## Cabin and foliage (2026-10-12)

Play tour `-chopperCabin`: `Logs/chopper_play/world_audit/cabin_play3.log` `[Cabin] RESULT PASS`. Shots in `Logs/chopper_play/world_audit/cabin3/` (`cabin_front.png`, `hub_front.png`). Hub mesh bounds 2.30 × 4.22 × 4.30 m. The longhouse is gone. Bed, chest, fireplace, and repair stay. The carry cap is 40 wood, so the tour refills before the gable and the roofs.

`ChopperFoliageSwap.Apply` (`foliage_swap.log`, RESULT PASS) copies the world-kit tufts and bushes in. The grass carpet instances `prop_grass_tuft_voxel_a_carpet` up close and `prop_grass_tuft_voxel_c_carpet` farther out. Ferns and the decorative bushes are `prop_bush_voxel_a` and `prop_bush_voxel_b`. Foliage walk: `foliage_walk3.log` `[Foliage] RESULT PASS` (lean 27.3 deg mid-walk, 0 after). Shot `foliage3/foliage_b_mid_walk.png`.

The build menu has no new silhouettes: the game-icons SVG pack is not in the repo. Window uses `piece-doorway`, gable uses `piece-roof`.

## Do not

- Do not remodel Chopper or replace the live Animator controller.
- Do not turn chunk shadow casting back on without a bias fix.
- Do not use a paid voxel plugin without an explicit yes.
