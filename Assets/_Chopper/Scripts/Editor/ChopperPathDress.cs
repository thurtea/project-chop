using System.IO;
using Chopper.World;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Copies the layered pine and the path stones into the live world and points the hub at them.
    public static class ChopperPathDress
    {
        const string MeshFolder = "Assets/_Chopper/Art/Meadow";
        const string DirectorPath = "Assets/_Chopper/Prefabs/World/EnemyDirector.prefab";

        public static void Apply()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Chopper/Art/Meadow"));
            CopyFbx("prop_pine_voxel");
            CopyFbx("prop_path_stone_voxel");
            ChopperMeadowSwap.Apply();
            GameObject stones = PathPrefab();
            Wire(stones);
            AssetDatabase.SaveAssets();
            Debug.Log("[HubPaths] path stones wired on the hub. RESULT PASS");
        }

        static void CopyFbx(string name)
        {
            string export = Path.Combine(Application.dataPath, "..", "Chopper", "Art", "Export", name + ".fbx");
            string dest = Path.Combine(Application.dataPath, "..", MeshFolder, name + ".fbx");
            if (!File.Exists(export)) throw new FileNotFoundException(export);
            File.Copy(export, dest, true);
            AssetDatabase.ImportAsset(MeshFolder + "/" + name + ".fbx");
        }

        static GameObject PathPrefab()
        {
            string fbxPath = MeshFolder + "/prop_path_stone_voxel.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) throw new FileNotFoundException(fbxPath);
            importer.globalScale = 1f;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            Material world = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Chopper/Materials/World/M_World.mat");
            if (world == null) throw new FileNotFoundException("M_World");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            string path = MeshFolder + "/prop_path_stone_voxel.prefab";
            var root = new GameObject("prop_path_stone_voxel");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                model.name = "prop_path_stone_voxel";
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = world;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void Wire(GameObject stones)
        {
            var root = PrefabUtility.LoadPrefabContents(DirectorPath);
            try
            {
                HubPathStones paths = root.GetComponent<HubPathStones>();
                if (paths == null) paths = root.AddComponent<HubPathStones>();
                var so = new SerializedObject(paths);
                so.FindProperty("_model").objectReferenceValue = stones;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DirectorPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
