# Chopper: asset requirements (sounds, UI icons, animations)

This list is kept up to date as the game grows. Use it to source assets: mark a row **have** once you've dropped a file in, and add rows whenever you like.
Audit taken 2026-10-17 from the repo, read only: `Assets/_Chopper/Resources/Audio` (not changed), the GameIcons folder, PlayerAnimator.controller and the code that asks for sounds.

## Where to put files
- Put new files in the inbox: `E:\Projects\project-chop\Chopper\Incoming\sounds\`, `...\Incoming\icons\` and `...\Incoming\animations\`. An agent imports them into the project and wires them up, then sets the row to **have**.
- Sounds go live in `Assets/_Chopper/Resources/Audio/`. Agents never change that folder on their own; it only changes when you ask for an import.
- Icons go live in `Assets/ThirdParty/GameIcons_net/Resources/GameIcons/` (loaded as `GameIcons/<id>`). Custom voxel-style icons can replace these.
- Animations go live in `Assets/_Chopper/Art/Animations/<group>/` and are hooked into `PlayerAnimator.controller` or the enemy controllers.

## Naming convention
- Sounds: `<group>-<name>-<n>.wav` (or `.ogg`), lower case, hyphens, and n counting up from 1 for variations. The game picks a random variation. Example: `sword-swing-1.wav`, `sword-swing-2.wav`.
- Icons: `<id>.png`. The id is lower case with hyphens, for example `mace`, `skill-fishing`, `find-gold`.
- Animations: `<Rig>@<Action>[_<Variant>].fbx`. Examples: `Player@Sword_Sweep.fbx`, `Treant@Smash.fbx`.

## Specs (unless a row says otherwise)
- **Sound one-shots:**
  - 44.1 kHz, 16-bit WAV (OGG is fine too).
  - Mono for anything placed in the world (3D); stereo only for UI and music.
  - Peak around -3 dBFS, trimmed tight, no long tails unless noted.
  - Give 3 to 6 variations for anything that repeats (steps, hits, swings).
- **Sound loops:**
  - 44.1 kHz stereo OGG, 30 s to 3 min, seamless.
  - Ambience around -18 LUFS; music around -16 LUFS.
- **Icons:**
  - 128 x 128 px PNG with transparency (256 px also fine; current icons are 256 px).
  - Chunky voxel / low-poly style that matches `Chopper/Docs/art-direction.md`.
  - Readable at 48 px, light 1-2 px dark outline, no text in the icon.
- **Animations:**
  - Humanoid FBX on a Mecanim Humanoid rig. In-place, no root motion unless noted.
  - 30 fps.
  - Note the frame count and the contact frame for any attack.
  - "loop" means the first and last frame match.
  - Enemies (treants) currently use a generic rig without an Animator. Enemy animation rows need either an FBX for that rig or a Blender-script pose set.

States used below: **have** = in the game; **placeholder** = something plays but it's generic, borrowed or wrong; **missing** = nothing yet.

## 1. Player movement and tools
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| footstep-grass-n | step on grass | have (5) | one-shot 0.2-0.4 s, mono |
| footstep-dirt-n | step on dirt and paths | have (8) | one-shot, mono |
| footstep-water-n | wading | have (3) | one-shot, mono |
| footstep-stone-n | caves, crypts, hut stone | missing | one-shot, 6 vars |
| footstep-wood-n | hut floors, stairs | missing | one-shot, 6 vars |
| footstep-snow-n | snowy ground | missing | one-shot, 6 vars |
| jump-n / land-n / land-hard-n | jump take-off, landing | missing | one-shot 0.2-0.5 s |
| roll-n | dodge roll | missing | one-shot 0.4 s |
| swim-stroke-n | swimming | missing | one-shot 0.5 s, 4 vars |
| water-enter / water-exit | splash in or out | placeholder (splash-1) | one-shot 0.6-1 s |
| ladder-climb-n | ladder rungs | missing | one-shot, 4 vars |
| player-hurt-n | taking damage | have (4) | one-shot |
| player-death | death | missing | one-shot 1-2 s |
| breath-low-stamina | stamina empty | missing | one-shot 1 s |
| axe-slash-n | axe swing whoosh | have (4) | one-shot 0.3 s |
| axe-impact-n | axe hits wood or enemy | have (6) | one-shot |
| chop-trunk-n | blade into a trunk (tree chop only) | placeholder (axe-impact) | one-shot, 6 vars, woody thunk |
| wood-chips | chips flying on chop | missing | one-shot 0.3 s |
| tree-creak / tree-fall-n | tree about to fall, crash | have (fall ×3), creak missing | creak one-shot 1-2 s |
| stump-clear / log-split-n | finishing logs and stumps | missing | one-shot |
| wood-pickup-n / wood-deposit-n | gather and drop wood | have (4 / 2) | one-shot |
| pick-strike-n | pickaxe on stone or ore | missing | one-shot, 6 vars |
| hammer-place-n | building hammer tap | missing | one-shot, 4 vars |
| torch-ignite / torch-loop | light torch, crackle | missing | one-shot 1 s / loop 10 s mono |
| bandage | using a bandage | missing | one-shot 1.5 s |
| eat-fish | eating | missing | one-shot 1.5 s |
| equip-tool-n / holster-n | draw or put away a tool | missing | one-shot 0.3 s |
| Anim: Locomotion (idle, walk, run, sprint, crouch) | moving | have (Kevin Iglesias, KayKit) | humanoid, loop |
| Anim: Jump, LandHard, Roll | movement | have | humanoid |
| Anim: Swim | swimming | have | loop |
| Anim: Ladder climb | ladders | placeholder (procedural ClimbCycle) | humanoid loop, 24-32 f |
| Anim: Tree chop (two-handed, overhead + diagonal) | chopping a targeted tree | placeholder (procedural ToolPose) | humanoid, 2 variants, ~24 f, contact frame noted |
| Anim: Pick strike | mining | placeholder (procedural) | humanoid ~18 f |
| Anim: Hammer tap | building | placeholder (procedural) | humanoid ~14 f |
| Anim: Torch hold, Rod hold | holding items | placeholder (old clips) | humanoid upper-body loop |
| Anim: Bandage / Heal, Eat | items | have (Heal) / missing (Eat) | humanoid ~40 f |
| Anim: Hit react, Death, Get up | damage | have (HitReact) / missing (Death, Get up) | humanoid |

## 2. Weapons (axe, sword, mace, spear)
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| sword-swing-n | sword sweep whoosh | missing | one-shot 0.3 s, 4 vars |
| sword-hit-n | sword hits a treant (bark + steel) | missing | one-shot, 4 vars |
| mace-swing-n | heavy overhead whoosh | missing | one-shot 0.5 s, 3 vars |
| mace-smash-n | smash impact plus ground thud (splash) | missing | one-shot 0.6 s, 3 vars |
| spear-thrust-n | thrust whoosh | missing | one-shot 0.25 s, 4 vars |
| spear-hit-n | pierce impact | missing | one-shot, 4 vars |
| weapon-switch | key 8, next weapon | missing | one-shot 0.2 s |
| shield-block-n / shield-parry-n | block and parry | have (4 / 3) | one-shot |
| stagger | enemy staggered | missing | one-shot 0.4 s |
| elemental-hit-<element> | buffed hit, per element (water, fire, earth, wind) | missing | one-shot 0.4 s each |
| Anim: Axe combat chop (fast) | axe combat swing, 3-hit combo | placeholder (procedural AxeFight) | humanoid, 3 clips ~16 f |
| Anim: Sword sweep | wide horizontal sweep, combo of 3 | placeholder (procedural) | humanoid, 3 clips ~18 f |
| Anim: Mace overhead smash | two-handed overhead, slow | placeholder (procedural) | humanoid, 2 clips ~28 f |
| Anim: Spear thrust | long-reach lunge thrust | placeholder (procedural) | humanoid, 2-3 clips ~16 f |
| Anim: Guard / Parry per weapon | block pose | have (generic Guard, Parry) | humanoid |
| Icons: axe, sword | hotbar and bag | have | 128 px |
| Icons: mace, spear | hotbar and bag | missing (craft list falls back) | 128 px |

## 3. Enemies (treants) per type and element
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| treant-wake | sapling or treant rises | missing | one-shot 1.5 s, creaking wood |
| treant-step-n | heavy footsteps | missing | one-shot, 4 vars (pitch varied per size) |
| treant-attack-<weapon> | axe, sword, mace, spear swing | missing | one-shot 0.5 s each |
| treant-hurt-n / treant-death | hit and death | missing | one-shot / 2 s |
| treant-idle-<element> | voice or ambience per element (water gurgle, fire crackle, earth grind, wind howl) | missing | loop 5-10 s mono, 3D |
| sapling-sprout | sapling planted after a fell | missing | one-shot 0.8 s |
| night-horn | night begins | have | one-shot |
| boss-roar-<element> | boss intro | missing | one-shot 3 s |
| hut-hit-n | enemy hits the hut | have (4) | one-shot |
| Anim: Treant walk, idle, wake, hit, death, stagger | all enemies | placeholder (procedural or none, no Animator) | generic rig or Blender pose set |
| Anim: Treant chop, slash, smash, stab | per weapon archetype | have (Blender Elemental_* actions) | keep |
| Icons: water, fire, earth, wind | element markers | have | 128 px |

## 4. Fishing
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| rod-cast | cast whoosh | missing | one-shot 0.6 s |
| bobber-plop | float lands | missing | one-shot 0.4 s |
| bite-nibble / bite-hook | bite cue, HOOKED! | missing | one-shot 0.3 s / 0.6 s |
| reel-loop | reeling | missing | loop 2 s seamless |
| line-tension / line-snap | strain, snapped line | missing | loop 2 s / one-shot 0.5 s |
| fish-splash-n / fish-leap | thrash and leap | placeholder (splash-1) | one-shot, 4 vars |
| catch-fanfare | fish landed | missing | stereo one-shot 1.5 s |
| underwater-loop | underwater cut camera | missing | loop 10 s |
| Anim: FishCast, FishWait, FishFight, FishCatch | fishing | have | humanoid |
| Icons: rod, fish, fed | hotbar and status | have | 128 px |
| Icons: one per fish kind | catch log and bag | missing (generic fish) | 128 px |

## 5. Boat and shark
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| boat-deploy / boat-pickup | B key | missing | one-shot 1 s |
| oar-stroke-n | rowing | missing | one-shot 0.8 s, 4 vars |
| hull-water-loop | moving on water | missing | loop 10 s |
| shark-hook / shark-tow-loop / shark-leap / shark-catch | shark fight phases | missing | one-shots 1-2 s, loop 5 s |
| sea-ambience | open ocean | missing | loop 1-2 min stereo |
| Anim: Sit rowing (oars) | in the boat | placeholder (procedural arm aim) | humanoid loop 32 f |
| Anim: Balance during tow | shark tow | missing | humanoid loop |
| Icons: boat, shark rod | bag and hotbar | missing | 128 px |

## 6. Mining
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| pick-strike-n | see section 1 | missing | |
| ore-break / find-reveal | vein worked, find revealed | missing | one-shot 1 s |
| cave-ambience | mines | missing | loop 1-2 min stereo, drips and low wind |
| crystal-hum | cyan crystals | missing | loop 5 s mono 3D |
| cart-rumble | portal cart | missing | loop 4 s mono 3D |
| Anim: Mine | mining | have (Mine state) | keep |
| Icons: find-* (19), iron, shard, find-pickhead | finds and materials | have | 128 px |

## 7. Building
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| piece-place-<material> | wood or stone piece placed | missing | one-shot 0.5 s, 3 vars each |
| piece-remove / piece-invalid | remove, can't place here | missing | one-shot 0.3 s |
| craft-complete | workbench craft | placeholder (upgrade) | one-shot 1 s |
| workbench-use / chest-open / chest-close / door-open / door-close | stations | missing | one-shot 0.5-1 s |
| campfire-loop | campfire | missing | loop 10 s mono 3D |
| Icons: piece-* (8), workbench, chest, forge, bed, repair, upgrade | build menu | have | 128 px |

## 8. UI and HUD
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| ui-click / ui-hover | menus | have | stereo one-shot |
| ui-open / ui-close | Tab inventory, map | missing | one-shot 0.2 s |
| ui-error | can't do it | missing | one-shot 0.2 s |
| toast-skill-up | skill level gained (phase 2) | missing | one-shot 1 s |
| toast-quest | quest updated or done | missing | one-shot 1 s |
| upgrade | upgrade bought | have | one-shot |
| Icons: skill-fishing, skill-logging, skill-mining, skill-axe, skill-sword, skill-mace, skill-spear | Skills page (phase 2) | missing | 128 px |
| Icons: health, stamina, wet, cold, burning, rested, fed | status | have (wet, cold, burning, rested, fed) / missing (health, stamina) | 64-128 px |
| Icons: map-player, map-house, map-crypt, map-death | minimap | have | 64 px |
| Icons: map-home, map-barrier, map-boss | biome homes (planned) | missing | 64 px |
| Icon: keyboard / pad glyphs | prompts | missing | 64 px set |

## 9. Ambience and music per biome and day/night
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| ambience-day | hub by day (requested by code) | missing | loop 1-2 min stereo |
| dawn-birds | dawn sting (requested by code) | missing | one-shot or loop 30 s |
| ambience-night | crickets, owls | missing | loop 1-2 min |
| ambience-<zone>-day / -night | water, fire, earth, wind forests (8 loops) | missing | loop 1-2 min stereo |
| wind-gust-n, rain-loop, thunder-n | weather | missing | one-shots / loop |
| pond-lapping / sea-waves | water bodies | missing | loop 30 s mono 3D |
| music-day-n / music-night-n / music-title | music | have (3 / 2 / 1) | stereo OGG |
| music-boss / music-crypt | boss fight, crypt | missing | loop 2-3 min |

## 10. Crypt and boss
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| crypt-door-open / crypt-door-close | enter or leave | missing | one-shot 2 s |
| crypt-ambience | inside crypts | missing | loop 1-2 min |
| chest-glint / chest-loot | crypt chest | missing | loop 3 s / one-shot 1 s |
| arena-gate / guardian-wake | arena | missing | one-shot 2 s |
| boss-roar-<element>, boss-death, boss-phase | bosses | missing | one-shot 2-4 s |
| Anim: boss attacks (slam, sweep, summon) | bosses | missing | generic rig or Blender poses |

## 11. Quests (planned)
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| quest-accept / quest-step / quest-complete | quest flow | missing | stereo one-shot 1-2 s |
| Icons: quest-marker, quest-done, quest-home, quest-barrier | HUD and map | missing | 64-128 px |

## 12. Biome home and magic barrier (planned)
| Name | Purpose / trigger | State | Spec |
|---|---|---|---|
| barrier-raise | barrier activated | missing | one-shot 3 s |
| barrier-hum-loop | barrier active | missing | loop 10 s mono 3D |
| barrier-hit-n / barrier-fail | enemy strikes it, upkeep ran out | missing | one-shot 0.5 s / 2 s |
| barrier-upkeep | paying upkeep | missing | one-shot 1 s |
| raid-horn | nightly crypt raid starts | missing | one-shot 3 s |
| Icons: barrier, upkeep | HUD | missing | 128 px |
| Anim: channel / cast (raise barrier, upkeep) | player | missing | humanoid ~60 f |

## Notes
- Code already asks for `ambience-day` and `dawn-birds`, but there are no files for them yet. Those are the first sounds to source.
- Credits for the current sounds are in `Assets/_Chopper/Resources/Audio/CREDITS.txt`. Add a credit line for each new file.
