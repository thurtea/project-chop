using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Batch: Chopper.Editor.ChopperCaveDress.Apply
    // Copies the cave kit FBX into Art/World (same path as the other voxel props) and saves one prefab each
    // under Resources/Props, on M_World (vertex colour, no atlas). Box collider on solids and wall skins only.
    public static class ChopperCaveDress
    {
        const string ArtFolder = "Assets/_Chopper/Art/World";
        const string PrefabFolder = "Assets/_Chopper/Resources/Props";

        static readonly string[] Names =
        {
            "prop_crypt_wall_panel_voxel", "prop_crypt_wall_moss_voxel", "prop_crypt_wall_crack_voxel",
            "prop_crypt_wall_scorch_voxel", "prop_crypt_trim_voxel", "prop_crypt_arch_voxel",
            "prop_crypt_pillar_voxel", "prop_crypt_pillar_broken_voxel", "prop_crypt_pillar_arena_voxel",
            "prop_crypt_sarcophagus_voxel", "prop_crypt_bone_pile_voxel", "prop_crypt_urn_voxel",
            "prop_crypt_chain_voxel", "prop_crypt_cobweb_voxel", "prop_crypt_roots_voxel",
            "prop_crypt_rubble_voxel", "prop_crypt_floor_cracked_voxel", "prop_crypt_floor_moss_voxel",
            "prop_crypt_dripstone_voxel", "prop_crypt_flowstone_voxel",
            "prop_mine_timber_frame_voxel", "prop_mine_gallery_frame_voxel", "prop_mine_rail_voxel",
            "prop_mine_cart_voxel", "prop_mine_ore_voxel", "prop_mine_lantern_voxel",
            "prop_mine_crate_voxel", "prop_mine_barrel_voxel", "prop_mine_rubble_voxel", "prop_mine_crystal_voxel"
        };

        // Frames, floors, rails, overhead pieces and the lantern. From the kit manifest collider field.
        static readonly HashSet<string> NoCollider = new HashSet<string>
        {
            "prop_crypt_arch_voxel", "prop_crypt_chain_voxel", "prop_crypt_cobweb_voxel",
            "prop_crypt_floor_cracked_voxel", "prop_crypt_floor_moss_voxel", "prop_crypt_dripstone_voxel",
            "prop_mine_gallery_frame_voxel", "prop_mine_rail_voxel", "prop_mine_lantern_voxel"
        };

        public static void Apply()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Chopper/Materials/World/M_World.mat");
            if (mat == null) throw new FileNotFoundException("Missing M_World.");
            Directory.CreateDirectory(ArtFolder);
            Directory.CreateDirectory(PrefabFolder);
            int boxes = 0;
            int open = 0;
            foreach (string name in Names)
            {
                Import(name);
                bool box = !NoCollider.Contains(name);
                if (!SavePrefab(name, mat, box)) throw new FileNotFoundException(name);
                if (box) boxes++;
                else open++;
            }
            AssetDatabase.SaveAssets();
            bool pass = boxes + open == Names.Length && boxes > 0 && open > 0;
            Debug.Log($"[CaveDress] prefabs {Names.Length}, box colliders {boxes}, no collider {open}, material M_World vertex colour");
            Debug.Log("[CaveDress] RESULT " + (pass ? "PASS" : "FAIL"));
        }

        static void Import(string name)
        {
            string export = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Chopper", "Art", "Export", name + ".fbx"));
            if (!File.Exists(export)) throw new FileNotFoundException(export);
            string dest = ArtFolder + "/" + name + ".fbx";
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

        static bool SavePrefab(string name, Material mat, bool box)
        {
            var root = new GameObject(name);
            try
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/" + name + ".fbx");
                if (fbx == null) return false;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                model.name = name;
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool any = false;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = mat;
                    renderer.enabled = true;
                    if (!any) bounds = renderer.bounds;
                    else bounds.Encapsulate(renderer.bounds);
                    any = true;
                }
                if (box && any)
                {
                    BoxCollider collider = root.AddComponent<BoxCollider>();
                    collider.center = bounds.center - root.transform.position;
                    collider.size = bounds.size;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab") != null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
