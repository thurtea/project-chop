# Chopper playtest, 2026-10-16 (about 15 minutes)

Build: `Builds/Dev/Chopper.exe` (dev build). Host a new world. Open the console with the backtick key (`` ` ``, left of 1) or F5. Esc closes it. Player input is paused while it is open. Type `help` for every command.

Write what you see on each "Result:" line. Feel is yours to judge.

## 1. Feet and clipping (3 min)
- Walk up and down a steep grassy slope near the hut, then stop on it. Look for: soles on the ground, no feet sunk into the slope or floating above it.
  Result:
- Walk the hut stairs up and down, slowly, then stop halfway. Look for: feet on the treads, no legs through the steps.
  Result:
- Walk into a hut wall and rub along it. Look for: no arms, axe or body through the wall.
  Result:
- In a mine (`tp` to one, or walk in), walk over the uneven floor. Look for: feet on the floor, no sinking.
  Result:
- `spawn earth 2` on a slope and watch the saplings stand. Look for: their bottoms on the ground, not buried or floating.
  Result:

## 2. Swimming (2 min)
- Swim in the pond or the sea. Then press Sprint once (it is a toggle) while swimming. Look for: clearly faster strokes (about 3.0 m/s normal, 4.8 m/s with sprint on), stamina draining faster.
  Result:
- Press Crouch while swimming. Look for: nothing happens. Crouch on land, then walk into deep water. Look for: the crouch is gone when you swim.
  Result:

## 3. The three-storey hut (4 min)
- Walk in the front door. Hub floor: fireplace under the chimney, notice board, tables and stools, chest. Look for: no see-through gaps, door and headroom fit.
  Result:
- Stairs at the -X end down to the basement: barrels, crates, sacks, workbench.
  Result:
- Stairs from the hub (+X end) up to the dormitory: beds and bunks, crates.
  Result:
- Stairs (-X end) up to the workshop: workbench, tool wall, vise, spinning wheel. Look for: lit enough to see.
  Result:
- Outside, the ladder on the +X end: walk into it to mount. Look for: he snaps to the ladder, faces it, climbs hand over hand; at the top keep pushing in to step through the workshop door. Walk away from the wall to climb down; at the bottom keep walking away to step off.
  Result:
- Rooms: do they feel lived-in, or still too big and empty?
  Result:

## 3b. Hut look and camera (3 min)
- At noon (`time 12`), walk round the hut outside. Look for: walls and roof fully solid, no floor, rafters or mountains showing through.
  Result:
- Inside on every floor: no fog or milky haze, no ghosting over your character.
  Result:
- Pan the camera slowly across walls, floors and stairs. Look for: no flickering or shimmering faces.
  Result:
- Walk the stairs and stand in corners with the camera swung into the wall. Look for: the camera stays in the room, never inside a wall.
  Result:
- Outside, put a pine between the camera and yourself. Look for: the tree vanishes (its shadow stays) while it blocks you, and comes back after.
  Result:

## 4. Mine portals (1 min)
- Walk to a mine entrance in daylight. Look for: dark log frame, black opening, cyan crystal clusters, rails and a cart, craggy dark rock.
  Result:

## 5. Haze and light (2 min)
- `time 9`, `time 12`, `time 17`: look across the hub toward the mountains. Look for: soft distance haze, warm toward the sun and cool away, sun shafts through trees.
  Result:
- Step inside the hut: haze and shafts should drop away indoors. Look for: no milky film, no ghosting over your character.
  Result:
- `time 22` (or `night`), light a torch. Look for: dark night, the torch lights a few trees ahead.
  Result:

## 6. Combat at the hub with the console (3 min)
- `god` (toggle), `spawn fire 2`, `spawn plain 2`, fight them. Look for: enemies come at you, hits land.
  Result:
- `wave` (6 by default, or `wave 10`): a wave rises 24-32 m around the hut. Look for: they head for the hut and the players.
  Result:
- `killall` clears every tree enemy. `heal` fills health. `day` / `night` / `time <0-24>` change the clock.
  Result:

## Known issues
- Hut rooms still read large (21.6 x 14.4 m floors); furniture is real size, so floors can look sparse.
- The ladder climb is a procedural hand-over-hand pose, seen only on your own screen (other players see the old pose).
- Trees that block the camera are hidden (shadow kept), not dithered.
- Hut exterior: the outcrop and chimney read small from the yard; no cutaway view.
- Mine portals: rock reads near-black in daylight (concept is lighter grey-blue), no moss on the crag, the cart is small.
- Cave interiors are still evenly lit with flat floors (item 3 not started).
- Fishing line arc is still thin; New World x Ocarina of Time polish not done (item 4).
- Enemy weapon archetypes (Earth mace, Wind axe, Fire sword, Water spear), element enchantments, felled-tree night spawns and the hit-count element buff are designed but not built yet (item 5).
- Boat and shark minigame not started (item 7).
- Player collision radius stays 0.50 m (the mesh is that wide with the arms).
