# 03 How to make a change and prove it

## Before you edit
1. Check nothing is running: `Get-Process Unity,Chopper -ErrorAction SilentlyContinue`. If a `Unity.exe` with `-batchmode` or the Unity editor or `Chopper.exe` is running, STOP and tell the human. (`unity.exe serve` is the Hub; ignore it.)
2. Find the code with `search_files` (regex) first. Then `read_file` the whole file you will change.
3. Before you call any method, property or class, search that it exists with that exact name and arguments. If you cannot find it, do not invent it: say "not found" and stop.

## Editing
- Use `replace_in_file`. The SEARCH block must be copied exactly from the file you just read (spaces included), 3 to 10 lines, unique in the file.
- Use `write_to_file` only for a NEW file. Never for an existing `.cs` file.
- One task = the smallest change that does it, at most 3 files. No clean-ups, renames or reformatting on the side.
- Match the code around you: same naming (`_camelCase` private fields, PascalCase public), same brace style, 4 spaces, no comments.

## Prove it (from the repo root, in this order)
1. `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build`
   It must end `Build Finished, Result: Success` and `error CS lines: 0`.
   If not: open the newest `Logs\homework\build-*.log`, search `error CS`, fix ONLY that line. Two failed tries: undo your own edits with `replace_in_file` (put the old text back) and report the error text to the human.
2. `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 editmode`
   It must report `failed 0`. A failing test means your change is wrong; fix the code, do not edit the test to pass unless the task asked for new behaviour.
3. If the task names a tour: `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 tour <Name>` and read its `RESULT` line.
Run one Unity job at a time. A build takes several minutes; wait for it.

## Report
Append ONE line to the end of `Chopper/Docs/TASKBOARD.md`:
`- RESULT YYYY-MM-DD HH:MM <what changed, files>; build <Success/FAIL> CS <n>; editmode <passed>/<total> <failed> failed; tour <Name> <PASS/FAIL>; still off: <what is not done>.`
Copy the numbers from the command output. Never write a result you did not see. Never say the game "feels" fixed; the human judges feel.
Then end with a one-line suggested commit message. Do not commit.
