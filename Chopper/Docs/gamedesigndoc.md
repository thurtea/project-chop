I couldn’t load the form page itself, so I can’t see its exact fields. Here’s a paste-ready version organized around the standard GDD sections; paste each block into the matching field, or send me the field names/screenshots and I’ll reformat it exactly.

## Game title

**Chopper**

## Genre

3D co-op survival ARPG with crafting, base building, procedural world generation, and multiplayer.

## Platform

- PC (Windows) first
- Linux dedicated server
- Steam release target
- Controller, keyboard/mouse, Steam Input support

## Engine and tech

- Unity 6000.5.10f1
- Universal Render Pipeline (URP)
- FishNet networking
- Server-authoritative gameplay
- Host-and-play first, dedicated Linux server later
- Deterministic procedural world generation from a shared seed

## High concept

Chopper is a 1–8 player co-op survival ARPG set in a hostile procedurally generated world. Players gather resources, craft gear, build bases, fight biome-specific enemies, survive hunger and weather, defeat bosses, and unlock progressively more dangerous regions.

## Core fantasy

You are a survivor with an axe, a campfire, and almost nothing else. Every expedition risks your gear, food, and life—but returns with the materials needed to become stronger.

## Player promise

- Every session creates visible progress: gear, base, recipes, map knowledge, or boss progress.
- Combat is active and readable: dodge, block, parry, stagger, position, and exploit elemental weaknesses.
- The world is dangerous but fair: biomes communicate their threats, resources, and rewards.
- Co-op is central: players specialize, share resources, defend bases, and revive each other.

## Target audience

Players who enjoy survival crafting games, action RPG combat, co-op PvE, exploration, base building, and long-term progression. Comparable audience interests: Valheim, Terraria, Minecraft, V Rising, and classic MMORPG progression.

## Core pillars

1. **Prepare or die:** Survival pressure makes planning meaningful.
2. **Chop, craft, conquer:** Gathering directly feeds combat and base progression.
3. **Explore a living world:** Procedural biomes, dungeons, weather, events, and bosses create variety.
4. **Cooperation matters:** Multiplayer rewards shared bases, roles, revives, and coordinated boss fights.
5. **Readable danger:** Every major threat has a telegraph and counterplay.

## Core loop

1. Leave base or camp.
2. Travel into a biome.
3. Gather wood, ore, food, hides, herbs, and rare materials.
4. Fight wildlife, humanoids, elementals, and dungeon enemies.
5. Return before night, starvation, cold, or a raid overwhelms you.
6. Craft, cook, repair, upgrade, build, and prepare for the next expedition.
7. Defeat a boss or activate a progression gate to unlock the next biome tier.

## Session loop

1. Load into world or join a friend’s server.
2. Check hunger, warmth, durability, objectives, and base status.
3. Choose an activity: resource run, dungeon, boss, exploration, farming, defense, or trade.
4. Explore, gather, fight, and discover points of interest.
5. Return, deposit loot, craft upgrades, and plan the next run.

## Player character

### Attributes

| Attribute | Purpose |
|---|---|
| Health | Survivability |
| Stamina | Sprinting, attacks, dodging, blocking, gathering |
| Hunger | Food drain; low hunger reduces regeneration and stamina |
| Warmth | Cold biomes and night cause exposure damage |
| Carry Weight | Limits hauling; upgraded packs increase capacity |
| Armor | Physical damage reduction |
| Elemental Resistance | Reduces fire, frost, poison, lightning, shadow damage |
| Movement Speed | Walking, sprinting, swimming, mounted travel |

### Core stats

- **Strength:** melee damage, carry weight, harvesting speed.
- **Dexterity:** attack speed, dodge distance, bow damage, stealth.
- **Vitality:** max health, food efficiency, resistances.
- **Endurance:** max stamina, stamina regeneration, blocking stability.
- **Intellect:** spell damage, mana, crafting quality, enchanting.
- **Luck:** rare drops, chest quality, proc chance.

## Movement

- Walk, sprint, crouch, jump, swim, climb, roll/dodge.
- Sprint, attacks, blocking, dodging, and gathering consume stamina.
- Swimming drains stamina; drowning causes damage.
- Falling damage scales with height and landing surface.
- Late-game traversal includes ladders, ropes, grappling hook, boats, mounts, portals, and ziplines.

## Combat

### Melee

- Light attack: fast, low stamina.
- Heavy attack: slow, high damage, breaks guard.
- Combo attacks: timed chains.
- Charged attack: increased damage and stagger.
- Weapon types: axe, sword, hammer, spear, dagger, greatsword, shield plus one-handed weapon.

### Ranged

- Bow: charged shots and arrow types.
- Crossbow: slow reload, high burst damage.
- Thrown weapons: axes, spears, bombs, flasks.
- Magic staff/wand: elemental projectiles and area spells.

### Defense

- Dodge roll with invulnerability frames.
- Block reduces damage and drains stamina.
- Timed parry staggers enemies.
- Shield bash interrupts weaker enemies.
- Armor and elemental resistance mitigate damage.

### Status effects

| Effect | Result |
|---|---|
| Bleed | Damage over time; movement increases it |
| Burn | Fire damage over time; spreads through dry materials |
| Freeze | Slows movement and attack speed |
| Shock | Interrupts actions and chains briefly |
| Poison | Damage over time; reduces healing |
| Curse | Reduces healing and increases damage taken |
| Stagger | Interrupts enemy action |
| Knockback | Pushes target away |
| Root | Prevents movement |
| Fear | Causes low-tier enemies to flee |

## Damage model

```text
final_damage =
  raw_damage
  * weapon_scaling
  * elemental_multiplier
  * (1 - armor_reduction)
  * (1 - resistance)
  * difficulty_multiplier
```

Critical hits, backstabs, weak points, and debuffs apply as explicit multipliers.

## Death

- Players respawn at bed, campfire, or world spawn.
- A recoverable corpse marker contains a portion of carried inventory.
- Corpse runs preserve risk without total loss.
- Co-op players can revive downed allies when the encounter permits.
- Hardcore mode optionally deletes the character on death.

## Survival systems

### Hunger
- Food restores hunger and grants temporary buffs.
- Raw food can cause poisoning.
- Cooking improves nutrition and adds buffs.
- Starvation reduces stamina regeneration, then health.

### Thirst
- Water comes from rivers, lakes, rain collectors, wells, and waterskins.
- Dirty water can cause disease unless boiled or filtered.

### Temperature
- Cold biomes require clothing, fires, shelter, and warm food.
- Hot biomes require shade, water, cooling gear, and daylight planning.
- Storms amplify exposure effects.

### Rest
- Beds set respawn points.
- Sleeping skips time only in safe conditions.
- Rested buff improves stamina regeneration and experience gain.
- Comfort objects extend the rested duration.

### Injury and disease
- Food poisoning, infection, broken bones, bleeding, and curses.
- Treated with bandages, splints, herbs, potions, shrines, or rest.

## Resources

- **Wood:** branches, logs, hardwood, resin, sap.
- **Stone:** rocks, flint, coal, obsidian, crystal.
- **Metal:** copper, tin, iron, silver, gold, mythril, voidsteel.
- **Organic:** fiber, hides, leather, bone, feathers, chitin.
- **Food:** berries, mushrooms, meat, fish, crops, eggs, honey.
- **Magic:** essence, souls, elemental shards, runes, boss materials.
- **Rare:** relics, schematics, treasure maps, boss trophies.

## Gathering

- Axes harvest trees; picks harvest rock and ore.
- Tool tier gates node quality and available resources.
- Nodes have health, yield tables, respawn timers, and biome restrictions.
- Trees fall physically and produce logs and debris.
- Mining produces ore chunks and occasional rare bonuses.
- Animals drop meat, hide, bone, and trophies.
- Fishing uses bait, rod tier, timing, and biome fish tables.
- Farming uses soil, seeds, water, growth time, weather, and seasons.

## Inventory

- Grid inventory with weight and stack limits.
- Equipment slots: head, chest, legs, feet, hands, cape, weapon, offhand, ammo, tool, two trinkets.
- Hotbar for weapons, tools, consumables, and placeables.
- Chests, barrels, and storage containers with sorting and filters.
- Optional shared team storage at bases.

## Items

### Item properties
- ID, name, icon, description, rarity.
- Type, stack size, weight, value.
- Damage, armor, durability, elemental type.
- Tool tier, skill requirement, station requirement.
- Recipe, repair cost, upgrade path.
- Buffs, status effects, lore, and set bonuses.

### Rarity
- Common, Uncommon, Rare, Epic, Legendary, Mythic.
- Rarity affects stats, affixes, visuals, and salvage value.
- Crafted gear remains competitive with rare drops.

## Crafting

### Stations
- Workbench
- Campfire
- Cooking pot
- Forge
- Anvil
- Sawmill
- Loom
- Alchemy table
- Enchanting table
- Rune altar
- Repair station
- Storage/logistics stations

### Rules
- Recipes unlock via materials, exploration, bosses, skills, and schematics.
- Some recipes require station tier, fuel, power, or biome conditions.
- Quality is affected by skill, station tier, materials, and blessings.
- Salvaging returns partial materials.
- Progression: stone tools → copper/bronze → iron → steel → crystal → void/ashen gear.

## Building

### Pieces
- Foundations, floors, walls, roofs, doors, gates, stairs.
- Ladders, windows, bridges, docks, towers.
- Campfires, torches, braziers, lamps.
- Beds, chests, crafting stations.
- Walls, spike traps, ballistae, arrow slits, alarms.
- Decorative and comfort pieces.

### Rules
- Pieces require structural support.
- Unsupported pieces collapse.
- Fire, explosions, and boss attacks damage structures.
- Placement uses snapping, rotation, terrain alignment, and ghost preview.
- Server validates placement to prevent overlap and exploit structures.

## Base defense

- Raids target players, beds, key structures, or stored resources.
- Raid intensity scales with base value, boss progress, night count, and difficulty.
- Players defend with walls, traps, turrets, alarms, and escape routes.
- Raids must be survivable with preparation.

## Procedural generation

### Generation layers
1. World seed.
2. Continent and ocean shape.
3. Biome map from temperature, moisture, altitude, corruption, and magic density.
4. Terrain heightmap.
5. Rivers, lakes, caves, and coastlines.
6. Roads, ruins, and points of interest.
7. Dungeon placement.
8. Resource distribution.
9. Enemy spawn tables.
10. Boss arenas and landmarks.

### Biomes

| Biome | Resources | Threats | Role |
|---|---|---|---|
| Meadow | Wood, stone, berries, deer | Low wildlife | Tutorial and first base |
| Forest | Hardwood, mushrooms, hides | Wolves, bandits, treants | Early combat/building |
| Coast | Fish, salt, shells, driftwood | Pirates, drowned | Boats and fishing |
| Swamp | Herbs, leeches, mud, iron hints | Poison, insects, undead | Alchemy and resistance |
| Mountain | Ore, crystal, cold materials | Frost wolves, drakes | Mining and cold survival |
| Volcanic | Obsidian, sulfur, fire essence | Fire elementals, lava | Fire gear and late weapons |
| Ashen/Corrupted | Void materials, cursed relics | Shadows, curses, elites | Endgame |
| Underground | Ore veins, fungi, ruins | Darkness, cave predators | Dungeons and mining |

### Guarantees
- Every seed must be completable.
- Critical resources must be reachable.
- Boss arenas must be accessible without impossible gear.
- Essential progression cannot be blocked by terrain generation.

## Dungeons and POIs

### Dungeon types
- Surface camps
- Burial chambers
- Mines
- Sunken ruins
- Bandit forts
- Elemental shrines
- Boss arenas
- Endgame vaults

### Contents
- Rooms, corridors, traps, locked doors, keys, puzzles.
- Enemy packs, elites, mini-bosses.
- Chests, lore, schematics, rare resources.
- Escape route or teleport item after boss kills.
- Cleared rooms remain cleared; resource chests respawn slowly or not at all.

### Other POIs
- Abandoned villages
- Watchtowers
- Shrines
- Merchant camps
- Ancient obelisks
- World bosses
- Event sites
- Hidden treasure locations

## AI and enemies

### AI states
Idle, patrol, investigate, chase, attack, flee, call reinforcements, guard, return home, dead.

### Enemy families
- Wildlife: deer, boar, wolves, bears, birds, fish.
- Humanoids: bandits, cultists, undead soldiers, shamans.
- Constructs: animated armor, golems, turrets.
- Elementals: fire, frost, storm, poison, stone.
- Monstrosities: treants, spiders, leeches, wyrms.
- Bosses: biome guardians and final threats.

### Behaviors
- Wolves and bandits use pack tactics.
- Ranged enemies keep distance and seek cover.
- Tanks protect weaker allies.
- Shamans buff, heal, or summon.
- Ambushers hide in vegetation, caves, and ruins.
- Elites use affixes and drop better loot.

### Spawning
- Natural spawns use biome, time, weather, player proximity, and threat level.
- Events and raids are server-controlled.
- Spawn caps prevent performance issues and unfair pile-ons.

## Bosses

Each boss has:
- A distinct arena.
- Melee, ranged/AoE, and environmental attacks.
- Phase changes at 66% and 33% health.
- Clear counterplay: dodge timing, cover, elemental weakness, destructibles, or positioning.
- Guaranteed first-kill reward.
- Repeatable rewards with diminishing returns.

### Boss arc
1. **Forest Guardian:** dodge, stagger, environmental hazards.
2. **Swamp Matron:** poison resistance, adds, arena movement.
3. **Mountain Warden:** frost, ranged pressure, shield timing.
4. **Volcano Tyrant:** fire zones, interrupts, burst damage.
5. **Ashen King:** final multi-phase encounter combining prior mechanics.

## Magic

### Elements
- Fire: burn, ignition, area denial.
- Frost: slow, freeze, armor reduction.
- Lightning: chain damage and interruption.
- Poison: sustained damage and healing reduction.
- Holy: anti-undead, healing, shields.
- Shadow: lifesteal, fear, curse.
- Nature: roots, healing, thorns, beast control.

### Reactions
- Fire + Frost = Steam burst
- Fire + Nature = Wildfire
- Frost + Lightning = Superconduct
- Poison + Fire = Toxic cloud
- Shadow + Holy = Annihilation burst

## Multiplayer

### Session flow
1. Main menu.
2. Create or join world.
3. Select character.
4. Connect via Steam, LAN, direct IP, or dedicated server.
5. Spawn at bed, ship, or world spawn.
6. Sync chunks, time, weather, inventory, structures, quests, and boss state.

### Networking
- FishNet server authority.
- Host acts as server when hosting.
- Dedicated server runs headless simulation.
- Clients predict local movement.
- Server reconciles position and validates actions.
- Inventory, crafting, death, and boss state use reliable messaging.
- Frequent cosmetic updates may use unreliable messaging.

### Co-op
- Shared world progression.
- Individual inventory and character progression.
- Permission-based base ownership.
- Revive downed teammates.
- Group buffs from food, shrines, banners, and trophies.
- Loot rules: personal, free-for-all, or round-robin.
- Admin tools: kick, ban, mute, save, backup, teleport, spawn control.

### PvP
- PvE only by default.
- Optional PvP zones.
- Optional full PvP with configurable loot rules.
- Friendly-fire toggle for co-op groups.

## Quests

### Types
- Tutorial objectives.
- Gather requests.
- Build requests.
- Hunt requests.
- Explore/discover locations.
- Clear dungeon.
- Defeat boss.
- Rescue NPC.
- Defend structure.
- Deliver item.
- Environmental puzzle.

### Structure
- Main quests drive biome and boss progression.
- Side quests reward recipes, followers, base upgrades, and lore.
- Dynamic events create short-term choices and high-risk rewards.
- Journal tracks active, completed, and failed objectives.
- Map markers are earned through discovery.

## Economy

- Merchants appear at fixed camps or wander the world.
- Currency: coins, relics, and barter goods.
- Vendors sell recipes, seeds, tools, consumables, cosmetics, and map fragments.
- Players sell salvage, trophies, and rare materials.
- Prices scale with rarity, scarcity, and reputation.
- No pay-to-win; monetization limited to cosmetics or expansions.

## Day/night, weather, events

### Day/night
- Day: safer gathering and exploration.
- Dusk: increased predator activity.
- Night: dangerous spawns, reduced visibility, stronger magic enemies.
- Some resources and dungeons appear only at night.

### Weather
- Clear, rain, storm, fog, snow, ashfall, blood moon.
- Weather affects visibility, temperature, fire, fishing, enemy spawns, and travel.
- Storms can damage exposed structures.
- Blood moons trigger raids or elite hunts.

### Events
- Meteor crash.
- Caravan ambush.
- Cursed grove.
- Ancient vault opening.
- Boss herald.
- Resource surge.
- Migration herd.
- Dungeon breach.

## UI/UX

### Screens
- Main menu
- World select/create
- Character select/create
- Settings
- HUD
- Inventory
- Crafting
- Map
- Journal/quests
- Skills
- Bestiary
- Codex/lore
- Social/player list
- Server browser
- Death screen
- Pause menu

### HUD
- Health, stamina, hunger, warmth, buffs.
- Hotbar and equipped weapon.
- Compass or optional minimap.
- Objective tracker.
- Damage numbers, status icons, interaction prompts.
- Teammate health frames and pings.

### UX rules
- Every interaction has a clear prompt.
- Crafting shows missing materials and station requirements.
- Map supports personal and shared markers.
- Death screen shows corpse location.
- Settings include accessibility, keybinds, graphics, audio, network, and gameplay options.

## Audio

- Footsteps vary by terrain and speed.
- Weapon impacts vary by material.
- Biome ambience changes with time and weather.
- Boss music changes by phase.
- Damage feedback uses hitstop, particles, screen shake, and directional indicators.
- Low health uses heartbeat and vignette.
- Server events use clear audio cues: raid warning, boss spawn, teammate down.

## Persistence

### World save
- Seed.
- Time, weather, day count.
- Modified chunks.
- Placed/destroyed structures.
- Harvested nodes.
- Chest contents.
- Boss flags.
- Quest state.
- Discovered POIs.
- Active events.

### Player save
- Appearance.
- Level, skills, attributes.
- Inventory and equipment.
- Known recipes.
- Map markers.
- Respawn point.
- Reputation and quest flags.

### Save rules
- Autosave on interval, sleep, boss kill, major build action, logout.
- Manual save for host/admin.
- Save versioning and migration.
- Backup previous save before overwrite.
- Dedicated server scheduled backups.

## Difficulty

| Mode | Design |
|---|---|
| Relaxed | Slower hunger, weaker enemies, forgiving death |
| Standard | Intended experience |
| Hard | Stronger enemies, harsher survival, better rewards |
| Brutal | Limited deaths, scarce resources, aggressive raids |
| Custom | Sliders for damage, spawns, resources, raids, death penalties |

## Endgame

- Ascension / NG+ after final boss.
- Endless rifts or corrupted dungeons.
- Weekly modifier world bosses.
- High-tier base upgrades.
- Legendary crafting and enchanting.
- Cosmetic titles, banners, armor skins, and base decorations.
- Optional hardcore and speedrun modes.

## Milestones

### M0 — Playable core
Movement, melee, health, stamina, death, respawn, test terrain, one enemy, basic multiplayer movement.

### M1 — Survival foundation
Inventory, pickup, trees, rocks, tools, crafting, campfire, hunger, day/night, basic building.

### M2 — World systems
Procedural terrain, biomes, resources, map, compass, weather, enemy spawning.

### M3 — Combat depth
Weapon classes, dodge, block, parry, status effects, AI families, ranged combat, dungeons.

### M4 — Progression
Skills, crafting tiers, armor, bosses, biome gating, quests, journal.

### M5 — Multiplayer hardening
Server-authoritative inventory/combat, host/join, persistence, permissions, raids, admin tools.

### M6 — Content and polish
Full biomes, bosses, audio, UI polish, optimization, Steam integration, release pipeline.

## Success metrics

- Players complete the first boss without external guidance.
- Average session creates at least one meaningful upgrade.
- Co-op players use base, storage, crafting, and revive systems.
- Combat deaths are mostly attributable to identifiable mistakes.
- Server remains stable with 8 players during raids and boss fights.
- World generation produces completable seeds at a high rate.