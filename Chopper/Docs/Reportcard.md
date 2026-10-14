# Report card

You fill this in after doing the items in `Homework.md`. Agents read it first, before any other work, and act on it:
- **PASS:** closed. The agent does not re-run it, moves the row to History, and removes the item from Homework.md.
- **FAIL:** fixed first, before any new task. The agent uses your notes and does not re-run the test to "see for itself" unless your notes are not enough.
- **SKIP or blank:** still open. The agent does not run it in your place; it carries on with other work and leaves the row as it is.

Status values: `PASS`, `FAIL`, `SKIP`, or for decisions the answer itself (for example `KEEP`). Paste printed lines as they are.

The **Automatic checks** section is written by `Tools\playtest.ps1`; do not edit it by hand. Your rows are under **Current**.

<!-- playtest:start -->
## Automatic checks

Filled by `Tools\playtest.ps1` on 2026-10-13 22:53 (11 min). Do not edit by hand; run the script again.

| ID | Check | Status | Printed line | Log |
|---|---|---|---|---|
| A2 | KayKit strike and flinch clips import | PASS | `[KayKit] RESULT PASS` | `Logs\homework\unity-KayKitImport_Apply-1013-2242.log` |
| A3 | Player prefab rebuild | PASS | `Exiting batchmode successfully now!` | `Logs\homework\player-1013-2243.log` |
| A4 | Zones sealed, passes open | PASS | `[ZoneSeal] RESULT PASS` | `Logs\homework\unity-VoxelChecks_ZoneSeal-1013-2243.log` |
| A5 | Crypt and mine doors reachable on foot | PASS | `[Entrances] RESULT PASS` | `Logs\homework\unity-VoxelChecks_EntranceReach-1013-2243.log` |
| A7 | Dev build | PASS | `Build Finished, Result: Success.` | `Logs\homework\build-1013-2243.log` |
| T1 | Title screen | LOOK | `shots: E:\Projects\project-chop\Logs\homework\tour-Title-1013-2244` | `Logs\homework\tour-Title-1013-2244.log` |
| T2 | Arrival: drop into the pond, kit on the bank | PASS | `[Arrival] RESULT PASS` | `Logs\homework\tour-Arrival-1013-2244.log` |
| T3 | Swimming | LOOK | `shots: E:\Projects\project-chop\Logs\homework\tour-Swim-1013-2245` | `Logs\homework\tour-Swim-1013-2245.log` |
| T4 | Double jump with the flip | PASS | `[DoubleJump] RESULT PASS` | `Logs\homework\tour-DoubleJump-1013-2245.log` |
| T5 | Axe combo (KayKit strikes) | PASS | `[Combo] RESULT PASS (6 of 6 swings playing a strike on the Chop layer)` | `Logs\homework\tour-Combo-1013-2246.log` |
| T6 | Combat: enemy engages, staggers, falls; group takes turns | PASS | `[Combat] RESULT PASS (engaged True, staggered True, down True, group True)` | `Logs\homework\tour-Combat-1013-2246.log` |
| T7 | Block guard | PASS | `[Guard] RESULT PASS no spin (largest body turn in one straight move 80 degrees, axe wobble standing still 3.3 degrees, upper body drift standing still 0.1 degrees)` | `Logs\homework\tour-GuardTest-1013-2247.log` |
| T8 | Armour kit | PASS | `[Armour] RESULT PASS` | `Logs\homework\tour-Armour-1013-2248.log` |
| T9 | Movement: no teleports or freezes | PASS | `[Move] RESULT host teleports 0, freezes 3, speed check snap backs 0, biggest step 0.08 m` | `Logs\homework\tour-MoveTest-1013-2248.log` |
| T10 | Block ground and building rules | PASS | `[BlockGround] RESULT PASS (feet checked 5, worst 0.080 m; trees off 0; climbed out of the pond True; building True)` | `Logs\homework\tour-BlockGround-1013-2249.log` |
| T11 | Hut: floors, stairs, enemies can leave | PASS | `[House] RESULT PASS` | `Logs\homework\tour-House-1013-2250.log` |
| T12 | Mine: portal, veins, ladder out | PASS | `[Mine] RESULT PASS (out by the ladder)` | `Logs\homework\tour-Mine-1013-2250.log` |
| T13 | Crypt: door, rooms, vault | LOOK | `shots: E:\Projects\project-chop\Logs\homework\tour-Crypt-1013-2251` | `Logs\homework\tour-Crypt-1013-2251.log` |
| T14 | Fishing | LOOK | `shots: E:\Projects\project-chop\Logs\homework\tour-Fishing-1013-2252` | `Logs\homework\tour-Fishing-1013-2252.log` |
| T15 | Character against the model sheet | LOOK | `shots: E:\Projects\project-chop\Logs\homework\tour-Concept-1013-2253` | `Logs\homework\tour-Concept-1013-2253.log` |

### Shots to look at

Open each contact sheet. Put anything that looks wrong in your rows below.

| ID | Check | Contact sheet | Look for |
|---|---|---|---|
| T1 | Title screen | `Logs\homework\playtest-1013-2242\T1-Title.jpg` | CHOPPER in the heading font over the meadow and hut, menu Continue..Exit |
| T2 | Arrival: drop into the pond, kit on the bank | `Logs\homework\playtest-1013-2242\T2-Arrival.jpg` | head and shoulders above water while swimming; dressed shot |
| T3 | Swimming | `Logs\homework\playtest-1013-2242\T3-Swim.jpg` | the head and shoulders clear of the water |
| T4 | Double jump with the flip | `Logs\homework\playtest-1013-2242\T4-DoubleJump.jpg` | doublejump_flip: Chopper mid front flip |
| T5 | Axe combo (KayKit strikes) | `Logs\homework\playtest-1013-2242\T5-Combo.jpg` | three different strikes, axe head up, blade forward |
| T6 | Combat: enemy engages, staggers, falls; group takes turns | `Logs\homework\playtest-1013-2242\T6-Combat.jpg` | windup, hit, fall, the group spread round the player |
| T7 | Block guard | `Logs\homework\playtest-1013-2242\T7-GuardTest.jpg` | axe across the chest, left hand on the haft |
| T8 | Armour kit | `Logs\homework\playtest-1013-2242\T8-Armour.jpg` | dressed: no helmet; armoured: helmet and shield, hair hidden |
| T9 | Movement: no teleports or freezes | `Logs\homework\playtest-1013-2242\T9-MoveTest.jpg` |  |
| T10 | Block ground and building rules | `Logs\homework\playtest-1013-2242\T10-BlockGround.jpg` |  |
| T11 | Hut: floors, stairs, enemies can leave | `Logs\homework\playtest-1013-2242\T11-House.jpg` | floors on every level, no light between the log courses |
| T12 | Mine: portal, veins, ladder out | `Logs\homework\playtest-1013-2242\T12-Mine.jpg` | the portal sits on flat ground you can walk to |
| T13 | Crypt: door, rooms, vault | `Logs\homework\playtest-1013-2242\T13-Crypt.jpg` | crypt_1_entrance: the door at ground level, no hill to climb |
| T14 | Fishing | `Logs\homework\playtest-1013-2242\T14-Fishing.jpg` | rod in the right hand, tension bar over the float |
| T15 | Character against the model sheet | `Logs\homework\playtest-1013-2242\T15-Concept.jpg` | character_front beside the model sheet; colours not too bright |
<!-- playtest:end -->

## Current

| ID | Item | Date | Status | Notes, pasted lines, video times |
|---|---|---|---|---|
| HW-1 | Unity opens cleanly |  |  |  |
| HW-2 | Playtest run and video (A to I) |  | A: <br>B: <br>C: <br>D: <br>E: <br>F: <br>G: <br>H: <br>I:  |  |
| HW-3 | Fishing cast in the hand |  |  |  |
| HW-4 | Hut floors and basement |  |  |  |
| HW-5 | Herb colour: KEEP or YELLOWER |  |  |  |
| HW-6 | Website screenshots folder |  |  |  |
| HW-7 | Automatic checks: run `Tools\playtest.ps1`, then look at the contact sheets (PASS when every row above is PASS or LOOK and the sheets look right) |  |  |  |
| HW-8 | Minimap and ring peaks |  |  |  |
| HW-9 | Gather motion |  |  |  |
| HW-11 | The new start, played |  |  |  |
| HW-13 | Golden-hour light: KEEP, TOO WARM, TOO DARK or TOO FLAT |  |  |  |
| HW-14 | Axe strikes and flinch (KayKit clips): KEEP or name what looks wrong |  |  |  |
| HW-15 | Double jump flip: KEEP, TOO FAST, TOO SLOW or REMOVE |  |  |  |
| HW-16 | Swimming: head and shoulders clear of the water |  |  |  |
| HW-17 | Colours: KEEP, STILL TOO BRIGHT or TOO DULL |  |  |  |
| HW-18 | Crypt and mine doors reached on foot, no building |  |  |  |

## History

Closed rows move here, newest first, with what the agent did about them.

| ID | Item | Date | Status | Outcome |
|---|---|---|---|---|
