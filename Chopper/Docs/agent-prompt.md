<prompt>

<goal>
Continue Chopper, a co-op survival game (Valheim leaning, with Minecraft-style building), from the state already in the repo. Make the game match chopper-3D-concept-2.0.jpg as closely as possible: world, house, character and objects. Work the open items below in order, collect proof for each result, and return only when that work is complete, blocked, or needs approval. Do not restart the character or rebuild the game from the concept images.
</goal>

<project_brief>
Project: Chopper, live Unity game (repo root) plus the Chopper art project (Chopper/).
Audience: a solo developer. Art must read clearly at third-person gameplay distance.
Look: concept 2.0 is the target for the whole world and the characters (Minecraft shapes, Valheim-style light and motion). Forests only change colour.

Already done, do not redo (details and proof in Chopper/Docs/cursor-claude-handoff.md and TASKBOARD.md):
- Character pipeline in Blender (Chopper/Art/Scripts): build_chopper, uv_atlas, rig_chopper, animate_chopper, export_fbx, check_assets (136 PASS), check_rig (145 PASS). Atlas paint colour bug fixed, grain halved, hair and beard strands softened, gutter filled (no dark mip edges), compact spade beard, patchwork vest.
- Axe swings: Chopper's own Valheim-style one-handed chops Attack_1 (diagonal from the right shoulder), Attack_2 (diagonal from the left), Attack_3 (overhead finisher), wind-ups found by a search over the rig. The live combo and the tree chop use them (CharacterSetup.ChopperTake). Proof: Chopper/Art/Renders/attacks_sheet.png; Logs/chopper_play/chop_probe.log (each swing plays its clip, hand rises 0.5 m over the shoulder). Walking is fine (user).
- Live player: new chopper.fbx and atlas, bear-head axe carried edge down, atlas anisotropic 4. Starts unarmoured; an armour kit from each felled boss tree puts on helmet, chainmail, bracers, shield (PlayerUpgrades, PlayerOutfit; armour.log PASS). Double jump (double_jump.log PASS). stepOffset 0.55.
- Hub house (Hut.DressCabin): wide, low log house 18 m x 12 m, basement, main floor, loft in the roof, 2 beds, 2 workbenches, door and steps on the long front side (house5.log PASS). Stairs use tread colliders; the doorway sill is gone.
- Voxel props in the live world: pine, rock, stump, log, grass tufts, bushes, torch (yard and piece), sign (tutorial and notice board), chest, grand chest (crypts), bed, workbench (yard and piece), fireplace. Dress tool: Chopper.Editor.ChopperPropDress.Apply (rerun it after any MeadowBuilder or PieceBuilder rebuild).
- Block ground draws (shader fix), with distance-faded detail; website pictures were retaken once but must be retaken again last.

Still open, in this order:
1. Texture flicker on the character and the ground: confirm in Play whether it is gone after the gutter fill, softer paint, anisotropic 4 and the block shader fade. Use a moving camera capture, not stills. If it remains, find the cause (shadow acne on the blocky body, mip selection, TAA/MSAA) before changing more.
2. Character to concept 2.0: skull helmet with horns (the current one is a plain cap with stubs), chainmail texture (reads flat blue-grey), chunkier bear axe, broader chest. Keep the rig and the module names.
3. House details from concept 2.0: the small porch roof over the door and the raised grass and stone terrace with steps. Basement walls are tinted log pieces today.
4. Water, block clouds and the block sun to concept 2.0. Healing herbs are still the old red flowers; the campfire piece and the held torch are still old models.
5. W5-08 step 5: chunk colliders, WorldGround reads the block columns, Terrain collider removed, then snap trees onto block tops.
6. Website pictures last, after everything above (-chopperSite tour on a development build; launch players with PowerShell Start-Process, not Git Bash).
7. Art-project Unity checks (ChopperCheck expects 12 skinned meshes; the FBX has 13) not rerun. W2-05 needs a reviewer who is not the builder.

Tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. Git. No gh CLI.
Checks that exist: Play tours on a development build (Builds/Site, BuildTools.BuildDevPlayer): -chopperHouse, -chopperArmour, -chopperDoubleJump, -chopperAnimProbe, -chopperGearLook, -chopperGroundProbe, -chopperTutorialTour, -chopperSite. Blender: render_attacks.py, render_base_look.py, build_furniture.py checks.
Constraints: Do not generate final meshes with AI image-to-3D. Do not remodel Chopper from scratch. Do not replace the live Animator controller (swap motions only). Mesh read/write stays on. Do not run PieceBuilder.BuildAll. Work on main; no other branch. No commits, pushes, deletions or paid services without explicit approval; stage the work and give the user a commit message. Unity batch on one project at a time. Trust "Exiting batchmode successfully" and RESULT lines, not exit codes. The concept JPGs other than 2.0 were deleted from the repo root by the user; leave that unstaged.
</project_brief>

<working_rules>
- Read Chopper/Docs/cursor-claude-handoff.md and TASKBOARD.md first. Treat "Already done" as closed.
- No proof, no done: a Play tour log line, a check report, or a picture you looked at.
- After every change, update cursor-claude-handoff.md, TASKBOARD.md, this file and PRODUCTION_BRIEF.md where they are affected.
- Ask one focused question only when an answer changes what gets built.
- End with: what changed, the proof, what is still open, the staged files, and a commit message.
</working_rules>

<start>
Read the handoff and the task board, then start with open item 1 (texture flicker, verified in motion).
</start>

</prompt>
