using System.IO;
using Chopper.World;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Puts the world-kit grass tufts and bushes into the live meadow. The carpet still instances clumps;
    // each clump is a voxel tuft whose vertex colour is already the Chopper/Grass height channel.
    public static class ChopperFoliageSwap
    {
        const string MeshFolder = "Assets/_Chopper/Art/Meadow";
        const string ResourceFolder = "Assets/_Chopper/Resources/Foliage";
        const string DirectorPath = "Assets/_Chopper/Prefabs/World/EnemyDirector.prefab";

        [MenuItem("Chopper/Art/Use Voxel Grass And Bushes")]
        public static void Apply()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Chopper/Art/Meadow"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Chopper/Resources/Foliage"));
            AssetDatabase.Refresh();

            string[] tufts = { "prop_grass_tuft_voxel_a_carpet", "prop_grass_tuft_voxel_b_carpet", "prop_grass_tuft_voxel_c_carpet" };
            string[] bushes = { "prop_bush_voxel_a", "prop_bush_voxel_b" };
            foreach (string name in tufts) CopyAndImport(name);
            foreach (string name in bushes) CopyAndImport(name);

            Material world = ChopperMeadowSwap.MeadowModel("prop_rock_voxel") != null
                ? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Chopper/Materials/World/M_World.mat")
                : null;
            if (world == null) throw new FileNotFoundException("M_World");

            foreach (string name in tufts) SaveUprightPrefab(name, null);
            GameObject bushA = SaveUprightPrefab(bushes[0], world);
            GameObject bushB = SaveUprightPrefab(bushes[1], world);
            WireBushes(bushA, bushB);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChopperFoliage] grass tufts and voxel bushes wired. RESULT PASS");
        }

        static void CopyAndImport(string name)
        {
            string export = Path.Combine(Application.dataPath, "..", "Chopper", "Art", "Export", name + ".fbx");
            string dest = MeshFolder + "/" + name + ".fbx";
            File.Copy(export, Path.Combine(Application.dataPath, "..", dest), true);
            AssetDatabase.ImportAsset(dest);
            var importer = AssetImporter.GetAtPath(dest) as ModelImporter;
            if (importer == null) throw new FileNotFoundException(dest);
            importer.globalScale = 1f;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        // A plain root. The mesh is baked upright so GrassCarpet can instance it without the FBX root turn.
        static GameObject SaveUprightPrefab(string name, Material material)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(MeshFolder + "/" + name + ".fbx");
            if (fbx == null) throw new FileNotFoundException(name);
            var filter = fbx.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) throw new FileNotFoundException(name + " mesh");
            Mesh source = filter.sharedMesh;
            var baked = new Mesh { name = name };
            var rot = filter.transform.rotation;
            var verts = source.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] = rot * verts[i];
            baked.vertices = verts;
            baked.triangles = source.triangles;
            if (source.colors != null && source.colors.Length == verts.Length) baked.colors = source.colors;
            else if (source.colors32 != null && source.colors32.Length == verts.Length) baked.colors32 = source.colors32;
            baked.RecalculateNormals();
            baked.RecalculateBounds();
            if (baked.bounds.size.y < baked.bounds.size.z)
                Debug.LogWarning($"[ChopperFoliage] {name} is still flat, size {baked.bounds.size}");
            else
                Debug.Log($"[ChopperFoliage] {name} upright {baked.bounds.size.x:F2} x {baked.bounds.size.y:F2} x {baked.bounds.size.z:F2} colors {baked.colors.Length}");

            string meshPath = ResourceFolder + "/" + name + "_mesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(baked, meshPath);

            string prefabPath = ResourceFolder + "/" + name + ".prefab";
            var root = new GameObject(name);
            try
            {
                var view = new GameObject("Mesh");
                view.transform.SetParent(root.transform, false);
                view.AddComponent<MeshFilter>().sharedMesh = baked;
                var renderer = view.AddComponent<MeshRenderer>();
                if (material != null) renderer.sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void WireBushes(GameObject bushA, GameObject bushB)
        {
            var root = PrefabUtility.LoadPrefabContents(DirectorPath);
            try
            {
                var herbs = root.GetComponent<HerbManager>();
                if (herbs == null) throw new FileNotFoundException("HerbManager on " + DirectorPath);
                var so = new SerializedObject(herbs);
                so.FindProperty("_fabricBushModel").objectReferenceValue = bushA;
                so.FindProperty("_foliageBushB").objectReferenceValue = bushB;
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
