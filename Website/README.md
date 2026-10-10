# Chopper website

The landing page for Chopper, a co-op survival game in Unity. Published at https://thurtea.com/3D-Game/ by `.github/workflows/chopper-pages.yml`, by hand only (Actions, Chopper website, Run workflow; repository Settings, Pages, Source: GitHub Actions).

Plain HTML, CSS and a little JavaScript, with no build step.

## Files

| Path | What it is |
|---|---|
| `index.html` | The one page: the game, home (workbench, chest, bed, building), crypts, fishing, playing together, status and credits |
| `main.css`, `main.js` | Styles and the small menu script |
| `chopper.jpg` | The logo art |
| `images/` | The page's pictures, 1600 by 900 JPGs from the `-chopperSite` tour (and the creator from `-chopperCreatorTour`) |
| `favicon_io-chopper/` | Favicons and the web manifest |

## Updating the page

- Keep the text short and honest, and the credits to what the game really uses (the music is by AlkaKrab; keep the credit).
- Retake the pictures with the `-chopperSite` tour after visible changes, check each at full size, and keep the file names.

## Preview locally

From this folder run `python -m http.server 8000` and open http://localhost:8000/.
