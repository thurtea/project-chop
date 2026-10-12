using System.IO;
using Chopper.Core;
using Chopper.World.Building;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Chopper.Editor
{
    // Swaps the hammer pieces' old pixel meshes for the concept 2.0 log and thatch meshes.
    // Colliders, snap points, and the catalog order of the eight original pieces stay.
    // Window and gable are new, and they are appended so old saves keep their piece indexes.
    public static class ChopperPieceDress
    {
        const string PrefabFolder = "Assets/_Chopper/Prefabs/Building";
        const string ConfigFolder = "Assets/_Chopper/Config/Building";
        const string ArtFolder = "Assets/_Chopper/Art/Building";
        const string CatalogPath = ConfigFolder + "/SO_PieceCatalog.asset";

        static readonly string[] Meshes =
        {
            "piece_floor_voxel", "piece_wall_voxel", "piece_doorway_voxel", "piece_window_voxel",
            "piece_roof_voxel", "piece_stairs_voxel", "piece_gable_voxel"
        };

        public static void Apply()
        {
            foreach (string name in Meshes) Import(name);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Chopper/Materials/World/M_World.mat");
            if (mat == null) throw new FileNotFoundException("Missing M_World.");

            Dress("Piece_Floor", "piece_floor_voxel", mat);
            Dress("Piece_Wall", "piece_wall_voxel", mat);
            Dress("Piece_Doorway", "piece_doorway_voxel", mat);
            Dress("Piece_Roof", "piece_roof_voxel", mat);
            Dress("Piece_Stairs", "piece_stairs_voxel", mat);

            GameObject window = NewPrefab("Window", "piece_window_voxel", mat, new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 0.3f));
            GameObject gable = NewPrefab("Gable", "piece_gable_voxel", mat, new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 0.3f));
            Vector3[] wallPoints = { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(-1f, 2f, 0f), new Vector3(1f, 2f, 0f) };
            Vector3[] gablePoints = { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f) };
            Append(Define("Window", "Log window", window, 4, 180, new Vector3(2f, 2f, 0.3f), PieceSnap.Edge, wallPoints));
            Append(Define("Gable", "Log gable", gable, 3, 120, new Vector3(2f, 2f, 0.3f), PieceSnap.Edge, gablePoints));
            AssetDatabase.SaveAssets();

            bool wallUp = Upright(PrefabFolder + "/Piece_Wall.prefab", 1.5f);
            bool roofRises = RisesTowardPlusZ(PrefabFolder + "/Piece_Roof.prefab");
            Debug.Log($"[ChopperPieces] wall upright {wallUp}, roof rises toward +Z {roofRises}");
            Debug.Log("[ChopperPieces] RESULT " + (wallUp && roofRises ? "PASS" : "FAIL"));
        }

        static void Import(string name)
        {
            string export = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Chopper", "Art", "Export", name + ".fbx"));
            string dest = ArtFolder + "/" + name + ".fbx";
            Directory.CreateDirectory(ArtFolder);
            File.Copy(export, dest, true);
            AssetDatabase.ImportAsset(dest);
            var importer = AssetImporter.GetAtPath(dest) as ModelImporter;
            if (importer == null) throw new FileNotFoundException(dest);
            importer.globalScale = 1f;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        static void Dress(string prefabName, string fbxName, Material mat)
        {
            string path = PrefabFolder + "/" + prefabName + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform old = root.transform.Find("Voxel");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                Attach(root.transform, fbxName, mat);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject NewPrefab(string id, string fbxName, Material mat, Vector3 center, Vector3 size)
        {
            var root = new GameObject("Piece_" + id);
            try
            {
                root.AddComponent<PieceView>();
                var box = root.AddComponent<BoxCollider>();
                box.center = center;
                box.size = size;
                var obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = center;
                obstacle.size = size;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                Attach(root.transform, fbxName, mat);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/Piece_" + id + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void Attach(Transform root, string fbxName, Material mat)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/" + fbxName + ".fbx");
            if (fbx == null) throw new FileNotFoundException(fbxName);
            var pivot = new GameObject("Voxel");
            pivot.transform.SetParent(root, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, pivot.transform);
            model.name = fbxName;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = mat;
                renderer.enabled = true;
            }
            if (fbxName == "piece_roof_voxel" || fbxName == "piece_stairs_voxel")
            {
                if (!Rises(pivot.transform)) pivot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }

        static PieceDefinition Define(string id, string display, GameObject prefab, int wood, int health, Vector3 size, PieceSnap snap, Vector3[] points)
        {
            string path = ConfigFolder + "/SO_Piece_" + id + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<PieceDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            definition.Configure(display, prefab, wood, 0, health, size, snap, false, "piece-" + id.ToLowerInvariant(), points);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        static void Append(PieceDefinition definition)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PieceCatalog>(CatalogPath);
            var so = new SerializedObject(catalog);
            SerializedProperty pieces = so.FindProperty("_pieces");
            for (int i = 0; i < pieces.arraySize; i++)
                if (pieces.GetArrayElementAtIndex(i).objectReferenceValue == definition) return;
            pieces.InsertArrayElementAtIndex(pieces.arraySize);
            pieces.GetArrayElementAtIndex(pieces.arraySize - 1).objectReferenceValue = definition;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static bool Upright(string path, float minHeight)
        {
            GameObject temp = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                float min = float.MaxValue, max = float.MinValue;
                foreach (Vector3 p in Points(temp.transform.Find("Voxel")))
                {
                    min = Mathf.Min(min, p.y);
                    max = Mathf.Max(max, p.y);
                }
                Debug.Log($"[ChopperPieces] {path} height {max - min:F2} base {min:F2}");
                return max - min >= minHeight && min > -0.05f && min < 0.35f;
            }
            finally { Object.DestroyImmediate(temp); }
        }

        static bool RisesTowardPlusZ(string path)
        {
            GameObject temp = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                bool rises = Rises(temp.transform.Find("Voxel"));
                Debug.Log($"[ChopperPieces] {path} rises toward +Z {rises}");
                return rises;
            }
            finally { Object.DestroyImmediate(temp); }
        }

        static bool Rises(Transform voxel)
        {
            float high = 0f, low = 0f;
            int highCount = 0, lowCount = 0;
            foreach (Vector3 p in Points(voxel))
            {
                if (p.z > 0.25f) { high += p.y; highCount++; }
                else if (p.z < -0.25f) { low += p.y; lowCount++; }
            }
            return highCount > 0 && lowCount > 0 && high / highCount > low / lowCount + 0.4f;
        }

        static System.Collections.Generic.List<Vector3> Points(Transform voxel)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            foreach (MeshFilter filter in voxel.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    points.Add(filter.transform.TransformPoint(vertex));
            }
            return points;
        }
    }
}
