# Homework

Checks for you to run yourself, so the agents (Claude Code, Cursor, Grok) do not spend tokens and time building, compiling and running tours. Each item has an ID. Do it, then fill the matching row in `Reportcard.md`. The next agent reads the report card before it does anything else.

Rules for the items below:
- Only items that still need doing are listed. Agents remove an item once its report card row is PASS, and add new items at the bottom with the next free ID.
- Every item says what to run, what to look for, and what to paste into the report card.
- Close the Unity editor before any `homework.ps1` command except `tour` (batch jobs need the project to themselves).

## How to run checks

All commands run from the repo root, `E:\Projects\project-chop`, in PowerShell (not Git Bash: the game quits during world load when started from Git Bash).

```powershell
cd E:\Projects\project-chop
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build                 # dev build -> "Build Finished, Result: Success", error CS lines: 0
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour BlockGround      # a Play tour -> its RESULT line, log and shots folder
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 editmode              # unit tests -> "EditMode: Passed - total N, passed N, failed 0"
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 unity VoxelChecks.ZoneSeal   # an editor check -> "[ZoneSeal] RESULT PASS"
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 blender               # check_assets and check_rig SUMMARY lines
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 player                # rebuild Player.prefab -> "Exiting batchmode successfully now!"
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

### HW-7 Rebuild and run the checks for this session
Close the Unity editor, then run these in order and paste each printed line set:
```powershell
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 blender                  # expect: check_assets SUMMARY 137 PASS, 0 FAIL; check_rig SUMMARY ... 0 FAIL (new Gather take)
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 player                   # expect: Exiting batchmode successfully now!
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 unity VoxelChecks.ZoneSeal   # expect: [ZoneSeal] RESULT PASS (the ring now has peaks)
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 editmode                 # expect: EditMode: Passed - total 295, passed 295, failed 0
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build                    # expect: error CS lines: 0 and Build Finished, Result: Success
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour Mine                # expect: [Mine] RESULT PASS (out by the ladder)
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour House               # expect: [House] RESULT PASS, and three "[House] nav" lines each saying PathComplete
```
- Report: the lines each command printed. For `tour House`, paste all three `[House] nav` lines. Each should say PathComplete (an enemy on that floor can leave through the door).

### HW-8 Minimap and ring peaks (look)
In the new build, open the map (M) and walk to the hub's edge.
- Look for: the map shows the block terraces (stepped shading), grey stone and white snow on the mountains, sand at the water; from the hub, the mountain ring has separate stepped peaks with snow tops instead of one even wall. Out past the beach, eight separate peaks stand in the sea (two per forest), stone with snow tops, not one more wall.
- Report: PASS or FAIL, and a screenshot path for each.

### HW-9 Gather motion (look)
Pick a healing herb or a plant (E next to it).
- Look for: Chopper crouches, reaches down with the right hand in front of the foot, lifts the hand to the chest and stands up. Blocky, with no floppy motion-capture look.
- Report: PASS or FAIL and the video time.

### HW-10 Arrival tour (after HW-7, which builds the game with this code)
```powershell
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour Arrival     # expect: [Arrival] RESULT PASS
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour BlockGround # expect: [BlockGround] RESULT PASS, with "building in the hub refused: True" and "building in a forest allowed: True"
```
- Look for: the three `[Arrival]` lines (dropped in, no outfit and no axe, kit on the ground; reached the kit, axe and outfit True; fell, gear left 2 of 2, arrives again in the air over the pond, empty-handed) and the four `arrival_*` shots in the shots folder.
- Report: the printed lines, and whether the shots look right (in the pond, on the bank by the kit, dressed with the axe, back in the pond).

### HW-11 The new start, played (look and feel)
Host a new world in the new build.
- Look for: you fall from the sky into the hub pond, bare-chested in trousers and boots with no axe; you swim to the bank toward the hut and climb out; the outfit bundle (red, brown) and the axe lie on the bank; walking over them puts the outfit on and the axe in the hand. Then, with god mode off, die (type `night` and `spawn 5`, or stand where a big tree falls): your axe and outfit lie where you fell, you fall into the pond again, and walking back picks them up.
- Also: take the hammer (6) in the hub: every piece should be refused with "The hub stays as it is: build in the forests"; in a forest building works as before (W9-03).
- Report: PASS or FAIL per step, and video times. Also say if the drop height (22 m) or the kit spot feels wrong.

### HW-12 Block guard on Chopper (after HW-7, which rebuilds the game with this code)
The block pose (`GuardPose`) never ran on Chopper's own rig (wrong bone names); it now uses the humanoid bones.
```powershell
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour GuardTest   # expect: [Guard] RESULT PASS no spin
```
- Look for: in the `guard_*` shots, the axe comes up in front of the chest with the haft slanting up to the left, the left hand on the haft, knees a little bent, the head of the axe clear of the face and helmet. In play (hold the Block key), the same pose, and it drops cleanly when you let go.
- Report: the RESULT line, and PASS or FAIL for the look with a shot name or video time.

### HW-13 Golden-hour light (look; after HW-7, which rebuilds the game with this code)
The midday sun now stands lower (46 deg, was 62) and a little more golden, and the warm morning and evening haze lasts longer into the day.
```powershell
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour Concept   # shots in the late afternoon
pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour Site      # shots near midday
```
- Look for: warm gold light on the hut, trees and the character with longer shadows; block faces show a clear lit side and a shaded side; shade still reads cool blue, not muddy; distant hills fade into the haze. Nothing too orange or too dark at midday.
- Report: KEEP, or TOO WARM, TOO DARK or TOO FLAT, with a shot name. Paste the last printed lines of each tour.
