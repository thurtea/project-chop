Continue Chopper, a Valheim-style co-op survival game in Unity 6000.5.10f1 (URP, FishNet), in the private repo at E:\Projects\3D-Game (the game is in the Chopper folder). Read Chopper/summary.md first, then Chopper/cursor-prompt.md, then Chopper/requirements.md if the work touches art. My playtest build is Chopper/Builds/Release/windows/Chopper.exe: rebuild it (release) at the end of every session that changes the game, and make sure Chopper.exe itself gets a new timestamp, not only Chopper_Data. Do not rebuild the zips unless I ask. The demo is not on Steam yet; I will buy the Steam access once fishing, building, crafting and the night creatures are done.

Style: no emojis and no em dashes, in docs, code comments, commit messages and in-game text. Minimal UI, Valheim-style prompts. No amateur stand-ins (no props from primitives, no words in colored squares as icons); if one cannot be avoided, list it in requirements.md in the same change.

Standing rules:

- Act. Do not ask me to run things. Never copy the game folder anywhere: no shadow copies (I had them deleted on 2026-10-06). Run Unity in batch mode on E:\Projects\3D-Game\Chopper itself (NetworkScenesBuilder.Build after asset or setup changes, then the player build). If my editor is open on it, tell me once and wait for me to close it.
- Every MonoBehaviour or NetworkBehaviour must be alone in a file named after its class.
- Player runs never touch my real saves or settings: reg export HKCU\Software\DefaultCompany\Chopper before; after, reg delete that key and then reg import (an import alone leaves values a test added, like the creator's Chopper.Appearance2); pass -chopperTestSaves (and -chopperNoSteam).
- Verify with the EditMode tests (expect 291) and the smoke test (-chopperHost -chopperSmoke, headless, 323 checks), and look at tour screenshots before calling visual work done. Tours: -chopperTitle <folder> -chopperCreatorTour (every outfit and look change), -chopperHold (hand close ups standing, running and chopping, the holster in slow motion), -chopperLook (held items and collar close up), -chopperAxeRun (axe flips while walking and sprinting, expect 0), -chopperHouse, -chopperInventory, -chopperFishing, -chopperMine, -chopperCrypt, -chopperCombat (forest bosses are shots 7 and 8), -chopperWeather (15 checks), -chopperPadHotbar (pad checks), -chopperSite (website pictures).
- Raw downloads go in Chopper/SourceAssets (not in git, not in Assets). Blender 5.2 is installed for converting models.
- Git: the only git command you run is git add . from E:\Projects\3D-Game. I commit and push. At the end, propose a short commit message.

Where things stand (v0.2.9, 2026-10-07)

- Character creator is in: New Game now opens it first (then the world page), and Character on the start screen, six outfits (Chopper's plaid is the default), hair, beard, hair color, shirt color and skin, synced to every player (PlayerAppearance, AppearanceData saved under Chopper.Appearance2). Each outfit has its own cut of the body. The Mixamo monk is out.
- The axe sits at his side, Valheim style: head forward and a little down, fingers round the handle, swinging with the arm when running. Fixed 2026-10-07: the whole character (player, draugr, golems) faced backward, because FacingCorrectionYaw turned the humanoid model 180 degrees; humanoid models now get no turn. PlayerAxe solves the carry at runtime (first frame in hand, then while standing). -chopperHold logs [Facing] (toes and hips about 0,0,1) and [Grip] (stand: head 0.13, -0.31, 0.94). -chopperAxeRun reported 0 flips. Holster and draw reach over the shoulder. The model is Stylized Woodcutting Axe by Price (OpenGameArt, CC0).
- Mining is a pastime like fishing, with little weight on the rest of the game. One mine per forest (Drip, Cinder, Deep Root, High Gallery), a timber portal in a ridge, chambers and veins below. The muted CC0 pickaxe is on the hotbar. Eighteen finds are carried, chested and saved after the fish. A glowworm is bait; a catch can bring up a geode or an old pick head. Tab has a Mining page. Tour: -chopperMine. Trees in front of a portal are moved, never removed, so saves keep their felled trees.
- Building snaps corner to corner (BuildRules.SnapMoves, BuildManager.SnapToPieces). The house tour placed every piece aimed up to 0.6 m off exactly on its edge. The hut joints were clean.
- Night creatures stay the wood-creature stand-in until the Elderwood Guardian arrives.
- Website pictures were retaken earlier today. Not published; publish only when I say.
- prompt-linux.md and prompt-macos.md expect 291 EditMode tests. Confirm that count on Windows first.

Not done, and first:

1. 2026-10-08 (late): my own Mixamo motions are in. Raw files in SourceAssets/Motions/raw, converted (Tools/mixamo_convert.py, skeleton only) in SourceAssets/Motions/converted, slot files in Assets/ThirdParty/Mixamo_Motions named as requirements.md USER 1 (idle, walk, run, sprint, crouch_walk, swim, swim_idle, fall, death, land_hard, attack_1 to 3, trimmed to each swing in CharacterSetup.SlotTrim). A slot without a file keeps the Quaternius clip. Mining, fishing (cast, wait, fight, pull out) and gathering use the Kevin Iglesias Human Crafting Animations in Assets/Kevin Iglesias; fishing and gathering motions show only on the fisher's own machine so far. Building is Minecraft style now: pieces may float, nothing collapses. Fall damage: safe to 4 m, deadly at 20 m, water breaks a fall, with a hard landing motion. Axe facing unchanged (stand head 0.13, -0.31, 0.94). EditMode 293 of 293, smoke PASS. Next: my verdict on each motion; swap fast when I say "try another" (spares in SourceAssets/Motions/converted). Then sync the fishing stage so others see it, and put the rod and hammer in the hand.
2. Whatever I report from playing that build: the creator, the axe at the side, the holster, a mine, and snapping a wall onto a floor.
3. The hammer while building. The muted CC0 hammer is already at Assets/ThirdParty/OGA_LowPolyTools/Hammer.fbx and is not in the hand. Then the fishing rod and float as a model (requirements.md USER 4).
4. The results from my Linux and Mac machines (prompt-linux.md, prompt-macos.md).
5. Skills that level with use: logging, mining, foraging and combat, shown in the Character page.
6. Enemy health bars once a fight starts, and floating damage numbers.
7. When I hand over models (the Elderwood Guardian, boss treants), import, recolor per element and swap them in.

Do not redo the shadow-copy deletion, the character creator, the axe flip fix, the mines, the find icons, or the corner snapping.

Update and run the EditMode tests and the smoke test. At the end, rewrite Chopper/tomorrow.md as a prompt like this one for the next session, and propose a commit message.
