# Motions

Last updated: 2026-10-09. Art direction: the Valheim look is retired and a new one is not chosen (`requirements-assets.md`). Do not download a new batch of clips to match the old look. The clips already in the game stay until you approve or reject them. A missing or rejected slot keeps its code stand-in, so Chopper never stands in a T-pose.

First priority of the old list was real motions, because the motions written in code look stiff. That queue waits on the new art direction, except for your review of what is already wired.

## In the game now

Restored 2026-10-09 to the October 8 set. Do not replace these with Blender splits, KayKit, or new libraries.

Your Mixamo files are back in `Assets/ThirdParty/Mixamo_Motions/`: idle (`Idle.fbx`), walk (`Walking.fbx`), run (`Running.fbx`), sprint (`Fast Run.fbx`), swim (`Swimming.fbx`), swim idle (`Treading Water.fbx`), attack 1 (`Standing Melee Attack Horizontal.fbx`), attack 2 (`Standing Melee Attack 360 Low.fbx`), attack 3 (`Standing Melee Combo Attack Ver. 1.fbx`), death (`Fall Flat.fbx`, file name `down.fbx`).

Chop is `TreeChopping_Loop` from Quaternius Universal Animation Library 2 (`UAL2_Standard.fbx`), on the same skeleton as the body. Crouch, the fall loop, roll, jab, cross, guard, bandage, and the torch carry are clips inside `UAL1_Standard.fbx`.

Mining, fishing, gathering, and hammering are the Kevin Iglesias Human Animations clips: `HumanM@MiningOneHand01_R - Wall`, `HumanM@FishingThrow01`, `HumanM@Fishing01 - Loop`, `HumanM@FishingFighting01`, `HumanM@FishingPullOut01`, `HumanM@Gathering01`, `HumanM@HammeringGround01_R - Loop`. The bandage wrap and the reach to the back stay the hand-keyed clips. That pack has no bandage and no draw.

The Blender re-exports from 2026-10-09 are in `SourceAssets/Motions/archive/blender-splits-2026-10-09/` and are not imported. KayKit stays in `SourceAssets/Motions/new/` and is not wired. A slot with no Mixamo file and no library clip still uses the hand-keyed stand-in.

When a motion is approved and the new direction still wants it:

1. Convert each file onto Chopper's skeleton with `Tools/mixamo_convert.py` (Blender 5.2), import it as Humanoid, and swap it in for the code stand-in of the same name.
2. One group at a time (Moving first), with pictures of each for you to approve before the next group. A stand-in is removed only when its replacement is approved.
3. Match each loop's playback to the player's real speed so the feet do not slide, and check every loop for a hitch at the seam.
4. When the axe idle, walk and run arrive, delete the code wrist turn in PlayerAxe: the clips hold the axe themselves. The fist layer and grip stay.
5. Line up the gameplay moments with the clips: the frame the axe meets the tree or the enemy, the frame the bandage is done, the cast release, the hammer blow, the hand over the shoulder for the holster.
6. The draugr and golems keep the stand-ins until their own motions arrive.

## Where new files go

- Folder: `Chopper/SourceAssets/Motions/new/`. Save each under the exact name in the Save as column. A file with that name replaces the code stand-in; a missing file keeps the stand-in.
- From Mixamo: FBX for Unity (.fbx), Without Skin, 30 fps, keyframe reduction none. Tick In Place for anything that walks, runs, crawls or swims.
- One handed axe (or tool) in the right hand, left hand free (it holds the torch). Props in the file do not matter; the game adds the axe, pick, rod and hammer.
- Facing forward, starting and ending in the same spot. Loops must join (last frame matches the first).
- One source for everything looks best.
- Already on disk from 2026-10-08 (Mixamo, in `SourceAssets/Motions/raw`): Idle, Walking, Running, Fast Run, Crouch Walk Forward and Back, Falling To Landing, Falling Flat Impact, Fall Flat, Swimming, Treading Water, three melee attacks, strafes, turns and walking backwards. These are candidates, not approved replacements. The On disk column names the matching source file. `Falling To Landing` is a one-shot candidate for a landing, not a loop for the in-air Falling motion.

## Review, 2026-10-08 (waiting for your approval)

Previews: `Chopper/Shots/motions-2026-10-08/` (`slot_*.png` is the clip on Chopper's body, `motion_*.png` the code stand-in, `ingame_*.png` the in-game motion tour). Do not mark these approved on your behalf.

| Slot | Clip | Status | Notes |
|---|---|---|---|
| idle | Idle | In, to approve | Relaxed, axe hangs blade down at the side. Creator preview is posed (no T-pose) |
| walk | Walking | In, to approve | Feet planted (slide 0.09 m/s); the axe swings with the arm |
| run | Running | In, to approve | Good without the axe (slide 0.66 m/s). With the axe drawn the arm pump throws the head up and back over the shoulder |
| sprint | Fast Run | In, to approve | Playback matched to the body (was 13% fast). Same axe problem as run, worse: the head swings into the ground |
| swim, swim_idle | Swimming, Treading Water | In, to approve | Prone breaststroke and treading water read fine |
| crouch_walk, crouch_idle | Crouch Walk Forward | Rejected, stand-in kept | Cartoon sneak: one hand at the face, the axe arm held out level at head height, feet off the ground |
| fall | Falling To Landing | Rejected, stand-in kept | Lands and stands up inside the clip, so a long fall shows a landing pose in mid air, and the axe arm flails overhead |
| land_hard | Falling Flat Impact | Rejected, stand-in kept | Starts upside down above the body and never gets up; the game would snap from lying flat to standing |
| chop | none on disk | Stand-in kept | The three melee attacks are fighting swings, not a chop into a trunk |

Open problem, left as a choice: with the axe drawn, run and sprint pump the axe arm. A one handed weapon run keeps the axe steady. An arm layer that holds the axe arm still is only if you ask for it. Do not add that layer, and do not move to the next group, until you say so.

Still wanted for the Moving group once a direction exists: a crouch walk with the arms low, a falling loop (in the air only), a hard landing that gets back up, a jump start, a landing and a roll.

Priority in the tables: **A** was needed before anyone plays again, **B** for the demo, **C** when there is time. Those letters belong to the old plan. They are not a new art order.

### Moving

| Motion | Loop | Notes | On disk | Priority | Save as |
|---|---|---|---|---|---|
| Idle | Loop | Standing, relaxed, axe at the side | `Idle.fbx` | A | `idle.fbx` |
| Walk | Loop | About 1.5 m/s | `Walking.fbx` | A | `walk.fbx` |
| Run | Loop | About 6 m/s, the axe swings with the arm | `Running.fbx` | A | `run.fbx` |
| Sprint | Loop | About 9 m/s, leaning in | `Fast Run.fbx` | A | `sprint.fbx` |
| Crouch idle | Loop | Sneaking pose, still | | A | `crouch_idle.fbx` |
| Crouch walk | Loop | Sneaking, about 2.5 m/s | `Crouch Walk Forward.fbx` | A | `crouch_walk.fbx` |
| Jump start | | Push off | | A | `jump_start.fbx` |
| Falling | Loop | In the air | | A | `fall.fbx` |
| Landing | | Touch down and recover | `Falling To Landing.fbx` (preview first) | A | `jump_land.fbx` |
| Hard landing | | A fall long enough to hurt (over 4 m) | `Falling Flat Impact.fbx` | B | `land_hard.fbx` |
| Dodge roll | | Forward roll, about 0.7 s, ends standing | | A | `roll.fbx` |
| Swim idle | Loop | Treading water, head above | `Treading Water.fbx` | B | `swim_idle.fbx` |
| Swim | Loop | Head above water | `Swimming.fbx` | B | `swim.fbx` |

### Logging

| Motion | Loop | Notes | On disk | Priority | Save as |
|---|---|---|---|---|---|
| Chop | Loop | Side swing into a trunk at waist height, about 1 s; the blade meets the wood about halfway | | A | `chop.fbx` |
| Draw axe | | Hand over the right shoulder, takes the axe off the back, about 0.6 s | | B | `draw.fbx` |
| Holster axe | | Puts the axe over the right shoulder onto the back | | B | `holster.fbx` |
| Pick up | | Bends to take wood, branches or drops off the ground | | B | `pick_up.fbx` |
| Carry | Loop | Arms holding logs or a load in front (upper body only is fine) | | C | `carry.fbx` |

### Fighting

| Motion | Loop | Notes | On disk | Priority | Save as |
|---|---|---|---|---|---|
| Ready idle | Loop | Axe up, ready to fight | | B | `ready_idle.fbx` |
| Attack 1 | | Quick one handed swing, about 0.6 s | `Standing Melee Attack Horizontal.fbx` | A | `attack_1.fbx` |
| Attack 2 | | Swing back the other way | `Standing Melee Attack 360 Low.fbx` | A | `attack_2.fbx` |
| Attack 3 | | Heavy finisher, steps forward, about 0.9 s | `Standing Melee Combo Attack Ver. 1.fbx` | A | `attack_3.fbx` |
| Block idle | Loop | Axe raised across the body | | A | `block_idle.fbx` |
| Block hit | | Taking a blow on the guard (also the parry) | | B | `block_hit.fbx` |
| Punch 1 and 2 | | No axe: jab and cross | | B | `punch_1.fbx`, `punch_2.fbx` |
| Hit | | A flinch | | A | `hit.fbx` |
| Death | | Falls and stays down (also crushed by a tree) | `Fall Flat.fbx` | A | `death.fbx` |
| Get up | | Lying to standing (revived, out of bed) | | B | `get_up.fbx` |

### Mining

| Motion | Loop | Notes | On disk | Priority | Save as |
|---|---|---|---|---|---|
| Mining strike | Loop | Overhead pickaxe blow into a rock face at chest height, about 1 s | | B | `mine.fbx` |
| Mining, low vein | Loop | Kneeling or bent over, striking near the floor | | C | `mine_low.fbx` |

### Fishing

| Motion | Loop | Notes | On disk | Priority | Save as |
|---|---|---|---|---|---|
| Cast | | Rod back over the shoulder and forward | | B | `fish_cast.fbx` |
| Waiting | Loop | Rod held out in front, small movements | | B | `fish_idle.fbx` |
| Strike | | Pulling the rod up sharply on a bite | | C | `fish_strike.fbx` |
| Reel | Loop | Reeling in with the rod up, leaning back | | B | `fish_reel.fbx` |
| Catch | | Lifting the fish out of the water | | C | `fish_catch.fbx` |

### Gathering, building and living

| Motion | Loop | Notes | On disk | Priority | Save as |
|---|---|---|---|---|---|
| Gather | | Kneels or bends to pick a plant | | B | `gather.fbx` |
| Hammering | Loop | One handed hammer blows at chest height (placing and repairing) | | B | `hammer.fbx` |
| Crafting at the bench | Loop | Working at a table with both hands | | C | `craft.fbx` |
| Bandage | | Wraps the left forearm, about 1.5 s | | B | `bandage.fbx` |
| Eat | | Hand to mouth, a couple of bites | | C | `eat.fbx` |
| Open chest | | Lifts a lid at knee height | | C | `open_chest.fbx` |
| Lie down and sleeping | Loop | Into bed, then asleep | | C | `sleep_down.fbx`, `sleep_idle.fbx` |
| Sit by the fire | Loop | Sitting on the ground or a log | | C | `sit_idle.fbx` |

### Enemies

The draugr and golems use Chopper's stand-ins today. Wanted, in `SourceAssets/Motions/new/enemies/`: `enemy_idle.fbx`, `enemy_walk.fbx`, `enemy_run.fbx`, `enemy_attack.fbx`, `enemy_hit.fbx`, `enemy_death.fbx`, `enemy_rise.fbx` (rising out of the ground). Old priority B. Do not import a new enemy set until the art direction names the creature.
