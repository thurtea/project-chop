using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Chopper.Editor
{
    // Development helper: renders pack models side by side into one PNG so they can be judged without playing.
    // Needs a GPU (do not pass -nographics).
    // Batch: -executeMethod Chopper.Editor.PixelPackSheet.Render -sheetModels a,b,c -sheetOutput <file.png> [-sheetYaw 30]
    //        Models are names of FBX files in the pixel pack mesh folder, or full asset paths, or prefab paths.
    public static class PixelPackSheet
    {
        private const int Tile = 320;
        private const int Columns = 4;

        public static void Render()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string models = Arg(args, "-sheetModels");
            string output = Arg(args, "-sheetOutput") ?? Path.Combine(Path.GetTempPath(), "pixel_sheet.png");
            float yaw = float.TryParse(Arg(args, "-sheetYaw"), out float y) ? y : 30f;
            string[] names = models.Split(',');

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(45f, 140f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            var camGo = new GameObject("Camera");
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.5f, 0.6f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            int rows = (names.Length + Columns - 1) / Columns;
            var sheet = new Texture2D(Columns * Tile, rows * Tile, TextureFormat.RGB24, false);
            for (int i = 0; i < names.Length; i++)
            {
                GameObject asset = Load(names[i].Trim());
                var tile = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
                if (asset != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    Bounds bounds = BoundsOf(instance);
                    float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
                    Quaternion rotation = Quaternion.Euler(18f, yaw, 0f);
                    camera.orthographic = true;
                    camera.orthographicSize = radius * 1.05f;
                    camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * (radius * 3f + 5f), rotation);
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = radius * 8f + 20f;

                    var rt = new RenderTexture(Tile, Tile, 24);
                    camera.targetTexture = rt;
                    camera.Render();
                    RenderTexture.active = rt;
                    tile.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
                    camera.targetTexture = null;
                    RenderTexture.active = null;
                    Object.DestroyImmediate(rt);
                    Object.DestroyImmediate(instance);
                }
                int col = i % Columns;
                int row = rows - 1 - i / Columns;
                sheet.SetPixels(col * Tile, row * Tile, Tile, Tile, tile.GetPixels());
                Object.DestroyImmediate(tile);
                Debug.Log($"[PixelPackSheet] {i}: {names[i]}{(asset == null ? " (missing)" : "")}");
            }
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            Debug.Log("[PixelPackSheet] Wrote " + output);
        }

        private static GameObject Load(string name)
        {
            string path = name;
            if (!name.EndsWith(".fbx") && !name.EndsWith(".prefab"))
            {
                path = AssetDatabase.FindAssets(name + " t:Model", new[] { PixelPackSetup.Root })
                    .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
            }
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static string Arg(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
