# Art direction (t50u) - governing look spec

Source: the user's brief as relayed by the dispatching agent on 2026-10-16 (the relayed summary is reproduced word for word below; replace with the user's original text if it differs).

Move away from a Minecraft-style exposed-cube grid to a chunky, voxel-adjacent low-poly look with atmospheric depth.

1. Geometry: a tight uniform bevel or chamfer on visible silhouettes, with no razor 90-degree corners. Stay grid-snapped, but hills and large structures use broad, sweeping, stepped planes, not per-block faces.
2. Materials: detail (wood grain, bark, stone cracks, leaf layers) lives in flat-shaded, hand-painted-style textures baked onto low-poly surfaces, not in geometry noise. Use flat or per-face shading or custom normals, with no noisy specular across cubes.
3. Lighting (crucial): bring back a SUBTLE height fog and depth haze, desaturating distant terrain and mountains to show scale. Never heavy or opaque fog; the user hated the last fog. Restrained god-rays through the canopy. Warm golden sun in the morning and late afternoon, with saturated cool blue or teal ambient and shadow.
4. Water: organic, terraced, curving shorelines, not staircase edges. A smooth stylized fluid plane with vertex waves and contact foam; no blocky water grid. Sky: puffy stylized cloud meshes with soft edge gradients, no hard rectangular clouds.
5. Plus lightning, volumetrics, and enemy movement and AI as close as possible to Valheim and Enshrouded.

## Plan

Finish the current polish round, then this is the next major work block, before item 5's content. Increments with proof (shots beside the concepts) each step, in order:

a) bevel or chamfer and flat-shading pass on the block shader and chunk meshes (tuning only, do not replace Chopper/Block without asking);
b) subtle haze and warm/cool grade, haze off by default until proven subtle in shots;
c) water shader and terraced shores;
d) clouds;
e) god-rays.

Enemy AI and movement toward Valheim and Enshrouded goes with item 5.

Boundary: do not replace Chopper/Block, Chopper/Water, the chunk meshes, DayNightCycle or ZonePalette. If a step needs replacing rather than tuning, stop and report.

## Lighting and volumetrics overhaul (queued after item 5 and the pick hotbar slot), toward Valheim
- Phase 0: audit the URP asset, renderer and volume profiles; baseline shots (dawn, noon, dusk, night, forest, hut, cave, sea) and a frame-time capture.
- Phase 1: tonemapping, bloom (threshold about 1.1), per-zone and time-of-day colour lookups, warm highlights and teal shadows, sun and ambient colour curves, 4 shadow cascades to about 120 m, SSAO medium at half resolution; under +1 ms.
- Phase 2: one custom height and distance fog pass tinted toward the sun plus screen-space sun shafts; hard density cap about 0.3, no fog indoors, an Atmosphere slider and console command; under +0.7 ms.
- Phase 3: moonlight curve, a minimum night ambient so night stays readable, torch and fire light volumes with flicker.
- Rules: only tune and add hooks to DayNightCycle and ZonePalette; ask before replacing ZoneTracker's fog. Total budget no more than 3 ms. Proof per phase with shots beside the concept sheets and Valheim references; report after each phase.

## Hut exterior reference

Chopper/Art/Concepts/chopper-hut-outside.jpg is the reference for the hub hut exterior: a Norse log longhouse with horizontal stacked log walls and dark vertical posts, a steep stepped layered shingle roof in warm golden wood with thick bevelled edges, crossed carved gable horns at both ridge ends and on the porch gable, a small gabled dormer on the ridge, a front porch with its own gable roof, a grey stone foundation band and stone entry steps, small dark windows, bevelled voxel-adjacent forms in a misty pine forest.

## Tree enemy concepts (official references, 2026-10-16)

- Non-elemental: Art/Concepts/non elemental tree.jpg - dark bark, green willow hair, blue eyes, antlers, shield; carries a random plain mace, axe, sword or spear (no enchantment).
- Earth: Art/Concepts/earth elemental tree.jpg - gold veins, willow, gold crown; mace.
- Fire: Art/Concepts/fire elemental tree.jpg - charred body, orange cracks, flame hair; sword.
- Water: Art/Concepts/water elemental tree.jpg - blue water body, swirl hair, coral base; spear.
- Wind: Art/Concepts/wind elemental tree.jpg - pale cloud body; axe.

