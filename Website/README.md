# Chopper website

The landing page for Chopper, a co-op survival game in Unity. Published at https://thurtea.com/3D-Game/ by `.github/workflows/chopper-pages.yml`, by hand only (Actions, Chopper website, Run workflow; repository Settings, Pages, Source: GitHub Actions).

Plain HTML, CSS and a little JavaScript, with no build step.

The page says what the game is, and that one developer is building it in Unity with Claude Code and Cursor. The banner is the top band of the user's title-screen art (`banner-chopper.png`, cropped above the menu). The gallery shows the user's art: the title screen, the model sheet, the hut concept and the armoured cover. In-game screenshots join it when the user supplies them.

## Files

| Path | What it is |
|---|---|
| `index.html` | The page: the game, and the gallery |
| `main.css`, `main.js` | Styles and the small menu script |
| `favicon_io-chopper/` | Favicons and the web manifest |

## Preview locally

From this folder run `python -m http.server 8000` and open http://localhost:8000/.
