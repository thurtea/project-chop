CHOPPER - playtest build
========================

Chop the forests by day, hold the hut by night. Up to a few players, co-op.

Start
-----
Windows : run Chopper.exe
Linux   : run ./Chopper.x86_64   (if it will not start: chmod +x Chopper.x86_64)
macOS   : open Chopper.app. The app is not signed, so macOS may block it the first time.
          Right-click the app, choose Open, then Open again. If it says the app is damaged, run
              xattr -dr com.apple.quarantine Chopper.app
          once in a terminal, then open it again.

On the start screen choose New Game to host, or Join Game and type the host's address to play together
(the host must forward UDP port 7770, or you can use a VPN such as Tailscale or ZeroTier).
Character lets you pick your outfit, hair, beard and colors. Settings has volume, mouse sensitivity,
field of view and fullscreen or windowed.

Controls
--------
WASD move, mouse look, scroll zoom, Space jump, Shift toggle sprint, Ctrl toggle crouch,
hold left click chop and attack, press E at the hut to deposit wood (hold E to pick herbs), R repair the hut and walls,
Tab inventory and workbench, M map, 2 torch, 3 healing salve, Esc menu.
Xbox controller works too (see Controls in the Esc menu).

Please report
-------------
Crashes, anything that feels wrong, performance problems (say your CPU, GPU and operating system),
and what you liked. The log file helps with crashes:
  Windows : %USERPROFILE%\AppData\LocalLow\DefaultCompany\Chopper\Player.log
  Linux   : ~/.config/unity3d/DefaultCompany/Chopper/Player.log
  macOS   : ~/Library/Logs/DefaultCompany/Chopper/Player.log
