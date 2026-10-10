# Chopper: art, audio and store assets to get

Last updated: 2026-10-06 (candidates from the asset search added; links checked on their pages unless marked "not checked"). Rewritten from scratch as the shopping list for turning the prototype look into a real survival crafting game. The previous version (free download plan, the Copilot clean up) is in `Docs/archive/2026-10-02/` and `Docs/archive/2026-10-05/`.

Everything below is something the game shows today that was drawn or built in code as a stand-in, or reused from something else. None of it should ship in the Steam demo.

## How to hand assets over

- Put each pack in `Assets/ThirdParty/<Publisher>_<PackName>/` with its license file, then tell me the folder name. I wire it in and list it in `Assets/ThirdParty/CREDITS.md`.
- Downloads, zips and source files (`.blend`, `.psd`, `.unitypackage`, raw music) go in `Chopper/SourceAssets/`, outside `Assets/`, so Unity never imports them. That folder is not in git.
- Only buy or download what allows a commercial game. Asset Store and Fab purchases do (single entity license). For anything else, CC0 is easiest; CC BY needs a credit line (tell me the exact credit).
- Before the Steam demo, every asset needs a known license. **Settled 2026-10-06:** the "100+ Pixel Lowpoly Assets 3D" pack (trees, rocks, house parts, swords) is "100+ Pixelated Lowpoly Assets 3D" by Markus Darius, https://markusdarius.itch.io/100-pixelated-lowpoly-assets , **CC0** (checked on the page). Its six swords are now free to use.

## 0. Decide the art direction first

Pick one direction and buy only into it; mixing styles is what makes the game look amateur today (pixel textured pack models, smooth Quaternius characters, and shapes made in code).

| Option | What it means | Fits what we have |
|---|---|---|
| **A. Valheim style (my recommendation)** | Low poly models with low resolution, slightly pixelated textures, muted earthy colors, and heavy lighting, fog and shadows doing the mood. Rough and handmade, not cute. | Keeps the pixel pack world, the lighting, the fog and the water. Replaces the characters, the enemies, the icons and every code-built prop. |
| B. Stylized realism | Hand painted, higher detail models (Synty-like shapes are too toy-like for this). | Means replacing the world too: the trees, the rocks and the house. Much more to buy. |

Everything below assumes **A**. Say if you want B and I will rewrite the list.

## 1. Interface: icons and HUD markers (top priority, cheapest win)

Today the icons are drawn in code from a few shapes (the hammer, axe, fists, torch, rod, fish, iron, shard, essence), and status effects are colored squares with a word in them ("Wet", "Fed").

**What to get:** one icon set, all in the same painted or engraved style, matching the Cinzel gold and dark brown menus. Each icon a **256 by 256 px PNG with a transparent background**, readable at 64 px.

| Group | Icons needed | Shown today as |
|---|---|---|
| Tools and hotbar | Axe, fists (unarmed), torch, bandage, fishing rod, fish (a generic one), hammer, sword (for the planned weapons) | Code drawings; the bandage is a render of a code-built model |
| Materials | Wood, healing herb, fiber, crypt iron, rune shard, essence (one icon, tinted per element, or four) | Renders of game models (wood, herb, fern) and code drawings (iron, shard, essence) |
| Fish | One per species (32 kinds), or one fish shape tinted by rarity | One code drawn fish, tinted |
| Status effects | Wet (a water drop), Fed (a meal), Rested (later), four element charges (water, fire, earth, wind), cold or burning if added | Colored squares with "Wet" and "Fed" written in them; plain colored squares for the elements |
| Building pieces | Floor, wall, doorway, roof, stairs, workbench, standing torch, campfire (and every new piece) | Names written on a plain tile |
| Stations and upgrades | Workbench, chest, bed, upgrade (axe, armor, hut), forge (sturdy gear, rune axe, ward, element stone), repair | Text rows only |
| Map | House, crypt door, player arrow, other players, death marker | Code drawings |

**Where:** search the Unity Asset Store or Fab for "survival icons", "RPG item icons painted", "fantasy crafting icons" and check the list covers wood, herbs, fiber, ore and tools. For status effects only, game-icons.net has clean silhouettes (CC BY 3.0, needs a credit line) that look right on a dark round badge. A commission (Fiverr, ArtStation) for about 60 icons in one style is the most consistent option.

**Candidates (2026-10-06):**

| Pack | Where | Price | Notes |
|---|---|---|---|
| Survival & Crafting Icon Pack Vol. 1 (B & B Creations) | [Asset Store](https://assetstore.unity.com/packages/2d/gui/icons/survival-crafting-icon-pack-vol-1-321948) | 8.99 USD | Cheapest start. The store page lists no contents or style: check the screenshots cover axe, wood, herbs, fish, ore and hammer before buying |
| Survival Icons Megapack (REXARD) | [Asset Store](https://assetstore.unity.com/packages/2d/gui/icons/survival-icons-megapack-238993) | 59.99 USD | Large set, one style (not checked) |
| Crafting Materials Icons: Survival RPG Resources, Herbs, Minerals | [Asset Store](https://assetstore.unity.com/packages/2d/crafting-materials-icons-survival-rpg-resources-herbs-minerals-f-336274) | 24.99 USD | Materials only (not checked) |
| 5800 Fantasy RPG Icons Pack, 5000 Fantasy Icons (PONETI) | Asset Store, search the names | varies | Big hand painted sets (not checked) |
| game-icons.net | already in the game | free, CC BY 3.0 | Fine for the status badges (wet, fed, elements) on a dark round badge |

Buy one pack and stay in it: two icon styles side by side look worse than code drawings.

**HUD frames (optional, same set if possible):** hotbar slot frame, selected slot frame, a round badge for status effects, the health and stamina bar frames, a crosshair. Today these are plain colored boxes.

## 2. Characters

### 2a. Chopper (the player model)

Today Chopper is pieced together from Quaternius modular parts with the body hidden under them. The shirt has short sleeves built into the mesh, gaps show through, and the hands hold items with a code-built grip. The concept (`original-chopper-concept-male.png` in the repo root) is the target.

| Part | Wanted |
|---|---|
| Build | Short, broad woodsman; grounded proportions, not cartoon |
| Head | Full dark brown beard and mustache, short dark hair |
| Hat | Decide: none (as now) or a brown wide-brimmed hat, part of the model |
| Clothes | Red and black buffalo plaid flannel with long sleeves, dark suspenders or a belt with a plain buckle, blue work trousers, dark brown boots |

The file needs one mesh (or parts that fit with no gaps) with a full body underneath, a **Humanoid** rig, 5,000 to 15,000 triangles, one 1024 or 2048 px texture, matte. FBX.

Candidates (2026-10-06):

- **Low Poly Lumberjack 3D, Rigged Character** by Backrock Studios, [Asset Store](https://assetstore.unity.com/packages/3d/characters/low-poly-lumberjack-game-ready-character-308470), 9.99 USD, URP. The page does not say whether the rig is Humanoid, or describe the beard and shirt: check the screenshots and the Package Content tab.
- **Puppet Lumberjack Pack** by Joao Baltieri, [Sketchfab store](https://sketchfab.com/3d-models/puppet-lumberjack-pack-35beed590afb4a148334e41d00c7755b) (also on the Asset Store): 6k triangles, Mecanim ready, an axe (286 triangles), logs and a chopping stump, with idle, walk, run, jump and **chopping wood** animations. The axe already held in its animations makes it a second reference for 3a. Its look is a puppet, so check it is not too cartoon.
- Also seen, not checked: "3D Character #042 LumberJack Male" and "Rigged Low Poly Cartoony Lumberjack Character" on the Asset Store.

Ways, cheapest first: an image to 3D tool (Meshy, Tripo or Rodin) from the concept, then Mixamo's auto-rigger (expect to clean the hands and beard; check the tool's commercial license); a commission with the concept and this table; or a close "lumberjack" or "woodsman" character from Fab or the Asset Store, recolored.

### 2b. The night tree creature (chosen: Monsterlings Elderwood Guardian)

Buy and import it as described in `elderwood-recolor-guide.md` (folder `Assets/ThirdParty/Monsterlings_ElderwoodGuardian/`). It has idle, walk and death only. Still needed for it: **an attack, a hit reaction, and rising out of the ground** (the sapling waking). If it is Humanoid, Mixamo attacks can be retargeted; otherwise ask the publisher or a commission.

Until that pack is in the project, an awake forest enemy is a stand-in built by `GolemSetup`: the animation rig wearing the base body in the pine's bark, with that element's pine foliage as a crown. It walks, punches, flinches and falls. Saplings stay pines. Crypt and arena guardians stay the draugr. Replace this the moment the Elderwood Guardian arrives.

### 2b2. The boss treants (five, free assets to find)

There is no separate warden creature. A forest boss is the current night tree enemy made bigger (the `GolemSetup` stand-in scaled by `Treant.CurrentHeight`, with its element's crown and glow). That is the stand-in until real boss models are found. Five are needed:

| Boss | Look to aim for |
|---|---|
| Fire treant | charred black bark, glowing ember cracks, red and orange crown |
| Water treant | wet dark bark, moss and hanging weed, blue green crown |
| Earth treant | thick root legs, stones grown into the bark, yellow brown crown |
| Wind treant | tall and thin, pale birch bark, light green crown that streams in the wind |
| Plain treant (no element) | an old oak giant, plain brown bark and green crown, the neutral boss |

What each one needs: free for commercial use (CC0 best, CC BY fine with a credit line), rigged, with at least idle, walk or run, an attack, a hit reaction and a death; rising out of the ground is a bonus (it is the sapling waking). Low poly or stylized, so it sits with the Quaternius and pixel pack art. One base model recolored five ways is fine (see `elderwood-recolor-guide.md` for how a recolor is done).

Candidates found so far (2026-10-05, checked on their pages):

- **Treant Pack** by Tennessippi Studios, https://tennessippistudios.itch.io/treant-pack . CC0, free. Two stylized tree creatures, light and dark versions, FBX and OBJ. Animations: Attack01 to 03, Death01 to 03, Idle, Dance, Run, Taunt. No walk or hit reaction (Run can be slowed for a walk). Best first try.
- **Tree Monster Animated** by DreamPlayStudios, https://www.highend3d.com/3d-model/tree-monster-animated-3d-model . CC BY 4.0 (needs a credit), free. FBX among others. Animations: Come Up From Under Ground, Idle, Idle Look Around, Right Hook Attack, Run, Two Handed Attack, Two Handed Downward Attack, Walk. Has the rising out of the ground. Check its look and polygon count before buying into it.
- Still to search: Quaternius (quaternius.com, all CC0; no tree monster seen yet), Kenney, OpenGameArt (filter CC0), Sketchfab (filter downloadable, CC0 or CC BY, "treant" or "ent"), Poly Pizza.

Drop downloads in `Chopper/SourceAssets/BossTreants/` and tell me which one; I import it, recolor it per element and swap it in for the bosses, keeping the sapling to awake switch.

### 2c. Crypt guardians and arena bosses

They are Quaternius draugr wearing the village clothes, glowing in their element. Fine for now; replace when a matching undead pack is found (same style as 2a).

## 3. Weapons, tools and their animations (planned: axe for logging, swords for fighting)

| Item | Today | Wanted |
|---|---|---|
| Axe | Stylized Woodcutting Axe by Price (OpenGameArt, CC0), 2026-10-06; grip and back placement guessed in code | **Not done: see 3a.** The model is fine, but how it sits in the hand and on the back is computed in code and keeps coming out wrong. Tiers later (stone, iron, rune) in the same look |
| Swords (new) | Six swords in the pixel pack (CC0, settled 2026-10-06) | Use them. One-handed, grip and back holster set from the 3a reference |
| Hammer (building) | Not in the hand at all; only a code drawn icon | A wooden mallet or carpenter's hammer model, held while building. Search "carpenter hammer mallet low poly" |
| Fishing rod, line, float | Built from cylinders and spheres in code | **Found:** [Fishing Rod and Reel (Quiet Lake Fishing)](https://3dassets.dev/assets/quiet-lake-fishing-fishing-rod-25b95ce5) by 3D Assets, CC0, free, GLB, 2,248 triangles: curved rod, line guides, spinning reel, cork grip and a float. Download the GLB to `Chopper/SourceAssets/Packs/QuietLake_FishingRod/`; I convert it in Blender |
| Torch | Built from shapes in code with the pack's pixel flames | A torch model (the flames can stay) |
| Shield | None | Optional, if blocking moves to a shield |

Animations needed (Humanoid, so they retarget to the new character): tree chopping (side swing), a sword combo of three, block and block hit, a hammer swing (placing a piece), casting and reeling, eating, lying down to sleep, and getting hit. Mixamo has chopping, sword sets and most of the rest for free (download as FBX for Unity, Without Skin, to `Assets/ThirdParty/Mixamo_<Set>/`).

### 3a. A proper reference: a character holding an axe (top priority, 2026-10-06)

The problem: the axe's place in the hand and on the back is worked out in code from the mesh (`CharacterSetup.MeasureAxe`, `PlayerAxe.ApplyCarry`, `PlayerAxe.PlaceBackAxe`), and every new axe or pose breaks it. On 2026-10-06 the new axe went on the back turned the wrong way, blade into the spine and clipping through the body. Real games do not compute this: an artist sets it once, either in the animation (the clip is made holding the axe) or as two fixed attach points on the skeleton, a hand grip and a back holster.

What to download (any one of these, in this order of preference):

1. **An axe animation set made for a held axe, with sheath and unsheath.** The clips then show exactly where the hand holds the handle and where the axe rests on the back, and Chopper can copy those positions instead of guessing.
   - [Axe Combat Animation V1](https://gamedev-hero.itch.io/axe-combat-animation-v1-uemixamo) by Game Dev Hero, about 20 USD: 361 animations (idles, walk and run with the axe, attacks, **sheath and unsheath**), Unity Humanoid and Mixamo rigs, FBX, and an axe prop (a Quaternius CC0 mesh) already placed in the hand. The best fit for this problem. Check its license page (itch.io/blog/1578239) before buying.
   - [Woodcutting Animation Pack](https://assetstore.unity.com/packages/3d/animations/woodcutting-animation-pack-345850) by The Mighty Cat Games, 18.99 USD on the Unity Asset Store: made for chopping trees. Check the Package Content tab for a held axe idle and run, and a put away clip.
2. **Free, animations only:** [Human Melee Animations FREE](https://kevdev.itch.io/human-melee-animatons-free) by Kevin Iglesias (name your own price; take the Unity Asset Store copy for Humanoid): idles, runs and attacks for one-handed axes. No props and no sheath clip, so the back holster would still be set by hand.
3. **Mixamo (free, Adobe login):** search "axe". Download "FBX for Unity", "Without Skin", into `Assets/ThirdParty/Mixamo_Axe/`.

What I need from the download: the FBX files (put the archive in `Chopper/SourceAssets/Packs/`), plus one picture of a character holding the axe and one with it on the back, if the pack has them. Ideal assets show the axe with its handle in the fist, the blade facing forward, and on the back the blade flat against the body, head up over one shoulder, edge facing out.

Note for the import: Chopper's rig is Generic (the Quaternius Universal Animation Library). Humanoid clips need the player rig switched to Humanoid, which section 2a's new Chopper model needs anyway. Until then the clips are used as a reference for the grip and holster, set as fixed offsets.

## Buy or download first (2026-10-06)

1. **The axe reference (3a):** Axe Combat Animation V1 (about 20 USD), or the free Human Melee Animations plus Mixamo. Fixes the hand and back placement for good.
2. **Free, grab now:** the fishing rod (CC0), the Treant Pack (CC0, 2b2), the free fire (Vefects).
3. **One icon set** (section 1).
4. **Chopper's model** (2a): a lumberjack pack, or the concept through image to 3D.
5. Workbench and building kit once the above are in.

## 4. World props built in code (replace with models)

| Prop | Built today from | Wanted |
|---|---|---|
| Workbench | The pack's plain table (code props were removed) | A carpenter's workbench with a vice and tools. Candidates (not checked): [Workshop: Workbench with Tools, Low Poly](https://assetstore.unity.com/packages/3d/props/tools/workshop-workbench-with-tools-low-poly-262357) (Boxx-Games), [Workbench With Tools](https://assetstore.unity.com/packages/3d/props/tools/workbench-with-tools-121021) (2018, PBR, may be too detailed) |
| Building pieces | Floor: the pack's plank slab. Walls and doorway: the house wall piece stretched. Roof: a code-made wedge. Stairs: boxes | A timber building kit: floor, wall, half wall, doorway with an opening door, window, 45 and 26 degree roofs, ridge, stairs, ladder, pillar and beam, all on a 2 m grid. Candidates (not checked): [Modular Building Kit](https://assetstore.unity.com/packages/3d/environments/modular-building-kit-273019), [Low Poly Modular Building Set](https://assetstore.unity.com/packages/3d/environments/industrial/low-poly-modular-building-set-184703) (Alstra Infinite, 13.99 USD, industrial, probably the wrong era). Still searching for timber framing |
| Standing torch | The held torch scaled up | A torch on a post or a wall sconce |
| Campfire | Three cylinders as logs, a flattened sphere as embers, the pack's cube flames | A campfire model with a stone ring and logs; softer fire particles |
| Tree stumps | A cylinder | Stump models, one per tree kind |
| Notice board and signs | Boxes with a blank page | A signpost and a notice board model |
| Ruined hut | Boxes as planks, boards across the door | A few broken plank and beam models |
| Fish under the water | Dark spheres | Fish silhouettes or simple fish models |
| Tutorial training tree | Primitives | Use the creature from 2b, held in place |
| Dropped items | Small copies of the models (fine) | Keep; add a soft glint so they read from afar |

## 5. Effects

| Effect | Today | Wanted |
|---|---|---|
| Fire (torches, campfire, fireplace) | The pixel pack's cube flames | A fire particle set with soft flames, embers and light smoke |
| Hit sparks, wood chips, dust, splashes | One code particle system with square particles | A small set: wood chips, bark dust, sparks, water splash, a tree hitting the ground |
| Rain | Code particles | A rain set with splashes on the ground |
| Saplings waking, wardens rising | Colored bursts | Dirt and root bursts |

Fire candidates (not checked): [Stylized Fire VFX Pack](https://assetstore.unity.com/packages/vfx/stylized-fire-vfx-pack-346022) (FlexUnit, about 10 to 20 USD, Built-in, URP and HDRP, campfire and torch included), or the free [Stylized Fire, URP](https://assetstore.unity.com/packages/vfx/stylized-fire-urp-355834) (Vefects, URP only). Try the free one first.

Still to search: "survival VFX pack" (wood chips, sparks, dust), "rain URP". Check URP support.

## 6. Water

Waves and shore foam are still to do. Route A (I build it): a seamless water normal map and a foam texture (CC0, ambientCG) plus swimming sounds. Route B (buy): Stylized Water 3 for URP with its Underwater add-on. My suggestion is A.

## 7. Sound

Today's effects are good recordings, but many sounds are reused for things they were not made for. Each line below needs its own sound. Name the file as shown and drop it in `Assets/_Chopper/Resources/Audio/` (numbered variations `-1`, `-2` ... play at random).

| File name | For | Reuses today |
|---|---|---|
| `build-place` | Placing a building piece | The wood deposit thud |
| `build-break` | A piece breaking or taken down | Nothing, or the hut hit |
| `build-hit` | An enemy hitting a piece | The hut hit |
| `tool-break` | The axe or rod breaking | The parry clang |
| `repair` | Mending a tool or the hut | The upgrade chime |
| `eat` | Eating a fish | The pickup blip |
| `chest-open`, `chest-close` | The storage chest | Nothing |
| `bed` | Lying down | Nothing |
| `fishing-cast`, `fishing-reel` (loop), `fishing-hook`, `fishing-snap` | Fishing | UI clicks and the block sound |
| `sword-slash`, `sword-hit` | The planned swords | Nothing |
| `enemy-growl`, `enemy-attack`, `enemy-death` | The tree creature (if the pack has none) | Procedural |
| `footstep-stone`, `footstep-wood` | Crypt stone and house floors | Dirt steps |
| `swim-stroke`, `water-exit` | Swimming | The splash |
| `rain-loop`, `wind-gust` | Weather | Nothing (no ambience yet) |
| `ui-open`, `ui-close` | Menus | The UI click |

Kenney (CC0), Sonniss GDC bundles (free, royalty free) and the Asset Store's "survival sound" packs cover most of this.

### Music

The music follows the place and the hour (done in code). Drop `.ogg` files with these names into `Assets/_Chopper/Resources/Audio/` and they play with no code change; until then each mood uses one of the six AlkaKrab tracks.

| Files | When | What to look for | Plays now |
|---|---|---|---|
| `music-hub-1.ogg` ... | The hub by day | Cozy and peaceful: soft folk, lute or harp, a hearth feel | "Odin's Whisper" |
| `music-water-1.ogg` ... | North forest by day | Cool, misty, flowing | "Northern Lights" |
| `music-earth-1.ogg` ... | East forest by day | Deep, rooted, slow drums | "Frostbound Horizons" |
| `music-fire-1.ogg` ... | South forest by day | Warm, restless | "Echoes of Valhalla" (also the title music) |
| `music-wind-1.ogg` ... | West forest by day | Open, airy | "Saga of the Sea Wolves" |
| `music-calm-night-1.ogg` ... | Night, no enemies close | Calm but uneasy | "Throne of the Fjords" |
| `music-fight-1.ogg` ... | Awake enemies within 35 m (also wardens and arenas) | Tense, driving, drums; 2 to 4 minutes, loops cleanly | "Frostbound Horizons" and "Echoes of Valhalla" |

AlkaKrab's other packs keep the same license and sound. Add every credit to `Resources/Audio/CREDITS.txt`.

## 8. Steam demo: store assets and setup

Needed before the demo can go up (you said you will buy the Steam access once fishing, building, crafting and the new night creatures are done):

- **Steamworks:** the Steam Direct fee for the app, then a separate **demo app** under it. I switch `steam_appid.txt` and `BuildTools.SteamAppId` from 480 (Valve's test app) to the real ids and set up the depots.
- **Store art** (Steam's current sizes; check the Steamworks docs on the day, they change): header capsule 920 by 430, small capsule 462 by 174, main capsule 1232 by 706, vertical capsule 748 by 896, library capsule 600 by 900, library header 920 by 430, library hero 3840 by 1240 (no text), library logo 1280 by 720 (transparent PNG), page background 1438 by 810, and a 32 by 32 community icon. The capsules carry the game's name and key art; the logo art (`Chopper/Branding`) is a start but needs a proper key art scene.
- **Screenshots:** at least five, 1920 by 1080 or larger, from the release build, no HUD clutter (console `hud`).
- **Trailer:** 30 to 90 seconds, recorded from the exe (see "Recording" in `playtest.md`).
- **Store text:** short description (about 300 characters), the long description, tags, the content survey, and system requirements (minimum and recommended, measured).
- **Licenses:** every pack's license on file. The pixel pack is settled (CC0). CC BY credits so far: game-icons.net, and "Tree Monster Animated by DreamPlayStudios (CC BY 4.0)" if that boss is used.

## 9. Making your own (Blender and Blockbench)

- **Blender:** Scale 1.0 = 1 meter, +Y up in Unity (FBX export: Forward -Z, Up Y, Apply Transform).
- **Weapons and tools:** origin at the grip, blade or head along +Z. Name them clearly (`Axe_Iron`, `Sword_Short`, `Hammer_Wood`).
- **Characters:** Mixamo's auto-rigger, or keep the bone names of the Universal Animation Library (`Assets/ThirdParty/Quaternius_UniversalAnimationLibrary`), and choose Humanoid in Unity.
- **Textures:** for the Valheim style, small textures (128 to 512 px) with point filtering, muted colors, matte.
- Keep `.blend` and `.bbmodel` sources in `Chopper/SourceAssets/`.
