using System.IO;
using Chopper.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Chopper.Editor
{
    // Development helper: renders the player with the guard pose (axe raised) from several sides, next to the plain
    // idle pose, so the pose can be judged and tuned without playing. Needs a GPU (do not pass -nographics).
    // Batch: -executeMethod Chopper.Editor.GuardPoseCapture.Capture -poseOutput <folder>
    public static class GuardPoseCapture
    {
        private const int Size = 640;

        [MenuItem("Chopper/Setup/Capture Guard Pose")]
        public static void Capture()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ChopperGuard");
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-poseOutput") folder = args[i + 1];
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

            var holder = new GameObject("Player");
            CharacterSetup.Result character = CharacterSetup.AttachModel(holder);
            GameObject model = character.Animator.gameObject;
            var guard = model.GetComponent<GuardPose>();

            // The player faces +Z; the camera yaw is measured from the front (0 looks at the face). Pitch and distance
            // match the game camera for the "game" views. The guard shots run the real Animator with Blocking on, so the
            // legs and the upper body layer are what the game plays, and then the procedural pose goes on top.
            (string name, float yaw, bool guarded, float pitch, float distance)[] shots =
            {
                ("idle_front", 0f, false, 8f, 3.6f), ("guard_front", 0f, true, 8f, 3.6f), ("guard_left", 90f, true, 8f, 3.6f),
                ("guard_right", -90f, true, 8f, 3.6f), ("game_behind", 180f, true, 24f, 4.6f), ("game_behind34", 150f, true, 24f, 4.6f),
                ("game_front", 20f, true, 24f, 4.6f), ("game_idle_behind", 180f, false, 24f, 4.6f)
            };
            foreach (var shot in shots)
            {
                Animator animator = character.Animator;
                animator.Rebind();
                animator.SetBool("Blocking", shot.guarded);
                animator.Update(0f);
                for (int step = 0; step < 24; step++) animator.Update(0.05f);
                if (shot.guarded && guard != null) guard.Evaluate(1f);
                Render(camera, model, Path.Combine(folder, shot.name + ".png"), shot.yaw, shot.pitch, shot.distance);
            }
            Debug.Log("[GuardPoseCapture] Wrote " + shots.Length + " pictures to " + folder);
        }

        private static void Sample(GameObject model, string clipName, float time)
        {
            AnimationClip clip = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CharacterSetup.ModelPath))
            {
                if (o is AnimationClip c && c.name == "Armature|" + clipName) clip = c;
            }
            if (clip != null) clip.SampleAnimation(model, time);
        }

        private static void Render(Camera camera, GameObject model, string path, float yaw, float pitch = 8f, float distance = 3.6f)
        {
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.forceMatrixRecalculationPerRender = true;
                smr.updateWhenOffscreen = true;
                var baked = new Mesh();
                smr.BakeMesh(baked);
                Object.DestroyImmediate(baked);
            }
            EditorApplication.QueuePlayerLoopUpdate();

            Vector3 target = new Vector3(0f, 1.0f, 0f);
            Quaternion rotation = Quaternion.Euler(pitch, 180f + yaw, 0f);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);

            var rt = new RenderTexture(Size, Size, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }
}
