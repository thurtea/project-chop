# Chopper: handoff brief (written 2026-10-17 12:40 ADT, for a new conversation)

Trust a later TASKBOARD line over this brief. Never invent check results.

## 1. Project setup
- Chopper is a co-op survival game for 1 to 4 players. It is a Unity 6000.5.10f1 project using FishNet and URP.
- Repo: `E:\Projects\project-chop` on the Windows machine. Work on main.
- Shared record: `Chopper/Docs/TASKBOARD.md`. Every proven item gets a RESULT line there.
- Unity: `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe`
- Blender 5.2: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -P <script.py>`. Asset scripts are in `Chopper/Art/Scripts` and exports go to `Chopper/Art/Export`. homework.ps1 syncs newer Export fbx files into Assets automatically (only files newer than `Tools\export-sync-since.txt`).
- Other tools: Python with PIL, .NET 10 SDK, Git (no gh CLI).
- Playable exe: `E:\Projects\project-chop\Builds\Dev\Chopper.exe` (dev build). The playtest guide is `Chopper/Docs/playtest-windows.md`.
- Checks, run from the repo root:
  - `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build`: dev build into Builds\Dev. It must end "Build Finished, Result: Success" with "error CS lines: 0".
  - `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 editmode`: EditMode tests. It reports the passed and failed counts.
  - `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour <Name>`: runs a Play tour on the dev build (passes `-chopper<Name>`) and prints its RESULT line.
  - `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 unity <Class.Method>`: runs an editor batch method `Chopper.Editor.<Class.Method>`.
  - Also available: `player` and `blender`.
- Logs go to `Logs\homework\`. Tour shots go to `Logs\homework\tour-<Name>-<MMdd-HHmm>\`. Comparison sheets and scratch files go in `C:\Users\itsth\chopper-shots\` (staging folder `...\src`). `Tools/shot_diff.py` diffs two shots.
- Main tours: Feet, Concept, House, Mine, Crypt, Zones, Gear, Shafts, Fishing, Fly, Logging, MoveTest, Arrival, Boat, Combat, Weapons.
- All tour flags in ShotCapture: AnimProbe, Armour, Arrival, AxeRun, BlockGround, Boat, Cabin, Combat, Combo, Concept, Console, Crypt, DoubleJump, Feet, Fishing, Flicker, Floors, Fly, FoliageWalk, GearLook, GrassTest, GroundProbe, GuardTest, Hold, House, Hut, Inventory, Land, Light, LightAtmo, LightBase, LoadTour, Logging, Look, Mine, Motion, MoveTest, PadHotbar, PadTest, Perf, Pickup, Plants, SetFov, Shafts, Shimmer, Shot, Site, Sky, Swim, Title, TutorialTour, Weapons, Weather, WorldAudit, Yard, Zones.
- Other docs: `art-direction.md` (look), `req-assets.md` (living list of needed sounds, icons and animations; the human drops files into `Chopper/Incoming/sounds|icons|animations`), `gamedesigndoc.md`, `PRODUCTION_BRIEF.md`, `playtest-windows.md`.
- Practical notes:
  - homework's `Start-Process -Wait` can hang after Unity exits on compile errors. Check the newest log in `Logs\homework`, and kill only the hung pwsh wrapper or the -batchmode Unity.
  - NetworkBehaviours must be on Player.prefab. Add them through an editor setup method and through `NetworkScenesBuilder`.
  - In builds, vertex-colour meshes use the world material (the same one as `Resources/Props/prop_boat_voxel`).
  - Animator states are added by editor setup methods (for example `WeaponAnimSetup.Apply`), never by hand-editing PlayerAnimator.controller.
  - Cave and crypt interiors: colliders are one BoxCollider per CryptBox; the visible mesh is the separate CaveVoxel 0.5 m voxel shell. Keep new cave boxes on the room's 0.5 m lattice (offsets from the room centre in 0.5 m steps), or visible floors and colliders drift apart.

## 2. Multi-agent workflow
- Several agents (Claude Code, Cline via `.clinerules/`) take turns on this repo. The human relays briefs between them and commits.
- Before starting: check the Unity editor is closed and that no -batchmode Unity.exe or Chopper.exe is running; read the TASKBOARD tail; run `git status`; list `Logs\homework` by time to see what the last agent ran.
- If logs or code show work newer than the last TASKBOARD line, treat it as unreported: read its logs, finish or verify it, and write the line. (On 10-17 a 05:00 session left cave, fishing and weapon-animation code with no TASKBOARD line; see section 6.)
- Any session that leaves code behind writes a TASKBOARD line before it ends, even a PARTIAL or FAIL one.
- End each session with a brief suggested commit message (no author lines).

## 3. Rules (current, verbatim intent)
- No commit, push, deploy, upload or buying. The human commits.
- No deletes unless the human asked for that specific removal. List every deleted file.
- No `//` or `#` comments in new code. Rationale goes on the TASKBOARD.
- Don't touch `Assets/_Chopper/Resources/Audio` unless the human asks for an import.
- Don't retune W11-51. Motion is locked: move 6, sprint 9.5, crouch 2.6, jump 1.05, gravity 26, and so on.
  - Acceleration, deceleration and turning live in `Core/SteerRules.cs` (user's no-slide request, 2026-10-16).
- Don't run `build_chopper_voxel.py`.
- Keep `Piece_Stairs`.
- Don't replace Chopper/Block, Chopper/Water, DayNightCycle or ZonePalette. Tuning their values is fine. The chunk mesh bevel was approved.
- Ask before replacing ZoneTracker's fog.
- Leave `unity-assets.md` unchanged. Leave the registry value `chopper.atmosphere` alone.
- Don't edit `cursor-claude-handoff.md`. Edit this file only when the human asks.
- Custom Blender assets only. No asset-store packs or Unity primitives for art. (KayKit melee clips already in the project are the animation source for player weapon moves.)
- One Unity job at a time, with the editor closed. If the editor is open or another bot has it, say so and don't fight it.
- If a Unity job is stuck, kill only the `-batchmode` Unity.exe, never the Hub or the editor GUI.
- Check that Chopper.exe isn't running before any build.
- Weapons: players DO get the axe (as a weapon), sword, mace and spear, each with its own skill (user rule t58u). Ranged weapons are still excluded.
  - The logging chop on trees stays separate from axe combat.
- Proof discipline, for every item:
  - A fresh build (Success, 0 CS errors).
  - EditMode with 0 failed.
  - The relevant tour(s).
  - Opened shots compared beside the concept sheet.
  - A TASKBOARD RESULT line with counts, log names, the shots opened and what is still off.
  - A script summary is not a picture.
  - Never call the game or the feel finished. Feel is the human's to judge.
  - Report after each phase.
- Work in small steps. If a tool fails, report the error and make the smallest fix. Ask one focused question only if it blocks the work.

## 4. Art direction
- `Chopper/Docs/art-direction.md` governs the look:
  - Chunky voxel-adjacent low-poly with bevelled silhouettes and flat hand-painted textures.
  - Subtle haze and a warm/cool grade.
  - Organic water, soft clouds and restrained god-rays.
  - It also has "Player weapons" and "Lighting and volumetrics overhaul" sections.
- Open the named sheet before building any asset.
- All concept images are in `E:\Projects\project-chop\Chopper\Art\Concepts\`:
  - Hut:
    - `hut-three-story-concept.jpg`: the locked three-story hut (basement storage, hub, dormitory, workshop, outside ladder, chimney).
    - `chopper-hut-outside.jpg`: the hub hut exterior. A Norse log longhouse with a stepped shingle roof, crossed gable horns, a dormer, a porch and a stone foundation.
    - `chopper-hut-zone-3D-concept-3.0.jpg`
  - Hub framing: `chopper-start-menu-background-3D-concept-2.0.jpg` (pines, stepped pond, stone paths, misty mountains).
  - Caves and portals: `cave-chopper-concept-3.0.jpeg`.
  - Player: `chopper-character-and-axe-3D-concept-2.0.jpg`.
  - Overviews: `chopper-3D-concept-2.0.jpg`, `chopper-3D-concept-3.0.jpg` (the tree-enemy overview), `chopper-all-3D-concept-3.0.jpg`.
  - Water crypt zone: `chopper-water-crypt-zone-3D-concept-3.0.jpg`.
  - The 5 tree enemy concepts (these govern):
    - `non elemental tree.jpg`: dark bark, green willow hair, blue eyes, antlers, a shield; carries a random plain weapon.
    - `earth elemental tree.jpg`: gold veins and a gold crown; mace.
    - `fire elemental tree.jpg`: charred body, orange cracks, flame hair; sword.
    - `water elemental tree.jpg`: blue water body, coral base; spear.
    - `wind elemental tree.jpg`: pale cloud body; axe.
  - Older backups: `chopper-fireelemental-3D-concept-3.0.jpg`, `chopper-nonelemental-3D-concept-3.0.jpg`, `chopper-waterelemental-3D-concept-3.0.jpg`, `chopper-windelemental-3D-concept-3.0.jpg`.

## 5. User design decisions so far
- **Toggles:** sprint, crouch and similar are toggles as set in Keybinds.
- **Swimming:** nothing in the hands while swimming. Entering water holsters the axe, rod, torch and hammer; leaving it restores what was held; tools can't be drawn while swimming.
- **Fog and atmosphere:**
  - The Atmosphere setting defaults to 0.6, and is 0 indoors.
  - Distance haze is capped so it never goes milky.
  - ZoneTracker fog stays unless the human approves replacing it.
- **Ladders:** follow the ladder rules on the TASKBOARD. Climb only when facing and touching the ladder, exit cleanly at the top, and no ladder snapping from a distance.
- **Building and world:**
  - Windows are transparent.
  - No white crosses on paths.
- **Pickups:** ground items as they are now on the TASKBOARD (walk-over or press pickup, visible voxel items).
- **Enemies:**
  - Weapons follow the element: earth mace, fire sword, water spear, wind axe. Non-elementals carry a random plain weapon.
  - Enemies are bigger than before.
  - Sapling spawn loop: felled trees leave saplings that can wake into enemies at night.
- **Hub night raids (t64u):** the hub IS attacked at night, but only as a consequence of chopping. Felling any tree in one of the 4 zones spawns 2-3 saplings (a mix of elemental and non-elemental); at night they go for the player and the hub hut. No chopping, no hub raid.
  - Code vs this (checked 2026-10-17): fell-only, zones only, zone-table element mix and night targeting all match. Gaps: the count is per tree size (small 1-2, medium 2-3, large 3-4) times player scaling (coefficient 0.4), not a flat 2-3; trees felled at night plant nothing; global cap 150. The human has not yet said whether to change these.
- **Biome home bases (t63u, not built):** every biome lets the player build a home base. Per biome: beat the boss, clear its crypt, catch its fish, then a quest to build a home, then a quest to raise a magic barrier around it. The barrier needs upkeep; if it lapses, enemies can attack the home. Biome homes are NOT safe at night: crypt enemies raid them every night regardless of chopping.
- **Elemental buff:** a hit-count buff. Killing an elemental loads charges; each effective hit spends one.
- **Hotbar:** the pick is on slot 7. Slot 8 is the Next-weapon key (pad uses the hotbar path).
- **Boat and shark:**
  - The boat is crafted at the workbench, carried, deployed only on ocean water and picked back up.
  - The shark tows the boat in a balance phase; keep the arc centred to drain shark stamina. Then normal reeling.
  - Mining, fishing and the shark are meant to share the arc UI.
  - The shark rod is crafted after landing 10 fish.
- **Movement:**
  - No sliding anywhere: SteerRules gives a firm stop and no drift on turns, including on snow.
  - Per-tool animations (ToolPose and ToolMotion). The axe alternates overhead and diagonal two-handed chops.
  - Chops, pick strikes and hammer placement trigger on the contact frame, with facing and snap to the trunk.
- **Weapons and skills plan (APPROVED):**
  - Weapons:
    - Sword: wood 6 / iron 6. Mace: wood 8 / iron 8. Spear: wood 10 / iron 4.
    - All costs live in GameConfig and are crafted at the workbench.
    - Moves: axe fast chop; sword sweep (multi-target); mace overhead smash (stagger and splash); spear thrust (long reach).
    - Each weapon needs a clearly unique swing (its own clips, not one clip with different stats) (t63u).
    - The tree chop only happens when a tree is targeted (face, snap, blade into the trunk) and is separate from free combat swings.
    - They must work with the elemental buff.
  - Skills:
    - Fishing, Logging, Mining, Axe, Sword, Mace, Spear, each 0 to 100. XP on use.
    - Modest bonuses: at level 100, about +25% damage or yield and up to 15% faster swings.
    - Death loss: about 5% of the current level per skill (configurable), then a grace period of about 10 min with no further loss (configurable).
    - HUD toast, a Skills page under Tab, save/load with migration from LogSkill/MineSkill/FishSkill, network sync, tests.

## 6. Status at 2026-10-17 12:40 ADT
- **Git:** everything up to commit ca5fd5e is committed (the 10-16 work and the 10-17 05:00 session). Uncommitted: the cave fix below (CaveVoxel.cs, CaveShell.cs, MinePlan.cs, ShotCapture.cs, CaveFloorTests.cs, TASKBOARD.md) and this file. `Steam/images/Chopper-Logo.jpg` is the human's untracked file; leave it.
- **Latest proof (one build):** build-1017-1221 Success, 0 CS; editmode-1017-1218 410/410, 0 failed; tour-Feet-1017-1222 PASS; tour-Mine-1017-1224 PASS; tour-Crypt-1017-1225 PASS.
- **Done 10-17, cave boots (TODO 1 from the last brief):** TASKBOARD RESULT line written. The visible cave floor is built at the main floor height (MainFloorTop), the voxel grid is anchored to the main floor box centre (GridAnchor), relief and corner platforms sit on the 0.5 m grid, and crystals no longer grow on the walking floor. Feet: worst sole 0.000, 16 cave floor checks, worst hover 0.046 m. New Feet stop `cave_wall`. Still off: the cave_relief and cave_wall shots are blocked or too dark to judge by eye; terrace steps along wall feet are visual-only.
- **10-17 05:00 session, unreported (no TASKBOARD line yet):**
  - Fishing "spawns trees" (t59u): a FishTrees check in the Fishing tour PASSES (tour-Fishing-1017-0525: tree-like renderers 1082 -> 1082, new 0, moved 0, hidden 0, treants 0 -> 0 after a full session). The likely cause of what the human saw was the underwater fish-run cut camera sitting over the bank and showing trees; `Core/FishShow.RunCutEye/RunCutLook` now keep it under water and off the bank, far clip 60 -> 14 m. FishRunCutTests 3 new.
  - Weapon animations: `Editor/WeaponAnimSetup.Apply` adds 11 KayKit melee states (WeaponRules.MoveClips: Swing/Swing2/Swing3, Sword1-3, Mace1-2, Spear1-3); its RESULT PASS (11 clips, 11 distinct, tree chop distinct). PlayerAnimator now picks the state per weapon and combo step. But tour-Weapons-1017-0541 FAILS: at contact only the mace shows its clip; axe, sword and spear show none, and the tree chop shows no Chop state (facing and reach 1.37 m are fine; turned away from the tree it is a combat swing).
- **Weapons phase 1 (code from 10-16):** WeaponRules, ItemRules (Sword/Mace/Spear, IsWeapon), GameConfig costs, RecipeBook recipes, KeyAction.Weapon on key 8, PlayerWeapons (SyncVars, cycle/select RPC, model in place of the axe), PlayerAxe per-weapon range/damage/cooldown/extra targets/mace splash and stagger/buff per target, save/load, dev console `weapons`, WeaponRulesTests, Weapons tour. Gameplay numbers pass (targets axe 1, sword 3, mace 3, spear 2; mace 7.20 vs axe 4.50 units; buff spent; models shown). Not reported to the human; no TASKBOARD RESULT line.

## 7. TODO (priority order)
1. **Fishing trees (t59u): close it out.** Rebuild-proof is already there (FishTrees PASS). Rerun the Fishing tour on the current build, open the run-cut shots beside the concept, write the TASKBOARD RESULT line, and ask the human whether they still see trees appear in normal play.
2. **Weapons phase 1: fix the clips and report.**
   - Find why axe, sword and spear states (and the tree Chop state) are not playing at contact while the mace is: trigger hashes vs state names, transitions from Empty/Chop, layer weights, or the tour sampling the wrong layer or time.
   - Weapons tour must PASS with unique clips per weapon and a distinct tree chop.
   - Open the windup and contact shots per weapon; get a craft-page shot of the weapon recipes; add a HUD hint for the active weapon.
   - Write the TASKBOARD RESULT line and the phase 1 report.
3. **Skills phase 2** as approved in section 5. Not started.
4. **Hub raid gaps:** ask the human whether to switch to a flat 2-3 saplings per felled tree and whether night fells should plant too. Don't change it before they answer.
5. **Known misses:**
   - Catch shot framing.
   - Rod and torch animations still use their old clips.
   - Remote players see the swing start at contact.
   - Leg lag of about 0.05 s.
   - One remaining MoveTest freeze at a real 4.5 m cliff.
   - Fishing and mining are not on ArcRules.
   - The landed shark is only a counter.
   - B is not rebindable.
   - The sharkrod console help text was changed to "normally crafted at the workbench after landing 10 fish"; verify it.
   - Rerun the Arrival tour.
   - The cave cliff drop.
   - Cave terrace steps along wall feet have no collider; cave_relief and cave_wall shots need better framing.
   - Lighting phase 4+ options.
   - Enemies appear mid-size in frame.
   - Earth gold looks muted.
   - The BoatCamBlockers helper is tour-only.
   - PlayerCamera ignores invisible walls everywhere, which may let it pass world edges.
6. **Older open items:**
   - The three-story hut.
   - Portal and cave interiors versus `cave-chopper-concept-3.0.jpeg`, including the hsh to h01 fix in the other Blender scripts.
   - Fishing polish.
   - Final tours on one build.
   - The website "In game" shots (no deploy).
   - Biome home bases (t63u): design only so far; plan it for the human's review before building.

## 8. Start
- Check that the Unity editor is closed and that no -batchmode Unity.exe or Chopper.exe is running.
- Read the TASKBOARD from the 2026-10-16 lines onward, run `git status`, and list `Logs\homework` by time.
- Then begin TODO 1.
