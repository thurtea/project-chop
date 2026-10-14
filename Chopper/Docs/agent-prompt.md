<prompt>

<goal>
Make Chopper look and play closer to the concept sheets, in the order below, and prove each item in Builds/Dev/Chopper.exe before starting the next. Feel is judged only by the human. Never call the game or the feel finished.
</goal>

<project_brief>
Project: Chopper, a co-op survival game (one to four players). Live Unity 6000.5.10f1 project at E:\Projects\project-chop (FishNet, URP). Art is made by script in Blender: Chopper/Art/Scripts. Concepts are in Chopper/Art/Concepts. The shared record is Chopper/Docs/TASKBOARD.md. Week 11 is the truth.
Audience: one developer playing the Windows dev build, sometimes with friends.
Deadline: none.
Available tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. .NET 10 SDK. Git (no gh CLI). Checks: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 <task> (build, player, editmode, blender, unity <Class.Method>, tour <Name>).
Sources: Chopper/Docs/TASKBOARD.md. Read W11-51, W11-52, W11-53, W11-54, W11-55, W11-56, and W11-57 before editing. Open a concept sheet before building toward it.
</project_brief>

<where_we_are>
W11-51 motion is in the dev build and stopped for feel ratings. Do not redo it. Do not retune it. Leave these numbers: move 6, sprint 9.5, crouch 2.6, acceleration 16, deceleration 22, turn 480, jump 1.05, gravity 26, landing recovery height 3 / 0.2 s / 0.35, swing commit 0.85, swing move 0.45, swing turn 0.35, strike step 2.5, hit-stop 0.07 / heavy 0.09, hit kick 0.05 / heavy 0.12.
W11-52 cave cover is in build-1014-1749 (Success, error CS 0). EditMode editmode-1014-1752 passed 309, failed 0.
W11-53 cave shell is in build-1014-1853 (Success, error CS 0). EditMode editmode-1014-1852 passed 309, failed 0. tour-Mine-1014-1855 RESULT PASS. Do not reopen W11-52 or W11-53.
W11-54 elementals are in build-1014-1927 and the close pass build-1014-1934 (Success, error CS 0). EditMode passed 309, failed 0. Shots still miss parts of the 3.0 sheets (neutral body in the night shot; fire eyes, skirt, root feet, and axe at that angle). Do not restart W11-54 unless the human rejects that look.
W11-55 voxel body is in build-1014-1943. The human rejected it on 2026-10-14. The screenshot shows tiny cubes. They want the body smoothed in Blender toward chopper-3D-concept-2.0.jpg and chopper-character-and-axe-3D-concept-2.0.jpg. chopper-3D-concept-3.0.jpg is the tree-enemy sheet, not a character turnaround. A smoothing pass may already be editing the Blender character files and the Unity copies. Read those files and finish that pass. Do not start a second body. Do not run build_chopper_voxel.py as the look.
W11-56 forests, cave ceilings, and a fishing start have RESULT lines. Feel is not verified. Fishing shots do not show a fish body, a jump, or a readable arc. That package is not the work of this chat. Do not restart it.
The site compare is done locally and is not deployed. Do not deploy or upload.
Frame-time budget already on the board (W5-08): hut 6 ms, night with 16 enemies 7 ms. Keep it. Do not raise it to make a new effect fit.
</where_we_are>

<what_to_do>
Work these three items in order. Finish one item's proof in Builds/Dev/Chopper.exe before starting the next. Do not stop after a handful of files. Do not stop while an item below is still unproven.

1. Character. Smooth the tiny-cube body in Blender toward Chopper/Art/Concepts/chopper-3D-concept-2.0.jpg and Chopper/Art/Concepts/chopper-character-and-axe-3D-concept-2.0.jpg. Scripted Blender geometry. Not an AI mesh. Not motion capture. Keep the Humanoid bone names, the module names, the Animator states, and the axe grip frame (Weapon_R, AxeGrip, HeldItemFrame). Sizes stay in Core/CharacterSizes.cs. Prove it in Builds/Dev/Chopper.exe with the Concept tour. Open the shots next to those two sheets and write what still misses.

2. Skills. Required system, recorded as W11-57. Chopping felt wrong going from 2D to 3D. Trees must take more hits than they do now. A skill only makes sense if a lower skill does less damage. Start at 0 logging, 0 mining, and 0 fishing. At 100 the player chops faster, and a large tree is never felled in one hit. That was never fun. Do not invent a huge skill tree. Hook the actions that already exist.
   What exists today, and what it is not: SO_GameConfig hits to fell are small 4, medium 6, large 9. ChopRules.PointsPerHit is 10. ChopRules.SwingPoints(power) is what one swing takes off. TreeHitTracker.ApplyHit compares that with ChopRules.TreePoints. PlayerAxe.ServerResolveHit passes power from PlayerUpgrades.AxeDamageMultiplier into TreeManager.ServerHitTree. That multiplier is gear, levels 0 to 3, values 1, 1.25, 1.5, 2, bought with wood. It is not a logging skill. ChopRules already says later skills multiply power there. There is no logging skill and no mining skill. MiningRules.FreeAt is 3. PlayerMining.ServerStrike adds MiningRules.RhythmProgress. Fishing already has experience: PlayerFishing stores xp, FishingRules.Level reads GameConfig.FishingLevelXp (0, 15, 40, 80, 140), and that level only shortens the bite and helps rarer fish. It is not a 0 to 100 damage skill. Herbs and branches are instant picks (PlayerWood.ServerPickHerb, ServerPickBranch) with no skill. Use the same 0-to-100 pattern only for gathering skills that already exist. Do not add a skill for a pickup that has none. Do not replace the axe, armour, hut, rune, rod, or lure upgrades with a skill tree.
   Skill 0 must do less chop damage than today's plain axe (power 1, so a large tree is more than 9 swings). Skill 100 raises chop damage and may shorten the swing, and ChopRules.SwingsToFell for a large tree stays at least 2 at every skill, including axe gear at its top multiplier. Mine strikes at skill 0 free a vein more slowly than MiningRules.FreeAt does today; skill 100 is faster and still needs more than one strike. Fishing skill 0 is the weak start; skill 100 helps the fight that PlayerFishing already runs. New players, and a reset, start at 0. Save the three skills the same way axe level and fishing xp are saved.

3. Graphics, after the skill proof. Unity 6 URP. No COZY, Enviro, Stylized Water, Flat Kit, Buto, or any paid Asset Store package. Do not replace the custom chunk meshes, the block shader, the water shader, or the day/night and fog scripts. New effects read those values.
   Confirmed custom, not URP Lit: Assets/_Chopper/Shaders/ChopperBlock.shader is Shader "Chopper/Block" (vertex colour, block type in alpha). Assets/_Chopper/Art/Shaders/ChopperWater.shader is Shader "Chopper/Water" (depth absorption, scene colour, foam, fog). Leave both.
   Day and night: DayNightCycle drives RenderSettings.sun (colour, intensity, pitch and yaw; moon when darkness is high; forest canopy multiplies intensity by 0.42). ZonePalette.Fog and FogDensity are the zone fog. ZoneTracker applies them. WeatherSystem eases WeatherRules.Look (Sunny, Cloudy, Rain). That look already multiplies sun, ambient, and sky, and thickens fog. Do not add a second lighting path.
   Light shafts first. Shafts through pine canopies. Intensity and colour come from the existing directional light and from fog density. They are gone when the sun is down. No second volumetric engine. DayNightCycle.ApplyShafts already writes _ForestSunUV, and Assets/_Chopper/Shaders/ForestShafts.shader is a screen pass. W11-56 says those shafts do not read as rays. Fix that pass so it reads the sun and ZonePalette fog. Do not retune W11-51. Do not change lighting, ambient, or the skybox to fake the shafts.
   Then rain and snow. Particles follow the player camera. They are driven by the existing weather state. They must not change lighting, ambient, or the skybox. WeatherSystem.UpdateRain already parents a rain box to the local player and uses WeatherLook.Rain. There is no snow kind. Add snow on that same weather state, camera-relative, without a new sun, ambient, or skybox write. Keep the frame-time budget (hut 6 ms, night with 16 enemies 7 ms).

4. Do not restart finished packages. W11-51 motion, W11-52 and W11-53 cave shell, and W11-54 elementals stay as they are unless the human rejects that look.
</what_to_do>

<proof>
No proof, no done. For each item, with the Unity editor closed, one Unity batch job at a time: homework.ps1 build ends Build Finished, Result: Success, error CS lines: 0; EditMode has 0 failed; one tour shows the change; you open the shots and compare them with the named sheets or with the skill and weather rules; a TASKBOARD RESULT line names the check counts, log names, shots opened, and what is still off. A script summary is not a picture. Feel is the human's to judge.
Character proof is a Concept tour in Builds/Dev/Chopper.exe, shots opened beside the two named character sheets. Skill proof is a logging tour (and a mine strike and a fishing cast if those hooks changed) showing skill 0 takes more hits than today, and that a large tree is still more than one hit at skill 100. Shaft proof is a forest shot with the sun up, and one with the sun down where the shafts are gone. Rain and snow proof is a shot of each on the camera, with lighting, ambient, and skybox unchanged by the particles.
Do not add // or # comments in code. Rationale goes on the TASKBOARD. Edit this file only when asked. Do not touch Assets/_Chopper/Resources/Audio.
</proof>

<boundaries>
Stay inside this prompt. Do not commit, push, delete, deploy, upload, or buy. If the Unity editor is open, say so and stop. If a tool fails, report the error and make the smallest fix. Do not invent files, test results, or completed actions. Do not retune W11-51. Do not restart W11-52, W11-53, or W11-54 unless the human rejects that look. Do not replace Chopper/Block, Chopper/Water, the chunk meshes, DayNightCycle, or ZonePalette. Work on main. The human commits.
</boundaries>

<completion_gate>
Before saying an item is done, confirm: the change is in a fresh build made with the editor closed; EditMode has 0 failed; a tour ran; the shots were opened; the TASKBOARD line is written; misses are named. Then start the next item. Never call the game or the feel finished. Return a partial status until character, skills, light shafts, and rain/snow each have that proof.
</completion_gate>

<handoff>
Return a short report: what changed, the check counts and log names, which shots you opened and how they compare, what is still off, which item is next, and one suggested commit line with no author line.
</handoff>

<start>
Unity editor must be closed. If it is open, say so and stop. If it is closed, read the task board and start item 1, the smoothed character. Do not retune W11-51. Do not deploy the site.
</start>

</prompt>
