# Chopper website: screenshots to take

The website (`Chopper/Website/index.html`) shows nine pictures: `character.jpg`, `hut.jpg`, `night.jpg`, `pass.jpg`, `forest-pond.jpg` and `fishing.jpg` (updated 2026-10-05; `hut.jpg` was retaken without the old axe on the bench), plus `hud.jpg`, `bag.jpg` and `build.jpg` (added 2026-10-05, HUD on, to show the game-icons.net icons). Any of them can be replaced by a better shot you take yourself: drop it in `Chopper/Website/images/` with the same name. Then publish by hand: GitHub, Actions, Chopper website, Run workflow (it never runs on its own). The table below is the original shot list.

## Format

- **JPG, 1600 by 900** (16:9). A bigger 16:9 screenshot is fine; I can scale and compress it if you drop the PNG in and ask.
- Keep each under about 500 KB (JPG quality 85 to 90).
- Use the **release** exe (`Builds/Release/windows/Chopper.exe`), not a development build (that one prints "Development Build" in the corner).
- **Hide the HUD** for clean pictures: press F5 to open the console, type `hud`, Enter, then F5 to close it. Type `hud` again to bring it back. The console's `day`, `night`, `torches`, `axe` and `tp hut` commands help set a shot up (they work for the host).
- Settings: the highest graphics quality, full screen, FOV 80.

## The three pictures

| File | Page caption | What to show |
|---|---|---|
| `character.jpg` | "You play Chopper, axe in hand. The Fire forest is red pines on ash and crags." | Chopper from the front or three quarters, close enough to see the beard and the plaid shirt, the axe in his hand. Daylight with the sun behind the camera so his front is lit. Ideally in the Fire forest with red pines behind him (or change the caption to wherever you take it). |
| `night.jpg` | "Night at the hut. Light a torch and hold the line." | At night in front of the house, Chopper holding a lit torch, facing the camera or three quarters, with the house and the glowing yard torches behind him. |
| `fishing.jpg` | "The hut's pond. Work the lure and the fish come to it." | Chopper on the bank of the hub pond with the rod out and the float on the water, from behind and a little to the side so the water fills much of the frame. |

## Before you take them, check

- The axe and the torch sit **in his fist**, not floating beside it (this was being fixed on 2026-10-04; tell me if it still looks wrong and I will fix it before you shoot).
- No pickup cards or notes on screen (wait a few seconds after picking things up, or hide the HUD).

## Social preview

`character.jpg` is also the page's link preview image (the `og:image` and `twitter:image` tags), so it is the one people see when the link is shared.

## Optional extras (only if you want more on the page)

Tell me and I will add a spot on the page for any of these: a campfire by the hut at dusk, swimming in a forest lake, a grove warden fight, the inside of a crypt.
