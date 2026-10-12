using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chopper.Editor
{
    // Batch: Chopper.Editor.ChopperPropDress.Apply
    // Swaps the old torch and sign looks for the concept 2.0 voxel props. The yard torches (Hut.prefab), the
    // standing torch piece (Piece_Torch.prefab) and the hub notice board (World.unity) keep their components,
    // colliders, lights and flames; their old renderers are hidden and the voxel mesh goes under a child named
    // Voxel. The tutorial yard signs are built at runtime and load Resources/Props/prop_sign_voxel.
    // The held torch is not changed.
    public static class ChopperPropDress
    {
        const string ArtFolder = "Assets/_Chopper/Art/World";
        const string PropsResources = "Assets/_Chopper/Resources/Props";
        const string HutPrefab = "Assets/_Chopper/Prefabs/World/Hut.prefab";
        const string TorchPiecePrefab = "Assets/_Chopper/Prefabs/Building/Piece_Torch.prefab";
        const string WorldScene = "Assets/_Chopper/Scenes/World.unity";
        const string Torch = "prop_torch_voxel";
        const string SignMesh = "prop_sign_voxel";
        // The notice board was 1.3 m wide on a 2 m post; the voxel sign is 1 m wide.
        const float NoticeScale = 1.3f;

        public static void Apply()
        {
            Import(Torch);
            Import(SignMesh);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Chopper/Materials/World/M_World.mat");
            if (mat == null) throw new FileNotFoundException("Missing M_World.");

            int yard = DressPrefab(HutPrefab, "NightTorch", mat);
            int piece = DressPrefab(TorchPiecePrefab, null, mat);
            bool notice = DressNotice(mat);
            bool signPrefab = SaveSignPrefab(mat);

            float torchHeight = Height(AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/" + Torch + ".fbx"));
            float signHeight = Height(AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/" + SignMesh + ".fbx"));
            bool upright = torchHeight > 2.1f && torchHeight < 2.4f && signHeight > 1.5f && signHeight < 1.7f;
            Debug.Log($"[ChopperProps] yard torches {yard}, torch piece {piece}, notice board {notice}, sign prefab {signPrefab}");
            Debug.Log($"[ChopperProps] torch height {torchHeight:F2} m, sign height {signHeight:F2} m, upright {upright}");
            bool pass = yard == 6 && piece == 1 && notice && signPrefab && upright;
            Debug.Log("[ChopperProps] RESULT " + (pass ? "PASS" : "FAIL"));
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
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        // Each torch root (named rootName, or the prefab root when null) hides its TorchModel and gets the voxel torch.
        // The voxel ember bed sits at 2.125 m, where the old flame already burns (2.2 m post, flame 0.08 m below its top).
        static int DressPrefab(string path, string rootName, Material mat)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int count = 0;
            try
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (rootName != null ? t.name != rootName : t != root.transform) continue;
                    Transform model = t.Find("TorchModel");
                    if (model == null) continue;
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    Transform old = t.Find("Voxel");
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    Attach(t, Torch, mat, Quaternion.Euler(0f, model.localEulerAngles.y, 0f), 1f);
                    count++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return count;
        }

        static bool DressNotice(Material mat)
        {
            var scene = EditorSceneManager.OpenScene(WorldScene, OpenSceneMode.Single);
            GameObject board = GameObject.Find("NoticeSign");
            if (board == null) return false;
            Transform old = board.transform.Find("Voxel");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (Renderer r in board.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Attach(board.transform, SignMesh, mat, Quaternion.identity, NoticeScale);
            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }

        // A plain root with the model under it, like the foliage prefabs, so callers can turn the root freely.
        static bool SaveSignPrefab(Material mat)
        {
            Directory.CreateDirectory(PropsResources);
            var root = new GameObject(SignMesh);
            try
            {
                Attach(root.transform, SignMesh, mat, Quaternion.identity, 1f);
                return PrefabUtility.SaveAsPrefabAsset(root, PropsResources + "/" + SignMesh + ".prefab") != null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void Attach(Transform parent, string fbxName, Material mat, Quaternion rotation, float scale)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/" + fbxName + ".fbx");
            if (fbx == null) throw new FileNotFoundException(fbxName);
            var pivot = new GameObject("Voxel");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localRotation = rotation;
            pivot.transform.localScale = Vector3.one * scale;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, pivot.transform);
            model.name = fbxName;
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.enabled = true;
            }
        }

        static float Height(GameObject fbx)
        {
            if (fbx == null) return 0f;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                var bounds = new Bounds();
                bool any = false;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    if (!any) bounds = r.bounds;
                    else bounds.Encapsulate(r.bounds);
                    any = true;
                }
                return any ? bounds.size.y : 0f;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
