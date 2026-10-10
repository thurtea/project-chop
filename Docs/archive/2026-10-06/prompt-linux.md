# Prompt: test and build Chopper on Linux (Fedora)

Copy everything below the line into a Claude Code session on the Fedora machine. Pull the repo first; this prompt is in it. The old prompts are in `Docs/archive/2026-10-02/prompt-linux.md`.

---

Test Chopper on Linux, a co-op survival game made in Unity, by building it from source on this machine if Unity is here, or from a zip I give you if it is not. Act without asking me, except for steps that need a graphical sign in or someone at the screen (then say so and stop). Style rule: no emojis and no em dashes. Do not commit, push or publish anything. Tests must never change my settings or saved worlds: always pass `-chopperTestSaves` to anything that opens a window.

`<P>` is `~/projects/3D-Game/Chopper`.

## A. Get the latest

1. The project is the private repo `https://github.com/thurtea/3D-Game.git`; Chopper is the `Chopper` folder. If `~/projects/3D-Game` exists, `git pull` there; otherwise clone it into `~/projects/3D-Game`. If git asks for credentials, stop and tell me.
2. Report the latest commit (`git log -1 --oneline`). It should mention campfires and swimming (2026-10-04) or something newer.
3. **Runtime libraries** (install any that are missing): `sudo dnf install -y mesa-libGL vulkan-loader libX11 libXcursor libXrandr libXi alsa-lib pulseaudio-libs gtk3 libatomic libicu`.
4. **Is Unity here?** Look for `~/Unity/Hub/Editor/6000.5.10f1/Editor/Unity`, or search under `~/Unity` and `/opt`. Call it `<UNITY>`. If there is none, skip to D.

## B. Build and test from source (works over SSH)

1. **Open and import:** `<UNITY> -batchmode -nographics -projectPath <P> -quit -logFile ~/chopper-test/import.log`. Exit code 198 or "No valid Unity Editor license found" means the license is not active: stop and tell me.
2. **Rebuild the generated scenes and prefabs:** `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.NetworkScenesBuilder.Build -quit -logFile ~/chopper-test/scenes.log`. Look for "Player prefab, Boot and World rebuilt" and no "error CS".
3. **Unit tests:** `<UNITY> -batchmode -nographics -projectPath <P> -runTests -testPlatform EditMode -testResults ~/chopper-test/editmode.xml -logFile ~/chopper-test/tests.log`. Expect 254 passed and 0 failed. Report the counts and any failures.
4. **Development player and smoke test** (258 checks of the whole game loop):
   - Build: `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.BuildTools.BuildDevLinux -buildOutput ~/chopper-test/dev -quit -logFile ~/chopper-test/devbuild.log`. Look for "linux build Succeeded". If Linux build support is missing, stop and tell me.
   - Run: `~/chopper-test/dev/Chopper.x86_64 -batchmode -nographics -chopperHost -chopperSmoke -chopperTestSaves -chopperNoSteam -logFile ~/chopper-test/smoke.log`. Look for "[Smoke] PASS" and report every "[Smoke] FAIL" line.
5. **Co-op test** (a host and a joined client on this machine, 75 ms of simulated lag each way):
   - Host: `~/chopper-test/dev/Chopper.x86_64 -batchmode -nographics -chopperHost -chopperNetTest host -chopperNoSteam -chopperTestSaves -chopperLatency 75 -chopperJitter 15 -chopperPacketLoss 2 -logFile ~/chopper-test/host.log &`
   - Wait 6 seconds, then the client: the same command with `-chopperJoin 127.0.0.1 -chopperNetTest client` instead of `-chopperHost -chopperNetTest host`, and `-logFile ~/chopper-test/client.log`.
   - The host log must say "interest: far enemy hidden from the client True, a near one seen True" and "DONE". Report any "FAIL" or "[MovementCheck] Correction" lines.
6. **Release player:** `<UNITY> -batchmode -nographics -projectPath <P> -executeMethod Chopper.Editor.BuildTools.BuildPlaytestLinux -buildOutput <P>/Builds/Release/linux -quit -logFile ~/chopper-test/release.log`. Look for "linux build Succeeded". Do not make a zip unless I ask.

## C. Needs someone at the screen (tell me when you get here)

1. **Swim tour:** `~/chopper-test/dev/Chopper.x86_64 -chopperHost -chopperNoSteam -chopperTestSaves -chopperShot ~/chopper-test/shots/swim -chopperSwim -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile ~/chopper-test/swim.log`. Report every "[Swim]" line. Expect: floating with the feet about 1.05 m under the surface, swimming about 2.3 m/s, the camera at least 0.35 m above the water when looking up, wet after leaving the sea, and dry after about 11 seconds by the campfire. Check the PNGs: no grey wall when looking up from the water, and the campfires visible (stone ring, logs, small flames).
2. **Look tour:** the same command with `-chopperShot ~/chopper-test/shots/look -chopperLook` instead of the swim arguments. Tell me if any part of Chopper's body or clothes is missing, see-through or the wrong color in the five shots.
3. **Play it** (`<P>/Builds/Release/linux/Chopper.x86_64`). Report:
   - the version on the start screen, frame rate, anything pink, black or missing, audio, mouse look, fullscreen and a controller;
   - **trees:** they stand straight. Chopped on level ground they fall away from you; chopped from below on a hillside they come down the hill toward you;
   - **water:** you float at the top, swimming is slow and uses stamina, a "Wet" square shows after you get out, and a campfire dries it within about 10 seconds;
   - **Chopper:** look closely from the front and the back and tell me if anything is missing or see-through.

## D. No Unity on this machine

Ask me for `Chopper-<version>-linux.zip`, unzip it into `~/chopper-test`, `chmod +x Chopper.x86_64`, and run the headless check: `./Chopper.x86_64 -batchmode -nographics -chopperHost -chopperNoSteam -chopperStopAfter 20 -logFile run.log`. The log must contain "Scene loaded: World" and "[World] Built seed", and no "Crash!!!". Then do C3 with it. Installing Unity needs my Unity sign in: stop and tell me before trying.

Finish with a short summary of what worked and what failed, with the numbers from B3 to B5 and C1.
