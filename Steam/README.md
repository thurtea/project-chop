# Steam store images

This file maps each Steamworks image slot to an art file that already exists in the repo. No art in this folder was made for Steam. Slots that no file can fill are marked **empty**.

Source files measured 2026-10-13:

| File | Size | What it shows |
|---|---|---|
| `Website/chopper-logo.jpg` | 656×656 JPG, no alpha | Logo: hut, crossed axes, rune, "CHOPPER" banner, meadow behind |
| `Website/Chopper-Banner.jpg` | 656×656 JPG, no alpha | The same picture as the logo (different bytes) |
| `Website/Chopper-Character-Cover.jpg` | 686×752 JPG | Chopper with axe and shield, horned skull helm, meadow behind |
| `Website/favicon_io-chopper/` | 16, 32, 180, 192, 512 PNG; `favicon.ico` with 16, 32 and 48 | The exe icon (the 512 matches `icon.png`) |
| `Chopper/Art/Concepts/chopper-3D-concept-2.0.jpg` | 1376×752 JPG | Hut by the pond, pines, mountains, Chopper on the right, no logo |
| `Chopper/Art/Concepts/chopper-character-and-axe-3D-concept-2.0.jpg` | 1376×752 JPG | Model sheet on grey: Chopper, base layer, the axe |
| `Chopper/Art/Concepts/chopper-start-menu-background-3D-concept-2.0.jpg` | 1408×736 JPG | Hut, pond, pines and mist, no character, no logo. **This is the game's menu backdrop** (`Assets/_Chopper/Resources/Branding/start-menu-background.jpg`), not a capsule |

Status key:
- **Fits:** the file covers the slot with a crop and a downscale, without upscaling.
- **Candidate:** the file is the closest match, but something is missing. The gap is named.
- **Empty:** no existing file can fill the slot.

Steam asks for the game name to be readable on store and library capsules. Only the logo files carry the name.

## Store

| Slot | Size | File | Status |
|---|---|---|---|
| Header capsule | 920×430 | none | **Empty.** The only files with the name are square (656×656). A 920×430 crop of a concept image would have no name |
| Small capsule | 462×174 | none | **Empty.** Same reason as the header capsule |
| Main capsule | 1232×706 | none | **Empty.** `chopper-3D-concept-2.0.jpg` (1376×752) is large enough to crop but has no name on it |
| Vertical capsule | 748×896 | `Website/Chopper-Character-Cover.jpg` | **Candidate.** 686×752 needs an upscale of about 1.2×, and the cover has no name on it |
| Page background | 1438×810 | `chopper-start-menu-background-3D-concept-2.0.jpg` | **Candidate only.** 1408×736 needs an upscale of about 1.1×. The same file is the menu backdrop |
| Screenshots | 1920×1080 suggested | none | **Empty.** These must come from the game. The user supplies them |

## Library

| Slot | Size | File | Status |
|---|---|---|---|
| Library capsule | 600×900 | `Website/Chopper-Character-Cover.jpg` | **Candidate.** A 2:3 crop of the 686×752 cover is 501×752 and needs an upscale of about 1.2×. There is no name on the cover |
| Library header | 920×430 | none | **Empty.** Same reason as the store header capsule |
| Library hero | 3840×1240 | none | **Empty.** The largest file is 1408 px wide. The hero must also have no logo or text |
| Library logo | 1280×720 PNG with transparency | none | **Empty.** `Website/chopper-logo.jpg` is a 656×656 JPG on a painted background with no alpha |

## Community and client

| Slot | Size | File | Status |
|---|---|---|---|
| Community icon | 184×184 | `Website/favicon_io-chopper/android-chrome-192x192.png` (or the 512) | **Fits.** Downscale only |
| Client icon | ICO with 16, 24, 32, 48, 64, 96, 128 and 256 | `Website/favicon_io-chopper/favicon.ico` | **Candidate.** It holds 16, 32 and 48 only. The missing sizes (24, 64, 96, 128, 256) would be downscaled from `android-chrome-512x512.png`. That ICO has not been built yet |

## Exported 2026-10-14 (`Steam/images/`)

From the user's art: `banner-chopper.png` (2560×1037, the title screen with the name and the menu) and `chopper-cover.jpg` (686×752, same bytes as `Website/Chopper-Character-Cover.jpg`). Crop boxes are in source pixels.

| File | From | Crop | Scale | Status |
|---|---|---|---|---|
| `header_capsule_920x430.jpg` | banner | 175,0 – 905,341 (title band) | 1.26× up | **Candidate.** Name readable, but tight to the letters, and the menu's gold rule shows along the bottom |
| `library_header_920x430.jpg` | banner | same as the header | 1.26× up | **Candidate.** Same file as the header |
| `small_capsule_462x174.jpg` | banner | 170,0 – 910,279 | down | **Fits.** Name fills the capsule |
| `main_capsule_1232x706.jpg` | banner | 0,0 – 1810,1037 | down | **Candidate.** Name and hut, but the menu buttons (Continue … Exit) are in it |
| `page_background_1438x810.jpg` | banner | 1000,0 – 2560,879 (right side, no text) | down | **Fits** |
| `vertical_capsule_748x896.jpg` | cover | 29,0 – 657,752 | 1.19× up | **Candidate.** No name on it |
| `library_capsule_600x900.jpg` | cover | 92,0 – 593,752 | 1.20× up | **Candidate.** No name on it |

Still **empty**: library hero (3840×1240: the widest text-free band is 1560 px), library logo (1280×720 PNG with transparency: the wordmark is painted onto the scene), screenshots (from the game), client ICO sizes.

## Not done here

- Nothing has been uploaded to Steamworks.
- Every **Empty** slot needs new art with the name on it, or a game screenshot (hero). That is the user's decision.
