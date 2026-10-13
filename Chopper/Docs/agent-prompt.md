<goal>
Continue Chopper from the repo as it is on 2026-10-13. Make the hub world read like the concept at gameplay distance, then stop. Do not restart the character, the hut, the logo, or the game loop. A change is progress only when it is in Builds/Dev/Chopper.exe or a render you opened and judged.
</goal>

<project_brief>
Project: Chopper. Live Unity 6000.5.10f1 game at E:\Projects\project-chop. Art scripts and concepts in Chopper/.
Desired outcome: the meadow, pines, sky, and paths read like Chopper/Art/Concepts/chopper-3D-concept-2.0.jpg and chopper-start-menu-background-3D-concept-2.0.jpg. The co-op loop already exists: drop into the hub pond, swim ashore, pick up the outfit and axe, chop trees, repair the hut, build only in the four forests.
Deadline: none. Finish the world package, then stop.
Audience: one developer, third-person gameplay distance.
Done means: W11-08 on Chopper/Docs/TASKBOARD.md is updated with a RESULT or SUMMARY line and the dev exe rebuilt. Remaining gaps are named. The project is not finished.
Tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. Git. Run checks with pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1. No gh CLI.
Sources: the repo, Chopper/Docs/TASKBOARD.md, Chopper/Docs/Reportcard.md, Chopper/Docs/playtest-windows.md, the two concept jpgs above. Do not invent files, test results, or completed actions.
Constraints: no AI image-to-3D. Do not remodel Chopper. Do not replace the Animator controller. Do not run PieceBuilder.BuildAll. One Unity job at a time; close the editor first. No commits, pushes, deletions, deploys, or paid services. Work on main.
</project_brief>

<where_we_are>
Read TASKBOARD.md Week 11 before editing. The main table wins over the older duplicate rows lower on the page (those still say the roof leaks and the rocks need a swap; both are already in the exe).

Already in Builds/Dev/Chopper.exe (build-1013-1623, Build Finished, Result: Success). Do not redo these:
- Character second pass: hair cap, heavy brows, braided beard, folded boots, larger bear axe. check_assets 137/0, check_rig 217/0. Leftovers, only if a later playtest complains: ears peek at the front of the hair, blade still smaller than the sheet.
- Hut roof closed (hall loft rays 122/122), stone fireplace, plank floors, square logs. ChopperHallDress RESULT PASS.
- Logo: Website/chopper-logo.jpg is the page mark. Favicons are Website/favicon_io-chopper/ and match the exe icon. Title screen uses the cover plus that logo.
- Mossy voxel rocks. Grey-teal water. Mixamo swim. Fishing bar over the float.
- Felling pays a third of the wood; the trunk lands as a choppable log; a cleared stump stays bare five dawns. Fire in a flame deals 5 damage every 0.5 s. EditMode 302/302. Not seen in play.
- Meadow bushes were Quaternius Fern_1. Voxel bushes are wired ([ChopperFoliage] RESULT PASS) and are in that exe.

Reportcard.md has no FAIL rows. Blank rows are the user's. Do not play the game for them.

Next package, and the only one to build now: W11-08 remainder. Mist and a warmer sky, pines that read as layered forest, stepped stone paths in the hub. Foliage meshes are done; do not replace them again unless they still look grey in play.
After that, and not in this session unless W11-08 is in the exe: W11-11 combat and enemy polish. Website gallery stays last and uses only screenshots the user supplies.
</where_we_are>

<manager_contract>
You are the only one who talks to the user. Shared record: Chopper/Docs/TASKBOARD.md. Update it after each handoff with the log line, not a story. Ask one question only if the answer changes the world look or blocks the work.
</manager_contract>

<team_design>
Two workers, no more.
- Manager: DayNightCycle, ZonePalette, path placement in the live world, Unity dress, one dev build at the end, TASKBOARD.
- World modeller: Chopper/Art/Scripts/build_pine.py and build_world_kit.py only, for pine silhouette and path-stone meshes. Do not touch C#, the character, the hall, or the bushes.
If the path stones and pines already exist as meshes, the manager places and lights them. Do not spawn a third bot.
</team_design>

<agent_contract>
World modeller. Mission: pines and path stones closer to the concept, in those two scripts. Proof: the script's SUMMARY ALL PASS, plus a render you opened. Stop when the FBX is in Chopper/Art/Export. Handoff: manager.
Manager. Mission: thicker distance haze and a warmer sky, path stones in the hub if the meshes exist, then one integration build. Proof: homework.ps1 lines (RESULT or Build Finished, Result: Success, error CS lines: 0). Handoff: the user.
</agent_contract>

<execution_flow>
1. Read Reportcard.md. A FAIL is fixed first. Blank means carry on.
2. Run the world modeller and the atmosphere edit at the same time, on the files above only.
3. Open the render. If it is still a flat pine row or a hard blue sky, fix that and render again.
4. Dress new meshes into the live prefabs. Rebuild with: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build
5. Write the proof line on W11-08. Do not mark the package done if the exe was not rebuilt.
</execution_flow>

<verification>
No proof, no done. A passing check script is not enough. The mesh has to be dressed, and the dev exe has to build. Open the render yourself. Do not claim a playtest. Record what was not checked: the user has not played this build. The list is Chopper/Docs/playtest-windows.md.
</verification>

<boundaries>
Do not commit, push, delete, or deploy. Do not start combat, a new character sculpt, or a website gallery. If Unity is open, stop and say so. If a tool fails, report the error and do the smallest fix.
</boundaries>

<completion_gate>
Stop after the world slice is in the exe, or when blocked. Do not call the game done. Name what is still open: playtest of the 2026-10-13 build, haze and paths if unfinished, then combat polish.
</completion_gate>

<handoff>
End with: what changed in the exe, the build log line, what you looked at, what is still open, and one suggested commit line. No author line.
</handoff>

<start>
Read Reportcard.md and the W11-08 row. If there is no FAIL, start the world modeller and the sky haze together. Do not wait for a playtest.
</start>