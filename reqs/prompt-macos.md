# Prompt: test and build Chopper on macOS

Copy everything below the line into a Claude Code session on the Mac (over SSH is fine for parts A and B). Pull the repo first; this prompt is in it. The previous version is in `Docs/archive/2026-10-06/prompt-macos.md`.

---

Test Chopper on macOS, a co-op survival game made in Unity 6000.5.10f1, by building it from source on this Mac (Unity is installed here). Act without asking me, except for steps that need a graphical sign in or someone at the Mac's screen (then say so and stop). Style rule: no emojis and no em dashes. Do not commit, push or publish anything, and do not copy the project folder anywhere: work in it directly. Tests must never change my settings or saved worlds: always pass `-chopperTestSaves` to anything that opens a window, and before any run that opens a window, back up `~/Library/Preferences/unity.DefaultCompany.Chopper.plist` and put it back afterwards (the character creator saves its choices there).

`<UNITY>` is `/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity` (if the editor is elsewhere, find it under `/Applications/Unity/Hub/Editor`). `<P>` is `~/projects/3D-Game/Chopper`. Put all logs and pictures in `~/chopper-test`.

## A. Get the latest

1. The project is the private repo `https://github.com/thurtea/3D-Game.git`; Chopper is the `Chopper` folder. If `~/projects/3D-Game` exists, `git pull` there; otherwise clone it into `~/projects/3D-Game`. If git asks for credentials, stop and tell me.
2. Report the latest commit (`git log -1 --oneline`). It should mention the character creator (2026-10-06) or something newer.

## B. Build and test from source (works over SSH)

1. **Open and import** (the first import takes a while): `<UNITY> -batchmode -nographics -projectPath <P> -quit -logFile ~/chopper-test/import.log`. Exit code 198 or "No valid Unity Editor license found" means the license is not active: stop and tell me.
2. **Rebuild the generated scenes, prefabs and the character catalog:** `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.NetworkScenesBuilder.Build -quit -logFile ~/chopper-test/scenes.log`. Look for "Player prefab, Boot and World rebuilt", six "[OutfitSetup] Body for" lines (Chopper, Lumberjack, Woodsman, Scout, Ranger, Trapper) and no "error CS".
3. **Unit tests:** `<UNITY> -batchmode -nographics -projectPath <P> -runTests -testPlatform EditMode -testResults ~/chopper-test/editmode.xml -logFile ~/chopper-test/tests.log`. Expect 291 passed and 0 failed (Windows, 2026-10-06; the re-run was waiting on the editor, so confirm the count). Report the counts and the names of any failures.
4. **Development app and smoke test** (the whole game loop over real networking, 323 checks):
   - Build: `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.BuildTools.BuildDevMac -buildOutput ~/chopper-test/dev -quit -logFile ~/chopper-test/devbuild.log`. Look for "macos build Succeeded". If the target is not supported, install the module with `"/Applications/Unity Hub.app/Contents/Resources/unity" install-modules -e 6000.5.10f1 -m mac-mono --accept-eula -y` and retry (if that needs the Hub window, stop and tell me).
   - Run: `~/chopper-test/dev/Chopper.app/Contents/MacOS/Chopper -batchmode -nographics -chopperHost -chopperSmoke -chopperTestSaves -chopperNoSteam -logFile ~/chopper-test/smoke.log`. Look for "[Smoke] PASS" and report every "[Smoke] FAIL" line.
   - If it crashes on load with "level1 is corrupted [Position out of bounds]", check that no script file holds two MonoBehaviour classes, repeat steps 2 and 4 once, and tell me.
5. **Co-op test with two different characters** (two copies on this Mac, a host and a joined client, 75 ms of simulated lag each way):
   - Host: `~/chopper-test/dev/Chopper.app/Contents/MacOS/Chopper -batchmode -nographics -chopperHost -chopperNetTest host -chopperNoSteam -chopperTestSaves -chopperAppearance 33588288 -chopperLatency 75 -chopperJitter 15 -chopperPacketLoss 2 -logFile ~/chopper-test/host.log &`
   - Wait 10 seconds, then the client: the same command with `-chopperJoin 127.0.0.1 -chopperNetTest client` instead of `-chopperHost -chopperNetTest host`, `-chopperAppearance 102891620`, and `-logFile ~/chopper-test/client.log`.
   - The host log must say "interest: far enemy hidden from the client True, a near one seen True" and "DONE", and show "[Appearance] player 1 shown as 102891620". The client log must show "[Appearance] player 0 shown as 33588288". Report any "FAIL" or "[MovementCheck] Correction" lines.
6. **Release app:** `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.BuildTools.BuildPlaytestMac -buildOutput <P>/Builds/Release/macos -quit -logFile ~/chopper-test/release.log`. Look for "macos build Succeeded". Do not make a zip unless I ask.

## C. Needs someone at the Mac's screen (tell me when you get here)

These open a window (add `-screen-width 1600 -screen-height 900 -screen-fullscreen 0`). Over SSH, ask me to sit at the Mac or to tell you it is ready. Back up the plist first and restore it after each one.

1. **Character creator:** `~/chopper-test/dev/Chopper.app/Contents/MacOS/Chopper -chopperNoSteam -chopperTestSaves -chopperTitle ~/chopper-test/shots/creator -chopperCreatorTour -logFile ~/chopper-test/creator.log`. It opens the Character screen and pictures every outfit and a few hair, beard, skin and color changes. Look at every PNG: nothing see-through at the collar, back, cuffs or waist, and no tan patch on the back of the plaid shirt.
2. **Holding things:** `~/chopper-test/dev/Chopper.app/Contents/MacOS/Chopper -chopperHost -chopperNoSteam -chopperTestSaves -chopperShot ~/chopper-test/shots/hold -chopperHold -logFile ~/chopper-test/hold.log`. Look at the `hold_close_*` pictures: the fingers wrap the axe handle standing, running and chopping. The holster pictures show the hand reaching over the right shoulder, not the axe jumping to the back.
3. **Look tour:** the same command with `-chopperShot ~/chopper-test/shots/look -chopperLook`. Check the close collar shots, the axe in the hand (head up, handle in the fist) and the torch held up in front of the chest.
4. **Play it** (`open <P>/Builds/Release/macos/Chopper.app`; if macOS blocks it, run `xattr -dr com.apple.quarantine` on the app first). Report: the version on the start screen, frame rate, audio, mouse look, fullscreen and a controller; the Character screen (change a few things, then start a world and check you look the same); the axe and torch in the hand; trees falling; swimming and drying by a campfire.
5. **Steam co-op (optional):** with Steam running on an account that is a friend of my Windows account (not the same account), start the release app. I host on Windows and invite this Mac from the pause menu; accept and join. Tell me whether we each look as we made ourselves, how smooth it felt, and paste any "[MovementCheck]" lines from `~/Library/Logs/DefaultCompany/Chopper/Player.log`.

Finish with a short summary of what worked and what failed, with the numbers from B3 to B5.
