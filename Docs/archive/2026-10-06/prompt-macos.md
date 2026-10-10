# Prompt: test and build Chopper on macOS

Copy everything below the line into a Claude Code session on the Mac (over SSH is fine for parts A and B). The Mac has Unity 6000.5.10f1 installed and signed in. Pull the repo first; this prompt is in it. The old prompts are in `Docs/archive/2026-10-02/prompt-macos.md`.

---

Test Chopper on macOS, a co-op survival game made in Unity, by building it from source on this Mac. Act without asking me, except for steps that need a graphical sign in or someone at the Mac's screen (then say so and stop). Style rule: no emojis and no em dashes. Do not commit, push or publish anything. Tests must never change my settings or saved worlds: always pass `-chopperTestSaves` to anything that opens a window.

Where `<UNITY>` is `/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity` (check it exists; if the editor is elsewhere, find it under `/Applications/Unity/Hub/Editor`). `<P>` is `~/projects/3D-Game/Chopper`.

## A. Get the latest

1. The project is the private repo `https://github.com/thurtea/3D-Game.git`; Chopper is the `Chopper` folder. If `~/projects/3D-Game` exists, `git pull` there; otherwise clone it into `~/projects/3D-Game`. If git asks for credentials, stop and tell me.
2. Report the latest commit (`git log -1 --oneline`). It should mention campfires and swimming (2026-10-04) or something newer.

## B. Build and test from source (works over SSH)

1. **Open and import:** `<UNITY> -batchmode -nographics -projectPath <P> -quit -logFile ~/chopper-test/import.log`. Exit code 198 or "No valid Unity Editor license found" means the license is not active: stop and tell me.
2. **Rebuild the generated scenes and prefabs** (the world, the campfire, the enemies and the asset lists are made by code): `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.NetworkScenesBuilder.Build -quit -logFile ~/chopper-test/scenes.log`. Look for "Player prefab, Boot and World rebuilt" and no "error CS".
3. **Unit tests:** `<UNITY> -batchmode -nographics -projectPath <P> -runTests -testPlatform EditMode -testResults ~/chopper-test/editmode.xml -logFile ~/chopper-test/tests.log`. Expect 254 passed and 0 failed (on Windows on 2026-10-04). Report the counts and the names of any failures.
4. **Development app and smoke test** (the whole game loop over real networking, 258 checks):
   - Build: `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.BuildTools.BuildDevMac -buildOutput ~/chopper-test/dev -quit -logFile ~/chopper-test/devbuild.log`. Look for "macos build Succeeded". If the target is not supported, install the module with `"/Applications/Unity Hub.app/Contents/Resources/unity" install-modules -e 6000.5.10f1 -m mac-mono --accept-eula -y` and retry (if that needs the Hub window, stop and tell me).
   - Run: `~/chopper-test/dev/Chopper.app/Contents/MacOS/Chopper -batchmode -nographics -chopperHost -chopperSmoke -chopperTestSaves -chopperNoSteam -logFile ~/chopper-test/smoke.log`. Look for "[Smoke] PASS" and report every "[Smoke] FAIL" line.
   - If it crashes on load with "level1 is corrupted [Position out of bounds]", check that no script file holds two MonoBehaviour classes, repeat steps 2 and 4 once, and tell me.
5. **Co-op test** (two copies on this Mac, a host and a joined client, with 75 ms of simulated lag each way):
   - Host: `~/chopper-test/dev/Chopper.app/Contents/MacOS/Chopper -batchmode -nographics -chopperHost -chopperNetTest host -chopperNoSteam -chopperTestSaves -chopperLatency 75 -chopperJitter 15 -chopperPacketLoss 2 -logFile ~/chopper-test/host.log &`
   - Wait 6 seconds, then the client: the same command with `-chopperJoin 127.0.0.1 -chopperNetTest client` instead of `-chopperHost -chopperNetTest host`, and `-logFile ~/chopper-test/client.log`.
   - The host log must say "interest: far enemy hidden from the client True, a near one seen True" and "DONE". Report any "FAIL" or "[MovementCheck] Correction" lines.
6. **Release app:** `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.BuildTools.BuildPlaytestMac -buildOutput <P>/Builds/Release/macos -quit -logFile ~/chopper-test/release.log`. Look for "macos build Succeeded". Do not make a zip unless I ask.

## C. Needs someone at the Mac's screen (tell me when you get here)

These open a window. Over SSH, ask me to sit at the Mac or to tell you it is ready.

1. **Swim tour:** `~/chopper-test/dev/Chopper.app/Contents/MacOS/Chopper -chopperHost -chopperNoSteam -chopperTestSaves -chopperShot ~/chopper-test/shots/swim -chopperSwim -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile ~/chopper-test/swim.log`. Report every "[Swim]" line. Expect: floating with the feet about 1.05 m under the surface, swimming about 2.3 m/s, the camera at least 0.35 m above the water when looking up, wet after leaving the sea, and dry after about 11 seconds by the campfire. Look at the PNGs: no grey wall when looking up from the water, and the campfires (stone ring, logs, small flames) not buried in grass.
2. **Look tour:** the same command with `-chopperShot ~/chopper-test/shots/look -chopperLook` instead of the swim arguments. It takes five close shots of Chopper from every side. Tell me if any part of the body or clothes is missing, see-through or the wrong color. On Windows I could not see the "invisible body under the clothes" problem; I want to know if the Mac shows it.
3. **Play it** (`open <P>/Builds/Release/macos/Chopper.app`; if macOS blocks it, run `xattr -dr com.apple.quarantine` on the app first). Report:
   - the version on the start screen, frame rate, audio, mouse look, fullscreen and a controller;
   - **trees:** they stand straight (no tilt). Chop a few on level ground (they should fall away from you) and one on a hillside from below (it should come down the hill toward you);
   - **water:** swim in the pond and the sea. You float at the top, swimming is slow and uses stamina, and a "Wet" square shows by the stamina bar after you get out. Stand by a campfire in the hut yard or on the pond bank and check Wet goes away within about 10 seconds;
   - **Chopper:** look at the character closely from the front and the back (zoom with the wheel) and tell me if anything is missing or see-through.
4. **Steam co-op (optional):** with Steam running and signed in on an account that is a friend of my Windows account (not the same account), start the release app. I host on Windows and invite this Mac from the pause menu; accept and join. Tell me how smooth it felt (rubber banding of either character or the enemies) and paste any "[MovementCheck]" lines from `~/Library/Logs/DefaultCompany/Chopper/Player.log`.

Finish with a short summary of what worked and what failed, with the numbers from B3 to B5 and C1.
