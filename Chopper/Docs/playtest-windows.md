# Chopper playtest on Windows

Two parts: the computer runs every test it can simulate, then you play for what only a person can judge (feel, look, sound). Tick each box as you go, and write down anything that looks or feels wrong, with the time in the video.

## 1. Run the simulated tests

Close the Unity editor first. Then double-click **`Tools\ChopperTests.exe`** (or run it from PowerShell). It shows a menu:

| Option | What it does | Time |
|---|---|---|
| 1 Everything | Blender art checks, enemy and player rebuilds, zone and entrance checks, unit tests, a fresh dev build, the title screen and every tour | about 20 min |
| 2 Quick | The same without the art checks and unit tests | about 12 min |
| 3 Tours only | Every tour on the build you already have | about 8 min |
| 4 One tour | Pick one tour; its screenshots folder opens when it ends | 1 min |
| 5 Build | Only rebuild `Builds\Dev\Chopper.exe` | 2 min |
| 6 Play | Starts the game | |
| 7, 8, 9 | Open the report card, this checklist, the latest results folder | |

Options 1 to 3 write the **Automatic checks** table at the top of `Chopper\Docs\Reportcard.md`, with a contact sheet of screenshots per tour in `Logs\homework\playtest-<date>`. The same runs are available from PowerShell: `pwsh -ExecutionPolicy Bypass -File Tools\playtest.ps1` (`-Quick`, `-ToursOnly`).

What the simulation covers, so you do not have to:

| ID | Simulated check | Passes when |
|---|---|---|
| A1 | Character and props: sizes, rig, weights, nothing poking through clothes | both SUMMARY lines say 0 FAIL |
| A2 | Strike and flinch clips import | RESULT PASS |
| A3 | Player prefab rebuild | Exiting batchmode successfully |
| A3b | Draugr and tree elemental rebuilt on Chopper's rig | RESULT: draugr on Chopper True |
| A4, A5 | Forests sealed except the passes; crypt and mine doors reachable on foot | RESULT PASS |
| A6 | Unit tests: rules for building, combat, stamina, sizes, loot, fishing and more | failed 0 |
| A7 | Dev build | Result: Success |
| T1 to T15 | Tours: the game plays itself (arrival, swim, double jump, combo, combat, guard, armour, movement, building rules, hut, mine, crypt, fishing, character sheet) and saves screenshots | RESULT PASS, or LOOK (pictures only) |

A **FAIL** row is fixed before anything new. A **LOOK** row is fine; it only means the check is a picture. The report card's rows below the table are yours.

## 2. Start the game yourself

From the menu (option 6), or PowerShell (not Git Bash, which can quit while the world loads):

```powershell
Start-Process "E:\Projects\project-chop\Builds\Dev\Chopper.exe"
```

Choose **Host** to start a world. Use 2560 x 1440 or 1920 x 1080.

## 3. Controls

| Action | Key |
|---|---|
| Move, look | WASD, mouse |
| Attack, chop, cast | Left click (hold to charge a cast) |
| Guard | Right click |
| Jump (again in the air for the double jump) | Space |
| Roll | Left Alt |
| Sprint, crouch | Left Shift, Left Ctrl |
| Interact | E |
| Repair the hut; turn a piece while building | R |
| Axe, torch, potion, rod, food, hammer | 1, 2, 3, 4, 5, 6 |
| Put away what is in the hands | H |
| Inventory, map | Tab, M |
| Developer console | F5 or the key under Esc |

Console: `hud` (hide the HUD), `day`, `night`, `wood 100`, `tp hut`, `tp <x> <z>`, `crypts`, `tp crypt 1`, `fly`, `god`, `spawn 5`, `boss`, `weather sunny`, `fps`, `help`.

Recording: Win + Alt + R starts and stops a Game Bar video. Screenshots: Win + Shift + S.

## 4. What only you can judge

### A. Feel of movement and combat (the next big pass, W11-51)
Rate each from 1 (floaty or twitchy) to 5 (weighty like Valheim, readable like Minecraft), and say why.
- [ ] Starting and stopping a run: does he lean in and plant his feet, or slide?
- [ ] Turning on the spot and while running.
- [ ] Jump height, fall speed, the landing (a short stop after a high fall?).
- [ ] The axe combo: does each swing commit you, and does a hit feel like it lands (a pause, a shake)?
- [ ] Guard, parry and roll timing against a tree enemy.
- [ ] Getting hit: is the flinch clear without taking control away too long?

### B. Enemies (new this build)
- [ ] At night in a forest, tree elementals rise: a bark body, long branch arms with claws, root feet, glowing eyes, and a crown that tells the forest (Water ice, Fire flames, Earth moss and stones, Wind teal wisps, leafy green in between).
- [ ] In a crypt, the draugr are Chopper's own body as the dead: grey-green skin, grey beard, horned helm, rusted mail, a rusty double axe. They should look textured, not flat.
- [ ] Sizes: an ordinary enemy is about your height (1.7 to 2 m), a large elemental about 3.2 m, the forest warden boss about 5.5 m, the crypt boss about 3.2 m. Say if any looks wrong next to the hut door.
- [ ] They walk, swing, flinch and fall with Chopper's blocky motions.

### C. Building (changed this build)
- [ ] In a forest, with no workbench near: floors, walls, roofs, stairs, torches and campfires all go up.
- [ ] A doorway, a window or a gable says "Needs a workbench nearby" until a workbench is built close by.
- [ ] Pieces can hang in the air (no support rule). Nothing falls when a piece below is taken down.
- [ ] The hub still refuses pieces ("build in the forests").

### D. Caves (the redesign after the motion pass)
- [ ] Walk a crypt and a mine. Write down which rooms feel plain and what would make them feel real to you (carved stone, pillars, bones, roots, rubble, timber props, cobwebs, light).

### E. The world and the hut (only if something changed or looks wrong)
- [ ] The title screen, the arrival in the pond, dressing on the bank.
- [ ] The hut inside and out, the pond and fishing, a night with torches.

### F. Sound (W11-44 set, not heard yet)
- [ ] Chops, steps, tree falls, blocks and parries sound like wood, not metal.

### G. Co-op (if a second PC is free)
- [ ] Host, share the six-character code, join from the second PC. Both see the same world.

## 5. Screenshots for the website

Saved as JPG in `Website/images/`, 16:9, taken at full resolution with `hud` off (unless the picture is about the HUD) and `day`, `weather sunny` for daylight.

| File | What to show |
|---|---|
| `character.jpg` | Chopper close, three-quarter front, armour on, the hut behind |
| `hut.jpg` | The house from the front yard |
| `forest-pond.jpg` | The hub pond with the house behind it |
| `night.jpg` | Night at the house with torches lit and a tree elemental or two |
| `crypt.jpg` | A crypt room with draugr |
| `build.jpg` | A small build in progress |
| `fishing.jpg` | Casting or reeling at a bank |
| `fire-forest.jpg` | Inside one of the four forests |

## 6. After the run

Send the notes, the 1 to 5 feel ratings, the video times and the screenshots. Problems go on `Chopper/Docs/TASKBOARD.md`.
