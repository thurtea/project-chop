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
            MeadowBuilder.BuildCampfirePrefab();
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

        static GameObject PropPrefab(string prefabName, Material mat, string fbxName = null)
        {
            fbxName ??= prefabName;
            string path = MeshFolder + "/" + prefabName + ".prefab";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(MeshFolder + "/" + fbxName + ".fbx");
            if (fbx == null) throw new FileNotFoundException(fbxName);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                inst.name = prefabName;
                foreach (var renderer in inst.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = mat;
                return PrefabUtility.SaveAsPrefabAsset(inst, path);
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
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
