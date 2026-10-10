# Chopper: art, animation and audio requirements

Last updated: 2026-10-07 (evening). Three parts:

- **OLD**: what the game used before and is being replaced, or was tried and dropped. Kept so nobody brings it back.
- **NEW**: what the game uses now and what Claude does next with it.
- **USER**: what you find, download, buy or decide yourself. Each item says exactly where the files go.

The previous version of this file is in `Docs/archive/2026-10-07/requirements.md`.

Rules for every asset: it must allow a commercial game (CC0 is easiest; CC BY needs a credit line; Asset Store, Fab and Mixamo are fine). Raw downloads go in `Chopper/SourceAssets/` (not in git, never imported by Unity). Never put downloads in `Assets/_Chopper/Resources`: everything in that folder is packed into every build.

Art direction: **Valheim style**. Low poly shapes, small low resolution textures with point filtering (crisp pixels), muted earthy colors, and the lighting, fog and shadows doing the mood. Rough and handmade, not cute and not smooth.

---

## OLD

| What | Why it went |
|---|---|
| Chopper pieced from Quaternius modular outfit parts (Superhero body, peasant shirt, ranger sleeves and boots, code-baked plaid) | The parts were made for a different body: skin showed through at the collar and back, the sleeves were stitched on. Replaced 2026-10-06 by one finished mesh |
| The Quaternius Medieval Weapons battle axe | Wrong shape for a woodsman and sat badly in the hand. Replaced by the woodcutting axe |
| Finger bones bent in code to close the fist | Some clips left the hand flat, so the axe lay on an open palm. Replaced by a fist animation layer |
| Axe grip computed from the mesh every frame | Flipped while running and pointed out of the knuckles like a pen |
| KayKit Adventurers (Barbarian) | Holds an axe properly, but chibi and cute. Rejected 2026-10-06; still in `SourceAssets/Packs/KayKit_Adventurers` |
| Kenney Survival Kit tools | Toy-like. In `SourceAssets/Packs/Kenney_SurvivalKit` |
| A lantern as the held light | Did not fit the game (2026-10-04) |
| Night and dawn words and the countdown under the clock | Removed 2026-10-06; the sun says it |
| The axe carried with a wrist turn worked out in the editor from a sampled idle pose | The game's pose differed, so the head pointed backward (2026-10-07) |
| A 180 degree turn on the humanoid model | Humanoid clips already face forward, so Chopper, the draugr and the golems ran backward. Removed 2026-10-07 |
| **Every motion in the game today:** the Quaternius Universal Animation Libraries 1 and 2 (walk, jog, crouch, jump, swim, roll, death, punches, tree chopping, the sword combo used for the axe, the overhand throw cut in half for draw and holster, the pistol reload used for bandaging) | Stiff and wrong for a woodsman. Rejected 2026-10-07; each stays only until its slot's replacement is in (USER 1) |
| Kevin Iglesias, Human Basic Motions FREE (run and sprint, tried 2026-10-07) | Rejected the same day; stays only until the new run and sprint arrive |
| The Mixamo clips received 2026-10-06 (Crouch Walk Forward and Back, Dwarf Walk, Orc Walk, Great Sword Attack, Sword And Shield Attack, Jump, Strafe and Walk) | Crouch Walk Forward is the crouch since 2026-10-08; the rest unused. Still in `SourceAssets/Packs/Mixamo/` (USER 1 lists each) |
| Code-built hammer icon, words in colored squares ("Wet", "Fed") | Amateur stand-ins; still in the game until the icon set arrives (USER 3) |

---

## NEW

### Character and animation (in the game now)

- **Character creator (restored 2026-10-06):** Character on the start screen. Six outfits (Chopper's plaid shirt and jeans, Lumberjack, Woodsman, Scout, Ranger with a hood, Trapper), four hairstyles, a beard or not, eight hair colors, six shirt colors and five skin tones, on a turning preview. Each player's look is synced to everyone. Built from the Quaternius base character and outfit packs (CC0) on the Quaternius skeleton, with a **Humanoid** rig, so any Humanoid clip plays on it.
- **Held items (fixed 2026-10-07):** the axe in the right fist with the head on the thumb side and the blade down; the torch in the left fist; a fist animation layer closes each hand round what it holds. Until axe clips arrive, PlayerAxe turns the wrist in code so the head points ahead (one turn standing, one averaged over the stride while moving).
- **Motions:** Quaternius idle, walk, run, sprint and the rest, plus the Mixamo crouch walk (2026-10-08). All placeholders until you drop in your own (USER 1).

### What Claude does when your motions arrive

1. Wire them in one group at a time (1A first), with pictures and a side by side of each (standing, walking, running, chopping, blocking) for you to approve before the next group. A placeholder is removed only when its replacement is approved; Chopper cannot stand or walk with an empty slot.
2. Match each loop's playback to the player's real speed so the feet do not slide, and check every loop for a hitch at the seam.
3. When the axe idle, walk and run arrive (1A), delete the code wrist turn in PlayerAxe: the clips hold the axe themselves. The fist layer and grip stay.
4. Line up the gameplay moments with the clips: the frame the axe meets the tree or the enemy, the frame the bandage is done, the cast release, the hammer blow, the hand over the shoulder for the holster.
5. The draugr and golems use Chopper's clips today; they keep the old ones until their own arrive (1F).
6. Then the rest: the pixel look (USER 2), icons (USER 3), props (USER 4), sounds (USER 6), each listed in `Assets/ThirdParty/CREDITS.md`.

### Unity editor: what to run

Nothing, each time you open the project. Everything generated (scenes, prefabs, materials, the animator) is saved in the project and in git.

| Menu item | When |
|---|---|
| **Chopper > Setup > Rebuild Network Scenes** | Only after you pull changes Claude made, if the game in the editor looks out of date. Claude runs it in batch mode after every change, so usually never |
| Prepare Pixel Pack, Prepare Third Party Assets | Never. One time import settings, already saved |
| Capture ..., Dump ..., Probe ..., Export Sounds | Never. Tools Claude uses to check things |
| Chopper > Build > ... | Only if you want a build yourself; Claude rebuilds the playtest exe at the end of each session |

---

## USER

### 1. Every motion Chopper needs (top priority)

You pick motions you like and drop them in; Claude wires each one in and rebuilds the exe so you can judge it in seconds. Say "try another" and it gets swapped.

#### Where to put them

- Folder: `Chopper/SourceAssets/Motions/` (make it). Save each under the exact name in the tables below. A file with that name replaces what plays today; a missing file keeps today's motion.
- From Mixamo: **FBX for Unity (.fbx)**, **Without Skin**, 30 fps, keyframe reduction none. Tick **In Place** for anything that walks, runs, crawls or swims.
- One handed axe (or tool) in the **right** hand, left hand free (it holds the torch). Props in the file do not matter; the game adds the axe, pick, rod and hammer.
- Facing forward, starting and ending in the same spot. Loops must join (last frame matches the first).
- Try a few first (walk, run, axe idle, one chop) to settle the style, then the rest. One source for everything looks best.

#### Mixamo motions already on disk (`SourceAssets/Packs/Mixamo/`, received 2026-10-06)

| File | In the game? |
|---|---|
| Crouch Walk Forward | **Yes**: the crouch walk (and its first frame is the crouch idle), since 2026-10-08 |
| Crouch Walk Back | No |
| Dwarf Walk | No (imported as a spare, `Assets/ThirdParty/Mixamo_Motions`) |
| Orc Walk | No (imported as a spare) |
| Great Sword Attack | No (two handed, wrong for a hatchet) |
| Sword And Shield Attack, Attack(1), Jump, Jump(1), Strafe, Walk | No (shield in the left hand) |
| character.fbx | No (the old Mixamo body, retired 2026-10-06) |

Your 2026-10-08 files are converted in `SourceAssets/Motions/converted` (spares: Idle(1), Walking(1), Standing Walk Forward, Start Walking, the strafes, turns and walking backwards; the player always faces where he moves, so strafes are not used). Bold in the "Now" column is yours or the Kevin Iglesias crafting pack; the rest is still Quaternius.

#### Moving

| Motion | Loop | Notes | Now | Save as |
|---|---|---|---|---|
| Idle | Loop | Standing, relaxed, axe at the side | **Mixamo Idle** | `idle.fbx` |
| Walk | Loop | About 1.5 m/s | **Mixamo Walking** | `walk.fbx` |
| Run | Loop | About 6 m/s, the axe swings with the arm | **Mixamo Running** | `run.fbx` |
| Sprint | Loop | About 9 m/s, leaning in | **Mixamo Fast Run** | `sprint.fbx` |
| Crouch idle | Loop | Sneaking pose, still | Mixamo Crouch Walk Forward, first frame | `crouch_idle.fbx` |
| Crouch walk | Loop | Sneaking, about 2.5 m/s | **Mixamo Crouch Walk Forward** | `crouch_walk.fbx` |
| Jump start | | Push off | Quaternius Jump | `jump_start.fbx` |
| Falling | Loop | In the air | **Mixamo Falling To Landing** | `fall.fbx` |
| Landing | | Touch down and recover | none | `jump_land.fbx` |
| Hard landing | | A fall long enough to hurt (over 4 m) | **Mixamo Falling Flat Impact** | `land_hard.fbx` |
| Swim idle | Loop | Treading water, head above | **Mixamo Treading Water** | `swim_idle.fbx` |
| Swim | Loop | Head above water | **Mixamo Swimming** | `swim.fbx` |
| Dodge roll | | Forward roll, about 0.7 s, ends standing | Quaternius Roll | `roll.fbx` |

#### Logging

| Motion | Loop | Notes | Now | Save as |
|---|---|---|---|---|
| Chop | Loop | Side swing into a trunk at waist height, about 1 s; the blade meets the wood about halfway | Quaternius Tree Chopping | `chop.fbx` |
| Draw axe | | Hand over the right shoulder, takes the axe off the back, about 0.6 s | Quaternius Overhand Throw (cut) | `draw.fbx` |
| Holster axe | | Puts the axe over the right shoulder onto the back | same as draw, reversed | `holster.fbx` |
| Pick up | | Bends to take wood, branches or drops off the ground | none | `pick_up.fbx` |

#### Fighting

| Motion | Loop | Notes | Now | Save as |
|---|---|---|---|---|
| Attack 1 | | Quick one handed swing, about 0.6 s | **Mixamo Standing Melee Attack Horizontal** | `attack_1.fbx` |
| Attack 2 | | Swing back the other way | **Mixamo Standing Melee Attack 360 Low** | `attack_2.fbx` |
| Attack 3 | | Heavy finisher, steps forward, about 0.9 s | **Mixamo Standing Melee Combo Attack Ver. 1** (its last swing) | `attack_3.fbx` |
| Block idle | Loop | Axe raised across the body | Quaternius Sword Idle | `block_idle.fbx` |
| Block hit | | Taking a blow on the guard (also the parry) | none | `block_hit.fbx` |
| Punch 1 and 2 | | No axe: jab and cross | Quaternius Punch Jab, Cross | `punch_1.fbx`, `punch_2.fbx` |
| Hit, light | | A flinch | none | `hit_light.fbx` |
| Hit, heavy | | Knocked back a step | none | `hit_heavy.fbx` |
| Death | | Falls and stays down (also crushed by a tree) | **Mixamo Fall Flat** | `death.fbx` |
| Get up | | Lying to standing (revived, out of bed) | none | `get_up.fbx` |

#### Mining

| Motion | Loop | Notes | Now | Save as |
|---|---|---|---|---|
| Mining strike | Loop | Overhead pickaxe blow into a rock face at chest height, about 1 s | **Kevin Iglesias Mining One Hand (wall)** | `mine.fbx` |
| Mining, low vein | Loop | Optional: kneeling or bent over, striking near the floor | none | `mine_low.fbx` |

#### Fishing

| Motion | Loop | Notes | Now | Save as |
|---|---|---|---|---|
| Cast | | Rod back over the shoulder and forward | **Kevin Iglesias Fishing Throw** | `fish_cast.fbx` |
| Waiting | Loop | Rod held out in front, small movements | **Kevin Iglesias Fishing Loop** | `fish_idle.fbx` |
| Twitch | | A small flick of the rod tip | none | `fish_twitch.fbx` |
| Strike | | Pulling the rod up sharply on a bite | none | `fish_strike.fbx` |
| Reel | Loop | Reeling in with the rod up, leaning back | **Kevin Iglesias Fishing Fighting** | `fish_reel.fbx` |

#### Gathering

| Motion | Loop | Notes | Now | Save as |
|---|---|---|---|---|
| Gather | | Kneels or bends to pick a plant (herbs, ferns) | **Kevin Iglesias Gathering** | `gather.fbx` |
| Pick up | | Same as in Logging | none | `pick_up.fbx` |

#### Building and crafting

| Motion | Loop | Notes | Now | Save as |
|---|---|---|---|---|
| Hammering | Loop | One handed hammer blows at chest height (placing and repairing) | none | `hammer.fbx` |
| Crafting at the bench | Loop | Working at a table with both hands | none | `craft.fbx` |

#### Living

| Motion | Loop | Notes | Now | Save as |
|---|---|---|---|---|
| Bandage | | Wraps the left forearm, about 1.5 s | Quaternius Pistol Reload | `bandage.fbx` |
| Eat | | Hand to mouth, a couple of bites (fish) | none | `eat.fbx` |
| Open chest | | Lifts a lid at knee height | none | `open_chest.fbx` |
| Lie down | | Into bed | none | `sleep_down.fbx` |
| Sleeping | Loop | | none | `sleep_idle.fbx` |
| Sit by the fire | Loop | Optional: sitting on the ground or a log | none | `sit_idle.fbx` |
| Torch held up | Loop | Left arm only; skip it if the torch looks right to you | Quaternius Torch Idle | `torch_idle.fbx` |

#### Enemies (later)

The draugr and golems use Quaternius walks and attacks today. When there is time: `enemy_walk.fbx`, `enemy_attack.fbx`, `enemy_hit.fbx`, `enemy_death.fbx`, `enemy_rise.fbx` (rising out of the ground).

#### 1G. A body that fits the look (optional, decide before the motions)

The body is the Quaternius creator body today. For a lower poly Valheim body instead: any Mixamo character, or any low poly woodsman uploaded to Mixamo to be rigged (Upload Character, FBX or OBJ, one mesh, T-pose, facing forward), downloaded as **FBX for Unity, T-Pose, With Skin**, saved as `Chopper/SourceAssets/Packs/Mixamo/character.fbx`. The creator's outfits would then need refitting.

### 2. The pixel look on the character

Aseprite only edits flat images, so the work happens on the character's **texture** (a flat PNG unwrapped from the model), not the model itself:

1. **Quickest (Claude can do it):** shrink the texture to 128 or 256 px with point filtering. Instant pixels, no painting.
2. **Paint it in Aseprite:** in Blender, open the character, UV Editing tab, UV > Export UV Layout, 256 px. Open that layout in Aseprite as a guide layer, paint the clothes and face in pixels over it (red and black plaid shirt, dark trousers, beard), save as PNG. Put it in `Chopper/SourceAssets/Packs/Mixamo/` named `character_texture.png`.
3. **Paint on the model directly (easiest to see):** Blockbench (free, made for pixel art 3D). File > Import > OBJ or glTF, then paint on the model with pixel brushes. Export the texture PNG and send it as in 2.

For a real Valheim look, also lower the polygons (1a, second way); pixels on a smooth, high detail body still look smooth.

### 3. Icons (still drawn in code)

You already have `RavenmoreIconPack2023`, `Ultimate_Fantasy_RPG_Icons`, `Basic RPG Item Free`, `Fishes` and the game-icons.net SVGs in `Chopper/SourceAssets/IconPacks`. Decision needed: **which one pack is the style** (do not mix two). Then tell Claude, and it maps axe, fists, torch, bandage, rod, fish, hammer, sword, wood, herb, fiber, iron, shard, essence, the status badges (wet, fed, elements), the building pieces, stations and map markers from it, and lists any icon the pack lacks. Check each pack's license file before choosing.

### 4. Models still built in code or reused

| Item | Today | Wanted, and where to look |
|---|---|---|
| Fishing rod and float | cylinders and spheres | **Found:** Fishing Rod and Reel (Quiet Lake Fishing), CC0, https://3dassets.dev/assets/quiet-lake-fishing-fishing-rod-25b95ce5 . Download the GLB to `SourceAssets/Packs/QuietLake_FishingRod/` |
| Hammer (building) | not held at all | a wooden mallet or carpenter's hammer, low poly |
| Torch | shapes with pixel flames | a torch model (flames can stay) |
| Swords | none held | the pixel pack's six swords (CC0, already in the project): nothing to get |
| Workbench | the pixel pack table | a carpenter's bench with a vice. Candidates (not checked): "Workshop: Workbench with Tools, Low Poly" (Boxx-Games, Asset Store) |
| Building pieces | pack slabs stretched, a code wedge roof, box stairs | a timber kit on a 2 m grid: floor, wall, half wall, doorway with a door, window, 45 and 26 degree roofs, ridge, stairs, ladder, pillar, beam |
| Campfire | three cylinders and pack cube flames | campfire with a stone ring and logs |
| Standing torch | the held torch scaled up | torch on a post or a wall sconce |
| Tree stumps | a cylinder | one stump per tree kind |
| Notice board and signs | boxes | a signpost and a notice board |
| Ruined hut | boxes as planks | broken plank and beam models |
| Fish under water | dark spheres | simple fish silhouettes |

### 5. Night creatures and bosses

- **Night creature:** the Monsterlings Elderwood Guardian (chosen earlier; see `elderwood-recolor-guide.md`). Buy it and put it in `Assets/ThirdParty/Monsterlings_ElderwoodGuardian/`. It still needs an attack, a hit reaction and rising out of the ground (Mixamo attacks work if it is Humanoid).
- **Five boss treants** (Fire, Water, Earth, Wind, plain). Free candidates: **Treant Pack** by Tennessippi Studios (CC0, https://tennessippistudios.itch.io/treant-pack , best first try) and **Tree Monster Animated** by DreamPlayStudios (CC BY 4.0, credit needed, has rising out of the ground). Put downloads in `SourceAssets/BossTreants/`.
- Crypt guardians stay the Quaternius draugr until a matching undead pack is found.

### 6. Sounds and music

Each sound below is reused from something else today. Name the file as shown and drop it in `Assets/_Chopper/Resources/Audio/` (`-1`, `-2` ... variations play at random). Kenney (CC0) and the Sonniss GDC bundles cover most of it.

`build-place`, `build-break`, `build-hit`, `tool-break`, `repair`, `eat`, `chest-open`, `chest-close`, `bed`, `fishing-cast`, `fishing-reel` (loop), `fishing-hook`, `fishing-snap`, `sword-slash`, `sword-hit`, `enemy-growl`, `enemy-attack`, `enemy-death`, `footstep-stone`, `footstep-wood`, `swim-stroke`, `water-exit`, `rain-loop`, `wind-gust`, `ui-open`, `ui-close`.

Music (`.ogg`, same folder; until then the six AlkaKrab tracks fill in): `music-hub-1` (cozy folk), `music-water-1` (cool, misty), `music-earth-1` (slow drums), `music-fire-1` (warm, restless), `music-wind-1` (open, airy), `music-calm-night-1` (calm, uneasy), `music-fight-1` (tense drums, 2 to 4 minutes, loops cleanly).

### 7. Licenses to confirm

- The two axe sound files (unknown source).
- Each icon pack in `SourceAssets/IconPacks` before it is chosen (USER 3).
- Settled: the pixel pack is CC0 (Markus Darius, itch.io); Mixamo is royalty free in games (raw files must not be shared on their own, so keep the repo private).

### 8. Steam demo (when fishing, building, crafting and the night creatures are done)

- The Steam Direct fee and a demo app; Claude then switches the app id from 480 and sets up depots.
- Store art: header 920x430, small capsule 462x174, main capsule 1232x706, vertical 748x896, library capsule 600x900, library header 920x430, library hero 3840x1240 (no text), logo 1280x720 transparent, background 1438x810, community icon 32x32 (check the Steamworks docs on the day).
- Screenshots (Claude takes them from the release build once you approve the look), a 30 to 90 second trailer, the store text and the system requirements.
