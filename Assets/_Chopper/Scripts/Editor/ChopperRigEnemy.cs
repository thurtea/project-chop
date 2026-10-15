using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Chopper.Editor
{
    public static class ChopperRigEnemy
    {
        public const string ElementalPath = "Assets/_Chopper/Art/Character/chopper_elemental.fbx";
        public const string DraugrAtlasPath = "Assets/_Chopper/Art/Character/chopper_atlas_draugr.png";
        private const string DoubleAxePath = "Assets/_Chopper/Art/Character/chopper_axe.fbx";
        private const string ShaderPath = "Assets/_Chopper/Shaders/ChopperVertexColorLit.shader";
        private const string MaterialFolder = "Assets/_Chopper/Materials/Character/";
        private const float WalkTakeSpeed = 0.75f;
        private const float ElementalWalkSpeed = 0.70f;
        private const float DraugrWalkSpeed = 0.65f;
        private const float RunTakeSpeed = 4.1f;
        private const float WalkSpeed = 1.6f;
        private const float RunSpeed = 5.2f;

        public static bool DraugrReady =>
            AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.ChopperModelPath) != null &&
            AssetDatabase.LoadAssetAtPath<Texture2D>(DraugrAtlasPath) != null;

        public static bool ElementalReady =>
            AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.ChopperModelPath) != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(ElementalPath) != null;

        public static void RebuildEnemies()
        {
            GameObject draugr = DraugrSetup.Build();
            GameObject golem = GolemSetup.Build();
            AssetDatabase.SaveAssets();
            int draugrParts = draugr != null ? draugr.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled) : 0;
            int golemParts = golem != null ? golem.GetComponentsInChildren<Renderer>(true).Length : 0;
            bool draugrHuman = draugr != null && draugr.GetComponentInChildren<Animator>().avatar is { isHuman: true };
            bool golemHuman = golem != null && golem.GetComponentInChildren<Animator>().avatar is { isHuman: true };
            Debug.Log($"RESULT: draugr on Chopper {DraugrReady}, {draugrParts} shown parts, humanoid {draugrHuman}; " +
                      $"elemental {ElementalReady}, {golemParts} renderers, humanoid {golemHuman}");
        }

        public static Avatar ChopperAvatar() =>
            AssetDatabase.LoadAllAssetsAtPath(CharacterSetup.ChopperModelPath).OfType<Avatar>().FirstOrDefault(a => a.isHuman);

        public static void PrepareElemental()
        {
            var importer = AssetImporter.GetAtPath(ElementalPath) as ModelImporter;
            if (importer == null) return;
            importer.globalScale = 1f;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = ChopperAvatar();
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        public static Material Material(string name, string atlasPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (atlasPath != null && AssetImporter.GetAtPath(atlasPath) is TextureImporter texture && texture.anisoLevel != 4)
            {
                texture.sRGBTexture = true;
                texture.mipmapEnabled = true;
                texture.anisoLevel = 4;
                texture.SaveAndReimport();
            }
            Texture2D atlas = atlasPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath) : null;
            material.SetFloat("_UseAtlas", atlas != null ? 1f : 0f);
            material.SetTexture("_BaseMap", atlas);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static GameObject Instantiate(string name, string modelPath, string controllerPath, float attackSpeed,
            out GameObject model)
        {
            var root = new GameObject(name);
            model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            model.name = "Model";
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = ChopperAvatar();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = BuildController(controllerPath, attackSpeed);
            return root;
        }

        public static void MountDoubleAxe(Transform model, Material material)
        {
            GameObject axe = CharacterSetup.PrepareChopperProp(DoubleAxePath);
            Transform socket = CharacterSetup.FindDeep(model, "Weapon_R");
            if (axe == null || socket == null)
            {
                Debug.LogWarning("[ChopperRigEnemy] chopper_axe or Weapon_R missing; the draugr fights bare handed.");
                return;
            }
            CharacterSetup.MountProp(axe, socket, "chopper_axe", material);
        }

        private static AnimatorController BuildController(string path, float attackSpeed)
        {
            bool elemental = path.IndexOf("Golem", System.StringComparison.OrdinalIgnoreCase) >= 0;
            string walkName = elemental ? "Elemental_Walk" : "Draugr_Walk";
            string attackName = elemental ? "Elemental_Slam" : "Draugr_Attack";
            AnimationClip walk = CharacterSetup.ChopperTake(walkName) ?? CharacterSetup.ChopperTake("Walk");
            AnimationClip attack = CharacterSetup.ChopperTake(attackName) ?? CharacterSetup.ChopperTake("Attack_1");
            float walkNatural = walk != null && walk.name.Contains(walkName) ? (elemental ? ElementalWalkSpeed : DraugrWalkSpeed) : WalkTakeSpeed;

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(CharacterSetup.ChopperTake("Idle"), 0f);
            tree.AddChild(walk, WalkSpeed);
            tree.AddChild(CharacterSetup.ChopperTake("Run"), RunSpeed);
            ChildMotion[] children = tree.children;
            children[1].timeScale = WalkSpeed / walkNatural;
            children[2].timeScale = RunSpeed / RunTakeSpeed;
            tree.children = children;
            machine.defaultState = locomotion;

            if (elemental)
            {
                controller.AddParameter("Weapon", AnimatorControllerParameterType.Int);
                string[] weaponTakes = { "Elemental_Chop", "Elemental_Thrust", "Elemental_Smash", "Elemental_Stab" };
                var used = new System.Text.StringBuilder();
                for (int i = 0; i < weaponTakes.Length; i++)
                {
                    AnimationClip clip = CharacterSetup.ChopperTake(weaponTakes[i]);
                    if (i > 0) used.Append(", ");
                    used.Append(clip != null ? clip.name : "MISSING");
                    AddWeaponAttack(machine, locomotion, weaponTakes[i], clip ?? attack, i, attackSpeed);
                }
                Debug.Log($"RESULT: {Path.GetFileNameWithoutExtension(path)} walk {(walk != null ? walk.name : "MISSING")} attacks {used}");
            }
            else
            {
                AddAction(machine, locomotion, "Attack", attack, attackSpeed, 0.9f);
                Debug.Log($"RESULT: {Path.GetFileNameWithoutExtension(path)} walk {(walk != null ? walk.name : "MISSING")} attack {(attack != null ? attack.name : "MISSING")}");
            }
            AddAction(machine, locomotion, "Hit", CharacterSetup.ChopperTake("HitReact"), 1f, 0.8f);
            AnimatorState death = machine.AddState("Death");
            death.motion = CharacterSetup.ChopperTake("Death");
            AnimatorStateTransition toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            toDeath.duration = 0.08f;
            toDeath.canTransitionToSelf = false;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddWeaponAttack(AnimatorStateMachine machine, AnimatorState back, string stateName, Motion clip, int weapon, float speed)
        {
            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;
            state.speed = speed;
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            enter.AddCondition(AnimatorConditionMode.Equals, weapon, "Weapon");
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            AnimatorStateTransition exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = 0.92f;
            exit.duration = 0.1f;
        }

        private static void AddAction(AnimatorStateMachine machine, AnimatorState back, string trigger, Motion clip, float speed, float exitTime)
        {
            AnimatorState state = machine.AddState(trigger);
            state.motion = clip;
            state.speed = speed;
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.duration = 0.06f;
            enter.canTransitionToSelf = false;
            AnimatorStateTransition exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = exitTime;
            exit.duration = 0.12f;
        }
    }
}
