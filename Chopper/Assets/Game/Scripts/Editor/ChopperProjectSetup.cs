using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Chopper.EditorTools
{
    /// <summary>
    /// Batch-mode helpers for the Chopper project.
    /// Build the test scene:  Unity.exe -batchmode -quit -projectPath Chopper -executeMethod Chopper.EditorTools.ChopperProjectSetup.CreateTestScene
    /// Verify the project:    Unity.exe -batchmode -quit -projectPath Chopper -executeMethod Chopper.EditorTools.ChopperProjectSetup.VerifyProject
    /// </summary>
    public static class ChopperProjectSetup
    {
        public const string TestScenePath = "Assets/Game/Scenes/TestScene.unity";

        [MenuItem("Chopper/Create Test Scene")]
        public static void CreateTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 20 x 20 m ground (the built-in Plane is 10 x 10 m).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            ground.isStatic = true;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Third-person view of the origin: character faces +Z, camera sits behind it on -Z.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 1.8f, -3.5f);
            camGo.transform.LookAt(new Vector3(0f, 1f, 0f));

            var spawn = new GameObject("CharacterSpawn");
            spawn.transform.position = Vector3.zero;
            spawn.transform.rotation = Quaternion.identity;

            if (!EditorSceneManager.SaveScene(scene, TestScenePath))
            {
                Debug.LogError("[ChopperSetup] Failed to save " + TestScenePath);
                return;
            }

            var others = EditorBuildSettings.scenes.Where(s => s.path != TestScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TestScenePath, true) }
                .Concat(others).ToArray();
            AssetDatabase.SaveAssets();

            Debug.Log("[ChopperSetup] Saved " + TestScenePath + " and set it as build scene 0.");
        }

        public static void VerifyProject()
        {
            var defaultRp = GraphicsSettings.defaultRenderPipeline;
            var qualityRp = QualitySettings.renderPipeline;
            var currentRp = GraphicsSettings.currentRenderPipeline;
            Debug.Log($"[ChopperVerify] Default RP asset: {Describe(defaultRp)}");
            Debug.Log($"[ChopperVerify] Quality level '{QualitySettings.names[QualitySettings.GetQualityLevel()]}' RP asset: {Describe(qualityRp)}");
            Debug.Log($"[ChopperVerify] Active RP asset: {Describe(currentRp)}");
            bool isUrp = currentRp != null && currentRp.GetType().Name == "UniversalRenderPipelineAsset";
            Debug.Log($"[ChopperVerify] Active RP is UniversalRenderPipelineAsset: {isUrp}");

            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            Debug.Log($"[ChopperVerify] Scene loaded: {scene.path} valid={scene.IsValid()} loaded={scene.isLoaded} roots=[{string.Join(", ", roots.Select(r => r.name))}]");

            var cam = roots.FirstOrDefault(r => r.name == "Main Camera");
            var ground = roots.FirstOrDefault(r => r.name == "Ground");
            var spawn = roots.FirstOrDefault(r => r.name == "CharacterSpawn");
            if (cam != null) Debug.Log($"[ChopperVerify] Camera pos={cam.transform.position} forward={cam.transform.forward}");
            if (ground != null) Debug.Log($"[ChopperVerify] Ground bounds size={ground.GetComponent<Renderer>().bounds.size}");
            if (spawn != null) Debug.Log($"[ChopperVerify] CharacterSpawn pos={spawn.transform.position}");

            var buildScenes = EditorBuildSettings.scenes;
            Debug.Log($"[ChopperVerify] Build scene 0: {(buildScenes.Length > 0 ? buildScenes[0].path : "<none>")}");

            bool ok = isUrp && scene.isLoaded && cam != null && ground != null && spawn != null
                      && buildScenes.Length > 0 && buildScenes[0].path == TestScenePath;
            Debug.Log($"[ChopperVerify] RESULT: {(ok ? "PASS" : "FAIL")}");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static string Describe(RenderPipelineAsset rp) =>
            rp == null ? "<none>" : $"{rp.name} ({rp.GetType().Name})";
    }
}
