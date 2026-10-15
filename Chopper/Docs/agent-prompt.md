<prompt>

<goal>
Make Chopper look and play closer to the concept sheets, in the order below, and prove each item in Builds/Dev/Chopper.exe before starting the next. Feel is judged only by the human. Never call the game or the feel finished.
</goal>

<project_brief>
Project: Chopper, a co-op survival game (one to four players). Live Unity 6000.5.10f1 project at E:\Projects\project-chop (FishNet, URP). Art is made by script in Blender: Chopper/Art/Scripts. Concepts are in Chopper/Art/Concepts. The shared record is Chopper/Docs/TASKBOARD.md. Week 11 is the truth. Smallest team. No proof, no done. No deploy without the human's approval. Never call the game or the feel finished.
Audience: one developer playing the Windows dev build, sometimes with friends.
Deadline: none.
Available tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. .NET 10 SDK. Git (no gh CLI). Checks: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 <task> (build, player, editmode, blender, unity <Class.Method>, tour <Name>).
Sources: Chopper/Docs/TASKBOARD.md. Read W11-51 through W11-57 and the latest dated RESULT lines before editing. Open the named concept sheet before building toward it. Site compare is local only.
</project_brief>

<where_we_are>
Trust Chopper/Docs/TASKBOARD.md over this paragraph if a later dated line exists. Do not invent check results.
W11-51 motion is LOCKED in the exe. Do not redo it. Do not retune it. Leave these numbers: move 6, sprint 9.5, crouch 2.6, acceleration 16, deceleration 22, turn 480, jump 1.05, gravity 26, landing recovery height 3 / 0.2 s / 0.35, swing commit 0.85, swing move 0.45, swing turn 0.35, strike step 2.5, hit-stop 0.07 / heavy 0.09, hit kick 0.05 / heavy 0.12. Feel goal is Valheim x Enshrouded / Minecraft weight. Feel is the human's.
W11-52/53 cave PLAN is kept. Do not restart as wallpaper. Human rejected the gray checker and then the 3D occupancy cube wallpaper (hard on the eyes). Latest intended look is planar Chopper/Block faces (dirt floors, stone walls, timber courses) with quieter _Edge/_Jitter (world 0.055/0.72, cave 0.035/0.62) in build-1015-0137, tour-Mine-1015-0138 RESULT PASS, tour-Crypt-1015-0121 shots. Still off: empty box rooms, crypt_1 is a pine not the temple, empty chest, pale vein, no water run. Edges still rough vs 2.0.
W11-54 elementals are in build-1014-1934. Do not restart unless the human rejects that look.
W11-55 voxel character was REJECTED on 2026-10-14. Live body is build_chopper.py smooth modules plus atlas. Stance is CharacterSizes.PlayerLegX 0.20 with a remesured Humanoid (build-1014-2239, tour-Concept-1014-2240). Do not run build_chopper_voxel.py.
W11-56 forests, cave mouths, and a fishing start have RESULT lines. Do not restart world lighting. Fishing VISUALS were rebuilt 2026-10-15 01:39: build_fish.py Blender perch (teal, cream belly) in build-1015-0137 / tour-Fishing-1015-0139. Tour cameras still sit under the pond; the bank rod is hard to read.
W11-57 skills are in the exe. EditMode editmode-1014-2239 passed 319, failed 0. tour-Logging-1014-2243 RESULT PASS: skill 0 large 14 swings (plain 9), skill 100 + top gear 3 (still more than one). Mining and fishing skills are hooked. Do not invent a huge skill tree. Do not replace axe, armour, hut, rune, rod, or lure upgrades. Light shafts, rain, and snow already have tour proof on build-1014-2239; do not restart them.
Hut: 0.25 m voxels in build_hall.py (2026-10-14 23:25). Two-post porch gable is in tour-House-1015-0015 (2026-10-15 00:15). Still off vs the hut-zone / start-menu sheets: porch X reads as a blob; house_out_steps is the terrace more than the porch treads.
AxeOnBack: the holstered spine copy is hidden in build-1015-0201 (2026-10-15 02:02 RESULT; editmode-1015-0200 passed 319, failed 0). sheet_back and meadow_behind had no AxeOnBack lump. The human then sent a pond-behind shot that still showed a brown diagonal slab and a pale stub (shield strap / leftover limb). A later pass was interrupted. No later RESULT says those extra back meshes are gone. EXTRA LIMBS ON THE BACK ARE STILL AN OPEN DEFECT. First job.
GroundItem axe/outfit are already Blender modules as of 2026-10-15 02:11 (prop_ground_axe_voxel / prop_ground_outfit_voxel on build-1015-0201, tour-Arrival-1015-0211 RESULT PASS). Not Unity cubes. Still off vs the sheet: block-stepped bits, folded outfit stack, held axe is still the single-blade bear axe. Do not restart them as cubes.
Website: in-game stills were stripped 2026-10-15. Landing page is logo + banner only. Gallery is concept art from Chopper/Art/Concepts/. Use concept art on the site for now. No gameplay stills until the human asks. No deploy.
Frame-time budget already on the board (W5-08): hut 6 ms, night with 16 enemies 7 ms. Keep it.
</where_we_are>

<look_lock>
Open the relevant sheet before every asset. Overall block language is planar texel blocks (chopper-3D-concept-2.0.jpg), NOT 3D cube wallpaper, NOT a razor 1 m black lattice. chopper-3D-concept-3.0.jpg is the TREE-ENEMY sheet, not a character turnaround. All under E:\Projects\project-chop\Chopper\Art\Concepts\:
chopper-3D-concept-2.0.jpg
chopper-3D-concept-3.0.jpg
chopper-all-2-3D-concept-3.0.jpg
chopper-all-3D-concept-3.0.jpg
chopper-character-and-axe-3D-concept-2.0.jpg
chopper-earth-zone-3D-concept-3.0.jpg
chopper-fire-hut-zone-3D-concept-3.0.jpg
chopper-fire-zone-3D-concept-3.0.jpg
chopper-fireelemental-3D-concept-3.0.jpg
chopper-hut-zone-3D-concept-3.0.jpg
chopper-nonelemental-3D-concept-3.0.jpg
chopper-start-menu-background-3D-concept-2.0.jpg
chopper-water-crypt-zone-3D-concept-3.0.jpg
chopper-water-hut-zone-3D-concept-3.0.jpg
chopper-water-zone-3D-concept-3.0.jpg
chopper-waterelemental-3D-concept-3.0.jpg
chopper-wind-hut-zone-3D-concept-3.0.jpg
chopper-wind-zone-3D-concept-3.0.jpg
chopper-windelemental-3D-concept-3.0.jpg
</look_lock>

<what_to_do>
Work these items in order. Finish one item's proof in Builds/Dev/Chopper.exe before starting the next. Do not stop after a handful of files. Do not stop while an item below is still unproven. Do not redesign skills or combat motion. Do not retune W11-51.

1. Character back clean. No extra limbs, no AxeOnBack tail, no pale stub, no mystery strap-limb. Prove with a behind shot and a pond-behind shot. Keep Weapon_R, AxeGrip, HeldItemFrame. AxeOnBack spawn is already off (02:02); remaining extras are the open defect.

2. Soften remaining hard edges (Chopper/Block knobs / merged faces / shallow chamfer on props). No shader replace. No 3D occupancy caves.

3. GroundItem look vs chopper-character-and-axe-3D-concept-2.0.jpg and chopper-3D-concept-2.0.jpg. Modules already exist (02:11). Do not restart as Unity cubes. Refine the block-stepped bits toward the sheet.

4. Fishing cameras above water. Bank shot must show the rod and the perch body.

5. Weapon types = axe, pick, rod as first-class (no invented bow/sword tree). A double-bit axe look is allowed without moving the grip frame.

6. Hut porch vs chopper-hut-zone-3D-concept-3.0.jpg and chopper-start-menu-background-3D-concept-2.0.jpg. Two-post gable is already in (00:15). Refine the weak porch X; do not restart the 0.25 m hall.

7. Cave rooms vs chopper-water-crypt-zone-3D-concept-3.0.jpg (temple mouth, water, vein). Planar Chopper/Block only. No occupancy cubes. No gray checker.

8. Website stays concept-only until the human asks for gameplay stills. Landing page stays logo + banner only. No deploy. No new video.
</what_to_do>

<proof>
No proof, no done. For each item, with the Unity editor closed, one Unity batch job at a time: homework.ps1 build ends Build Finished, Result: Success, error CS lines: 0; EditMode has 0 failed; one tour shows the change; you open the shots and compare them with the named sheets; a TASKBOARD RESULT line names the check counts, log names, shots opened, and what is still off. A script summary is not a picture. Feel is the human's to judge.
Item 1 proof is a Concept tour in Builds/Dev/Chopper.exe: behind and pond-behind shots opened beside chopper-character-and-axe-3D-concept-2.0.jpg. Item 4 proof is a Fishing tour with cameras above water and a bank shot that shows rod + perch. Do not add // or # comments in code. Rationale goes on the TASKBOARD. Edit this file only when asked. Do not touch Assets/_Chopper/Resources/Audio.
</proof>

<boundaries>
Stay inside this prompt. Do not commit, push, delete, deploy, upload, or buy. If the Unity editor is open, say so and stop. If a tool fails, report the error and make the smallest fix. Do not invent files, test results, or completed actions. Do not retune W11-51. Do not restart W11-52, W11-53, or W11-54 unless the human rejects that look. Do not run build_chopper_voxel.py. Do not replace Chopper/Block, Chopper/Water, the chunk meshes, DayNightCycle, or ZonePalette. Do not fight Unity if another bot has the editor. Work on main. The human commits.
</boundaries>

<completion_gate>
Before saying an item is done, confirm: the change is in a fresh build made with the editor closed; EditMode has 0 failed; a tour ran; the shots were opened; the TASKBOARD line is written; misses are named. Then start the next item. Never call the game or the feel finished. Return a partial status until each started item has that proof.
</completion_gate>

<handoff>
Return a short report: what changed, the check counts and log names, which shots you opened and how they compare, what is still off, which item is next, and one suggested commit line with no author line.
</handoff>

<start>
Unity editor must be closed. If it is open, say so and stop. If it is closed, read the task board, then start item 1, the character back. Do not retune W11-51. Do not deploy the site.
</start>

</prompt>
