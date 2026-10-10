# Chopper: how to run, test and build

Last updated: 2026-10-09. The old version is in `Docs/archive/2026-10-02/RUNNING.md`.

## Play the built game

Run `Builds\Release\windows\Chopper.exe`. The start screen shows the version in the bottom right corner.

The exe is a frozen copy from the last build. Changes in Unity never reach it until a new build is made (see "Make builds" below).

## Play in the Unity editor

1. Open `E:\Projects\3D-Game\Chopper` in Unity 6000.5.10f1 and wait for the import (spinner bottom right).
2. After any update, run **Chopper > Setup > Rebuild Network Scenes**. It rebuilds the World scene shell, prefabs and asset lists.
3. Press **Play**, choose **New Game**, keep or type a seed, then **Start**.

The land is generated when the game starts, so the Scene view is mostly empty until you press Play.

If the Console shows "Build asset version error ... SO_GameConfig.asset", right click that asset, choose Reimport, or restart Unity.

## Saves

Worlds are saved in `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Chopper\Saves` as `<name>.world.json`. Games hosted from the command line (tests, tours) never save. Screenshot tours and the pad test take `-chopperTestSaves`, which points the save list at a scratch folder, so they never see or change real worlds.

## Multiplayer

- **Through Steam (the normal way):** both players run Steam and are Steam friends.
  - The host starts a world and can press Invite friends in the pause menu.
  - The friend picks Join Game, makes their character, and presses Join next to the host's name.
  - Accepting a Steam invite, or Join Game in the Steam friends list, works too.
  - Everything goes through Steam's relays: no addresses, no ports, and no one sees anyone's IP.
  - Steam needs `steam_appid.txt` (480 for now) next to `Chopper.exe`; the build writes it.
- **Without Steam:** solo play works. Command line tests use the local UDP transport (`-chopperHost`, `-chopperJoin 127.0.0.1`); `-chopperNoSteam` forces it for screenshot tours.
- One Steam account cannot join itself, so testing co-op needs a second PC with a second Steam account.

## Tests

- **Unit tests:** Window > General > Test Runner, EditMode, Run All.
- **Headless smoke test:**
  1. Build a development player (Chopper > Build > Windows Development Player).
  2. Run `Chopper.exe -batchmode -nographics -chopperHost -chopperSmoke`.
  3. Look for "PASS" in the log.
- **Simulated local co-op conditions:** start both a host and a client with `-chopperNoSteam -chopperLatency <ms> -chopperJitter <ms> -chopperPacketLoss <percent>`. These are outbound delays, so use half the target round-trip latency on each process: 40, 75 and 125 ms simulate roughly 80, 150 and 250 ms round trips. For example, run the host with `-chopperHost -chopperNetTest host -chopperLatency 75 -chopperJitter 15 -chopperPacketLoss 2`, then the client with `-chopperJoin 127.0.0.1 -chopperNetTest client -chopperLatency 75 -chopperJitter 15 -chopperPacketLoss 2`. This affects only command-line local transport sessions, never Steam play.
- **Movement test:** `Chopper.exe -chopperHost -chopperNoSteam -chopperTestSaves -chopperShot <folder> -chopperMoveTest` (needs a window) sprints round the hub and the ridges and logs `[Move] RESULT` with teleports, freezes and speed check snap backs. Run it as a client with `-chopperJoin 127.0.0.1` against a headless host, with `-chopperLatency` on both.
- **Swim tour:** `-chopperHost -chopperNoSteam -chopperTestSaves -chopperShot <folder> -chopperSwim` (needs a window) floats in the pond and the sea, swims, looks up and dries off by a campfire; it logs `[Swim]` lines (feet, surface, camera, speed, wet).
- **House tour:** the same with `-chopperHouse`: the workbench, chest and bed, the Tab bag, a small hut built from pieces, and the hammer.
- **Axe run check:** the same with `-chopperAxeRun`: walks and sprints with the axe drawn and logs `[AxeRun]` flips (frames where the axe turns more than 25 degrees). Expect 0.
- **Look tour:** the same with `-chopperLook` takes five close pictures of Chopper from every side.
- **Tutorial:** `-chopperHost -chopperTutorial` hosts the practice yard from the command line; add `-chopperShot <folder> -chopperTutorialTour` to check it and take pictures (`[Tutorial] RESULT`).
- **Guard test:** `Chopper.exe -chopperHost -chopperShot <folder> -chopperGuardTest` (needs a window).
- **Screenshot tours:** `-chopperShot <folder>` with `-chopperLight`, `-chopperYard`, `-chopperCombat`, `-chopperCrypt` (a crypt entrance, its rooms, a chest and a crushing), `-chopperTitle <folder>` (add `-chopperLoadTour` for Continue, the load list and the loading screen; it deletes its temporary save afterwards). `-chopperSite` captures the website's hub and forest images.
- **Command line hosting:** uses world seed 1337 unless you add `-chopperSeed <number or word>`.
- **Dev keys** (editor and development builds): F1 developer panel (host), F6 40 wood, F7 drop saplings, F10 skip to the next phase.
- **Unity console:** a host session logs no warnings or errors (checked 2026-10-05 with the house tour).
- **Frame rate:** F5 then `fps` shows frames per second and the slowest frame. For recordings see `../playtests/playtest.md` (Recording); Settings, Graphics has a frame rate cap.

## Make builds

- After every session that changes the game, rebuild the Release players for Windows, Linux and macOS, then package the current release version for all three platforms. Do this after the applicable tests pass and the user-facing changes are ready to release.
- Confirm that each platform's player output is newly built. In particular, check that `Builds\Release\windows\Chopper.exe` itself has a new timestamp, not only files in `Chopper_Data`.
- Preserve existing versioned ZIPs as historical releases. Create new ZIPs using the current version from `Assets/_Chopper/Scripts/Editor/BuildTools.cs`; never replace an older versioned ZIP with newer game contents.
- **Windows only:** Chopper > Build > Windows Playtest Player.
- **All three systems:**
  1. Chopper > Build > All Platforms (Release).
  2. In a terminal in the project folder, run `python Tools/package_release.py Builds/Release <current-version>`.
  3. The newly versioned zips appear in `Builds\Release`; keep prior version archives unchanged.
- The version number is set in `Assets/_Chopper/Scripts/Editor/BuildTools.cs` (`Version`).

## Git

You commit and push. My only git command is `git add .`, and I propose a commit message each session.

## One rule that costs hours when broken

Every MonoBehaviour or NetworkBehaviour must be alone in a file named after its class. Otherwise player builds crash on load with "level1 is corrupted".
