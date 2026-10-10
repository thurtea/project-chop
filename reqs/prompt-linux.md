# Prompt: test and build Chopper on Linux (Fedora)

Copy everything below the line into a Claude Code session on the Fedora machine. Pull the repo first; this prompt is in it. The previous version is in `Docs/archive/2026-10-06/prompt-linux.md`.

---

Test Chopper on Linux, a co-op survival game made in Unity 6000.5.10f1, by building it from source on this machine (Unity is installed here). Act without asking me, except for steps that need a graphical sign in or someone at the screen (then say so and stop). Style rule: no emojis and no em dashes. Do not commit, push or publish anything, and do not copy the project folder anywhere: work in it directly. Tests must never change my settings or saved worlds: always pass `-chopperTestSaves` to anything that opens a window, and before any run that opens a window, back up `~/.config/unity3d/DefaultCompany/Chopper/prefs` and put it back afterwards (the character creator saves its choices there).

`<P>` is `~/projects/3D-Game/Chopper`. `<UNITY>` is the editor: look for `~/Unity/Hub/Editor/6000.5.10f1/Editor/Unity`, otherwise search under `~/Unity` and `/opt`. Put all logs and pictures in `~/chopper-test`.

## A. Get the latest

1. The project is the private repo `https://github.com/thurtea/3D-Game.git`; Chopper is the `Chopper` folder. If `~/projects/3D-Game` exists, `git pull` there; otherwise clone it into `~/projects/3D-Game`. If git asks for credentials, stop and tell me.
2. Report the latest commit (`git log -1 --oneline`). It should mention the character creator (2026-10-06) or something newer.
3. **Runtime libraries** (install any that are missing): `sudo dnf install -y mesa-libGL vulkan-loader libX11 libXcursor libXrandr libXi alsa-lib pulseaudio-libs gtk3 libatomic libicu`.
4. Check `<UNITY>` exists and that Linux build support is installed (`<UNITY>/../Data/PlaybackEngines/LinuxStandaloneSupport`). If either is missing, stop and tell me.

## B. Build and test from source (works over SSH)

1. **Open and import** (the first import takes a while): `<UNITY> -batchmode -nographics -projectPath <P> -quit -logFile ~/chopper-test/import.log`. Exit code 198 or "No valid Unity Editor license found" means the license is not active: stop and tell me.
2. **Rebuild the generated scenes, prefabs and the character catalog:** `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.NetworkScenesBuilder.Build -quit -logFile ~/chopper-test/scenes.log`. Look for "Player prefab, Boot and World rebuilt", six "[OutfitSetup] Body for" lines (Chopper, Lumberjack, Woodsman, Scout, Ranger, Trapper) and no "error CS".
3. **Unit tests:** `<UNITY> -batchmode -nographics -projectPath <P> -runTests -testPlatform EditMode -testResults ~/chopper-test/editmode.xml -logFile ~/chopper-test/tests.log`. Expect 291 passed and 0 failed (Windows, 2026-10-06; the re-run was waiting on the editor, so confirm the count). Report the counts and the names of any failures.
4. **Development player and smoke test** (the whole game loop over real networking, 323 checks):
   - Build: `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.BuildTools.BuildDevLinux -buildOutput ~/chopper-test/dev -quit -logFile ~/chopper-test/devbuild.log`. Look for "linux build Succeeded".
   - Run: `~/chopper-test/dev/Chopper.x86_64 -batchmode -nographics -chopperHost -chopperSmoke -chopperTestSaves -chopperNoSteam -logFile ~/chopper-test/smoke.log`. Look for "[Smoke] PASS" and report every "[Smoke] FAIL" line.
   - If it crashes on load with "level1 is corrupted [Position out of bounds]", check that no script file holds two MonoBehaviour classes, repeat steps 2 and 4 once, and tell me.
5. **Co-op test with two different characters** (a host and a joined client on this machine, 75 ms of simulated lag each way):
   - Host: `~/chopper-test/dev/Chopper.x86_64 -batchmode -nographics -chopperHost -chopperNetTest host -chopperNoSteam -chopperTestSaves -chopperAppearance 33588288 -chopperLatency 75 -chopperJitter 15 -chopperPacketLoss 2 -logFile ~/chopper-test/host.log &`
   - Wait 10 seconds, then the client: the same command with `-chopperJoin 127.0.0.1 -chopperNetTest client` instead of `-chopperHost -chopperNetTest host`, `-chopperAppearance 102891620`, and `-logFile ~/chopper-test/client.log`.
   - The host log must say "interest: far enemy hidden from the client True, a near one seen True" and "DONE", and show "[Appearance] player 1 shown as 102891620". The client log must show "[Appearance] player 0 shown as 33588288". Report any "FAIL" or "[MovementCheck] Correction" lines.
6. **Release player:** `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.BuildTools.BuildPlaytestLinux -buildOutput <P>/Builds/Release/linux -quit -logFile ~/chopper-test/release.log`. Look for "linux build Succeeded". Do not make a zip unless I ask.

## C. Needs someone at the screen (tell me when you get here)

These open a window (1600 by 900 is fine: add `-screen-width 1600 -screen-height 900 -screen-fullscreen 0`). Back up the prefs file first and restore it after each one.

1. **Character creator:** `~/chopper-test/dev/Chopper.x86_64 -chopperNoSteam -chopperTestSaves -chopperTitle ~/chopper-test/shots/creator -chopperCreatorTour -logFile ~/chopper-test/creator.log`. It opens the Character screen and pictures every outfit and a few hair, beard, skin and color changes. Look at every PNG: nothing see-through at the collar, back, cuffs or waist, and no tan patch on the back of the plaid shirt.
2. **Holding things:** `~/chopper-test/dev/Chopper.x86_64 -chopperHost -chopperNoSteam -chopperTestSaves -chopperShot ~/chopper-test/shots/hold -chopperHold -logFile ~/chopper-test/hold.log`. Look at the `hold_close_*` pictures: the fingers wrap the axe handle standing, running and chopping. The holster pictures show the hand reaching over the right shoulder, not the axe jumping to the back.
3. **Look tour:** the same command with `-chopperShot ~/chopper-test/shots/look -chopperLook`. Check the close collar shots, the axe in the hand (head up, handle in the fist) and the torch held up in front of the chest.
4. **Play it** (`<P>/Builds/Release/linux/Chopper.x86_64`). Report: the version on the start screen, frame rate, anything pink, black or missing, audio, mouse look, fullscreen and a controller; the Character screen (change a few things, then start a world and check you look the same); the axe and torch in the hand; trees falling; swimming and drying by a campfire.

Finish with a short summary of what worked and what failed, with the numbers from B3 to B5.
