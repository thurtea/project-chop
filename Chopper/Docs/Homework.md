# Homework

Checks for you to run yourself, so the agents (Claude Code, Cursor, Grok) do not spend tokens and time building, compiling and running tours. Each item has an ID. Do it, then fill the matching row in `Reportcard.md`. The next agent reads the report card before it does anything else.

Rules for the items below:
- Only items that still need doing are listed. Agents remove an item once its report card row is PASS, and add new items at the bottom with the next free ID.
- Every item says what to run, what to look for, and what to paste into the report card.
- Close the Unity editor before any `homework.ps1` command except `tour` (batch jobs need the project to themselves).

## The quick way: one script for every automatic check

Close the Unity editor, then in PowerShell from the repo root:

```powershell
cd E:\Projects\project-chop
pwsh -ExecutionPolicy Bypass -File Tools\playtest.ps1
pwsh -ExecutionPolicy Bypass -File Tools\playtest.ps1 -Quick
pwsh -ExecutionPolicy Bypass -File Tools\playtest.ps1 -ToursOnly
```

It runs the art checks, the clip import, the player rebuild, the world checks, the unit tests, the dev build and every tour, then writes the **Automatic checks** table at the top of `Reportcard.md` (PASS, FAIL, LOOK or BLOCKED per row) and one contact sheet per tour in `Logs\homework\playtest-<date>\`. You then only play the build (HW-2) and fill your own rows.

## How to run checks one at a time

All commands run from the repo root, `E:\Projects\project-chop`, in PowerShell (not Git Bash: the game quits during world load when started from Git Bash).

```powershell
cd E:\Projects\project-chop
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour BlockGround
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 editmode
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 unity VoxelChecks.ZoneSeal
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 blender
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 player
```

Logs and shots go to `Logs\homework\`. Paste the printed lines into the report card; that is the proof the agent needs.

Tours with a RESULT line: `BlockGround`, `Plants`, `House`, `Armour`, `Sky`, `DoubleJump`, `MoveTest` (`[Move] RESULT`), `WorldAudit` (`[Audit] RESULT`), `Perf` (`[NavCheck] RESULT` plus frame times). Tours that only take pictures: `Fishing`, `Swim`, `Crypt`, `Mine`, `Site`, `Concept`, `GearLook`, `Motion`. For those, paste the last lines and say whether the shots look right.

## Open items

### HW-1 Unity opens cleanly
Open `E:\Projects\project-chop` in Unity 6000.5.10f1 from Unity Hub. This covers every C# change so far, including the 2026-10-12 session (mine tour, house nav probe, minimap from blocks, ring peaks, Gather take wiring). Close the editor again before HW-7.
- Look for: no "Enter Safe Mode?" dialog, and the Console shows no red compile errors.
- Report: PASS or FAIL; on FAIL, the first red error line.

### HW-2 Playtest run and video
Do HW-7 first (it rebuilds the player and the game). Then play the development build and follow `Chopper/Docs/playtest-windows.md` sections A to I, recording a video.
- Look for: what each checklist line says.
- Report: one line per section (A to I): PASS, FAIL or SKIP, and for every FAIL the video time and what you saw.

### HW-3 Fishing cast in the hand
Part of HW-2, section D. Cast a few times at the hub pond.
- Look for: the rod stays in the right hand; the cast swings it back over the shoulder and whips it forward; no throwing motion with an empty hand.
- Report: PASS or FAIL and the video time.

### HW-4 Hut floors and basement
Part of HW-2, section B.
- Look for: plank floors on the basement, main floor and loft; stone-tinted basement walls (no bare dirt); you can walk all three levels and both stairs.
- Report: PASS or FAIL and where.

### HW-5 Herb colour (a decision, not a test)
Look at a healing herb in the meadow (small voxel flower on a green clump).
- Report: KEEP (pale yellow is fine) or YELLOWER (make the flower a stronger yellow).

### HW-6 Website screenshots
Take the pictures in `playtest-windows.md` section 5 and put them in a folder.
- Report: the folder path, and which file names you took. The agent crops them to 1600 x 900 and fills the gallery only after this row is filled.

### HW-7 Automatic checks
Close the Unity editor and run `pwsh -ExecutionPolicy Bypass -File Tools\playtest.ps1`. It replaces the old list of commands (blender, player, ZoneSeal, editmode, build, tours Arrival, GuardTest, Mine, House and the rest).
- Look for: every row of the **Automatic checks** table says PASS or LOOK; then open each contact sheet listed under "Shots to look at" and check what its row says to look for.
- Report: PASS, or FAIL with the row IDs and sheet names that are wrong. If a row says BLOCKED, the Unity editor was open: close it and run again.

### HW-8 Minimap and ring peaks (look)
In the new build, open the map (M) and walk to the hub's edge.
- Look for: the map shows the block terraces (stepped shading), grey stone and white snow on the mountains, sand at the water; from the hub, the mountain ring has separate stepped peaks with snow tops instead of one even wall. Out past the beach, eight separate peaks stand in the sea (two per forest), stone with snow tops, not one more wall.
- Report: PASS or FAIL, and a screenshot path for each.

### HW-9 Gather motion (look)
Pick a healing herb or a plant (E next to it).
- Look for: Chopper crouches, reaches down with the right hand in front of the foot, lifts the hand to the chest and stands up. Blocky, with no floppy motion-capture look.
- Report: PASS or FAIL and the video time.

### HW-11 The new start, played (look and feel)
Host a new world in the new build.
- Look for: you fall from the sky into the hub pond, bare-chested in trousers and boots with no axe; you swim to the bank toward the hut and climb out; the outfit bundle (red, brown) and the axe lie on the bank; walking over them puts the outfit on and the axe in the hand. Then, with god mode off, die (type `night` and `spawn 5`, or stand where a big tree falls): your axe and outfit lie where you fell, you fall into the pond again, and walking back picks them up.
- Also: take the hammer (6) in the hub: every piece should be refused with "The hub stays as it is: build in the forests"; in a forest building works as before (W9-03).
- Report: PASS or FAIL per step, and video times. Also say if the drop height (22 m) or the kit spot feels wrong.

### HW-13 Golden-hour light (look; after HW-7, which rebuilds the game with this code)
The midday sun now stands lower (46 deg, was 62) and a little more golden, and the warm morning and evening haze lasts longer into the day.
```powershell
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour Concept
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour Site
```
- Look for: warm gold light on the hut, trees and the character with longer shadows; block faces show a clear lit side and a shaded side; shade still reads cool blue, not muddy; distant hills fade into the haze. Nothing too orange or too dark at midday.
- Report: KEEP, or TOO WARM, TOO DARK or TOO FLAT, with a shot name. Paste the last printed lines of each tour.

### HW-14 Axe strikes and flinch (look and feel)
The three axe strikes and the flinch are now the blocky KayKit clips: a diagonal slice, a horizontal slice, then an overhead chop; the flinch is KayKit's Hit_A and a parry plays a block shove. Tree chopping is unchanged.
- Look for: in a fight (`night`, `spawn 3`), each click plays the next strike; the axe stays head up with the blade leading; getting hit plays a short flinch; nothing floppy.
- Report: KEEP, or which strike looks wrong and the video time.

### HW-15 Double jump flip (look)
The second jump in the air is now a front flip round the hips (half a second).
- Look for: Space, Space: a clean flip, the axe and gear stay in the hands, the camera does not spin.
- Report: KEEP, TOO FAST, TOO SLOW or REMOVE.

### HW-16 Swimming height (look)
Chopper now floats 35 cm higher in the water.
- Look for: swimming in the hub pond, the head and shoulders are clearly above the surface from the normal camera.
- Report: PASS or FAIL with the video time.

### HW-17 Colours (a decision)
Day colours are a little duller (saturation -26 to -34) and a touch darker (exposure 0.30 to 0.20).
- Report: KEEP, STILL TOO BRIGHT or TOO DULL, with a screenshot if you can.

### HW-18 Crypt and mine doors on foot
Crypt entrances now stand on levelled ground, and a mine is only cut where you can walk up to its portal. `VoxelChecks.EntranceReach` checks this on four seeds (row A5 of the automatic checks).
- Look for: in a **new** world (old saves keep their old ground), `crypts` then `tp crypt 1`, 2 and 3: the door is at ground level with no hill to build up. Walk from a forest path to each mine portal without the hammer.
- Report: PASS, or FAIL with the crypt or mine and a screenshot.

