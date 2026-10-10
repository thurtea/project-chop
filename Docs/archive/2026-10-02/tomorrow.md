# Chopper: plan for the next session

Last updated: 2026-10-02. Read `summary.md` first for the full state, `playtest.md` for what to test.

## Done 2026-10-02 (v0.2.0)

Instanced Valheim style grass carpet, mountain rim replaced by a beach and sea, warped fractal hills, 320 fallen branches to pick up for wood, recordings drop-in system for all sounds (exact names in `requirements.md`), surface footsteps, surf, menu sounds, Music slider, positional audio bug fixed, time of day haze, cleaner menus (no frames or lines, text buttons with a glow), title backdrop, version on the title screen. Details in `summary.md` under "Grass carpet, a sea at the world edge". Release zips for Windows, Linux and macOS 0.2.0 in `Builds\Release`.

Next, in order:
1. The user tests v0.2.0 on Windows, Fedora and the Mac and drops recordings into `Resources/Audio`; level them with `Tools/analyze_sounds.py`.
2. Procedural worlds per game (the user wants the survival game method): a world seed chosen at New Game and sent to joiners, the terrain, trees, decor, grass map, herbs and branches generated at runtime from it instead of baked by Rebuild Network Scenes. Needs runtime terrain generation (heights and splat in a few seconds, maybe on a worker thread), the tree database built at runtime with the same ids on every peer, the NavMesh bake (already runtime), and a seed field in the New Game screen. Then a bigger world in chunks.
3. Grass polish after a look in play: density per performance (a Grass quality option in Graphics), wind strength, colors per zone, fireflies and insects over it at dusk.
4. The rest of the queue below (more zones, tutorial area, Choppette, cape, gritty grade).

## Where things stand

The opening loop is now: spawn outside with fists, punch trees, repair the hut, take the axe from the stand inside, read the board. A playable MVP exe is at `Builds\Release\windows\Chopper.exe` (start screen with character creator, fullscreen by default). The loop works end to end and is covered by 107 EditMode tests and a 99 check headless host smoke test (including "an enemy attacks the player before the walls or hut"): a 640 m Valheim style meadow with paths, four elemental forests, a palisade around the hut with a repair key, infinite lives with a respawn timer, enemies that go for players first, then walls, then the hut, stamina, crafting, a full HUD with minimap and menus, and Xbox controller support. Real axe sounds are in; everything else is still placeholder.

Open this session's unknowns, all visual or feel based, because nothing has been played by eye since the latest changes:
- Does the bigger world feel right (scale, tree density, path look)?
- Is a full night winnable but tense with walls, the 12 s respawn and the 40 m enemy aggro?
- Does the controller feel right?

## First thing tomorrow (you)

See `RUNNING.md` for how to run, test, update, build and commit.

Running the game after pulling changes (Windows, in the editor): open `E:\Projects\3D-Game\Chopper` in Unity 6000.5.10f1, wait for the import, run `Chopper > Setup > Rebuild Network Scenes` (needed after ANY update: it regenerates the world, the player prefab and the character catalog), then press Play and choose New Game. If the Console shows "Build asset version error ... SO_GameConfig.asset", right click that asset and choose Reimport, or restart Unity. For the exe: `Builds\Release\windows\Chopper.exe`.

0. Build Linux and macOS: follow "Building the Linux and macOS versions" in `summary.md` (install the two Unity modules with `unity install-modules -e 6000.5.10f1 -m linux-mono mac-mono --accept-eula -y`, approve the admin prompt, run the BuildAllPlaytest command or menu item, then `python Tools/package_release.py Builds/Release 0.1.0`). Test each zip on Fedora and the Mac before any public release.

1. Open the project and run `Chopper > Setup > Rebuild Network Scenes` (this creates the character catalog, materials and preview in your project), then commit the generated assets. Launch `Builds\Release\windows\Chopper.exe` or press Play.
   Linux and macOS: run `unity install-modules -e 6000.5.10f1 -m linux-mono mac-mono --accept-eula -y` and approve the admin prompt, then I build and package them. Test the Windows, Fedora and Mac builds before publishing a release (tag `v0.1.0` runs the GitHub workflow once the Unity secrets are set).
2. Play through `playtest.md` section 000 (new items) and 00, then the rest as time allows. Send me the failures and your top three annoyances.

## Then, in this order (me)

1. Fix what the playtest shows. Known: the house roof now looks flat and the notice board text is small; palisade logs are very dark at dusk; the world map (M) labels overlap the forest markers for Wind and Earth; the night is very dark away from the torch. Likely candidates: day and night balance (330 s day, 150 s night, enemies run at 2x beyond 60 m), zone lighting (low morning sun, dark valley floors), enemy cap and speed, wall and hut health, palisade look, torch highlight on the ground, tree density per forest.
2. Debug console (requested, after your playtests): a Valheim style console on the F5 or backquote key with commands such as `god` (infinite health), `wood 100`, `stamina`, `time day|night`, `heal`, `spawn`, `tp`, `help`. Host only (server authoritative), off in public builds unless a `-chopperDebug` flag or a config switch is set.
3. Art pass from `requirements.md` as you add packs (ground textures, trees, animated Treants, sounds).
4. Valheim style survival depth:
   - more resources than wood: stone from rocks (needs a pickaxe) and resin or bark from trees,
   - more craftables: a campfire (light and safe spot), better axes and armor tiers, arrows and a bow,
   - food that raises maximum health and stamina, with a simple cooking step,
   - placeable hut pieces (walls, gates, fences, a workbench you build) with a build menu.
5. Replace placeholders:
   - animated Treant enemy (arms and legs, walk and attack animations) and a proper boss model,
   - a real hut and palisade (Medieval Village MegaKit is already imported),
   - more real sounds: tree fall, footsteps, hurt, growl, ambience (the two Forest-Survival "Wood Chooping" clips exist in the backup folder but their license is unknown, so they are not used yet),
   - item icons for the hotbar and inventory.
6. Performance: 1700 trees are individual GameObjects. Move to instanced rendering with a spatial grid (plan T12 and T13) before adding more, and check the NavMesh bake time at world start.
7. Four player soak test (plan T34).

## Decisions I made that you may want to change

- Lives are infinite by default. A finite mode (three per night) is built and switchable with `Finite Lives` in `SO_GameConfig`; expose it as a game mode when you want it.
- Repair is its own key (R, RB on pad). Deposit is E (X). Repair pays from the shared store first, then from carried wood.
- Enemy aggro range is 40 m. Ordinary saplings cannot be hit by the axe at all; only grown boss saplings can.
- The menu key is Esc (Start on pad), inventory Tab (Y), map M (Back).

## Housekeeping

- Confirm the license of `axe-impact.mp3` and `axe-slash.mp3` in `Assets/_Chopper/Resources/Audio` (from the earlier Chopper project) before a public release.
- Commit and push from `E:\Projects\3D-Game\Chopper` when you are happy (the remote `origin` is already set).
- Resume prompt: the last section of `summary.md`.

- Remapping covers keyboard and mouse only; add gamepad rebinding if wanted. Move and look stay on WASD and the mouse.
- Check the shield placement against the roll and swim animations, and consider a shield that stays on the back when not blocking.

## Roadmap to make the world feel alive (from the gap review)

1. Playtest the night themes: confirm the counter element shows up early enough, tune DominantWeight, CounterWeight and the share range.
2. Boss tree: show a growth stage and a map marker, a proper arena at nightfall, boss attack patterns and a telegraph, a reward chest.
3. Life in the world: birds, deer and rabbits (Quaternius animals), insects and fireflies at dusk, fish in the ponds, wind in the leaves and grass.
4. Sound: water sounds (waiting on assets), forest day and night ambience per zone, footsteps by surface, music for day, dusk and night.
5. Look: day and night lighting pass, fog and light shafts in the forests, water shader, better tree variety per zone, ground detail and paths.
6. Survival depth: hunger or comfort at the hut, cooking at a fire, more buildings to place with wood (Valheim style), tool durability.
7. Co-op: late join, host leaving, latency test, shared map markers, revive.

## Done 2026-10-01 (second round): controller menus, audio, guard

Controller navigation of all menus, wind and sound rebuilt and rebalanced with separate Effects and Ambience sliders, crosshair removed, procedural shield guard and blocking movement. Details in `summary.md` under "Controller menus, audio and the guard pose". Next candidates: real CC0 recordings for wind, footsteps, tree fall and hits (freesound.org CC0 filter, Kenney audio), strafe and backpedal animations while guarding, hit reactions while guarding, a real pad test, then the queue below (Grok shortlist decision, more zones, tutorial area).

## Queue from the user (2026-10-01, after the pixel world), in order

1. DONE (2026-10-01): menus and sign text. New display font and panel style, readable world space notice board. See "Settings, controls and menu look" in `summary.md`. Possible polish: the HUD (health bars, hotbar, clock) still uses the old plain look and font.
2. DONE (2026-10-01): Controls and Controller options in the start menu (and pause menu), real gamepad rebinding. Needs a test with a real pad. Items 1 and 2 of "Stopped here" below are finished.
3. DONE (2026-10-01): crisp resolution and lighting pass. Graphics tab, brighter balanced materials, readable night. See "Crisp resolution and lighting pass" in `summary.md`. Original request: Crisp resolution and the best lighting possible: render at native resolution with the best anti aliasing (check URP asset: MSAA, SMAA or TAA, shadow resolution and cascades, soft shadows), keep the pixel look in the textures only, then a lighting pass (ambient is too dark in shade and forests, the low morning sun leaves the yard in shadow, the lantern glass blows out to white).
4. Use the Grok shortlist where it helps (user approved "how we can, if needed"): Staggart Stylized Grass Shader for Unity 6 ($40, UPM, URP only), Staggart Stylized Water 3, KayKit Forest Nature Pack (CC0, itch.io), Poly Haven and ambientCG ground textures, a sky and time of day system, volumetric fog. Caveat: these are smooth, not pixel art, so they only fit if the look moves toward Valheim gritty; decide before buying. The user will say which folders they add.
5. More zones beyond the four meadows (water, wind, earth, fire): design new biomes and where they sit (rings beyond the current forests, or new regions), each with its own tree color, ground and enemy mix.
6. A separate tutorial area instead of teaching in the main world (the in game yard intro "looks good", so keep its pieces): a small start zone with the broken hut, punching trees, the axe stand and the notice board, then a gate or portal into the real world.

## Stopped here (2026-10-01), still to do

Requested but not started:
1. Controls and every other setting available from the title screen, as one tabbed settings view shared with the pause menu. Redo the UI look (the old Controls screen overflowed its panel).
2. Gamepad: detect the controller, show Xbox button names, set Valheim style defaults (RT attack, LT block, A jump, B roll, X interact, Y inventory, L3 sprint, R3 crouch, D-pad hotbar), allow rebinding on the pad too.
3. Choppette (the female character): female body, outfits, eyebrows and hair (the packs have Female_Peasant, Female_Ranger, Hair_Buns, Hair_BuzzedFemale). More beards and hairs for both: derive mustache, goatee and long beard from the beard mesh, add braids or buns variants. Needs a gender bit in `AppearanceData` (it is full at 30 bits; plan: repack Outfit 4, Hair 5, Beard 4, HairColor 4, Cloth 4, Skin 3, Gender 1, Cape style 2, Cape color 4, and bump the PlayerPrefs key).
4. Cape or cloak: a procedural strip of cloth on the upper back, simulated with a few verlet rows, selectable in the creator.
5. Gritty look: a URP Volume with lower saturation, more contrast, vignette, grain, colder shadows and fog, and darker zone palettes.
6. Art revamp: remove the weeds, leaf scatter and tall grass blades, add a real short grass ground cover and better terrain textures. Waiting on the assets listed in `requirements.md` (ART REVAMP).
7. The title screen Character preview was not in a T-pose in my build; if you still see one, say which screen.

Dev leftovers: temporary `[Shield]` and `[Combo]` debug logs in `ShotCapture.cs` can be removed.
