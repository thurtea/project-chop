# Chopper: Plan v2 (big world, CC0 art)

Historical design plan from 2026-09-29, kept for its reasoning. Much has changed since (the island layout, the house instead of a palisade, building, crypts, fishing); the current state is `summary.md` and the road to a release is `Docs/STATUS.md`.

Status when written: T01 to T07 done (project, assemblies, core types, FishNet boot, networked player, movement check, axe swing). v1 of this plan is kept as `CHOPPER_PLAN_v1.md`.
Style rule for this project: no emojis and no em dashes anywhere (docs, code comments, commit messages, in-game text).

What changed from v1 (2026-09-29):
- Art: free CC0 low-poly packs instead of primitives only.
- World: about 3 km x 3 km sculpted terrain instead of a small blockout. Valheim-meadows feel.
- Loop: the game design text below replaces the v1 loop. Wood, hut repair, per-swing sapling drops, sized enemies that also attack players, kill-count element buffs, and boss saplings are in.
- Terminology: **Sapling** everywhere (was Seedling in v1).
- Trees at this scale cannot be one GameObject each; they become baked data with instanced rendering (D14).

---

## 0. The game

You live at a hut next to a pond. Four forests sit around it: North is Water, South is Fire, West is Wind, East is Earth. You spawn in the main area and pick a direction to chop.

Day
- Pick a forest and chop. Small trees take one hit, medium two, large three.
- A felled tree gives wood. Each swing on a tree can drop a random number of saplings, more for bigger trees.
- Carry wood home to repair and upgrade the hut.

Night
- The day's saplings become tree enemies and walk to the hut. They attack the hut and players.
- Enemy hit counts match their size: one, two, or three.
- Some enemies have an element. Kill enough of one element and you keep that element's buff until dawn. Neutral enemies still take hits but give no buff.
- Nights come out easy or ugly depending on what spawned.

Boss saplings
- Rarely a sapling is a boss sapling. It takes root where it lands and grows over a few days.
- Cutting the grown boss tree down is a job (many hits). Fighting what comes out of it that night is the boss fight.

Not in the first build: crafting (maybe later if people ask), building beyond hut upgrades, inventory beyond carried wood and the axe.

## 0.1 Technical constraints

- Unity 6000.5.10f1, URP, C# MonoBehaviours only. No ECS/DOTS.
- FishNet 4.7.3 (UPM git, pinned). Server authoritative, host mode, 1 to 4 players (scaling capped at 5).
- Standing trees are not networked. Felling is one small ObserversRpc; falling logs are local physics only.
- Enemies: server-only NavMesh AI, clients get interpolated transforms.
- The world is authored at edit time by tools with fixed seeds. No runtime procedural generation.
- Camera is third person orbit (T05); movement uses acceleration and deceleration from `GameConfig` for weight.
- Out of scope for the first build: crafting, building, VFX beyond simple particles, sound polish beyond the chop and fall sounds in T14b, UI beyond a minimal HUD, mobile, save/load across sessions.

---

## 1. Decisions

Each item has a default. If nobody objects, the default is the decision. Rows marked (v1) are unchanged and already implemented where relevant.

| # | Decision | Default |
|---|----------|---------|
| D2 (v1) | Movement authority | Owner-authoritative NetworkTransform with server speed check (T06 done). |
| D3 (v1) | Axe hit authority | Client plays swing instantly, sends `SwingAxeServerRpc(aim)`, server resolves hits (T07 done, hits pending). |
| D4 | Tree identity | Trees are baked by the scatter tool into a `TreeDatabase` asset with a stable `int TreeId` per tree. Every peer loads the same database. |
| D5 (v1) | Late joiners | One `TargetRpc` with the felled bitset plus damage of damaged trees. |
| D6 (v1) | Regrowth | At dawn the server regrows felled trees up to the density target for the player count. |
| D7 | Sapling vs enemy | One networked, pooled `Treant` with states `Sapling` (on the ground, colored by element) and `Active`. Size (S/M/L) comes from the tree it dropped from. |
| D8 (v1) | Counter cycle | Water beats Fire, Fire beats Wind, Wind beats Earth, Earth beats Water (T03 done). |
| D10 | End of night and failure | Night ends on a timer; survivors wither. Hut damage persists. Hut at 0 = game over, host restarts. |
| D11 (v1) | Transport | Tugboat, LAN / direct IP. |
| D12 (v1) | Local testing | Multiplayer Play Mode, 2 to 4 players in one editor. |
| D13 | Rendering | URP. Stylized textured materials from the CC0 packs; element identity from tinted leaves and emission (section 5). |
| D14 | Trees at scale | Trees are data, not GameObjects. `TreeDatabase` holds position, rotation, scale, size, zone, model index. A `TreeRenderer` draws them with GPU instancing per spatial cell with distance culling and LOD. A `TreeColliderStreamer` keeps real colliders only near each local player. The server resolves axe hits against a spatial grid in `TreeDatabase`, not physics. Felled trees are hidden in the renderer and spawn a local falling prefab. |
| D15 | Wood | Felling grants wood to the player who landed the final hit (Small 2, Medium 4, Large 8, config). Server-owned SyncVar on the player, carry cap 40 (config). Pressing Interact (E) within the hut radius deposits it into the hut store. |
| D16 | Hut repair and upgrades | Hut has health and a wood store (SyncVars). Interact near the hut spends wood to repair (config wood per HP). Upgrade levels raise max health at a wood cost (3 levels to start). Upgrades change the hut model. |
| D17 | Sapling drops | Every accepted hit on a tree rolls a sapling count from a per-size range (Small 0 to 1, Medium 0 to 1, Large 0 to 2 per hit; config), times the player-count sapling multiplier, rounded stochastically. |
| D18 | Enemy sizes | Treant size S/M/L = 1/2/3 axe hits (base damage). Size scales model and hut damage. |
| D19 | Enemies attack players | Treants target the nearest player within aggro range, else the hut. Players get health. At 0 a player is down for 10 s then respawns at the hut. No other death penalty in the first build. |
| D20 | Element buffs | Each player counts elemental kills per element during the night. At 3 kills (config) that element is active on the player until dawn. Active element: +50% damage against enemies it beats (D8). Several elements can be active. Replaces v1 axe charges. |
| D21 | Boss sapling | Each sapling roll has a small chance (1%, config) to be a boss sapling instead. It roots where it lands and grows for 2 in-game days (config), visibly getting bigger. Grown, it can be chopped (30 hits, config). Felling it spawns the boss at the next nightfall. See open question Q1. |
| D22 | World size and layout | 3072 m x 3072 m, 3 x 3 terrain tiles of 1024 m. Hub meadow radius about 300 m around the hut and pond. Forests are a ring from about 400 m to 1300 m, split into four 90 degree quadrants (N Water, E Earth, S Fire, W Wind). Mountains and water beyond 1400 m form the edge. |
| D23 | Draw distance | Camera far clip about 800 m, distance fog (Valheim-like haze), shadow distance about 150 m, tree LOD and cull distances in config. |
| D24 | Player and enemy visuals | Player: Quaternius Universal Animation Library mannequin with idle, walk, run, jump, and chop animations; axe from the Medieval Weapons pack. Treant: the same humanoid rig with bark material and a canopy mesh from the nature pack, scaled per size, until a dedicated model exists. |

---

## 2. World

Coordinates: origin at the hut. +Z is north.

| Area | Radius from hut | Content |
|------|-----------------|---------|
| Hub meadow | 0 to 300 m | Gentle rolling grass, the hut, the pond (about 30 m across), flowers, scattered rocks, no trees within 150 m of the hut. Player spawn points around the hut. |
| Forest edge | 300 to 400 m | Thinning bushes and lone trees, dirt paths start here toward each forest. |
| Forests | 400 to 1300 m | Four quadrants with their own tree species, ground texture, and tint. Density rises with distance. Clearings and rocks break it up. |
| Edge | 1300 m and beyond | Rising mountains and cliffs (or sea) so players cannot leave. Invisible wall at 1450 m as a backstop. |

Forest species (from the Stylized Nature MegaKit, Standard):

| Forest | Element | Trees | Ground |
|--------|---------|-------|--------|
| North | Water | Pine_1 to Pine_5, blue-green tint | Mossy dark grass |
| East | Earth | CommonTree_1 to CommonTree_5, deep green | Rich soil and grass |
| South | Fire | DeadTree_1 to DeadTree_5 and TwistedTree with orange leaves | Dry, reddish ground |
| West | Wind | TwistedTree_1 to TwistedTree_5, pale leaves | Pale grass, more wind sway |

Size mapping: each species has small, medium, and large prefabs made by model choice and scale. Size sets hits (1/2/3), wood, and sapling ranges.

Terrain is generated by an editor tool (`WorldTerrainBuilder`) from a fixed seed: layered noise for hills, a flattened basin for the hub, a dug pond, raised rim mountains, terrain layers painted by zone and slope, and grass/flower detail meshes in the meadow. Rebuilding with the same seed is identical.

---

## 3. Assets (all CC0, credited in `Assets/ThirdParty/CREDITS.md`)

| Pack | Source | Use |
|------|--------|-----|
| Stylized Nature MegaKit (Standard) | quaternius.itch.io/stylized-nature-megakit | Trees, bushes, grass, flowers, rocks, pebbles, rock paths |
| Medieval Village MegaKit (Standard) | quaternius.itch.io/medieval-village-megakit | Hut pieces, fences, crates, wagon |
| Universal Animation Library (Standard) | quaternius.itch.io/universal-animation-library | Player and Treant mannequin and animations |
| Medieval Weapons Pack | quaternius.itch.io/lowpoly-medieval-weapons | Axe models |
| Nature Kit (optional) | kenney.nl/assets/nature-kit | Logs, stumps, extra props if needed; flat colored, use sparingly |
| Terrain textures (optional) | polyhaven.com / ambientcg.com | Ground layers if the packs' textures are not enough |

Only FBX and textures are imported (no duplicate glTF/OBJ/Blend). Packs live under `Assets/ThirdParty/<Pack>/` and are never edited in place; project-specific prefabs and material variants live under `Assets/_Chopper/`.

---

## 4. Project structure

```
Assets/
  _Chopper/
    Scenes/            Boot.unity, World.unity
    Scripts/
      Core/            GameConfig, Element, ElementRules, TreeSize, PlayerScaling, ElementPalette
      Networking/      BootMenu, PlayerSpawner
      Player/          PlayerMotor, PlayerCamera, PlayerMovementValidator, PlayerAxe, PlayerWood, PlayerHealth, PlayerBuffs, PlayerAnimator
      Trees/           TreeDatabase, TreeRenderer, TreeColliderStreamer, TreeManager, TreeFallDummy
      Enemies/         SpawnTable, Treant, TreantAI, SaplingSpawner, EnemyDirector, BossSapling
      World/           PlayerSpawnPoint, DayNightCycle, ForestZone, Hut, GameStateManager
      Pooling/         LocalPool
      UI/              Hud
      Editor/          NetworkScenesBuilder, AssetImportTools, WorldTerrainBuilder, TreeScatterTool, HutBuilder
    Prefabs/           Player, Trees, Enemies, World
    Materials/         Elements, World, Trees
    Config/            SO_GameConfig, SO_ElementPalette, SO_SpawnTable_*, SO_TreeSpecies_*, SO_TreeDatabase
  ThirdParty/          CC0 packs (read only) + CREDITS.md
  Tests/EditMode/
```

Naming (v1, unchanged): namespaces match folders, one public type per file, `_camelCase` private fields, server methods prefixed `Server`, client-only `Client`, RPCs suffixed `ServerRpc`/`ObserversRpc`/`TargetRpc`, materials `M_`, ScriptableObjects `SO_`. No magic gameplay numbers in behaviours.

---

## 5. Element visuals

Base colors from v1 stay (Neutral #4F8A3C, Water #2F7FD8, Fire #E0521F, Earth #7A6232, Wind #BFEFEA). With textured assets they become tints on leaf materials and emission on elemental saplings and Treants. Forest trees get their zone tint so players always know where they are. Buff state shows on the axe head and in the HUD.

## 6. Scaling (v1, implemented)

`multiplier(n) = 1 + 0.4 * log2(min(n, 5))`. Separate coefficients for enemy cap, tree density at dawn regrowth, and sapling drops.

## 7. Spawn tables (v1, implemented)

Zone element 55%, Neutral 25%, each other element about 6.7%.

---

## 8. Task packets

Rules: one packet per commit, compiles and runs at the end, no emojis or em dashes, EditMode tests where logic is pure, headless multiplayer smoke test for networked behaviour.

### Done
- T01 Project, T02 Assemblies, T03 Core types and config, T04 FishNet boot, T05 Networked player, T06 Movement check, T07 Axe swing.
- Meadow slice playable (2026-09-30), built by `Chopper > Setup > Rebuild Network Scenes`: T08 done; single 320 m terrain with pond and rim (`MeadowBuilder`, seed 1337); 420 baked trees (162 S, 176 M, 82 L) as data with plain GameObject views (instancing deferred to T12); server-side chop targeting (`TreeTargeting`); wood by size (2/4/8) with carry cap 40; hut with health and wood store starting at 50 percent; hold E to deposit and repair; IMGUI HUD. Felling is a scripted topple plus stump, not yet Rigidbody logs. Placeholders: primitive hut, generated ground textures. Headless host smoke test (`-chopperHost -chopperSmoke` in a development build) passes: 1/2/3 hits, wood 14, hut 250 to 390. 69 EditMode tests pass. Not yet visually checked in the editor.
- Playtest round 1 feedback (2026-09-30): meadow looks right. Added camera chase (eases behind the player while walking forward, pauses after manual look, tunable in `GameConfig`), hold left click to keep chopping (cooldown paced), and T14b chop feel: wood chips per hit, leaf burst on felling, distance faded camera shake, procedural chop and fall sounds (`ProceduralSfx`, to be replaced by CC0 samples). Hit stop is deliberately skipped because slowing time would desync a networked game. Smoke test and 69 tests still pass.
- Full loop built and headless tested (2026-09-30): zones (hub safe by day, Water N, Earth E, Fire S, Wind W, each with its own species, leaf tint, ground, fog, marker pillar; `ZoneMap`, `ZonePalette`, `ZoneTracker`), day and night (`DayNightClock`, `DayNightCycle`, lighting and fog by phase), saplings dropped per chop hit and woken at night up to a player scaled cap (`EnemyDirector`), `Treant` (S/M/L = 1/2/3 hits, element colored, NavMesh AI to hut or nearest player, `TreantAI`), player health, down and respawn (`PlayerHealth`), element kill buffs (`PlayerBuffs`, 3 kills, +50 percent against the element it beats, until dawn), boss saplings (1 percent, grow 2 days, must be chopped 30 hits, boss rises the next nightfall with a ground slam and drops 60 store wood), axe, armor and hut upgrades bought with the hut store (`PlayerUpgrades`, keys 1 2 3 near the hut), game over restart at day one, dawn withers survivors and regrows trees. The headless host smoke test checks 47 assertions across all of it and passes. 89 EditMode tests pass. Not yet visually checked in the editor.
- Revision (2026-09-30): D20 changed. One elemental kill loads the axe with that element for 15 hits (cap 15, config), each hit on an awake enemy spends a charge, and a charged element gives +50 percent damage against the enemy it beats. Charges reset at dawn. The old "3 kills until dawn" rule is gone. Day and night now 300 s day, 15 s dusk, 120 s night, 15 s dawn (450 s cycle), guarded by `PacingTests`. Smoke test now 49 checks. 97 EditMode tests pass. See `summary.md` in the repo root for the current status, next step and resume prompt.
- Lighting and feel pass (2026-09-30): URP shadow range 140 m, post processing volume with day and night grading, trilight ambient, sun arc and moon, reactive grass (`GrassField`, `GrassPushers`), Play always starts Boot and auto hosts in the editor, FishNet prefab list regenerated once per scene rebuild (removes the asset path hash log spam).
- Controls and character pass (2026-09-30): Valheim style controls (mouse always looks, scroll zoom, smooth follow, arrows and WASD), Quaternius mannequin player with animator and hand held axe (D24 done for the player). Verified in a separate project copy because the editor was open.
- Fix and additions (2026-09-30): the player stayed in a T-pose because the FBX imported with no avatar and the builder overwrote the Animator avatar with null; the import now creates an avatar, the builder fails loudly on missing clips, and the smoke test checks the pose at runtime. Added sprint (Shift toggle), crouch (Ctrl hold), a full procedural sound set with ambience, and a top of screen day and night clock. 55 smoke checks pass.
- UI and survival pass (2026-09-30): uGUI interface (`GameUi`) with Valheim style HUD, minimap with fog of war, Esc pause menu and saved settings, Tab inventory and workbench, M map; stamina; crafting of torches and healing salves from carried wood at the hut; upgrades moved to workbench buttons. Crafting and inventory, previously out of scope, are now in (first slice). 105 tests and 69 smoke checks pass; UI checked with real screenshots.
- World, defense and controls pass (2026-09-30): 640 m meadow (hub 110 m, forests beyond, dirt paths, grass and rocks only), 1700 trees, day 420 s and night 150 s, palisade ring with gate and R repair, infinite lives with a 12 s respawn (finite lives mode available behind a config switch), enemies target players then walls then hut (40 m aggro), untargetable saplings, start kit of one torch and one salve, cheaper stamina (chopping trees free), Xbox controller support, camera collision, real axe recordings. 105 tests and 89 smoke checks pass.
- Known gaps: Treants are tree models with glowing eyes and a bob walk (no limbs or animation), boss and slam visuals are only a scaled tree and camera shake, armor has no visual, the hut model does not change per level, sounds are placeholders, no save or load, NavMesh does not update when trees fall.
- Earlier next steps (done): Phase E, enemies. Start with T18 (Treant prefab), T19 (sapling drops from chops), T20 (NavMesh over the meadow), T21 (Treant AI to the hut), T22 (player health).
- `TreeHitTracker` plus 8 EditMode tests (hits 1/2/3 by size, per-tree damage, felled ignored, regrow). 55 of 55 tests pass.
- Movement weight: `PlayerMotor` ramps horizontal velocity with `PlayerAcceleration` and `PlayerDeceleration` (2026-09-30). Needs a quick play test to tune the two values; the T06 speed check limit is unchanged since top speed is the same.

### Build order: Meadow slice first (decided 2026-09-30)

The chopping mechanic must be sound before the big world and the four forests. Order of work:
1. Meadow slice: one small terrain (about 300 m across, single tile, Valheim meadows feel), the hut, and roughly 300 to 500 trees in small, medium and large, all from the Quaternius packs. Tasks: T08, then a scaled down T09 (single tile), T10 (hut with health), T11 (one meadow species set), T12 (database plus renderer, no zones).
2. Chop and wood: T14, T14b, T16, T17. Done when a player can walk out, chop small/medium/large trees, carry wood home, deposit it, and repair the hut, from every approach angle, in a 2 player host test.
3. Enemies: Phase E onward (T18 to T22, then Phase F).
4. Only after that: the full 3 km world, four forests, elements, bosses (T09 full, T11 other species, Phase G and H).

Reference material: `krituP/Unity-Tree-Chopping-Simulation` was reviewed and is NOT a base for our system. It pre-fractures a tree into many kinematic Rigidbody pieces, resolves axe hits by collision momentum, and scans the scene with `FindGameObjectsWithTag`. That cannot support thousands of trees, server authority, or fixed 1/2/3 hit counts. The only idea we keep is that a felled tree ends as physics-driven pieces. Our reference for the actual design is the Code Monkey Valheim tree video (stump plus falling log, damage interface) combined with D14 data-driven trees. `TreeHitTracker` (server hit counts by tree id, EditMode tested) is the core of T14.

### Phase C: World and art foundation
**T08 Asset import and conversion**
- Import the four Quaternius packs (FBX + textures) under `Assets/ThirdParty`, write `CREDITS.md`. Editor tool converts imported materials to URP Lit (alpha clip for leaves and grass), sets import scale and normals.
- Rename Seedling to Sapling in code with `FormerlySerializedAs` so config values survive.
- AC: every model renders in URP with no pink materials; leaves are cut out correctly; no console errors; tests pass.

**T09 World terrain**
- `WorldTerrainBuilder` editor tool builds the 3 x 3 terrain tiles from a seed per section 2: hills, hub basin, pond, rim mountains, painted layers, meadow grass details. Directional light, skybox, and fog.
- AC: same seed rebuilds identically; a player can walk from the hut to every forest and cannot leave the map; frame time acceptable at the hub with grass on.

**T10 Hub**
- Hut prefab built from village pieces (3 upgrade variants), pond water surface, fences and props, spawn points around the hut, `ForestZone` quadrants as data.
- AC: hut is enterable or at least solid with a clear door side; players spawn around it; zones report the right element for any position.

**T11 Tree species and prefabs**
- `SO_TreeSpecies_*` per zone: models, small/medium/large scale ranges, tinted materials, collider size. Falling-dummy prefab per model.
- AC: all 4 x 3 variants look right in each forest; one capsule collider each.

**T12 Tree database, renderer, and scatter tool**
- `TreeScatterTool` fills each forest from a seed with density, spacing, size mix, clearings, and hub exclusion, and bakes `SO_TreeDatabase` with ids and a spatial grid. `TreeRenderer` draws instanced per cell with culling and LOD; `TreeColliderStreamer` adds colliders near local players.
- AC: rebake with the same seed is identical; at least 4000 trees per forest by default; stable frame rate with 4 MPPM players; players collide with nearby trees.

**T13 Performance pass**
- Far clip, fog, shadow distance, LOD distances, grass distance in config. Profile with 4 players.
- AC: documented frame times at hub and in a forest; no single frame spikes from tree culling.

### Phase D: Chopping and wood
**T14 Tree hits and felling**: server resolves `SwingAxeServerRpc` against the tree grid, hit counts by size, `TreeHitObserversRpc` shake, `TreeFelledObserversRpc` hide plus local falling dummy. Felled trees leave a stump (the database tree switches to a stump model instead of vanishing) and the falling piece is a log with a Rigidbody, as in the Code Monkey tree physics video.
- AC additions: chopping works from every approach angle (front, side, back, diagonal) and while moving, since aim comes from camera yaw and not body facing; hit counts 1/2/3 verified for small, medium, large.

**T14b Chop feel** (right after T14): local-only feedback on each accepted hit and on felling. Small hit stop, camera shake scaled by tree size, wood chip particles at the hit point, chop and tree fall sounds from CC0 packs (Freesound, credited in `CREDITS.md`). All strengths in `GameConfig`. No networking beyond the existing hit and felled RPCs.
- AC: hits feel weighty in a 2 player test; nothing here changes server results.
**T15 Late-join tree sync**: felled bitset and damage via `TargetRpc`.
**T16 Wood**: `PlayerWood` SyncVar, grant on fell, carry cap, deposit at hut with Interact.
**T17 Hut health, repair, upgrades**: `Hut` SyncVars for health, wood store, level; repair and upgrade via Interact; model swap per level.

### Phase E: Saplings and enemies
**T18 Treant prefab and pooling**: sized, elemental, sapling and active states, pool prewarm.
**T19 Sapling drops**: per-hit rolls (D17), spawn table element, size from tree, visible to late joiners.
**T20 NavMesh**: bake over the terrain for the hub and paths out of each forest; hut obstacle.
**T21 Treant AI**: walk to hut, aggro on nearby players, attack hut and players; agents only on server.
**T22 Player health and respawn**: health SyncVar, damage from Treants, down and respawn at hut.

### Phase F: Loop
**T23 Day and night cycle**: phases, synced time, sun and ambient, fog color by phase.
**T24 Enemy director**: activate saplings at night (scaled cap), wither at dawn, regrow trees.
**T25 Game over and restart**.

### Phase G: Combat and elements
**T26 Axe damage on Treants**: hits by size, death to pool, never hit twice.
**T27 Element buffs**: kill counts per element, activation at threshold until dawn, damage math with EditMode tests.
**T28 Axe and HUD element visuals**.

### Phase H: Boss
**T29 Boss sapling**: rare roll, rooting, multi-day growth synced to late joiners, heavy chop.
**T30 Boss fight**: boss Treant at the next nightfall, bigger model, more health, special attack.

### Phase I: Feel and polish
**T31 Player character model and animations** (can move earlier if wanted): UAL mannequin, animator driven by motor state, chop animation synced with the swing.
**T32 Minimal HUD**: phase and timer, hut health and wood store, carried wood, active buffs, saplings tonight.
**T33 Player count scaling wiring**.
**T34 Four player soak test**: 3 full days, no errors, stable frame time.

---

## 9. Risks

| Risk | Mitigation |
|------|-----------|
| Tens of thousands of trees | Data plus instanced rendering (D14), colliders only near players, measured in T12/T13. |
| NavMesh over 9 km^2 | Bake only the ring the Treants use (forest edge to hut) with a coarser voxel size; trees baked as not walkable in that ring only. |
| Big import size in git | Only FBX + textures are imported. Consider Git LFS later. |
| Mixed art styles | Prefer Quaternius everywhere; Kenney only where nothing else fits. |
| Pooled objects keep stale state | Reset in `OnStartServer`/`OnStartClient` (v1). |
| FishNet API drift | Pinned 4.7.3; check the installed source before using new APIs. |

## 10. Open questions

- Q1 Boss sapling: default is "grown boss tree must be chopped; felling it spawns the boss at next nightfall". Alternative: it uproots and attacks on its own when fully grown, and chopping it early prevents or weakens the fight. Which one?
- Q2 Hut upgrades: default is 3 levels that raise max health. Any other upgrade effects wanted (bigger wood store, defensive thorns, light at night)?
- Q3 Wood on death: default keeps carried wood. Should a downed player drop it?
