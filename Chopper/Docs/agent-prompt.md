<prompt>

<goal>
Keep making Chopper look and play like the concept sheets, picking up where the 2026-10-15 session stopped. Prove each item in Builds/Dev/Chopper.exe before starting the next. Use the smallest useful team. No proof, no done. Feel is judged only by the human. Never call the game or the feel finished.
</goal>

<project_brief>
Project: Chopper, a co-op survival game (one to four players). Live Unity 6000.5.10f1 project at E:\Projects\project-chop (FishNet, URP). Art is made by script in Blender: Chopper/Art/Scripts. Concepts are in Chopper/Art/Concepts. The shared record is Chopper/Docs/TASKBOARD.md.
Desired outcome: the exe matches the concept sheets item by item, with tour shots as proof.
Audience: one developer playing the Windows dev build, sometimes with friends.
Deadline: none.
Done means: every item in what_to_do has a fresh build, EditMode 0 failed, a tour, opened shots compared with the named sheet, and a TASKBOARD RESULT line.
Available tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. .NET 10 SDK. Git (no gh CLI). Checks: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 <task> (build, player, editmode, blender, unity <Class.Method>, tour <Name>). Tours: Concept, House, Mine, Crypt, Zones, Gear, Shafts, Fishing, Fly, Logging, MoveTest, Arrival. Shots land in Logs\homework\tour-<Name>-<MMdd-HHmm>\ and comparison sheets in C:\Users\itsth\chopper-shots\.
Sources and assets: Chopper/Docs/TASKBOARD.md (read W11-51 to W11-57 and every dated RESULT line from 2026-10-15 19:00 onward before editing). Open the named concept sheet before building toward it.
Constraints: see boundaries.
</project_brief>

<where_we_are>
State at 2026-10-15 21:55. Trust a later dated TASKBOARD line over this paragraph. Do not invent check results. Latest exe: build-1015-2149 (CS 0), editmode-1015-2150 passed 321 failed 0.
W11-51 motion is LOCKED. Do not retune: move 6, sprint 9.5, crouch 2.6, acceleration 16, deceleration 22, turn 480, jump 1.05, gravity 26, landing recovery 3 / 0.2 s / 0.35, swing commit 0.85, swing move 0.45, swing turn 0.35, strike step 2.5, hit-stop 0.07 / heavy 0.09, hit kick 0.05 / heavy 0.12. Feel goal is Valheim x Enshrouded / Minecraft weight.
Grip (19:20 to 19:44): GripRules (Core) stop rate = PlayerDeceleration x StopGrip, 2.5 on normal ground (firm stop, about 0.50 m), 0.4 on Snow (glides about 4.8x farther). Slippery = feet within 0.6 m of a Snow column top (no isGrounded). PlayerAnimator StopSmoothing 14/s rising, 28/s falling, so legs stop with the body. build-1015-1939, tour-MoveTest proof. Done; leave it.
Hand axe (19:32): PlayerAxe.ApplyAxeVisual no longer leaves chopper_axe renderers off after a holster. Axe shows in the right hand (tour-Concept-1015-2024). No AxeOnBack.
Saplings: build_saplings.py voxel saplings, one per element, Props/prop_sapling_N_voxel. Every spawn goes EnemyDirector.ServerSpawn -> Treant.BuildVisual -> voxel. tour-Logging-1015-2038 RESULT PASS (voxel 11, old look 0). The pixel_tree_lowpoly fallback in Treant was removed at 21:55.
Branches: build_branches.py, 4 voxel sticks, BranchManager uses Props/prop_branch_N_voxel. The human likes them. Leave them.
Mine: "Drip Mine" renamed "Mine" (MiningRules.MineName(MineZone.Water)); the other mines are still Cinder Mine, Deep Root Mine, High Gallery Mine (the human was asked whether they should be "Mine" too; no answer yet). CaveVoxel floor sink fixed (tour-Mine-1015-2013 RESULT PASS, feet on floor).
Caves (new lock: Chopper/Art/Concepts/cave-chopper-concept-3.0.jpeg). 3D chunky stepped caves are now WANTED; this supersedes the old "no occupancy cubes / planar only" rule. Target: dark teal stone, saturated cyan crystal clusters, timber mine frames with crossed braces, warm lanterns, stepped relief and ledges. Done so far: crystals cyan (.10/.85/.92) with glow pools; Roughen ledges/overhangs; Chopper/Block got a _Fill property (default 0.22, surface unchanged), cave stone 0.03, portal stone 0.3; underground exposure 0.18 with teal-dark ambient; lanterns 4.6 / 11 m; entrance adits 4.4 m deep with near-black back, two 40 deg timber braces and a king post, 4 threshold crystals. Proof: tour-Mine-1015-2134 RESULT PASS, tour-Crypt-1015-2126 (the "You died!" shot is the tour's deliberate crush test, not a bug). Still off: chambers flat and evenly lit vs the sheet, floors flat, crystals too big and too few, timber frames read dark, no tall stepped cliffs. Entrances still look dusky in daytime: they sit in canyon shade with trees in front of two portals. OPEN.
Fog and light (t20u to t23u, Valheim-style): ZonePalette fog roughly halved; light sky-tinted haze far away, near and mid ground clear; ForestShafts 1.7; DayNightCycle forest grade eased (sun canopy 0.72, sky exposure x0.72, exposure -0.2, tint 0.35). Nights are properly dark (moon 0.32, night exposure 0.02); held torch warm, 1.9x, range 12 m (enough to see a few trees ahead). tour-Zones-1015-2135, tour-Gear-1015-2114 (night torch on/off).
Hut (lock: chopper-start-menu-background-3D-concept-2.0.jpg, plus chopper-hut-zone-3D-concept-3.0.jpg). One floor (loft removed, beds on the main floor), HouseScale 1.8 (storey 3.6 m, he fits the door), WorldGen.TerraceHeight 3.1 so floor and door sit on the ground, stairs draw their 8 collider treads (no clipping), golden-tan roof planks, crossed log corners, posts, shutters, door lintel, 8-board plank floor with seams, roof rafters/purlins. Proof: build-1015-2149, tour-House-1015-2150. The human says it looks NOTHING like the concept yet. Target: low log cabin sitting on the grass (not on a big mound), round notched logs, steep golden shingle roof with crossed gable boards, porch gable, dormer, framed by layered blocky pines, a stepped pond and stone paths. Still off: square logs, flat ceiling, no dormer, sits high, no door leaf, the House tour storey check fails after the hut was lowered, and the steps are still an old asset.
Old assets: the human wants old assets DELETED entirely so nothing can fall back to them, once nothing uses them. Still used: pixel_tree_lowpoly (forest trees, Golem), Piece_Stairs colliders/steps. Replace with voxel versions first, then delete, then list every deleted file for the human (deletion is approved for these; git keeps history).
Fishing (target: New World x Ocarina of Time): cameras above water (rod cam 2.43 m, bank cam 2.63 m); tour probes in to the bank so the rod comes out; pond clarity raised, fish lifted, 1.25x, faint emission; "HOOKED!" 50 px outlined, tension arc readable with black backing (tour-Fishing-1015-2053, 2115). Still off: yellow line arc thin, arcs small at distance, underwater view a little pale, no OoT/New World polish judged by the human.
Enemy weapon arms: build_elemental.py tree elementals carry axe / mace / sword / spear right arms with Elemental_Chop / Thrust / Smash / Stab, bark wrist sleeve so the weapon grows from the arm (tour-Fly-1015-2022). ENEMY ONLY. No player swords, spears, maces, or ranged. Each zone gets several archetypes. Still off: enemies read small.
Character: chopper-character-and-axe-3D-concept-2.0.jpg IS the player. Legs proof stands (tour-Concept-1015-0249). Do not run build_chopper_voxel.py. Leftovers: tighter leg gap, vest X on the back, single-blade held axe, tall boots.
Website: the human now WANTS gameplay stills (supersedes concept-only). Gallery has an "In game" section (Website/images/shots/ingame-*.jpg, 1600x900) above the concept art. Landing page stays logo + banner. No deploy, no upload.
Frame-time budget (W5-08): hut 6 ms, night with 16 enemies 7 ms. Keep it.
</where_we_are>

<look_lock>
Open the relevant sheet before every asset. chopper-character-and-axe-3D-concept-2.0.jpg is the PLAYER. cave-chopper-concept-3.0.jpeg is the CAVE lock. chopper-start-menu-background-3D-concept-2.0.jpg and chopper-hut-zone-3D-concept-3.0.jpg are the HUT and hub world. The four *elemental tree.jpg files are the enemy lock (the older chopper-*elemental-3D-concept-3.0.jpg are backup only). chopper-3D-concept-3.0.jpg is the tree-enemy overview, not a character turnaround.
E:\Projects\project-chop\Chopper\Art\Concepts\cave-chopper-concept-3.0.jpeg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-3D-concept-2.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-all-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-character-and-axe-3D-concept-2.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-fireelemental-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-hut-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-nonelemental-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-start-menu-background-3D-concept-2.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-water-crypt-zone-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-waterelemental-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\chopper-windelemental-3D-concept-3.0.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\fire elemental tree.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\non elemental tree.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\water elemental tree.jpg
E:\Projects\project-chop\Chopper\Art\Concepts\wind elemental tree.jpg
</look_lock>

<what_to_do>
Work in order. Finish one item's proof before starting the next. Do not stop while an item is unproven.

1. Hut and hub vs chopper-start-menu-background-3D-concept-2.0.jpg (TOP PRIORITY, the human is frustrated). Low log cabin on the grass, round notched logs, golden shingle roof with crossed gable boards, porch gable, dormer, sloped ceiling, door leaf. Lower the mound so it reads as sitting on the meadow. Frame it with layered blocky pines, a stepped pond, and stone paths. Fix the House tour storey check. Replace the old steps with voxel steps. Give the wood a real plank and log grain texture. Proof: tour-House concept_view beside the sheet.

2. Old assets: replace pixel_tree_lowpoly (forest trees, Golem) and Piece_Stairs with voxel versions, prove nothing references the old files (search + tours), then delete them and list every deleted file.

3. Caves vs cave-chopper-concept-3.0.jpeg: stepped 3D relief and tall stepped cliffs, uneven floors, more and smaller cyan crystals, lit timber frames with crossed braces, warm lantern pools against dark teal. Then the entrances: read as a dark framed adit in daylight from a distance (canyon shade and trees in front are the known cause).

4. Fishing toward New World x Ocarina of Time: thicker, readable line arc; arcs readable at distance; clear (not pale) underwater view with the fish easy to see.

5. Enemy weapon-arm archetypes per zone (axe / sword / mace / spear, each its own attack: sword sweep, spear thrust, mace overhead smash). Enemies read bigger. Enemy only.

6. Leftovers still unproven: item 3 soften hard block edges, item 4 ground items (axe/outfit) vs the sheets, item 6 pick and rod first-class beside the axe.

7. Queued after the above: a craftable little boat that goes out on the ocean, and a shark fishing minigame for ocean spaces (the shark tows the boat around; tire it out, then reel it in). Ask the human whether the boat needs the workbench.

8. Website: when a new item gets strong clean shots, add them to Website/gallery.html "In game" (copy to Website/images/shots/ as 1600x900 JPG). Skip sheets, composites, failed and "You died!" shots. Landing page stays logo + banner. No deploy.
</what_to_do>

<proof>
No proof, no done. With the Unity editor closed, one Unity batch job at a time: homework.ps1 build ends Build Finished, Result: Success, error CS 0; EditMode has 0 failed (321 at last count); one tour shows the change; open the shots and compare them with the named sheet; write a TASKBOARD RESULT line with check counts, log names, shots opened, and what is still off. A script summary is not a picture. Feel is the human's to judge.
</proof>

<boundaries>
Do not commit, push, deploy, upload, or buy. The human commits. Only delete the old assets named in item 2, after replacements are proven, and list them. Do not add // or # comments in code; rationale goes on the TASKBOARD. Do not touch Assets/_Chopper/Resources/Audio. Do not retune W11-51. Do not run build_chopper_voxel.py. Do not add player swords, spears, maces, or ranged. Do not replace Chopper/Block, Chopper/Water, the chunk meshes, DayNightCycle, or ZonePalette (tuning values is fine). If the Unity editor is open or another bot has it, say so and do not fight it. Work on main. If a tool fails, report the error and make the smallest fix. Do not invent files, test results, or completed actions. Ask one focused question only if it blocks the goal.
</boundaries>

<completion_gate>
Before saying an item is done, confirm: the change is in a fresh build made with the editor closed; EditMode 0 failed; a tour ran; the shots were opened beside the sheet; the TASKBOARD line is written; misses are named. Never call the game or the feel finished. Return a partial status until every started item has that proof.
</completion_gate>

<handoff>
Return a short report: what changed, the build to test, check counts and log names, which shots you opened and how they compare, what is still off, which item is next, any files deleted, and one suggested commit line with no author line.
</handoff>

<start>
Check the Unity editor is closed. Read Chopper/Docs/TASKBOARD.md from 2026-10-15 19:00 onward, then start item 1 (hut and hub vs chopper-start-menu-background-3D-concept-2.0.jpg).
</start>

</prompt>
