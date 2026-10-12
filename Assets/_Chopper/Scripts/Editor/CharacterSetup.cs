using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chopper.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Chopper.Editor
{
    // Builds the player character: the Quaternius mannequin from the Universal Animation Library with an
    // Animator (locomotion blend tree, jump, down, and a chop on an upper body layer) and the woodcutting
    // axe attached to the right hand. Called from NetworkScenesBuilder when it makes the Player prefab.
    public static class CharacterSetup
    {
        public const string ModelPath = "Assets/ThirdParty/Quaternius_UniversalAnimationLibrary/UAL1_Standard.fbx";
        public const string ChopperModelPath = "Assets/_Chopper/Art/Character/chopper.fbx";
        public const string ChopperAtlasPath = "Assets/_Chopper/Art/Character/chopper_atlas.png";
        // The second library, on the same skeleton: tree chopping, sword combos and block, eating, chests, the bed.
        public const string ModelPath2 = "Assets/ThirdParty/Quaternius_UniversalAnimationLibrary2/UAL2_Standard.fbx";

        // A woodsman's axe (CC0, OpenGameArt), straight handle and one blade. The Quaternius battle axe it replaced sat
        // badly in the fist.
        internal const string AxePath = "Assets/ThirdParty/OGA_WoodcuttingAxe/WoodcuttingAxe.fbx";
        // The pickaxe (Stylized Low Poly Tools, CC0, recolored), out only while working a vein.
        internal const string PickaxePath = "Assets/ThirdParty/OGA_LowPolyTools/Pickaxe.fbx";
        private const string ControllerPath = "Assets/_Chopper/Prefabs/Player/PlayerAnimator.controller";
        private const string MaskPath = "Assets/_Chopper/Prefabs/Player/UpperBody.mask";
        private const string StowMaskPath = "Assets/_Chopper/Prefabs/Player/Stow.mask";
        private const string GripMaskPath = "Assets/_Chopper/Prefabs/Player/Grip.mask";
        private const string FistClipPath = "Assets/_Chopper/Prefabs/Player/Fist.anim";
        public const string GripLayerName = "Grip";
        public const string GripLeftLayerName = "GripLeft";
        public const string CarryRightLayerName = "CarryRight";
        public const string CarryLeftLayerName = "CarryLeft";
        private const string FistLeftClipPath = "Assets/_Chopper/Prefabs/Player/FistLeft.anim";
        private const string GripLeftMaskPath = "Assets/_Chopper/Prefabs/Player/GripLeft.mask";
        private const string CarryRightMaskPath = "Assets/_Chopper/Prefabs/Player/CarryRight.mask";
        private const string CarryLeftMaskPath = "Assets/_Chopper/Prefabs/Player/CarryLeft.mask";
        // Humanoid finger muscles for a fist round a handle: -1 is fully curled. The thumb wraps less.
        private const float FingerCurl = -0.95f;
        private const float ThumbCurl = -0.45f;

        private const string ClipPrefix = "Armature|";
        private const string LoopSuffix = "_Loop";
        private const string RightHand = "hand_r";
        private const string RightMiddleFinger = "middle_01_r";
        private const string UpperBodyRoot = "spine_01";
        private const string LeftFoot = "foot_l";
        private const string LeftToe = "ball_l";

        // Where each loop sits on the Speed blend, in meters per second: the player's real speeds (run 6, 5.5 with the axe
        // drawn; sprint 9.5, 8.7 with the axe; crouch 2.6). The walk only shows while speeding up or slowing down.
        private const float WalkBlendSpeed = 1.0f;
        private const float JogBlendSpeed = 5.5f;
        public const float SprintBlendSpeed = 8.7f;
        public const float CrouchWalkBlendSpeed = 2.6f;
        // How fast each clip's planted foot travels when played at normal speed, on Chopper's body (the -chopperMotion tour
        // logs it in the game as "moves back under the body"). Each loop plays at blend speed / natural speed, so the feet
        // keep pace with the ground. Mixamo clips: the file's own travel times 0.87 for Chopper's smaller frame.
        private const float WalkNaturalSpeed = 0.94f;
        private const float JogNaturalSpeed = 5.5f;
        private const float SprintNaturalSpeed = 5.5f;
        private static readonly Dictionary<string, float> SlotNaturalSpeed = new Dictionary<string, float>
        {
            { "walk", 1.48f }, { "run", 4.9f }, { "sprint", 4.7f }, { "crouch_walk", 1.46f },
        };
        // Clips the user chose (Mixamo, 2026-10-08), one file per slot in MixamoFolder, named as requirements.md USER 1
        // lists them. A slot without a file keeps the library's clip.
        internal const string CrouchWalkPath = MixamoFolder + "/crouch_walk.fbx";
        // One shots (not loops), and the frames kept from long files: the swing from its wind up to its recovery.
        private static readonly Dictionary<string, (int first, int last)> SlotTrim = new Dictionary<string, (int first, int last)>
        {
            { "attack_1", (12, 45) }, { "attack_2", (15, 45) }, { "attack_3", (75, 108) },
        };
        private static readonly HashSet<string> OneShotSlots = new HashSet<string> { "attack_1", "attack_2", "attack_3", "death", "land_hard", "fall" };
        // The Kevin Iglesias Human Crafting Animations (Asset Store, free): mining, fishing and gathering.
        private const string CraftingFolder = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Work";
        private const string CrouchIdleClipPath = "Assets/_Chopper/Prefabs/Player/CrouchIdle.anim";
        private const float CrouchIdleFrameTime = 0f;
        private const float DefaultSwingSpeed = 2.6f;
        private const float SwingExitTime = 0.8f;
        private const float ChopExitTime = 0.52f;
        // Where along the handle the hand grips, as a fraction of the axe length from the handle end.
        private const float GripFraction = 0.2f;
        // The lower part of the axe that is only handle, used to find the handle's center line.
        private const float HandleSampleFraction = 0.45f;

        // How far the handle tips from straight across the palm toward the fingertips (a real hammer grip runs
        // diagonally, about 30 degrees).
        private const float GripDiagonal = 0.35f;
        private const float PalmAlongHand = 0.5f;
        // Chopper is shorter and broader than the 1.9 m mannequin used by the animation library.
        public const float ChopperScale = 0.88f;
        // Kept uniform: a Humanoid rig under a stretched root skews the limbs.
        public const float ChopperBreadth = 1f;
        // Keep the axe proportional to Chopper's smaller frame.
        public static float AxeScaleMultiplier = 1.0f;

        public class Result
        {
            public Animator Animator;
            public Transform AxeGrip;
            public Renderer AxeRenderer;
            public GameObject TorchVisual;
            public CharacterOutfitter Outfitter;
            public Transform PickaxeGrip;
        }

        // ---------- Imports ----------

        // The FBX ships every clip non-looping. Locomotion and idle clips need to loop.
        public static void PrepareClips()
        {
            PrepareClips(ModelPath);
            PrepareClips(ModelPath2);
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MixamoFolder }))
                PrepareMixamo(AssetDatabase.GUIDToAssetPath(guid));
        }

        // Mixamo clips (skeleton only, bones renamed to the library's names by Tools/mixamo_convert.py): Humanoid, one
        // looping clip named after the file, height and turn kept in the pose so the body never drifts or spins; the
        // forward travel is root motion, which the player's Animator ignores, so the clip plays in place.
        internal const string MixamoFolder = "Assets/ThirdParty/Mixamo_Motions";

        private static void PrepareMixamo(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            string name = Path.GetFileNameWithoutExtension(path);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            bool loop = !OneShotSlots.Contains(name);
            bool trimmed = SlotTrim.TryGetValue(name, out var trim);
            if (importer.animationType == ModelImporterAnimationType.Human && clips != null && clips.Length == 1 && clips[0].name == name
                && !clips[0].keepOriginalOrientation && clips[0].loopTime == loop
                && (!trimmed || (Mathf.RoundToInt(clips[0].firstFrame) == trim.first && Mathf.RoundToInt(clips[0].lastFrame) == trim.last))) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.SaveAndReimport();
            ModelImporterClipAnimation clip = importer.defaultClipAnimations[0];
            clip.name = name;
            clip.loopTime = loop;
            if (trimmed)
            {
                clip.firstFrame = trim.first;
                clip.lastFrame = trim.last;
            }
            clip.lockRootRotation = true;
            // Facing from the body (hips and shoulders), not the file's root: Mixamo's root is not lined up with it.
            clip.keepOriginalOrientation = false;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = false;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        // The user's clip for a slot when its file is there, otherwise the library's clip.
        internal static AnimationClip Slot(string slot, string fallback)
        {
            AnimationClip clip = MotionClip($"{MixamoFolder}/{slot}.fbx");
            return clip != null ? clip : FindClip(fallback);
        }

        private static bool HasSlot(string slot) => File.Exists($"{MixamoFolder}/{slot}.fbx");

        // A loop on the Speed blend at its natural pace: the user's clip with its own speed, or the library's.
        private static void AddSlotLoop(BlendTree tree, string slot, string fallback, float speed, float fallbackNatural)
        {
            bool own = HasSlot(slot);
            AddLoop(tree, Slot(slot, fallback), speed, own && SlotNaturalSpeed.TryGetValue(slot, out float natural) ? natural : fallbackNatural);
        }

        private static AnimationClip CraftingClip(string file) => MotionClip($"{CraftingFolder}/{file}.fbx");

        // The first real clip in a motion file (Unity adds a hidden preview clip to some).
        internal static AnimationClip MotionClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static void PrepareClips(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            bool changed = false;
            // Humanoid, so every library's clips play on any humanoid body.
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool shouldLoop = clip.name.EndsWith(LoopSuffix) || clip.name.EndsWith("Sword_Idle");
                if (clip.loopTime == shouldLoop) continue;
                clip.loopTime = shouldLoop;
                changed = true;
            }
            if (!changed) return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        internal static AnimationClip FindClip(string shortName)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == ClipPrefix + shortName);
            if (clip != null) return clip;
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath2).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == ClipPrefix + shortName);
        }

        private static void AddLoop(BlendTree tree, AnimationClip clip, float speed, float naturalSpeed)
        {
            tree.AddChild(clip, speed);
            ChildMotion[] children = tree.children;
            children[children.Length - 1].timeScale = naturalSpeed > 0f ? speed / naturalSpeed : 1f;
            tree.children = children;
        }

        // A still pose: every curve of the clip held at one moment.
        private static AnimationClip BuildPoseClip(AnimationClip source, float time, string path)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { frameRate = source.frameRate, name = Path.GetFileNameWithoutExtension(path) };
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                float value = AnimationUtility.GetEditorCurve(source, binding).Evaluate(time);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, value));
            }
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = true;
            settings.startTime = 0f;
            settings.stopTime = 1f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---------- Controller ----------

        public static AnimatorController BuildController(Transform modelRootForMask)
        {
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Down", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Swing", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Punch", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Punch2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Swing2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Swing3", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("RollSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Blocking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Swimming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("SwingSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Heal", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Chop", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Stow", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("StrideRate", AnimatorControllerParameterType.Float);
            controller.AddParameter("LandHard", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Mine", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Gather", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("FishStage", AnimatorControllerParameterType.Int);
            controller.AddParameter("FishCast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("FishCatch", AnimatorControllerParameterType.Trigger);
            controller.parameters = controller.parameters.Select(p =>
            {
                if (p.name == "Grounded") p.defaultBool = true;
                if (p.name == "SwingSpeed") p.defaultFloat = DefaultSwingSpeed;
                if (p.name == "RollSpeed") p.defaultFloat = 2.9f;
                if (p.name == "StrideRate") p.defaultFloat = 1f;
                return p;
            }).ToArray();

            AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            // One library for every step (mixing sources made the gait change style between speeds).
            tree.AddChild(Slot("idle", "Idle_Loop"), 0f);
            AddSlotLoop(tree, "walk", "Walk_Loop", WalkBlendSpeed, WalkNaturalSpeed);
            AddSlotLoop(tree, "run", "Jog_Fwd_Loop", JogBlendSpeed, JogNaturalSpeed);
            AddSlotLoop(tree, "sprint", "Sprint_Loop", SprintBlendSpeed, SprintNaturalSpeed);
            // Past the top of the blend (sprinting without the axe) the whole state plays faster to keep pace.
            locomotion.speedParameterActive = true;
            locomotion.speedParameter = "StrideRate";
            baseMachine.defaultState = locomotion;

            AnimatorState crouch = controller.CreateBlendTreeInController("Crouch", out BlendTree crouchTree);
            crouchTree.blendType = BlendTreeType.Simple1D;
            crouchTree.blendParameter = "Speed";
            crouchTree.useAutomaticThresholds = false;
            AnimationClip crouchWalk = MotionClip(CrouchWalkPath);
            if (crouchWalk != null)
            {
                crouchTree.AddChild(BuildPoseClip(crouchWalk, CrouchIdleFrameTime, CrouchIdleClipPath), 0f);
                AddLoop(crouchTree, crouchWalk, CrouchWalkBlendSpeed, SlotNaturalSpeed["crouch_walk"]);
            }
            else
            {
                Debug.LogWarning($"[CharacterSetup] {CrouchWalkPath} missing; using the library's crouch.");
                crouchTree.AddChild(FindClip("Crouch_Idle_Loop"), 0f);
                crouchTree.AddChild(FindClip("Crouch_Fwd_Loop"), CrouchWalkBlendSpeed);
            }
            AnimatorStateTransition toCrouch = locomotion.AddTransition(crouch);
            toCrouch.hasExitTime = false;
            toCrouch.duration = 0.12f;
            toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "Crouching");
            AnimatorStateTransition fromCrouch = crouch.AddTransition(locomotion);
            fromCrouch.hasExitTime = false;
            fromCrouch.duration = 0.15f;
            fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");

            AnimatorState jump = baseMachine.AddState("Jump");
            jump.motion = Slot("fall", "Jump_Loop");
            AnimatorState down = baseMachine.AddState("Down");
            down.motion = Slot("death", "Death01");

            AnimatorStateTransition toJump = locomotion.AddTransition(jump);
            toJump.hasExitTime = false;
            toJump.duration = 0.08f;
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            AnimatorStateTransition fromJump = jump.AddTransition(locomotion);
            fromJump.hasExitTime = false;
            fromJump.duration = 0.12f;
            fromJump.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            AnimatorStateTransition toDown = baseMachine.AddAnyStateTransition(down);
            toDown.hasExitTime = false;
            toDown.duration = 0.1f;
            toDown.canTransitionToSelf = false;
            toDown.AddCondition(AnimatorConditionMode.If, 0f, "Down");
            AnimatorStateTransition fromDown = down.AddTransition(locomotion);
            fromDown.hasExitTime = false;
            fromDown.duration = 0.2f;
            fromDown.AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");

            // A hard landing (a fall long enough to hurt): thrown flat and back up.
            AnimationClip landClip = Slot("land_hard", "Hit_Knockback");
            if (landClip != null)
            {
                AnimatorState land = baseMachine.AddState("LandHard");
                land.motion = landClip;
                AnimatorStateTransition toLand = baseMachine.AddAnyStateTransition(land);
                toLand.hasExitTime = false;
                toLand.duration = 0.05f;
                toLand.canTransitionToSelf = false;
                toLand.AddCondition(AnimatorConditionMode.If, 0f, "LandHard");
                toLand.AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");
                AnimatorStateTransition fromLand = land.AddTransition(locomotion);
                fromLand.hasExitTime = true;
                fromLand.exitTime = 0.9f;
                fromLand.duration = 0.15f;
            }

            // Dodge roll: a full body clip, sped up to fit the roll time.
            AnimatorState roll = baseMachine.AddState("Roll");
            roll.motion = FindClip("Roll");
            roll.speedParameterActive = true;
            roll.speedParameter = "RollSpeed";
            AnimatorStateTransition toRoll = baseMachine.AddAnyStateTransition(roll);
            toRoll.hasExitTime = false;
            toRoll.duration = 0.05f;
            toRoll.canTransitionToSelf = false;
            toRoll.AddCondition(AnimatorConditionMode.If, 0f, "Roll");
            toRoll.AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");
            AnimatorStateTransition fromRoll = roll.AddTransition(locomotion);
            fromRoll.hasExitTime = true;
            fromRoll.exitTime = 0.92f;
            fromRoll.duration = 0.12f;

            // Swimming: idle and forward strokes, entered from anywhere while in deep water.
            AnimatorState swim = controller.CreateBlendTreeInController("Swim", out BlendTree swimTree);
            swimTree.blendType = BlendTreeType.Simple1D;
            swimTree.blendParameter = "Speed";
            swimTree.useAutomaticThresholds = false;
            swimTree.AddChild(Slot("swim_idle", "Swim_Idle_Loop"), 0f);
            // The Mixamo stroke is slow (4.5 s for its strokes); a little quicker reads as swimming at the swim speed.
            AddLoop(swimTree, Slot("swim", "Swim_Fwd_Loop"), 2.4f, HasSlot("swim") ? 1.6f : 2.4f);
            AnimatorStateTransition toSwim = baseMachine.AddAnyStateTransition(swim);
            toSwim.hasExitTime = false;
            toSwim.duration = 0.2f;
            toSwim.canTransitionToSelf = false;
            toSwim.AddCondition(AnimatorConditionMode.If, 0f, "Swimming");
            toSwim.AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");
            AnimatorStateTransition fromSwim = swim.AddTransition(locomotion);
            fromSwim.hasExitTime = false;
            fromSwim.duration = 0.25f;
            fromSwim.AddCondition(AnimatorConditionMode.IfNot, 0f, "Swimming");

            AddCrouchArmsLayer(controller);
            AddWorkLayer(controller);
            AddChopLayer(controller, modelRootForMask);
            ValidateMotions(controller);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // Fishing and gathering, the whole body (the player stands still for both): the Kevin Iglesias crafting clips.
        // FishStage 0 is off, 1 the rod out or the line in the water, 2 a fish on the line; FishCast and FishCatch play
        // the cast and the pull out once; Gather bends down to pick a plant.
        private static void AddWorkLayer(AnimatorController controller)
        {
            AnimationClip wait = CraftingClip("Fishing/HumanM@Fishing01 - Loop");
            AnimationClip cast = CraftingClip("Fishing/HumanM@FishingThrow01");
            AnimationClip fight = CraftingClip("Fishing/HumanM@FishingFighting01");
            AnimationClip pull = CraftingClip("Fishing/HumanM@FishingPullOut01");
            AnimationClip gather = CraftingClip("Gathering/HumanM@Gathering01");
            if (wait == null || cast == null || fight == null || pull == null || gather == null)
            {
                Debug.LogWarning($"[CharacterSetup] Crafting clips missing from {CraftingFolder}; no fishing or gathering motions.");
                return;
            }
            var machine = new AnimatorStateMachine { name = "Work" };
            AssetDatabase.AddObjectToAsset(machine, controller);
            AnimatorState off = machine.AddState("Empty");
            off.writeDefaultValues = false; // an Empty humanoid state that writes defaults shows the T-pose
            AnimatorState waiting = machine.AddState("FishWait");
            waiting.motion = wait;
            AnimatorState casting = machine.AddState("FishCast");
            casting.motion = cast;
            AnimatorState fighting = machine.AddState("FishFight");
            fighting.motion = fight;
            AnimatorState pulling = machine.AddState("FishCatch");
            pulling.motion = pull;
            AnimatorState gathering = machine.AddState("Gather");
            gathering.motion = gather;
            machine.defaultState = off;

            AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration)
            {
                AnimatorStateTransition t = from.AddTransition(to);
                t.hasExitTime = false;
                t.duration = duration;
                return t;
            }
            AnimatorStateTransition Finish(AnimatorState from, AnimatorState to)
            {
                AnimatorStateTransition t = from.AddTransition(to);
                t.hasExitTime = true;
                t.exitTime = 0.9f;
                t.duration = 0.2f;
                return t;
            }
            Link(off, waiting, 0.2f).AddCondition(AnimatorConditionMode.Greater, 0f, "FishStage");
            foreach (AnimatorState state in new[] { waiting, fighting })
                Link(state, off, 0.25f).AddCondition(AnimatorConditionMode.Equals, 0f, "FishStage");
            Link(waiting, fighting, 0.2f).AddCondition(AnimatorConditionMode.Equals, 2f, "FishStage");
            Link(fighting, waiting, 0.25f).AddCondition(AnimatorConditionMode.Equals, 1f, "FishStage");
            foreach (AnimatorState state in new[] { off, waiting })
                Link(state, casting, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "FishCast");
            Finish(casting, waiting);
            foreach (AnimatorState state in new[] { waiting, fighting })
                Link(state, pulling, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "FishCatch");
            Finish(pulling, off);
            Link(off, gathering, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "Gather");
            Finish(gathering, off);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Work",
                stateMachine = machine,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override
            });
        }

        // The crouch walk sneaks with both arms reaching out in front, which lifted the axe to the face. While crouching
        // the arms hang relaxed instead (the idle's arms); the legs and back keep the sneak. Swings, the guard and the
        // holster are later layers and still take the arms.
        private const string CrouchArmsMaskPath = "Assets/_Chopper/Prefabs/Player/CrouchArms.mask";

        private static void AddCrouchArmsLayer(AnimatorController controller)
        {
            var machine = new AnimatorStateMachine { name = "CrouchArms" };
            AssetDatabase.AddObjectToAsset(machine, controller);
            AnimatorState off = machine.AddState("Empty");
            off.writeDefaultValues = false; // an Empty humanoid state that writes defaults shows the T-pose
            AnimatorState arms = machine.AddState("Arms");
            arms.motion = FindClip("Idle_Loop");
            machine.defaultState = off;
            AnimatorStateTransition down = off.AddTransition(arms);
            down.hasExitTime = false;
            down.duration = 0.12f;
            down.AddCondition(AnimatorConditionMode.If, 0f, "Crouching");
            AnimatorStateTransition up = arms.AddTransition(off);
            up.hasExitTime = false;
            up.duration = 0.15f;
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "CrouchArms",
                stateMachine = machine,
                avatarMask = BuildHumanMask(CrouchArmsMaskPath, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm),
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override
            });
        }

        // A missing clip leaves a state empty and the model in its T-pose, so fail loudly at build time.
        private static void ValidateMotions(AnimatorController controller)
        {
            string[] required =
            {
                "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Crouch_Idle_Loop", "Crouch_Fwd_Loop",
                "Jump_Loop", "Death01", "Sword_Attack", "Punch_Jab", "Punch_Cross", "Roll", "Swim_Idle_Loop", "Swim_Fwd_Loop", "Sword_Idle",
                "TreeChopping_Loop", "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C", "OverhandThrow"
            };
            var missing = required.Where(name => FindClip(name) == null).ToList();
            if (missing.Count > 0) throw new System.InvalidOperationException("Player animation clips missing from " + ModelPath + ": " + string.Join(", ", missing));
        }

        private static void AddChopLayer(AnimatorController controller, Transform modelRoot)
        {
            AvatarMask mask = BuildUpperBodyMask(modelRoot);

            var machine = new AnimatorStateMachine { name = "Chop" };
            AssetDatabase.AddObjectToAsset(machine, controller);
            AnimatorState empty = machine.AddState("Empty");
            empty.writeDefaultValues = false; // an Empty humanoid state that writes defaults shows the T-pose
            // The axe in a fight: the second library's sword combo (A, B, then the C finisher). On a tree: a real chop.
            AnimatorState swing = machine.AddState("Swing");
            swing.motion = Slot("attack_1", "Sword_Regular_A");
            swing.speedParameterActive = true;
            swing.speedParameter = "SwingSpeed";
            machine.defaultState = empty;

            // Bare handed: a jab until the axe is taken from its stand.
            AnimatorState punch = machine.AddState("Punch");
            punch.motion = FindClip("Punch_Jab");
            punch.speedParameterActive = true;
            punch.speedParameter = "SwingSpeed";
            AnimatorStateTransition punchStart = empty.AddTransition(punch);
            punchStart.hasExitTime = false;
            punchStart.duration = 0.05f;
            punchStart.AddCondition(AnimatorConditionMode.If, 0f, "Punch");
            AnimatorStateTransition punchAgain = punch.AddTransition(punch);
            punchAgain.hasExitTime = false;
            punchAgain.duration = 0.05f;
            punchAgain.canTransitionToSelf = true;
            punchAgain.AddCondition(AnimatorConditionMode.If, 0f, "Punch");
            AnimatorStateTransition punchFinish = punch.AddTransition(empty);
            punchFinish.hasExitTime = true;
            punchFinish.exitTime = SwingExitTime;
            punchFinish.duration = 0.1f;

            // Heal: wrapping a bandage, played on the upper body so the player can keep walking.
            AnimatorState heal = machine.AddState("Heal");
            // The library has no bandage clip; the pistol reload brings both hands together in front of the chest.
            heal.motion = FindClip("Pistol_Reload");
            AnimatorStateTransition healStart = empty.AddTransition(heal);
            healStart.hasExitTime = false;
            healStart.duration = 0.1f;
            healStart.AddCondition(AnimatorConditionMode.If, 0f, "Heal");
            AnimatorStateTransition healEnd = heal.AddTransition(empty);
            healEnd.hasExitTime = true;
            healEnd.exitTime = 0.9f;
            healEnd.duration = 0.15f;

            // Guard: the upper body holds a sword guard pose while blocking; GuardPose then raises the axe across the body.
            AnimatorState guard = machine.AddState("Guard");
            guard.motion = FindClip("Sword_Idle");
            AnimatorStateTransition guardStart = empty.AddTransition(guard);
            guardStart.hasExitTime = false;
            guardStart.duration = 0.08f;
            guardStart.AddCondition(AnimatorConditionMode.If, 0f, "Blocking");
            AnimatorStateTransition guardEnd = guard.AddTransition(empty);
            guardEnd.hasExitTime = false;
            guardEnd.duration = 0.1f;
            guardEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, "Blocking");

            AnimatorStateTransition start = empty.AddTransition(swing);
            start.hasExitTime = false;
            start.duration = 0.05f;
            start.AddCondition(AnimatorConditionMode.If, 0f, "Swing");

            AnimatorStateTransition again = swing.AddTransition(swing);
            again.hasExitTime = false;
            again.duration = 0.05f;
            again.canTransitionToSelf = true;
            again.AddCondition(AnimatorConditionMode.If, 0f, "Swing");

            AnimatorStateTransition finish = swing.AddTransition(empty);
            finish.hasExitTime = true;
            finish.exitTime = SwingExitTime;
            finish.duration = 0.1f;

            // Combo: a backhand (the slash played backwards), then a heavy finisher; fists alternate jab and cross.
            AnimatorState backhand = machine.AddState("Swing2");
            backhand.motion = Slot("attack_2", "Sword_Regular_B");
            backhand.speedParameterActive = true;
            backhand.speedParameter = "SwingSpeed";
            AnimatorState finisher = machine.AddState("Swing3");
            finisher.motion = Slot("attack_3", "Sword_Regular_C");
            finisher.speedParameterActive = true;
            finisher.speedParameter = "SwingSpeed";
            AnimatorState chop = machine.AddState("Chop");
            chop.motion = BuildOneShotClip("TreeChopping_Loop");
            chop.speedParameterActive = true;
            chop.speedParameter = "SwingSpeed";
            // A pickaxe blow into a rock face (the Kevin Iglesias crafting pack); the tree chop if it is missing.
            AnimatorState mine = machine.AddState("Mine");
            AnimationClip mineClip = CraftingClip("Mining/HumanM@MiningOneHand01_R - Wall");
            mine.motion = mineClip != null ? mineClip : chop.motion;
            mine.speedParameterActive = true;
            mine.speedParameter = "SwingSpeed";
            AnimatorState cross = machine.AddState("Punch2");
            cross.motion = FindClip("Punch_Cross");
            cross.speedParameterActive = true;
            cross.speedParameter = "SwingSpeed";

            var strikes = new[]
            {
                (state: swing, trigger: "Swing"), (state: backhand, trigger: "Swing2"), (state: finisher, trigger: "Swing3"),
                (state: punch, trigger: "Punch"), (state: cross, trigger: "Punch2"), (state: chop, trigger: "Chop"),
                (state: mine, trigger: "Mine")
            };
            foreach (var to in strikes)
            {
                foreach (var from in strikes)
                {
                    if (from.state == to.state && (to.state == swing || to.state == punch)) continue; // already wired above
                    AnimatorStateTransition link = from.state.AddTransition(to.state);
                    link.hasExitTime = false;
                    link.duration = 0.05f;
                    link.canTransitionToSelf = true;
                    link.AddCondition(AnimatorConditionMode.If, 0f, to.trigger);
                }
                if (to.state == swing || to.state == punch) continue;
                AnimatorStateTransition enter = empty.AddTransition(to.state);
                enter.hasExitTime = false;
                enter.duration = 0.05f;
                enter.AddCondition(AnimatorConditionMode.If, 0f, to.trigger);
                AnimatorStateTransition leave = to.state.AddTransition(empty);
                leave.hasExitTime = true;
                // The chop is one cycle of a loop: after the follow through it would wind up again, so it ends there.
                leave.exitTime = to.state == chop ? ChopExitTime : SwingExitTime;
                leave.duration = to.state == chop ? 0.2f : 0.1f;
            }

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Chop",
                stateMachine = machine,
                avatarMask = mask,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override
            });

            // Holding an item up in front of the chest, Valheim style: the library's torch idle, on the left arm for the
            // torch and mirrored onto the right arm for the axe at rest (head up). Weights are driven by HeldTorch and
            // PlayerAxe; a swing, the guard or the holster take the arm back.
            AnimationClip holdClip = FindClip("Idle_Torch_Loop");
            AddPoseLayer(controller, CarryRightLayerName, holdClip, true, CarryRightMaskPath, AvatarMaskBodyPart.RightArm);
            AddPoseLayer(controller, CarryLeftLayerName, holdClip, false, CarryLeftMaskPath, AvatarMaskBodyPart.LeftArm);
            AddPoseLayer(controller, GripLeftLayerName, BuildFistClip("Left", FistLeftClipPath), false, GripLeftMaskPath, AvatarMaskBodyPart.LeftFingers);

            // The right hand closed round a handle: a humanoid fist pose on the right fingers only. PlayerAxe turns the
            // layer's weight up while the axe is in the hand, so the grip is the same whatever the arms are doing (the
            // sprint clip's flat open hands let the handle lie on an open palm).
            var gripMachine = new AnimatorStateMachine { name = "Grip" };
            AssetDatabase.AddObjectToAsset(gripMachine, controller);
            AnimatorState fist = gripMachine.AddState("Fist");
            fist.motion = BuildFistClip("Right", FistClipPath);
            gripMachine.defaultState = fist;
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = GripLayerName,
                stateMachine = gripMachine,
                avatarMask = BuildHumanMask(GripMaskPath, AvatarMaskBodyPart.RightFingers),
                defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override
            });

            // Draw and holster: the hand reaches back over the right shoulder (the wind up of the overhand throw) and
            // returns. Its own layer, masked to the chest and the right arm, so the left arm and legs keep walking.
            // PlayerAxe moves the axe between the hand and the back while the hand is back there.
            var stowMachine = new AnimatorStateMachine { name = "Stow" };
            AssetDatabase.AddObjectToAsset(stowMachine, controller);
            AnimatorState rest = stowMachine.AddState("Empty");
            rest.writeDefaultValues = false; // an Empty humanoid state that writes defaults shows the T-pose
            AnimatorState stow = stowMachine.AddState("Stow");
            stow.motion = BuildReachBackClip();
            stowMachine.defaultState = rest;
            AnimatorStateTransition stowStart = rest.AddTransition(stow);
            stowStart.hasExitTime = false;
            stowStart.duration = 0.06f;
            stowStart.AddCondition(AnimatorConditionMode.If, 0f, "Stow");
            AnimatorStateTransition stowEnd = stow.AddTransition(rest);
            stowEnd.hasExitTime = true;
            stowEnd.exitTime = 0.9f;
            stowEnd.duration = 0.1f;
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Stow",
                stateMachine = stowMachine,
                avatarMask = BuildHumanMask(StowMaskPath, AvatarMaskBodyPart.Body, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.RightFingers),
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override
            });
        }

        // A one state layer playing a pose, off until a script turns its weight up.
        private static void AddPoseLayer(AnimatorController controller, string name, AnimationClip clip, bool mirror, string maskPath, params AvatarMaskBodyPart[] parts)
        {
            var machine = new AnimatorStateMachine { name = name };
            AssetDatabase.AddObjectToAsset(machine, controller);
            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            state.mirror = mirror;
            machine.defaultState = state;
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = name,
                stateMachine = machine,
                avatarMask = BuildHumanMask(maskPath, parts),
                defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override
            });
        }

        // A one frame humanoid clip with only one hand's finger muscles: every finger curled, the thumb wrapped over.
        private static AnimationClip BuildFistClip(string side, string path)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { name = "Fist" + side };
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                float curl = finger == "Thumb" ? ThumbCurl : FingerCurl;
                for (int joint = 1; joint <= 3; joint++)
                    clip.SetCurve("", typeof(Animator), $"{side}Hand.{finger}.{joint} Stretched", AnimationCurve.Constant(0f, 1f, curl));
                clip.SetCurve("", typeof(Animator), $"{side}Hand.{finger}.Spread", AnimationCurve.Constant(0f, 1f, 0f));
            }
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // Seconds into OverhandThrow where the right hand is furthest back over the shoulder.
        public const float ReachPeakSeconds = 0.24f;
        // How long the reach back and return takes (the peak is halfway).
        public const float ReachClipSeconds = 0.56f;

        // The wind up of the overhand throw to the moment the hand is back over the right shoulder, then the same way
        // back to rest, resampled into one short clip.
        private static AnimationClip BuildReachBackClip()
        {
            AnimationClip source = FindClip("OverhandThrow");
            string path = "Assets/_Chopper/Prefabs/Player/ReachBack.anim";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { frameRate = 60f, name = "ReachBack" };
            const int Steps = 34;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                var keys = new Keyframe[Steps + 1];
                for (int i = 0; i <= Steps; i++)
                {
                    float u = i / (float)Steps;
                    float there = u <= 0.5f ? u * 2f : (1f - u) * 2f;
                    keys[i] = new Keyframe(u * ReachClipSeconds, curve.Evaluate(Mathf.SmoothStep(0f, 1f, there) * ReachPeakSeconds));
                }
                var built = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyLeftTangentMode(built, i, AnimationUtility.TangentMode.ClampedAuto);
                for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyRightTangentMode(built, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetEditorCurve(clip, binding, built);
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // One cycle of a looping clip, as its own non looping asset, so a single chop plays once and ends.
        private static AnimationClip BuildOneShotClip(string shortName)
        {
            AnimationClip source = FindClip(shortName);
            string path = "Assets/_Chopper/Prefabs/Player/" + shortName + "_Once.anim";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { frameRate = source.frameRate, name = shortName + "_Once" };
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
                AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(source, binding));
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // Only the spine and everything above it (arms, head) plays the chop; the legs keep walking.
        private static AvatarMask BuildUpperBodyMask(Transform modelRoot)
        {
            return BuildHumanMask(MaskPath, AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm,
                AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
                AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK);
        }

        // Only the listed humanoid parts play from the layer; the legs and the root keep the walk underneath.
        private static AvatarMask BuildHumanMask(string path, params AvatarMaskBodyPart[] active)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (AvatarMaskBodyPart part in active) mask.SetHumanoidBodyPartActive(part, true);
            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }

        // ---------- Model ----------

        static void PrepareChopperModel()
        {
            var importer = AssetImporter.GetAtPath(ChopperModelPath) as ModelImporter;
            if (importer == null) return;
            importer.globalScale = 1f;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var tex = AssetImporter.GetAtPath(ChopperAtlasPath) as TextureImporter;
            if (tex != null)
            {
                tex.sRGBTexture = true;
                tex.SaveAndReimport();
            }
        }

        static void DressChopper(GameObject model)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Chopper/Shaders/ChopperVertexColorLit.shader");
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(ChopperAtlasPath);
            Material Body(string matName)
            {
                string path = "Assets/_Chopper/Materials/Character/" + matName + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = matName };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.SetFloat("_UseAtlas", 1f);
                mat.SetTexture("_BaseMap", atlas);
                mat.SetColor("_BaseColor", Color.white);
                return mat;
            }
            Material body = Body("M_Chopper");
            Material gear = Body("M_Chopper_Gear");
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                bool gearPart = renderer.gameObject.name is "chopper_helmet" or "chopper_plate" or "chopper_pauldrons";
                renderer.sharedMaterial = gearPart ? gear : body;
                if (gearPart) renderer.enabled = false;
            }
        }

        // Adds the animated mannequin and the axe to the player root. Returns null when the model is missing.
        public static Result AttachModel(GameObject playerRoot)
        {
            PrepareClips();
            bool chopper = AssetDatabase.LoadAssetAtPath<GameObject>(ChopperModelPath) != null;
            if (chopper) PrepareChopperModel();
            string modelPath = chopper ? ChopperModelPath : ModelPath;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[CharacterSetup] Missing {modelPath}; the player has no model.");
                return null;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, playerRoot.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, FacingCorrectionYaw(model.transform), 0f);
            model.transform.localScale = chopper
                ? Vector3.one
                : new Vector3(ChopperScale * ChopperBreadth, ChopperScale, ChopperScale * ChopperBreadth);

            // Not '??': Unity objects override null checks, so the operator would keep a missing component.
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar != null && avatar.isHuman) animator.avatar = avatar;
            else Debug.LogError("[CharacterSetup] The character has no humanoid Avatar; it will stay in its T-pose.");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = BuildController(model.transform);

            // The axe guard is posed in code on top of the animation (the library has no block clip).
            Chopper.Player.GuardPose guardPose = model.GetComponent<Chopper.Player.GuardPose>();
            if (guardPose == null) guardPose = model.AddComponent<Chopper.Player.GuardPose>();

            var result = new Result { Animator = animator };

            if (chopper)
            {
                DressChopper(model);
            }
            else
            {
                // Dress the skeleton from the character catalog (the mannequin mesh is hidden at runtime). Every player is
                // Chopper: PlayerAppearance dresses each one in the default look.
                CharacterCatalog catalog = OutfitSetup.BuildCatalog();
                if (catalog != null)
                {
                    CharacterOutfitter outfitter = model.AddComponent<CharacterOutfitter>();
                    outfitter.Catalog = catalog;
                    outfitter.ApplyChopper();
                    result.Outfitter = outfitter;
                    OutfitSetup.BuildPreview(catalog, fbx, avatar, animator.runtimeAnimatorController, model.transform.localEulerAngles.y);
                }
            }

            // Chopper holds his own double-headed axe (the art project's chopper_axe on the Weapon_R socket) and carries
            // his shield stowed on the back (Shield_Back), so the left hand stays free for the torch.
            if (!chopper || !AttachChopperGear(model.transform, playerRoot.transform, result))
                AttachAxe(model.transform, playerRoot.transform, result);
            AttachPickaxe(model.transform, result);
            result.TorchVisual = AttachTorch(model.transform, playerRoot.transform);
            return result;
        }

        // The mannequin is authored facing -Z. Turn it so its toes point along +Z, the way the player faces. A humanoid
        // Avatar already plays the body facing the model's +Z whatever the bind pose, so it needs no turn (the 180 degree
        // turn from the bind pose had the player, draugr and golems running backward, measured 2026-10-07).
        internal static float FacingCorrectionYaw(Transform model)
        {
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject);
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Avatar>().Any(a => a.isHuman)) return 0f;

            Transform foot = FindDeep(model, LeftFoot);
            Transform toe = FindDeep(model, LeftToe);
            if (foot == null || toe == null) return 180f;

            Vector3 forward = toe.position - foot.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return 180f;
            return Vector3.SignedAngle(forward, Vector3.forward, Vector3.up);
        }

        private static void AttachAxe(Transform model, Transform playerRoot, Result result)
        {
            var axePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AxePath);
            if (axePrefab == null)
            {
                Debug.LogWarning("[CharacterSetup] Axe missing; the player has no axe.");
                result.AxeGrip = playerRoot;
                return;
            }
            // Measure the axe at identity: long axis, which end is the head, and which way the blade faces.
            Transform grip = AttachHeld(model, axePrefab, MeasureAxe(axePrefab), "AxeGrip", "Axe", out Renderer renderer);
            result.AxeGrip = grip != null ? grip : playerRoot;
            result.AxeRenderer = renderer;
        }

        // ---------- Chopper's own axe and shield ----------

        // The single-bladed bear-head axe of concept 1.5 (user, 2026-10-11). It mounts under the name chopper_axe.
        public const string ChopperAxePath = "Assets/_Chopper/Art/Character/chopper_axe_bear.fbx";
        public const string ChopperShieldPath = "Assets/_Chopper/Art/Character/chopper_shield.fbx";
        private const string ChopperGearMaterialPath = "Assets/_Chopper/Materials/Character/M_Chopper_Gear.mat";
        // The Blender exports in the art project, copied in when they are newer.
        private const string ArtExportFolder = "Chopper/Art/Export";
        // How far the stowed shield's board should sit off the back (meters).
        private const float ShieldBackGap = 0.02f;

        // Copies a prop FBX from the art project's exports when it is missing or different, and imports it like the body:
        // axis conversion not baked, as in the art project (ChopperImport), scale 1, mesh
        // read/write on (the checks sample vertices), no imported materials (the atlas material is assigned here).
        private static GameObject PrepareChopperProp(string assetPath)
        {
            string source = Path.Combine(ArtExportFolder, Path.GetFileName(assetPath));
            if (File.Exists(source) && (!File.Exists(assetPath) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(assetPath))))
            {
                File.Copy(source, assetPath, true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[CharacterSetup] Copied {source} to {assetPath}.");
            }
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return null;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        // An art project prop on a socket. In the art project the rig and props are imported without baking the axis
        // conversion and the prop's -90 X root turn is reset to identity (ChopperImport.AttachProp). The live chopper.fbx
        // bakes the conversion, which leaves every socket frame turned 180 degrees about its own Y against the frame the
        // rig script designed (measured on Shield_Back: its Y points at the body as designed, its Z points down instead of
        // up). So the root turn is reset and that half turn is put back on the mount. Position 0, scale 1.
        public static readonly Quaternion BakedSocketTurn = Quaternion.Euler(0f, 180f, 0f);

        private static GameObject MountProp(GameObject prop, Transform parent, string name, Material material)
        {
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prop, parent);
            item.name = name;
            Transform t = item.transform;
            Quaternion imported = prop.transform.localRotation;
            Debug.Log($"[CharacterSetup] {name} under {parent.name}: imported root rot {imported.eulerAngles}, scale {prop.transform.localScale}, children {t.childCount}; " +
                      $"socket world rot {parent.rotation.eulerAngles}, model-space position {parent.root.InverseTransformPoint(parent.position):F3}; mount rot set to {BakedSocketTurn.eulerAngles}.");
            t.localPosition = Vector3.zero;
            t.localRotation = BakedSocketTurn;
            t.localScale = Vector3.one;
            foreach (Transform child in t)
            {
                if (child.GetComponent<MeshFilter>() == null) continue;
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
            }
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
            return item;
        }

        private static bool AttachChopperGear(Transform model, Transform playerRoot, Result result)
        {
            GameObject axeProp = PrepareChopperProp(ChopperAxePath);
            GameObject shieldProp = PrepareChopperProp(ChopperShieldPath);
            var gear = AssetDatabase.LoadAssetAtPath<Material>(ChopperGearMaterialPath);
            Transform hand = FindDeep(model, RightHand) ?? FindDeep(model, "RightHand");
            Transform socket = hand != null ? FindDeep(hand, "Weapon_R") : null;
            if (axeProp == null || hand == null || socket == null)
            {
                Debug.LogWarning($"[CharacterSetup] chopper_axe ({axeProp != null}), the right hand ({hand != null}) or Weapon_R ({socket != null}) missing; using the old axe.");
                return false;
            }

            // The grip is a direct child of the hand (PlayerAxe treats the grip's parent as the hand, GuardPose looks the
            // grip up by name under it) and sits exactly on the Weapon_R socket, so the axe hangs as the socket says.
            var grip = new GameObject("AxeGrip");
            grip.transform.SetParent(hand, false);
            grip.transform.SetPositionAndRotation(socket.position, socket.rotation);
            grip.transform.localScale = Vector3.one;
            GameObject axe = MountProp(axeProp, grip.transform, "chopper_axe", gear);

            // Head and edge in the grip's space, from the mesh: the haft is the longest axis, the head the far end from
            // the grip (the socket is the grip centre), the blades the wider of the other two axes.
            MeshFilter filter = axe.GetComponentInChildren<MeshFilter>();
            Bounds b = filter.sharedMesh.bounds;
            Vector3 size = b.size;
            int haft = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            int blade = Enumerable.Range(0, 3).Where(i => i != haft).OrderByDescending(i => size[i]).First();
            Vector3 head = Vector3.zero, edge = Vector3.zero;
            head[haft] = Mathf.Abs(b.max[haft]) >= Mathf.Abs(b.min[haft]) ? 1f : -1f;
            edge[blade] = 1f;
            Matrix4x4 toGrip = grip.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            HeldItemFrame frame = grip.AddComponent<HeldItemFrame>();
            frame.Head = toGrip.MultiplyVector(head).normalized;
            frame.Edge = toGrip.MultiplyVector(edge).normalized;
            Debug.Log($"[CharacterSetup] chopper_axe mesh size {size:F3} m, bounds min {b.min:F3} max {b.max:F3}; grip {Vector3.Distance(grip.transform.position, hand.position) * 100f:F1} cm from the hand, " +
                      $"head along {frame.Head:F2}, blades along {frame.Edge:F2} (grip space); haft in model space {model.InverseTransformDirection(grip.transform.rotation * frame.Head):F2}.");
            result.AxeGrip = grip.transform;
            result.AxeRenderer = axe.GetComponentInChildren<Renderer>();

            Transform back = FindDeep(model, "Shield_Back");
            if (shieldProp == null || back == null)
            {
                Debug.LogWarning($"[CharacterSetup] chopper_shield ({shieldProp != null}) or Shield_Back ({back != null}) missing; no shield on the back.");
                return true;
            }
            GameObject shield = MountProp(shieldProp, back, "chopper_shield", gear);
            // The socket leaves the board floating off the back; it is moved toward the body (on the mount, not the
            // bone) to sit ShieldBackGap off it.
            Bounds shieldBounds = shield.GetComponentInChildren<Renderer>().bounds;
            Debug.Log($"[CharacterSetup] chopper_shield bounds center in model space {model.InverseTransformPoint(shieldBounds.center):F3}, size {shieldBounds.size:F3}; " +
                      $"axe bounds center {model.InverseTransformPoint(axe.GetComponentInChildren<Renderer>().bounds.center):F3}, size {axe.GetComponentInChildren<Renderer>().bounds.size:F3}; " +
                      $"right hand at {model.InverseTransformPoint(hand.position):F3}.");
            float before = ShieldGap(model, shield);
            if (!float.IsNaN(before))
            {
                Vector3 towardBody = shield.transform.up;
                shield.transform.localPosition = back.InverseTransformVector(towardBody * (before - ShieldBackGap));
                float after = ShieldGap(model, shield);
                Debug.Log($"[CharacterSetup] chopper_shield on Shield_Back: gap to the back before {before * 100f:F1} cm, after {after * 100f:F1} cm (mount local position {shield.transform.localPosition:F3}).");
            }
            else Debug.LogWarning("[CharacterSetup] chopper_shield: could not measure the gap to the back (no body surface in front of the board).");
            return true;
        }

        // The smallest distance toward the body from a shield vertex to the body surface in front of it, in the
        // bind pose (negative when the board is inside the body). Only visible body renderers count.
        private static float ShieldGap(Transform model, GameObject shield)
        {
            // The board's +Y points at the body (rig_chopper.py, Shield_Back).
            Vector3 dir = shield.transform.up;
            var triangles = new List<Vector3>();
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled || skin.sharedMesh == null) continue;
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                Vector3[] v = baked.vertices;
                int[] tris = baked.triangles;
                Matrix4x4 m = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                for (int i = 0; i < tris.Length; i++) triangles.Add(m.MultiplyPoint3x4(v[tris[i]]));
                Object.DestroyImmediate(baked);
            }
            const float lead = 0.5f;
            float best = float.PositiveInfinity;
            foreach (MeshFilter filter in shield.GetComponentsInChildren<MeshFilter>())
            {
                Matrix4x4 m = filter.transform.localToWorldMatrix;
                foreach (Vector3 local in filter.sharedMesh.vertices)
                {
                    Vector3 origin = m.MultiplyPoint3x4(local) - dir * lead;
                    for (int i = 0; i + 2 < triangles.Count; i += 3)
                    {
                        if (RayTriangle(origin, dir, triangles[i], triangles[i + 1], triangles[i + 2], out float t) && t - lead < best) best = t - lead;
                    }
                }
            }
            return float.IsPositiveInfinity(best) ? float.NaN : best;
        }

        private static bool RayTriangle(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0f;
            Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            Vector3 s = origin - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float w = Vector3.Dot(dir, q) * inv;
            if (w < 0f || u + w > 1f) return false;
            t = Vector3.Dot(e2, q) * inv;
            return t >= 0f;
        }

        // The pickaxe in the same fist, off until a vein is struck (PlayerMining).
        private static void AttachPickaxe(Transform model, Result result)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickaxePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterSetup] Missing {PickaxePath}; the player has no pickaxe.");
                return;
            }
            Transform grip = AttachHeld(model, prefab, MeasurePickaxe(prefab), "PickaxeGrip", "Pickaxe", out _);
            if (grip == null) return;
            grip.gameObject.SetActive(false);
            result.PickaxeGrip = grip;
        }

        // A tool in the right fist. A hammer grip: the handle runs across the palm, diagonally from the heel of the hand
        // (little finger side) up past the base of the index finger, so the head sits beyond the thumb and tips a
        // little toward the fingertips; the edge (or the pick's point) faces the way the knuckles face. (Pointing the
        // axe along the fingers, 2026-10-06, put the handle out of the knuckles like a pen.)
        private static Transform AttachHeld(Transform model, GameObject prefab, AxeShape shape, string gripName, string itemName, out Renderer renderer)
        {
            renderer = null;
            Transform hand = FindDeep(model, RightHand) ?? FindDeep(model, "RightHand");
            Transform finger = hand != null ? FindDeep(hand, RightMiddleFinger) : null;
            if (finger == null) finger = FindDeep(model, "Weapon_R");
            if (hand == null || finger == null)
            {
                Debug.LogWarning($"[CharacterSetup] Right hand bone missing; the player has no {itemName}.");
                return null;
            }
            Vector3 fingers = hand.InverseTransformPoint(finger.position).normalized;
            Vector3 outLocal = fingers;
            Transform thumb = FindDeep(hand, "thumb_01_r");
            Transform pinky = FindDeep(hand, "pinky_01_r");
            if (thumb != null && pinky != null)
            {
                Vector3 across = hand.InverseTransformPoint(thumb.position) - hand.InverseTransformPoint(pinky.position);
                across = Vector3.ProjectOnPlane(across, fingers).normalized;
                outLocal = (across + fingers * GripDiagonal).normalized;
            }
            Quaternion toOut = Quaternion.FromToRotation(shape.HeadDirection, outLocal);

            Vector3 wantedEdge = Vector3.ProjectOnPlane(fingers, outLocal).normalized;
            Vector3 currentEdge = toOut * shape.EdgeDirection;
            float roll = Vector3.SignedAngle(currentEdge, wantedEdge, outLocal);
            Quaternion rotation = Quaternion.AngleAxis(roll, outLocal) * toOut;

            var grip = new GameObject(gripName);
            grip.transform.SetParent(hand, false);
            // The grip is in the palm, halfway from the wrist to the middle knuckle, so the handle runs through the fist.
            grip.transform.localPosition = hand.InverseTransformPoint(Vector3.Lerp(hand.position, finger.position, PalmAlongHand));
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grip.transform);
            item.name = itemName;

            float handScale = Mathf.Max(0.0001f, hand.lossyScale.x);
            item.transform.localRotation = rotation;
            item.transform.localScale = prefab.transform.localScale * (AxeScaleMultiplier / handScale);
            item.transform.localPosition = rotation * (-shape.GripPoint * (AxeScaleMultiplier / handScale));

            // The head and edge directions in the grip's space, so the back holster places it exactly.
            HeldItemFrame frame = grip.AddComponent<HeldItemFrame>();
            frame.Head = rotation * shape.HeadDirection;
            frame.Edge = rotation * shape.EdgeDirection;
            renderer = item.GetComponentInChildren<Renderer>();
            return grip.transform;
        }

        // The pickaxe's handle runs along its up axis (the export puts it so); the head is at the end where the mesh
        // spreads widest, and the pick's points run along x. The grip is a fifth of the way up from the butt.
        internal static AxeShape MeasurePickaxe(GameObject prefab)
        {
            GameObject temp = Object.Instantiate(prefab);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var vertices = new List<Vector3>();
            foreach (MeshFilter filter in temp.GetComponentsInChildren<MeshFilter>())
            {
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                foreach (Vector3 v in filter.sharedMesh.vertices) vertices.Add(matrix.MultiplyPoint3x4(v));
            }
            Object.DestroyImmediate(temp);
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 v in vertices)
            {
                minY = Mathf.Min(minY, v.y);
                maxY = Mathf.Max(maxY, v.y);
            }
            float length = maxY - minY;
            float SpreadNear(float y)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (Vector3 v in vertices)
                {
                    if (Mathf.Abs(v.y - y) > length * 0.2f) continue;
                    lo = Mathf.Min(lo, v.x);
                    hi = Mathf.Max(hi, v.x);
                }
                return hi > lo ? hi - lo : 0f;
            }
            bool headUp = SpreadNear(maxY) >= SpreadNear(minY);
            Vector3 head = headUp ? Vector3.up : Vector3.down;
            float butt = headUp ? minY : maxY;
            // The handle's center line, from the lower part where there is only handle.
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 v in vertices)
            {
                if (Mathf.Abs(v.y - butt) > length * 0.35f) continue;
                sum += v;
                count++;
            }
            Vector3 center = count > 0 ? sum / count : Vector3.zero;
            return new AxeShape
            {
                HeadDirection = head,
                EdgeDirection = Vector3.right,
                GripPoint = new Vector3(center.x, butt + (headUp ? 1f : -1f) * length * 0.2f, center.z)
            };
        }

        // A woodsman's torch in the left hand: a tapered handle in the pixel pack's oak bark, a charred head bound with
        // two rope bands, the pack's pixel flames on top (smaller, no smoke) and a warm light. The meshes are saved as
        // assets so the prefab keeps them. HeldTorch closes the fist round the handle every frame. Off until lit.
        private const string TorchMeshFolder = "Assets/_Chopper/Prefabs/Player";
        private const float TorchShaftLength = 0.34f;
        private const float TorchHeadLength = 0.1f;
        private const float TorchFlameScale = 0.075f;

        private static GameObject AttachTorch(Transform model, Transform playerRoot)
        {
            Transform hand = FindDeep(model, "hand_l") ?? FindDeep(model, "LeftHand");
            if (hand == null)
            {
                Debug.LogWarning("[CharacterSetup] Left hand bone missing; the player has no torch.");
                return null;
            }

            var root = new GameObject("Torch");
            root.transform.SetParent(playerRoot, false);
            var held = root.AddComponent<HeldTorch>();

            float top = BuildTorchModel(root.transform);
            if (MeadowBuilder.PixelFlames(root.transform, new Vector3(0f, top - 0.02f, 0f), TorchFlameScale) == null)
                Debug.LogWarning($"[CharacterSetup] Missing {MeadowBuilder.FireEffectPath}; the torch has no flames.");

            var lightGo = new GameObject("TorchLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, top + 0.05f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 20f;
            light.intensity = 3.4f;
            light.shadows = LightShadows.None;

            var heldSo = new SerializedObject(held);
            heldSo.FindProperty("_hand").objectReferenceValue = hand;
            heldSo.FindProperty("_body").objectReferenceValue = playerRoot;
            heldSo.FindProperty("_light").objectReferenceValue = light;
            heldSo.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return root;
        }

        // The torch itself (bark handle, charred head, two rope bands), standing on the parent's origin at its natural
        // size. Returns the height of the head's top. Shared by the held torch and the yard torches (scaled up).
        public static float BuildTorchModel(Transform parent)
        {
            Material bark = AssetDatabase.LoadAssetAtPath<Material>(PixelPackSetup.Root + "/Essential/Materials/tree_oak_log_psx2.mat");
            if (bark == null) bark = TorchMaterial("M_TorchWood", new Color(0.30f, 0.19f, 0.10f), false);
            Material charred = TorchMaterial("M_TorchHead", new Color(0.09f, 0.07f, 0.06f), false);
            Material rope = TorchMaterial("M_TorchRope", new Color(0.42f, 0.33f, 0.2f), false);

            // The handle, thin at the butt and a little thicker under the head; then the head flaring out.
            TorchPart(parent, "Handle", TaperedMesh("Mesh_TorchHandle", 0.019f, 0.026f, TorchShaftLength, 0f), bark, 0f);
            TorchPart(parent, "Head", TaperedMesh("Mesh_TorchHead", 0.032f, 0.042f, TorchHeadLength, 0.015f), charred, TorchShaftLength - 0.03f);
            TorchPart(parent, "BandLow", TaperedMesh("Mesh_TorchBand", 0.035f, 0.036f, 0.018f, 0f), rope, TorchShaftLength - 0.01f);
            TorchPart(parent, "BandHigh", TaperedMesh("Mesh_TorchBand", 0.035f, 0.036f, 0.018f, 0f), rope, TorchShaftLength + 0.05f);
            return TorchShaftLength - 0.03f + TorchHeadLength;
        }

        // How big the held torch's flames are at its natural size (yard torches scale this with the torch).
        public static float HeldTorchFlameScale => TorchFlameScale;

        private static void TorchPart(Transform root, string name, Mesh mesh, Material material, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // A tapered round piece standing on y = 0, closed at both ends, with a little random wobble on the rim so it
        // does not read as a machined cylinder. Saved (or refreshed) as a mesh asset.
        private static Mesh TaperedMesh(string name, float bottomRadius, float topRadius, float length, float wobble)
        {
            const int sides = 10;
            const int rings = 4;
            string path = $"{TorchMeshFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = name };
            mesh.Clear();
            var random = new System.Random(name.GetHashCode());
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float along = r / (float)rings;
                float radius = Mathf.Lerp(bottomRadius, topRadius, along);
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2f;
                    float bump = s == sides ? 0f : (float)(random.NextDouble() * 2.0 - 1.0) * wobble;
                    float rr = radius + bump;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * rr, along * length, Mathf.Sin(angle) * rr));
                    uvs.Add(new Vector2(s / (float)sides, along * length * 4f));
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = r * (sides + 1) + s;
                    int b = a + sides + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            // Caps.
            foreach (bool topCap in new[] { false, true })
            {
                int center = vertices.Count;
                float y = topCap ? length : 0f;
                vertices.Add(new Vector3(0f, y, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                int ring = topCap ? rings * (sides + 1) : 0;
                for (int s = 0; s < sides; s++)
                {
                    if (topCap) { triangles.Add(center); triangles.Add(ring + s + 1); triangles.Add(ring + s); }
                    else { triangles.Add(center); triangles.Add(ring + s); triangles.Add(ring + s + 1); }
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        internal static Material TorchMaterial(string name, Color color, bool emissive)
        {
            const string folder = "Assets/_Chopper/Materials/World";
            if (!AssetDatabase.IsValidFolder("Assets/_Chopper/Materials")) AssetDatabase.CreateFolder("Assets/_Chopper", "Materials");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Chopper/Materials", "World");
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 3f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        internal struct AxeShape
        {
            public Vector3 HeadDirection;
            public Vector3 EdgeDirection;
            public Vector3 GripPoint;
        }

        internal static AxeShape MeasureAxe(GameObject prefab)
        {
            GameObject temp = Object.Instantiate(prefab);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var bounds = new Bounds();
            bool first = true;
            var vertices = new List<Vector3>();
            foreach (MeshFilter filter in temp.GetComponentsInChildren<MeshFilter>())
            {
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                foreach (Vector3 v in filter.sharedMesh.vertices) vertices.Add(matrix.MultiplyPoint3x4(v));
            }
            foreach (Vector3 v in vertices)
            {
                if (first) { bounds = new Bounds(v, Vector3.zero); first = false; }
                else bounds.Encapsulate(v);
            }
            Vector3 centroid = Vector3.zero;
            foreach (Vector3 v in vertices) centroid += v;
            centroid /= Mathf.Max(1, vertices.Count);
            Object.DestroyImmediate(temp);

            Vector3 size = bounds.size;
            int longAxis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            int shortAxis = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
            int widthAxis = 3 - longAxis - shortAxis;

            float headSign = centroid[longAxis] >= bounds.center[longAxis] ? 1f : -1f;

            Vector3 head = Vector3.zero;
            head[longAxis] = headSign;

            // The grip is on the handle's own center line. The bounding box's middle across the width lies in the middle of
            // the blade, well off the handle, which put the grip point (and so the fist) in the air beside the handle.
            Vector3 handleEnd = bounds.center - head * (size[longAxis] * 0.5f);
            Vector3 handleCenter = bounds.center;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 v in vertices)
            {
                float along = Vector3.Dot(v - handleEnd, head);
                if (along > size[longAxis] * HandleSampleFraction) continue;
                sum += v;
                count++;
            }
            if (count > 0) handleCenter = sum / count;

            // The edge points at the blade, the side the head sticks off the handle. Comparing the vertex centroid to the
            // bounds center pointed the other way: the tip pulls the box toward the blade while the vertices stay on the
            // handle, so the edge faced the poll and the blade pointed up in the hand (measured 2026-10-07, metal at -X).
            float extreme = 0f;
            foreach (Vector3 v in vertices)
            {
                float along = Vector3.Dot(v - handleEnd, head);
                if (along < size[longAxis] * 0.55f) continue;
                float off = v[widthAxis] - handleCenter[widthAxis];
                if (Mathf.Abs(off) > Mathf.Abs(extreme)) extreme = off;
            }
            float edgeSign = extreme < 0f ? -1f : 1f;
            Vector3 edge = Vector3.zero;
            edge[widthAxis] = edgeSign;
            Vector3 grip = handleEnd + head * (size[longAxis] * GripFraction);
            grip[widthAxis] = handleCenter[widthAxis];
            grip[shortAxis] = handleCenter[shortAxis];
            return new AxeShape
            {
                HeadDirection = head,
                EdgeDirection = edge,
                GripPoint = grip
            };
        }

        internal static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }
    }
}
