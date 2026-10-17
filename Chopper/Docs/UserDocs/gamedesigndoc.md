# Chopper: game design document

Status: rewritten 2026-10-17 from the code on `main` (commit ec8e676 plus the 10-17 afternoon work). It describes the game as it is built.
Each section marks what is **Built** (in the game and checked by a tour or test), **Approved** (agreed with the owner, not built yet) or **Idea** (not agreed).
Numbers are defaults from `Assets/_Chopper/Config/SO_GameConfig.asset` (`Core/GameConfig.cs`) and the rules classes in `Core/`. When this file and the code disagree, the code wins; fix this file.

## 1. Summary

| | |
|---|---|
| Title | Chopper |
| Genre | Co-op survival action game: chop, fight, fish, mine, build |
| Players | 1 to 4 in co-op (the Steam lobby allows up to 8; difficulty scaling counts up to 5) |
| Platform | Windows PC, Steam. Keyboard and mouse, or a gamepad |
| Engine | Unity 6000.5.10f1, URP, FishNet networking (host and play, server authoritative), Steamworks lobbies |
| Look | Chunky voxel-adjacent low poly with bevels, flat hand-painted textures, soft haze, warm/cool grade (`art-direction.md`) |
| Camera | Third person, over the shoulder |

**Pitch.** You are Chopper, a ginger-bearded lumberjack dropped into a pond in the middle of four elemental forests. Every tree you fell pays wood, but its saplings wake at night as tree enemies that come for you and your hut. Chop by day, defend by night, and grow strong enough to clear the crypts and beat the grove wardens.

**Pillars**
1. **The axe is everything.** Chopping is the main verb. It feeds wood, the hut, crafting and the night's danger.
2. **You cause your own night.** Hub raids come only from trees you felled. No chopping, no raid.
3. **Readable melee.** Enemies telegraph their attacks. You block, parry, roll and use the elemental counter.
4. **Calm pastimes between fights.** Fishing, mining and the boat are rhythm/arc mini-games with their own collections.
5. **Co-op first.** Shared hut store, shared chest, revive by respawn, enemy counts scale with players.

## 2. Core loop

**Built**
1. Day: leave the hub with the axe and fell trees in the forests. Each tree pays a third of its wood when it falls; chopping up the log pays the rest plus a bonus. Stumps regrow at dawn unless cleared.
2. Felled forest trees plant saplings around the stump (by day only).
3. Bring wood home: deposit it in the hut store (shared), craft at the workbench, upgrade the axe, armour and hut.
4. Dusk then night: saplings wake as tree enemies and march on the players and the hut. Fight them at the hut, or sleep through the night if everyone is in bed.
5. Dawn: survivors root again, element charges reset, stumps regrow, crypt arenas re-arm.
6. Between runs: fish, mine, go to sea, explore crypts, gather herbs and fiber.

**Lose condition (Built):** the hut reaches 0 health. The run is over and the world restarts (EnemyDirector).

## 3. The world

**Built.** Generated from a seed (deterministic on every peer). Voxel terrain made of chunk meshes with a bevel, plus voxel props.

| Area | What is there |
|---|---|
| **Hub** (Neutral) | The hut, the stepped pond (arrival point and fishing), campfires, stone paths, pines. Protected: nobody can build here, and hub trees plant no saplings. |
| **Water forest** | Water-element trees and enemies, marsh water, its crypt, mine portals |
| **Fire forest** | Fire-element trees and enemies, its crypt |
| **Earth forest** | Earth-element trees and enemies, its crypt |
| **Wind forest** | Wind-element trees and enemies, its crypt |
| **Ocean** | Around the land; the boat and the shark |
| **Mines** | Under the world, reached by portal carts; veins and finds |
| **Crypts** | One per element, entered by a door; rooms, chests, and an arena under the vault |

- Zones colour the fog (ZoneTracker) and the palette (ZonePalette). Each forest differs mainly in colour.
- Weather: Sunny, Cloudy, Rain, Snow (WeatherRules). Rain makes you wet. Snow is visual (no slide).
- Day/night cycle: Day, Dusk, Night, Dawn (DayNightCycle). Moonlight and fire light at night. Atmosphere slider (default 0.6, 0 indoors).
- **Tutorial / practice yard (Built):** fixed seed 2026, never saved, clock held at day, signs at 8 stations (Welcome, Chopping, Fighting, Gathering, Workbench, Torch and bandage, Fishing, Crypts), a training tree that comes back 4 s after it falls. The player gets everything: axe, shield, sword, mace, spear, rod, boat, shark rod, torches, bandages, materials, all four element stones and full charges.

## 4. Arrival, death and lives

**Built**
- A new player drops from the sky into the hub pond with no outfit and no axe. They swim out and find the outfit and the axe on the bank toward the hut (the shield is a separate pickup).
- Going down: carried wood drops where you fell, and the axe, outfit and shield drop as ground items to walk back for. You respawn after 3 s, back in the hub pond (in the tutorial, beside the bed).
- Lives are infinite by default. An optional finite-lives mode gives N lives per night, restored at dawn.
- Death causes: enemy, falling tree, fall, burn (campfire).
- **A falling large tree** crushes anyone under the trunk. Without Sturdy gear it kills outright; Sturdy levels reduce it. Blocking and rolling do not help. The first large tree a player fells is always aimed at them, to teach this.
- Fall damage scales with height above a safe height.

**Approved, not built:** skill loss on death (about 5% of each skill level, then a 10 min grace period).

## 5. Player

### Movement (Built, locked; do not retune)
- Move 6, sprint 9.5, crouch 2.6 m/s; jump 1.05 m; gravity 26. Sprint and crouch are toggles.
- Acceleration, deceleration and turning live in `Core/SteerRules.cs`: firm stops, no drift on turns, the same on snow.
- Auto-climb: stepping onto a ledge up to 1.05 m (1.65 m out of water) while holding forward.
- Swimming: slower, costs stamina; out of stamina you only paddle. Entering water holsters everything (axe, weapon, shield, rod, torch, hammer); leaving restores it.
- Ladders: climb only when facing and touching the ladder; clean exit at the top.
- Wet (Valheim style): water or rain soaks you. While wet you run a little slower and regain stamina more slowly. It dries fast by a campfire.
- Roll (Left Alt): invulnerable frames, costs stamina.
- Hands full (axe drawn, torch lit) makes you run a little slower.

### Health, stamina, armour
- Health and stamina bars. Stamina pays for sprinting, rolling, fighting swings (tree chops are free) and blocks.
- Armour level (bought with hut wood) and the armoured outfit (an armour kit from each felled boss tree: helmet, chainmail, bracers, shield on the back) reduce damage, within a cap.
- Elemental wards: outfit upgrades per element, bought with arena essence.

### Hotbar and keys (Built)
| Slot | Key | Item |
|---|---|---|
| 1 | 1 | Shield (left hand, from the back) |
| 2 | 2 | Axe |
| 3 | 3 | Torch |
| 4 | 4 | Bandage |
| 5 | 5 | Fishing rod |
| 6 | 6 | Fish (eat) |
| 7 | 7 | Hammer (build) |
| 8 | 8 | Pickaxe |
| 9 | 9 | Sword |
| 10 | 0 | Mace |
| 11 | - | Spear |

- Pressing a weapon key puts that weapon in the right hand in place of whatever was there; pressing it again puts it away. Only one right-hand weapon at a time. The shield can be held together with any weapon.
- Other keys: Attack left click, Block right click, Roll Left Alt, Jump Space, Interact E, Repair R, Sprint Left Shift, Crouch Left Ctrl, Inventory Tab, Map M, Put away H, Boat B. All rebindable except B.
- Gamepad: triggers attack and block, face buttons jump, roll, interact and inventory, D-pad left/right moves the hotbar cursor and up uses the slot, R3 puts away what is held.

## 6. Chopping (logging)

**Built**
- Trees come in Small, Medium and Large. Each takes a number of chop points; a fist punch is a fraction of a hit.
- The tree chop is separate from combat. When a tree is targeted, the player faces the trunk and snaps to about 1.3 m reach; the axe alternates an overhead and a diagonal two-handed chop; the hit lands on the contact frame.
- Felled trees fall physically (away from the blow, pulled downhill), leave a log (chop it for the rest of the wood) and a stump (regrows at dawn unless cleared; a cleared spot stays bare for a few dawns).
- Logging skill: faster swings with level (SkillRules.LogSwingMultiplier).

## 7. Combat

### Weapons (Built, phase 1)
| Weapon | Recipe (workbench) | Move | Targets | Notes |
|---|---|---|---|---|
| Axe | start kit | fast chop combo (3 steps) | 1 | also the tree chop (separate move) |
| Sword | 6 wood, 6 iron | sweep | up to 3 | wider arc |
| Mace | 8 wood, 8 iron | overhead smash | 1 + splash | strong stagger, slower (x1.45 cooldown), splash 1.8 m |
| Spear | 10 wood, 4 iron | thrust | up to 2 | x1.55 reach |

- Each weapon has its own animation clips (KayKit melee clips, set by `Editor/WeaponAnimSetup.Apply`) and a 3-step combo; the third step is a finisher with a lunge and a strong stagger.
- No ranged weapons.

### Defence (Built, Valheim style)
- **Block** (hold right click): cuts damage (75% with the weapon alone, 90% with the shield in hand) and costs stamina per blocked hit (40% less with the shield). Out of stamina, your guard breaks.
- **Parry**: a block raised within 0.25 s of the hit takes no damage and staggers the attacker hard.
- **Stagger bonus**: an enemy staggered by a parry, a mace blow or a combo finisher takes **double damage** from every hit until it recovers. Ordinary hit flinches do not count.
- **Roll**: invulnerable frames.

### Elements (Built)
- Four elements in a cycle: Water beats Fire, Fire beats Wind, Wind beats Earth, Earth beats Water. Neutral beats nothing.
- **Charges**: killing an elemental enemy loads the counter element onto your weapon for a number of hits (15 by default, capped). Each hit on an enemy spends a charge. While charged, hits on enemies that element beats deal bonus damage. Charges reset at dawn. Hotbar badges show the count.
- **Element stones**: permanent enchantments bought at the workbench with essence and shards. A stone works like a charge that never runs out. All weapons use the player's stones and charges.

**Approved, not built (t65u):** each weapon archetype gets its own moveset (axes chop, spears jab, swords swing, maces overhand) for players and for the enemies carrying that weapon, so fights vary by enemy weapon.

## 8. Enemies

**Built**
- **Tree enemies (Treants)**: saplings planted by felled trees wake at night. Sizes follow the tree's size (bigger than the player). Elements:
  - Non-elemental: dark bark, green willow hair, blue eyes, antlers, a shield; carries a random plain weapon.
  - Earth: its own stone-brown body with gold veins, a moss crown and a glowing gold tree shield; mace.
  - Fire: charred body, orange cracks, flame hair; sword.
  - Water: the same willow tree spirit in teal (teal bark and willow, coral on head and shoulders); spear. Follows chopper-all-3D-concept-3.0 (user, 2026-10-17).
  - Wind: the same willow tree spirit in tan bark with pale green willow; axe. Follows chopper-all-3D-concept-3.0.
- They telegraph each hit (0.65 s wind-up), can be staggered and knocked back, and break building pieces and the hut.
- Each night rolls an elemental share, so some nights are mostly elemental and some mostly plain.
- **Hub night raids (t64u)**: only felled forest trees plant saplings. A tree plants 1-2 (small), 2-3 (medium) or 3-4 (large), times player scaling (coefficient 0.4). Trees felled at night plant nothing. Global cap 150. No chopping means no hub raid.
- **Grove warden (zone boss)**: when enough felled trees lie close together in one forest grove, a warden rises among them. Three phases by health: (1) slams and swings; (2) roots burst under players and it calls guards that harden its bark (75% less damage taken while they stand); (3) enraged, faster. Killing it counts as a boss beaten: everyone gets a boon and the hut gets wood. The trees needed rise after each boss.
- **Boss trees**: rooted boss saplings that grow; a grown one can be chopped down. Each felled boss tree gives every player an armour kit.
- **Crypt guardians and crypt bosses**: in the arena under each crypt's vault.

**Approved, not built (t71u, owner answer 2026-10-17): night difficulty and element mix.**
- Sapling counts keep a random roll, scaled by a night difficulty that grows with days played and with how geared the players are (axe, armour, weapons, stones), so some nights are harder.
- Every zone spawns all four elements, favouring its own (most enemies are the zone's element, the rest are the other three in roughly equal shares).
- Element charges last fewer hits than today. A charged weapon still damages every enemy; it only gets the bonus against the element it beats. With a mixed spawn the charge from one kill helps against the next kind of enemy.

Owner's words: This should have an element of RNG to it and factor in things like how long the player has played, if they're heavily geared, etc. This way we can try to give them more challenging nights occasionally. Where the elementals spawn at roughly the same rate, and you have the elemental buff for less time. I kind of backed myself into a corner by putting all the one element in one area, then that buff going onto the weapon. We might have to rethink how that is done in order to make it still fun and engaging. Make all the elemental enemies spawn in a zone but have it favor the zones element. So you could have a bvunch of other elementals that spawn when you have the fire buff. You would be able to dispatch of them quickly. And it isnt like the fire buffed weapon doesnt do damage to the fire elemental it does it just doesnt have the elemental bonus damage. 
## 9. The hut and building

**Built**
- **The hut** (hub): starts as a ruin. Deposited wood rebuilds it. It has health, a shared wood store and upgrade levels (more health per level). It contains the bed, the workbench and the shared storage chest. Enemies walk to its walls and hit it. A three-story hut (basement storage, hub floor, dormitory, workshop, outside ladder, chimney) is the target design.
- **Sleeping**: from dusk on, lie in the bed; when every player is in bed the night is skipped.
- **Building** (hammer, slot 7): pieces are Wood floor, Wood wall, Doorway, Roof, Stairs, Workbench, Standing torch, Campfire. They cost wood (the torch also fiber). The server checks range, a nearby workbench, cost, overlap and slope. No support rules (Minecraft style). Enemies can break pieces; taking a piece down refunds by how whole it is. Not allowed in the hub.
- **Protective dome**: a dome stone makes a 12 m dome over a base in a forest.
- Windows are transparent.

**Approved, not built (t63u, clarified 2026-10-17): zone homes.**
- Each of the four forests has objectives: catch that zone's fish, mine that zone's finds, and beat that zone's boss. When a zone's objectives are done, the players can build a home there.
- A zone home is raided every night by crypt enemies (the Draugr from that zone's crypt, not tree enemies), whether anyone chopped or not.
- Players collect materials to raise a barrier (shield) around the home so it is not destroyed. The barrier must be maintained: without upkeep it lapses and the raiders can damage the home.
- This is separate from the hub. The hub hut is only attacked by the saplings of trees the players fell in the forests.
- Already in the code to build on: per-zone fish (Marsh/Fire/Earth/Wind waters, 5 kinds each), per-zone mine finds (MineZone), the grove warden per forest and the crypt arena boss per element, Draugr crypt guardians, building pieces and the 12 m protective dome (no health or upkeep yet).

## 10. Crafting and upgrades

**Built**
- **Workbench**: in the hut, or placed as a piece. Pages: Craft, Upgrades, Forge, Repair.
- **Craft**: Torch, Bandage, Little boat (20 wood, 6 fiber), Shark rod (12 wood, 8 fiber, 4 iron; unlocks after landing 10 fish), Sword, Mace, Spear.
- **Upgrades** (paid from the hut's wood store): Axe level (faster, stronger swings), Armor level, Hut level.
- **Forge** (paid in crypt materials): Sturdy gear (iron; survive falling trees), Rune axe (shards; more damage), Elemental wards (essence), Element stones (essence and shards).
- **Repair**: worn tools (axe, rod) are repaired at the workbench.
- **Boons** (won from bosses, permanent for the session): better axe, sturdier outfit, longer torch, stronger salve, more stamina.

## 11. Items

**Built.** Carried as counts per kind (no grid or weight).
- Materials: Wood, Herb, Fiber, Iron (crypt), Rune shard, Essence (Water, Fire, Earth, Wind).
- Consumables: Bandage (heals; made from herbs and fiber), Torch (burns for a few minutes, lights the night).
- Fish: 32 kinds across six waters (Pond, Marsh, Fire, Earth, Wind, Sea); some only in rain, at night or at twilight.
- Mine finds: iron ore, quartz, gold nugget, geode, glowworm (bait), river pearl, fossils, gems, storm crystal, old pick head, per mine zone.
- Gear (not counted): axe, shield, outfit, rod, pickaxe, hammer, sword, mace, spear, boat, shark rod.
- Ground items: wood, herbs, fiber, and a fallen player's axe, outfit and shield. Walk over them or press Interact.
- **Shared chest** in the hut: one store for every player, saved with the world.

## 12. Pastimes

### Fishing (Built)
- Cast (hold to charge, a landing ring shows), wait (the float dips as fish nose at it), strike on the bite, then the fight: keep line tension out of the red (green, orange and red arc by the float) while the yellow arc shows the line left. Fish tire, thrash and leap.
- Hotspots on the water deplete and respawn. Fish have windows (rain, night, twilight). Biggest catch of each kind is kept.
- Fishing skill. Eating fish gives a Fed bonus.

### Mining (Built)
- Portal carts lead into the mines. Engage a vein and strike to the beat (the vein flashes the beats): clean strikes free the find, a full bar counts double, a rough one can crack it. Each player may work each vein once a day.
- Mining skill. Collection of finds with the biggest of each.

### Boat and shark (Built)
- Craft the little boat at the workbench, carry it, deploy it on the ocean only (B), row with WASD, hold B on shore to pick it up.
- With the shark rod at sea, a shark hooks about 25 m out and tows the boat: keep the balance marker in the green centre of the arc to drain the shark's stamina, then reel (attack) and hold Sprint through its leaps.
- The landed shark is a counter only for now.

**Approved, not built:** mining and fishing move onto the shared arc UI (ArcRules) like the shark.

## 13. Skills

**Built (simple):** Logging, Mining and Fishing levels, saved, with modest bonuses.

**Approved, not built (phase 2):** seven skills (Fishing, Logging, Mining, Axe, Sword, Mace, Spear), each 0 to 100, XP on use. At level 100, about +25% damage or yield and up to 15% faster swings. Death costs about 5% of the current level per skill, then a 10 min grace period (both configurable). HUD toast on level-up, a Skills page under Tab, save/load migrating LogSkill/MineSkill/FishSkill, network sync, tests.

## 14. Multiplayer

**Built**
- Host and play through Steam lobbies (join code), FishNet with the host as server. The server owns gameplay state (SyncVars and ServerRpcs). Clients predict their own movement; the server validates speed and snaps back.
- Shared: the world, the hut and its wood store, the chest, bosses, building pieces, day/night. Per player: carried items, gear, weapons, skills, boons, charges.
- Sapling counts and arena waves scale with the number of players.

## 15. Saving

**Built.** Worlds are saved as JSON under `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Chopper\Saves`: the seed, clock, felled trees and remains, hut, chest, building pieces, domes, bosses, mines worked, and every player's items, gear, weapons, skills, upgrades and collections. Tutorial and test games are never saved.

## 16. UI

**Built:** in-game interface built in code (`UI/GameUi.cs`, no prefabs), laid out like Valheim: hotbar top left, minimap top right, health and stamina bottom left, status badges (element charges, Wet, Fed), one-word cues for fishing and mining, pickup feed, workbench and inventory pages (Tab), map (M), settings (graphics, audio, keybinds for keyboard and pad), dev console (developer builds), title screen with New world, Load, Tutorial.

## 17. Audio

**Built:** procedural sound effects (chops, swings, blocks, parries, splashes, footsteps by surface), music moods, imported clips in `Resources/Audio` (do not touch without the owner).

## 18. Out of scope (decided)

- Ranged weapons (bows, crossbows, thrown, magic).
- Hunger, thirst, temperature, disease.
- Item rarity tiers, grid inventory, carry weight.
- PvP.
- Asset-store art packs and Unity primitive art (art is custom Blender; the KayKit melee clips are the one animation source for player weapons).

## 19. Open items

See the tail of `Chopper/Docs/TASKBOARD.md`. At the time of writing:
- Per-weapon movesets for players and enemies (t65u).
- Skills phase 2.
- Biome home bases (t63u).
- Hub raid sapling count question (t64u).
- Three-story hut; cave and portal interiors versus `cave-chopper-concept-3.0.jpeg`.
- Separate enchanted weapon items (asked: today enchantment is the player's stones and charges).
- A tutorial freeze the owner saw after chopping one tree (not reproduced; need steps or a log).
