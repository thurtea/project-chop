# Continue Claude's Chopper animation work

Continue the interrupted work in `E:\Projects\3D-Game`. The Unity project is `E:\Projects\3D-Game\Chopper`. Read `summary/summary.md`, `prompts/cursor-prompt.md`, `requirements/requirements.md`, and `tomorrow/tomorrow.md` before changing anything. The Valheim art direction is retired. Do not treat the motion clips as approved, and do not buy art for the old look. Treat the current worktree as the source of truth and preserve all existing user changes.

## First: reconcile the logs and current state

1. Read the Claude/session logs supplied with this prompt, if available. Identify the last completed action, any unfinished command, and the exact result of tests, builds, or tours. Do not infer success from a command starting or from a Unity editor import.
2. Compare that evidence with the current files and `requirements/requirements-animation.md`'s "Review, 2026-10-08" section. If the pasted logs are not available in Cursor, continue from the recorded state below and inspect the project logs. Do not invent missing results.
3. The local `Chopper/Logs/Editor.log` was checked while preparing this prompt. It shows Unity 6000.5.10f1 importing assets and compiling, with no C# compiler error or test result found. It does contain licensing/D3D and thread-abort notices. This log alone does not prove that EditMode tests, the smoke test, or a player build passed.
4. Do not discard, regenerate, or overwrite existing work just to get a clean starting point. Do not run destructive Git commands, commit, or push.

## Current work and exact stopping point

Claude replaced selected `CodeMotions` movement stand-ins with the user's converted Mixamo clips through `Assets/_Chopper/Scripts/Editor/MotionSlots.cs` and `CharacterSetup.cs`. Only these slots are currently in the project:

- `idle.fbx`, `walk.fbx`, `run.fbx`, `sprint.fbx`, `swim.fbx`, and `swim_idle.fbx` in `Assets/ThirdParty/Mixamo_Motions/`.
- `MotionSlots` prepares these as Humanoid clips and falls back to `CodeMotions` for absent clips. Do not remove fallback clips until the user approves the replacements.
- The preview captures are in `Shots/motions-2026-10-08/`. The `slot_*.png` files show the imported clips on Chopper, `motion_*.png` the stand-ins, and `ingame_*.png` the in-game tour.

The documented review results in `requirements/requirements-animation.md` are:

- Idle, walk, run, sprint, swim, and swim idle are integrated, but **awaiting the user's approval**.
- Walk foot slide was measured at 0.09 m/s. Run slide without the axe was 0.66 m/s.
- With the axe drawn, run pumps the axe arm and throws the head up and back. Sprint is worse and swings the head into the ground.
- Crouch walk, fall, and hard landing were rejected; keep their stand-ins. Chop has no suitable clip and remains a stand-in.
- Still missing from the Moving group: suitable crouch walk, falling loop, hard landing that recovers, jump start, landing, roll, and chop.

The axe issue is an open user-facing design choice. A one-handed weapon run clip would address it; an arm layer that holds the axe arm still was only allowed if the user asks. Do not implement the arm layer, substitute a new clip, remove stand-ins, or advance into another animation group without that approval. Do not mark the current clips approved on the user's behalf.

## Task

Finish only the safe, unblocked portion of Claude's current animation work:

1. Inspect the current `MotionSlots` integration and the supplied logs for specific implementation or validation failures. Fix only demonstrable bugs in the existing Moving-group integration. Keep all absent/rejected slots on their working `CodeMotions` fallbacks.
2. Do not make aesthetic choices or change the animation review outcome. If the run/sprint axe issue cannot be fixed without a new user clip or explicit approval for the arm-layer approach, leave it unchanged and report it clearly.
3. If the Unity editor is open on this project, tell the user once and wait for them to close it before invoking batch Unity. Never make or use a shadow/project copy.
4. If validation is safe to run, use the existing project workflow. Run the EditMode suite (expected baseline: 293 tests) and the headless smoke test (expected baseline: 323 checks, `-chopperHost -chopperSmoke`). Player runs must use `-chopperTestSaves` and `-chopperNoSteam`, and must back up and restore `HKCU\Software\DefaultCompany\Chopper` exactly as `prompts/cursor-prompt.md` specifies. Do not report a baseline count as a fresh pass.
5. Look at the existing motion captures and any newly produced relevant screenshots. Do not claim visual approval. Do not build or replace the release player while the motion set awaits review; the current `tomorrow/tomorrow.md` explicitly says to wait for the user's review before a release build. Do not rebuild release zips.
6. Preserve the user's preference that they commit and push. If the project's Git instructions require staging, the only permitted Git command is `git add .` from `E:\Projects\3D-Game`; do not run it unless changes are ready and relevant.

Do not take on the unrelated future items in `tomorrow/tomorrow.md` (hammer, fishing rod, skill levels, health bars, enemy models, platform testing) during this continuation.

## Project constraints

- No emojis or em dashes in docs, comments, commit messages, or in-game text.
- No amateur placeholder assets. Raw downloads belong in `Chopper/SourceAssets`, never in `Assets` or Git.
- Every `MonoBehaviour` and `NetworkBehaviour` must be in its own file named after its class.
- After code or setup changes, run the project's network-scene build before the relevant tests, following `prompts/cursor-prompt.md`. Only run Unity in batch mode against `E:\Projects\3D-Game\Chopper`.
- Do not ask the user to run commands. Do not commit or push.

At the end, report what the logs prove, what you changed, the exact tests/tours and outcomes, the remaining axe issue and approval boundary, and a short proposed commit message. Update `tomorrow/tomorrow.md` only if the actual continuation changes the next-session priorities; keep it accurate and do not imply that the user approved the motion clips.
