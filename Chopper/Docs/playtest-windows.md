# Chopper playtest on Windows

A run-through to play yourself, record a video, and take the website screenshots. Tick each box as you go. Write down anything that looks or feels wrong, with the time in the video.

## 1. Start the game

Pick one:

- **Unity editor:** open `E:\Projects\project-chop` in Unity 6000.5.10f1 from Unity Hub. Open `Assets/_Chopper/Scenes/Boot.unity` and press Play.
- **Development build:** from PowerShell (not Git Bash, which can quit during world load):
  ```powershell
  Start-Process "E:\Projects\project-chop\Builds\Dev\Chopper.exe"
  ```
  The build is current as of 2026-10-12. If you change anything in Unity, rebuild first with `Chopper.Editor.BuildTools.BuildDevPlayer`.

In the menu choose **Host** to start a world. Set the screen to 2560 x 1440 or 1920 x 1080 (16:9).

## 2. Controls

| Action | Key |
|---|---|
| Move, look | WASD, mouse |
| Attack, chop, cast | Left click (hold to charge a cast) |
| Guard | Right click |
| Jump (press again in the air to double jump) | Space |
| Roll | Left Alt |
| Sprint, crouch | Left Shift, Left Ctrl |
| Interact | E |
| Repair the hut; turn a piece while building | R |
| Axe, torch, potion, rod, food, hammer | 1, 2, 3, 4, 5, 6 |
| Put away what is in the hands | H |
| Inventory, map | Tab, M |
| Developer console | F5 or the key under Esc (`) |

Useful console commands: `hud` (hide the HUD for screenshots), `day`, `night`, `repair`, `wood 100`, `torches 3`, `tp hut`, `tp <x> <z>`, `fly`, `god`, `spawn 5`, `boss`, `weather sunny`, `fps`, `help`.

## 3. Recording

- Video: Win + Alt + R starts and stops a Game Bar recording (or use OBS). Record at 1080p or higher, 60 fps if you can.
- Screenshots: Win + Shift + S, or Game Bar (Win + Alt + PrtScn). Type `hud` in the console first so the bars and hotbar are hidden, and again to show them.

## 4. Checklist

### A. Character and movement (first minute)
- [ ] You arrive by falling from the sky into the hub pond, bare-chested in trousers and boots, with no axe.
- [ ] Swim to the bank toward the house and climb out. The outfit (plaid shirt, patched vest, belt) and the bear-head axe lie on the bank: walk over them to put them on.
- [ ] Walk, run, sprint, crouch: blocky and firm, no floppy limbs.
- [ ] Jump, then double jump in the air. A third press in the air does nothing.
- [ ] Roll (Left Alt).
- [ ] The axe is held head up, blade forward, both standing and walking.
- [ ] Chop a tree with left click: the three swings chain, and the tree falls and leaves a stump.

### B. The hub house
- [ ] It looks like the concept hut: stone base, log walls between dark posts, a golden plank roof, big crossed beams at the gable ends, a small ridge dormer, and a porch with its own crossed gable over the door.
- [ ] Repair it: bring 10 wood and press R at the hut (or type `repair`).
- [ ] Inside, the floors show on every level: the basement (stone-tinted walls), the main floor, and the loft. Stairs go basement to main floor and main floor to the loft.
- [ ] Two beds in the loft, two workbenches (main floor and basement), the chest and the fireplace on the main floor.
- [ ] Walk in and out the door and up the porch steps without getting stuck.

### C. Block ground and mountains
- [ ] Single half-block steps are walked up without jumping.
- [ ] A step of two blocks or more needs a jump.
- [ ] The mountains around the hub are stone steps of about 4.5 m, with snow on the highest tops. Try to climb them with double jumps: you should not get over.
- [ ] Each path leaves the hub through a pass into its forest (Water, Earth, Fire, Wind).
- [ ] No black wedges or holes along the grass steps. Every step shows a green grass lip over brown dirt.

### D. Water and fishing
- [ ] The pond is flat blue with a stepped sand bank and a shallow edge you can wade in.
- [ ] Swim to the middle, then swim back to the bank: you climb out without jumping.
- [ ] Fishing (rod on 4): stand at the bank, hold left click to charge, let go to cast, wait for the bite, reel in.
- [ ] The rod stays in the right hand the whole time: the cast swings it back over the shoulder and whips it forward (new, 2026-10-12, check it on video).

### E. Plants, torch and campfire
- [ ] Healing herbs are small yellow voxel flowers on a green clump (tell me if they should be a stronger yellow).
- [ ] At night (`night`), take the torch (2): a lit voxel torch in the hand.
- [ ] Build a campfire with the hammer (6): logs and stones, with flames.

### F. Building with the hammer (6)
- [ ] Place a workbench first; pieces need one nearby.
- [ ] A floor on flat grass sits flush on the blocks.
- [ ] A floor over a half-block step is allowed and sits on the higher step.
- [ ] Over a bigger step the ghost turns red (too steep).
- [ ] Walls, doorway, stairs, roof and torches snap together; R turns a piece.

### G. Night, enemies and armour
- [ ] At night tree enemies wake and come at you. Guard (right click) and roll work against them.
- [ ] `boss` makes a grove warden. Fell it: it drops an armour kit. Use the kit from the bag (Tab).
- [ ] With armour on: skull helmet with bone horns, chainmail with scale rows, bracers and the shield on the back. The hair hides under the helmet; the beard stays.

### H. Crypts and mines
- [ ] `crypts` lists them; `tp crypt 1` takes you to a door. Go in, take the stairs, find the vault and loot, and come back up.
- [ ] Mines sit in notches at the mountain foot. Go through the portal, strike a vein (it glows), and keep a find.
- [ ] Climb out by the ladder in the first chamber (E at its foot): you come back up beside the mine's portal. (The earlier "ladder does nothing" came from the test tour's teleport, not the game; check it here.)

### I. Co-op (if a second PC is free)
- [ ] Host, share the six-character code, and join from the second PC. Both players see the same world, house and trees.

## 5. Screenshots for the website

The gallery takes 16:9 pictures, saved at 1600 x 900 as JPG in `Website/images/`. Take them at full resolution and they are cropped and scaled later. Hide the HUD (`hud`) unless the picture is about the HUD. Use `day` and `weather sunny` for daylight shots.

| File | What to show |
|---|---|
| `character.jpg` | Chopper close, three-quarter front, armour on, the hut behind (the concept 2.0 framing) |
| `hut.jpg` | The house from the front yard with the porch and the crossed gables |
| `forest-pond.jpg` | The hub pond with the house behind it |
| `pass.jpg` | A path running through a mountain pass, stone steps on both sides |
| `fishing.jpg` | Casting or reeling at a bank, the rod in the hand |
| `night.jpg` | Night at the house with torches lit and an enemy or two |
| `build.jpg` | A small build in progress: floor, walls, the hammer ghost |
| `bag.jpg` | The inventory open (HUD on) |
| `hotbar.jpg` | Gameplay with the HUD showing |
| `fire-forest.jpg` | Inside one of the four forests (any zone that looks best) |

Spare pictures are welcome: a crypt vault, a mine chamber, a felled tree, the mountains with snow tops.

## 6. After the run

Send the notes, the video timestamps and the screenshots. Problems found go on `Chopper/Docs/TASKBOARD.md`. The website gallery is filled only after you have picked the pictures.
