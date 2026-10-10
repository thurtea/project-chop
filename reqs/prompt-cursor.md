# Fix Chopper's character and everything he holds (Cursor prompt)

Continue Chopper, a Valheim-style co-op survival game in Unity 6000.5.10f1 (URP, FishNet), in the repo at E:\Projects\3D-Game (the game is in the Chopper folder). Read Chopper/summary.md first. Style: no emojis and no em dashes anywhere (code comments, docs, commit messages, in-game text).

## The problem, in my words

The character model is broken and does not look right, and nothing is held in the right spot. The axe has been held the wrong way round for four days. The torch looked right in my last test. This is the most important thing in the game right now.

## How to work

1. Before any code change, ask me for screenshots and for what looks wrong with the body. Do not assume.
2. Show me pictures before and after every change and let me confirm. The tours below make them; lay several out side by side in one image so I can compare quickly.
3. Measure, do not guess: the hold tour already logs numbers (see below). Check `[Facing]` first every time.
4. Fix one thing at a time and get my yes before moving on.

## What happened on 2026-10-07 (the last two commits; read their diffs)

- `499269d` (11:55): New Game now opens the character creator first (TitleScreen.cs, keep this). PlayerAxe got a runtime carry solve, and the editor-time `SolveAxeCarry` was deleted from CharacterSetup.cs. Also a mining test threshold change (keep).
- `e7a92c8` (12:27): `CharacterSetup.FacingCorrectionYaw` now returns 0 for a humanoid Avatar (it used to turn the model 180 degrees). The measurement before the change: the toes and hips pointed backward in the player's frame, so the player, the draugr and the golems ran backward. After it: forward. PlayerAxe solves the carry turn on the first frame in hand and refines it while standing. ShotCapture's hold tour got the `[Facing]` and `[Grip]` logs.
- Yesterday's commit `d6826a6` is the baseline before both. If the body looked better then, build it in a git worktree only if I agree (I do not allow copies of the project folder otherwise), or compare by reverting single files.

Possible causes worth checking, in order:
- The facing change: humanoid playback faces the model's +Z; check the `[Facing]` numbers, the creator preview (OutfitSetup.BuildPreview takes the same yaw), and the draugr and golem setups (DraugrSetup.cs, GolemSetup.cs call the same function).
- Outfits: `CharacterOutfitter` (Player/CharacterOutfitter.cs) binds each outfit part's SkinnedMeshRenderer to the skeleton's bones by name, hides the original mesh, and tints with property blocks. The catalog is built by `OutfitSetup.BuildCatalog`. Look for parts bound to the wrong bones, missing bones falling back to `root`, or the wrong outfit cut.
- Scale: `ChopperScale` and `ChopperBreadth` in CharacterSetup.

## How held items work now (the files)

- Model: `Assets/ThirdParty/Quaternius_UniversalAnimationLibrary/UAL1_Standard.fbx`, humanoid Avatar. Set up in `CharacterSetup.AttachModel` (Scripts/Editor/CharacterSetup.cs). Animator layers built in `CharacterSetup.BuildController`: `Grip` and `GripLeft` (fists closed on the right and left fingers), `CarryRight` and `CarryLeft` (the torch idle pose, mirrored for the right arm), `Stow` (the reach over the shoulder for draw and holster).
- Axe (right hand): `CharacterSetup.MeasureAxe` guesses the head end and edge from the mesh; `CharacterSetup.AttachHeld` parents `AxeGrip` under `hand_r` and writes `HeldItemFrame.Head` and `.Edge`. At runtime `PlayerAxe` (Scripts/Player/PlayerAxe.cs): `HoldInFist` puts the grip in the fist's hollow, `ApplyCarry`, `TrySolveCarry` and `SolveCarry` turn the hand so the head points forward and 18 degrees down. On the back: `BackPose`, `TickStow`. Model: `Assets/ThirdParty/OGA_WoodcuttingAxe/WoodcuttingAxe.fbx` (CC0).
- Torch (left hand): `CharacterSetup.AttachTorch` builds it; `HeldTorch.LateUpdate` (Scripts/Player/HeldTorch.cs) places it every frame along the fist's own line (`HandGrip.Across`, little finger to index), flame on the thumb side. This is the simple approach that worked; consider the same for the axe and pickaxe.
- Fist geometry: `HandGrip` (Scripts/Player/HandGrip.cs): `Center` is the fist's hollow, `Across` runs from the little finger to the index finger.
- Pickaxe: `CharacterSetup.AttachPickaxe` (same AttachHeld path as the axe), shown by `PlayerMining`.
- Fishing rod: `PlayerFishing`. Hammer: not in the hand yet (`Assets/ThirdParty/OGA_LowPolyTools/Hammer.fbx` exists).
- Guard pose (axe raised to block): `GuardPose` (Scripts/Player/GuardPose.cs).

What I want: every held item sits in a closed fist, handle through the fist, the business end on the thumb side (head of the axe, flame of the torch, head of the pick), and the same from the first frame, standing, walking, running, chopping and blocking. Valheim is the reference. If the axe model itself is the problem, propose a different CC0 one-handed axe, put the raw download in Chopper/SourceAssets and the source in Chopper/requirements.md.

## Tools (Windows, my editor must be closed; ask me if it is open)

- Unity: `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe`. Always batch mode on E:\Projects\3D-Game\Chopper itself, never a copy. Use a timeout; batch Unity hangs after a compile error.
- After changing CharacterSetup, OutfitSetup or any prefab setup: `-batchmode -quit -projectPath E:\Projects\3D-Game\Chopper -executeMethod Chopper.Editor.NetworkScenesBuilder.Build -logFile scenes.log`.
- Development player: `-executeMethod Chopper.Editor.BuildTools.BuildDevPlayer -buildOutput E:\Projects\3D-Game\Chopper\Builds\Dev`.
- Every player run passes `-chopperHost -chopperNoSteam -chopperTestSaves -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -chopperShot <folder> -logFile <log>` plus a tour:
  - `-chopperHold`: hand close ups standing, running and chopping, draw and holster in slow motion, the axe on the back from every side. Logs `[Facing]` (toes and hips in the player's frame, both should be about (0, 0, 1)) and `[Grip]` (axe head and edge in the player's frame, the handle's angle to the fist line, fingertip distances to the handle, the fist layer weight). The `[Grip]` angle is folded to 0 to 90 degrees; log the sign of `Vector3.Dot(head, HandGrip.Across)` too (positive means the head is on the thumb side).
  - `-chopperLook`: the body from five sides, the collar, cuffs and waist close up, the axe drawn and on the back, the torch by day and night.
  - `-chopperAxeRun`: walks and sprints with the axe drawn, logs `[AxeRun]` flips (expect 0).
  - `-chopperMine` (pickaxe), `-chopperFishing` (rod), `-chopperCombat` (blocking, the golem), `-chopperCrypt` (draugr).
  - Creator: no `-chopperHost`; `-chopperTitle <folder> -chopperCreatorTour` pictures every outfit.
- My settings are in the registry and the dev player shares them. Before player runs: `reg export HKCU\Software\DefaultCompany\Chopper backup.reg /y`. After: `reg delete HKCU\Software\DefaultCompany\Chopper /f`, then `reg import backup.reg`.
- Before calling it done: EditMode tests (`-batchmode -projectPath ... -runTests -testPlatform EditMode -testResults results.xml -logFile tests.log`, no `-quit`; expect 291 passed) and the smoke test (`Builds\Dev\Chopper.exe -batchmode -nographics -chopperHost -chopperSmoke -chopperTestSaves -chopperNoSteam -logFile smoke.log`, expect `[Smoke] PASS`).
- Then rebuild my playtest exe: `-executeMethod Chopper.Editor.BuildTools.BuildPlaytestPlayer -buildOutput E:\Projects\3D-Game\Chopper\Builds\Release\windows`, and set Chopper.exe's LastWriteTime to now (Unity leaves the exe alone when only the data changed).

## Also broken by the facing change (fix after the body and hands)

- The website tour (`-chopperSite`, ShotCapture.cs, "if (Array.IndexOf(... "-chopperSite")"): the hero shot's camera is now inside a tree, and the other pictures need checking at full size. Nothing goes on the website until I say so. The page is Chopper/Website/index.html with ten screenshot placeholders.
- Check any other offsets written in the body's frame that were tuned while the model faced backward: `PlayerAxe.BackGripOffset`, `BackHeadDirection`, `BackEdgeDirection`, `GuardPose`, the hero camera in the site tour.

## Git

Run only `git add .` from E:\Projects\3D-Game. I commit and push. Propose a one line commit message at the end.
