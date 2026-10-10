# Chopper: art, animation and audio requirements

Last updated: 2026-10-07. Three parts:

- **OLD**: what the game used before and is being replaced, or was tried and dropped. Kept so nobody brings it back.
- **NEW**: what the game uses now and what Claude does next with it.
- **USER**: what you find, download, buy or decide yourself. Each item says exactly where the files go.

The full previous version of this file is in `Docs/archive/2026-10-06/requirements.md`.

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
| The axe carried with a wrist turn worked out in the editor from a sampled idle pose | The game's pose differed, so the head pointed backward. Replaced 2026-10-07 by a turn worked out in the game (PlayerAxe) until the axe clips arrive (USER 1b) |
| The Quaternius jog and sprint (Jog_Fwd_Loop, Sprint_Loop) | Did not suit the game. Replaced 2026-10-07 by Kevin Iglesias's Human Basic Motions run and sprint |
| A 180 degree turn on the humanoid model | Humanoid clips already face forward, so Chopper, the draugr and the golems ran backward. Removed 2026-10-07 |
| Code-built hammer icon, words in colored squares ("Wet", "Fed") | Amateur stand-ins; still in the game until the icon set arrives (USER 3) |

---

## NEW

### Character and animation (in the game now)

- **Character creator (restored 2026-10-06):** Character on the start screen. Six outfits (Chopper's plaid shirt and jeans, Lumberjack, Woodsman, Scout, Ranger with a hood, Trapper), four hairstyles, a beard or not, eight hair colors, six shirt colors and five skin tones, on a turning preview. Each player's look is synced to everyone. Built from the Quaternius base character and outfit packs (CC0) on the Quaternius skeleton; each outfit has its own cut of the body (only the skin its clothes cover is removed), so nothing is see-through at the collar, back or cuffs.
- **The Mixamo test body (the monk, Ch39) is out of the game.** Its files are in `SourceAssets/Packs/Mixamo/` with your Mixamo animations; `Tools/mixamo_convert.py` converts Mixamo files to the game's bone names if they are wanted later.
- **Run and sprint (2026-10-07):** Kevin Iglesias's Human Basic Motions FREE (Unity Asset Store), male Run01 and Sprint01, in place. Each plays faster to keep pace with the player (1.37x run, 1.51x sprint, from their measured stride). While moving, the axe uses a second wrist turn averaged over the stride, so it points ahead and swings with the arm.
- **Animations (to be replaced, USER 1b):** the two Quaternius Universal Animation Libraries (CC0) on a Humanoid rig, for everything but the run and sprint. They look wrong for a woodsman (walk, run and the others read as stiff or odd, 2026-10-07), so every one is a placeholder until your Mixamo clips arrive. The axe at rest is the plain walk and idle with the wrist turned in code (PlayerAxe), which is why the arm looks odd while carrying it.
- **Hands:** a fist animation layer closes each hand round what it holds; the axe and torch handles run through the fist from the little finger to the index, as in the torch idle.

### What Claude does when your downloads arrive

1. Mixamo animations (USER 1b): wire each file into the slot it is named after, one at a time, and take pictures of each (standing, walking, running, chopping, blocking) for you to approve before the next. The Quaternius clip in that slot is removed only when its replacement is approved. When the axe idle, walk and run arrive, the code wrist turn on the axe (PlayerAxe carry) is deleted: the clips hold the axe themselves.
2. Nothing is removed before its replacement is here: Chopper cannot stand or walk without a clip in each slot.
3. The pixel look on the character (USER 2): apply the low resolution texture with point filtering and check it next to the world at the same distance as gameplay.
4. Icons (USER 3), props (USER 4), sounds (USER 6): wire in, list in `Assets/ThirdParty/CREDITS.md`.

Claude can also do the quick version of the pixel look now, without you: shrink the current character texture to 256 px and switch it to point filtering. It is crisper, but the body shape stays smooth.

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

### 1. Chopper's body and animations (top priority)

#### 1a. A body that fits the look

Valheim's characters are low poly with small, rough textures. Any Mixamo character can be the body, and the animations come with it, so the choice is only about looks.

Two ways:

- **Pick one on Mixamo:** mixamo.com, Characters tab, look for a bearded man in work clothes (try "lumberjack", "farmer", "worker", "viking", "peasant"). Download: **FBX for Unity, T-Pose, With Skin**.
- **Use any model you like and let Mixamo rig it:** a low poly woodsman from anywhere (Sketchfab, itch.io, a commission, or your own in Blender or Blockbench). On Mixamo, Upload Character (FBX or OBJ, one mesh, T-pose or A-pose, facing forward), place the markers, then download it rigged the same way. This is the best road to a real Valheim look: low poly shape plus a small texture.

Put the file in `Chopper/SourceAssets/Packs/Mixamo/` named `character.fbx` (replacing the monk).

#### 1b. Animations from Mixamo (top priority, 2026-10-07)

The walk, run and the other movements look wrong. Each row below is one slot in Chopper's animator. Download the clip, save it under the exact file name in the last column, and Claude swaps it in (NEW, "What Claude does").

How to download, every clip:

- mixamo.com, Animations tab. **Any character can be selected** (the default Y Bot is fine): the game's body uses a Humanoid rig, so Mixamo clips play on it whatever character they were downloaded with.
- **Format: FBX for Unity. Skin: Without Skin. Frames per second: 30. Keyframe reduction: none.**
- Tick **In Place** whenever it is offered (every walk, run, sprint, crouch walk and swim).
- If the exact name is gone, pick the closest one and keep the file name from the table.
- Put the files in `Chopper/SourceAssets/Packs/Mixamo/Animations/` (make the folder).

| Slot (what it is for) | Plays today (Quaternius) | Search on Mixamo | Save as |
|---|---|---|---|
| **Axe idle** (standing, axe in hand) | Idle_Loop with the wrist turned in code | "Melee Axe Pack" (download the whole pack), its axe idle; or "Axe Idle" | `axe_idle.fbx` |
| **Axe walk** | Walk_Loop with the wrist turned in code | Melee Axe Pack walk forward; or "Axe Walk" | `axe_walk.fbx` |
| **Axe run** | Kevin Iglesias Run01_Forward with the wrist turned in code | Melee Axe Pack run forward; or "Axe Run" | `axe_run.fbx` |
| **Axe sprint** | Kevin Iglesias Sprint01_Forward | "Fast Run" (In Place); the arm with the axe is fixed by the fist layer | `axe_sprint.fbx` |
| **Tree chop** (side swing into a trunk) | TreeChopping_Loop | "Chopping", "Axe Chop" or the Melee Axe Pack horizontal attack | `chop.fbx` |
| **Combat swings 1, 2, 3** (three hit combo, the last a lunge) | Sword_Regular_A, B, C | Melee Axe Pack attacks: two quick ones and one heavy | `attack_1.fbx`, `attack_2.fbx`, `attack_3.fbx` |
| **Guard / block idle** | Sword_Idle plus an arm posed in code | Melee Axe Pack "Standing Block Idle" | `block_idle.fbx` |
| **Block hit** | none | Melee Axe Pack "Standing Block React Large" | `block_hit.fbx` |
| **Holster** (axe onto the back) | OverhandThrow cut in half | "Standing Disarm Over Shoulder" or "Sheath Sword" | `holster.fbx` |
| **Draw** (axe off the back) | OverhandThrow cut in half | "Standing Draw Armed" or "Draw Sword" | `draw.fbx` |
| **Idle, empty hands** | Idle_Loop | "Breathing Idle" | `idle.fbx` |
| **Walk, empty hands** | Walk_Loop | "Walking" (In Place) | `walk.fbx` |
| **Run, empty hands** (done 2026-10-07, Kevin Iglesias) | Run01_Forward | "Running" (In Place) | `run.fbx` |
| **Sprint, empty hands** (done 2026-10-07, Kevin Iglesias) | Sprint01_Forward | "Fast Run" (In Place) | `sprint.fbx` |
| **Crouch idle** | Crouch_Idle_Loop | "Crouch Idle" | `crouch_idle.fbx` |
| **Crouch walk** | Crouch_Fwd_Loop | "Crouched Walking" (In Place); you already sent "Crouch Walk Forward", rename it | `crouch_walk.fbx` |
| **Jump** | Jump_Loop | "Jump"; you already sent "Sword And Shield Jump", rename it if it looks right | `jump.fbx` |
| **Roll** | Roll | "Sprinting Forward Roll" or "Stand To Roll" | `roll.fbx` |
| **Punches** (no axe) | Punch_Jab, Punch_Cross | "Punching", "Cross Punch" | `punch_1.fbx`, `punch_2.fbx` |
| **Hit** | none | "Hit Reaction" | `hit.fbx` |
| **Death** | Death01 | "Dying" | `death.fbx` |
| **Swim idle** | Swim_Idle_Loop | "Treading Water" | `swim_idle.fbx` |
| **Swim** | Swim_Fwd_Loop | "Swimming" (In Place) | `swim.fbx` |
| **Torch held up** | Idle_Torch_Loop (left arm) | "Torch Idle"; skip it if the torch looks right to you now | `torch_idle.fbx` |
| **Bandage** | Pistol_Reload | "Wrapping" or any two hand fiddling idle | `bandage.fbx` |
| **Fishing** (cast, wait, reel) | none | "Fishing Cast", "Fishing Idle", "Fishing Reel" | `fish_cast.fbx`, `fish_idle.fbx`, `fish_reel.fbx` |
| **Mining** (pickaxe strike) | the chop | "Mining" | `mine.fbx` |
| **Hammer** (building) | none | "Hammering" | `hammer.fbx` |
| **Eat** | none | "Eating" or "Drinking" | `eat.fbx` |
| **Sleep** | none | "Laying Down", "Lying Idle", "Getting Up" | `sleep_down.fbx`, `sleep_idle.fbx`, `sleep_up.fbx` |
| **Pick up** | none | "Picking Up" | `pick_up.fbx` |

Most important first: the four axe rows, the chop and the three combat swings, then the empty hand walk, run and idle. The rest can come later.

Already received (2026-10-06, in `SourceAssets/Packs/Mixamo/`, not wired in yet): Crouch Walk Forward and Back, Dwarf Walk, Orc Walk, Great Sword Attack, Sword And Shield Attack (two), Sword And Shield Jump (two), Sword And Shield Strafe, Sword And Shield Walk. Rename any you want used to its slot's file name and move it into `Animations/`.

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
