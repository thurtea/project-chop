using System.IO;
using Chopper.Core;
using Chopper.World;
using Chopper.World.Building;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Chopper.Editor
{
    // Builds the building pieces (Valheim style v1): a plain prefab per piece from the pixel pack's parts, a
    // PieceDefinition asset for each, the catalog that orders them, and the BuildManager network prefab that holds
    // the placed pieces. Run as part of Rebuild Network Scenes. The order of Pieces is the save format: add new pieces
    // at the end.
    public static class PieceBuilder
    {
        private const string PrefabFolder = "Assets/_Chopper/Prefabs/Building";
        private const string ConfigFolder = "Assets/_Chopper/Config/Building";
        private const string CatalogPath = ConfigFolder + "/SO_PieceCatalog.asset";
        private const string ManagerPrefabPath = "Assets/_Chopper/Prefabs/World/BuildManager.prefab";
        private const string RoofMeshPath = PrefabFolder + "/PieceRoofMesh.asset";

        private const float Thick = 0.3f;
        private const float FloorThick = 0.2f;

        private struct Spec
        {
            public string Id;
            public string Name;
            public int Wood;
            public int Fiber;
            public int Health;
            public Vector3 Size;
            public PieceSnap Snap;
            public bool Workbench;
            public Vector3[] Points;
            public System.Func<GameObject> Make;
        }

        public static NetworkObject BuildAll(GameConfig config)
        {
            MeadowBuilder.EnsureFolder(PrefabFolder);
            MeadowBuilder.EnsureFolder(ConfigFolder);

            Spec[] specs =
            {
                new Spec { Id = "Floor", Name = "Wood floor", Wood = 2, Health = 120, Size = new Vector3(2f, FloorThick, 2f), Snap = PieceSnap.Cell, Make = MakeFloor },
                new Spec { Id = "Wall", Name = "Wood wall", Wood = 4, Health = 200, Size = new Vector3(2f, 2f, Thick), Snap = PieceSnap.Edge, Make = MakeWall },
                new Spec { Id = "Doorway", Name = "Doorway", Wood = 4, Health = 180, Size = new Vector3(2f, 2f, Thick), Snap = PieceSnap.Edge, Make = MakeDoorway },
                new Spec { Id = "Roof", Name = "Roof", Wood = 3, Health = 120, Size = new Vector3(2f, 2f, 2f), Snap = PieceSnap.Cell, Make = MakeRoof },
                new Spec { Id = "Stairs", Name = "Stairs", Wood = 4, Health = 120, Size = new Vector3(2f, 2f, 2f), Snap = PieceSnap.Cell, Make = MakeStairs },
                new Spec { Id = "Workbench", Name = "Workbench", Wood = 10, Health = 150, Size = new Vector3(1.8f, 1.05f, 1.2f), Snap = PieceSnap.Free, Workbench = true, Make = MakeWorkbench },
                new Spec { Id = "Torch", Name = "Standing torch", Wood = 2, Fiber = 1, Health = 60, Size = new Vector3(0.4f, 2.4f, 0.4f), Snap = PieceSnap.Free, Make = MakeTorch },
                new Spec { Id = "Campfire", Name = "Campfire", Wood = 5, Health = 100, Size = new Vector3(1.4f, 0.8f, 1.4f), Snap = PieceSnap.Free, Make = MakeCampfire },
            };

            // Snap points (Valheim): a floor's top corners; a wall's bottom and top corners on its center line; a roof's
            // and stairs' low front and high back corners. Pieces join corner to corner, so a wall stands exactly on a
            // floor's edge, the next storey exactly on the wall, and a roof's eave exactly on the wall top.
            Vector3[] floorPoints = Corners(1f, FloorThick, 1f);
            Vector3[] wallPoints = { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(-1f, 2f, 0f), new Vector3(1f, 2f, 0f) };
            Vector3[] slopePoints = { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 2f, 1f), new Vector3(1f, 2f, 1f) };
            for (int i = 0; i < specs.Length; i++)
            {
                switch (specs[i].Id)
                {
                    case "Floor": specs[i].Points = floorPoints; break;
                    case "Wall":
                    case "Doorway": specs[i].Points = wallPoints; break;
                    case "Roof":
                    case "Stairs": specs[i].Points = slopePoints; break;
                }
            }

            var definitions = new PieceDefinition[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                Spec spec = specs[i];
                GameObject root = spec.Make();
                root.name = "Piece_" + spec.Id;
                root.AddComponent<PieceView>();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Piece_{spec.Id}.prefab");
                Object.DestroyImmediate(root);

                string path = $"{ConfigFolder}/SO_Piece_{spec.Id}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<PieceDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }
                definition.Configure(spec.Name, prefab, spec.Wood, spec.Fiber, spec.Health, spec.Size, spec.Snap, spec.Workbench, "piece-" + spec.Id.ToLowerInvariant(), spec.Points);
                EditorUtility.SetDirty(definition);
                definitions[i] = definition;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<PieceCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PieceCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(definitions);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var managerRoot = new GameObject("BuildManager");
            managerRoot.AddComponent<NetworkObject>();
            BuildManager manager = managerRoot.AddComponent<BuildManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(managerRoot, ManagerPrefabPath);
            Object.DestroyImmediate(managerRoot);
            return saved.GetComponent<NetworkObject>();
        }

        private static Vector3[] Corners(float half, float y, float halfDepth) => new[]
        {
            new Vector3(-half, y, -halfDepth), new Vector3(half, y, -halfDepth), new Vector3(-half, y, halfDepth), new Vector3(half, y, halfDepth)
        };

        // ---------- Pieces ----------

        private static Material Plank => PackOr("pixel_woodplank", "M_PiecePlank", new Color(0.72f, 0.55f, 0.34f));
        private static Material DarkWood => PackOr("pixel_wood_black", "M_PieceDark", new Color(0.26f, 0.17f, 0.1f));
        private static Material Tiles => PackOr("pixel_roof", "M_PieceRoof", new Color(0.55f, 0.2f, 0.15f));

        // A pack material, or a plain color when the pack is missing (no ?? on Unity objects).
        private static Material PackOr(string pack, string fallback, Color color)
        {
            Material material = MeadowBuilder.PixelMaterial(pack);
            if (material == null) material = MeadowBuilder.CreateColorMaterial(fallback, color, 0.1f);
            return material;
        }

        // Solid boxes a piece is made of: both drawn and walked on.
        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localRotation = rotation;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        // Enemies walk round it (and hit it when it is in their way).
        private static void BlockPaths(GameObject root, Vector3 center, Vector3 size)
        {
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = center;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        // The pack's plank slab (its "ceiling" piece, which the house floor uses too), scaled to 2 m square with its top
        // at FloorThick, over one walkable box.
        private static GameObject MakeFloor()
        {
            var root = new GameObject("Floor");
            GameObject model = MeadowBuilder.PixelModel("pixel_ceiling_lowpoly");
            if (model != null)
            {
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                tile.name = "Planks";
                tile.transform.localPosition = Vector3.zero;
                Bounds natural = Measure(tile);
                tile.transform.localScale = new Vector3(2f / Mathf.Max(0.01f, natural.size.x), 1f, 2f / Mathf.Max(0.01f, natural.size.z));
                Bounds scaled = Measure(tile);
                tile.transform.localPosition = new Vector3(-scaled.center.x, FloorThick - scaled.max.y, -scaled.center.z);
            }
            else
            {
                for (int i = 0; i < 5; i++) Box(root.transform, "Plank", new Vector3(0f, FloorThick - 0.05f, -0.8f + i * 0.4f), new Vector3(2f, 0.1f, 0.38f), Quaternion.identity, Plank);
            }
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, FloorThick * 0.5f, 0f);
            box.size = new Vector3(2f, FloorThick, 2f);
            return root;
        }

        private static Bounds Measure(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // The house wall piece (stone capped timber), stretched to 2 by 2 m.
        private static GameObject MakeWall()
        {
            var root = new GameObject("Wall");
            MeadowBuilder.HouseWall(root.transform, MeadowBuilder.WallModel, Vector3.zero, true, 2f, 2f, 0f);
            FitThickness(root);
            BlockPaths(root, new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, Thick));
            return root;
        }

        // Two jambs and a lintel: a 1 m wide opening, 1.75 m high.
        private static GameObject MakeDoorway()
        {
            var root = new GameObject("Doorway");
            const float gap = 1f;
            const float jamb = (2f - gap) * 0.5f;
            const float doorHeight = 1.75f;
            MeadowBuilder.HouseWall(root.transform, MeadowBuilder.WallModel, new Vector3(-(gap + jamb) * 0.5f, 0f, 0f), true, jamb, 2f, 0f);
            MeadowBuilder.HouseWall(root.transform, MeadowBuilder.WallModel, new Vector3((gap + jamb) * 0.5f, 0f, 0f), true, jamb, 2f, 0f);
            MeadowBuilder.HouseWall(root.transform, MeadowBuilder.WallModel, new Vector3(0f, 0f, 0f), true, gap, 2f - doorHeight, doorHeight);
            FitThickness(root);
            var left = new GameObject("BlockLeft");
            left.transform.SetParent(root.transform, false);
            left.transform.localPosition = new Vector3(-(gap + jamb) * 0.5f, 0f, 0f);
            BlockPaths(left, new Vector3(0f, 1f, 0f), new Vector3(jamb, 2f, Thick));
            var right = new GameObject("BlockRight");
            right.transform.SetParent(root.transform, false);
            right.transform.localPosition = new Vector3((gap + jamb) * 0.5f, 0f, 0f);
            BlockPaths(right, new Vector3(0f, 1f, 0f), new Vector3(jamb, 2f, Thick));
            return root;
        }

        // The pack's wall pieces are half a meter thick; building walls are thinner, so they sit on the 2 m grid lines.
        private static void FitThickness(GameObject root)
        {
            foreach (Transform wall in root.transform)
            {
                if (wall.name != "Wall") continue;
                foreach (Transform model in wall) model.localScale = new Vector3(Thick / MeadowBuilder.PieceThickness, model.localScale.y, model.localScale.z);
                BoxCollider box = wall.GetComponent<BoxCollider>();
                if (box != null) box.size = new Vector3(Thick, box.size.y, box.size.z);
            }
        }

        // A 45 degree slope of red tiles over a 2 by 2 m cell, rising from the front (local -Z) to the back.
        private static GameObject MakeRoof()
        {
            var root = new GameObject("Roof");
            var slab = new GameObject("Tiles");
            slab.transform.SetParent(root.transform, false);
            slab.AddComponent<MeshFilter>().sharedMesh = RoofMesh();
            slab.AddComponent<MeshRenderer>().sharedMaterial = Tiles;
            var collider = new GameObject("Slope");
            collider.transform.SetParent(root.transform, false);
            collider.transform.localPosition = new Vector3(0f, 1f, 0f);
            collider.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            var box = collider.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 0.15f, 2.83f);
            Box(root.transform, "Rafter", new Vector3(-0.95f, 1f, 0f), new Vector3(0.1f, 0.1f, 2.83f), Quaternion.Euler(-45f, 0f, 0f), DarkWood);
            Box(root.transform, "Rafter", new Vector3(0.95f, 1f, 0f), new Vector3(0.1f, 0.1f, 2.83f), Quaternion.Euler(-45f, 0f, 0f), DarkWood);
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) if (c.name == "Rafter") Object.DestroyImmediate(c);
            return root;
        }

        private static Mesh RoofMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RoofMeshPath);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "PieceRoof" };
            const float t = 0.12f;
            // Top face of the slab (front low edge to back high edge), its underside, and the two edges.
            Vector3[] v =
            {
                new Vector3(-1f, t, -1f), new Vector3(1f, t, -1f), new Vector3(1f, 2f + t, 1f), new Vector3(-1f, 2f + t, 1f),
                new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(1f, 2f, 1f), new Vector3(-1f, 2f, 1f),
            };
            Vector3[] vertices = { v[0], v[1], v[2], v[3], v[5], v[4], v[7], v[6], v[4], v[5], v[1], v[0], v[3], v[2], v[6], v[7] };
            int[] triangles = { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6, 8, 10, 9, 8, 11, 10, 12, 14, 13, 12, 15, 14 };
            Vector2[] uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) uv[i] = new Vector2(vertices[i].x * 0.5f + 0.5f, (vertices[i].z + vertices[i].y) * 0.35f);
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, RoofMeshPath);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Eight plank steps up 2 m over 2 m, walked as one ramp.
        private static GameObject MakeStairs()
        {
            var root = new GameObject("Stairs");
            const int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                float rise = 2f / steps;
                float run = 2f / steps;
                Box(root.transform, "Step", new Vector3(0f, rise * (i + 0.5f), -1f + run * (i + 0.5f)), new Vector3(1.6f, rise, run), Quaternion.identity, Plank);
            }
            Box(root.transform, "Stringer", new Vector3(-0.85f, 1f, 0f), new Vector3(0.12f, 0.2f, 2.83f), Quaternion.Euler(-45f, 0f, 0f), DarkWood);
            Box(root.transform, "Stringer", new Vector3(0.85f, 1f, 0f), new Vector3(0.12f, 0.2f, 2.83f), Quaternion.Euler(-45f, 0f, 0f), DarkWood);
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var ramp = new GameObject("Ramp");
            ramp.transform.SetParent(root.transform, false);
            ramp.transform.localPosition = new Vector3(0f, 1f, 0f);
            ramp.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            var box = ramp.AddComponent<BoxCollider>();
            box.size = new Vector3(1.6f, 0.1f, 2.83f);
            BlockPaths(root, new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 1f, 2f));
            return root;
        }

        // The pack's table and the Workbench component: crafting, upgrades and repairs. (A proper workbench model is wanted.)
        private static GameObject MakeWorkbench()
        {
            var root = new GameObject("Workbench");
            const float tableHeight = 1.05f;
            GameObject table = MeadowBuilder.PlaceModel("pixel_table_lowpoly", root.transform, Vector3.zero, 0f, tableHeight, true, 0f);
            if (table != null) table.transform.localPosition = Vector3.zero;
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, tableHeight * 0.5f, 0f);
            box.size = new Vector3(1.8f, tableHeight, 1.2f);
            root.AddComponent<Workbench>();
            BlockPaths(root, new Vector3(0f, tableHeight * 0.5f, 0f), new Vector3(1.8f, tableHeight, 1.2f));
            return root;
        }

        // The held torch scaled up on a post, lit at night like the yard torches.
        private static GameObject MakeTorch()
        {
            var root = new GameObject("Torch");
            const float height = 2.2f;
            var model = new GameObject("TorchModel");
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.down * 0.15f;
            float headTop = CharacterSetup.BuildTorchModel(model.transform);
            float scale = (height + 0.15f) / headTop;
            model.transform.localScale = Vector3.one * scale;
            GameObject fire = MeadowBuilder.PixelFlames(root.transform, new Vector3(0f, height - 0.08f, 0f), CharacterSetup.HeldTorchFlameScale * scale);
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, height + 0.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.64f, 0.3f);
            light.range = 16f;
            light.intensity = 7f;
            light.shadows = LightShadows.None;
            NightTorch night = root.AddComponent<NightTorch>();
            var so = new SerializedObject(night);
            so.FindProperty("_light").objectReferenceValue = light;
            so.FindProperty("_flame").objectReferenceValue = fire;
            so.FindProperty("_intensity").floatValue = 7f;
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(0.3f, height, 0.3f);
            return root;
        }

        // The hub's campfire: always burning, it dries Wet players (Campfire).
        private static GameObject MakeCampfire()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MeadowBuilder.CampfirePrefabPath);
            if (source == null) source = MeadowBuilder.BuildCampfirePrefab();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            BlockPaths(root, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.8f, 1.4f));
            return root;
        }
    }
}
