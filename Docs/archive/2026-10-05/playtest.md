# Chopper: playtest checklist (v0.2.9)

Last updated: 2026-10-05. The v0.2.3 checklist is in git history; the old long one is in `Docs/archive/2026-10-02/playtest.md`.

Run `Builds\Release\windows\Chopper.exe` and check that the start screen says **v0.2.9** in the bottom right corner. Write down anything broken, plus your top three annoyances.

## 00. The house works, building, zone music (new, 2026-10-05)

- **Workbench:** walk up to the table by the east wall of the house: "Workbench" over "[E] Use". It opens its own menu with Craft (torches, bandages), Upgrades, Forge and Repair. Walk away and it closes. Everything is made there except bandages.
- **Bandages anywhere:** in the Tab screen, Craft shows only the bandage, and in the Bag a healing herb or fiber has a "Make bandage" button. Away from the bench, a torch cannot be made.
- **Storage chest:** beside the bed, "[E] Open". Your bag on the left, the chest on the right; click (or A) moves a whole stack across. It holds everything you carry, wood included, it is the same chest for every player, and it is saved with the world.
- **Bed:** by day it says "Sleep at night". From dusk on, "[E] Sleep" lies you down: the screen darkens and "In bed 1/2" shows how many are in. When everyone is in bed the night is skipped to morning (the night's enemies wither, as at dawn). Moving, jumping or interact gets you up.
- **Tools wear out:** you start with the axe and a rod. The thin bar on hotbar slots 1 and 4 shows their state (green, yellow, red). Every hit wears the axe, every catch wears the rod and a snapped line wears it more. A broken axe leaves you with fists (the slot shows fists), a broken rod cannot cast. Repair both at the workbench (Repair page: 10 wood for the axe, 4 wood and 2 fiber for the rod).
- **Fish are food:** eating a fish now gives extra stamina for 5 minutes (a "Fed" square by the stamina bar) instead of health.
- **Quiet hub:** trees in the hub plant no saplings. Chop only at home and the night stays quiet; chop in a forest and it comes for you.
- **Building (Valheim style, first version):** press 6 (LB + D-pad right on a pad) to take out the hammer (slot 6 lights up). Block (right click, LT) opens the piece list: wood floor, wood wall, doorway, roof, stairs, workbench, standing torch, campfire. A green copy follows the crosshair on a 2 m grid; red means it cannot go there (the prompt says why: no workbench nearby, not enough wood). Attack places it, R or the wheel turns it, Interact on a placed piece takes it down and gives its wood back. Pieces need a workbench within 20 m; a workbench can be built anywhere. Enemies hit pieces in their way and break them; the Repair key at a workbench mends nearby pieces for wood. Pieces are saved with the world.
- **Music:** quieter by default (a quarter instead of a half). The hub has its own calm day music, each forest its own tune, the night its own, and a fight theme takes over while awake enemies are close. Until new tracks arrive these are the six AlkaKrab tracks, one per place (see requirements.md section 5).
- **Fishing words:** one word pops up and fades at the moments that matter: Hooked!, Caught!, Snapped, Got away, Missed. Nothing else stays on screen.
- **Low health:** no more red flashing. The screen edges shade a faint, steady red below 30 percent health.
- **Join requests on a pad:** when someone asks to join, the panel says "[Start] Answer"; the pause menu then shows Let in and Turn away buttons. Y and N still work on the keyboard.
- Check: does the hammer feel right (grid, turning, taking down)? Is the sleep skip too easy given the night is the core of the game? Does each zone's music fit?

## 0. Start screen and Chopper (redone)

- The main menu is just the logo and New Game, Join Game, Settings, Exit. No tagline, no Controls or Character entries.
- The controls are in Settings, on the Keyboard and mouse and Gamepad tabs.
- New Game opens **Choose Your World**: the world name, seed, New random seed, then Start.
- Join Game goes straight to **Join a Friend** after you choose it.
- Everyone plays the same fixed Chopper: shorter and broader than the animation-library mannequin, with his full dark-brown beard and mustache, short dark hair parted in the middle (no hat), warm skin, a red and black buffalo-plaid shirt with the sleeves rolled to the elbow, a brown belt with a gold buckle, blue jeans and brown boots. There is no character creator, outfit, hair, beard, color or Choppette selection.
- Does it still feel crude anywhere? Tell me which screen.

## 0a. Playing with a friend: join codes (new)

- Both players need Steam running. You do not have to be Steam friends. Steam shows you as playing "Spacewar" for now: that is Valve's test app, until Chopper has its own.
- Host: start or load a world. A note shows the **join code** (six letters and numbers, for example K7P2QX). It is also at the bottom of the pause menu, and **Copy join code** puts it on the clipboard to paste into a chat. Every hosted game gets a new code.
- Friend: Join Game on the start screen, type the code, press **Join** (or Enter). The loading screen says "Waiting for the host to let you in".
- Host: "Name wants to join" appears at the top of the screen. Press **Y** to let them in or **N** to turn them away. If you do not answer within a minute, they are turned away and told why.
- Steam friends who are already in a world still show in the list under the code box, and Invite friends on the pause menu still works. Those joins also wait for the host's Y.
- Nobody types an address or opens a port. All traffic goes through Steam's relays, so nobody sees the other's IP.
- Check: a wrong code says no world has it; a turned away friend lands back on the start screen with the reason; two friends asking at once are asked one after the other.
- How does it feel across Canada? Note any rubber banding: your own character, your friend's character, and the enemies.

## 0a2. The hut is a house now (new)

- The hut is a timber house you walk into through the doorway facing the yard. Inside: the bed, the storage chest, a fireplace, a hanging lantern, and the workbench (section 00).
- After you go down (once the hut is repaired) you come back beside the bed.
- A new world starts with the hut as a ruin (roof caved in on one side, walls knocked down, planks everywhere, boards across the door, a cold fireplace). Repairing it swaps in the whole house. There is no palisade: enemies walk straight to the house and hit its walls, and if they destroy it at night the run restarts with the hut a ruin again.
- The yard torches are now the same torch you carry, planted in the ground and scaled up.
- You can no longer see across the island: the fog is about twice as thick (the marsh thickest, the windy highlands clearest) and nothing past 600 m is drawn. From the hut the mountain ring still shows through a haze. Is it too thick or not enough?
- Check: can you walk round all the furniture, does the camera stay inside the room, and does the room read well at night (fireplace and lantern)?

## 0b. Saving and loading (new)

- New Game, step 2 now asks for a **world name** as well as the seed. Press Start: a loading screen shows the name, seed, a filling bar and a tip, then fades into the world.
- The world saves itself **at every dawn**. The pause menu has **Save game**, and **Save and quit to title** and **Save and quit game** save first.
- Back at the start screen there are now **Continue** (the most recent world) and **Load Game** (the list, with Delete that asks once more).
- Load a world and check that it comes back as it was:
  - the day;
  - the hut, its wood store, the storage chest and anything you built;
  - felled trees and planted saplings;
  - your wood, torches, bandages, herbs, fabric, crypt iron and shards;
  - how worn your axe and rod are, your upgrades and Sturdy level;
  - your position, and crypt chests you already emptied (they show as empty);
  - guardians you already killed stay dead.
- In co-op: a friend who joins again gets their own things back (saved by the host).
- Saves live in `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Chopper\Saves`.

## 0c. Sound and music (redone)

- No background sounds at all: no birds, crickets, wind, surf or frogs. You should only hear what you do and what attacks you.
- Every effect is new and cleaned up: axe hits and swings, footsteps on grass, dirt, water and crypt stone, tree falls, the hut being hit, wood, the block and parry, grunts, treant growls, the night horn and the menu clicks.
- Music: a storyteller theme on the start screen, a calm Celtic tune by day, and a darker piece at night. It goes quieter inside crypts. The Music slider in Settings controls it.
- Settings General no longer has an Ambience slider.
- The camera starts closer (3 m instead of 4 m). The scroll wheel zooms. On a pad, hold LB (L1) and press D-pad up to zoom in or down to zoom out; it stops at the closest and farthest instead of wrapping. D-pad up and down alone are still the axe and the rod, and tapping LB on its own still eats a fish (now when you let go).

## 0c3. Fighting groups, stones and fishing (new)

- Enemies fight as a group now: when several come at you, only two attack at a time. The others circle you, spread round your sides and back, and an enemy backs off after its swing. Do fights feel fairer and more readable? Are they still dangerous?
- Element stones: at the workbench, a stone of a crypt's element costs 4 of its essence and 3 rune shards. It enchants the axe with that element for good (no charges used). The Tab screen lists your stones.
- Crypt arenas come back every dawn, so their essence, shards, iron and gifts can be farmed.
- Fishing at the hub pond (cast, wait, strike, reel):
  - Everyone starts with a rod. Every arena boss also gives fishing gear: the next rod or lure, in turn.
  - On the bank press Interact or 4 (D-pad down on a pad) to take the rod out; you stand still while it is out. Jump, roll, raise your guard or press it again to stop.
  - Hold Attack to charge a cast (a ring on the water shows where it lands) and let go. The bobber nibbles now and then; that is not a bite. A real bite twitches twice, then pulls the bobber under with a splash and BITE!: press Attack then.
  - Hold Attack to reel, let go to ease off. The bar is the line's tension (Safe, Risky, Critical, with marks): keep it out of the red. The fish goes Calm, then Pulling, then Thrashing, then Tired, always in that order; "ease off!" warns before a thrash. Reel hard while it is tired.
  - Perch, pike, golden carp (fishing level 1) and the legendary Old Mossback (level 3). Catches give fishing experience; the Tab screen shows your level.
  - Two hotspots in the pond bubble and splash: cast into one for faster bites, rarer fish and more experience. Each gives a few fish, then rests a while; at night they are quiet.
  - Press 5 (LB on a pad) to eat a fish for extra stamina (smallest first).
  - Does a common fish take about 5 to 12 seconds, a pike 10 to 20, a golden carp 15 to 30? Is it readable the first time?
- Chopper wears no hat now: short dark hair parted in the middle, and his full beard.
- No hand holding: prompts say what and to what, never the key ("Pick up Wood" for a press, "Gather Fabric" for a hold, "Open Chest", "Store Wood", "Fish", "Reel"). No control hints along the bottom while playing, and the How to play screen no longer pops up when you take the axe (it is still on the guide sign).
- The Tab screen has pages: Character, Bag, Craft (bandages only) and Fishing. Page with LB and RB (Q and E on the keyboard) or click a tab. The Bag is a grid of what you carry only; pick an item to see it and use it (eat a fish, wrap a bandage, light a torch, make a bandage). Upgrades, the forge and repairs are in the workbench menu.
- The hotbar no longer shows a fish with a 0: empty slots stay empty, and used up items fade without a number.
- Weather: sunny, cloudy and rain come and go every few minutes (rain always through cloud first). Clouds dim the sun and grey the sky; rain falls around you, thickens the fog and makes fish bite a little sooner. No rain sound, since there is no background ambience. Crypts are untouched.
- Performance: the stutter and the lag came from the HUD naming keys every frame (over half a megabyte of garbage a frame). Is play smooth now, in the editor and in a build?

## 0c2. Enemies and the yard (fixed)

- Crypt guardians and arena draugr were invisible in builds (only their swords showed). They now show, glowing in their element's color, and walk and swing instead of standing in a T-pose.
- A tree enemy you kill now topples away from your blow and sinks into the ground instead of vanishing.
- The torch posts in the yard carry an iron fire basket, so by day they read as torches.
- Trees keep further back from crypt doors, so the entrances are not buried in leaves.

## 0d. Mountains between the zones (new, replaces the palisade gateways)

- The hub is ringed by steep mountains. Each path leaves the hub through a narrow gorge: Water north, Earth east, Fire south and Wind west.
- High ridges run from the ring out into the sea along the diagonals, so no forest can be seen from the next one or from the hub (except down a gorge).
- Try to climb any mountainside, by walking or by jumping: you should slide back down. Try to swim round the end of a ridge: it should be blocked.
- Enemies keep to their own forest and the hub. Lead one into another forest through the hub: it should give up the chase and head for the hut through its own gorge.
- On the map the mountains show as grey stone.

## 0e. Crypt arena (new)

- In each crypt's deepest room (the vault, with the grand chest) a dark archway framed by stone says "Go down to the arena".
- Below is a big pillared hall. Three waves of the crypt's guardians come (the top left line says "Wave 1 of 3" and so on), then the crypt's boss rises.
- Felling the boss gives everyone in the arena 3 essence of that crypt's element, 2 rune shards and 2 crypt iron. The arena stays quiet afterwards ("The arena lies quiet").
- If everyone leaves or falls, the arena resets and can be tried again.
- At the workbench (its Forge page) there are ward rows for this world's three crypt elements. A ward makes that element's enemies do 25% less damage, then 50%.
- Is it hard enough with one player, and with two?

## 0f. Signs and screen text (changed)

- The board by the hut is now a signpost with a blank page pinned to it. Walk up and press interact ("Read the notice") to read it.
- No big text in the middle of the screen any more (no "The hut is repaired!" and similar).
- No objective text in the top left. The current goal ("Find the hut", "Repair the hut 4/10", "Wave 2/3" in an arena) is at the top of the Tab screen.
- Prompts are one word: [E] Enter, Open, Read, Take, Pick up, Climb, Descend.
- Fewer notes on the left: only Parry, Saved, repairs and similar.
- The start screen has a Credits page (music, sound, art) instead of a line in the corner.

## 0f. Crypts (flicker)

- Walk through every crypt doorway and corridor with the camera zoomed in and out. The camera no longer pops in and out against the walls. Does anything still flicker? A screenshot or a short clip would help. In the editor, run Rebuild Network Scenes first.

## 1. Picking herbs

- Hold interact next to a red herb. Instead of a number, a thin gold bar under "Picking herb" fills up, and the herb is yours when it is full.

## 1a. Fabric and bandages (new)

- Small leafy bushes give **fabric** when you hold interact. They use the same filling bar as herbs, but the pickup card says Fabric.
- Anywhere (Tab, Craft, or the Bag), make a **Bandage** from 2 herbs and 1 fabric. Using it restores health.
- Save, return to the title screen, then load the world. Your fabric and bandages should still be there.

## 2. Falling trees (changed again)

- The first large tree you fell in a world deliberately comes down toward you. Move clear during the fall unless you want to test the crushing rule. Save and load afterwards: the next large tree should return to the normal rules.
- Nothing leans a tree to hint at its fall. Oaks grow with a bend in their trunk, but that is just the art and says nothing about where it will drop. On level ground it falls away from the last blow. On a slope it comes down the hill, so a tree chopped from below can come back down on the chopper.
- No "Timber!" text any more.

- A large tree that lands on you now **kills you outright**, unless you wear Sturdy gear. Blocking and wood armor do not help.
- The death screen says so and points to the crypts.
- With one level of Sturdy gear you survive at about a fifth of your health; with three levels a falling tree is a heavy hit.
- Medium and small trees still never hurt.

## 3. Crypts (new)

- Every world has **three crypt entrances**, small stone tombs with two lanterns, each in a different forest and past the hub. The door faces the hut.
- Walk up to the door: "Enter the Ash Crypt" (or Drowned, Root, Gale). Press interact: the screen fades and you are in a lit stone room at the foot of a staircase.
- Inside:
  - rooms joined by short corridors, lit by orange lanterns;
  - no wind, birds or grass sounds, and no swimming, even under a pond;
  - the minimap shows the crypt's name instead of the zone.
- **Guardians**: treants of the crypt's element wake the first time anyone goes down. They stay in the crypt, do not wither at dawn, and walk back to their post if you leave.
- **Chests**: a glow over each unopened chest. Press interact for crypt iron and rune shards, with a card on the left. The deepest room has a bigger grand chest.
  - In co-op each player loots every chest once for themselves.
- At the foot of the stairs: "Climb back up" takes you out in front of the door.
- The world map shows a grey door icon for each crypt you have walked near.

## 4. Crypt upgrades at the workbench (its Forge page)

- **Sturdy gear** (crypt iron: 4, 6, 10). Each level makes a falling tree hurt less (see section 2).
- **Rune axe** (rune shards: 3, 5, 7). +15%, +30%, +50% axe damage on top of the wood upgrades.
- The three crypts together hold enough for both upgrades to reach the top level.
- The character sheet lists your iron, shards and Sturdy level.

## 5. Still to check from v0.2.3

- The character in game and in the creator: is anything missing under the clothing? Send a screenshot if so.
- Blocking: both hands on the axe, nothing spins while walking and turning.
- Frame rate in a big meadow at dusk with the torch on.
