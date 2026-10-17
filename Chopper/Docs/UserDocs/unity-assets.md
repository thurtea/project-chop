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

-------------

this is the results of searching for those assets

--------------

Here’s a purchase shortlist matched to your asset list. The strongest fit is **buy the named packs first, then fill gaps from a small number of voxel-compatible packs** rather than hunting one asset per item.

## Buy first

| Your need | Best match | Where | Why it fits |
|---|---|---|---|
| Humanoid combat/work clips: roll, draw/holster, mine, fish, hit reactions, death | **RPG Character Mecanim Animation Pack** by Explosive | Unity Asset Store, $119.99 | Already named in your list; it is Humanoid/Mecanim-oriented and includes a very large clip library, with a free version available for testing.  [assetstore.unity](https://assetstore.unity.com/packages/3d/animations/rpg-character-mecanim-animation-pack-free-65284) |
| Treants, boss variants, four zone tints | **Treant Monster Creature - Fantasy RPG** by Infinity PBR | itch.io, from $55 | Already named; includes many animations, multiple textures, and blend-shape morphing for visual variation—good for water/earth/fire/wind treants.  [infinitypbr.itch](https://infinitypbr.itch.io/treant-unity) |
| Broad missing SFX families | **RPG & Fantasy Sounds Bundle** by PlaceHolder Inc. | Unity Asset Store, $44.99 | Already named; covers fantasy/RPG sound categories and is inexpensive relative to the amount of audio you need.  [assetstore.unity](https://assetstore.unity.com/listing?f-ec_category=audio+%3E+sound-fx) |
| Item, resource, fish, tool, weapon, essence icons | **Voxel Icons and Items Megapack** by monogon | itch.io | 400+ voxel-style icons/items, usable as UI icons or world objects; best starting point for your inventory/finds/fish icon pass.  [itch](https://itch.io/s/177442/48-packs-thousands-of-voxel-assets-one-bundle) |

## Best bulk voxel packs

| Your need | Best match | Where | Notes |
|---|---|---|---|
| Weapons, tools, nature, buildings, props, boats, interior pieces | **48 Packs! Thousands of VOXEL Assets! One Bundle!** by monogon | itch.io | Covers modular weapons/props, environments/buildings, interiors, animals, ships/boats, and source `.vox` files; likely the best single purchase for filling many “need” meshes in one visual language.  [itch](https://itch.io/s/177442/45-packs-thousands-of-voxel-assets-one-bundle) |
| Standalone nature/props/tools/characters | **Fantasy Voxel Pack** by Vedia Games | itch.io, from $4.47 | Includes weapons/tools, nature, houses, furniture, characters; has `.vox` sources, FBX/OBJ/GLB/DAE, Unity prefabs, and Mixamo-ready characters.  [vediagames.itch](https://vediagames.itch.io/fantasy-voxel-pack) |
| Dungeon/crypt dressing and mine props | **Voxel Dungeon Crawler** by monogon | itch.io | Large dungeon/RPG prop collection; use it for crypt pillars, rubble, chains, grates, barrels, mine dressing, and similar filler.  [itch](https://itch.io/s/177442/48-packs-thousands-of-voxel-assets-one-bundle) |
| Boats: raft, rowboat, longship, oars, masts, docks | **Voxel Pirates and Ships** by monogon | itch.io | Explicitly includes modular boats and buildings; better fit than buying individual boat models.  [itch](https://itch.io/s/177442/45-packs-thousands-of-voxel-assets-one-bundle) |
| Fishing-specific props | **Voxel Fishing Pack** | itch.io, $30 | Worth checking for rod, bobber, net, bucket, drying rack, and shore/fishing dressing; inspect its style before buying because it may not match your voxel language.  [itch](https://itch.io/game-assets/tag-boat) |

## Focused gap fillers

| Your need | Candidate | Where | Recommendation |
|---|---|---|---|
| Pick, sword, mace, spear, two-hander, hammer, bow, knife | **900 Low Poly Fantasy RPG Weapons - Ultimate 3D Pack** by Pure Poly | Unity Asset Store, $99.95 | Broad weapon coverage, but inspect silhouette/poly density; your bear axe should remain the visual anchor.  [assetstore.unity](https://assetstore.unity.com/packages/3d/props/weapons/900-low-poly-fantasy-rpg-weapons-ultimate-3d-pack-247005) |
| Crypt interiors: pillars, sarcophagi, urns, statues, rubble | **Catacombs & Crypts Interiors Kit** by 3DForge | Unity Asset Store, $19.98 | Direct match for the crypt dressing list, though it may need retex/recolour to match voxel style.  [assetstore.unity](https://assetstore.unity.com/listing?f-ec_category=3d+%3E+environments+%3E+dungeons) |
| Generic dungeon kit | **Modular Low Poly Dungeon Environment & Props Kit** | Unity Asset Store | Useful alternative if the crypt kit’s style is too realistic; still expect tinting/retexturing.  [assetstore.unity](https://assetstore.unity.com/packages/3d/environments/dungeons/modular-low-poly-dungeon-environment-props-kit-188377) |
| Mining tools, ore, gems, cave props | **Ultimate Low Poly Mining, Cave & Blacksmith Pack** by Pure Poly | Unity Asset Store | Good candidate for mine dressing, support beams, rubble, crystals, tools, and blacksmith/building props.  [assetstore.unity](https://assetstore.unity.com/publishers/49700?aid=1100liZev) |
| Ambient critters | **Voxel Animals** by monogon | itch.io | Includes 60+ animable animal assets; use for birds, fish-school stand-ins, and simple biome life.  [itch](https://itch.io/s/177442/48-packs-thousands-of-voxel-assets-one-bundle) |

## What I would not buy separately

- **A second character pack** — your existing Chopper body plus the Explosive animation pack should cover it.
- **A realistic environment pack** — it will clash with the voxel/chunky concept sheets.
- **Individual trees, rocks, bushes, stumps, or logs** — your existing world kit plus tint variants should handle these.
- **A separate audio pack per category** — start with the named RPG & Fantasy Sounds Bundle, then buy only for missing families such as fishing, mining, boats, weather, and crafting. [assetstore.unity](https://assetstore.unity.com/listing?f-ec_category=audio+%3E+sound-fx)

## Suggested purchase order

1. **Explosive RPG Character Mecanim Animation Pack** — unlocks dodge, mining, fishing, drawing/holstering, hit reactions, and death. [assetstore-fallback.unity](https://assetstore-fallback.unity.com/publishers/3415)
2. **Treant Monster Creature** — covers enemies, bosses, and four biome variants through tint/morph work. [infinitypbr.itch](https://infinitypbr.itch.io/treant-unity)
3. **RPG & Fantasy Sounds Bundle** — fills the largest number of empty audio families cheaply. [assetstore.unity](https://assetstore.unity.com/listing?f-ec_category=audio+%3E+sound-fx)
4. **monogon 48-pack voxel bundle** — likely covers weapons, tools, boats, crypt/mine dressing, furniture, shore props, and icons in one purchase. [itch](https://itch.io/s/177442/48-packs-thousands-of-voxel-assets-one-bundle)
5. **Voxel Icons and Items Megapack** — handles the icon pass after meshes exist. [maxparata.itch](https://maxparata.itch.io/voxelicons/devlog/844373/new-asset-pack-voxel-icons-and-items)
6. Only then add **Catacombs & Crypts**, **Mining/Cave pack**, or a dedicated **fishing pack** if the monogon bundle leaves visible holes. [itch](https://itch.io/game-assets/tag-boat)

## Practical matching rule

For every purchased mesh, apply this filter before importing:

- Same chunky voxel silhouette as the bear axe and hut.
- Flat or low-detail texture work; avoid PBR-normal-map-heavy assets.
- Recolour rather than replace: zone pines, treants, cloaks, crypt dressing.
- One Humanoid rig: purchased clips must retarget to Chopper without renaming your animator states.
- Prefer packs with `.vox`/FBX source files so you can edit silhouettes rather than accepting mismatched props. [itch](https://itch.io/s/177442/48-packs-thousands-of-voxel-assets-one-bundle)

If you want, I can next turn this into a exact **Unity package import checklist**—which prefab folders to pull from each pack, which meshes to recolour, and which assets to leave out.

------------

turn this into a exact Unity package import checklist—which prefab folders to pull from each pack, which meshes to recolour, and which assets to leave out.

------------

Below is the import checklist. Exact subfolder names vary by package version, so treat the quoted paths as **search filters inside each imported package**, not literal guarantees; import the package, then pull only the listed prefab/model types into `Chopper/Art/Purchased/`.

## Global import rules

- Import every package into `Assets/Chopper/Art/Purchased/<Publisher>/<PackName>/`, never directly into production folders.
- After import, create `Chopper/Art/Game/` copies of only approved prefabs/meshes; leave purchased originals untouched.
- Use one shared material atlas/palette where possible: wood, steel, stone, cloth, leather, foliage.
- For every recolour, duplicate the material—not the mesh—unless the silhouette itself must change.
- Reject any asset that introduces realistic PBR detail, high-poly silhouettes, or a second character style.

## 1. Explosive — RPG Character Mecanim Animation Pack

**Purpose:** Humanoid clips only. Do **not** import its character models, controllers, or demo scenes as game art.

| Pull | Use |
|---|---|
| `Animations/Locomotion` | Dodge/roll, crouch transitions, directional movement if useful |
| `Animations/Combat` | Hit reactions by direction, stagger, get-up, death |
| `Animations/Weapons` or weapon-hand clips | Draw, holster, pickup, mining strikes, two-handed swings |
| `Animations/Interaction` / `Action` | Sit, lie in bed, workbench use, fishing cast/wait/reel if present |
| `Animations/Emotes` | Wave, point, ground sit |

**Leave out:**
- Demo character prefabs and character-controller scripts.
- Its humanoid mesh, materials, and textures.
- Any clip that duplicates your existing locomotion, swim, chop, or KayKit combat set.

**Unity setup:**
- Set all imported clips to **Humanoid**.
- Do not rename clips in the source folder; create Animator State aliases matching your existing controller names.
- Retarget onto Chopper; keep your existing `Locomotion`, `Swim`, `Chop`, and `Combat` states.

## 2. Infinity PBR — Treant Monster Creature

**Purpose:** One enemy rig, scaled and tinted into all forest variants.

| Pull | Use |
|---|---|
| Treant base prefab/model | Small, medium, large, and boss treants |
| Animation clips: idle, walk, attack, hit, death | Crypt guardian and standard enemies |
| Boss/heavy clips: windup, slam, roar, fall | Boss phases |
| Texture variants / blend-shape presets | Zone and individual variation |
| Included SFX, only if they fit | Enemy idle, step, attack, hit, death |

**Recolour / variant plan:**

| Variant | Mesh treatment | Material treatment |
|---|---|---|
| Night-wood treant | Base mesh | Dark bark, sparse pale foliage |
| Water treant | Add reed/moss scatter meshes | Pale desaturated bark, dripping green |
| Earth treant | Thicker root/root-arch attachments | Mossy brown, heavy moss |
| Fire treant | Charred branch attachments | Black bark, ember emissive cracks |
| Wind treant | Remove dense foliage, add tattered cloth | Bare grey-brown branches |
| Boss per forest | Scale up; add one silhouette prop | Zone material + unique emissive/accent |

**Leave out:**
- Its realistic texture set if it fights your flat voxel look—replace with a simplified bark/foliage atlas.
- Extra creature scripts, demo scenes, and unrelated Infinity PBR support files.
- Any second enemy species; do not turn this into an undead/bandit army.

## 3. PlaceHolder Inc. — RPG & Fantasy Sounds Bundle

**Purpose:** Fill empty audio families only.

| Pull | Map to |
|---|---|
| Creature/monster SFX | Treant idle, step, attack, hit, death |
| Fishing/water SFX | Cast, bite, reel, splash, snap, wading, swimming |
| Mining/stone SFX | Pick strike, vein break, find chime |
| Dungeon/cave ambience | Crypt ambience, mine ambience, drips |
| Movement SFX | Roll, jump, land |
| Weather SFX | Rain, wind, night ambience, birds, fire crackle, thunder |
| Crafting/building SFX | Craft start/finish, place piece |
| Boat SFX | Creak, oar, sail |

**Leave out:**
- Music tracks unless one clearly replaces or supplements your existing day/night/title/combat music.
- UI sounds—you already have click/hover/upgrade.
- Weapon sounds already covered by your axe/shield library.

## 4. monogon — 48-Pack Voxel Bundle

Do **not** import the whole bundle into the project. Buy/download it, but import only these sub-packages or folder groups.

### `Voxel RPG Characters And Weapons`

| Pull | Use |
|---|---|
| Weapons: sword, mace, spear, two-handed axe/sledge, pick, hammer, bow, knife | `Weapon_R` socket meshes |
| Tool props | Pick, hammer, fishing-rod base if usable |

**Recolour:** Match steel to the bear axe; wood handles to hut planks.

**Leave out:**
- All 12 skinned voxel heroes.
- Character accessories and armour that conflict with Chopper’s existing outfit.
- Any weapon whose silhouette is more detailed than the bear axe.

### `Voxel Fantasy Weapons`

| Pull | Use |
|---|---|
| Sword, mace, spear, battleaxe/sledge, bow, knife | Combat/tool weapon alternatives |

**Leave out:**
- Magical/glowing weapons unless repurposed as later elemental gear.
- Staffs, wands, and spell effects.

### `Voxel Pirates and Ships`

| Pull | Use |
|---|---|
| Small boat/rowboat hull | Rowboat |
| Raft or simple flat hull | Early-coast raft |
| Larger ship hull | Longship base |
| Oars, rudder, mast, sail, rope coil | Boat equipment |
| Dock/mooring posts | Shore dressing |

**Recolour:** One shared hull plank material; longship gets zone-wood accents, not a new art style.

**Leave out:**
- Pirate characters, cannons, flags, treasure, and modern/steampunk ship parts.
- Any hull too detailed or curved for the voxel language.

### `Voxel Dungeon Crawler`

| Pull | Use |
|---|---|
| Pillars, rubble, broken statues, urns, sarcophagi, chains, grates | Crypt dressing |
| Stone doors/slabs | Crypt entrances and boss doors |
| Braziers, dais/platform pieces | Boss arena |
| Barrels, crates, shelves, stools, rugs | Player-building filler |
| Mine props: support beams, carts, rails, lanterns | Mine dressing if better than current kit |

**Recolour:**
- Stone: cool grey for crypt; warmer brown-grey for mines.
- Fire crypt: add scorch decals/emissive cracks.
- Water crypt: add drip-stone and pale moss.
- Earth crypt: root breaches and moss carpets.
- Wind crypt: cracked plaster and bare-root dressing.

**Leave out:**
- Enemies, traps, loot chests if your existing chests are already final.
- Gothic/horror props that read as a different game.

### `Voxel Medieval Town`

| Pull | Use |
|---|---|
| Doors, gates, palisades, fences, beams, posts, ladders | Building pieces |
| Barrels, crates, shelves, stools, rugs, map table | Interior/furniture |
| Signposts, market/shore props | Hub and coast dressing |

**Leave out:**
- Town buildings, NPCs, vehicles, and large environment kits—your generator already owns world structure.

### `Voxel House Interior`

| Pull | Use |
|---|---|
| Bed, chest variants, shelves, stools, rugs, barrels, crates | Interior fill |
| Fireplace accessories, hanging items | Hut polish |

**Leave out:**
- Modern furniture, appliances, bathrooms, and full room kits.

### `Voxel Virgin Island` / `Voxel Nature Pack: Rocky Mountains`

| Pull | Use |
|---|---|
| Beach grass, driftwood, shells, wet-sand props | Shore ring |
| Reeds, lily pads, marsh plants | Water forest |
| Moss carpets, root arches, boulder stacks | Earth forest |
| Bare trees, bent shrubs, pale stones | Wind forest |
| Snow caps/rocks | High-ring optional |
| Waterfall pieces | Short tiled waterfall |

**Recolour plan:**

| Zone | Tint |
|---|---|
| Hub | Existing warm green/brown |
| Earth | Deep moss green, brown bark, grey stone |
| Water | Pale blue-green, wet bark, reed green |
| Fire | Charcoal, ash grey, ember orange |
| Wind | Bare grey-brown, pale stone, faded cloth |

**Leave out:**
- Tropical palm trees, modern beach items, and any dense realistic foliage.
- Duplicate pines/rocks—you already have the core forest kit.

### `Voxel Animals`

| Pull | Use |
|---|---|
| Bird | Ambient forest critter |
| Fish / fish-school stand-in | Water ambience |
| Firefly or small glowing insect | Night/meadow ambience |

**Leave out:**
- Mounts, pets, livestock, and combat animals.

### `Voxel Icons and Items Megapack`

| Pull | Use |
|---|---|
| Wood, fibre, herb, ore, shard, essence icons | Inventory/resources |
| Tool/weapon icons | Hotbar |
| Food/drink icons | Future eating/mead |
| Fish icons | 32-fish set; recolour/relabel rather than buying another icon pack |
| Map/cursor icons | Map and reticle if style matches |

**Leave out:**
- Icons whose style is pixel-art rather than voxel; do not mix icon languages.
- Duplicate icons for assets you already have final art for.

## 5. Optional focused packs

### Catacombs & Crypts Interiors Kit

| Pull | Use |
|---|---|
| Sarcophagi, urns, pillars, rubble, statues, chains, grates | Crypt dressing |
| Stone doors/slabs | Crypt progression |
| Braziers and dais pieces | Boss arena |

**Leave out:** Full room modules, realistic wall/floor textures, and any prefab that replaces your existing block crypt shell.

### Ultimate Low Poly Mining, Cave & Blacksmith Pack

| Pull | Use |
|---|---|
| Pick, hammer, anvil/blacksmith props | Tool and crafting visuals |
| Ore veins, gems/crystals, support beams, rubble | Mine dressing |
| Cave props | Flooded sump, cave-in rubble |

**Recolour:** Crystals to aquamarine, sulfur, amber, and storm-blue; keep glow as emissive, not bloom-heavy.

**Leave out:** Blacksmith building, realistic tools, and any NPC.

## Import order

1. **Explosive animations** — import clips only; retarget and test dodge/mine/fish/hit first.
2. **Treant** — import base rig/animations; build the five variants and boss scale.
3. **Sounds bundle** — import audio only; wire missing families.
4. **monogon selected packs** — import weapons, boats, dungeon, nature, interior, animals, icons.
5. **Crypt/Mining focused packs** — only where monogon leaves a hole.
6. **Icons last** — after meshes and item IDs are stable.

## Rejection checklist

Before an imported prefab enters `Chopper/Art/Game/`, it must pass:

- [ ] Reads as voxel/chunky beside the bear axe and hut.
- [ ] Uses flat/simple texture detail; no photoreal normal maps.
- [ ] Can be recoloured through material duplication.
- [ ] Fits the existing scale: 2 m building pieces, player-height props.
- [ ] Does not add a new character race, realistic human, or second art style.
- [ ] Has a clear gameplay destination—weapon socket, generator scatter, crypt dressing, UI icon, or audio event.

The monogon bundle is large—11,300+ prefabs and 7,345 unique models—so selective import is essential; its included Unity packages and `.vox` sources make it usable as a source library rather than a drop-in project dependency.  The Treant package ships as a single 951 MB Unity package with multiple textures and blend-shape morphing, so import it once and derive variants through materials, attachments, and morphs. [itch](https://itch.io/s/177442/48-packs-thousands-of-voxel-assets-one-bundle)


- [https://assetstore.unity.com/packages/3d/animations/rpg-character-mecanim-animation-pack-25912](https://assetstore.unity.com/packages/3d/animations/rpg-character-mecanim-animation-pack-25912)
- [https://assetstore.unity.com/packages/3d/animations/rpg-character-mecanim-animation-pack-free-65284](https://assetstore.unity.com/packages/3d/animations/rpg-character-mecanim-animation-pack-free-65284)
- [https://infinitypbr.itch.io/treant-unity](https://infinitypbr.itch.io/treant-unity)
- [https://assetstore.unity.com/packages/audio/sound-fx/rpg-fantasy-sounds-bundle-250599](https://assetstore.unity.com/packages/audio/sound-fx/rpg-fantasy-sounds-bundle-250599)
- [https://itch.io/s/177442/48-packs-thousands-of-voxel-assets-one-bundle](https://itch.io/s/177442/48-packs-thousands-of-voxel-assets-one-bundle)
- [https://maxparata.itch.io/voxelicons](https://maxparata.itch.io/voxelicons)
- [https://mrmgames.itch.io/voxel-fishing-pack](https://mrmgames.itch.io/voxel-fishing-pack)
- [https://mrmgames.itch.io/voxel-fish-pack](https://mrmgames.itch.io/voxel-fish-pack)
- [https://assetstore.unity.com/packages/3d/props/weapons/900-low-poly-fantasy-rpg-weapons-ultimate-3d-pack-247005](https://assetstore.unity.com/packages/3d/props/weapons/900-low-poly-fantasy-rpg-weapons-ultimate-3d-pack-247005)
- [https://assetstore.unity.com/packages/3d/environments/dungeons/catacombs-crypts-interiors-kit-209297](https://assetstore.unity.com/packages/3d/environments/dungeons/catacombs-crypts-interiors-kit-209297)
- [https://assetstore.unity.com/packages/3d/environments/dungeons/modular-low-poly-dungeon-environment-props-kit-188377](https://assetstore.unity.com/packages/3d/environments/dungeons/modular-low-poly-dungeon-environment-props-kit-188377)
- [https://assetstore.unity.com/packages/3d/props/tools/ultimate-low-poly-mining-cave-blacksmith-pack-ores-gems-props-to-269631](https://assetstore.unity.com/packages/3d/props/tools/ultimate-low-poly-mining-cave-blacksmith-pack-ores-gems-props-to-269631)

The direct purchase links above cover the animation, treant, audio, bulk voxel, icon, fishing/fish, weapon, crypt, dungeon, and mining gaps. The monogon bundle is the broadest purchase; the fishing and fish packs are separate because their fish/animal assets are not included in the fishing pack. [itch](https://itch.io/s/118349/new-release-voxel-fishing-pack)