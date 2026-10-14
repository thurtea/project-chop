# Chopper asset list

Everything the game needs, judged against the three concept sheets and built as a seeded world in the Valheim sense: biomes, paths, shores, mountains, and caves grow from the seed. Models stay voxel and chunky. Detail lives in the texture. One Humanoid rig drives Chopper and any bought animation pack.

Sheets (look only, not store capsules):

- `Chopper/Art/Concepts/chopper-3D-concept-2.0.jpg` — world, hut, pines, Chopper in the meadow
- `Chopper/Art/Concepts/chopper-character-and-axe-3D-concept-2.0.jpg` — body, clothes, bear axe
- `Chopper/Art/Concepts/chopper-start-menu-background-3D-concept-2.0.jpg` — hut and meadow from farther out

Forests differ by colour, plants, stone, and water, not by a second art style. A mesh that already exists is marked **have**. A recolour of that mesh is **tint**. Anything else is **need**.

## Character

One player body. Outfit pieces swap on the same rig. Hair hides when the helmet is on.

| Asset | Status | Notes |
|---|---|---|
| Body, head, hands, bare feet | have | Grey tunic and shorts are the undressed layer |
| Ginger hair, heavy brows, beard with curls | have | Still judged against the sheet |
| Plaid shirt, rolled sleeves, plaid hem | have | |
| Patchwork vest, lacing, crossbelt, buckle | have | |
| Trousers, knee pads, shin wraps, folded boots | have | |
| Chainmail skirt, leather bracers | have | Armour kit |
| Horned helmet with skull and mail neck | have | Armour kit |
| Round-heater shield, axe and two pines painted | have | On the back until drawn |
| Bear-head axe | have | Trees and night-trees only |
| Cape or cloak | need | Wind forest dye, optional |
| Backpack or bedroll | need | Reads at a distance on other players |
| Swim / wet variant | need | Same mesh; darker atlas is enough |
| Hurt and death poses | need | Clips, not a second body |

## Animations

Bought Humanoid clips, retargeted. Do not replace the controller's state names.

| Set | Status | Clips |
|---|---|---|
| Locomotion | have | Idle, walk, run, crouch, jump, land, air |
| Swim | have | Mixamo swim and swim idle |
| Tree chop | have | |
| Combat trio and flinch | have | KayKit slice, horizontal, chop, hit, block |
| Roll / dodge | need | Valheim-style sidestep, i-frames |
| Draw, holster, pickup | need | Axe, sword, pick, rod, torch, hammer |
| Mining strikes | need | Same rhythm as the combat trio, pick in hand |
| Fishing cast, wait, reel, land | need | Rod in the right hand |
| Sit, lie in bed, use workbench | need | |
| Boat sit, row, steer | need | |
| Hit react by direction, stagger, get up, death | need | |
| Emotes | need | Wave, point, sit on the ground. Low priority |

Pack already named for the combat set: RPG Character Mecanim Animation Pack (Explosive).

## Weapons and tools

The axe stays the tree weapon. Fight weapons are separate meshes on `Weapon_R`. Each wants a held pose, a back-socket pose, and a floor pickup.

| Asset | Status | Use |
|---|---|---|
| Bear axe | have | Chop, and the night-trees |
| Shield | have | Block and parry |
| Pick | need | Mines. Engages a vein; strikes on the beat |
| Fishing rod and line | need | The float is a small mesh |
| Hammer | need | Building |
| Torch, held | have | |
| Sword, one-handed | need | Swing, swing, pause, swing |
| Mace or club | need | Same combo |
| Spear | need | Thrust, and a throw if that ships |
| Two-hander (battleaxe or sledge) | need | Slower combo, no shield |
| Bow and arrow | need | Only if ranged combat is added |
| Knife | need | Gut fish, quiet tool. Optional |

## Armour and clothing tiers

| Asset | Status |
|---|---|
| Start clothes (the sheet) | have |
| Armour kit: helmet, mail, bracers, shield | have |
| Ragged or repaired cloth | tint |
| Zone cloaks or dye kits (water, earth, fire, wind) | need |
| Heavy plate / elite pauldrons | need | Later than the sheet. Do not replace the vest until that pass |

## Enemies

Tree people, enlarged for bosses, varied for crypts. One bought creature pack, scaled and recoloured, not a new species per room.

Pack already named: Treant Monster Creature - Fantasy RPG.

| Asset | Status | Where |
|---|---|---|
| Treant, three sizes | have | Night woods, crypt guardians |
| Boss tree, one per forest | have | Armour-kit drop. Wants a larger silhouette per zone |
| Water treant (dripping, pale, reed growth) | tint | Water forest and drowned crypt |
| Earth treant (roots, moss, thicker trunk) | tint | Earth |
| Fire treant (charred, ember cracks) | tint | Fire |
| Wind treant (bare branches, tattered cloth) | tint | Wind |
| Crypt guardian idle, walk, attack, hit, death | need | On the treant rig |
| Boss windup, slam, roar, fall | need | |
| Stump that wakes, or a rooted boss phase | need | Optional |
| Ambient critters (bird, fish school, firefly) | need | No combat. Sells the biome |

No human bandits, no undead army, unless a later design says so. The night threat is the trees.

## World

Procedural, not a hand-built island. These are the materials and scatter meshes the generator instances.

| Asset | Status | Notes |
|---|---|---|
| Block terrain: grass, dirt, stone, sand, snow | have | Vertex or block colour |
| Hub meadow grass and path stones | have | |
| Pine, four foliage tiers, dirt pad and roots | have | One mesh, four tints |
| Stump with roots and pad | have | Stays when the trunk falls |
| Felled trunk log | have | Choppable |
| Rock clusters, mossy | have | |
| Bush, herb, fern, grass tuft, branch | have | |
| Zone pine tints: earth, water, fire, wind, hub | have | |
| Stepped mountains and ring | have | Terrain, not a mesh |
| Wobbly lakes and sea | have | |
| Clouds and sun blocks | have | |
| Fog, warm noon grade | have | Settings, not a mesh |
| Beach grass, driftwood, shells, wet sand | need | Shore ring |
| Reeds and lily pads | need | Water forest and marsh |
| Charred stumps, ash grass, cracked earth | need | Fire forest |
| Moss carpets, root arches, boulder stacks | need | Earth forest |
| Bare pines, wind-bent shrubs, pale stone | need | Wind forest |
| Snow cap on the high ring | need | Optional |
| Waterfall sheet on a step | need | Short, tiled |
| Signpost | have | |

### Sky, weather, time

| Asset | Status |
|---|---|
| Day, dusk, night sky colours | have |
| Rain streaks and splash | need |
| Thunder | need | Clips exist, not wired |
| Wind on pines, bushes, tufts | need | Motion, W7-03 |
| Campfire and torch flicker | need | |
| Lightning flash | need | Optional, with thunder |

## Player building

Pieces are 2 m, voxel, same wood language as the hut.

| Asset | Status |
|---|---|
| Wood floor, wall, doorway, roof, stairs | have |
| Workbench, standing torch, campfire | have |
| Log window, log gable | have |
| Door that closes | need |
| Chest and grand chest | have |
| Bed | have |
| Fireplace | have |
| Gate and palisade | need |
| Beam, post, fence | need |
| Ladder | need |
| Rug, stool, shelf, barrel, crate | need |
| Map table or hanging map | need | Optional |
| Ward or standing stone (one per element) | need | Crypt essence, later |

The hub hut is `hall_voxel`: stone base, log courses, plank roof, porch, loft, basement. **have.**

## Crypts

Stone rooms under a small mausoleum. One entrance mesh, then kits so a crypt is not one repeated cube.

| Asset | Status | Notes |
|---|---|---|
| Mausoleum entrance | have | Block boxes today |
| Stone floor, wall, ceiling, corridor | have | Boxes |
| Stairs down, arena hall | have | |
| Lantern | have | |
| Chest, grand chest | have | |
| Stone door or slab | need | |
| Pillar, rubble pier, broken statue | need | Breaks up flat walls |
| Sarcophagus, urn, chain, grate | need | |
| Root breach (earth), drip stone (water), scorch (fire), cracked plaster (wind) | need | One dressing set per zone |
| Arena braziers and boss dais | need | |

## Mines

Quiet work, like fishing. Timber portal, chambers, rails, veins.

| Asset | Status |
|---|---|
| Timber portal | have |
| Chamber, gallery, posts and beams | have |
| Ladder shaft | have |
| Lantern, cart, barrel, rails | have |
| Ore vein (crack in the wall) | have | The glow is a shader |
| Pick strike sparks, chips, dust | need |
| Support beam, cave-in rubble, flooded sump | need | So chambers are not the same box |
| Crystal cluster per zone (aquamarine, sulfur, amber, storm crystal) | need | The find can stay an icon; the wall prop sells the room |

Finds that need an icon, not a world mesh: iron ore, quartz, gold nugget, geode, glowworm, river pearl, nautilus, aquamarine, obsidian, sulfur, fire opal, amber, ammonite, fern fossil, sky quartz, feather fossil, storm crystal, old pick head.

## Fishing and boats

| Asset | Status |
|---|---|
| Rod, bobber, tension bar | have | Bar is UI |
| Fish icons for 32 kinds | need | Perch through breaker cod. One icon style |
| Glowworm jar (bait) | need | |
| Raft | need | Early coast |
| Rowboat | need | |
| Longship | need | Wood from all four zones |
| Oar, rudder, mast and sail, rope coil | need | |
| Dock or mooring post | need | |
| Net, bucket, drying rack | need | Shore dressing |

Bigger fish are farther out. The hulls can share one voxel plank language; the longship is longer, not a new style.

## Items, crafting, UI icons

One icon set, same pixel size, on the dark inventory background.

| Group | Status |
|---|---|
| Wood, herb, fiber, bandage, torch, iron, shard | need | Counts exist; icons should match the finds |
| Essence: water, fire, earth, wind | need |
| Food: cooked meat, stew, grilled fish | need | If eating is added. Raw fish can use the fish icons |
| Potion or mead | need | Optional |
| Hammer, pick, rod, weapons, armour kit | need | Hotbar icons |
| Build piece ghosts | have | The piece meshes |
| HUD: health, stamina, hotbar, compass, prompt | have | |
| Title backdrop | have | Start-menu concept |
| Logo and wordmark | have | |
| Map, fog of war | need | If a map is added |
| Damage numbers or cue words (Clean, Good, Rough) | have | Text |
| Cursor and reticle | need | |

## Effects

Small, chunky, not photoreal sparks.

| Effect | Status |
|---|---|
| Chop chips and stump dust | need |
| Tree fall and landing | need |
| Combat hit, parry flash, block sparks | need |
| Mine sparks, chips, bar burst | need |
| Fire, torch flame, ember | need |
| Rain, swim splash, bobber ripple | need |
| Portal or crypt door shimmer | need |
| Level-up or upgrade | need |
| Footstep puffs | need | Optional |

## Audio

One library, one voice per family. Pack already named: RPG & Fantasy Sounds Bundle.

| Family | Status |
|---|---|
| Axe on wood, axe whoosh, shield block, shield parry | have |
| Grass footsteps, tree fall | have |
| Hut hit, UI click and hover, upgrade | have |
| Enemy idle, step, attack, hit, death | need |
| Fishing cast, bite, reel, splash, snap | need |
| Pick on stone, vein break, find | need |
| Crypt ambience, mine ambience, drip | need |
| Roll, jump, land | need |
| Swimming, wading | need |
| Rain, wind, night, birds, fire crackle | need |
| Thunder | need | Files are in, not played |
| Craft start and finish, place piece | need | Files are in, not played |
| Boat creak, oar, sail | need |
| Music: day, night, title, combat | have | Title is the calm night track. Combat sting still to confirm in play |
| Zone music or stingers | need | Optional, one motif each |

## UI and presentation

| Asset | Status |
|---|---|
| Menu column, settings, inventory pages | have |
| World loading and join code | have |
| Steam capsules and library art | need | Sizes are in `Steam/README.md`. Do not invent new paintings for this list |
| Website gallery shots | need | Only after in-game pictures match the sheets |

## Buy or build, in order

1. Humanoid combat and work clips (dodge, draw, mine, fish, hit) on the rig Chopper already has.
2. One treant pack, four zone tints, boss scale.
3. One sound library for the empty families above.
4. Pick, sword, mace, spear, two-hander, rod, hammer, three boats. Same voxel wood and steel as the bear axe.
5. Shore, crypt dressing, and mine dressing so generated rooms are not one repeated chamber.
6. Icons for items, fish, and finds.

Do not buy a realistic character, a second player race, or a photoreal environment pack. Those will not sit next to the concept sheets.
