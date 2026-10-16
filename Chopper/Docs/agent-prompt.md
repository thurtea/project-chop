<prompt>

<goal>
Keep making Chopper look and play like the concept sheets, picking up where the session stopped on 2026-10-16 00:39. Prove each item in Builds/Dev/Chopper.exe before starting the next. Use the smallest useful team. No proof, no done. Feel is judged only by the human. Never call the game or the feel finished.
</goal>

<project_brief>
Project: Chopper, a co-op survival game (one to four players). Live Unity 6000.5.10f1 project at E:\Projects\project-chop (FishNet, URP). Shared record: Chopper/Docs/TASKBOARD.md. Concepts: Chopper/Art/Concepts.
Desired outcome: every asset is built by a Blender script in Chopper/Art/Scripts and matches its concept sheet as closely as possible, adapted to voxel, lit with Valheim-style depth and volumetrics (warm sun, cool shadow, distance haze, light shafts, dark nights).
Audience: one developer playing the Windows dev build, sometimes with friends.
Deadline: none.
Done means: every item in what_to_do has a fresh build, EditMode 0 failed, a tour, opened shots compared with the named sheet, and a TASKBOARD RESULT line.
Available tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. .NET 10 SDK. Git (no gh CLI). Checks: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 <task> (build, player, editmode, blender, unity <Class.Method>, tour <Name>). Tours: Feet, Concept, House, Mine, Crypt, Zones, Gear, Shafts, Fishing, Fly, Logging, MoveTest, Arrival. Shots land in Logs\homework\tour-<Name>-<MMdd-HHmm>\; comparison sheets in C:\Users\itsth\chopper-shots\.
Sources and assets: Chopper/Docs/TASKBOARD.md. Read W11-51 to W11-57 and every RESULT line from 2026-10-15 21:55 onward before editing (the late lines are dated only "23:40", "00:16", "00:35"). Open the named concept sheet before building toward it.
Constraints: see boundaries.
</project_brief>

<where_we_are>
State at 2026-10-16 00:39. Trust a later TASKBOARD line over this paragraph. Do not invent check results.
Latest proof: build-1016-0031 Build Finished, Result: Success, CS 0; editmode-1016-0032 passed 321, failed 0; tour-Feet-1016-0032 RESULT PASS; tour-Mine-1016-0013 PASS; latest House PASS tour-House-1015-2321.
W11-51 motion is LOCKED. Do not retune: move 6, sprint 9.5, crouch 2.6, acceleration 16, deceleration 22, turn 480, jump 1.05, gravity 26, landing recovery 3 / 0.2 s / 0.35, swing commit 0.85, swing move 0.45, swing turn 0.35, strike step 2.5, hit-stop 0.07 / heavy 0.09, hit kick 0.05 / heavy 0.12. GripRules firm stop on normal ground (0.50 m), snow glide (about 4.8x); PlayerAnimator StopSmoothing 14/28. Done.
Already done today (leave them): hand axe visible; voxel saplings (build_saplings.py) and voxel branches (build_branches.py, the human likes them); "Drip Mine" renamed "Mine" and the cave floor sink fixed; fog halved, dark nights, warm 12 m torch; cave crystals cyan, _Fill 0.03 on cave stone.
Clipping (t29u, TOP PRIORITY): 00:35 new Scripts/Player/FootGrounder.cs (auto-added to every Animator with leg bones: per-foot ground ray, hips drop to the lowest planted foot, two-bone leg lift, after GuardPose). New tour Feet (-chopperFeet): flat, 35 deg slope, slope walk, Earth enemy on a slope, hut steps, hut wall push, mine chamber + walk. tour-Feet-1016-0032 RESULT PASS (8 stops, worst sole 0.000 m, 0 buried bones, Treant sink 0.000); shots chopper-shots\feet_tour1.png. Still open: close camera frames legs only; slope_enemy boots hidden behind a block (sink not seen); Treants have no Animator, so no foot IK (mesh bottom only); ankle calibration varied 0.10-0.18 m between runs; capsule radius 0.5 not refitted to the mesh; body-vs-wall is a 4 cm bone probe, not a mesh test. The human has not confirmed it in play.
Old assets PURGED: 22:20 48 pixel_tree* files deleted (Logs/homework/deleted-legacy-1015.txt); Treant _modelsByElement removed, MeadowBuilder uses prop_pine_* voxel, GolemSetup tree list emptied. 23:00 PixelPurge.Run unpacked 59 nested pack instances, swapped materials to M_World / M_Bark / M_Fire, then deleted Assets/ThirdParty/PixelLowpoly_100Assets and Assets/_Recovery: 950 paths in Logs/homework/deleted-pixelpack-1015.txt (tours House/Mine/Crypt PASS after). 23:10 PixelPackSetup.cs, PixelPackSheet.cs, PixelPurge.cs deleted. MeadowBuilder.FireEffectPath is empty (no voxel fire effect yet). KEEP Piece_Stairs: it is the voxel player build piece (piece_stairs_voxel).
Yard and hub light: 23:10 build_yard.py (stepped stone pond rim + bed, pond water sheet, 2x2 m plank floor tile) via ChopperPropDress.ApplyYard; Hut.cs places prefabs (yard pines, stone path from the door, prop_mountain_backdrop_voxel). Hub light toward Valheim: cooler ambient, noon sun 1.65, cool shadow / warm highlight grade, sun shafts in the hub.
Haze, shafts, mountains: 23:20 to 23:25 ForestShafts adds depth/height volumetric haze (start 42 m, density 0.004, height falloff, cool away from the sun / warm toward it, global _ChopperHaze from DayNightCycle, off underground), shafts x2.4 (hub 0.9). Mountain backdrops about 125 m behind the hut at 2.4-3.4x. Still off: no visible beams outdoors, haze slightly milky on the hut.
Portals (lock cave-chopper-concept-3.0.jpeg): build_portal.py -> prop_mine_portal_voxel (dark round log posts, lintel, braces, stepped dark rock, cyan clusters) + prop_mine_rail_voxel x3 + prop_mine_cart_voxel, black adit quad, forest grade lifted within 28 m of a portal, Earth overhang cleared. 00:16 final round: craggy crown, rock 9 deep beside the posts, 3x3 tapered clusters, cart light; found kit.hsh returns a 32-bit int (not 0..1), so every 'hsh(...) > 0.x' was always true; build_portal.py now uses h01() (tour-Mine-1016-0013 PASS, chopper-shots\mine_portals13.png). The human's open portal misses to recheck: arch reads as a smooth dome, pale stone strips show, the cart reads as a black box, crystals look thin. Also from 00:16: cart small and hidden at the left cheek, rock near-black in daylight (concept is lighter grey-blue), no moss/grass on the crag, and the same hsh bug still lives in other Blender scripts (their noise is dead; fix with h01()).
Cave interiors (still off): chambers evenly lit, floors flat, crystals too big and too few, frames dark, no tall stepped cliffs.
Hut: NEW LOCK Chopper/Art/Concepts/hut-three-story-concept.jpg (three-story timber hut). This REPLACES the one-floor hut. Current exe still has the one-floor 1.8x hut (dormer, proud log courses, yard). Work in progress, not proven, no RESULT line: build_hut3.py, build_hut_props.py, and new exports prop_hut_bunk/notice/sack/stool/table/toolwall/vise/wheel_voxel.fbx, hall_voxel.fbx changed (all staged in git, uncommitted). Check them before building more.
Fishing: cameras above water, HOOKED! cue and tension arc readable (tour-Fishing-1015-2053). Still off: thin yellow line arc, small arcs at distance, pale underwater view, no New World x Ocarina of Time polish judged.
Enemy weapon arms: build_elemental.py tree elementals carry axe / mace / sword / spear right arms with Elemental_Chop / Thrust / Smash / Stab and a bark wrist sleeve (tour-Fly-1015-2022). ENEMY ONLY. Not yet placed as per-zone archetypes; enemies read small.
Character: chopper-character-and-axe-3D-concept-2.0.jpg IS the player. Legs proof stands (tour-Concept-1015-0249). Do not run build_chopper_voxel.py.
Website: the gallery has an "In game" section (Website/images/shots/ingame-*.jpg, 1600x900) above the concept art; those shots predate the portals, haze and hut work. Landing page stays logo + banner. No deploy.
Frame-time budget (W5-08): hut 6 ms, night with 16 enemies 7 ms. Keep it.
</where_we_are>

<look_lock>
Open the relevant sheet before every asset and match it as closely as voxel allows. Build assets in Blender scripts, not Unity primitives.
Hut: Chopper/Art/Concepts/hut-three-story-concept.jpg. Heavy timber on a grassy rock outcrop, steep gable roof with a stone chimney, an outside ladder. Basement: storage cave cut into the rock (barrels, crates, sacks). Ground floor: hub (stone fireplace, notice board, tables and stools, stairs). Second floor: dormitory (bunk and single beds, chests, chimney passing through). Third floor: workshop (workbench, tool wall, vise, spinning wheel). Hub framing stays from chopper-start-menu-background-3D-concept-2.0.jpg (pines, stepped pond, stone paths, misty mountains).
Caves and portals: cave-chopper-concept-3.0.jpeg (dark teal stone, cyan crystals, timber frames with crossed braces, warm lanterns, rails and cart, stepped 3D relief; chunky 3D caves are wanted).
Player: chopper-character-and-axe-3D-concept-2.0.jpg. Enemies: the four *elemental tree.jpg files (older chopper-*elemental-3D-concept-3.0.jpg are backup). chopper-3D-concept-3.0.jpg is the tree-enemy overview.
E:\Projects\project-chop\Chopper\Art\Concepts\hut-three-story-concept.jpg
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

1. Clipping (t29u). Legs and body must not sink into or pass through ground, stairs, walls, or props. Close the 00:35 gaps: a wider Feet camera that shows the whole body, a visible slope_enemy shot, Treant ground contact, stable ankle calibration, the capsule fitted to the mesh, and a body-vs-wall test that uses the mesh. Re-run Feet on the hut stairs, slopes, and cave floors.

2. Three-story hut from hut-three-story-concept.jpg, adapted to voxel. Start from the staged build_hut3.py / build_hut_props.py work. Basement storage cave, ground-floor hub, second-floor dormitory, third-floor workshop, outside ladder, chimney, steep gable roof, outcrop. Every floor walkable, stairs between floors (Piece_Stairs colliders with visible treads), no see-through gaps, the player fits doors and headroom. Update the House tour to visit every floor. Proof: House + Feet tours, shots of the outside and every floor beside the sheet.

3. Finish the portals vs cave-chopper-concept-3.0.jpeg: craggy arch (not a smooth dome), no pale strips, a readable timber cart, chunky crystals, rock in grey-blue daylight, moss on the crag. Fix the hsh -> h01 bug in the other Blender scripts. Then cave interiors: stepped relief, tall cliffs, uneven floors, more and smaller crystals, lit frames, warm lantern pools.

4. Leftover items 3-6: soften hard block edges; ground items vs the sheets; fishing toward New World x Ocarina of Time (thicker line arc, arcs readable at distance, clear underwater view); pick and rod first-class beside the axe.

5. Enemy weapon-arm archetypes per zone (axe / sword / mace / spear, each its own attack: sword sweep, spear thrust, mace overhead smash). Enemies read bigger. Enemy only.

6. Final tours: Feet, Concept, House, Mine, Crypt, Zones, Gear, Shafts, Fishing, Fly, Logging, MoveTest, Arrival on one fresh build; open the shots and list what is still off.

7. Craftable little boat for the ocean and the shark fishing minigame (the shark tows the boat; tire it out, then reel it in). Ask the human whether the boat needs the workbench.

8. Refresh the website gallery "In game" shots from the final tours (Website/images/shots/, 1600x900 JPG). Skip sheets, composites, failed and "You died!" shots. Landing page stays logo + banner. No deploy.
</what_to_do>

<proof>
No proof, no done. With the Unity editor closed, one Unity batch job at a time: homework.ps1 build ends Build Finished, Result: Success, error CS 0; EditMode has 0 failed (321 at last count); one tour shows the change; open the shots and compare them with the named sheet; write a TASKBOARD RESULT line with check counts, log names, shots opened, and what is still off. A script summary is not a picture. Feel is the human's to judge.
</proof>

<boundaries>
Do not commit, push, deploy, upload, or buy. The human commits. Do not delete files unless the human asked for that specific removal; list every deleted file. Do not add // or # comments in code; rationale goes on the TASKBOARD. Do not touch Assets/_Chopper/Resources/Audio. Do not retune W11-51. Do not run build_chopper_voxel.py. Do not add player swords, spears, maces, or ranged. Keep Piece_Stairs. Do not replace Chopper/Block, Chopper/Water, the chunk meshes, DayNightCycle, or ZonePalette (tuning values is fine). One Unity job at a time, editor closed; if the editor is open or another bot has it, say so and do not fight it. If a batch Unity.exe is stuck, kill only that -batchmode process, never the Hub or the editor GUI. Work on main. If a tool fails, report the error and make the smallest fix. Do not invent files, test results, or completed actions. Ask one focused question only if it blocks the goal.
</boundaries>

<completion_gate>
Before saying an item is done, confirm: the change is in a fresh build made with the editor closed; EditMode 0 failed; a tour ran; the shots were opened beside the sheet; the TASKBOARD line is written; misses are named. Never call the game or the feel finished. Return a partial status until every started item has that proof.
</completion_gate>

<handoff>
Return a short report: what changed, the build to test, check counts and log names, which shots you opened and how they compare, what is still off, which item is next, any files deleted, and one suggested commit line with no author line.
</handoff>

<start>
Check that the Unity editor is closed and that no -batchmode Unity.exe is running. Read Chopper/Docs/TASKBOARD.md from the 2026-10-15 21:55 line onward, check git status (the hut3 work is staged, uncommitted), then start item 1 (clipping).
</start>

</prompt>
