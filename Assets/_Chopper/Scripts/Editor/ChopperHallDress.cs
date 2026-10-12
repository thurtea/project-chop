using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Imports the hand-built hub hall and saves it where Hut.DressCabin loads it.
    // The hammer pieces stay; Hut hides their renderers and parents this mesh under Cabin.
    public static class ChopperHallDress
    {
        const string ArtPath = "Assets/_Chopper/Art/Building/hall_voxel.fbx";
        const string PrefabPath = "Assets/_Chopper/Resources/Props/hall_hub.prefab";

        public static void Apply()
        {
            string export = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Chopper", "Art", "Export", "hall_voxel.fbx"));
            if (!File.Exists(export)) throw new FileNotFoundException(export);
            Directory.CreateDirectory(Path.GetDirectoryName(ArtPath));
            File.Copy(export, ArtPath, true);
            AssetDatabase.ImportAsset(ArtPath);
            var importer = AssetImporter.GetAtPath(ArtPath) as ModelImporter;
            importer.globalScale = 1f;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Chopper/Materials/World/M_World.mat");
            if (mat == null) throw new FileNotFoundException("Missing M_World.");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ArtPath);
            var root = new GameObject("hall_hub");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                model.name = "hall_voxel";
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = mat;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            GameObject temp = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                float minY = float.MaxValue, maxY = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (MeshFilter filter in temp.GetComponentsInChildren<MeshFilter>())
                {
                    foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 p = filter.transform.TransformPoint(vertex);
                        minY = Mathf.Min(minY, p.y);
                        maxY = Mathf.Max(maxY, p.y);
                        minZ = Mathf.Min(minZ, p.z);
                        maxZ = Mathf.Max(maxZ, p.z);
                    }
                }
                bool upright = maxY - minY > 4f && minY > 1f && minY < 3f && maxZ > 5f;
                Debug.Log($"[ChopperHall] y {minY:F2}..{maxY:F2} z {minZ:F2}..{maxZ:F2}");
                Debug.Log("[ChopperHall] RESULT " + (upright ? "PASS" : "FAIL"));
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
            AssetDatabase.SaveAssets();
            NetworkScenesBuilder.UseChopperPlayer();
        }
    }
}
