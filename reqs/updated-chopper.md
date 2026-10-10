# Chopper, 2026-10-10

The motions have to be rebuilt entirely. Do not patch the current set again. Do not retarget another pack onto this body and call it fixed. The clips playing now come from four different skeletons, and the body does not survive that.

There is one character. His name is Chopper. He is the lumberjack in the plaid shirt. The character creator is off the main menu in the source. New Game goes to the world name and seed. That change is not in the last Windows player.

Do not use anything extracted from another game. Chopper only uses clips we have a license for: Quaternius, our own Mixamo downloads, and Kevin Iglesias.

## What changed

- The Valheim art direction was retired on 2026-10-09. No new art direction was chosen. Gameplay can stay a survival game. The world, the hut, and the building pieces are still the pixel set. Replacing that art is a later pass.
- Working notes were moved into local folders and kept out of git: `requirements/`, `playtests/`, `prompts/`, `summary/`, `running/`, `tomorrow/`.
- On the morning of 2026-10-08 the real motion packs were deleted and every motion was rewritten by hand in `CodeMotions.cs`. That is the stiff set.
- Later that day six Mixamo clips were put back (idle, walk, run, sprint, swim, treading water). Chopping and fighting stayed hand-keyed.
- On 2026-10-09 those six files were taken out and replaced with Blender copies of Quaternius clips, plus KayKit for mining and fishing. That build was rejected. Those copies are in `SourceAssets/Motions/archive/blender-splits-2026-10-09/` and are not imported.
- The October 8 set was then put back, and that is what the code still plays:
  - Mixamo files in `Assets/ThirdParty/Mixamo_Motions/`: idle, walk, run, sprint, swim, swim idle, attack 1, attack 2, attack 3, death (`down.fbx`).
  - Quaternius Library 1, inside `UAL1_Standard.fbx` (the same file as the skeleton): crouch idle, crouch walk, fall, roll, jab, cross, guard, torch carry.
  - Quaternius Library 2, `UAL2_Standard.fbx`: the tree chop (`TreeChopping_Loop`).
  - Kevin Iglesias Human Animations, male work clips: mining (wall, right hand), fishing cast, fishing wait, fishing fight, fishing catch, gathering, and the ground hammer loop.
  - Hand-keyed, because no licensed clip is the right action: bandage wrap (`heal`), reach over the shoulder to draw or holster (`reach_back`), hard landing.
- The upper-body Chop layer and the Stow layer stay on for the whole game. Their rest state was empty, so Unity wrote the humanoid T-pose over both arms. The legs kept playing. The axe, parented to the hand, stuck straight out. `writeDefaultValues` was turned off on those empty states, and a Windows player was built at 00:09 on 2026-10-10. That player was still wrong.
- After that, still only in source, not in the player: the character creator was removed from the menu, everyone is forced to Chopper's look, and a hammer blow was hooked to placing a piece and to repairing the hut. Unity was open, so that player was never built.

The last player is `Builds/Release/windows/Chopper.exe`, version 0.2.9, written 2026-10-10 00:09. It does not contain the creator removal or the hammer hook.

## How the animations are broken

The visible body is the Quaternius lumberjack: the Superhero body and the modular clothes, driven by the skeleton in `UAL1_Standard.fbx`. The clothes already cut into the body. That is the mesh. A new clip will not close those gaps.

The motions on top of that body are not one performance:

- The Mixamo files were bone-renamed and imported as their own Humanoid avatar, then retargeted onto Chopper. The legs take some of the motion. The arms do not. They sit in the bind pose, or they pump a weapon that this skeleton was not holding. Idle, walk, run, sprint, swim, the three attacks, and the death are all in this broken set.
- Library 1 and Library 2 clips share Chopper's bone names, so they can fit. They are mixed into the same controller as the Mixamo clips, so the body changes skeleton language every time the state changes.
- The Kevin Iglesias clips keep Kevin's bone names. Unity's Humanoid avatar is the only thing lining them up. Mining, fishing, gathering, and hammering depend on that retarget. When it fails, the tool motion is someone else's proportions on this lumberjack.
- The hand-keyed bandage, draw, and hard landing are poses written in code. They are stiff on purpose. They are not a replacement for a real clip.
- An empty override layer on the upper body still owns those bones for the whole session. Turning off default values was not enough. The arms in game were still wrong after the 00:09 player.

The 42 MB Mixamo downloads in `D:\Downloads\Old Downloads` are not Chopper. Each one still contains Mixamo's mesh. Dropping them into `Mixamo_Motions` puts that mesh on the lumberjack. They are not the missing piece.

## Rebuild them entirely

Throw out the mixed controller. One skeleton, one clip source, made for this body.

1. Start from `UAL1_Standard.fbx`. That is Chopper's skeleton. Every new clip is authored on it, or downloaded from Mixamo by uploading that file and taking the clip back in place, without skin, then imported with Chopper's avatar. Do not create a second Humanoid avatar from the animation file.
2. Do not keep the current Mixamo slot files, the Blender splits, or a Kevin clip playing through a foreign avatar. Archive them. They are the broken set.
3. Rebuild the full list in `requirements/requirements-animation.md` (48 player clips, 7 enemy clips) on that one skeleton before wiring the controller again. Locomotion first: idle, walk, run, sprint, crouch, jump, fall, land, roll, swim. Then the axe: chop, draw, holster, the three attacks, block, hit, death. Then tools: mine, fish, gather, hammer, bandage.
4. After the clips exist, rebuild the player animator from those files only, then build a new `Chopper.exe`. The 00:09 player is not a base to increment.

Until that pass exists, do not swap another library into `Mixamo_Motions` and do not ship another player from the current controller.
