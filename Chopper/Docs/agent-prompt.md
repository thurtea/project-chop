<goal>
Continue Chopper from the repo as it is on the evening of 2026-10-13, after the user played the dev build. Fix the four playtest failures below, write the Steam asset readme, rebuild Builds/Dev/Chopper.exe, then stop. Do not restart the character, the combat pass, or the website gallery. A change is progress only when it is in that exe or a file you opened and judged.
</goal>

<project_brief>
Project: Chopper. Live Unity 6000.5.10f1 game at E:\Projects\project-chop. Art scripts and concepts in Chopper/.
Desired outcome: the boot menu uses the start-menu concept as its backdrop; E picks things up and does not also equip a tool; a felled tree leaves its roots and dirt pad on the stump; the hut basement is walkable and the log courses stop flickering; Steam/README.md names every store image slot and which existing file can fill it.
Deadline: none. Finish those five packages, then stop.
Audience: one developer, playing the Windows dev build.
Done means: W11-01, W11-12, W11-14, W11-20 and W11-21 on Chopper/Docs/TASKBOARD.md each have a RESULT line, and the dev exe was rebuilt after the code and art changes. Remaining gaps are named. The project is not finished.
Tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Python with PIL. Git. Run checks with pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1. No gh CLI.
Sources: the repo, Chopper/Docs/TASKBOARD.md, Chopper/Docs/Reportcard.md, Chopper/Docs/playtest-windows.md, Chopper/Art/Concepts/chopper-start-menu-background-3D-concept-2.0.jpg, chopper-3D-concept-2.0.jpg, chopper-character-and-axe-3D-concept-2.0.jpg. The user's 2026-10-13 17:45 screenshots: boot menu is the character cover with the logo stamped on it; a felled pine is a brown checker slab sinking over the player; inside the hut the beams crowd the character and the log courses shimmer. Do not invent files, test results, or completed actions.
Constraints: no AI image-to-3D. Do not remodel Chopper. Do not replace the Animator controller. Do not run PieceBuilder.BuildAll. One Unity job at a time; close the editor first. No commits, pushes, deletions, deploys, or paid services. Work on main. Do not generate new Steam art.
</project_brief>

<where_we_are>
Read TASKBOARD.md Week 11 before editing. The main Week 11 table wins. The older duplicate rows under "Week 11 team" are stale where they disagree (roof leaks, pale rocks, title backdrop stays the cover).

Already in Builds/Dev from build-1013-1702 (Build Finished, Result: Success, error CS lines: 0). Player data and Chopper.Runtime.dll were written 2026-10-13 17:02. Do not redo these:
- Character second pass: hair cap, heavy brows, braided beard, folded boots, larger bear axe. check_assets 137/0, check_rig 217/0. Leftovers, only if a later playtest complains: ears peek at the front of the hair, blade still smaller than the sheet.
- Hut roof closed (hall loft rays 122/122), stone fireplace, plank floors, square logs. ChopperHallDress RESULT PASS. The playtest still cannot walk the basement and still sees log flicker, so the interior is not closed.
- Logo file Website/chopper-logo.jpg is the page mark. Favicons are Website/favicon_io-chopper/ and match the exe icon. The boot menu must stop using the character cover as its backdrop.
- Mossy voxel rocks. Grey-teal water. Mixamo swim. Fishing bar over the float.
- Felling pays a third of the wood; the trunk is meant to land as a choppable log; a cleared stump stays bare five dawns. Fire in a flame deals 5 damage every 0.5 s. EditMode 302/302 was an earlier run. The 17:45 playtest shows the roots and dirt pad still falling.
- Voxel bushes are wired ([ChopperFoliage] RESULT PASS). Do not replace them again.
- W11-08 world slice is in that build: pines are four separated foliage tiers (build_pine.py SUMMARY ALL PASS), path stones are on the hub paths and the hall steps ([HubPaths] RESULT PASS), hub fog density 0.0095, fog colour (0.80, 0.76, 0.66), noon sky tint warm, atmosphere 1.45. The user has not said the haze or the paths look wrong. Leave them unless a fix in this session breaks them.

Reportcard.md has no FAIL rows. Blank rows are the user's. Do not play the game for them.

This session, in this order:
1. W11-01. Copy Chopper/Art/Concepts/chopper-start-menu-background-3D-concept-2.0.jpg to Assets/_Chopper/Resources/Branding/start-menu-background.jpg (or .png). TitleScreen.cs currently loads Branding/chopper-cover, then Branding/title. The full-screen backdrop becomes the copy. Keep the menu column (Continue, New Game, Load Game, Tutorial, Join Game, Settings, Credits, Exit). Leave Website/Chopper-Character-Cover.jpg and the logo file where they are. Do not use the concept jpg as a Steam capsule.
2. W11-21. Create Steam/README.md. List each Steamworks store slot, its pixel size, and which existing file can fill it. Empty slots stay empty and are named. Slots: header capsule 920×430, small capsule 462×174, main capsule 1232×706, vertical capsule 748×896, library capsule 600×900, library header 920×430, library hero 3840×1240, library logo 1280×720 PNG with transparency, page background 1438×810, community icon 184×184, client icon ICO at 16, 24, 32, 48, 64, 96, 128 and 256. Existing files to map, not to redraw: Website/chopper-logo.jpg, Website/Chopper-Character-Cover.jpg, Website/Chopper-Banner.jpg, Website/favicon_io-chopper/, the three concept jpgs in Chopper/Art/Concepts/. The start-menu concept is the game menu backdrop, not a capsule, unless the readme says a slot is still empty and that file is only a candidate.
3. W11-20. E is picking things up and equipping a tool in the same press. Defaults in Assets/_Chopper/Scripts/UI/Keybinds.cs: E is Interact, 1 is Axe, H is Unequip, R is Repair. E stays Interact. Move draw, holster, and hotbar-use off E. Search every WasPressed(KeyAction.Interact) and every hotbar use so a pickup does not also draw the axe, rod, torch, or hammer. PlayerAxe, PlayerInventory, PlayerFishing, PlayerBuilder, and PlayerWood are the listeners.
4. W11-12. On fell, the trunk and crown fall. The roots and the dirt pad stay on the stump, upright, until the player chops the stump. The 17:45 shot is a large brown checker slab (the pine pad) sinking over the player. FallingTree.cs says it cuts at the stump top; that cut is failing on prop_pine_voxel, whose pad is the cells at Z = 0 and whose foliage starts higher. Fix the cut. Do not delete the stump mesh.
5. W11-14. The basement is too small to walk into. Open it up toward the hall in chopper-start-menu-background-3D-concept-2.0.jpg: headroom and a path a player can walk, including the stairs. Then stop the log-course flicker the user still sees on the interior walls. Do not run PieceBuilder.BuildAll. Re-dress with the existing hall and piece dress methods.

After the exe is rebuilt, stop. W11-11 combat and enemy polish stays next and is not this session. The website gallery stays last and uses only screenshots the user supplies.
</where_we_are>

<manager_contract>
You are the one manager the user talks to. Shared record: Chopper/Docs/TASKBOARD.md. Update it after each handoff with the log line, not a story. Ask one question only if the answer changes the menu, the basement size, or the key layout, or if it blocks the work.
</manager_contract>

<team_design>
Three workers, no more.
- Manager: the menu backdrop copy, Steam/README.md, Unity dress, one dev build at the end, TASKBOARD.
- Gameplay: Keybinds and the equip-on-E bug, FallingTree and the stump cut. Do not touch the character, the hall mesh, or the bushes.
- Hut: basement clearance and the log flicker, in the hall and piece scripts that already dress the live hut. Do not remodel Chopper. Do not run PieceBuilder.BuildAll.
The menu copy and the readme can run beside the gameplay and hut edits. The Unity build waits until all three have handed back.
</team_design>

<agent_contract>
Manager. Mission: copy the start-menu concept into Branding and point TitleScreen at it; write Steam/README.md from the slot list above; dress and build once. Proof: the backdrop file opened, the readme opened, homework.ps1 build lines (Build Finished, Result: Success, error CS lines: 0). Handoff: the user.
Gameplay. Mission: E only interacts; roots and the dirt pad stay on the stump. Proof: the key path named in a TASKBOARD line, and the cut described against the pine mesh (pad at Z = 0). Handoff: manager.
Hut. Mission: a walkable basement and stable log courses, dressed into the live hut. Proof: the dress RESULT line and a note of what was opened. Handoff: manager.
</agent_contract>

<execution_flow>
1. Read Reportcard.md. A FAIL is fixed first. Blank means carry on. The 17:45 playtest notes on the task board are the FAIL for this session even though the report card rows are blank.
2. Run the menu copy, the key and tree fix, and the basement and flicker fix at the same time, on the files each role owns.
3. Open the menu image you copied and confirm it is the meadow concept, not the character cover. Open the readme and confirm every slot has a size and either an existing file or the word empty.
4. Dress the hut changes into the live prefabs. Rebuild with: pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 build
5. Write the proof line on W11-01, W11-12, W11-14, W11-20 and W11-21. Do not mark a package done if the exe was not rebuilt.
</execution_flow>

<verification>
No proof, no done. A passing check script is not enough. The menu file has to be the concept image, the basement change has to be dressed, and the dev exe has to build. Open the copied backdrop and the readme yourself. Do not claim a playtest. Record what was not checked: the user has not played this new build. The list of what they play is Chopper/Docs/playtest-windows.md.
</verification>

<boundaries>
Do not commit, push, delete, or deploy. Do not start combat, a new character sculpt, or a website gallery. Do not generate Steam images. If Unity is open, stop and say so. If a tool fails, report the error and do the smallest fix.
</boundaries>

<completion_gate>
Stop after the five packages are in the exe, or when blocked. Do not call the game done. Name what is still open: the user playing this build, then W11-11 combat polish, then the website gallery from screenshots the user supplies.
</completion_gate>

<handoff>
End with: what changed in the exe, the build log line, what you looked at, what is still open, and one suggested commit line. No author line.
</handoff>

<start>
Read Reportcard.md and the W11-01, W11-12, W11-14, W11-20 and W11-21 rows. If there is no report-card FAIL, start the menu copy, the E-key and tree-root fix, and the basement and flicker fix together. Do not wait for another playtest.
</start>
