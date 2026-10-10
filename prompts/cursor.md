# Handoff to Claude, 2026-10-05

You stopped when the monthly spend limit hit. Cursor finished the step you were about to start. Read this, then `summary/summary.md` and `tomorrow/tomorrow.md`. Do not redo the icons, the credits, or the wood creature unless the user asks. The Valheim art direction is retired. The Elderwood Guardian is on hold.

Style is unchanged: no emojis and no em dashes, in docs, code comments, commits, and in-game text.

## Where you stopped

Icons and credits were already in. The house tour had been run so the new icons could be seen (hotbar, chest, piece list, map marker, Fed badge). Your last note was that you would check how the enemy switches look when the forest creature wakes. You had just opened `Treant.cs` (the visual build, and `_draugrPrefab`). You had already decided the night stand-in would be a wood creature on the animation rig, using punch clips from the Universal Animation Library, and that icons came first.

## What Cursor finished

An awake forest enemy no longer grows into a pine that bobs and slides. It stands up as a wood creature.

- Saplings, and bosses that are still saplings, stay pines.
- Awake forest enemies, including wardens, use the golem. `Treant.ShowsGolem` is `!IsGuardian && Active && _golemPrefab != null`.
- Crypt guardians and arena enemies stay the draugr (`ShowsDraugr` is unchanged).
- The same animator parameters drive it: Speed, Attack (`Punch_Cross`, sped so the swing finishes with the 0.65 s windup), Hit (`Hit_Chest`), Death (`Death01`). Walk and jog are the existing loops. Idle is `Idle_Loop`, not the sword idle.
- Each element has its own crown, named `Leaves0` through `Leaves4` in Element order (Neutral, Water, Fire, Earth, Wind), from `pixel_tree_lowpoly_dark`, `_blue`, `_red`, `_yellow`, `_lightgreen`. Only the matching crown is enabled.
- The crown is two copies of that pine's foliage, one turned 90 degrees, then squashed (wider, about 0.38 of the height) so it sits on the neck as a head of leaves. Shoulder tufts use the same foliage at 0.42 m.
- The body is the Quaternius superhero male mesh with the head triangles removed, skinned to the animation mannequin, material `M_GolemBark` (the pixel oak bark, tiled 2.5, emission enabled). A light element tint (0.12) is applied at runtime. The mannequin's own renderers stay hidden.
- Warden shield and phase 3 still change the glow and the scale, through `RefreshWardenLook` and `ApplyGolemScale`. Height follows `CurrentHeight()`, so a boss is still the large one.

Trunk segments were tried first (the pine log mesh parented to the arm and leg bones). Screenshots showed a flat jagged shard. That approach was thrown out. Do not put it back.

This is a stand-in. `requirements/requirements-assets.md` says the Elderwood Guardian belonged to the retired art direction and is on hold. Keep the sapling-to-awake switch when a replacement is named.

## Files

- `Assets/_Chopper/Scripts/Editor/GolemSetup.cs` builds the prefab. Called from `MeadowBuilder.BuildTreantPrefab`.
- `Assets/_Chopper/Prefabs/Enemies/Golem.prefab`
- `Assets/_Chopper/Prefabs/Enemies/Golem.controller`
- `Assets/_Chopper/Prefabs/Enemies/Mesh_GolemBody.asset`
- `Assets/_Chopper/Materials/World/M_GolemBark.mat`
- `Assets/_Chopper/Scripts/Enemies/Treant.cs` (`_golemPrefab`, `BuildGolem`, `ShowElementCrown`)
- `Assets/_Chopper/Scripts/Editor/MeadowBuilder.cs` wires the prefab
- `Assets/_Chopper/Scripts/Networking/ShotCapture.cs` adds `combat_2b_golem` and `combat_2c_golem_side` to the `-chopperCombat` tour
- `requirements.md` section 2b, `summary.md` night creatures bullet, `tomorrow.md`

The icon work you had already saved is in the same uncommitted set: `Assets/ThirdParty/GameIcons_net/`, `CREDITS.md`, `TitleScreen.cs`, `Website/index.html`, piece `_icon` fields, and the removal of `Editor/IconRenderer.cs` plus the old `Resources/Icons`. Credits now name each pack's real license. game-icons.net is CC BY 3.0 and the credit line is in the game and on the website. The pixel pack still has no license file.

## Already verified

- Unity batch compile, then `NetworkScenesBuilder.Build`, succeeded on `E:\Projects\3D-Game\Chopper` while the editor was closed. Later the user opened the editor (about 1:24 PM). After that, batch work used the shadow copy below.
- Development player and `-chopperCombat` tour, with registry backup and `-chopperTestSaves`. The tour log has no exceptions. Shots are in the scratchpad under `shots/golem/` (`combat_2b_golem`, `combat_2c_golem_side`, `combat_3_enemy_hit`, `combat_6_group`). From the front and the side it reads as a person-shaped wooden figure with a wide leaf crown, running and then flinching. Up close the bark is a flat wood color, not grain, and the crown is still the pine's flat leaves.
- `git add .` was run from `E:\Projects\3D-Game` before this platform pass. Nothing was committed. Suggested message, covering the icons you finished and the creature:

Replace the drawn icons with one credited set, and wake forest enemies as a wood creature instead of a sliding pine.

## Release players (done, 2026-10-05 afternoon)

The user asked whether `Builds\Release\windows\Chopper.exe` had the new icons and the wood creature, then to make a release for every platform this PC can build. They then pointed at that exe and said it still showed 1:00 AM, so it was not updated.

What was true of the 1:00 AM file: `Chopper.exe` is the small Unity launcher (667,648 bytes). A 1:20 PM build rewrote `Chopper_Data` and `Chopper.Runtime.dll` but left the launcher, `UnityPlayer.dll`, and the crash handler at 1:00 AM, because those bootstrap files had not changed. The user judges "updated" by the exe timestamp. That 1:20 PM player has been replaced. Linux and macOS in `Builds\Release` were from 2026-10-03. The zips next to them are still `Chopper-0.1.0`, `0.2.0`, and `0.2.8`. Do not rebuild the zips unless the user asks.

The editor was open on `E:\Projects\3D-Game\Chopper`, so the build used a shadow copy at `C:\Users\itsth\AppData\Local\Temp\chs` (Assets, Packages, and ProjectSettings mirrored with robocopy `/MIR`, plus `Library\PackageCache`). `NetworkScenesBuilder.Build` there exited 0. Log: `C:\Users\itsth\AppData\Local\Temp\chs-scenes.log`. Those regenerated scenes were not copied back to E:.

`BuildTools.BuildAllPlaytest` then wrote to `C:\Users\itsth\AppData\Local\Temp\chs-release`. Log: `C:\Users\itsth\AppData\Local\Temp\chs-release.log`. All three succeeded, and each folder was mirrored onto `E:\Projects\3D-Game\Chopper\Builds\Release`:

- Windows, 8 min 31 sec, 230 MB. `Chopper.exe` and `Chopper.Runtime.dll` are stamped 2:10 PM.
- Linux, 11 min 48 sec, 236 MB. `Chopper.x86_64` is stamped 2:22 PM.
- macOS (universal), 8 min 42 sec, 248 MB. `Chopper.app` was copied at 2:31 PM.

Unity modules installed for this editor: Windows, Linux, and macOS. The old `Assembly-CSharp.dll` left in the October 3 Linux and macOS players was removed by the mirror.

## Do next

`tomorrow.md`: wait for what the user reports from playing v0.2.9, including how the wood creature looks up close. When they drop in the Elderwood Guardian, replace the golem and keep the wake switch. Then swords (axe stays for logging), then crafting tiers and doors. Do not rebuild the release zips unless asked. Rebuild `Builds\Release\windows` at the end of a session that changes the game, and confirm `Chopper.exe` itself has a new timestamp, not only `Chopper_Data`. If the editor is open, build from the shadow and copy the player folder over.
