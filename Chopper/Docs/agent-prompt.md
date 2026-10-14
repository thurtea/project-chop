<prompt>

<goal>
Get the crypt and mine shell cover into Builds/Dev/Chopper.exe and prove it with shots (W11-52). That is the only active package. After that cover is in the exe and the shots pass, two later packages wait, in this order, and are not active work: compare the copied live site with the repo site and rewrite whichever is behind; then, only when asked, redo the enemies, the character and the objects in the scripted-Blender method and redo fishing. Use the smallest useful team, collect proof for each result, and return when a package is in that exe, when you are blocked, or when you need a rating or an approval. A change counts only when it is in that exe and you have opened an in-game shot or a tour RESULT that shows it.
</goal>

<project_brief>
Project: Chopper, a co-op survival game (one to four players). Live Unity 6000.5.10f1 project at E:\Projects\project-chop (FishNet, URP). Art is made by script in Blender: Chopper/Art/Scripts, concepts in Chopper/Art/Concepts.
Desired outcome:
1. W11-52, this package. The cave kit is already built. A later code pass covers crypt walls with carved panels, trims and arches, floors with stone tiles, and mine walls with timber, and turns the noisy texel hash off on crypt and mine stone only (M_CaveStone, _Jitter 0). Put that cover in a fresh dev build with the Unity editor closed, and prove it. The human rejected the live look: grey wool-like floor, walls and pillars; too many flat walls; texture flickering. Their screenshot is the sticker build build-1014-1650, which does not contain this cover.
2. Later, not this run. Website compare, not a deploy. The live VPS pages are copied at E:\Projects\project-chop\chopper-vps-copy (favicon_io-chopper, images, Chopper-Banner.jpg, Chopper-Character-Cover.jpg, Chopper-Logo.jpg, demo.html, demo.php, gallery.html, index.html, main.css, main.js, README.md). Compare them with the repo site in Website/ (W11-47). Rewrite whichever is behind. Gallery needs new photos from the new caves, so it waits until the cave tour shots exist. Do not deploy, upload or buy.
3. Later still, not this run, and not until they explicitly start it. After the enemies, the character and the objects are redone in the scripted-Blender method (the cave-kit method: hand-built voxel props, not an AI mesh, not motion capture), redo fishing: more detailed fish, fish that jump in and out of the water, a camera look under the water so you can see the size and kind of fish, and activity meters drawn as arcs instead of horizontal lines. Do not start fishing in the current package. Do not start that art redo in this package either.
Deadline: none. One package at a time.
Audience: one developer playing the Windows dev build, sometimes with friends.
Done for W11-52 means: a fresh dev build with the editor closed (homework.ps1 build: Build Finished, Result: Success, error CS lines: 0); EditMode tests have 0 failed; Crypt, Mine and VoxelChecks.EntranceReach ran; shots were opened and are not the grey checker; the walking line is clear; a TASKBOARD RESULT line names the check counts, log names, shots opened and what is still off; playtest-windows.md section D rows are listed for the human. Feel is judged only by the human. W11-51 already stopped for their 1-5 ratings; do not treat missing ratings as a block on the cave rebuild.
Available tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. .NET 10 SDK. Git (no gh CLI). Checks: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 <task> (build, player, editmode, blender, unity <Class.Method>, tour <Name>); everything at once: Tools\playtest.ps1 (-Quick, -ToursOnly) or the menu in Tools\ChopperTests.exe.
Sources and assets: Chopper/Docs/TASKBOARD.md (the Week 11 table is the truth; read W11-11, W11-40, W11-46 to W11-52), Chopper/Docs/Reportcard.md, Chopper/Docs/playtest-windows.md, Chopper/Docs/unity-assets.md. Shell cover: Chopper/Art/Scripts/build_cave_kit.py, cave_kit_manifest.json, Chopper/Art/Export/prop_*_voxel.fbx; Assets/_Chopper/Scripts/World/Generation/CryptPlan.cs (PlaceDressing, CoverSpan, DressArena), MinePlan.cs, CaveDress.cs; WorldBuilder.cs sets M_CaveStone _Jitter 0; Editor/ChopperCaveDress.cs copies the kit into prefabs. Motion, already stopped: Player/PlayerMotor.cs, PlayerAnimator.cs, PlayerAxe.cs, PlayerCombat.cs, PlayerCamera.cs, HitStop.cs; Core/GameConfig.cs and Assets/_Chopper/Config/SO_GameConfig.asset; Enemies/Treant.cs; Editor/ChopperRigEnemy.cs, CharacterSetup.cs. Art: animate_chopper.py, render_moves.py, render_attacks.py, build_elemental.py, render_enemies.py.
Constraints: no AI image-to-3D; geometry and takes by script. No motion capture on the block body (it reads as ragdoll; the human's call, 2026-10-12): weight comes from timing, commitment and feedback. Keep the smooth Chopper mesh; do not run build_chopper_voxel.py or return to per-cube colour until they explicitly start the art redo. Do not rename the Humanoid bones, module names or the Animator controller's states, and do not move the axe grip frame. Do not run PieceBuilder.BuildAll or the full MeadowBuilder pass. One Unity batch job at a time, with the editor closed. Do not retune W11-51. Do not add // or # comments when editing code; rationale goes on the TASKBOARD. Edit agent-prompt.md only when asked. Do not touch Assets/_Chopper/Resources/Audio. No commits, pushes, deletions, deploys, uploads or paid services; work on main; the human commits.
</project_brief>

<where_we_are>
Reportcard.md has no FAIL row. Current rows are blank, so they stay the human's. The Automatic checks block is the 2026-10-14 02:08 run and is not this afternoon's proof.

W11-51 round 1 is in the dev build and stopped for the human's 1-5 ratings. Do not redo it unless they flag a row. Feel is not verified.
Live movement in SO_GameConfig: move 6, sprint 9.5, crouch 2.6 m/s; acceleration 16, deceleration 22, turn 480 deg/s, jump 1.05 m, gravity 26; landing recovery from 3 m for 0.2 s at 0.35 speed; swing commit 0.85, swing move 0.45, swing turn 0.35, strike step 2.5; hit-stop 0.07 / heavy 0.09; hit kick 0.05 / heavy 0.12. Roll is still 9 m/s for 0.5 s. Do not retune these.
Takes wired: Draugr_Walk, Draugr_Attack, Elemental_Walk, Elemental_Slam. Chopper Walk, Run, Sprint, Attack_1, Attack_2, Attack_3, HitReact and LandHard were re-keyed. check_rig 242 PASS, 0 FAIL. Enemy run is still Chopper's Run. Run and crouch-walk feet still slide (0.58 and 0.46 m/s). HitReact is easy to miss from the front. A chop frame hides the axe in the tree.
Proof already collected, all present under Logs/homework: editmode-1014-1627 passed 308 failed 0; build-1014-1625 Success, 0 CS errors (player data rewritten; Chopper.exe stub timestamp stayed 2026-10-13); tour-MoveTest-1014-1628 teleports 0 freezes 0 biggest step 0.10 m; tour-Combo-1014-1629 PASS 6 of 6; tour-Combat-1014-1629 PASS; tour-GuardTest-1014-1630 PASS no spin. tour-Motion-1014-1630 and tour-GearLook-1014-1633 finished with shots opened (axe head up, blade forward, feet planted) and no RESULT line. Build and editmode names are .log files; the tour names are folders.
Playtest section A is already listed for them: starting and stopping a run; turning on the spot and while running; jump height, fall speed, and the landing; the axe combo; guard, parry, and roll against a tree enemy; getting hit. Do not wait for those ratings before the cave rebuild.

W11-52 is not done. The kit exists. The shell cover is written in CryptPlan, MinePlan, CaveDress and WorldBuilder, and is not in the exe. build-1014-1650 is the sticker build the human rejected (editmode-1014-1651, 309 passed, 0 failed; tour-Crypt-1014-1651 shots only, no RESULT). The Unity editor had the project open, so the cover was not rebuilt. Mine and VoxelChecks.EntranceReach were not rerun. No shot of the covered rooms exists.
Still quiet after the cover lands: the ceiling, and the band of wall above the 3.5 m panels (room height is 6 m). Arena pillars inside the 9 m spawn stay block columns (CryptPlan.BuildArena). Known kit limits: blocky cart wheels, thin cobweb, studded pillar shaft, no grate, sump or dais.

Done, do not redo:
- Character (W11-24..36, -40, -45): the smooth body matches the model sheet; 1.80 m; his own blocky takes plus KayKit strikes.
- W11-46 enemies on Chopper's rig: draugr = Chopper's mesh in chopper_atlas_draugr.png; tree elemental = build_elemental.py (Bark plus Leaves0..4 per Element).
- W11-47 website banner and gallery, Steam crops (not deployed, nothing uploaded). The site compare is a later package.
- W11-48 building: pieces may float; floors, walls, roofs, stairs, torches, campfires and the workbench need no workbench; doorway, window and gable do. The hub stays protected.
- W11-49 sizes: Core/CharacterSizes.cs (player 1.8; elementals 1.7/2.3/3.2; warden 5.5; draugr 1.75/1.85/2.0; crypt boss 3.2).
- W11-50 Tools\ChopperTests.exe and playtest-windows.md.

Out of scope until asked: W11-42 (new weapons, boats, bought packs, a sound library), W11-53 enemy look round 2, deploying the website, Steam uploads, new Steam art, the art redo, fishing.
</where_we_are>

<manager_contract>
You are the one manager the human talks to. The shared record is Chopper/Docs/TASKBOARD.md: rows W11-51 and W11-52 exist. W11-51 round 1 stays stopped. Add a log line after each handoff, and never reopen a closed row. Ask one focused question only if the answer changes the look target or blocks the work.
</manager_contract>

<team_design>
Two workers at most, only while W11-52 proof is active.
- Builder: Unity only, one batch job at a time, editor closed. Rebuilds the dev player so the shell cover is in Builds/Dev/Chopper.exe, then runs EditMode, the Crypt tour, the Mine tour and VoxelChecks.EntranceReach. Does not retune movement or combat.
- Reviewer: edits nothing. Opens the Crypt and Mine shots, rejects the grey wool checker and flat sticker walls, checks the walking line, and writes a pass or fail note per shot.
The manager writes the W11-52 RESULT line and lists the playtest rows. Do not start a motion engineer, an animator, the site compare, the art redo or fishing in this package.
</team_design>

<agent_contract>
For each active agent record: name; mission; inputs; allowed tools; expected artifact; acceptance test; proof required; stop condition; handoff target. Each gets only the files its job needs.
Proof per role: builder, the build line, the EditMode counts, and the Crypt, Mine and EntranceReach logs; reviewer, a pass or fail note tied to each shot it opened, saying whether the grey checker is gone and whether the centre line is clear. A miss goes back once; do not loop past that.
</agent_contract>

<execution_flow>
1. Read Reportcard.md: a FAIL row is fixed first; blank rows are the human's. There is no FAIL.
2. Leave W11-51 stopped. Do not retune it, and do not wait for the section A ratings.
3. If the Unity editor is open, say so and stop. If it is closed, rebuild and prove W11-52 only. One Unity batch job at a time: build, then EditMode, then Crypt, then Mine, then VoxelChecks.EntranceReach.
4. Reviewer opens the new Crypt and Mine shots. They must show carved panels, trims, arches, stone floor tiles and mine timber, not the grey checker. The walking line stays clear.
5. Write the W11-52 RESULT line, name what is still quiet stone, and list the playtest-windows.md section D rows. Stop. The site compare starts only after this cover is in the exe and those shots pass. Gallery photos wait on those shots. The art redo and fishing stay unstarted.
</execution_flow>

<verification>
No proof, no done. A passing script is not enough: open the tour shots yourself. Record what passed, failed and was not checked. The sticker build's Crypt shots are not proof of the cover. Feel is the human's to judge; never report it as verified.
</verification>

<boundaries>
Stay inside the brief. Do not commit, push, delete, deploy, upload or buy. If the Unity editor is open, say so and stop. If a tool fails, report the error and make the smallest fix. Do not invent files, test results or completed actions. Do not retune W11-51. Do not start the site compare, the art redo or fishing in this package.
</boundaries>

<completion_gate>
Before saying W11-52 is done, confirm: the cover is in a fresh build made with the editor closed; EditMode 0 failed; Crypt, Mine and VoxelChecks.EntranceReach ran; the shots were opened and are not the grey checker; the walking line is clear; the TASKBOARD line is written; the quiet ceiling, the band above the 3.5 m panels, the arena block columns and the kit limits are named. Never call the game or the feel finished.
</completion_gate>

<handoff>
Return a short report: what changed, the check counts and log names, which shots you opened and what they show, what is still off, the playtest-windows.md rows to play, and one suggested commit line with no author line.
</handoff>

<start>
Unity editor must be closed. If it is open, say so and stop. If it is closed, rebuild and prove W11-52 only. Do not retune W11-51. Do not wait for feel ratings before the cave rebuild. One Unity batch job at a time.
</start>

</prompt>
