# Prompt: continue Chopper after the mining and snapping session

Copy everything below the line into Claude. The workspace is `E:\Projects\3D-Game`. Chopper is the Unity project in `Chopper`. Unity is `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe`.

---

Continue Chopper, a co-op survival game in Unity 6000.5.10f1 (URP, FishNet), in the private repo at E:\Projects\3D-Game (the game is in the Chopper folder). Read Chopper/summary/summary.md first, then this file, then Chopper/requirements/requirements.md if the work touches art. The same instructions are in Chopper/tomorrow/tomorrow.md. The Valheim art direction is retired (2026-10-09). The new direction is not chosen. Do not buy or import art to match the old look. My playtest build is Chopper/Builds/Release/windows/Chopper.exe. Rebuild that release player at the end of every session that changes the game, and make sure Chopper.exe itself gets a new timestamp, not only Chopper_Data. Do not rebuild the zips unless I ask.

Style: no emojis and no em dashes, in docs, code comments, commit messages and in-game text. Minimal UI, Valheim-style prompts. No amateur stand-ins. If one cannot be avoided, list it in requirements/requirements-assets.md in the same change. The folders requirements, playtests, prompts, summary, running and tomorrow are gitignored.

Standing rules:

- Act. Do not ask me to run things. Never copy the game folder anywhere. Shadow copies were deleted on 2026-10-06 at my request. Run Unity in batch mode on E:\Projects\3D-Game\Chopper itself. If my editor is open on it, tell me once and wait for me to close it.
- Every MonoBehaviour or NetworkBehaviour must be alone in a file named after its class.
- Player runs never touch my real saves or settings: reg export HKCU\Software\DefaultCompany\Chopper before; after, reg delete that key and then reg import. Pass -chopperTestSaves and -chopperNoSteam.
- Verify with the EditMode tests (expect 291) and the smoke test (-chopperHost -chopperSmoke, headless, 323 checks). Look at tour screenshots before calling visual work done.
- Raw downloads go in Chopper/SourceAssets (not in git, not in Assets). Blender 5.2 is installed.
- Git: the only git command you run is git add . from E:\Projects\3D-Game. I commit and push. Propose a short commit message at the end.

## Where the last session stopped

You hit the session limit while the full EditMode run and a dev-player build were finishing. Cursor picked up from that log.

The dev player build succeeded (`BuildDevPlayer`, about 7 seconds, 326 MB). That is not the release player. `Builds/Release/windows/Chopper.exe` is still stamped 4:01 PM on 2026-10-06, before mining and corner snapping. The exe I launch does not have this afternoon's work.

EditMode that evening: 284 passed, 2 failed, 286 total.

- `BuildRulesTests.AWallSnapsOntoAFloorEdge` (HouseRulesTests.cs): y and z were right (0.2 and -1). x was 0, and the check wanted a cell center (`Mathf.Repeat(x + 1, 2) == 0`). The floor in the test sits at the origin, so a corner join correctly centers the wall at x 0. The assertion now expects 0.
- `HouseRulesTests.TheChestHoldsEveryCarriedKind`: expected `FirstFish + FishingRules.Kinds` (43). Finds were appended, so `ItemRules.Count` is 61 (`FirstFind + MiningRules.Kinds`, 18 finds). The test now checks both spans, and that a find is not a fish.

Cursor also added `MiningRulesTests` (glint, strike quality, a vein staying in its mine, fishing-only finds staying on the line, cleaner strikes freeing bigger pieces) and `WorldGenTests.EveryWorldCutsOneMineIntoEachForest` (seeds 1, 7, 2026, 1337, 4242, 99991). That is 291 tests. They have not been run: the Unity editor was open on this project (Hub launch at 5:34 PM, lockfile held). Smoke from the same session already printed `[Smoke] PASS: full loop works`.

## Already in the game (do not redo)

- No project copies. ChopperShadow is gone. Batch Unity on E: only.
- Character creator restored and synced. Six outfits, each with its own body cut. Chopper's plaid is the default. Mixamo monk is out. `Chopper.Appearance2`.
- Axe: CC0 woodcutting axe, head and handle split so only the metal takes an element tint. It sits at the side and rides the arm. `-chopperAxeRun` was 0 flips. Holster and draw reach over the shoulder.
- Clock text for night and dawn is gone. "Day N" still fades in.
- Mining, as I asked: a pastime like fishing, not a crafting tier. One mine per forest, always, including seed 2026 (looser fallback passes). Timber portal, chambers, veins, lanterns, rails. Pickaxe from `Assets/ThirdParty/OGA_LowPolyTools` (colors muted). Eighteen finds with game-icons.net icons; four more artists are credited. Glowworm bait, geode and old pick head from fishing. Mining page on Tab. Saves keep finds the way they keep fish. Trees in front of a portal are pushed aside, not deleted. Tour: `-chopperMine`.
- Building snap: snap points on the piece assets, `BuildRules.SnapMoves`, `BuildManager.SnapToPieces`. House tour: every piece aimed up to 0.6 m off landed on its edge (0.000 m). Hut joints were clean (floors, walls, corners, roof).
- Night creatures stay the wood-creature stand-in until I drop in the Elderwood Guardian.
- Pixel pack is CC0, Markus Darius, itch.io.

The hammer model is in that same tools pack (`Hammer.fbx`) and is not attached to the hand. That is the next held item.

## Do this first

1. If the editor is still open, tell me once and wait. Then run EditMode and expect 291 passed, 0 failed. If a new test fails, fix it.
2. Rebuild the release player:

`Unity.exe -batchmode -nographics -projectPath E:\Projects\3D-Game\Chopper -executeMethod Chopper.Editor.BuildTools.BuildPlaytestPlayer -buildOutput E:\Projects\3D-Game\Chopper\Builds\Release\windows -quit -logFile <scratchpad>\release.log`

Confirm `Chopper.exe` itself has a timestamp after 4:01 PM. Re-run the smoke test on a dev player if you want a fresh 323 before I play. Do not rebuild the zips.

3. Wait for what I report from that build: creator, axe at the side, holster, one mine, snapping a wall onto a floor.
4. Put the hammer in the hand while building, the same way the axe and pickaxe sit in the fist. Then the fishing rod and float (requirements/requirements-assets.md). Do not buy one for the retired look.
5. Linux and Mac results (prompts/prompt-linux.md, prompts/prompt-macos.md). Their expected test count is 291. Those prompts are not in git.
6. Skills that level with use (logging, mining, foraging, combat) on the Character page.
7. Enemy health bars once a fight starts, and floating damage numbers.
8. When I hand over the Elderwood Guardian or boss treants, import them, recolor per element, and keep the sapling-to-awake switch.

At the end, rewrite Chopper/tomorrow/tomorrow.md as a prompt like this one, and propose a commit message. Do not commit the gitignored working-note folders.

Suggested message for the work already on disk, once the tests are green:

Add mining as a fishing-like pastime, snap building pieces corner to corner, and keep the axe from flipping while running.
