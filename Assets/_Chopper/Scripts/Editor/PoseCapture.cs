using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Chopper.Editor
{
    // Development helper: renders the Player prefab in a few animation poses to PNG files so the model, its facing
    // and the axe grip can be checked without playing. Needs a GPU (do not pass -nographics).
    // Batch: -executeMethod Chopper.Editor.PoseCapture.Capture -poseOutput <folder>
    public static class PoseCapture
    {
        private const string PlayerPrefabPath = "Assets/_Chopper/Prefabs/Player/Player.prefab";
        private const string OutputArg = "-poseOutput";
        private const int Size = 768;

        [MenuItem("Chopper/Setup/Capture Player Poses")]
        public static void Capture()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ChopperPoses");
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == OutputArg) folder = args[i + 1];
            Directory.CreateDirectory(folder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);

            var camGo = new GameObject("Camera");
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.65f, 0.75f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            int[] modes = { 0 };
            foreach (int mode in modes)
            {
                var holder = new GameObject($"Player_mode{mode}");
                CharacterSetup.Result character = CharacterSetup.AttachModel(holder);
                GameObject model = character.Animator.gameObject;
                Shoot(camera, model, folder, $"m{mode}_front_idle", "Idle_Loop", 0.4f, 0f);
                Shoot(camera, model, folder, $"m{mode}_side_idle", "Idle_Loop", 0.4f, 90f);
                Shoot(camera, model, folder, $"m{mode}_side_chop_a", "Sword_Attack", 0.45f, 90f);
                Shoot(camera, model, folder, $"m{mode}_side_chop_b", "Sword_Attack", 0.75f, 90f);
                Shoot(camera, model, folder, $"m{mode}_front_chop", "Sword_Attack", 0.6f, 0f);
                Object.DestroyImmediate(holder);
            }
            Debug.Log($"[PoseCapture] Wrote poses to {folder}");
        }

        // Evaluates the real Player prefab's Animator controller (not raw clips) to check that it drives the model.
        [MenuItem("Chopper/Setup/Capture Animator States")]
        public static void CaptureAnimatorStates()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ChopperPoses");
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == OutputArg) folder = args[i + 1];
            Directory.CreateDirectory(folder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            var camGo = new GameObject("Camera");
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.65f, 0.75f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Animator animator = player.GetComponentInChildren<Animator>();
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true)) if (t.name == "Torch") t.gameObject.SetActive(true);
            Debug.Log($"[PoseCapture] animator enabled={animator.enabled} controller={(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NULL")} avatar={(animator.avatar != null ? animator.avatar.name : "NULL")} valid={animator.avatar != null && animator.avatar.isValid} layers={animator.layerCount}");
            animator.Rebind();
            animator.Update(0f);

            // Does the chop layer move the arm? Compare the right hand at idle with a quarter second into a swing.
            Transform rightHand = FindBone(player.transform, "hand_r");
            Transform legBone = FindBone(player.transform, "foot_l");
            animator.SetFloat("Speed", 0f);
            animator.SetBool("Grounded", true);
            for (int i = 0; i < 20; i++) animator.Update(0.05f);
            Vector3 idleHand = player.transform.InverseTransformPoint(rightHand.position);
            Vector3 idleFoot = player.transform.InverseTransformPoint(legBone.position);
            animator.SetFloat("SwingSpeed", 2.6f);
            animator.SetTrigger("Swing");
            for (int i = 0; i < 8; i++) animator.Update(0.03f);
            Vector3 swingHand = player.transform.InverseTransformPoint(rightHand.position);
            Vector3 swingFoot = player.transform.InverseTransformPoint(legBone.position);
            Debug.Log($"[PoseCapture] chop test: right hand moved {Vector3.Distance(idleHand, swingHand):F2} m, left foot moved {Vector3.Distance(idleFoot, swingFoot):F2} m");
            animator.Rebind();
            animator.Update(0f);

            foreach (var state in new (string name, float speed, bool crouch, bool down, bool swing)[]
                     {
                         ("idle", 0f, false, false, false),
                         ("jog", 5f, false, false, false),
                         ("sprint", 9f, false, false, false),
                         ("crouch", 0f, true, false, false),
                         ("crouchwalk", 2.5f, true, false, false),
                         ("down", 0f, false, true, false),
                         ("swing", 0f, false, false, true)
                     })
            {
                animator.SetFloat("Speed", state.speed);
                if (HasParameter(animator, "Crouching")) animator.SetBool("Crouching", state.crouch);
                animator.SetBool("Down", state.down);
                animator.SetBool("Grounded", true);
                if (state.swing) animator.SetTrigger("Swing");
                for (int i = 0; i < 30; i++) animator.Update(0.05f);
                Debug.Log($"[PoseCapture] state {state.name}: hand_r y={FindBone(player.transform, "hand_r")?.position.y:F2}");
                Shoot(camera, animator.gameObject, folder, "anim_" + state.name, null, 0f, 90f);
            }
        }

        private static bool HasParameter(Animator animator, string name)
        {
            foreach (AnimatorControllerParameter p in animator.parameters) if (p.name == name) return true;
            return false;
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private static void Shoot(Camera camera, GameObject model, string folder, string name, string clipName, float time, float yaw)
        {
            AnimationClip clip = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CharacterSetup.ModelPath))
            {
                if (o is AnimationClip c && c.name == "Armature|" + clipName) clip = c;
            }
            if (clip != null) clip.SampleAnimation(model, time);
            // In edit mode the skinned mesh lags one sample behind the bones unless it is told to refresh.
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.forceMatrixRecalculationPerRender = true;
                smr.updateWhenOffscreen = true;
                Mesh baked = new Mesh();
                smr.BakeMesh(baked);
                Object.DestroyImmediate(baked);
            }
            EditorApplication.QueuePlayerLoopUpdate();

            Vector3 target = new Vector3(0f, 1f, 0f);
            Quaternion rotation = Quaternion.Euler(12f, 180f + yaw, 0f);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 4.2f, rotation);

            var rt = new RenderTexture(Size, Size, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }
}
