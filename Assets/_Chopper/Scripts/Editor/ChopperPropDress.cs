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
        const string PieceWorkbenchPrefab = "Assets/_Chopper/Prefabs/Building/Piece_Workbench.prefab";
        const string WorldAssetsPath = "Assets/_Chopper/Config/SO_WorldAssets.asset";
        const string PropPrefabFolder = "Assets/_Chopper/Prefabs/World";
        static readonly string[] Furniture =
            { "prop_chest_voxel", "prop_chest_grand_voxel", "prop_bed_voxel", "prop_workbench_voxel", "prop_fireplace_voxel" };
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

            // Furniture (build_furniture.py): the house's bed, chest and fireplace, the yard and hammer workbenches, and
            // the crypt chests.
            foreach (string name in Furniture) Import(name);
            int house = DressFurniture(HutPrefab, mat, ("Bed", "prop_bed_voxel"), ("StorageChest", "prop_chest_voxel"), ("Fireplace", "prop_fireplace_voxel"));
            int benchPiece = DressFurniture(PieceWorkbenchPrefab, mat, (null, "prop_workbench_voxel"));
            bool yardBench = DressSceneFurniture("HutYard/Workbench", "prop_workbench_voxel", mat);
            bool cryptChests = SwapCryptChests(mat);
            int sills = RemoveDoorSill();
            int treads = StairTreads();
            Debug.Log($"[ChopperProps] house furniture {house}, workbench piece {benchPiece}, yard workbench {yardBench}, crypt chests {cryptChests}, doorway sills removed {sills}, stair treads {treads}");

            float torchHeight = Height(AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/" + Torch + ".fbx"));
            float signHeight = Height(AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/" + SignMesh + ".fbx"));
            bool upright = torchHeight > 2.1f && torchHeight < 2.4f && signHeight > 1.5f && signHeight < 1.7f;
            Debug.Log($"[ChopperProps] yard torches {yard}, torch piece {piece}, notice board {notice}, sign prefab {signPrefab}");
            Debug.Log($"[ChopperProps] torch height {torchHeight:F2} m, sign height {signHeight:F2} m, upright {upright}");
            bool pass = yard == 6 && piece == 1 && notice && signPrefab && upright && house == 3 && benchPiece == 1 && yardBench && cryptChests && treads == TreadCount;
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
                // Collected first: dressing deletes the previous run's Voxel children.
                var targets = new System.Collections.Generic.List<Transform>();
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (rootName != null ? t.name == rootName : t == root.transform) targets.Add(t);
                foreach (Transform t in targets)
                {
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

        // Hides the old meshes of each named child (null: the prefab root) and puts the voxel model under it at true size.
        // Particle systems and lights stay, so the fireplace keeps its fire.
        static int DressFurniture(string path, Material mat, params (string child, string fbx)[] parts)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int count = 0;
            try
            {
                foreach (var (child, fbx) in parts)
                {
                    Transform t = child == null ? root.transform : FindDeep(root.transform, child);
                    if (t == null) continue;
                    DressOne(t, fbx, mat);
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

        static bool DressSceneFurniture(string objectPath, string fbx, Material mat)
        {
            var scene = EditorSceneManager.OpenScene(WorldScene, OpenSceneMode.Single);
            GameObject go = GameObject.Find(objectPath);
            if (go == null) return false;
            DressOne(go.transform, fbx, mat);
            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }

        static void DressOne(Transform t, string fbx, Material mat)
        {
            Transform old = t.Find("Voxel");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                if (r is MeshRenderer || r is SkinnedMeshRenderer) r.enabled = false;
            foreach (Transform c in t)
                Debug.Log($"[ChopperProps] {t.name} child {c.name} at {c.localPosition:F2}, scale {c.localScale:F2}, light {c.GetComponentInChildren<Light>(true) != null}, particles {c.GetComponentInChildren<ParticleSystem>(true) != null}");
            Vector3 s = t.lossyScale;
            Attach(t, fbx, mat, Quaternion.identity, 1f);
            Transform pivot = t.Find("Voxel");
            // True size whatever the parent's scale.
            pivot.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
        }

        // The crypt chests are placed from SO_WorldAssets (WorldBuilder.PlaceChest scales them to the chest's width).
        static bool SwapCryptChests(Material mat)
        {
            var assets = AssetDatabase.LoadAssetAtPath<ScriptableObject>(WorldAssetsPath);
            if (assets == null) return false;
            GameObject chest = SavePropPrefab("prop_chest_voxel", mat);
            GameObject grand = SavePropPrefab("prop_chest_grand_voxel", mat);
            var so = new SerializedObject(assets);
            so.FindProperty("ChestModel").objectReferenceValue = chest;
            so.FindProperty("GrandChestModel").objectReferenceValue = grand;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            return chest != null && grand != null;
        }

        static GameObject SavePropPrefab(string fbx, Material mat)
        {
            var root = new GameObject(fbx);
            try
            {
                Attach(root.transform, fbx, mat, Quaternion.identity, 1f);
                return PrefabUtility.SaveAsPrefabAsset(root, PropPrefabFolder + "/" + fbx + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The old pixel doorway kept a 0.25 m sill collider across its opening, too tall to step over (in the house it
        // stopped the player at the door). The voxel doorway has no sill.
        static int RemoveDoorSill()
        {
            const string path = "Assets/_Chopper/Prefabs/Building/Piece_Doorway.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int removed = 0;
            try
            {
                foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (box.size.y > 0.3f || box.center.y > 0.2f) continue;
                    Object.DestroyImmediate(box);
                    removed++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return removed;
        }

        // The stairs climbed on one 45 degree ramp collider, right at the player's slope limit, so climbing failed now and
        // then. Eight solid treads of 0.25 m instead: the player steps up them (stepOffset 0.55) like any step.
        // The piece rises 2 m over 2 m toward local +Z, 1.6 m wide (the old ramp's width).
        const int TreadCount = 8;

        static int StairTreads()
        {
            const string path = "Assets/_Chopper/Prefabs/Building/Piece_Stairs.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int made = 0;
            try
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Ramp")
                        foreach (Collider c in t.GetComponents<Collider>()) c.enabled = false;
                Transform old = root.transform.Find("Treads");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var treads = new GameObject("Treads");
                treads.transform.SetParent(root.transform, false);
                const float rise = 2f / TreadCount;
                for (int i = 0; i < TreadCount; i++)
                {
                    // Each tread is a solid block from the ground to its top, one step deep.
                    var box = treads.AddComponent<BoxCollider>();
                    float top = (i + 1) * rise;
                    box.size = new Vector3(1.6f, top, rise);
                    box.center = new Vector3(0f, top * 0.5f, -1f + (i + 0.5f) * rise);
                    made++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return made;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
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
