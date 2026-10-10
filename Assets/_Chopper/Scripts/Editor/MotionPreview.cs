using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Chopper.Editor
{
    // Puts candidate motion clips on Chopper's own body, outside the game: how fast each one's planted foot travels (the
    // player speed it fits without sliding) and eight frames of one cycle seen from the right side. For choosing motions
    // without a player build. Batch mode with graphics:
    // Unity -batchmode -projectPath . -executeMethod Chopper.Editor.MotionPreview.Run -previewOut <folder> -quit
    public static class MotionPreview
    {
        private const string PlayerPrefabPath = "Assets/_Chopper/Prefabs/Player/Player.prefab";
        private const int ShotWidth = 360;
        private const int ShotHeight = 480;
        private const float SampleRate = 120f;

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [MenuItem("Chopper/Debug/Motion Preview")]
        public static void Run()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-previewOut");
            string outDir = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Path.GetTempPath(), "ChopperMotionPreview");
            Directory.CreateDirectory(outDir);
            CharacterSetup.PrepareClips();

            var candidates = new List<(string label, AnimationClip clip)>
            {
                ("q_idle", CharacterSetup.FindClip("Idle_Loop")),
                ("q_walk", CharacterSetup.FindClip("Walk_Loop")),
                ("q_walk_carry", CharacterSetup.FindClip("Walk_Carry_Loop")),
                ("q_jog", CharacterSetup.FindClip("Jog_Fwd_Loop")),
                ("q_sprint", CharacterSetup.FindClip("Sprint_Loop")),
                ("q_crouch_idle", CharacterSetup.FindClip("Crouch_Idle_Loop")),
                ("q_crouch_walk", CharacterSetup.FindClip("Crouch_Fwd_Loop")),
                ("k_run", CharacterSetup.MotionClip("Assets/ThirdParty/KevinIglesias_HumanBasicMotions/HumanM@Run01_Forward.fbx")),
                ("k_sprint", CharacterSetup.MotionClip("Assets/ThirdParty/KevinIglesias_HumanBasicMotions/HumanM@Sprint01_Forward.fbx")),
                ("m_crouch_walk", CharacterSetup.MotionClip(CharacterSetup.MixamoFolder + "/CrouchWalk.fbx")),
                ("m_dwarf_walk", CharacterSetup.MotionClip(CharacterSetup.MixamoFolder + "/DwarfWalk.fbx")),
                ("m_orc_walk", CharacterSetup.MotionClip(CharacterSetup.MixamoFolder + "/OrcWalk.fbx")),
            };

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // The outfit is skinned onto the skeleton when the game starts; do it here too, or the clothes stand still.
            var outfitter = player.GetComponentInChildren<Chopper.Player.CharacterOutfitter>();
            if (outfitter != null) outfitter.ApplyChopper();
            Animator animator = player.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            Transform toeL = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform toeR = animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (toeL == null) toeL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (toeR == null) toeR = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40f, -60f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 3f;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.fieldOfView = 32f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.72f);
            camera.transform.position = new Vector3(4.4f, 0.95f, 0f);
            camera.transform.LookAt(new Vector3(0f, 0.85f, 0f));
            var target = new RenderTexture(ShotWidth, ShotHeight, 24);
            camera.targetTexture = target;

            foreach (var (label, clip) in candidates)
            {
                if (clip == null)
                {
                    Debug.LogWarning($"[MotionPreview] {label}: clip missing");
                    continue;
                }
                PlayableGraph graph = PlayableGraph.Create("MotionPreview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "Out", animator);
                var playable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(playable);

                // A foot on the ground moves backward under the body at the speed the clip walks at: the lower foot's
                // backward speed while it is within 1.5 cm of its lowest, the median. The hips' facing (yaw from the
                // thighs, 0 = straight ahead) shows a clip that walks turned to one side.
                Transform thighL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform thighR = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                int samples = Mathf.CeilToInt(clip.length * SampleRate);
                var points = new List<(float t, Vector3 l, Vector3 r, float yaw)>();
                for (int i = 0; i <= samples; i++)
                {
                    float t = clip.length * i / samples;
                    playable.SetTime(t);
                    graph.Evaluate();
                    Vector3 across = player.transform.InverseTransformDirection(thighR.position - thighL.position);
                    Vector3 facing = Vector3.Cross(across, Vector3.up);
                    points.Add((t, player.transform.InverseTransformPoint(toeL.position), player.transform.InverseTransformPoint(toeR.position),
                        Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg));
                }
                float lowL = points.Min(p => p.l.y), lowR = points.Min(p => p.r.y);
                var speeds = new List<float>();
                for (int i = 1; i < points.Count; i++)
                {
                    float dt = points[i].t - points[i - 1].t;
                    if (dt <= 0f) continue;
                    // The lower foot, while it is down, moving backward (a shuffling sneak keeps the swinging foot low too,
                    // and its forward swing would count otherwise).
                    bool left = points[i].l.y - lowL < points[i].r.y - lowR;
                    float height = left ? points[i].l.y - lowL : points[i].r.y - lowR;
                    if (height > 0.015f) continue;
                    speeds.Add(-((left ? points[i].l.z - points[i - 1].l.z : points[i].r.z - points[i - 1].r.z) / dt));
                }
                speeds.Sort();
                float footSpeed = speeds.Count > 0 ? speeds[speeds.Count / 2] : 0f;
                Vector3 root = clip.averageSpeed;
                Debug.Log($"[MotionPreview] {label}: length {clip.length:F2} s, feet travel {footSpeed:F2} m/s, root motion {new Vector2(root.x, root.z).magnitude:F2} m/s, "
                          + $"hips face {points.Average(p => p.yaw):F0} deg (from {points.Min(p => p.yaw):F0} to {points.Max(p => p.yaw):F0})");

                for (int f = 0; f < 8; f++)
                {
                    playable.SetTime(clip.length * f / 8f);
                    graph.Evaluate();
                    camera.Render();
                    RenderTexture.active = target;
                    var shot = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
                    shot.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(outDir, $"motion_{label}_{f}.png"), shot.EncodeToPNG());
                    Object.DestroyImmediate(shot);
                }
                graph.Destroy();
            }
            Debug.Log("[MotionPreview] done");
        }
    }
}
