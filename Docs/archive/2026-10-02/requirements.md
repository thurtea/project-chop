# Chopper: assets and tools still needed

Last updated: 2026-10-02 (audio download list with exact file names added at the top). This is the shopping list for the Valheim plus Minecraft feel. Everything marked "free" is CC0 or has a license that allows use in a commercial game; check each pack's license file and keep it next to the pack.

How to add a pack: unzip it into `Assets/ThirdParty/<Publisher>_<PackName>/` (same style as `Quaternius_MedievalWeapons`), keep its license text in that folder, and tell me the folder name. I wire it in, rebuild the scenes and test it.

## AUDIO DOWNLOAD LIST (current priority, 2026-10-02)

The game now picks recordings up by file name, with no code change. Drop each file into `Assets/_Chopper/Resources/Audio/` with the exact name below (`.wav` or `.ogg` preferred, `.mp3` works). For variations add `-1`, `-2`, up to `-12` (for example `tree-fall-1.wav`, `tree-fall-2.wav`); the game picks one at random each time, which stops repeated sounds feeling robotic. Short effects are measured when first played and brought to the right loudness automatically, so you do not need to match levels. Anything missing keeps its current synthesized placeholder.

Where to look (all free and allowed in a commercial game): freesound.org with the license filter set to "Creative Commons 0", Kenney audio packs (kenney.nl/assets, category Audio, all CC0), Sonniss GDC Game Audio Bundles (royalty free, gdc.sonniss.com), bigsoundbank.com (free license), and for music Kevin MacLeod (incompetech.com, CC-BY: needs a credit line, tell me the track names) or Pixabay music (check each track). Write the source and license of every file into `Assets/_Chopper/Resources/Audio/CREDITS.txt` (one line each) so the release credits are right.

Also still open from earlier: confirm the license of `axe-impact.mp3` and `axe-slash.mp3` (they came from the earlier Chopper project), or replace them with CC0 chops under the same names.

### Most noticeable first

| File name | What it is | Variations | Search terms |
|---|---|---|---|
| `footstep-grass` | one step on grass or meadow | 4 to 8 | "footstep grass", "walking grass" (cut single steps) |
| `footstep-dirt` | one step on a dirt path or forest floor | 4 to 8 | "footstep dirt", "footstep gravel soft", "forest floor step" |
| `footstep-water` | one step wading in shallow water | 3 to 6 | "footstep water shallow", "wading step" |
| `axe-impact` | axe biting into wood (chop) | 4 to 8 | "axe chop wood", "wood chop impact" |
| `tree-fall` | a tree cracking, falling and crashing | 2 to 3 | "tree falling", "tree fall crash", "timber" |
| `wood-pickup` | picking up a stick or a log | 2 to 4 | "wood pickup", "stick pick up", "wood handle" |
| `ambience-wind` | outdoor wind through trees, seamless loop, 30 s or longer | 1 | "wind trees loop", "wind meadow ambience" |
| `ambience-day` | forest by day: birds, leaves, distant life, loop 60 s or longer | 1 | "forest ambience day birds loop" |
| `ambience-night` | night: crickets, light wind, maybe an owl, loop 60 s or longer | 1 | "night ambience crickets loop" |
| `ambience-sea` | waves on a beach, loop 30 s or longer (the world edge is now a sea) | 1 | "waves beach loop", "sea shore ambience" |

### Combat and creatures

| File name | What it is | Variations | Search terms |
|---|---|---|---|
| `axe-slash` | axe swing whoosh | 3 to 5 | "swing whoosh heavy", "axe swing" |
| `shield-block` | blow landing on a wooden shield | 3 to 4 | "wooden shield hit", "shield block wood" |
| `shield-parry` | sharp ringing parry | 2 to 3 | "parry metal ring", "sword clang" |
| `player-hurt` | short grunt of pain | 3 to 5 | "male grunt hurt", "pain grunt" |
| `treant-growl` | a wooden creature groaning (creaks plus a deep voice) | 3 to 5 | "tree creak groan", "monster growl wood", "ent" |
| `treant-die` | the creature collapsing into splinters | 2 to 3 | "wood break crumble", "splinter crack" |
| `hut-hit` | something heavy hitting the hut or palisade | 3 to 4 | "wood door hit", "wooden wall impact" |

### World and interface

| File name | What it is | Variations | Search terms |
|---|---|---|---|
| `wood-deposit` | dropping an armful of logs | 2 | "logs drop", "wood pile" |
| `upgrade` | short satisfying chime for an upgrade or craft | 1 to 2 | "upgrade chime", "craft success" (Kenney RPG audio) |
| `night-horn` | a distant horn or low drum when night falls | 1 | "war horn distant", "viking horn" |
| `dawn-birds` | a short burst of morning birdsong | 1 to 2 | "birds dawn chorus short" |
| `splash` | jumping or falling into water | 2 to 3 | "water splash body", "jump into water" |
| `ambience-marsh` | frogs, insects and water for the Water forest, loop | 1 | "swamp ambience frogs loop" |
| `ui-click` | menu click, soft wood or paper | 1 to 2 | Kenney "UI Audio" or "Interface Sounds" |
| `ui-hover` | very soft tick when a menu button is highlighted | 1 | Kenney "UI Audio" |

### Music (optional, plays only if present; Music volume slider in Settings)

| File name | What it is |
|---|---|
| `music-title` | calm theme for the start screen, loop |
| `music-day` | quiet exploration music, sparse, Valheim style ambient folk, loop |
| `music-night` | tense, low, slow music for the night defense, loop |

Tips: cut effects tight (no silence before the sound starts), keep loops seamless, mono is fine for effects (stereo for ambience and music). Tell me when files are in and I will check levels with `Tools/analyze_sounds.py` and tune.

## ART REVAMP (next): what to find or buy

Decision from the playtest: the world looks cartoony. The ground has no real grass (it is a flat green texture with scattered leaves and tall weeds), the lighting is flat and the colors are too saturated. The target is a gritty, darker low poly look with good lighting, close to Valheim. Budget is open: paid packs are fine. I will remove the current weeds, leaf scatter and tall grass blades once replacements are in, so the list below is what to look for. Check each pack's license for commercial use and for use in a multiplayer game, and note the render pipeline (we use URP, Unity 6).

### 1. Ground and grass (most important)

- A ground cover system that looks like a grass carpet: short dense grass, clover and moss patches, dry grass, fallen needles and leaf litter, small stones and twigs, mushrooms and ferns. Not tall weeds. In Valheim the ground is mostly low dense grass with a few bushes and flowers, and the ground texture carries the detail.
- Search the Unity Asset Store and Fab for: "stylized grass", "low poly grass URP", "grass shader URP", "terrain grass and ground cover", "stylized nature environment URP", "vegetation shader". A good pick has a URP grass shader with wind, distance fade, GPU instancing and works as terrain details (or draws with DrawMeshInstanced). Staggart Creations style shader packs and Synty POLYGON Nature are the kind of thing to look at; confirm the current names and prices.
- Terrain texture sets (4 to 6, tileable PBR, muted): short grass, dry grass, forest floor with needles, mud, wet dark soil, moss over rock, snow or ash optional. Free sources: polyhaven.com, ambientCG.com. Paid: terrain texture packs on the Asset Store marked URP and "stylized" or "realistic low poly".
- Optional: a terrain tool that paints and blends ground (for example Gaia, MapMagic or MicroVerse style tools) if the hand built terrain stays flat looking.

### 2. Trees, bushes, rocks

- Trees with bark texture variation, trunks that read as real wood (pines, birches, oaks, dead trees, stumps, logs, big roots) in a muted palette. Search: "low poly forest pack URP", "stylized trees URP", "Viking forest", "dark fantasy forest". Needs LOD or billboards for the distance.
- Bushes, ferns, blueberry or raspberry style bushes, fallen logs, mossy rocks and boulders, cliff rock pieces, standing stones.
- The Quaternius Stylized Nature MegaKit we have now is too bright and round for the new direction. It can stay for filler.

### 3. Lighting, sky, water, weather

- Skybox or sky system with a real day and night cycle, dawn and dusk color, stars and moon, clouds. Free: polyhaven.com HDRIs (overcast, golden hour, night). Paid: sky packs with a time of day controller for URP.
- Water: a good stylized or realistic water shader for URP (foam, depth color, shore waves, refraction, caustics). This is the weakest free area.
- Volumetric fog or light shaft asset for URP (god rays through trees), a post processing profile pack for a filmic grade (lower saturation, contrast, vignette, film grain, soft bloom).
- Weather: rain, mist and falling leaves particle prefabs, thunder and wind sounds.

### 4. Characters and animation

- Animated Treant and tree monster enemies (4 elemental variants if possible: water, earth, fire, wind) with idle, walk, attack, hit, death. Search the Asset Store and Sketchfab for "treant", "ent", "tree monster". Rigged, with a humanoid or generic rig, and a license that allows games.
- More melee animations that fit an axe: horizontal slash, backhand, overhead chop, kick, block hit reaction, dodge forward and back, stagger, death variants. Mixamo is free (Adobe account) and works with our generic skeleton after retargeting; a paid melee animation pack for the Universal Animation Library skeleton would be easier.
- Character parts: more hair styles and beards (braids, topknot, mohawk, mutton chops, mustache, goatee, long beard), female hair and outfits, cloaks and capes, hoods, gloves, belts and pouches, pauldrons, helmets. The Quaternius packs we have offer only a few hair meshes; a modular fantasy or Viking character pack with a matching skeleton would fix this.
- Shields and weapons in a darker, worn style: wooden round shields with metal bosses, iron and bronze tiers; axes with wear. The current Quaternius weapons work but look clean and toy like.
- Cloth physics: a cape or cloak mesh. I can make a simple one in code, but a proper cloak model from a pack looks better.

### 5. Buildings and props

- A Viking or medieval wood building kit that looks weathered: longhouse walls, roofs, gates, palisade, watchtower, workbench, forge, chests, barrels, torches, fire pits, beds. Search "Viking village", "medieval wood kit URP", "survival base building kit".
- Dark, worn textures and moss or snow decals to age them.

### 6. Interface

- A complete game UI skin: panels, buttons, tabs, sliders, frames, inventory slots, health and stamina bars, icons for items, upgrades and elements, in a dark wood, iron and parchment style. Search "RPG UI kit", "medieval UI", "survival UI". Fonts: a readable serif or rune style display font plus a clean body font.
- Game icons: game-icons.net (CC BY, needs credit) or an icon pack.

### 7. Audio (still needed)

- Water sounds (splash, wading, swimming, pond and marsh ambience), forest ambience for day and night per zone, footsteps by surface, axe chops on different woods, tree falling and splintering, shield block and parry hits, treant roars and groans, boss sounds, UI sounds, and music for day, dusk, night and boss fights.

### How to hand them over

Put each pack in `Assets/ThirdParty/<Publisher>_<PackName>/` with its license file, commit nothing paid to a public repo (add the folder to `.gitignore` if the license forbids redistribution; tell me and I will set it up), and tell me the folder names. I will then replace the weeds and leaf scatter, rework the terrain, lighting and post processing, and wire the new trees, props and UI.

## Shortlist (best free match per need)

| Need | Best pick | Notes |
|---|---|---|
| Ground textures | polyhaven.com/textures (forest ground, grass, mud, rocky terrain, dry riverbed) plus ambientCG.com (Ground, Grass, Forest) | CC0, full PBR sets. Start with 4 to 6 muted sets: grass, dry grass, forest floor with leaf litter, mud, dark rock, cracked earth or ash |
| Moodier trees and plants | KayKit Forest Nature Pack free tier (kaylousberg.itch.io/kaykit-forest) or Quaternius Ultimate Nature Pack; Kenney Nature Kit for light blockout pieces | CC0, FBX or glTF, replaces the bright round set |
| Sky | polyhaven.com/hdris (clear, overcast, dusk, night) | CC0 |
| Water shader | no good free CC0 pack; either a free Asset Store stylized water that allows commercial use, or I write one | the weakest free area |
| Animated Treants | Sketchfab (CC0 or CC-BY, search treant, ent, tree monster) plus Mixamo animations, or a roughly 20 to 30 USD Asset Store pack | the second weakest free area; the best candidate for a small purchase |
| Chop, hurt, drink animations | Mixamo.com (Humanoid FBX, no skin), plus the Quaternius Universal Animation Library | free with an Adobe account |
| Animals | Quaternius Ultimate Animated Animal Pack, Farm Animals, Animated Fish | CC0 |
| Outfits | Quaternius Modular Character Outfits Fantasy, full or Source version | pay what you want |
| Props and buildings | Quaternius Fantasy Props MegaKit and the rest of Medieval Village, Kenney Furniture and Castle kits, KayKit Medieval Builder | CC0 |
| Water sounds (urgent) | freesound.org with the CC0 filter: big and small splash, splashing footsteps in shallow water, jumping into water, swimming, pond and river ambience; marsh frogs and insects | name files `splash-big.wav`, `splash-small.wav`, `wade-loop.wav`, `swim-stroke.wav`, `pond-ambience.wav`, `marsh-ambience.wav` in `Assets/_Chopper/Resources/Audio/` |
| Other sound | Kenney audio packs (impact, RPG, UI), freesound CC0 (axe chops, tree fall, branch snap, footsteps by surface, forest day and night, rain, wind), bigsoundbank.com | CC0 |
| Music | Kevin MacLeod at incompetech.com (ambient, folk, medieval, exploration; CC-BY needs credit), Pixabay music (check each license) | |
| Particles, icons, fonts | Kenney Particle Pack, Kenney Game Icons, game-icons.net (CC-BY), Google Fonts Cinzel and IM Fell English or Almendra | |

Download order: ground textures and HDRIs and one nature pack; then Mixamo axe animations and the animals; then water and forest sounds; then props; then particles, icons, fonts.

## Where to find it (curated list)

Ground textures: ambientCG.com and polyhaven.com/textures (CC0 PBR sets: grass, forest floor, mud, moss, rock, ash). Skies: polyhaven.com/hdris and ambientCG HDRIs. Trees and nature: Kenney Nature Kit (kenney.nl/assets/nature-kit, CC0), KayKit Forest Nature Pack (kaylousberg.itch.io/kaykit-forest), Quaternius Ultimate Nature Pack, poly.pizza and Sketchfab with the CC0 filter. Animals: Quaternius Ultimate Animated Animal Pack, Farm Animals, Animated Fish. Player animations: Mixamo.com (axe, chop, swing, hurt, drink; FBX for Unity, Humanoid) and the Quaternius Universal Animation Library. Outfits: the fuller Quaternius Modular Character Outfits set. Buildings and props: Quaternius Medieval Village and Fantasy Props MegaKits, Kenney Furniture and Castle kits, KayKit Medieval Builder. Audio: Kenney audio packs (kenney.nl/assets/category:Audio), freesound.org with the CC0 filter (axe chop, tree fall, water splash, wading, swimming, forest ambience, marsh frogs, footsteps by surface), bigsoundbank.com. Music: Kevin MacLeod at incompetech.com (CC-BY, needs credit) and Pixabay music. Effects and UI: Kenney Particle Pack, Kenney Game Icons, game-icons.net (CC-BY), Google Fonts (Cinzel, IM Fell English, Almendra).

Suggested folder names under `Assets/ThirdParty/`: `ambientCG_GroundTextures`, `PolyHaven_Textures_HDRIs`, `Kenney_NatureKit`, `KayKit_ForestNaturePack`, `Quaternius_UltimateNaturePack`, `Quaternius_AnimatedAnimals`, `Quaternius_FantasyPropsMegaKit`, `Kenney_ParticlePack`, `Kenney_UIAudio_ImpactSounds`, `Mixamo_Animations`. Keep every license file with its pack. Start order: ground textures and HDRIs, then a moodier tree set, then Mixamo animations and animals, then water and forest sounds, then props and particles.

## What the game already has

- World and trees: Quaternius Stylized Nature MegaKit (cartoony, bright).
- Buildings and props: Quaternius Medieval Village MegaKit, Medieval Weapons.
- Player: Quaternius Universal Animation Library skeleton and animations, Universal Base Characters (head, hair, beard) and Modular Character Outfits Fantasy (free set: Peasant, Ranger). All CC0. Female bodies and the Regular body type are not wired in yet (different skeleton proportions).
- Sound: two real axe recordings, everything else is synthesized placeholder.
- Ground textures and the hut and palisade textures are generated placeholders.

## 1. What matters most for the look (do these first)

The biggest remaining reason the game reads as cartoony is art style, not lighting: bright flat-colored low poly trees and plants. Valheim uses low poly shapes with painterly, slightly muted, hand-painted textures and lots of depth from fog and light. To get close:

| Need | Why | Where to look | Cost |
|---|---|---|---|
| Ground textures: grass, dry grass, forest floor with leaf litter, mud, moss, dark rock, ash and cracked earth. Tileable PBR sets (color, normal, roughness), 2K | Replaces my generated noise. The single biggest upgrade to the ground | ambientCG.com, polyhaven.com (textures), both CC0 | free |
| A more realistic stylized tree set: pines, birch, oak, dead trees, stumps, logs, fallen branches, bushes, ferns, mushrooms, rocks with moss. Painterly textures, not flat colors | The forests define the look. The current set is too bright and round | Quaternius (other nature packs), Kenney Nature Kit (CC0), KayKit Forest Nature Pack, poly.pizza (filter CC0), Sketchfab (filter CC0) | free |
| If you will pay: a stylized nature pack with wind-animated foliage and LODs | Wind sway and leaf shaders add a lot of life | Unity Asset Store: search "stylized nature", "low poly forest", "Valheim style". Polytope Studio and similar publishers | about 20 to 60 USD |
| Sky: a few HDRI skies (clear, overcast, dusk, night with stars) | Current procedural sky is flat | polyhaven.com (HDRIs, CC0) | free |
| Water shader with shoreline foam and depth color | Marsh pools and the pond look like flat plates | Unity Asset Store (search "stylized water"), or I can write a simple one | free to about 25 USD |
| Grass and foliage shader with wind and color variation | Reactive grass exists but uses the stock material | I can do this in code if you prefer not to buy | free |

## 2. Characters and creatures

| Need | Why | Where | Cost |
|---|---|---|---|
| Animated Treant enemy models: small, medium, large, boss, with walk, attack, hit and death animations (tree creature, ent) | Enemies are still plain tree models that bob | Sketchfab (CC0 filter, search "treant", "ent", "tree monster"), Quaternius monster packs, Unity Asset Store | free to about 30 USD |
| Extra player outfits: Viking or woodsman gear, fur, leather armor, hats, helmets, capes | The free outfit pack has only Peasant and Ranger | Quaternius Modular Character Outfits Fantasy full (Source) version via his Patreon or itch.io page, which adds many more outfits and separate parts | pay what you want to a few USD |
| Female and other body types that fit the animation skeleton | Character creator is male only right now | Same Quaternius packs (Regular and Teen body types need an avatar match I can build) | free |
| Better chop animations: overhead axe swing, side swing, tool use, hurt, eat, drink, build | The chop reuses a sword lunge | Mixamo.com (free with an Adobe account, allowed in games; download as FBX for Unity, without skin), or Quaternius animation packs | free |
| Animals and ambient creatures: deer, rabbits, birds, fish | Valheim world life | Quaternius Animated Animals, Kenney, Sketchfab | free |

## 3. Buildings, props and "Minecraft" building feel

| Need | Why | Where | Cost |
|---|---|---|---|
| Wooden building pieces: floors, walls, roofs, doors, stairs, fences, beams, in a Viking timber style | For placeable hut pieces and a build menu | Quaternius Medieval Village (have), KayKit Medieval Builder, Unity Asset Store (search "viking village", "timber house", "modular wood") | free to about 40 USD |
| Workbench, forge, chests, campfire, beds, tools, resources (logs, planks, stone, resin, ore), food | Survival depth | Quaternius Fantasy Props MegaKit, Kenney Survival Kit, poly.pizza | free |
| Block and voxel style items (optional): if you want a literal Minecraft feel for items, crafting and some terrain pieces | Decide the direction first: painterly Valheim look or blocky Minecraft look. Mixing both fully will look inconsistent | Kenney Voxel Pack, Kenney Blocky Characters, Minecraft-style texture packs you make yourself | free |

Tell me which direction you want for items and UI: painterly or blocky. I recommend painterly world with a simple Minecraft style inventory grid and hotbar.

## 4. Audio (currently mostly placeholder)

Water sound is the most urgent: the synthesized splash was bad and has been removed, so entering, wading and swimming are silent right now. Needed: splash in (small and big), wading steps, swimming strokes, water lapping and a pond ambience loop, plus the same for a marsh (frogs, insects). Sources: freesound.org (CC0 filter), Kenney audio, Sonniss bundles. Drop the files in `Assets/_Chopper/Resources/Audio/` with clear names (for example `splash-big.wav`, `wade-loop.wav`, `swim-stroke.wav`, `pond-ambience.wav`) and tell me.

| Need | Where | Cost |
|---|---|---|
| Wood chop variations (5 to 10), tree falling and crashing, branch snaps, leaf rustle | freesound.org (filter CC0), Kenney audio, Sonniss GDC audio bundles (royalty free) | free |
| Footsteps by surface: grass, dirt, wood, stone, water | same | free |
| Ambience loops: day forest, night forest, marsh, ash lands, wind plains, rain | same | free |
| Enemy growls, wood creaks, death crumble, player hurt and grunt, hut damage | same | free |
| Music: calm exploration by day, tense fight at night, title theme (Valheim style ambient folk) | Kevin MacLeod incompetech (CC-BY, needs credit), Pixabay music, itch.io music packs (check license) | free to about 15 USD |
| UI sounds: click, craft, upgrade, pickup | Kenney UI audio | free |

## 5. Effects and interface

| Need | Where | Cost |
|---|---|---|
| Particle textures and VFX: fire, torch flame, smoke, embers, dust, leaves, chips, magic glow per element | Kenney particle pack (CC0), Unity Asset Store free VFX packs | free |
| Item icons (axe, torch, salve, wood, stone, armor) | game-icons.net (CC-BY, needs credit), Kenney Game Icons (CC0) | free |
| A fantasy font that reads well small, for UI and titles | Google Fonts: Cinzel, IM Fell, Almendra (SIL Open Font License) | free |
| Loading and title background art, game logo | Commission or AI generated; or I can do a simple 3D title scene | varies |

## 6. Tools and accounts (not assets)

- Unity build support modules for Linux and macOS. To install them, run this in a terminal, and approve the Windows permission prompt (it needs administrator rights, which I cannot grant): `unity install-modules -e 6000.5.10f1 -m linux-mono mac-mono --accept-eula -y`. Then tell me and I will build Linux and macOS players.
- For automatic builds on GitHub: repository secrets `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE` (see the comments at the top of `.github/workflows/release.yml`). A Unity Personal license works but needs the GameCI activation file step.
- Apple Developer account (99 USD per year) only if you want a signed and notarized macOS app with no security warning. Without it players right-click Open once (explained in `Tools/PLAYTEST_README.txt`).
- Testing machines: your Windows PC, Fedora Linux and Mac are enough. Tell me the Mac chip (Intel or Apple Silicon); the build is universal either way.

## Suggested order

1. Ground textures (ambientCG or Polyhaven) and a moodier tree set: the largest visible change for the least money.
2. Animated Treant enemies and real chop animations (Mixamo): biggest gameplay feel.
3. Real sounds and ambience.
4. Water and sky upgrades.
5. More outfits and building pieces.
