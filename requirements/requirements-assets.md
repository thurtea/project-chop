# Assets

Last updated: 2026-10-09.

## New art direction

Not chosen.

Write the direction here before any new model, texture, icon, or creature is bought or made. Until this section names it, the lists further down are an inventory of what the game uses today and what the old direction still wanted. They are not permission to match the Valheim look.

## Retired direction

Through 2026-10-08 the target was Valheim: low poly shapes, small low resolution textures with point filtering, muted earthy colors, and the lighting, fog and shadows doing the mood. Rough and handmade, not cute and not smooth. That target is closed.

## What left the game

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
| Mixed motion packs (Quaternius UAL 1 and 2 clips, Kevin Iglesias Human Basic Motions and Crafting, Mixamo), removed 2026-10-08 | Mixing sources looked inconsistent. All removed from `Assets`; the raw Mixamo files are still in `SourceAssets/Motions/raw`. Selected Mixamo clips were wired back the same day for the Moving group only. See `requirements-animation.md` |
| Every motion keyed by hand in code (`CodeMotions.cs`), 2026-10-08 | Stiff, robot-like. Kept only as a stand-in until each real motion arrives |
| Turning off animation import on `UAL1_Standard.fbx` (2026-10-08) | Unity then drops the Animator from the model, so every character stood in a T-pose. Never turn it off: the player, preview, draugr and golems get their Animator from that file |
| Code-built hammer icon, words in colored squares ("Wet", "Fed") | Amateur stand-ins; still in the game until a real icon set is chosen |

## In the game today

These are facts about the current build, not the new direction.

- **Character creator (2026-10-06):** Character on the start screen. Six outfits (Chopper's plaid shirt and jeans, Lumberjack, Woodsman, Scout, Ranger with a hood, Trapper), four hairstyles, a beard or not, eight hair colors, six shirt colors and five skin tones, on a turning preview. Each player's look is synced to everyone. Built from the Quaternius base character and outfit packs (CC0) on the Quaternius skeleton (`UAL1_Standard.fbx`), with a Humanoid rig, so any Humanoid clip plays on it, Mixamo included.
- **Held items (2026-10-07):** the axe in the right fist with the head on the thumb side and the blade down; the torch in the left fist; a fist animation layer closes each hand round what it holds. Until axe clips arrive, PlayerAxe turns the wrist in code so the head points ahead.
- **Motions:** see `requirements-animation.md`. Code stand-ins remain wherever a clip is missing or was rejected.
- **Controls (Valheim defaults, gameplay):** WASD move, Shift toggles sprint, Ctrl toggles crouch, Alt rolls (block plus Space also rolls), Space jumps, E interacts, left click attacks, right click blocks. Rebindable in Settings.

## Frozen shopping list (retired direction)

Do not buy or paint from this list until the new art direction says the object is still wanted, and in what style.

### Pixel look on the character

This work was for the Valheim look. Do not do it for that reason.

Aseprite only edits flat images, so the work happens on the character's texture (a flat PNG unwrapped from the model), not the model itself:

1. Shrink the texture to 128 or 256 px with point filtering.
2. Paint it in Aseprite: in Blender, open the character, UV Editing tab, UV > Export UV Layout, 256 px. Open that layout in Aseprite as a guide layer, paint the clothes and face in pixels over it, save as PNG. Put it in `Chopper/SourceAssets/Packs/Mixamo/` named `character_texture.png`.
3. Paint on the model in Blockbench. Export the texture PNG and send it as in 2.

### A body that fits the old look

The body is the Quaternius creator body today. The old plan was a lower poly body: any Mixamo character, or any low poly woodsman uploaded to Mixamo (Upload Character, FBX or OBJ, one mesh, T-pose, facing forward), downloaded as FBX for Unity, T-Pose, With Skin, saved as `Chopper/SourceAssets/Packs/Mixamo/character.fbx`. The creator's outfits would then need refitting. Decide the new body with the new art direction, before more motions.

### Icons

Still drawn in code, or taken from game-icons.net silhouettes. Packs already on disk: `RavenmoreIconPack2023`, `Ultimate_Fantasy_RPG_Icons`, `Basic RPG Item Free`, `Fishes`, and the game-icons.net SVGs in `Chopper/SourceAssets/IconPacks`. When a direction exists, pick one pack and do not mix two. Map axe, fists, torch, bandage, rod, fish, hammer, sword, wood, herb, fiber, iron, shard, essence, the status badges (wet, fed, elements), the building pieces, stations and map markers from it, and list any icon the pack lacks. Check each pack's license before choosing.

### Models still built in code or reused

| Item | Today | Old want |
|---|---|---|
| Fishing rod and float | cylinders and spheres | Fishing Rod and Reel (Quiet Lake Fishing), CC0, https://3dassets.dev/assets/quiet-lake-fishing-fishing-rod-25b95ce5 . GLB to `SourceAssets/Packs/QuietLake_FishingRod/` |
| Hammer (building) | not held at all. A muted CC0 hammer is already at `Assets/ThirdParty/OGA_LowPolyTools/Hammer.fbx` and is not in the hand | a wooden mallet or carpenter's hammer, low poly |
| Torch | shapes with pixel flames | a torch model (flames can stay) |
| Swords | none held | the pixel pack's six swords (CC0, already in the project) |
| Workbench | the pixel pack table | a carpenter's bench with a vice |
| Building pieces | pack slabs stretched, a code wedge roof, box stairs | a timber kit on a 2 m grid: floor, wall, half wall, doorway with a door, window, 45 and 26 degree roofs, ridge, stairs, ladder, pillar, beam |
| Campfire | three cylinders and pack cube flames | campfire with a stone ring and logs |
| Standing torch | the held torch scaled up | torch on a post or a wall sconce |
| Tree stumps | a cylinder | one stump per tree kind |
| Notice board and signs | boxes | a signpost and a notice board |
| Ruined hut | boxes as planks | broken plank and beam models |
| Fish under water | dark spheres | simple fish silhouettes |

### Night creatures and bosses

On hold with the retired direction.

- **Night creature:** the Monsterlings Elderwood Guardian was chosen for the old look. See `elderwood-recolor-guide.md`. Do not buy it until the new direction includes it. The game uses the wood-creature stand-in (`GolemSetup`) until a replacement is named.
- **Five boss treants** (Fire, Water, Earth, Wind, plain) were going to be free candidates: Treant Pack by Tennessippi Studios (CC0) and Tree Monster Animated by DreamPlayStudios (CC BY 4.0). Downloads would go in `SourceAssets/BossTreants/`. Do not import them for the old look.
- Crypt guardians stay the Quaternius draugr until a matching undead pack is chosen for the new direction.

## Licenses

- The two axe sound files (unknown source).
- Each icon pack in `SourceAssets/IconPacks` before it is chosen.
- Settled: the pixel pack is CC0 (Markus Darius, itch.io). Mixamo is royalty free in games. Raw Mixamo files must not be shared on their own, so they stay out of git with the rest of `SourceAssets/`.
