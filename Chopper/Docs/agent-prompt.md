<prompt>

<goal>
Continue Chopper, a co-op survival game (Valheim leaning, Minecraft-style building), from the state already in the repo, toward chopper-3D-concept-2.0.jpg (world, house, character and objects). Read the report card first, fix what it reports, then work the open items below in order. Return when the work is done, blocked, or needs approval. Do not restart the character or rebuild the game from the concept image.
</goal>

<report_card_first>
The user runs the tests, builds and play checks and records the results, so agents do not spend tokens and time on them.
1. Before anything else, read Chopper/Docs/Reportcard.md.
   - FAIL: fix it first, using the user's notes and video times. Do not re-run the test to see for yourself unless the notes are not enough.
   - PASS: closed. Move the row to History with one line on what you did, and delete the item from Homework.md.
   - SKIP or blank: still open. Do not run it in the user's place; carry on with other work and leave the row as it is.
2. Do not run builds, Unity batch jobs, Play tours, EditMode tests or Blender checks yourself unless the user's prompt asks you to. Instead, at the end of your work, add what needs checking to Chopper/Docs/Homework.md and a matching empty row in Reportcard.md.
3. Each homework item has the next free ID (HW-n), the exact command (normally `pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 <task>`), what to look for, and what to paste back. Keep items few: only what proves your change, or what needs the user's eyes (look, feel, decisions).
4. Without a compiler, keep C# edits small and careful, and re-read every edited block once. A broken script stops the user from opening the project (2026-10-12: a stray fragment in WorldGenTests.cs did exactly that). If you changed C#, HW "Unity opens cleanly" must be in Homework.md.
5. A result counts as proof only when it is pasted in the report card (a RESULT or SUMMARY line, a log line, or the user's PASS on a picture or video) or when you checked it yourself because the prompt asked you to.
</report_card_first>

<homework_and_report_card_notes>
Issues found while setting this up (2026-10-12), already handled in Tools/homework.ps1 and Homework.md. Keep them in mind when you write items:
- Unity batch jobs must be waited for: `Start-Process -Wait` (homework.ps1 does it). A plain call returns in seconds while Unity is still working, and its exit code is not the result; read the "Exiting batchmode successfully", "Build Finished" or RESULT lines.
- `-runTests` quits Unity by itself. Never add `-quit` to it, or the tests never run.
- Batch jobs need the editor closed. homework.ps1 refuses to start while an editor has project-chop open.
- Start the game from PowerShell. Started from Git Bash it quits during world load.
- Some tours only take pictures (Fishing, Swim, Crypt, Mine, Site, Concept, GearLook, Motion). For those, ask the user to judge the shots or the video, and do not wait for a RESULT line.
- Logs and shots from homework go to Logs/homework/. Agent-run logs stay in Logs/chopper_play/.
- Commits: the user stages, commits and pushes. End with a suggested one-line commit message, no author or co-author line.
</homework_and_report_card_notes>

<project_brief>
Project: Chopper, a live Unity game (repo root) plus the Chopper art project (Chopper/).
Audience: a solo developer. Art must read clearly at third-person gameplay distance.
Look: concept 2.0 for everything (Minecraft shapes, Valheim-style light and motion). The movement is blocky, with no ragdoll and no motion capture on the base layer.

Already done, do not redo (proof in TASKBOARD.md). Written in code and waiting on the report card counts as done for building; do not rewrite it:
- Character pipeline through W8-15. Gathering has its own Gather take (W8-16), wired, unproven in play (HW-9).
- Live player: unarmoured start, armour kit from boss trees, double jump, stepOffset 0.65, no sliding on block tops.
- Hub house dressed (Hut.DressCabin). Enemy paths out: KeepOut is off, stair obstacles are off, each flight has a NavMeshLink (W8-17). House tour must log PathComplete from basement, main floor and loft (HW-7).
- Block world through W5-08 step 12 (minimap from columns) and the ring peaks. Backdrop: eight separate snow-capped peaks in the sea (WorldGen.BackdropHeight). Step 13: no Unity Terrain object; WorldGround reads only the columns; saves are version 2 and older files stay unloaded.
- W9-01 arrival drop and W9-02 death drops the axe and outfit. W9-03: no building in the hub (PlaceResult.HubProtected) unless BuildManager.DebugAllowHubBuilding. The mine ladder prompt already prefers the ladder; the old "spent vein" shot was the tour teleport.
- Playtest guide: Chopper/Docs/playtest-windows.md.

Still open, in this order:
1. Whatever the report card says. Blank rows stay blank. Do not run the user's playtest for them. Do compile when a C# change needs it.
2. Website gallery only from the user's screenshots (HW-6), cropped to 1600 x 900 in Website/images/. Do not restore the old pictures.
3. Art-project ChopperCheck expects 12 skinned meshes and the FBX has 13; do not mark it done. W2-05 needs a reviewer who is not the builder.
4. W9-04 dome and W9-05 mining combo are written, unproven. Dropped axes and outfits are already saved (`GearRecord`). Raids on forest bases are still later. Only after the report card has no FAIL.

Tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. Git. No gh CLI. Tools/homework.ps1 runs every check (see Homework.md).
Constraints: no AI image-to-3D meshes. Do not remodel Chopper from scratch. Do not replace the live Animator controller (swap motions only). Mesh read/write stays on. Do not run PieceBuilder.BuildAll. Work on main. No commits, pushes, deletions, deploys or paid services without explicit approval. One Unity project at a time.
</project_brief>

<working_rules>
- Report card first (above). Then TASKBOARD.md, the status board: rows marked done are closed unless the report card says otherwise.
- Update TASKBOARD.md, Homework.md and Reportcard.md as you work. Update this file and cursor-claude-handoff.md only when the user says "Summary".
- Ask one focused question only when the answer changes what gets built.
- End with: what changed, the proof you have, the new homework IDs, what is still open, and a suggested one-line commit message (no author line).
</working_rules>

<start>
Read Chopper/Docs/Reportcard.md, act on it, then continue with the first open item that the report card does not cover.
</start>

</prompt>
