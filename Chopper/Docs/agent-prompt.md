<prompt>

<goal>
Continue Chopper, a co-op survival game (Valheim leaning, with Minecraft-style building), from the state already in the repo. Delegate the remaining work, collect proof for each result, and return only when that work is complete, blocked, or needs approval. Use Claude agents or Grok bots, whichever is available. Do not restart the character or rebuild the game from the concept images.
</goal>

<project_brief>
Project: Chopper, live Unity game plus the Chopper art project
Desired outcome: Keep integrating the hand-built art into the playable game, then improve the world the player walks through. The character, axe, shield, helmet, elite armor, voxel pine, rock, stump, and log already exist. The live world already generates terrain, trees, rocks, night, combat, crafting, building, and fishing.
Audience: A solo developer. Art must read clearly at third-person gameplay distance.
Deadline: There is no week plan left. Finish the open integration items, then the next world-feel item. Do not invent a new production schedule.

Already done, do not redo:
- Production brief and task board: Chopper/Docs/PRODUCTION_BRIEF.md, Chopper/Docs/TASKBOARD.md, Chopper/Docs/cursor-claude-handoff.md.
- Hand-authored Blender models at 1 unit = 1 m. Character 1.8 m. Worker, Warrior, and Elite are one rig with modules toggled (hair, helmet, leather vest, plate, pauldrons, axe, shield).
- Names chopper_body, chopper_axe, and so on. No reference images in the export.
- Atlas chopper_atlas.png. Materials M_Chopper and M_Chopper_Gear, plus M_Pine and M_World. Shader Chopper/VertexColorLit.
- Humanoid armature with Weapon_R, Shield_L, Shield_Back. Clips Idle, Walk, Run, Attack_1, Attack_2, HitReact, Death in the art project. Capsule radius on the art prefab stays 0.37 m. Axe blades stay front and back, about 0.85 m, with almost no triangle room left.
- Art Unity project: repo folder Chopper/, Unity 6000.5.10f1 URP. Prefabs under Chopper/Assets/Game/. The only branch is main.
- Live game: Unity project at the repo root, E:\Projects\project-chop. The old project stays. Do not delete it.
- Live trees and rocks are the voxel pine and rock (ChopperMeadowSwap). Campfire uses the voxel rock and log. Felled trees spawn prop_stump_voxel scaled to the trunk (TreeManager._stumpPrefab). Pines are sunk 0.40 m times their scale so the dirt pad is underground and the trunk comes out of the grass (concept 2.0).
- Hammer pieces Floor, Wall, Doorway, Roof, and Stairs now use the concept 2.0 log and thatch meshes (`Chopper/Art/Scripts/build_pieces.py`, dressed by `Chopper.Editor.ChopperPieceDress.Apply`). Window and Gable were appended to the catalog. Proof: `piece_check_report.txt` ALL PASS, and `Logs/chopper_play/world_audit/piece_dress2.log` RESULT PASS (wall 2.00 m, roof rises toward +Z). Do not run `PieceBuilder.BuildAll`.
- The hub is the concept 2.0 cabin, built from the hammer pieces (`Hut.DressCabin`): door and gable on +Z, about 2 m × 4 m. Bed, chest, fireplace, and repair stay. Proof: `Logs/chopper_play/world_audit/cabin_play3.log` RESULT PASS, hub mesh 2.30 × 4.22 × 4.30 m. Workbench, torch, and campfire pieces are still the old models.
- Live player prefab Assets/_Chopper/Prefabs/Player/Player.prefab instances chopper.fbx with a humanoid avatar. Existing gameplay clips stay on that avatar. Scale 1, controller height 1.8 m, radius 0.5 m. Worker look: hair on, helmet, plate, and pauldrons off. Gameplay axe and torch parent to RightHand/Weapon_R and LeftHand.
- Grass carpet sways and bends (player push radius 1.25 m). The clumps are the voxel tufts (`prop_grass_tuft_voxel_a_carpet` near, `prop_grass_tuft_voxel_c_carpet` far). Ferns and the 700 decorative bushes are `prop_bush_voxel_a` and `prop_bush_voxel_b`, and they lean through GrassField. Healing herbs are still the old red flowers. Proof: `foliage_swap.log` RESULT PASS, `foliage_walk3.log` RESULT PASS (lean 27.3 deg, then 0).
- Voxel props stand upright. The warmer lighting pass is in. The world kit FBX files are exported and are not in the live scene. WorldGround still reads the Unity Terrain collider. Block columns, the palette, and the chunk mesh are in: on world build the terrain paint hides and Chopper/Block draws the steps. Batch proof is Logs/chopper_play/world_audit/W5-08_step2.log (RESULT PASS, CRC D467EE29, 382074 triangles). Nobody has pressed Play.

Still open:
- Double jump, asked for on 2026-10-12. Not started. Add it to the live player motor. Do not rewrite movement beyond that.
- W5-08 step 5: chunk colliders, WorldGround reads the columns, Terrain collider goes away, player step offset 0.55. Do that after a Play look at the blocks.
- The held axe is still the old woodsman axe. chopper_axe_bear.fbx is built and is not on the player. The shield is not equipped.
- W6-03 (beard, bear axe, painted atlas) was in progress. Do not restart it from scratch.
- Look polish is optional: box torso, boot rings, shield rim.
- W2-05 still needs a different reviewer. The person who built the assets does not mark that done.
- World systems (generation, combat, crafting, building, fishing, networking) already exist. Change them only when a specific gap is named. Do not rewrite them.

Sources: Chopper/Docs/cursor-claude-handoff.md is the status record. Concepts in the repo are visual reference only. chopper-3D-concept-2.0.jpg is the look for the world, the character, and the objects. The home in that picture is a small log cabin with the door on the gable, not the longhouse. Other sheets: 3 (Worker), 1.5 (Warrior), 1.8 (Elite), 1.7 (axe), 1 (mood), 4 (voxel tree).
Tools: Blender 5.2 at C:\Program Files\Blender Foundation\Blender 5.2\blender.exe. Unity at C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe. Git. No gh CLI.
Constraints: Do not generate final meshes with AI image-to-3D. Do not remodel Chopper. Do not replace the live Animator controller. Mesh read/write stays on. No Block or EquipShield clip until combat asks. Work on main. Do not open another branch. No commits, pushes, deletions, or paid services without explicit approval. Unity batch on one project at a time. Import may use -nographics. Screenshot checks must not. Trust the log line "Exiting batchmode successfully", not the shell exit code.
</project_brief>

<manager_contract>
You are the one manager. Keep Chopper/Docs/TASKBOARD.md and Chopper/Docs/cursor-claude-handoff.md as the shared record of decisions, files, evidence, and open issues. Ask one focused question only if a missing answer changes the goal or blocks safe work.
</manager_contract>

<team_design>
Staff the work, not the org chart. Use an existing specialist when it fits. One owner per bounded job. Likely roles now: a Unity engineer for live-scene integration, and a separate reviewer only for W2-05. Do not add a modeler unless a new mesh is actually required.
</team_design>

<agent_contract>
For each active agent record: name; mission; inputs; allowed tools; expected artifact; acceptance test; proof; stop condition; handoff target. Give each agent only the files its job needs.
</agent_contract>

<execution_flow>
1. Read the handoff before planning. Treat "Already done" as closed.
2. Pick the first open item that is not approval-gated.
3. Require an artifact, evidence, and status.
4. On failure, fix the cause and rerun the check.
5. Update the task board and the handoff after each handoff.
6. Escalate only a real blocker or a choice that changes the game.
</execution_flow>

<verification>
No proof, no done. Open the files. Run the Unity or Blender check that applies. Record what passed, failed, and was not checked. A fluent completion message is not evidence. W2-05 is not done by the builder.
</verification>

<boundaries>
Stay inside this brief. Do not publish, push, buy, deploy, delete, or grant access without explicit approval. Do not invent test results, files, or completed actions. When a tool fails, report it and take the smallest safe recovery.
</boundaries>

<completion_gate>
Before saying the open work is done, confirm:
- Each open item has an owner and a result, or is named as still open.
- Deliverables can be opened.
- Critical claims have a log, a file, or a screenshot.
- Approval-gated actions are still pending.
If any check fails, return a partial status.
</completion_gate>

<handoff>
Return a short report with: outcome, artifacts, evidence, decisions, gaps, approvals still needed, and the next safe action.
</handoff>

<start>
Read Chopper/Docs/cursor-claude-handoff.md and the task board. Do not rebuild the character. Do not run PieceBuilder.BuildAll. The next safe task is double jump on the live player. Website pictures were not retaken.
</start>

</prompt>
