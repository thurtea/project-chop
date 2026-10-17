# Chopper: handoff brief (written 2026-10-16 23:55 ADT, for a new conversation)

Trust a later TASKBOARD line over this brief. Never invent check results.

## 1. Project setup
- Chopper is a co-op survival game for 1 to 4 players. It is a Unity 6000.5.10f1 project using FishNet and URP.
- Repo: `E:\Projects\project-chop` on the Windows machine. Work on main.
- Shared record: `Chopper/Docs/TASKBOARD.md`. Every proven item gets a RESULT line there.
- Unity: `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe`
- Blender 5.2: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -P <script.py>`. Asset scripts are in `Chopper/Art/Scripts` and exports go to `Chopper/Art/Export`.
- Other tools: Python with PIL, .NET 10 SDK, Git (no gh CLI).
- Playable exe: `E:\Projects\project-chop\Builds\Dev\Chopper.exe` (dev build). The playtest guide is `Chopper/Docs/playtest-windows.md`.
- Checks, run from the repo root:
  - `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build`: dev build into Builds\Dev. It must end "Build Finished, Result: Success" with "error CS lines: 0".
  - `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 editmode`: EditMode tests. It reports the passed and failed counts.
  - `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour <Name>`: runs a Play tour on the dev build (passes `-chopper<Name>`) and prints its RESULT line.
  - `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 unity <Class.Method>`: runs an editor batch method `Chopper.Editor.<Class.Method>`.
  - Also available: `player` and `blender`.
- Logs go to `Logs\homework\`. Tour shots go to `Logs\homework\tour-<Name>-<MMdd-HHmm>\`. Comparison sheets and scratch files go in `C:\Users\itsth\chopper-shots\` (staging folder `...\src`).
- Main tours: Feet, Concept, House, Mine, Crypt, Zones, Gear, Shafts, Fishing, Fly, Logging, MoveTest, Arrival, Boat, Combat, Weapons.
- All tour flags in ShotCapture: AnimProbe, Armour, Arrival, AxeRun, BlockGround, Boat, Cabin, Combat, Combo, Concept, Console, Crypt, DoubleJump, Feet, Fishing, Flicker, Floors, Fly, FoliageWalk, GearLook, GrassTest, GroundProbe, GuardTest, Hold, House, Hut, Inventory, Land, Light, LightAtmo, LightBase, LoadTour, Logging, Look, Mine, Motion, MoveTest, PadHotbar, PadTest, Perf, Pickup, Plants, SetFov, Shafts, Shimmer, Shot, Site, Sky, Swim, Title, TutorialTour, Weapons, Weather, WorldAudit, Yard, Zones.
- Practical notes:
  - homework's `Start-Process -Wait` can hang after Unity exits on compile errors. Check the newest log in `Logs\homework`, and kill only the hung pwsh wrapper or the -batchmode Unity.
  - NetworkBehaviours must be on Player.prefab. Add them through an editor setup method and through `NetworkScenesBuilder`.
  - In builds, vertex-colour meshes use the world material (the same one as `Resources/Props/prop_boat_voxel`).

## 2. Rules (current, verbatim intent)
- No commit, push, deploy, upload or buying. The human commits.
- No deletes unless the human asked for that specific removal. List every deleted file.
- No `//` or `#` comments in new code. Rationale goes on the TASKBOARD.
- Don't touch `Assets/_Chopper/Resources/Audio`.
- Don't retune W11-51. Motion is locked: move 6, sprint 9.5, crouch 2.6, jump 1.05, gravity 26, and so on.
  - Note: the 2026-10-16 slide fix moved acceleration, deceleration and turning into `Core/SteerRules.cs` at the user's request (no sliding).
- Don't run `build_chopper_voxel.py`.
- Keep `Piece_Stairs`.
- Don't replace Chopper/Block, Chopper/Water, DayNightCycle or ZonePalette. Tuning their values is fine. The chunk mesh bevel was approved.
- Ask before replacing ZoneTracker's fog.
- Leave `unity-assets.md` unchanged. Leave the registry value `chopper.atmosphere` alone.
- Custom Blender assets only. No asset-store packs or Unity primitives for art.
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

## 3. Art direction
- `Chopper/Docs/art-direction.md` governs the look:
  - Chunky voxel-adjacent low-poly with bevelled silhouettes and flat hand-painted textures.
  - Subtle haze and a warm/cool grade.
  - Organic water, soft clouds and restrained god-rays.
  - It also has a "Player weapons" section.
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

## 4. User design decisions so far
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
- **Elemental buff:** a hit-count buff. Killing an elemental loads charges; each effective hit spends one.
- **Hotbar:** the pick is on slot 7. Slot 8 is the new Next-weapon key (pad uses the hotbar path).
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
    - Sword: wood 6 / iron 6.
    - Mace: wood 8 / iron 8.
    - Spear: wood 10 / iron 4.
    - All costs live in GameConfig and are crafted at the workbench.
    - Moves: axe fast chop; sword sweep (multi-target); mace overhead smash (stagger and splash); spear thrust (long reach).
    - They must work with the elemental buff.
  - Skills:
    - Fishing, Logging, Mining, Axe, Sword, Mace, Spear, each 0 to 100. XP on use.
    - Modest bonuses: at level 100, about +25% damage or yield and up to 15% faster swings.
    - Death loss: about 5% of the current level per skill (configurable), then a grace period of about 10 min with no further loss (configurable).
    - HUD toast, a Skills page under Tab, save/load with migration from LogSkill/MineSkill/FishSkill, network sync, tests.

## 5. Status at end of 2026-10-16
- **Boat round:**
  - Pulled-back camera through the leap and catch.
  - The camera collides with the shark.
  - The catch happens beside the boat.
  - Rowing pose fixed and pick-up framing widened.
  - The camera ignores the invisible "Boundary" walls (cause of the close camera).
- **Playtest doc:** `playtest-windows.md` updated with a 2026-10-16 section.
- **Slide fix:**

  | Measure | Before | After |
  |---|---|---|
  | Sprint stop | 0.52 m (snow 2.38 m) | 0.15 m (snow 0.15 m) |
  | Turn drift | 2.70 m | 0.00 m |
  | Facing lag | 0.27 s | 0.08 s |

- **Tool animations:** ToolPose and ToolMotion added, with contact-frame chops, pick and hammer. Chop shots: trunk at 1.37 to 1.39 m, facing error 0.
- **Weapons phase 1:** code is in and the tour passed (see 6.3). Not yet reported to the human, and no TASKBOARD RESULT line for it yet.
- **Latest proof (2026-10-16 23:45 to 23:48 ADT):**
  - Build Success, 0 CS errors.
  - EditMode 401/401 passed. That is 395 before the weapon tests plus 6 WeaponRulesTests.
  - Tours:
    - tour-Weapons-1016-2345 PASS: targets axe 1, sword 3, mace 3, spear 2; mace 7.20 vs axe 4.50 units; buff spent; models shown.
    - tour-Logging-1016-2346 PASS.
    - tour-Feet-1016-2348 PASS.
  - The Weapons shots have not been opened yet.
- **All work is uncommitted.** Run `git status` to see it:
  - Modified: many scripts, Player.prefab, tests, and the docs TASKBOARD, agent-prompt, art-direction and playtest-windows.
  - New: SteerRules, ToolMotion, ToolPose, WeaponRules, PlayerWeapons, WeaponSetup, SteerAndToolTests, WeaponRulesTests, `Resources/Weapons/`, build_weapons.py and the sword/mace/spear fbx exports.

## 6. TODO for tomorrow (priority order)
1. **BUG: boots clip through the ground inside caves.**
   - Check FootGrounder and ModelGrounder on cave floors; the ray may hit the wrong layer or the chunk floor height.
   - Add a cave stop to the Feet tour and prove it with shots.
2. **BUG: fishing appears to spawn trees (user t59u).**
   - Find out whether it is real in normal play or tour-only, and fix it. Fishing must never spawn or move trees.
   - Already checked, with nothing found in the fishing code: PlayerFishing, FishSchoolView (spawns only the rod, bobber, aim-ring and fish props) and FishingHotspots.
   - Next suspects:
     - Tree streaming or culling popping in when the fishing or cast camera pulls back.
     - Night saplings or treants spawning while the player idles.
     - Dawn regrow in TreeManager.
     - ScatterMask re-culls.
     - The WorldGen.SurfaceAt change in tree placement.
     - A ShotCapture or tour helper leaking into normal play.
   - Add a test, then rebuild the exe (Chopper.exe must not be running).
3. **Weapons phase 1: finish and report.** What exists:
   - Models: `build_weapons.py`. The fbx files are in `Resources/Weapons` and their checks passed.
   - Rules and inventory:
     - `Core/WeaponRules.cs`: PlayerWeapon enum and per-weapon stats.
     - ItemRules: Sword, Mace and Spear items, plus IsWeapon.
     - GameConfig cost fields and RecipeBook recipes (Sword 4, Mace 5, Spear 6).
     - Keybinds: KeyAction.Weapon on key 8.
   - Player:
     - `Player/PlayerWeapons.cs`: SyncVars, a cycle and select RPC, and the model shown in place of the axe. It is on Player.prefab and in NetworkScenesBuilder.
     - PlayerAxe: per-weapon range, radius, damage, cooldown, sword and spear extra targets, mace splash and stagger, buff per target, and stats for the tour.
   - Save/load: Weapons and Weapon in PlayerRecord.
   - Tools and tests: the dev console `weapons` command; WeaponRulesTests; the Weapons tour.
   - Still to do:
     - Open the Weapons shots (windup and contact per weapon) and judge them.
     - Get a craft-page shot of the weapon recipes.
     - Add a HUD hint for the active weapon.
     - Write the TASKBOARD RESULT line and the phase 1 report.
4. **Skills phase 2** as approved in section 4. Not started.
5. **Known misses:**
   - Catch shot framing.
   - Rod and torch animations still use their old clips.
   - Remote players see the swing start at contact.
   - Leg lag of about 0.05 s.
   - One remaining MoveTest freeze at a real cliff.
   - Fishing and mining are not on ArcRules.
   - The landed shark is only a counter.
   - B is not rebindable.
   - The sharkrod console help text was changed to "normally crafted at the workbench after landing 10 fish"; verify it.
   - Rerun the Arrival tour.
   - The cave cliff drop.
   - The remaining items.
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

## 7. Start
- Check that the Unity editor is closed and that no -batchmode Unity.exe or Chopper.exe is running.
- Read the TASKBOARD from the 2026-10-16 lines onward and run `git status`.
- Then begin TODO 1.
