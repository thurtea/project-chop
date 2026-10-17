# Chopper playtest on Windows

## Today's playtest, 2026-10-16 (start here)

### Run it
- Exe: `E:\Projects\project-chop\Builds\Dev\Chopper.exe` (development build, built 2026-10-16 22:11 Halifax time; it includes boat round 2). Windows still shows the exe file itself as 2026-10-13 because Unity leaves the launcher stub alone. The game data in `Builds\Dev\Chopper_Data` is from today.
- Start it from PowerShell: `Start-Process "E:\Projects\project-chop\Builds\Dev\Chopper.exe"`, or double-click it.
- You don't need to open Unity or rebuild anything. Networking (FishNet) is already compiled into the build, so there's nothing to regenerate. Close the Unity editor first so it doesn't fight over the project.
- Choose **Host** for a world on this PC. For co-op, a second PC runs the same `Builds\Dev` folder, chooses Join and types the host's six-character code.

### Keys
WASD move, mouse look, Left click attack/cast (hold to charge), Right click guard, Space jump (again for the double jump), Left Alt roll, Left Shift sprint, Left Ctrl crouch, E interact, Tab inventory, M map, 1 axe, 2 torch, 3 potion, 4 rod, 5 food, 6 hammer, 7 pick, H puts away what's in your hands, B boat, the backtick key (left of 1) or F5 opens the console (Esc closes it).

### Checklist (write good / bad / notes on each)
1. Sprint and crouch are toggles: tap Left Shift and it stays on, tap again to stop. The same goes for Left Ctrl crouch.
2. Faster sprint-swim: in deep water tap Left Shift. You should move clearly faster (about 4.8 m/s against 3.0) and use stamina faster.
3. Tools stowed in water: hold the axe (1) or torch (2), then swim. They're put away while you swim and come back on land.
4. Auto-climb: walk into a ledge about 1 block high and you step up without jumping. Swim to a bank and keep pushing W; you should climb out. Walls higher than about 1.6 m stay walls.
5. Pick on slot 7: press 7. The pick is in hand, it mines ore, and H puts it away.
6. Enemy weapons and AI: `spawn earth 2` (mace), `spawn wind 2` (axe), `spawn fire 2` (sword), `spawn water 2` (spear), `spawn plain 3` (random weapons on non-elemental trees). Check their swings differ, they chase you and block, and an elemental buff shows after they land several hits.
7. Bigger enemies: next to the hut door, check ordinary, large and boss (`boss`) sizes read right.
8. New hut exterior: `tp hut`, `time 12`, walk all the way round.
9. No grass through floors: stand on hut floors and on built floors in a meadow. No grass should poke through.
10. Lighting: Settings, Atmosphere slider, default 0.6. If your old save had it at 0, move the slider once (your saved 0 sticks until you do). Try `time 6`, `12`, `17`, `19`.
11. Night torches and moonlight: `night`, light a torch (2). The torch light should be warm and the moonlit ground readable, not pitch black.
12. Water and shores: the pond and the sea, how waves meet the sand, and depth colour.
13. Bevel edges: blocks should show soft edges up close.
14. Clouds: look up in daytime and with `weather cloudy`.
15. Fishing: the rod (4) at a bank, hold Left click to cast, then reel.
16. The boat: `boat` (or craft it at a workbench). On the ocean (open sea past the shore), press B to put it in the water. Tap B by it to board or leave, hold B by it to pick it up. WASD rows (A/D turn). Check that you sit on the middle seat, the oars move, the boat bobs and has momentum, and there's no water inside the hull.
17. The shark: in the boat at sea, with the shark rod, press 4 (the rod key) to cast. The shark should surface 20-30 m ahead with a fin, a wake and a line. A/D keep it in the centre band while it tows you. When it's tired, hold Left click to reel. Hold Left Shift during its leaps. On the catch it should show beside the boat, and the camera should stay back through it all.
18. Shark rod unlock: at the workbench Craft page the shark rod shows "Land 10 fish" until you've landed 10, then it crafts (wood 12, fiber 8, iron 4). For a quick test, type `sharkrod` in the console instead.
19. Console commands (backtick key): `help`, `god`, `fly`, `day`, `night`, `time <h>`, `tp hut`, `wood 100`, `fiber 20`, `iron 10`, `spawn`, `wave`, `killall`, `boss`, `boat`, `sharkrod`, `weather sunny`, `hud`, `atmosphere 0.6`, `fps`.

### Report back (copy this)
```
Item | Good | Bad | Notes (video time)
1 toggle sprint/crouch | | |
2 sprint-swim | | |
3 tools stowed in water | | |
4 auto-climb / climb out of water | | |
5 pick slot 7 | | |
6 enemy weapons + AI + elemental buff | | |
7 enemy sizes | | |
8 hut exterior | | |
9 no grass through floors | | |
10 lighting / Atmosphere 0.6 | | |
11 night torches + moonlight | | |
12 water + shores | | |
13 bevel edges | | |
14 clouds | | |
15 fishing | | |
16 boat | | |
17 shark | | |
18 shark rod unlock | | |
19 console | | |
```

### Known issues today
- The catch view: the shark can cover most of the boat.
- From behind, the rowing arms are mostly hidden by the body.
- The shark fight's built-in test runs near the world edge. In play, row away from the edge if the camera acts oddly.
- The landed shark is only a counter (no item or trophy yet). B can't be rebound in Settings.
- The `sharkrod` console help text still says its unlock is "undecided". It is now the workbench, after 10 fish.
- Fishing and mining don't use the new arc bar yet (only the shark does).
- A 4.5 m cliff at the ridge foot is a real wall, so auto-climb won't go up it.
- This is a development build, so a small "Development Build" label shows in the corner.

---

## Full checklist (earlier builds)

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
- [ ] A crypt room: the walls are carved stone panels with a trim, and each doorway (including the stair) reads as an arch. The floor is stone tiles. Pillars, if you see them, are cut stone with a base and a capital. The grey wool checker from the old shell should be gone.
- [ ] A mine room: timber frames on the walls, a timber frame around each gallery mouth, ore on a solid wall. Not a grey box with one sticker.
- [ ] Walk door to door and along a gallery. The centre line stays clear (no cart, crate, or pillar in the middle).
- [ ] Light: darker and warmer than the old flat orange pools. A lantern and a scorch ember should actually glow. Say if the stone is too dark to read.

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
