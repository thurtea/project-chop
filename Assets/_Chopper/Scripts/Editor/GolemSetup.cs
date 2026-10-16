using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Chopper.Editor
{
    // Builds the night forest creature: the animation mannequin (hidden) wearing the base body with the pine's bark,
    // and that element's foliage as a crown. It walks, punches, flinches and falls. A stand-in until the Elderwood
    // Guardian is imported (requirements.md). Crypt guardians keep the draugr. Called from MeadowBuilder.
    public static class GolemSetup
    {
        public const string PrefabPath = "Assets/_Chopper/Prefabs/Enemies/Golem.prefab";
        private const string ControllerPath = "Assets/_Chopper/Prefabs/Enemies/Golem.controller";
        private const string BodyMeshPath = "Assets/_Chopper/Prefabs/Enemies/Mesh_GolemBody.asset";
        private const string BarkMaterialPath = "Assets/_Chopper/Materials/World/M_GolemBark.mat";
        private const string BodyFbx = "Assets/ThirdParty/Quaternius_UniversalBaseCharacters/Bodies/Superhero_Male_FullBody.fbx";
        private const string BarkSourcePath = "Assets/_Chopper/Materials/World/M_Bark.mat";
        // Same order as Element (Neutral, Water, Fire, Earth, Wind) and as MeadowBuilder.TreantModels.
        private static readonly string[] Trees =
        {
        };
        private const float CrownWidth = 1.15f;
        private const float TuftWidth = 0.42f;
        private const float PunchSeconds = 0.7f;

        private class Measured
        {
            public List<Transform> Sources = new List<Transform>();
            public Vector3 Center;
            public float Width;
        }

        public static GameObject Build()
        {
            if (ChopperRigEnemy.ElementalReady) return BuildOnChopper();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.ModelPath);
            var body = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbx);
            if (fbx == null || body == null)
            {
                Debug.LogWarning("[GolemSetup] Mannequin or body missing; forest enemies stay walking trees.");
                return null;
            }
            CharacterSetup.PrepareClips();

            var root = new GameObject("Golem");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0f, CharacterSetup.FacingCorrectionYaw(model.transform), 0f);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CharacterSetup.ModelPath))
            {
                if (asset is Avatar found) { avatar = found; break; }
            }
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = BuildController();

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            Dress(model.transform, body);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildOnChopper()
        {
            ChopperRigEnemy.PrepareElemental();
            Material material = ChopperRigEnemy.Material("M_Elemental", null);
            GameObject root = ChopperRigEnemy.Instantiate("Golem", ChopperRigEnemy.ElementalPath, ControllerPath, 0.8f,
                out GameObject model);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void Dress(Transform model, GameObject bodyPrefab)
        {
            AddBody(model, bodyPrefab);
            for (int i = 0; i < Trees.Length; i++)
            {
                string path = Trees[i];
                var tree = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (tree == null) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(tree);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Measured crown = MeasureFoliage(instance.transform);
                string label = "Leaves" + i;
                if (crown.Sources.Count > 0)
                {
                    AddFoliage(model, crown, label, CrownWidth, CharacterSetup.FindDeep(model, "Head"), true);
                    AddFoliage(model, crown, label, TuftWidth, CharacterSetup.FindDeep(model, "upperarm_l"), false);
                    AddFoliage(model, crown, label, TuftWidth, CharacterSetup.FindDeep(model, "upperarm_r"), false);
                }
                Object.DestroyImmediate(instance);
            }
        }

        // The base body, without the head (the crown covers the neck), in the pine's bark.
        private static void AddBody(Transform model, GameObject bodyPrefab)
        {
            SkinnedMeshRenderer source = null;
            foreach (SkinnedMeshRenderer smr in bodyPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name == "Eyes" || smr.name == "Eyebrows") continue;
                source = smr;
                break;
            }
            if (source == null)
            {
                Debug.LogWarning("[GolemSetup] The body mesh is missing.");
                return;
            }

            var go = new GameObject("Bark");
            go.transform.SetParent(model, false);
            var skin = go.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = BodyMesh(source);
            skin.sharedMaterial = BarkMaterial();
            Transform rootBone = CharacterSetup.FindDeep(model, "root");
            var bones = new Transform[source.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                string boneName = source.bones[i] != null ? source.bones[i].name : "root";
                bones[i] = CharacterSetup.FindDeep(model, boneName) ?? rootBone;
            }
            skin.bones = bones;
            skin.rootBone = rootBone;
            skin.localBounds = new Bounds(new Vector3(0f, 0.95f, 0f), new Vector3(1.8f, 2.1f, 1.8f));
            skin.updateWhenOffscreen = true;
        }

        private static Mesh BodyMesh(SkinnedMeshRenderer sourceRenderer)
        {
            Mesh source = sourceRenderer.sharedMesh;
            Transform[] bones = sourceRenderer.bones;
            BoneWeight[] weights = source.boneWeights;
            var keep = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                string dominant = bones[weights[i].boneIndex0] != null ? bones[weights[i].boneIndex0].name.ToLowerInvariant() : "";
                keep[i] = !dominant.Contains("head");
            }
            int[] triangles = source.triangles;
            var kept = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                if (keep[triangles[t]] && keep[triangles[t + 1]] && keep[triangles[t + 2]])
                {
                    kept.Add(triangles[t]);
                    kept.Add(triangles[t + 1]);
                    kept.Add(triangles[t + 2]);
                }
            }

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            Mesh mesh = existing != null ? existing : new Mesh();
            mesh.Clear();
            mesh.name = "Mesh_GolemBody";
            mesh.indexFormat = source.indexFormat;
            mesh.vertices = source.vertices;
            mesh.normals = source.normals;
            mesh.tangents = source.tangents;
            mesh.uv = source.uv;
            mesh.boneWeights = weights;
            mesh.bindposes = source.bindposes;
            mesh.SetTriangles(kept, 0);
            mesh.RecalculateBounds();
            if (existing == null) AssetDatabase.CreateAsset(mesh, BodyMeshPath);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material BarkMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(BarkSourcePath);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(BarkMaterialPath);
            Material mat = existing != null ? existing : new Material(source);
            if (source != null && existing == null) mat.CopyPropertiesFromMaterial(source);
            mat.name = "M_GolemBark";
            mat.SetTextureScale("_BaseMap", new Vector2(2.5f, 2.5f));
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.SetColor("_EmissionColor", Color.black);
            if (existing == null) AssetDatabase.CreateAsset(mat, BarkMaterialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        // Foliage centered on a bone. onHead lifts it so the leaves sit above the neck; a tuft is centered on the joint.
        private static void AddFoliage(Transform model, Measured part, string label, float width, Transform bone, bool onHead)
        {
            if (bone == null || part.Width < 0.01f) return;
            var limb = new GameObject(label);
            limb.transform.SetParent(bone, false);
            foreach (Transform source in part.Sources)
            {
                var copy = Object.Instantiate(source.gameObject, limb.transform);
                copy.name = label;
                copy.transform.localPosition = source.position - part.Center;
                copy.transform.localRotation = source.rotation;
                Vector3 scale = source.lossyScale;
                copy.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            }
            // The pine's leaves are flat cards. A second copy turned a quarter makes the crown read from every side.
            if (onHead)
            {
                var cards = new List<Transform>();
                foreach (Transform child in limb.transform) cards.Add(child);
                foreach (Transform child in cards)
                {
                    var twin = Object.Instantiate(child.gameObject, limb.transform);
                    twin.name = label;
                    twin.transform.localRotation = child.localRotation * Quaternion.Euler(0f, 90f, 0f);
                }
            }
            limb.transform.localScale = Vector3.one;
            float current = WidthOf(limb);
            if (current > 0.01f) limb.transform.localScale = Vector3.one * (width / current);
            // The pine crown is a tall spike. Squash it into a head of leaves so the body reads underneath.
            if (onHead)
            {
                Vector3 crownScale = limb.transform.localScale;
                limb.transform.localScale = new Vector3(crownScale.x * 1.25f, crownScale.y * 0.38f, crownScale.z * 1.25f);
            }
            Bounds bounds = BoundsOf(limb);
            if (onHead)
            {
                float lift = bone.position.y - bounds.min.y + 0.02f;
                limb.transform.position += new Vector3(bone.position.x - bounds.center.x, lift, bone.position.z - bounds.center.z);
            }
            else
            {
                limb.transform.position += bone.position - bounds.center;
            }
            bool show = label == "Leaves0";
            foreach (Renderer renderer in limb.GetComponentsInChildren<Renderer>(true)) renderer.enabled = show;
        }

        private static Measured MeasureFoliage(Transform tree)
        {
            var sources = new List<Transform>();
            foreach (Transform t in tree.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Renderer>() == null) continue;
                if (!t.name.ToLowerInvariant().Contains("foliage")) continue;
                if (t.parent != null && t.parent.name.ToLowerInvariant().Contains("foliage")) continue;
                sources.Add(t);
            }
            var bounds = new Bounds();
            bool first = true;
            foreach (Transform source in sources)
            {
                foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            return new Measured
            {
                Sources = sources,
                Center = first ? Vector3.zero : bounds.center,
                Width = Mathf.Max(0.01f, first ? 0.01f : Mathf.Max(bounds.size.x, bounds.size.z))
            };
        }

        private static float WidthOf(GameObject go)
        {
            Bounds bounds = BoundsOf(go);
            return Mathf.Max(bounds.size.x, bounds.size.z);
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private static AnimatorController BuildController()
        {
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(CharacterSetup.FindClip("Idle_Loop"), 0f);
            tree.AddChild(CharacterSetup.FindClip("Walk_Loop"), 1.6f);
            tree.AddChild(CharacterSetup.FindClip("Jog_Fwd_Loop"), 5.2f);
            machine.defaultState = locomotion;

            AnimationClip punch = CharacterSetup.FindClip("Punch_Cross");
            AddAction(machine, locomotion, "Attack", punch, punch != null ? punch.length / PunchSeconds : 1f, 0.92f);
            AddAction(machine, locomotion, "Hit", CharacterSetup.FindClip("Hit_Chest"), 1f, 0.8f);
            AnimatorState death = machine.AddState("Death");
            death.motion = CharacterSetup.FindClip("Death01");
            AnimatorStateTransition toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            toDeath.duration = 0.08f;
            toDeath.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddAction(AnimatorStateMachine machine, AnimatorState back, string trigger, Motion clip, float speed, float exitTime)
        {
            AnimatorState state = machine.AddState(trigger);
            state.motion = clip;
            state.speed = Mathf.Max(0.25f, speed);
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
