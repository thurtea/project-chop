<goal>
Continue Chopper from the repo as it is on the morning of 2026-10-14. Polish the live Chopper character until he reads closer to the model sheet at gameplay distance, put that body in Builds/Dev/Chopper.exe, then stop. A change is progress only when the new mesh is in that exe, or a render or in-game shot you opened and judged against the sheet.
</goal>

<project_brief>
Project: Chopper. Live Unity 6000.5.10f1 game at E:\Projects\project-chop. Art scripts and concepts in Chopper/.
Desired outcome: one more polish pass on the smooth Chopper body, clothes, hair, beard, and bear axe so the dressed look matches chopper-character-and-axe-3D-concept-2.0.jpg more closely. Record it as W11-44 on Chopper/Docs/TASKBOARD.md.
Deadline: none. Finish this pass, then stop.
Audience: one developer, playing the Windows dev build.
Done means: the gaps you opened and confirmed are reduced in a new front and three-quarter render and in the Concept tour shots; check_assets and check_rig are 0 FAIL; the FBX and atlases in Assets/_Chopper/Art/Character match the export; the player prefab was rebuilt; the dev exe was rebuilt after that. Remaining mismatches are named. The character is not finished, and the game is not finished.
Tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. Git. Run checks with pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1. No gh CLI.
Sources: the repo, Chopper/Docs/TASKBOARD.md (Week 11, especially W11-36, W11-40, W11-41), Chopper/Docs/Reportcard.md, Chopper/Docs/unity-assets.md, Chopper/Art/Concepts/chopper-character-and-axe-3D-concept-2.0.jpg. Compare against the latest in-game views from Logs/homework/playtest-1014-0156/T15-Concept.jpg and the Concept tour sheet_front and sheet_three_quarter shots, plus Chopper/Art/Renders/base_look_*.png. Do not invent files, test results, or completed actions.
Constraints: no AI image-to-3D. Keep the smooth mesh. Do not run build_chopper_voxel.py and do not turn the body back into per-cube colour. Do not replace the Animator controller, the Humanoid bone names, or the module names. Do not change the axe grip frame. Do not run PieceBuilder.BuildAll. One Unity job at a time; close the editor first. No commits, pushes, deletions, deploys, or paid services. Work on main. Do not generate new Steam art. Do not start weapons, boats, mining, enemies, or the website gallery.
</project_brief>

<where_we_are>
Read TASKBOARD.md Week 11 before editing. The main Week 11 table wins. Reportcard.md has no FAIL rows. Blank Current rows are the user's. Do not play the game for them. The Automatic checks table (2026-10-14 02:08, playtest-1014-0156) is PASS or LOOK, including A1 check_assets 137/0 and check_rig 217/0, A7 Build Finished, and T15 LOOK. That table is the smooth body after the voxel revert. W11-43 sounds landed later in build-1014-0209 and did not change the character.

Already in the live body. Do not redo these:
- W11-01, W11-12, W11-14, W11-20, W11-21, W11-22: menu backdrop, stump cut, hut basement and log chinking, E no longer equips a tool, Steam/README.md, title wordmark. The user confirmed in play that felled trees leave the roots.
- W11-24 through W11-36 and W11-40: dressed start is the sheet (vest, belt, chainmail skirt, bracers). The armour kit adds the helmet and shield. Hair is a swept-back mass with a forehead showing, beard has curls, vest has patches and lacing, crossbelt is wider, mail is painted as rings, boots are short with a cuff, arms hang nearer the sides, chest warp is 1.18. Height stays about 1.80 m.
- W11-41 voxel rebuild was reverted the same day. In play the per-cube colour read as noise. build_chopper_voxel.py and the voxel branches stay in the repo and stay unused.
- World, hut, pines, water, placement, fishing shores, crypt and mine pads, KayKit strikes, double-jump flip, and the sound-bank swap are in the recent exe. Leave them unless a character export breaks the player.

Still open on the character, from the last sheet comparison (W11-36), and only if a fresh look at the sheet and T15 still shows them:
- Hair top is flatter and lower than the sheet. The height check is 1.79-1.81 m. Raise the crown inside that rule.
- Shoulders are broader and boxier than the sheet, and the vest covers the shoulder tops.
- The blade has no engraved marking. Paint it on the atlas. Do not enlarge the blade or move the grip.

If a fresh comparison shows a different miss that reads at gameplay distance, fix that and say so. Do not chase millimetre differences the sheet does not show at that distance.

Out of this session: W11-42 (sword, mace, spear, two-hander, boats, tree-enemy pack, bought animation pack), W11-11 motion feel, and the website gallery. The gallery uses only screenshots the user supplies.
</where_we_are>

<manager_contract>
You are the one manager the user talks to. Shared record: Chopper/Docs/TASKBOARD.md. Update it after each handoff with the log line, not a story. Add W11-44; do not reopen a closed row. Ask one question only if the answer changes the dressed silhouette, or if it blocks the work.
</manager_contract>

<team_design>
Three workers, no more.
- Reviewer: opens the model sheet, the current base_look renders, and T15. Writes the gap list. Does not edit meshes, atlases, or C# files. Reviews the new shots at the end.
- Modeller: one pass in build_chopper.py, atlas_paint.py, and the bear-axe paint in build_props.py. Owns the blend, the checks, and the renders. Does not open Unity.
- Manager: copies the export into Assets/_Chopper/Art/Character, rebuilds the player prefab, runs one dev build, writes W11-44, and stops.
The reviewer goes first. The modeller starts only from that gap list. The Unity build waits until the checks pass and the reviewer has looked at the new renders.
</team_design>

<agent_contract>
Reviewer. Mission: name what still misses the sheet at gameplay distance, then accept or reject the new shots. Inputs: the concept jpg, T15-Concept.jpg, base_look renders. Proof: a short gap list before the edit, and a pass or fail note after, each tied to a file you opened. Stop when the second note is written. Handoff: modeller, then the manager.
Modeller. Mission: close the reviewer's gaps on the smooth body. Proof: check_assets and check_rig SUMMARY lines with 0 FAIL, and new front and three-quarter renders. Stop when those renders exist. Handoff: reviewer, then the manager.
Manager. Mission: get the passed mesh into the player and the dev exe. Proof: md5 match of the copied FBX and atlases, the player-rebuild log (Exiting batchmode successfully), and homework.ps1 build lines (Build Finished, Result: Success, error CS lines: 0), plus the Concept tour shots opened. Handoff: the user.
</agent_contract>

<execution_flow>
1. Read Reportcard.md. A FAIL is fixed first. Blank means carry on. Do not treat T15 LOOK as a failure and do not re-run the closed playtest packages.
2. Reviewer writes the gap list from files that exist now.
3. Modeller edits only those gaps. Re-run check_assets and check_rig. Render front and three-quarter.
4. Reviewer opens the new renders next to the sheet. A miss that is still obvious goes back to the modeller once. Do not loop past that.
5. Manager copies the export, rebuilds the player, rebuilds with: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build. Then capture the Concept tour (sheet_front and sheet_three_quarter) and open those shots.
6. Write the W11-44 RESULT line with the check counts, the build log name, and what is still off. Do not mark it done if the exe was not rebuilt.
</execution_flow>

<verification>
No proof, no done. A passing check script is not enough. Open the model sheet and the new renders yourself, and open the Concept tour shots after the build. Record what was not checked: the user has not played this new build. The list of what they play is Chopper/Docs/playtest-windows.md.
</verification>

<boundaries>
Do not commit, push, delete, or deploy. Do not voxelise Chopper. Do not start W11-42, a combat rewrite, or a website gallery. Do not generate Steam images. If Unity is open, stop and say so. If a tool fails, report the error and do the smallest fix.
</boundaries>

<completion_gate>
Stop after W11-44 is in the exe, or when blocked. Do not call the character or the game done. Name what is still open: the user playing this build, any sheet mismatches the second review still sees, then W11-42 and the website gallery.
</completion_gate>

<handoff>
End with: what changed on the body, the check counts, the build log line, which images you opened, what is still off the sheet, and one suggested commit line. No author line.
</handoff>

<start>
Read Reportcard.md and the W11-36, W11-40, and W11-41 rows. If there is no report-card FAIL, have the reviewer open the sheet and T15 and write the gap list. Do not wait for another playtest, and do not start the modeller before that list exists.
</start>
