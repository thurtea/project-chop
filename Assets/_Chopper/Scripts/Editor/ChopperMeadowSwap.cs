using System.IO;
using Chopper.World.Generation;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Points the live world's tree and rock slots at the hand-built meadow props.
    // Forest color stays a tint on the one pine, because each zone used to be a recolor of the pack pine.
    public static class ChopperMeadowSwap
    {
        public const string MeshFolder = "Assets/_Chopper/Art/Meadow";
        const string ShaderPath = "Assets/_Chopper/Shaders/ChopperVertexColorLit.shader";
        const string WorldAssetsPath = "Assets/_Chopper/Config/SO_WorldAssets.asset";

        [MenuItem("Chopper/Art/Use Chopper Meadow Props")]
        public static void Apply()
        {
            var assets = AssetDatabase.LoadAssetAtPath<WorldAssets>(WorldAssetsPath);
            if (assets == null) throw new FileNotFoundException("Missing " + WorldAssetsPath);
            Retarget(assets);
            WireStump();
            assets.Campfire = MeadowBuilder.BuildCampfirePrefab();
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChopperMeadow] trees and rocks now use the voxel pine and rock. Campfire rebuilt.");
        }

        public static void Retarget(WorldAssets assets)
        {
            if (assets.TreeModels == null || assets.RockModels == null) return;
            EnsureImported("prop_pine_voxel");
            EnsureImported("prop_rock_voxel");
            EnsureImported("prop_log_voxel");
            EnsureImported("prop_stump_voxel");

            Material pine = VertexMaterial("M_Pine", Color.white);
            GameObject rock = PropPrefab("prop_rock_voxel", VertexMaterial("M_World", Color.white));
            for (int i = 0; i < assets.RockModels.Length; i++)
                assets.RockModels[i] = rock;

            if (assets.TreeModelHeights == null || assets.TreeModelHeights.Length != assets.TreeModels.Length)
                assets.TreeModelHeights = new float[assets.TreeModels.Length];

            for (int i = 0; i < assets.TreeModels.Length; i++)
            {
                string oldName = assets.TreeModels[i] != null ? assets.TreeModels[i].name : "";
                Material tinted = VertexMaterial("M_Pine_" + TintKey(oldName), Tint(oldName));
                GameObject prefab = PropPrefab("prop_pine_" + TintKey(oldName), tinted, "prop_pine_voxel");
                assets.TreeModels[i] = prefab;
                assets.TreeModelHeights[i] = MeasureHeight(prefab);
                Debug.Log($"[ChopperMeadow] tree {i} '{oldName}' -> {prefab.name} height {assets.TreeModelHeights[i]:F2} m tint {TintKey(oldName)}");
            }
        }

        const string TreeManagerPath = "Assets/_Chopper/Prefabs/World/TreeManager.prefab";

        // Felled trees spawn this instead of a cylinder. Safe to run on its own.
        public static void WireStump()
        {
            EnsureImported("prop_stump_voxel");
            GameObject stump = StumpPrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreeManagerPath);
            if (prefab == null) throw new FileNotFoundException("Missing " + TreeManagerPath);
            var manager = prefab.GetComponent<Chopper.Trees.TreeManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("_stumpPrefab").objectReferenceValue = stump;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChopperMeadow] felled trees use prop_stump_voxel.");
        }

        public static GameObject StumpPrefab()
        {
            GameObject prefab = PropPrefab("prop_stump_voxel", VertexMaterial("M_World", Color.white));
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
            try
            {
                if (root.GetComponent<CapsuleCollider>() == null)
                {
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.center = new Vector3(0f, 0.375f, 0f);
                    capsule.height = 0.75f;
                    capsule.radius = 0.375f;
                    capsule.direction = 1;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GetAssetPath(prefab));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static GameObject MeadowModel(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(MeshFolder + "/" + name + ".fbx");
        }

        static void EnsureImported(string name)
        {
            string path = MeshFolder + "/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("Missing " + path);
            importer.globalScale = 1f;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        static Material VertexMaterial(string matName, Color tint)
        {
            string path = "Assets/_Chopper/Materials/World/" + matName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new FileNotFoundException("Missing " + ShaderPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = matName };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            mat.SetFloat("_UseAtlas", 0f);
            mat.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // The Blender export (axis_up Y, bake_space_transform off) leaves the Z-up to Y-up turn on the FBX's root node:
        // the imported model's root carries +90 degrees about X and its vertices lie on their side. Code that
        // instantiates a prefab with its own rotation (TreeManager and FallingTree: yaw only) replaces the root's
        // rotation and so threw that turn away, laying every tree down. So each prop prefab gets a plain root (no
        // rotation, no scale, origin at the model's base) with the model, and its turn, as a child.
        static GameObject PropPrefab(string prefabName, Material mat, string fbxName = null)
        {
            fbxName ??= prefabName;
            string path = MeshFolder + "/" + prefabName + ".prefab";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(MeshFolder + "/" + fbxName + ".fbx");
            if (fbx == null) throw new FileNotFoundException(fbxName);
            var root = new GameObject(prefabName);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                model.name = fbxName;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = mat;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Batch: Apply, then CheckOrientation. Logs "[ChopperMeadow] RESULT PASS" or "FAIL".
        public static void ApplyAndCheck()
        {
            Apply();
            CheckOrientation();
        }

        // Every prop is authored standing on Z = 0 with its origin at the base (build_pine.py, build_meadow.py), so a
        // prop placed the way the game places it (yaw only, like TreeManager) is upright when its bottom is at the
        // pivot's height and its footprint is centered on it. Logs each prop's size and "[ChopperMeadow] RESULT".
        [MenuItem("Chopper/Art/Check Meadow Prop Orientation")]
        public static void CheckOrientation()
        {
            var assets = AssetDatabase.LoadAssetAtPath<WorldAssets>(WorldAssetsPath);
            int failures = 0;
            void Check(string label, GameObject prefab)
            {
                if (prefab == null) { Debug.LogError($"[ChopperMeadow] {label}: missing"); failures++; return; }
                GameObject temp = Object.Instantiate(prefab, Vector3.zero, Quaternion.Euler(0f, 37f, 0f));
                try
                {
                    Bounds b = PropBounds(temp);
                    float wide = Mathf.Max(b.size.x, b.size.z);
                    bool upright = Mathf.Abs(b.min.y) < 0.02f && new Vector2(b.center.x, b.center.z).magnitude < 0.25f * wide + 0.05f;
                    if (!upright) failures++;
                    Debug.Log($"[ChopperMeadow] {label} ({prefab.name}): {(upright ? "upright" : "NOT UPRIGHT")}, size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m, bottom {b.min.y:F2}, center {b.center:F2}, root rotation {prefab.transform.localEulerAngles:F0}");
                }
                finally { Object.DestroyImmediate(temp); }
            }
            if (assets != null)
            {
                for (int i = 0; i < assets.TreeModels.Length; i++) Check($"tree {i} (height {assets.TreeModelHeights[i]:F2})", assets.TreeModels[i]);
                for (int i = 0; i < assets.RockModels.Length; i++) Check($"rock {i}", assets.RockModels[i]);
            }
            var manager = AssetDatabase.LoadAssetAtPath<GameObject>(TreeManagerPath)?.GetComponent<Chopper.Trees.TreeManager>();
            var stump = manager != null ? new SerializedObject(manager).FindProperty("_stumpPrefab").objectReferenceValue as GameObject : null;
            Check("stump", stump);
            if (stump != null)
            {
                var capsule = stump.GetComponent<CapsuleCollider>();
                Debug.Log($"[ChopperMeadow] stump collider on root: {(capsule != null ? $"center {capsule.center:F2}, height {capsule.height:F2}, radius {capsule.radius:F2}, axis {capsule.direction}" : "none")}");
            }
            // The log model is only used inside the campfire, placed under a pivot (it keeps its own root turn there).
            var logModel = AssetDatabase.LoadAssetAtPath<GameObject>(MeshFolder + "/prop_log_voxel.fbx");
            if (logModel != null)
            {
                var pivot = new GameObject("LogCheck");
                Object.Instantiate(logModel, pivot.transform, false);
                Bounds b = PropBounds(pivot);
                Debug.Log($"[ChopperMeadow] log model under a plain pivot: size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m, bottom {b.min.y:F2} (authored 2 m along X, lying)");
                Object.DestroyImmediate(pivot);
            }
            // The campfire: each stone and log in place, as built by MeadowBuilder.BuildCampfirePrefab.
            GameObject campfire = assets != null ? assets.Campfire : null;
            if (campfire != null)
            {
                foreach (Transform part in campfire.transform)
                {
                    if (part.name != "Stone" && part.name != "Log") continue;
                    Bounds b = PropBounds(part.gameObject);
                    Debug.Log($"[ChopperMeadow] campfire {part.name}: size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m, from {b.min.y:F2} to {b.max.y:F2} m up");
                }
            }
            Debug.Log($"[ChopperMeadow] RESULT {(failures == 0 ? "PASS" : "FAIL")} ({failures} props not upright)");
        }

        static Bounds PropBounds(GameObject go)
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

        static float MeasureHeight(GameObject model)
        {
            GameObject temp = Object.Instantiate(model);
            try
            {
                var bounds = new Bounds();
                bool first = true;
                foreach (Renderer renderer in temp.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                return Mathf.Max(0.1f, bounds.size.y);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        static string TintKey(string modelName)
        {
            // Already retargeted (prop_pine_<key>): keep the key, so running Retarget again keeps every zone's tint.
            if (modelName.StartsWith("prop_pine_")) return modelName.Substring("prop_pine_".Length);
            string n = modelName.ToLowerInvariant();
            if (n.Contains("blue")) return "Water";
            if (n.Contains("orange")) return "FireOrange";
            if (n.Contains("red")) return "Fire";
            if (n.Contains("yellow")) return "EarthYellow";
            if (n.Contains("lightgreen")) return "Wind";
            if (n.Contains("dark")) return "Earth";
            if (n.Contains("pink")) return "HubPink";
            if (n.Contains("tree2")) return "HubOak";
            return "Hub";
        }

        static Color Tint(string modelName)
        {
            switch (TintKey(modelName))
            {
                case "Water": return new Color(0.55f, 0.75f, 1f);
                case "Fire": return new Color(1f, 0.45f, 0.35f);
                case "FireOrange": return new Color(1f, 0.72f, 0.35f);
                case "Earth": return new Color(0.55f, 0.62f, 0.4f);
                case "EarthYellow": return new Color(1f, 0.92f, 0.45f);
                case "Wind": return new Color(0.75f, 1f, 0.72f);
                case "HubPink": return new Color(1f, 0.78f, 0.82f);
                case "HubOak": return new Color(0.9f, 0.95f, 0.75f);
                default: return Color.white;
            }
        }
    }
}
