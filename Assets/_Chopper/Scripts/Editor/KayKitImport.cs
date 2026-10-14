using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Batch: Chopper.Editor.KayKitImport.Apply. Imports the KayKit melee and general clips as Humanoid so they retarget
    // onto Chopper (strikes, flinch, parry). Logs each clip's length and "[KayKit] RESULT".
    public static class KayKitImport
    {
        public const string Folder = "Assets/KayKit_Character_Animations_1.1/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium";
        public const string MeleePath = Folder + "/Rig_Medium_CombatMelee.fbx";
        public const string GeneralPath = Folder + "/Rig_Medium_General.fbx";

        public const string StrikeA = "Melee_1H_Attack_Slice_Diagonal";
        public const string StrikeB = "Melee_1H_Attack_Slice_Horizontal";
        public const string StrikeC = "Melee_1H_Attack_Chop";
        public const string Flinch = "Hit_A";
        public const string Parry = "Melee_Block_Attack";

        private static readonly Dictionary<string, string> Bones = new Dictionary<string, string>
        {
            { "hips", "Hips" }, { "spine", "Spine" }, { "chest", "Chest" }, { "head", "Head" },
            { "upperarm.l", "LeftUpperArm" }, { "lowerarm.l", "LeftLowerArm" }, { "wrist.l", "LeftHand" },
            { "upperarm.r", "RightUpperArm" }, { "lowerarm.r", "RightLowerArm" }, { "wrist.r", "RightHand" },
            { "upperleg.l", "LeftUpperLeg" }, { "lowerleg.l", "LeftLowerLeg" }, { "foot.l", "LeftFoot" }, { "toes.l", "LeftToes" },
            { "upperleg.r", "RightUpperLeg" }, { "lowerleg.r", "RightLowerLeg" }, { "foot.r", "RightFoot" }, { "toes.r", "RightToes" },
        };

        public static AnimationClip Clip(string path, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == name || c.name.EndsWith("|" + name)));
        }

        public static void Apply()
        {
            bool pass = MakeHumanoid(MeleePath) & MakeHumanoid(GeneralPath);
            foreach ((string path, string name) in new[] { (MeleePath, StrikeA), (MeleePath, StrikeB), (MeleePath, StrikeC), (GeneralPath, Flinch), (MeleePath, Parry) })
            {
                AnimationClip clip = Clip(path, name);
                Debug.Log($"[KayKit] {name}: {(clip != null ? $"{clip.length:F3} s, humanoid {clip.humanMotion}" : "MISSING")}");
                pass &= clip != null && clip.humanMotion;
            }
            Debug.Log("[KayKit] RESULT " + (pass ? "PASS" : "FAIL"));
            if (!pass) EditorApplication.Exit(1);
        }

        private static bool MakeHumanoid(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[KayKit] no model at " + path);
                return false;
            }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            HumanDescription description = importer.humanDescription;
            description.human = Bones.Select(b => new HumanBone { boneName = b.Key, humanName = b.Value, limit = new HumanLimit { useDefaultValues = true } }).ToArray();
            importer.humanDescription = description;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            bool ok = avatar != null && avatar.isValid && avatar.isHuman;
            Debug.Log($"[KayKit] {System.IO.Path.GetFileName(path)}: avatar valid {avatar != null && avatar.isValid}, human {avatar != null && avatar.isHuman}, {clips.Length} clips");
            return ok;
        }
    }
}
