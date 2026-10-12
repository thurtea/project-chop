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
Open `E:\Projects\project-chop` in Unity 6000.5.10f1 from Unity Hub.
- Look for: no "Enter Safe Mode?" dialog, and the Console shows no red compile errors.
- Report: PASS or FAIL; on FAIL, the first red error line.

### HW-2 Playtest run and video
Play the development build and follow `Chopper/Docs/playtest-windows.md` sections A to I, recording a video. The build in `Builds\Dev` is current (2026-10-12, after the fishing and hut floor fixes). You don't need to rebuild unless you changed something.
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
