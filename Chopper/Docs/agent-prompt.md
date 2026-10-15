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
W11-52/53 cave PLAN is kept. Do not restart as wallpaper. Human rejected the gray checker and then the 3D occupancy cube wallpaper (hard on the eyes). Latest intended look is planar Chopper/Block faces (dirt floors, stone walls, timber courses) with quieter _Edge/_Jitter (world 0.055/0.72, cave 0.035/0.62) in build-1015-0137, tour-Mine-1015-0138 RESULT PASS, tour-Crypt-1015-0121 shots. Flatten exe was build-1015-0119 / tour-Mine-1015-0120 RESULT PASS. Still off: empty box rooms, crypt_1 is a pine not the temple, empty chest, pale vein, no water run. Edges still rough vs 2.0.
W11-54 elementals are in build-1014-1934 / tour-Fly-1014-1935. That look was locked to the older chopper-*elemental-3D-concept-3.0.jpg sheets. The human now wants those four bodies rebuilt from the four new tree jpgs after the character Concept proof. Do not invent a new rig or clips.
W11-55 voxel character was REJECTED on 2026-10-14. Live body is build_chopper.py smooth modules plus atlas. Stance is CharacterSizes.PlayerLegX 0.20 with a remesured Humanoid (build-1014-2239, tour-Concept-1014-2240). Do not run build_chopper_voxel.py.
W11-56 forests, cave mouths, and a fishing start have RESULT lines. Do not restart world lighting. Fishing VISUALS were rebuilt 2026-10-15 01:39: build_fish.py Blender perch (teal, cream belly) in build-1015-0137 / tour-Fishing-1015-0139. Tour cameras still sit under the pond; the bank rod is hard to read.
W11-57 skills are in the exe. EditMode editmode-1014-2239 passed 319, failed 0. tour-Logging-1014-2243 RESULT PASS: skill 0 large 14 swings (plain 9), skill 100 + top gear 3 (still more than one). Mining and fishing skills are hooked. Do not invent a huge skill tree. Do not replace axe, armour, hut, rune, rod, or lure upgrades. Light shafts, rain, and snow already have tour proof on build-1014-2239; do not restart them.
Hut: 0.25 m voxels in build_hall.py (2026-10-14 23:25, build-1014-2324, tour-House-1014-2325 RESULT PASS). Two-post porch gable is in tour-House-1015-0015 RESULT PASS (build-1015-0014). Still off vs the hut-zone / start-menu sheets: porch X reads as a blob; house_out_steps is the terrace more than the porch treads.
AxeOnBack: the holstered spine copy is hidden in build-1015-0201 / tour-Concept-1015-0202. Do not re-hunt back props as the legs bug.
GroundItem axe/outfit are already Blender modules as of 2026-10-15 02:11 (prop_ground_axe_voxel / prop_ground_outfit_voxel on build-1015-0201, tour-Arrival-1015-0211 RESULT PASS). Not Unity cubes. Still off vs the sheet: block-stepped bits, folded outfit stack, held axe is still the single-blade bear axe. Do not restart them as cubes.
Character vs chopper-character-and-axe-3D-concept-2.0.jpg: that sheet IS the player. Human: the pond-behind "tail" / extra limbs were THE LEGS, not shield/AxeOnBack — two thick columns with a gap, not a thigh out the back. 2026-10-15 02:49 RESULT PASS on the tail/lump: classify threshold 0.2 -> 0.36, PlayerLegForward 0.03 / LEG_Y -0.03, player-1015-0247, editmode-1015-0248 passed 319 failed 0, build-1015-0248 Build Finished Result Success error CS 0, tour-Concept-1015-0249 shots only. Shots opened beside that sheet: sheet_front, sheet_three_quarter, sheet_back, sheet_above, pond_behind. Legs PASS (two columns, no stub). Still off: front gap tighter than the turnaround; vest crossbelt shows on the back (sheet back is plaid only); held axe is still the single-blade bear axe; boots taller than the sheet's short blocks. Shield/vest/mail stay visible. If a later board line reopens the legs, trust that line and finish character first. If 02:49 still stands, do not restart the legs hunt.
Website: in-game stills were stripped 2026-10-15 02:30. Landing page is logo + banner only. Gallery is concept art from Chopper/Art/Concepts/. Use concept art on the site for now. No gameplay stills until the human asks. No deploy.
Frame-time budget already on the board (W5-08): hut 6 ms, night with 16 enemies 7 ms. Keep it.
</where_we_are>

<look_lock>
Open the relevant sheet before every asset. Overall block language is planar texel blocks (chopper-3D-concept-2.0.jpg), NOT 3D cube wallpaper, NOT a razor 1 m black lattice. chopper-3D-concept-3.0.jpg is the TREE-ENEMY overview, not a character turnaround. chopper-character-and-axe-3D-concept-2.0.jpg is the PLAYER lock. The four *elemental tree.jpg files are the new enemy lock (older chopper-*elemental-3D-concept-3.0.jpg still exist as backup only).
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-3D-concept-2.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-all-2-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-all-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-character-and-axe-3D-concept-2.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-earth-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-fire-hut-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-fire-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-fireelemental-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-hut-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-nonelemental-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-start-menu-background-3D-concept-2.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-water-crypt-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-water-hut-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-water-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-waterelemental-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-wind-hut-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-wind-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-windelemental-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\non elemental tree.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\fire elemental tree.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\water elemental tree.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\wind elemental tree.jpg
</look_lock>

<what_to_do>
Work these items in order. Finish one item's proof in Builds/Dev/Chopper.exe before starting the next. Do not stop after a handful of files. Do not stop while an item below is still unproven. Do not redesign skills or combat motion. Do not retune W11-51.

1. Character vs E:\Projects\project-chop\Chopper\Art\Concepts\chopper-character-and-axe-3D-concept-2.0.jpg. That sheet IS the character — very simple, make that character. Legs: two thick columns, clear gap, under the torso, no thigh behind the spine. Keep Weapon_R, AxeGrip, HeldItemFrame. Do not hide the shield. AxeOnBack spawn is already off (build-1015-0201). As of 2026-10-15 02:49 the legs-as-tail proof exists (tour-Concept-1015-0249 Legs PASS). If the board still says that and no later line reopens the legs, skip to item 2. If a later line reopens the legs, finish this proof first (front / behind / above / three-quarter beside that sheet) and do not start trees until it exists. Remaining sheet misses after 02:49 (tighter gap, vest X on the back, single-blade held axe, taller boots) are leftover notes, not a reopen of the tail bug.

2. AFTER the character Concept proof exists: rebuild tree-elemental enemies from the four new tree jpgs at the same accuracy as the character. Scripted boxes on Chopper's rig (build_elemental.py). Keep Humanoid, Elemental_Walk / Elemental_Slam. Map:
E:\Projects\project-chop\Chopper\Art\Concepts\non elemental tree.jpg → Bark / nonelemental
E:\Projects\project-chop\Chopper\Art\Concepts\fire elemental tree.jpg → fire
E:\Projects\project-chop\Chopper\Art\Concepts\water elemental tree.jpg → water
E:\Projects\project-chop\Chopper\Art\Concepts\wind elemental tree.jpg → wind
Older chopper-*elemental-3D-concept-3.0.jpg still exist; these four tree jpgs are the new enemy lock. chopper-3D-concept-3.0.jpg remains TREE-ENEMY overview, not a player turnaround.

3. Soften remaining hard block edges (Chopper/Block knobs / merged faces / shallow chamfer on props). No shader replace. No 3D occupancy caves.

4. GroundItem look vs chopper-character-and-axe-3D-concept-2.0.jpg and chopper-3D-concept-2.0.jpg. Modules already exist (02:11). Do not restart as Unity cubes. Refine the block-stepped bits toward the sheet.

5. Fishing cameras above water. Bank shot must show the rod and the perch body. Blender perch already exists (01:39).

6. Weapon types = axe, pick, rod as first-class (no invented bow/sword skill tree). A double-bit axe look is allowed without moving the grip frame. No AxeOnBack tail.

7. Hut porch vs chopper-hut-zone-3D-concept-3.0.jpg and chopper-start-menu-background-3D-concept-2.0.jpg. Two-post gable is already in tour-House-1015-0015. Refine the weak porch X if still weak; do not restart the 0.25 m hall.

8. Cave rooms vs chopper-water-crypt-zone-3D-concept-3.0.jpg (temple mouth, water, vein). Planar Chopper/Block only. No occupancy cubes. No gray checker.

9. Website stays concept-only until the human asks for gameplay stills. Landing page stays logo + banner only. No deploy. No new video.
</what_to_do>

<proof>
No proof, no done. For each item, with the Unity editor closed, one Unity batch job at a time: homework.ps1 build ends Build Finished, Result: Success, error CS lines: 0; EditMode has 0 failed; one tour shows the change; you open the shots and compare them with the named sheets; a TASKBOARD RESULT line names the check counts, log names, shots opened, and what is still off. A script summary is not a picture. Feel is the human's to judge.
Item 1 proof is a Concept tour in Builds/Dev/Chopper.exe: front, behind, above, and three-quarter opened beside chopper-character-and-axe-3D-concept-2.0.jpg (exists as tour-Concept-1015-0249 unless a later line reopens it). Item 2 proof is an enemy/Fly tour beside the four tree jpgs. Item 5 proof is a Fishing tour with cameras above water and a bank shot that shows rod + perch. Do not add // or # comments in code. Rationale goes on the TASKBOARD. Edit this file only when asked. Do not touch Assets/_Chopper/Resources/Audio.
</proof>

<boundaries>
Stay inside this prompt. Do not commit, push, delete, deploy, upload, or buy. If the Unity editor is open, say so and stop. If a tool fails, report the error and make the smallest fix. Do not invent files, test results, or completed actions. Do not retune W11-51. Do not restart W11-52 or W11-53 as wallpaper or occupancy cubes. Do not restart W11-54 unless you are on item 2 (the four new tree jpgs). Do not run build_chopper_voxel.py. Do not replace Chopper/Block, Chopper/Water, the chunk meshes, DayNightCycle, or ZonePalette. Do not fight Unity if another bot has the editor. Work on main. The human commits.
</boundaries>

<completion_gate>
Before saying an item is done, confirm: the change is in a fresh build made with the editor closed; EditMode has 0 failed; a tour ran; the shots were opened; the TASKBOARD line is written; misses are named. Then start the next item. Never call the game or the feel finished. Return a partial status until each started item has that proof.
</completion_gate>

<handoff>
Return a short report: what changed, the check counts and log names, which shots you opened and how they compare, what is still off, which item is next, and one suggested commit line with no author line.
</handoff>

<start>
Unity editor must be closed. If it is open, say so and stop. If it is closed, read the task board, then start item 1: the character vs chopper-character-and-axe-3D-concept-2.0.jpg, legs first. As of 2026-10-15 02:49 that legs proof exists (tour-Concept-1015-0249 Legs PASS). If the board still says that and no later line reopens the legs, skip to item 2 (tree elementals from the four tree jpgs). Do not start tree enemies until that character Concept proof exists. Do not retune W11-51. Do not deploy the site.
</start>

</prompt>
