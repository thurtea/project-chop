# Chopper website

The site for Chopper, a co-op survival game in Unity. The repo copy is the current one. It is published at https://thurtea.com/3D-Game/ by `.github/workflows/chopper-pages.yml`, by hand only (Actions, Chopper website, Run workflow; repository Settings, Pages, Source: GitHub Actions). The VPS copy at https://thurtea.com/chopper/ was compared on 2026-10-14 and was not updated.

Plain HTML, CSS, a little JavaScript, and one PHP form. No build step.

The home page says what the game is, and that one developer is building it in Unity with Claude Code and Cursor. The banner is the top band of the user's title-screen art (`banner-chopper.png`, cropped above the menu). The gallery shows that art, the crypt and mine rooms from the 2026-10-14 tours, and the older in-game shots.

## Files

| Path | What it is |
|---|---|
| `index.html` | The game, and a short gallery |
| `gallery.html` | Art, cave rooms, and the older screenshots |
| `demo.html`, `demo.php` | Demo requests. Saved to `private_html/chopper-demo-requests.jsonl` and mailed to itsthurtea@gmail.com |
| `main.css`, `main.js` | Styles, the menu, and the demo confirmation |
| `favicon_io-chopper/` | Favicons and the web manifest |

## Preview locally

From this folder run `python -m http.server 8000` and open http://localhost:8000/.
