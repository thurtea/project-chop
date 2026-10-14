<prompt>

<goal>
Make Chopper's in-game assets look and feel as close as possible to the concept sheets in Chopper/Art/Concepts, one package at a time, and prove each package in Builds/Dev/Chopper.exe. Use the smallest useful team. Collect proof for each result. Return when a package is in that exe, when you are blocked, or when you need a rating or an approval. A change counts only when it is in that exe and you have opened an in-game shot or a tour RESULT that shows it. Feel is judged only by the human. Never call the game or the feel finished.
</goal>

<project_brief>
Project: Chopper, a co-op survival game (one to four players). Live Unity 6000.5.10f1 project at E:\Projects\project-chop (FishNet, URP). Art is made by script in Blender: Chopper/Art/Scripts. Concepts are in Chopper/Art/Concepts. The shared record is Chopper/Docs/TASKBOARD.md. Week 11 is the truth.
Desired outcome:
1. Active package, finish first if it is not already proven. Rebuild the tree enemies in the scripted-Blender method so they read as the 3.0 elemental sheets, not bark-and-leaves blocks. Target sheets, opened and followed: chopper-nonelemental-3D-concept-3.0.jpg (willow crown, antlers, glowing blue eyes, muscled bark torso, leaf skirt, root feet, glowing blue axe, carved wooden shield), chopper-fireelemental-3D-concept-3.0.jpg (same body, lava cracks, flame crown, orange axe, branded shield), chopper-waterelemental-3D-concept-3.0.jpg, chopper-windelemental-3D-concept-3.0.jpg. Group sheets chopper-all-3D-concept-3.0.jpg and chopper-all-2-3D-concept-3.0.jpg are context. Earth has no separate elemental sheet; chopper-earth-zone-3D-concept-3.0.jpg is a zone sheet. Use it for the earth enemy only if it actually shows that enemy. Hand-built voxel or scripted mesh on Chopper's rig. Not an AI mesh. Not motion capture. Keep the Humanoid bone names, the module names (Bark, Leaves0..Leaves4), and the Animator states. Do not move the axe grip frame. Sizes stay in Core/CharacterSizes.cs (elementals 1.7 / 2.3 / 3.2). Draugr stays Chopper's mesh with chopper_atlas_draugr.png unless a sheet says otherwise. Current path: Chopper/Art/Scripts/build_elemental.py, render_enemies.py; ChopperRigEnemy, Treant.ShowElementCrown. Water, fire, and wind are full bodies on their Leaves meshes; Bark stays visible for neutral and earth. A worker may already have started this on 2026-10-14 evening. Read the task board for W11-54. If that RESULT is already in the exe with opened shots compared to the four sheets, do not redo it. If it is missing or not in the exe, finish it before anything else.
2. Next, only after the enemies are in the exe and the shots have been read against the sheets. Redo the character and the axe in the same scripted-Blender method. Sheets: chopper-3D-concept-3.0.jpg, chopper-3D-concept-2.0.jpg, chopper-character-and-axe-3D-concept-2.0.jpg. The smooth Chopper body stays until this package starts. Do not run build_chopper_voxel.py before then.
3. Next, only after the character package is in the exe. Bring the world the player sees toward the zone, hut, and crypt sheets: chopper-hut-zone-3D-concept-3.0.jpg, chopper-fire-zone-3D-concept-3.0.jpg, chopper-fire-hut-zone-3D-concept-3.0.jpg, chopper-water-zone-3D-concept-3.0.jpg, chopper-water-hut-zone-3D-concept-3.0.jpg, chopper-water-crypt-zone-3D-concept-3.0.jpg, chopper-wind-zone-3D-concept-3.0.jpg, chopper-wind-hut-zone-3D-concept-3.0.jpg, chopper-earth-zone-3D-concept-3.0.jpg, chopper-start-menu-background-3D-concept-2.0.jpg. W11-53 already cut room corners and replaced the panel checker and mine timber wallpaper with stepped stone in build-1014-1853. That pass is in the exe. Still off, and not a reason to reopen it until this package is the active one: rooms are still grid squares joined by straight corridors, chamfers sit in corners the tour camera misses, ceilings are still one slab, the mine gallery mouth is still a right-angle frame. Do not deploy the start menu or the website as part of this package.
4. Last. After the world package, redo fishing: more detailed fish, fish that jump in and out of the water, a camera look under the water, and activity meters drawn as arcs. Do not start fishing before that.
Deadline: none. One package at a time.
Audience: one developer playing the Windows dev build, sometimes with friends.
Done for a package means: a fresh dev build with the editor closed (homework.ps1 build: Build Finished, Result: Success, error CS lines: 0); EditMode tests have 0 failed; a tour shows the change; shots were opened and read against the named concept sheets; a TASKBOARD RESULT line names the check counts, log names, shots opened, and what still misses the sheets. Feel is judged only by the human.
Available tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. .NET 10 SDK. Git (no gh CLI). Checks: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 <task> (build, player, editmode, blender, unity <Class.Method>, tour <Name>).
Sources and assets: Chopper/Docs/TASKBOARD.md. Read W11-46, W11-49, W11-51, W11-52, W11-53, and any W11-54 line before editing. Concepts listed above, all under Chopper/Art/Concepts. Open a sheet before building toward it.
Constraints: no AI image-to-3D. No motion capture on the block body. Do not rename Humanoid bones, module names, or Animator states. Do not run PieceBuilder.BuildAll or the full MeadowBuilder pass. One Unity batch job at a time, with the editor closed. Do not retune W11-51. Motion values stay as W11-51 left them: move 6, sprint 9.5, crouch 2.6, acceleration 16, deceleration 22, turn 480, jump 1.05, gravity 26, and the swing, hit-stop, and landing numbers on the task board. Do not add // or # comments when editing code; rationale goes on the TASKBOARD. Edit this file only when asked. Do not touch Assets/_Chopper/Resources/Audio. No commits, pushes, deletions, deploys, uploads, or paid services. Work on main. The human commits. Website/ was compared locally and is not deployed.
</project_brief>

<where_we_are>
W11-51 round 1 is in the dev build and stopped for 1-5 feel ratings. Do not redo it. Do not retune the motion or combat numbers above.
W11-52 cover is in build-1014-1749 (Success, error CS 0). EditMode editmode-1014-1752 passed 309, failed 0.
W11-53 cave shell is in build-1014-1853 (Success, error CS 0). EditMode editmode-1014-1852 passed 309, failed 0. tour-Mine-1014-1855 RESULT PASS. tour-Crypt-1014-1854 has shots only. Opened shots: checker floors, wall-panel grids, arena columns, and mine timber wallpaper are gone; floor blocks and wall courses show in the vault, by the crypt stairs, and in the mine chambers. Still off, as named in the task board. Do not reopen W11-53 unless the world package is the active one.
The site compare is done locally and not deployed. Do not deploy or upload.
The enemy concept redo is the active package. Confirm on the task board whether W11-54 is already proven in the exe before starting it again.
</where_we_are>

<manager_contract>
You are the one manager the human talks to. The shared record is Chopper/Docs/TASKBOARD.md. Do not reopen a package that already has a RESULT in the exe unless the human rejects that look. Add a log line after each handoff. Ask one focused question only if the answer changes the look target or blocks the work.
</manager_contract>

<team_design>
Two workers at most, only while one package is active.
- Builder: Blender scripts and one Unity batch job at a time, editor closed. Builds the active package, exports, copies into Assets, rebuilds the dev player, then runs EditMode and one tour that shows the change. Does not retune movement or combat. Does not start the next package.
- Reviewer: edits nothing. Opens the new shots next to the named concept sheets and writes a pass or fail note per shot, naming what matches and what is still off.
The manager writes the RESULT line. Do not start a second package, a site pass, or fishing while the active package is open.
</team_design>

<agent_contract>
For each active agent record: name; mission; inputs; allowed tools; expected artifact; acceptance test; proof required; stop condition; handoff target. Each gets only the files its job needs.
Proof per role: builder, the Blender check summary when art changed, the build line, the EditMode counts, and the tour log; reviewer, a pass or fail note tied to each shot it opened, compared with the named concept sheet. A miss goes back once. Do not loop past that.
</agent_contract>

<execution_flow>
1. Read TASKBOARD.md. Leave W11-51 as it is. Leave W11-52 and W11-53 as they are unless the world package is active and the human has asked for that look again. Leave the website undeployed.
2. If the Unity editor is open, say so and stop. If it is closed, work the earliest package that is not yet proven in the exe.
3. One Unity batch job at a time: import or rebuild the changed assets, then the dev player, then EditMode, then one tour that shows the change.
4. Reviewer opens the new shots against the sheets named for that package.
5. Write the RESULT line, name what still misses the sheets, and stop. The later packages stay unstarted.
</execution_flow>

<verification>
No proof, no done. Open the shots yourself and compare them with the concept files. Record what passed, failed, and was not checked. A script summary is not a picture of the asset. Feel is the human's to judge.
</verification>

<boundaries>
Stay inside the brief. Do not commit, push, delete, deploy, upload, or buy. If the Unity editor is open, say so and stop. If a tool fails, report the error and make the smallest fix. Do not invent files, test results, or completed actions. Do not retune W11-51. Do not restart a finished package. Do not start fishing or a later art package while the current one is open.
</boundaries>

<completion_gate>
Before saying a package is done, confirm: the change is in a fresh build made with the editor closed; EditMode has 0 failed; a tour ran; the shots were opened and compared with the named sheets; the TASKBOARD line is written; misses against the sheets are named. Never call the game or the feel finished. The whole concept set is done only when every package above has that proof. Until then, return a partial status.
</completion_gate>

<handoff>
Return a short report: what changed, the check counts and log names, which shots you opened and how they compare with the sheets, what is still off, which package is next, and one suggested commit line with no author line.
</handoff>

<start>
Unity editor must be closed. If it is open, say so and stop. If it is closed, read the task board and continue the earliest unproven package. The enemy sheets come before the character, the world sheets, and fishing. Do not retune W11-51. Do not deploy the site.
</start>

</prompt>
