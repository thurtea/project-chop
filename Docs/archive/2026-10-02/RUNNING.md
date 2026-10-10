# Running, testing and updating Chopper

## Play the game

**The quick way (Windows):** unzip `Builds\Release\Chopper-0.1.0-windows.zip` (or use the `Builds\Release\windows` folder) and run `Chopper.exe`. Choose Character to pick your look, New Game to host, Join Game to join a friend by address.

**In the Unity editor:** open `E:\Projects\3D-Game\Chopper` in Unity 6000.5.10f1, wait for the import, run **`Chopper > Setup > Rebuild Network Scenes`**, press Play, choose New Game.

**The exe and the editor are separate.** `Builds\Release\windows\Chopper.exe` is a finished copy of the game from the last time a build was made. Rebuild Network Scenes changes only the project in the editor, never that exe. So after an update either test in the editor (Rebuild Network Scenes, then Play), or make a new exe first (`Chopper > Build > Windows Playtest Player`, or `All Platforms (Release)` for all three systems). The start screen shows the build version in the bottom right corner.

## Every time you update the game (pull, or after I change code)

1. Close any running game. In Unity wait for the compile to finish (spinner bottom right).
2. Run `Chopper > Setup > Rebuild Network Scenes`. It regenerates the world, the player prefab, the character catalog and the materials. Skipping it is the most common cause of "the new thing is not there" or odd errors.
   This is the only Setup item you need. `Prepare Third Party Assets` is a one-time import fixup that is already applied and committed (run it only on a fresh clone if the nature materials look pink or the axe is the wrong size). The other items under Setup are developer diagnostics that write reports; ignore them.
3. Run the tests: `Window > General > Test Runner`, EditMode, Run All. All should be green.
4. Press Play and try the change.
5. If the Console shows `Build asset version error ... SO_GameConfig.asset`, right click that asset (Assets/_Chopper/Config) and choose Reimport, or restart Unity. It only means Unity noticed the file changed on disk.
6. Yellow warnings (for example "FindFirstObjectByType is obsolete") do not matter. Red errors do: copy them to me.

## Make the exe

- Windows: `Chopper > Build > Windows Playtest Player`, output in `Builds`.
- All three systems: `Chopper > Build > All Platforms (Release)`, then in a terminal in the project folder `python Tools/package_release.py Builds/Release 0.1.0`. This needs the Linux and macOS modules once: `unity install-modules -e 6000.5.10f1 -m linux-mono mac-mono --accept-eula -y` (approve the admin prompt).
- The zips appear in `Builds\Release`. They are not in git; copy them to your Fedora machine and Mac (USB, scp, or a draft GitHub release).

## Test the Linux and Mac zips

- Fedora: unzip, `chmod +x Chopper.x86_64`, `./Chopper.x86_64`. If it will not start, run it from a terminal and send me the error.
- Mac: unzip, right click `Chopper.app`, Open. If it says damaged: `xattr -dr com.apple.quarantine Chopper.app`.
- You do not need Unity installed on those machines for this. `prompt-linux.md` and `prompt-macos.md` are only for building on them.

## Test multiplayer

- Two copies on one PC: start the first, choose New Game. Start a second copy, choose Join Game, address `127.0.0.1`. (Editor plus exe also works.)
- Two PCs: the host needs UDP port 7770 reachable, or both use a private network such as Tailscale. The joiner types the host's address.
- Automated check (dev builds only): start `Chopper.exe -batchmode -nographics -chopperHost -chopperNetTest host`, then a second `Chopper.exe -batchmode -nographics -chopperJoin 127.0.0.1 -chopperNetTest client`, and compare the `[NetHost]` and `[NetClient]` lines in the logs.

## Test and debug tools

- F1 in game (host only): skip to night or day, god mode, wood, axe, heal, repair, spawn enemies or a boss, boons.
- F6 wood, F7 saplings, F10 next phase work in the editor and development builds.
- Headless smoke test of the whole loop: build a development player (`Chopper > Build > Windows Development Player`), then `Chopper.exe -batchmode -nographics -chopperHost -chopperSmoke` and look for `PASS`.
- Screenshots: `-chopperShot <folder> -chopperZones` (forest tour), `-chopperYard` (the hut), `-chopperCombat`, `-chopperTitle`.

## Commit and push

In `E:\Projects\3D-Game\Chopper`: `git add .`, `git commit -m "your message"`, `git push`. The line ending warnings (LF will be replaced by CRLF) are harmless. After pushing, `git pull` on the other machines.

## One rule that costs hours when broken

Every script that is a MonoBehaviour or NetworkBehaviour must be alone in a file named after its class. Two in one file make the player build crash on load with "level1 is corrupted".
