# Chopper: assets to find

Last updated: 2026-10-04 (free downloads plan, tree enemy chosen). The older, longer version is in `Docs/archive/2026-10-02/requirements.md`.

Put each pack in `Assets/ThirdParty/<Publisher>_<PackName>/` with its license file, then tell me the folder name. Only use packs whose license allows a commercial game (CC0 is easiest).

## 0. The free asset downloads (fixing the Copilot attempt)

**What happened:** Copilot was asked to download 13 free packs. It made the folders but downloaded nothing, so `Assets/ThirdParty/` now has 11 empty folders. Five of them duplicate packs we already have under a slightly different name. Quaternius and Kenney packs come from download pages (itch.io, Google Drive, kenney.nl), so an agent cannot fetch them reliably. Downloading by hand takes about 15 minutes.

**What we already have (do not download again):**

| Requested folder (empty) | Already in the project as | Action |
|---|---|---|
| `Quaternius_UniversalBase` | `Quaternius_UniversalBaseCharacters` | Delete the empty folder |
| `Quaternius_StylizedNature` | `Quaternius_StylizedNatureMegaKit` | Delete the empty folder |
| `Quaternius_MedievalVillage` | `Quaternius_MedievalVillageMegaKit` | Delete the empty folder |
| `Quaternius_ModularOutfits`, `Quaternius_MedievalWeapons` | same names, already full | Nothing to do |
| `PolyHaven_Textures` | `PolyHaven_Ground` (ground, dirt and burned textures) | Delete the empty folder unless you add bark or rock textures |

**Worth downloading, in this order:**

| # | Folder | Pack and where | Why we want it | Get these files |
|---|---|---|---|---|
| 1 | `ambientCG_Water` | ambientCG.com, search "Water" (for example Water 002) and "Foam". CC0 | Water route A: waves need a normal map, shores need foam | The 1K or 2K PNG zip of one water material and one foam material: keep the `Color`, `NormalGL` and (foam) `Opacity` maps |
| 2 | `Kenney_Audio` | kenney.nl/assets/rpg-audio and kenney.nl/assets/impact-sounds. CC0 | Swimming splashes, strokes and dripping for water route A; spares for effects that sound wrong | The `.ogg` files and `License.txt` from both zips |
| 3 | `Mixamo_AxeChop` | mixamo.com (free Adobe login). Search "chop", "axe", "wood chop", "tree chop" | A real chopping swing instead of the one posed in code | Download each as **FBX for Unity, Without Skin, 30 fps, In Place** ticked if offered. Two or three clips are enough |
| 4 | `Quaternius_ModularDungeon` | quaternius.com, Modular Dungeon pack (or quaternius.itch.io). CC0 | Optional: proper crypt walls, floors, doors and props instead of the pixel pack pieces | The **FBX** folder and `License.txt` only |
| 5 | `Quaternius_UltimateMonsters` | quaternius.com, Ultimate Monsters. CC0 | Optional: there is no tree creature in it, so it does not replace the walking trees. Only get it for other enemy ideas (bats, blobs) | The **FBX** folder and `License.txt` only |

**Skip:** `Kenney_NatureKit` and `Kenney_FantasyTown`. Kenney's blocky, flat-colored style clashes with the pixel textured trees and the Quaternius characters (section 4 says to stay in one style). Delete those two empty folders.

**How to download a pack (Quaternius or Kenney):**
1. Open the pack page and press Download. On itch.io choose "No thanks, just take me to the downloads" if you do not want to pay; the packs are free.
2. Unzip it somewhere outside the project first (for example your Downloads folder).
3. Copy **only** the FBX folder (or the `.png` / `.ogg` files) and the `License.txt` into the folder from the table. Leave out the `.blend`, `.obj` and `.gltf` copies: they double the import time and Unity cannot use the `.blend` files without Blender.
4. Let Unity import, then tell me the folder name. I add the pack to `Assets/ThirdParty/CREDITS.md` and wire it in.

**Cleaning up the empty folders:** delete them in the Unity Project window (right click, Delete), not in Explorer, so their `.meta` files go too. Or tell me and I delete them.

## 1. Chopper's own character model (top priority)

**Why:** today Chopper is pieced together from Quaternius modular parts (a head, a peasant shirt with bare forearms, trousers, boots, hair and a beard) with the base body hidden underneath. Nothing sits under the clothes, so any gap between parts shows straight through. The shirt only has short sleeves (the forearms are skin in the mesh itself, so painting cannot fix it). Code-built extras (primitive hats, box suspenders) looked bad and were removed. The fix is one proper model made to match the concept art.

**The look** (from `original-chopper-concept-male.png` and `transcript.md`, both in the repo root):

| Part | Wanted |
|---|---|
| Build | Short and broad woodsman, stylized (not realistic), friendly game proportions |
| Head | Very full, thick dark brown beard and mustache covering the lower face; short dark hair |
| Hat | Brown wide-brimmed woodsman hat. **Decide first:** you asked for no hat on 2026-10-03; if it comes back, it should be part of the model (or a separate mesh made for this head), not built in code |
| Shirt | Red and black buffalo plaid flannel with **long sleeves** to the wrist |
| Suspenders | Dark (near black) straps over the shoulders |
| Belt | Brown leather with a plain gold or yellow rectangular buckle |
| Trousers | Solid medium blue |
| Boots | Dark brown |

**What the file must have:**
- One skinned mesh (or a few that fit with no gaps) with a **full body under the clothes** or no holes anywhere.
- A **Humanoid** rig (Mixamo's auto-rigger is fine), so I can retarget our Universal Animation Library moves onto it.
- Roughly 5,000 to 15,000 triangles, one 1024 or 2048 px texture, matte look.
- FBX, put in `Assets/ThirdParty/<Source>_Chopper/` with its license.

**Ways to get it, cheapest first:**
1. **Generate it from the concept:** an image to 3D tool (Meshy, Tripo or Rodin) from the concept picture, then Mixamo's auto-rigger. Check the tool's license allows commercial use on your plan. Expect to clean up the hands and the beard.
2. **Commission it:** a stylized character artist on Fiverr or ArtStation, with the concept image and the table above. Ask for a Humanoid rig and the source file.
3. **Buy a close match:** a stylized "lumberjack" or "woodsman" character on Fab or the Unity Asset Store with a Humanoid rig, then recolor it.

Until then I can try a cheaper step in code: the Quaternius Ranger outfit in the same pack may have long sleeves to swap in. Say if you want that tried first.

## 2. A real tree enemy (replaces the walking trees)

**Chosen (2026-10-04):** Monsterlings: Elderwood Guardian (Unity Asset Store, paid). It has idle, walk and death only, so the attack starts as a coded lunge. Recolor steps for the four forests are in `elderwood-recolor-guide.md`. Until it is imported the walking trees stay; no owned pack has a better creature.

**Why:** the night enemies are our pixel-pack pine trees that slide toward the hut with a code bob and a lunge. They have no legs, arms, attack, hit or death animation, so they read as trees sliding around, not as monsters. Crypt guardians already use a real animated draugr; the forest enemies need the same quality.

**What it is:** a tree creature (Treant, Ent, "tree golem", "wood golem", "forest guardian", "root monster", "bark troll"). It must still clearly be a tree, because the game is about chopping and the creature starts the day as a sapling that you can chop out.

**What the file must have:**
- **Animations:** idle, walk, attack (a swipe or a slam), hit reaction and death. A roar or spawn (rising out of the ground) is a bonus.
- **Rig:** best is a **Humanoid** rig (two legs, two arms). Then our Universal Animation Library moves can be retargeted onto it and Mixamo animations work too. A Generic rig is fine only if the pack ships every animation above.
- **Sizes:** one model is enough. I scale it for small, medium and large (1.9 m, 2.9 m and 4.2 m tall) and for the bosses.
- **Color:** the leaves or moss tinted by the element (water, fire, earth, wind), so leaves or glowing parts should be a **separate material** or a mostly grey texture I can tint.
- Roughly 3,000 to 15,000 triangles, one 1024 or 2048 px texture, a stylized low poly look that sits next to the pixel-pack trees and the Quaternius characters.
- FBX (or glTF), with a license that allows a commercial game, in `Assets/ThirdParty/<Publisher>_<PackName>/`.

**Ways to get it, cheapest first:**
1. **Free packs.** Search Quaternius (quaternius.com, CC0), Kenney, Poly Pizza and itch.io (filter: 3D, free, CC0 or commercial use) for "treant", "ent", "tree monster", "tree golem". Check that it is **animated**, not only a static model.
2. **Generate it and rig it.** Make a two-legged tree creature in an image to 3D tool (Meshy, Tripo or Rodin) from a reference picture, then put it through **Mixamo's auto-rigger** (it needs two legs and two arms in a T or A pose). That gives a Humanoid rig, so every walk and attack we already have can drive it. Check the tool's license on your plan.
3. **Buy one.** Fab or the Unity Asset Store: search "Treant", "Ent", "Tree Monster" or "Forest Guardian", filter by **Animated** and **URP**, and read the animation list before buying.

**Tell me:** the folder name and which rig it has. I will swap it in for the walking trees, keep the sapling stage (the creature rooted and asleep by day), and keep the element tint.

## 3. Water (Valheim style)

**What Valheim does:** the sea has real waves (the surface moves up and down, made in the shader), white foam where waves meet the shore and around objects, a deep color that changes with depth, and a murky blue fog when the camera dips under. Players and boats float on the same waves, because the game works out the wave height in code too. Swimming is slow, costs stamina, and leaves you Wet until you dry off by a fire.

**Done in code on 2026-10-04:** you float at the surface, the camera can no longer go under the water, the pond and lakes no longer show a grey wall from below, swimming is slower and costs stamina, Wet (after swimming or in rain) slows running and stamina, and campfires in the hut yard and on the pond bank dry you off.

**Still to do, and what it needs.** Pick one route:

- **Route A, build it ourselves (no purchase).** I add moving waves to our water shader and the same wave math in code for floating, plus foam along shores and an underwater fog. I only need:
  - a seamless **water normal map** and a **foam texture** (CC0, for example from ambientCG or a CC0 texture pack),
  - **swimming sounds**: a few splashes, swim strokes, getting out of the water dripping, and a soft shore lapping loop (CC0 from Kenney or Freesound). Drop them in `Assets/_Chopper/Resources/Audio/` like the other effects.
- **Route B, buy a finished system.** Staggart's **Stylized Water 3** for URP (paid, Unity Asset Store) has waves, foam, shore effects and a way to read the wave height for floating; its **Underwater Rendering** add-on (paid, separate) does the under-the-surface look. Check it supports Unity 6 URP before buying.

My suggestion: Route A first. Most of what looked bad was the camera going under the surface, which is fixed; waves and foam are a smaller step from our current shader than switching systems.

## 4. Equipment and animations

**Why the axe looks wrong:** our weapon (Quaternius Medieval Weapons) is a loose prop. It has no grip point, so the code guesses where the hand goes. The animation library also has no proper chopping animation, so the pose is faked in code. Assets made to fit Chopper fix both problems.

### What to look for

The best option is **a character pack that comes with its own weapons, shields and animations, all made for the same skeleton**. Search the Unity Asset Store or Fab for "modular fantasy character", "viking character", "sword and shield animations" or "RPG character with weapons".

| Need | What makes it work |
|---|---|
| Axes | A one-handed wood axe first, then better tiers (iron, bronze). The **pivot must be at the grip**, with the blade pointing along one clear axis. A two-handed felling axe is optional. |
| Animations | Axe set: idle, block, block hit, attacks (slash, overhead chop), roll, hit, death and **tree chopping**. They must fit our skeleton (below) or come with their own character. |

### Skeleton compatibility

- Our player uses the **Quaternius Universal Animation Library** skeleton (Unreal style bone names: pelvis, spine_01, upperarm_l, hand_l). Anything that says **"Humanoid"** (Mecanim) can usually be retargeted to it, and I can do that part.
- Mixamo (free with an Adobe account) has axe and chopping animations. Download as **FBX for Unity, Without Skin**, put them in `Assets/ThirdParty/Mixamo_Axe/`, and I will retarget them.
- Avoid generic (non-humanoid) rigs unless the pack also includes the character itself.

### Good places to look

- **Fab / Unity Asset Store:** Synty POLYGON Vikings (characters, axes and buildings in one consistent style, paid). Also any "Viking" or "fantasy RPG" pack that lists Humanoid rig and URP.
- **Mixamo:** "Chopping Tree" and axe animations.
- **Quaternius** (CC0): the fuller Modular Character Outfits and Ultimate Animated packs, if they add shield and weapon animations.
- **Kenney** (CC0): blocky weapons, if you choose a Minecraft-like style.

Pick one style and stay in it. Mixing Synty, Quaternius and pixel-art props looks patchy.

## 5. Audio

**Done 2026-10-02:**
- Every effect is a new, polished CC0 recording and the music is by AlkaKrab under its royalty-free commercial license; see `Resources/Audio/CREDITS.txt`.
- There is no background ambience on purpose: no birds unless a bird enemy is added, and weather sounds come with weather.

Still open:
- **Listen and swap** any effect that sounds wrong. Drop a file with the same name into `Assets/_Chopper/Resources/Audio/`. The tool that cleaned these up can polish a replacement the same way.
- **Weather sounds** (rain, storm, wind gusts) once weather exists.
- **A crypt sound** (dripping, a low drone), if wanted; it needs a small code hook first.
- **Music per place (wanted 2026-10-05, the code is done).** The music now follows the place and the hour (`MusicRules`, `AudioDirector`): each mood plays its own files when they exist and falls back to one of our six AlkaKrab tracks until then. Drop `.ogg` files into `Assets/_Chopper/Resources/Audio/` with these names (`-1`, `-2` ... for a playlist), add the credits to `CREDITS.txt`, and they play with no code change:

| Files | When | What to look for | Plays now (fallback) |
|---|---|---|---|
| `music-hub-1.ogg` ... | The hub by day, not under attack | Cozy and peaceful: soft folk, lute or harp, a hearth feel. Players spend whole days here fishing, talking and building. | "Odin's Whisper" |
| `music-water-1.ogg` ... | North forest by day | Cool, misty, flowing | "Northern Lights" |
| `music-earth-1.ogg` ... | East forest by day | Deep, rooted, slow drums | "Frostbound Horizons" |
| `music-fire-1.ogg` ... | South forest by day | Warm, restless | "Echoes of Valhalla" (also the title music) |
| `music-wind-1.ogg` ... | West forest by day | Open, airy | "Saga of the Sea Wolves" |
| `music-calm-night-1.ogg` ... | Night, no enemies close | Calm but uneasy | "Throne of the Fjords" |
| `music-fight-1.ogg` ... | Awake enemies within 35 m, day or night (grove wardens and crypt arenas too) | Tense, driving, drums; Viking or Nordic battle music, 2 to 4 minutes, loops cleanly | "Frostbound Horizons" and "Echoes of Valhalla" |

  Places: AlkaKrab's other packs (same license as our music), OpenGameArt or itch.io filtered to CC0 or commercial use, or Pixabay Music (free for commercial use). Any length works; a playlist plays its tracks in turn.

## 6. Later (not urgent)

- **Trees and nature in one consistent style:** pines, birches, oaks, stumps, fallen logs. Every plant should have a purpose (healing herbs, fabric bushes, wood).
- **Buildings:** a Viking timber building kit with walls, roof, gate, palisade and workbench.
- **Interface:** an item icon set (axe, shield, wood, herbs, fabric, bandages).
- **License check:** the "100+ Pixel Lowpoly Assets" pack has no license file. Find its source page before any public release.

## 7. Making your own assets (Blender and Blockbench)

- **Blockbench:** best for blocky, Minecraft-like items (axes, shields, props). Export as glTF or FBX.
- **Blender:** for anything else. Set **Scale 1.0 = 1 meter**, with **+Y up** in Unity (in the FBX export, Forward -Z and Up Y, Apply Transform ticked).
- **Weapons:** put the origin at the grip (axe) or at the handle on the back (shield). Point the axe blade along +Z, and point the shield face along +Z with its top along +Y. Name the objects clearly (`Axe_Wood`, `Shield_Round_Wood`).
- **Characters:** keep the bone names of the Universal Animation Library (export its skeleton from `Assets/ThirdParty/Quaternius_UniversalAnimationLibrary`), or use Mixamo's auto-rigger and choose Humanoid in Unity.
- **Textures:** one 512 or 1024 px texture per item, matte (low smoothness), muted colors.
- Keep the source `.blend` or `.bbmodel` files in a `SourceArt/` folder outside `Assets/`.
