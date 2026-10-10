# Chopper: status toward a release

Written 2026-10-05 (v0.2.9) after a full pass over the code, the docs and the folders. Two targets are measured: the **Steam demo** (the next milestone: fishing, building, crafting and the new night creatures finished, real art in place) and **1.0** (a full game).

## Summary

| Target | Estimate | What it hinges on |
|---|---|---|
| Steam demo | about 50 percent | Art (character, night creature, icons, building kit), the swords, more crafting, a real Steam test between two accounts, store assets, licenses |
| 1.0 | about 25 to 30 percent | Content depth (more recipes, tiers, biomes' own materials, enemies, bosses), progression, balance, polish, platform support |

The engineering base is strong: networked, server authoritative systems, saves, a seeded world, about 300 smoke checks, 276 unit tests, a co-op latency test and screenshot tours. What holds the game back from looking like a real survival crafting game is almost all **art and content**, not code.

## By system

| System | Done | State |
|---|---|---|
| World generation (seeded island, four forests, mountains, lakes, crypts) | 85% | Solid. Trees are 1,400 separate objects (needs instancing before the world grows); the NavMesh does not update when trees fall. |
| Chopping, tree falls, saplings, night | 80% | Mechanics done. The night creature is a sliding pine with no animation (Elderwood Guardian chosen, not imported). |
| Combat | 55% | Combo, block, parry, roll, group tactics work. Swords for fighting are planned (axe for logging); no weapon animations of our own. |
| Crafting | 30% | Two recipes (torch, bandage), upgrades bought with the hut's wood store, crypt gear. Recipes are data now (`RecipeBook`); needs tiers, materials from each forest and stations beyond the workbench. |
| Building | 35% | v1: eight pieces, grid snap, server checks, saving, enemy damage. Missing: doors that open, stability, more pieces, piece icons, a real building kit, distance culling for co-op. |
| Fishing | 80% | New World fight plus Ocarina lure and schools, records, hotspots, food. Rod, float and fish are code shapes. |
| House (workbench, chest, bed) | 75% | Works; the furniture is pack models, the workbench a plain table. |
| Crypts and arenas | 75% | Work; guardians are re-dressed village characters. |
| Survival (health, stamina, wet, food, tool wear) | 65% | Works; no hunger or cold by design so far; status icons are colored squares. |
| Saving and loading | 90% | Every system saves; older saves load. |
| Multiplayer | 70% | Steam relay, join codes with host approval, interest management, latency tested. **Never tried between two real Steam accounts.** No version check between client and host. |
| Interface | 55% | Consistent menus, full pad support and rebinding. Every icon is drawn in code or a model render; status effects are words in squares. |
| Audio | 50% | Good recordings, too few of them (many reused); music follows place and hour but with six tracks for seven moods. No ambience by design. |
| Art direction | 30% | Three styles mixed: the pixel pack world, Quaternius characters, and shapes built in code. Choosing one direction (requirements.md section 0) comes first. |
| Performance | 85% | Steady 60 fps at 1440p everywhere the perf tour goes (GPU about 12 ms at the hut, under 6 ms in the forests). Grass is the heaviest single item. |
| Release readiness | 15% | Builds for Windows, Linux and macOS work. Missing: real Steam app ids, store art, trailer, the pixel pack license, a version check, crash reporting, measured system requirements. |

## Risks

1. **The pixel pack's license is unknown.** It carries the trees, rocks, house and the swords. If it cannot be confirmed, those must be replaced before any public build.
2. **No real two-account Steam test yet.** The relay, the lobby code and the approval flow have only run over the local connection.
3. **Style mixing.** Buying assets before choosing a direction will make it worse.
4. **Content depth.** Two recipes and eight building pieces are a demo's floor, not a survival game's.

## Suggested order to the demo

1. Choose the art direction (requirements.md section 0) and confirm the pixel pack license.
2. Icons and HUD markers (requirements.md section 1): cheapest visible win.
3. Elderwood Guardian in, with an attack and a waking animation; swords with a combo.
4. Crafting tiers and more building pieces as data; doors.
5. The character model.
6. A two-account Steam test, then the Steam app, demo app, store art, trailer.

## Fixed in this pass (2026-10-05)

- About 370 MB of raw music downloads were inside `Assets/` and tracked in git, although `.gitignore` listed them and their license forbids passing them around. They now live in `Chopper/SourceAssets/` (not in git, not imported). Pack archives (`.rar`, `.unitypackage`) moved there too, and the music license PDF to `Docs/licenses/`.
- Leftovers from the URP project template (`TutorialInfo`, `Readme.asset`, `SampleScene`) and eleven empty pack folders were deleted; the website's raw PNG screenshots (about 32 MB, published with the site but unused) moved to `SourceAssets/WebsiteScreenshots`.
- Ten settings in `GameConfig` that no code read, four dead helper methods, and the switched-off code-built suspender straps were removed.
- Comments, docs and tooltips that still described the palisade, the gate or the axe stand were corrected; the old design plan is marked historical; the Elderwood guide had a pasted chat answer with a link to a different pack.
- The Delete button now fills completely red while held. A frame rate cap setting and an `fps` console readout were added for recording. The `axe` console command gives a whole axe.
- The workbench's cube "tools" were removed, the floor piece uses the pack's plank slab, and the website's hut picture was retaken without the old axe on the bench.
