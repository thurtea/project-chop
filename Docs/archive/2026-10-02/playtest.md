# Chopper: playtest guide

Use this list for your next session. Report anything that fails or feels wrong, with a screenshot if you can.

## Before you start

### The exe (the version to playtest)

Windows: launch `E:\Projects\3D-Game\Chopper\Builds\Release\windows\Chopper.exe` (or unzip `Builds\Release\Chopper-0.1.0-windows.zip`, which also has `PLAYTEST_README.txt`). It opens borderless fullscreen at your desktop resolution. On the start screen pick New Game. Settings has a Display button to switch between Fullscreen and Windowed; the choice is remembered. Alt+F4 closes the game.

Linux (Fedora) and macOS: not built yet, because the Unity build support modules are not installed. Run `unity install-modules -e 6000.5.10f1 -m linux-mono mac-mono --accept-eula -y` in a terminal (approve the admin prompt), then tell me, or run `Chopper > Build > All Platforms (Release)` yourself and `python Tools/package_release.py Builds/Release 0.1.0`. See `requirements.md` and `.github/workflows/release.yml` for automatic GitHub builds.

To make a fresh Windows exe after changes: `Chopper > Setup > Rebuild Network Scenes`, then `Chopper > Build > Windows Playtest Player`.

### In the editor

1. Open `E:\Projects\3D-Game\Chopper` in Unity 6000.5.10f1.
2. Run `Chopper > Setup > Rebuild Network Scenes`. This is required after every update. Wait for it to finish (a minute or two the first time).
3. Press Play. The start screen appears; choose New Game. (To skip it and host instantly, turn on `Chopper > Play > Auto Host On Play`.)
4. Watch the Console for red errors during the first minute. Copy any you see.

## v0.2.0. New in this round: grass, sea, branches, sound, menus

Check the version in the bottom right of the start screen says v0.2.0 on each machine (Windows, Fedora, Mac).
- Menus: no gold frames or lines; buttons are text that glows when the mouse or pad is on them, with a soft tick, and a click sound. Settings has a Music volume slider (silent until music files exist).
- Grass: a dense carpet that sways in waves of wind and parts around you and enemies. Lush green in the hub, dark in the marsh, golden on the highlands, almost none on the ash. Note the frame rate in a big meadow at night with the torch; say if it stutters.
- The world edge: walk to any edge. There is a beach and then the sea (you can wade and swim in it), no mountain wall. Surf gets louder as you near the shore.
- Branches: sticks lie in the grass, a few right outside the gate. Interact next to one: +1 wood. Can you repair the hut from branches alone? They come back at dawn.
- Footsteps sound different on grass, on paths and in water.
- Dusk and dawn have a golden haze in the air; noon is clear.

## 00000000. New in this round: healing herbs

- [ ] Small red flowers grow around the world. Stand next to one: "[E] pick the healing herb" appears, E picks it, "+1 healing herb" shows, it vanishes, and it is back after dawn.
- [ ] Tab shows "Healing herbs n / 20". At the workbench the Healing potion costs 3 herbs (not wood); the Torch still costs wood. The hotbar slot 3 is now called Potion.
- [ ] Tell me if there are too few or too many herbs, or if they are hard to spot.

## 0000000. New in this round: saplings, the element loop, multiplayer

- [ ] Fell a small tree: a green message says 1 to 2 saplings were planted, with small sprouts and a ground burst. Medium 2 to 3, large 3 to 4. The top bar shows "Saplings planted: N (E elemental)".
- [ ] In the Water forest roughly half the saplings are blue (Water) and half plain brown; in Fire half red, and so on. The hub gives plain ones only.
- [ ] At night, kill a Water enemy: your axe frame turns blue, 15 hits. Then fight a Fire enemy: it dies much faster and "Super effective against Fire!" appears. Also confirm any enchantment helps against plain enemies a bit.
- [ ] Restless nights wake the elemental ones first; quiet nights the plain ones. Tell me if a night is ever all one kind and boring.
- [ ] Two player test: one hosts, another joins by address (or run two copies on one PC: second one Join 127.0.0.1). Check you see each other move, chop, block and roll, see the same outfits, the same felled trees and saplings, the same hut and night.
- [ ] No ambient particles drifting across the screen (they are off by default).

## 000000. New in this round: combat, water, developer tools

- [ ] Block: hold right mouse (LB on a pad). A shield shows on your left arm, you slow down and damage drops by 75 percent. A block raised just before a hit parries it (spark, "Parry!", the enemy is stunned). Stamina drains while guarding and per blocked hit; run out and your guard breaks for a moment. You cannot swing while blocking.
- [ ] Roll: Q or left Alt (right stick click on a pad). A quick dash with a short untouchable window, costs stamina. Dodge an enemy swing by rolling as its arms rise.
- [ ] Enemies telegraph: they rear back for about 0.7 seconds before a hit lands. A hit that does not kill stuns and shoves them, with a flash and bark chips. Their damage is lower and slower than before. Tell me if it is still too hard or now too easy.
- [ ] The axe does twice the damage (small and medium trees die in one hit, large in two). After you kill ANY elemental enemy the axe is enchanted: +50 percent damage against everything for 15 hits, and a further +50 percent against the element it beats.
- [ ] Water: ponds and marsh pools are translucent with moving ripples and end where the ground rises. Walking in slows you a little; deep water makes you swim (swim animation, no sinking, no standing on the bottom, no rolling or blocking). The water sounds are silent on purpose until real recordings are added.
- [ ] Ambient zone particles are OFF by default. Settings has an "Ambient particles" switch if you want to see them. Combat sparks and chips are always on.
- [ ] Settings has "Look: Smooth / Pixel" (Pixel renders at low resolution with hard pixel edges).
- [ ] Developer tools: press F1 (host only) for skip to night or day, god mode (no damage, endless stamina), +100 wood, give axe, heal, repair, spawn enemies or a boss, random boon.

## 00000. Linux and macOS builds (for you)

Install the modules once: `unity install-modules -e 6000.5.10f1 -m linux-mono mac-mono --accept-eula -y` (approve the admin prompt), then in the editor `Chopper > Build > All Platforms (Release)` and `python Tools/package_release.py Builds/Release 0.1.0`. Full steps are in `summary.md`. Test the zips on your Fedora machine and Mac.

## 0000. New in this round: the opening loop

- [ ] You wake up at a random place in the world, not at the hut, with an objective telling you to find the hut. Restart a few times: the spot should differ. Tell me if you ever start in water, stuck, or next to the hut.
- [ ] The hut stands on a low hill inside a palisade of round logs, with a proper house (plaster walls, door, windows, tiled roof). Tell me how it looks and reads from far away.
- [ ] You spawn OUTSIDE the palisade with only your fists (hotbar slot 1 says Fists, no axe in hand). The hut looks darker and worn and the gate does not open. An Objective line at the top left tells you what to do next.
- [ ] Punch trees (hold left click): three punches fell a small tree and give wood. Tell me if punching feels too slow or too fast.
- [ ] Stand at the gate and press E with wood: "Hut repair: x / 10 wood". At 10 the hut is repaired, a banner says so, the gate now opens as you walk up.
- [ ] Inside the yard there is a table with the axe and a notice board. Press E at the table to take the axe: it appears in your hand and the How to play screen opens. Close it with Got it or Esc.
- [ ] E at the notice board (with no wood in hand) shows the instructions again. The Esc menu has How to play too.
- [ ] Without the axe you cannot charge it, and the R key reminds you to fix the hut first.
- [ ] Nights differ: the dusk and night banner says whether many or few trees are elemental tonight (restless night or quiet night). Tell me if a bad night is too hard or a good night too easy.
- [ ] Boss trees no longer appear at random. After day 3 and about 40 trees felled, a banner says a boss tree has started to grow in one forest. Chop it when it is grown (it takes many hits, easier with friends), then beat the boss at night: everyone gets a permanent boon (axe, outfit, torch, salve or stamina) and the banner names it. The inventory lists your boons.
- [ ] After a game over the hut is broken again (the gate shuts) but you keep your axe and boons.

## 000. New in this round: look, character, start screen, zones, night

- [ ] Look: the world should feel moodier and less cartoony than before (muted colors, textured ground with detail up close, warm sun and cool shadows, film grain, contact shadows under trees). No glossy pale bands along the ground. Tell me what still looks like stock art, and use `requirements.md` for what to add.
- [ ] Character: your player is a bearded man in a peasant outfit (shirt, belt, trousers, boots), no longer the plain mannequin. He walks, runs, crouches, chops and falls with the same animations, and holds the axe and torch. Other players see your outfit too.
- [ ] Start screen, Character: pick outfit (Lumberjack, Woodsman, Scout, Ranger, Trapper), shirt color (includes red flannel), hair, hair color, beard, skin tone, or Randomize. The preview turns and updates at once. Your choice is saved and used in the next game. Tell me if any combination clips (for example a hairstyle through a hood) or looks wrong.
- [ ] Start screen: New Game, Join Game, Settings, Exit. Settings has volume, mouse sensitivity, field of view and Display (Fullscreen or Windowed). Exit closes the game.
- [ ] Tree colors: Water forest blue, Fire forest red, Earth forest brown, Wind forest green, hub trees natural green.
- [ ] Enemy trees at night: elemental ones use their element color and glowing eyes; plain ones are brown with no glow. Only elemental kills charge your axe.
- [ ] Each zone has its own ground color, fog and drifting particles (embers in Fire, mist and fireflies in Water, spores in Earth, blowing leaves in Wind).
- [ ] The day is shorter (5 min 30 s) so night comes sooner.
- [ ] At night the nearest enemy charges you before it goes for walls or the hut. Enemies far away run faster so they still arrive in time. Stand outside the wall at night and you should get attacked first. Tell me if nights still end before you meet an enemy.
- [ ] The morning sun is higher and ambient light brighter. Tell me if any zone still looks too dark or has a pale glossy band along the ground edges.

## Controls

| Action | Key |
|---|---|
| Move | WASD or arrow keys (relative to the camera) |
| Look | Mouse (always, cursor is locked) |
| Zoom | Scroll wheel |
| Jump | Space |
| Sprint | Shift (press to toggle on, again to toggle off) |
| Crouch | Ctrl (press to toggle) |
| Chop or attack | Hold left click |
| Deposit wood and repair the hut | Press E near the hut (hold E for herbs) |
| Inventory and hut workbench (craft, upgrade) | Tab |
| World map | M |
| Pause menu (settings, disconnect) | Esc |
| Torch on or off / use healing salve | 2 / 3 |
| Dev: skip to the next day or night phase | F10 |
| Dev: drop saplings around you | F7 |
| Dev: give yourself 40 wood | F6 |

## 00. New in this round: world, walls, lives, controller

World (rebuild the scenes first; the world is now 640 m across):
- [ ] The hub meadow around the hut is large and open, with scattered trees, rocks and grass. There are NO yellow, red or pink flowers and no bushes.
- [ ] Dirt paths lead from the hut gate out to each of the four forests.
- [ ] The four forests (north Water, east Earth, south Fire, west Wind) start far enough out to feel like a trip. Tell me if the scale feels right for a Valheim meadow, too big or too small.

Hut, walls and repair:
- [ ] A wooden palisade ring surrounds the hut. The north side is a gate that sinks when you walk up to it and rises again after you pass.
- [ ] Press E (X on a controller) near the hut to deposit wood. Press R (RB) to repair walls and hut. A message shows how much was repaired.
- [ ] At night enemies chase the nearest player first, then hit the palisade, and only hit the hut once a wall segment is gone. The hut should NOT be destroyed before enemies come for you.
- [ ] A broken wall segment disappears and enemies can walk through the gap. R rebuilds it.

Lives and combat:
- [ ] Lives are infinite: when you go down you respawn at the hut after 12 seconds, as many times as it takes. The respawn wait is the only cost, and only the hut being destroyed ends the run. (No life dots are shown in this mode.)
- [ ] You start with 1 torch and 1 healing salve.
- [ ] Saplings on the ground cannot be hit. Your axe passes through them and hits the tree behind. They wake up as enemies at night.
- [ ] Chopping trees does not use stamina; swinging at an enemy does. Sprinting uses much less than before.
- [ ] Axe sounds: a real swing whoosh on each swing and a real impact on each hit of a tree, enemy or wall.
- [ ] The camera no longer ends up inside the hut or walls.

Xbox controller (plug one in):
- [ ] Left stick move, right stick look, D-pad up and down zoom, A jump, B toggle crouch, click left stick to toggle sprint.
- [ ] RT hold chops, LT torch, LB salve, X hold deposit, RB repair, Y inventory and workbench, Back map, Start menu.
- [ ] In menus the stick or D-pad moves between buttons, A selects, B goes back.
- [ ] The hints on screen (hotbar labels, hut prompt, bottom left text) switch to controller button names when you use the pad.

## 0. The new interface (added last round)

- [ ] Nothing is drawn in the top right corner. Health and stamina are bottom left, the hotbar bottom center, the clock top center, the minimap bottom right.
- [ ] Minimap: a round map with you in the middle, north at the top, dark where unexplored. It reveals as you walk. The gold square is the hut, colored dots mark the forest entrances, red dots are awake enemies.
- [ ] M opens the full map with the forest names. M or Esc closes it.
- [ ] Esc opens the pause menu. Try Settings (volume, mouse sensitivity, field of view change things right away and are remembered next time), Controls, Disconnect (goes back to the title) and Resume. The mouse is free while any menu is open and the game does not react to clicks.
- [ ] Tab opens Inventory. Left side shows your stats and items, right side is the hut workbench. Walk to the hut and craft a torch (3 wood) and a healing salve (5 wood), and buy axe, armor and hut upgrades with the buttons (they use the wood stored in the hut). Away from the hut the buttons are greyed out.
- [ ] Press 2 to light or put out the torch (held in the left hand, lights the night around you, burns 4 minutes). Press 3 to drink a salve when hurt.
- [ ] Pickup messages appear on the left ("+8 wood"), the screen flashes red when you are hit, and a dark red screen shows while you are down.
- [ ] Tell me if anything is hard to read, too small, too big or in the wrong place.

## 1. Character and movement (the last thing that was broken)

- [ ] The character is NOT in a T-pose. It idles, then walks and jogs when you move.
- [ ] Axe is in the right hand and looks reasonable. Note if it points the wrong way or clips through the body.
- [ ] Left click plays a chop with the upper body while the legs keep moving.
- [ ] Shift toggles sprint: faster, sprint animation, stops when you stop or crouch.
- [ ] Ctrl toggles crouch: press once to crouch (slower, lower camera, crouch animation, quieter footsteps), press again to stand. Sprinting also stands you up.
- [ ] You can jump and the jump animation plays.
- [ ] Camera: mouse always looks, scroll zooms in and out smoothly, the camera trails a little behind when you run. Tell me if the follow is too loose or too tight, or if zoom steps feel wrong.
- [ ] Stamina: the yellow bar drains while sprinting and from each swing and jump, refills after a pause, and when it hits zero you cannot sprint or swing until it recovers a bit.

## 2. Sound

Turn your volume up. Listen for each of these:

- [ ] Footsteps (faster when sprinting, quieter when crouching)
- [ ] Axe swing whoosh, chop thump when a tree is hit, a longer rumble when a tree falls
- [ ] Wood pickup blip when a tree falls, knock sound when you deposit at the hut
- [ ] Upgrade chime when you buy something
- [ ] Wind in the background by day, crickets at night
- [ ] Horn when night begins, birds at dawn
- [ ] Enemy growl and death crackle, hut thump when it is hit, grunt when you are hurt

Note anything missing, too loud, too quiet or annoying. All sounds are synthesized placeholders for now.

## 3. Lighting, zones and grass

- [ ] Walk through grass: it should lean away from you and spring back.
- [ ] The four forests look clearly different: Water (north, blue-teal), Earth (east, olive), Fire (south, orange leaves, red ground), Wind (west, pale). The zone name in the top right panel changes when you cross in.
- [ ] Marker pillars with glowing orbs are visible on the path into each forest.
- [ ] Shadows look right at distance, nothing flickers or looks blown out. Tell me if the overall look is too green, too teal or too washed out.
- [ ] The pond and hub look fine.

## 4. Day, night and enemies (you did not see this last time)

The day lasts 5 and a half minutes, so use F10 to skip ahead. The big clock at the top of the screen shows the time to nightfall.

1. Chop some trees in a forest so saplings drop (small trees standing on the ground).
2. Press F10 once (dusk, banner appears), then F10 again (night). The screen should darken and turn blue, a horn plays, and a banner says the saplings are awake.
3. [ ] Saplings wake up as glowing-eyed tree enemies and walk toward the hut. Enemies from all four directions come if you chopped in all four.
4. [ ] They attack the hut (hut health drops) and you (health drops, you get knocked down at zero and respawn at the hut after 8 seconds).
5. [ ] Hitting an enemy: small takes 1 hit, medium 2, large 3.
6. [ ] Kill an elemental enemy (colored eyes): your axe glows in that element color for 15 hits. A charged axe does more damage to the enemy it beats. Fire beats Wind, Wind beats Earth, Earth beats Water, Water beats Fire. The top right panel shows the hits left.
7. [ ] Press F10 to reach dawn: enemies wither, the axe charge clears, you heal, felled trees regrow.

Also check: can you actually see enemies at night, or is it too dark? Is a four direction night too hard, too easy, or fun?

## 5. Wood, hut, crafting and upgrades

- [ ] Felled small, medium and large trees give 2, 4 and 8 wood. The panel shows wood carried (cap 40).
- [ ] Press E near the hut: wood moves to the store and repairs it.
- [ ] Press F6 for wood, deposit it at the hut (press E), then open Tab and buy upgrades. Each item goes to level 3. Check the axe is faster and hits harder, armor reduces damage, the hut gets more health.
- [ ] If the hut hits zero, the run restarts at day one.

## 6. Boss (optional, slow)

Boss saplings are rare (1 in 100). To test quickly, ask me for a debug key. Otherwise just watch for a large sapling that ignores your axe until it is fully grown (2 game days).

## 7. Multiplayer (optional)

- [ ] Open Multiplayer Play Mode, add a second player, turn off `Chopper > Play > Auto Host On Play`, host in the main editor and Join 127.0.0.1 in the second.
- [ ] Each player sees the other walk, crouch and chop with animation, and sees trees fall for both.

## What to send me after testing

1. Anything on this list that failed, with the exact step.
2. Any red errors from the Console.
3. Your top three things that feel worst (visuals, controls, difficulty, sound) so I can fix those first.

## Controls and shield checks

- [ ] Esc, Controls: click Roll, press the mouse thumb button; the row shows "Mouse 5 (forward thumb)". Close the menu, the thumb button rolls.
- [ ] Bind Roll to a key already used (say Block's): the two swap. Reset to defaults restores everything. Restart the game: bindings are remembered.
- [ ] Hold right click (block) and press Space: you roll and do not jump. With nothing held the roll hops backward; with W held it rolls forward.
- [ ] Press Esc while "Press a key..." shows: the binding is cancelled and the menu stays open.
- [ ] Hut prompts show your bound interact key. Holding it next to a red herb shows a percent and picks it; tapping does not.
- [ ] Block: the round wooden shield appears on the left arm, face forward, readable from the front.

## Combo and shield checks

- [ ] Hold left click against an enemy or into the air: the swings alternate slash, backhand, then a heavy finisher with a small lunge. Chopping a tree stays the same chop every time.
- [ ] The finisher staggers an enemy and hits clearly harder. Pausing for over a second restarts the combo at the slash.
- [ ] With bare fists the punches alternate jab and cross.
- [ ] Buy armor upgrades at the workbench: each level changes the shield you hold up (wooden round, round, heater, gilded).
