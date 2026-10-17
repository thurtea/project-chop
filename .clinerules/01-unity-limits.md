# 01 Hard limits (read first, never break)

Project: Chopper, Unity 6000.5.10f1, URP, FishNet. C# only. Repo root `E:\Projects\project-chop`.

## You may edit
- `.cs` files under `Assets/_Chopper/Scripts/` and `Assets/Tests/EditMode/`.
- `Chopper/Docs/TASKBOARD.md` (append one line at the end, never change old lines).

## You must NOT edit, create, move or delete
- Any `.prefab`, `.unity`, `.controller`, `.asset`, `.mat`, `.meta`, `.fbx`, `.png` file. They are Unity YAML/binary. Editing them by hand breaks the game.
- Anything in `Packages/`, `ProjectSettings/`, `Library/`, `Builds/`, `Logs/`.
- `Assets/_Chopper/Resources/Audio/`, `Chopper/Docs/agent-prompt.md`, `Chopper/Docs/cursor-claude-handoff.md`, `Chopper/Docs/unity-assets.md`.
- `Chopper/Art/Scripts/build_chopper_voxel.py` (never run it either).

## Never
- Never delete a file, class, method, field or enum value. Never rename them.
- Never reorder an enum. New values go at the END (saves and key binds store them by number).
- Never rewrite a whole existing `.cs` file. Change only the lines the task needs.
- Never add `//` or `/* */` comments. Put the reason in the TASKBOARD line instead.
- Never commit, push, or run `git reset`, `git checkout --`, `git clean`, `git stash`.
- Never add packages, ranged weapons, or asset-store art.
- Never change the Animator controller by text. Animator states come from editor methods such as `Chopper.Editor.WeaponAnimSetup.Apply`.
- Never add a `NetworkBehaviour` by hand to a prefab. Ask the human (it needs an editor setup method).

## Locked numbers (do not change)
Move 6, sprint 9.5, crouch 2.6, jump 1.05, gravity 26 (defaults in `Core/GameConfig.cs`; the live values are in the config asset, which you must not edit).
Acceleration, deceleration and turning are only in `Core/SteerRules.cs`; do not change them.
Do not replace `Chopper/Block`, `Chopper/Water`, `DayNightCycle`, `ZonePalette`, or ZoneTracker's fog.
