using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chopper.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Chopper.Editor
{
    // Builds the draugr, the crypt's undead swordsman: the player's animated mannequin (Universal Animation Library)
    // dressed by the character catalog, with the Medieval Weapons sword in its right hand and its own animator
    // (idle, walk, sword attack, hit, death). A visual only: the networked Treant shows it for crypt guardians and
    // arena enemies and drives the animator. Called from MeadowBuilder when it builds the Treant prefab.
    public static class DraugrSetup
    {
        public const string PrefabPath = "Assets/_Chopper/Prefabs/Enemies/Draugr.prefab";
        private const string ControllerPath = "Assets/_Chopper/Prefabs/Enemies/Draugr.controller";
        private const string SwordPath = "Assets/ThirdParty/Quaternius_MedievalWeapons/FBX/Sword.fbx";
        private const string CatalogPath = "Assets/_Chopper/Resources/CharacterCatalog.asset";
        private const float SwordScale = 1.1f;
        // Where along the sword the hand grips, from the pommel end, as a fraction of its length.
        private const float GripFraction = 0.08f;

        public static GameObject Build()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.ModelPath);
            if (fbx == null)
            {
                Debug.LogWarning("[DraugrSetup] The animation library model is missing; crypt enemies stay trees.");
                return null;
            }
            CharacterSetup.PrepareClips();

            var root = new GameObject("Draugr");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0f, CharacterSetup.FacingCorrectionYaw(model.transform), 0f);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(CharacterSetup.ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = false;
            // The outfit is replaced when a draugr wakes, and a culling Animator only watches the renderers it found when
            // it started, so it thought itself unseen and left the body in its T-pose. There are only a few at a time.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = BuildController();

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (catalog != null)
            {
                CharacterOutfitter outfitter = model.AddComponent<CharacterOutfitter>();
                outfitter.Catalog = catalog;
                outfitter.WearChopperClothes = false;
                outfitter.ApplyChopper();
            }

            AttachSword(model.transform, root.transform);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
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
            tree.AddChild(CharacterSetup.FindClip("Sword_Idle"), 0f);
            tree.AddChild(CharacterSetup.FindClip("Walk_Loop"), 1.6f);
            tree.AddChild(CharacterSetup.FindClip("Jog_Fwd_Loop"), 5.2f);
            machine.defaultState = locomotion;

            AddAction(machine, locomotion, "Attack", "Sword_Attack", 0.9f);
            AddAction(machine, locomotion, "Hit", "Hit_Chest", 0.8f);
            AnimatorState death = machine.AddState("Death");
            death.motion = CharacterSetup.FindClip("Death01");
            AnimatorStateTransition toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            toDeath.duration = 0.08f;
            toDeath.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // A one shot state entered from anywhere by its trigger, returning to walking when it is nearly done.
        private static void AddAction(AnimatorStateMachine machine, AnimatorState back, string trigger, string clip, float exitTime)
        {
            AnimatorState state = machine.AddState(trigger);
            state.motion = CharacterSetup.FindClip(clip);
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.duration = 0.08f;
            enter.canTransitionToSelf = false;
            AnimatorStateTransition exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = exitTime;
            exit.duration = 0.15f;
        }

        // The sword points out of the fist along the fingers, edge forward. Its blade is the long thin end: fewer
        // vertices than the hilt, so the vertex centroid sits toward the pommel.
        private static void AttachSword(Transform model, Transform facing)
        {
            var sword = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
            Transform hand = CharacterSetup.FindDeep(model, "hand_r");
            Transform finger = hand != null ? CharacterSetup.FindDeep(hand, "middle_01_r") : null;
            Transform thumb = hand != null ? CharacterSetup.FindDeep(hand, "thumb_01_r") : null;
            Transform pinky = hand != null ? CharacterSetup.FindDeep(hand, "pinky_01_r") : null;
            if (sword == null || hand == null || finger == null || thumb == null || pinky == null)
            {
                Debug.LogWarning("[DraugrSetup] Sword or hand bones missing; the draugr fights bare handed.");
                return;
            }

            Measure(sword, out Vector3 bladeDirection, out Vector3 flatDirection, out Vector3 gripPoint);

            // A fist grips across the palm, so the blade leaves the hand on the thumb side.
            Vector3 fingers = hand.InverseTransformPoint(finger.position).normalized;
            Vector3 across = hand.InverseTransformPoint(thumb.position) - hand.InverseTransformPoint(pinky.position);
            Vector3 outLocal = Vector3.ProjectOnPlane(across, fingers).normalized;
            Quaternion toOut = Quaternion.FromToRotation(bladeDirection, outLocal);
            Vector3 wantedFlat = Vector3.ProjectOnPlane(hand.InverseTransformDirection(facing.right), outLocal).normalized;
            float roll = Vector3.SignedAngle(toOut * flatDirection, wantedFlat, outLocal);
            Quaternion rotation = Quaternion.AngleAxis(roll, outLocal) * toOut;

            var grip = new GameObject("SwordGrip");
            grip.transform.SetParent(hand, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(sword, grip.transform);
            instance.name = "Sword";
            float handScale = Mathf.Max(0.0001f, hand.lossyScale.x);
            instance.transform.localRotation = rotation;
            instance.transform.localScale = sword.transform.localScale * (SwordScale / handScale);
            instance.transform.localPosition = rotation * (-gripPoint * (SwordScale / handScale));
        }

        private static void Measure(GameObject prefab, out Vector3 blade, out Vector3 flat, out Vector3 grip)
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

            var bounds = new Bounds(vertices.Count > 0 ? vertices[0] : Vector3.zero, Vector3.zero);
            Vector3 centroid = Vector3.zero;
            foreach (Vector3 v in vertices)
            {
                bounds.Encapsulate(v);
                centroid += v;
            }
            centroid /= Mathf.Max(1, vertices.Count);

            Vector3 size = bounds.size;
            int longAxis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            int thinAxis = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
            int flatAxis = 3 - longAxis - thinAxis;
            blade = Vector3.zero;
            blade[longAxis] = centroid[longAxis] >= bounds.center[longAxis] ? -1f : 1f;
            flat = Vector3.zero;
            flat[flatAxis] = 1f;
            Vector3 pommel = bounds.center - blade * (size[longAxis] * 0.5f);
            grip = pommel + blade * (size[longAxis] * GripFraction);
        }
    }
}
