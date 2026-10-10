# Chopper requirements

Last updated: 2026-10-09. The single file from 2026-10-08 is in `Docs/archive/2026-10-08/requirements.md` (that copy stays in git). These working notes stay on this PC. The `requirements/` folder is gitignored.

## Art direction

The Valheim art direction is retired. The game still contains that mix: the pixel pack world, Quaternius characters, and shapes built in code. Those assets stay in the project until they are replaced. Do not buy, paint, or import anything to match that look.

The new art direction is not chosen. Write it at the top of `requirements-assets.md` before any new model, texture, icon, creature, or motion is bought or made.

Gameplay can stay a Valheim-like co-op survival game. This change is the look, not the loop.

## Files

| File | What it holds |
|---|---|
| `requirements-assets.md` | The new direction (empty until you write it), what the game uses today, and the frozen shopping list from the old look |
| `requirements-animation.md` | Motions. The Moving group is in the game and still waiting for approval |
| `requirements-audio.md` | Sounds and music still wanted |
| `requirements-website.md` | Website screenshots |
| `requirements-steam.md` | Steam demo store assets |
| `elderwood-recolor-guide.md` | Old night-creature paint notes. On hold with the retired direction |

## Rules

Every asset must allow a commercial game (CC0 is easiest; CC BY needs a credit line; Asset Store, Fab and Mixamo are fine). Raw downloads go in `Chopper/SourceAssets/` (not in git, never imported by Unity). Never put downloads in `Assets/_Chopper/Resources`: everything in that folder is packed into every build.

Style for docs, code comments, commits, and in-game text: no emojis and no em dashes.

How to run, test, and build is `running/RUNNING.md`. The next session prompt is `tomorrow/tomorrow.md`.
