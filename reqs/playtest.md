# Chopper: playtest checklist (v0.2.9)

Last updated: 2026-10-05. Rewritten from scratch: the old checklist described holds, progress bars and prompts that are gone. It is kept in `Docs/archive/2026-10-05/playtest.md`.

Run `Builds\Release\windows\Chopper.exe` (not the editor) and check that the start screen says **v0.2.9** in the bottom right corner. Write down anything broken, plus your top three annoyances. To record, see "Recording" at the end.

## 1. Start screen and saves

- The menu: Continue and Load Game (when a world exists), New Game, Join Game, Settings, Exit. Credits has its own page.
- New Game: a world name and a seed (random or typed), then Start. A loading screen shows the name, seed and a tip.
- Load Game lists worlds, most recent first. **Delete is a hold:** the whole button fills red from left to right while you hold it, and the world is gone when it is full. Let go early and it drains back.
- A world saves at every dawn, from the pause menu, and when the host quits. Load one and check it comes back: the day, the hut, the shared chest, anything you built, felled trees, saplings, your items, how worn your axe and rod are, upgrades, your position.

## 2. The first minutes

- You wake outside the ruined hut with the axe and a fishing rod. The ruin: caved roof, knocked down walls, planks, a cold fireplace.
- Branches lie around it. Interact knocks one loose; walk over it to take it. Gather wood (or chop) and press interact at the ruin until it is rebuilt: the whole house swaps in.
- The notice board in the yard holds the guide (How to play).

## 3. The house

- **Workbench** (by the east wall): "Workbench" over "[E] Use". Its own menu with Craft, Upgrades, Forge and Repair, paged with Q and E (LB and RB). Walk away and it closes.
- **Crafting:** torches only at a workbench. Bandages anywhere: the Tab screen's Craft page, or a healing herb or fiber in the Bag ("Make bandage").
- **Storage chest** (beside the bed): "[E] Open". Bag on the left, chest on the right; a click or A moves a whole stack. One chest for everyone, saved with the world.
- **Bed:** by day it says "Sleep at night". From dusk, "[E] Sleep": the screen darkens and "In bed 1/2" counts. When everyone is in bed, the night passes. Moving, jumping or interact gets you up.
- After dying (once the hut is rebuilt) you come back beside the bed.

## 4. Tools, food and status

- **Chopping:** a small tree takes 4 swings, a medium 6, a large 9 with the starting axe, and gives 3, 6 or 12 wood. An upgraded axe needs fewer swings (level 3: a large tree in 5). Tell me if it feels too slow or still too quick.

- Hotbar slots 1 (axe) and 4 (rod) have a thin bar: green, yellow, red. Every hit wears the axe, every catch the rod, a snapped line more. A broken axe leaves fists; a broken rod cannot cast. Repair at the workbench (Repair page).
- **Pad hotbar:** D-pad left and right move a gold rim along the hotbar (it wraps at the ends); D-pad up takes out what is under it, or puts it away if it is already out. D-pad down zooms the camera out, and from the farthest back in. No LB combos. On the keyboard the hotbar is still 1 to 6.
- **Unequip:** H (R3 on a pad) puts away whatever is in your hands: axe on the back, torch out, rod in, hammer away. Press it again with empty hands and the axe comes back out. Crouch on a pad is now LB.
- Eating a fish (5, or the fish slot on a pad) gives extra stamina for 5 minutes.
- Low health shades the screen edges a faint steady red. Nothing flashes or pulses.
- Known stand-ins (see requirements.md): the Wet and Fed squares by the stamina bar, the hotbar icons, the colored element squares. They will be replaced by real art.

## 5. Building

- 6 (on a pad: the hammer slot on the hotbar) takes out the hammer; slot 6 lights up. Block (right click, LT) opens the piece list.
- A green copy follows the crosshair on a 2 m grid; red means it cannot go there, and the prompt says why (no workbench nearby, not enough wood).
- Attack places, R or the wheel turns, Interact on a piece takes it down for its wood back.
- Pieces need a workbench within 20 m; a workbench can be built anywhere.
- **Support:** pieces hold each other up, up to 8 pieces away from the ground (two storeys and a roof). Further out the piece shows red with "Too far from the ground". Take a lower piece away and what it held comes down (you get its wood). A damaged piece gives back wood by how whole it is.
- Enemies hit pieces in their way. R at a workbench mends nearby pieces for wood.
- Check: floors, walls meeting at corners, a doorway, a gable roof (two rows facing each other), stairs, a torch that lights at night, a campfire that dries you off.

## 6. Day, night and the forests

- Trees in the hub plant no saplings: stay home and the night is quiet. Fell trees in a forest and saplings wake at night and come for you, then what you built, then the hut.
- Kill an elemental enemy and the axe carries its element for 15 hits.
- From day 3, too many trees felled in one grove raise its warden.
- Big trees kill where they land unless you wear Sturdy gear from crypt iron.

## 7. Fishing

- On a shore, interact or 4 takes out the rod. Hold Attack to charge a cast, let go to throw. You can see the fish. Tap to twitch the lure, hold to reel slowly.
- A bite pulls the float under: strike, then hold to reel and ease off when the bar gets red.
- One word pops up and fades: Hooked!, Caught!, Snapped, Got away, Missed. Nothing else stays on screen.

## 8. Crypts and arenas

- Three crypts per world, one each in three forests. Guardians, chests of crypt iron and rune shards, and the arena below the vault (three waves, then a boss, for essence).
- The workbench Forge page spends them: Sturdy gear, the Rune axe, wards and element stones.

## 9. Music and sound

- Music is a quarter volume by default. The hub has its own calm day music, each forest its own, the night its own, and a fight theme while awake enemies are close. Until new tracks arrive they are the six AlkaKrab tracks.
- No background ambience on purpose.

## 10. Playing together

- Host: a join code shows as a note and in the pause menu (Copy join code). A friend types it on Join Game. "Name wants to join": Y or N on the keyboard; on a pad press Start, then Let in or Turn away.
- Untested so far between two real Steam accounts. Note any rubber banding: you, your friend, the enemies.

## 11. Settings

- Graphics has a **Frame rate cap** (None, 30, 60, 120, 144). It overrides vertical sync while set.
- Keyboard and Gamepad tabs list every action, including Build (6). On the Gamepad tab the six hotbar items say Hotbar and cannot be rebound.

## Recording

The exe holds a steady 60 fps at 1440p in every place the performance tour visits (no frame over 25 ms, GPU about 12 ms at the hut). Stutter in a recording usually comes from the recording, not the game:

- Record the exe, never the editor's Game view (the editor draws the game and the editor together, and capture tools sample it unevenly).
- Make the game's frame rate match the recording: Settings, Graphics, **Frame rate cap 60** for a 60 fps recording. On a 144 Hz screen with vertical sync and no cap the game makes 144 frames a second, and a 60 fps recording keeps an uneven 2 or 3 of every 5, which reads as skipping.
- In OBS use Game Capture (not Display Capture), the GPU encoder (NVENC on NVIDIA), and the same 60 fps.
- F5 opens the console; `fps` shows frames per second and the slowest frame in the bottom right, so you can see whether the game itself dips while you record.
