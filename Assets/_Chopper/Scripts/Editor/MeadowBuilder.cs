using System.Collections.Generic;
using System.IO;
using Chopper.Core;
using Chopper.Audio;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.Trees;
using Chopper.UI;
using Chopper.World;
using Chopper.World.Generation;
using FishNet.Object;
using FishNet.Component.Observing;
using FishNet.Component.Transforming;
using FishNet.Observing;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Chopper.Editor
{
    // Builds what the World scene needs that does not change from world to world: the assets the generator dresses a
    // world with (SO_WorldAssets: ground layers, water, rock and tree models, grass), the networked prefabs (trees,
    // hut, day and night, enemies), and the scene itself (sun, camera, the yard by the hut, the interface, and the
    // WorldBuilder that makes the land from the game's seed at runtime, see World/Generation).
    // Called from NetworkScenesBuilder.Build (menu: Chopper > Setup > Rebuild Network Scenes).
    // World coordinates: the hut is at the origin, +Z is north, the terrain is centered on the hut.
    public static class MeadowBuilder
    {
        public const string WorldFolder = "Assets/_Chopper/World";
        public const string HutPrefabPath = "Assets/_Chopper/Prefabs/World/Hut.prefab";
        public const string TreantPrefabPath = "Assets/_Chopper/Prefabs/Enemies/Treant.prefab";
        public const string DayNightPrefabPath = "Assets/_Chopper/Prefabs/World/DayNightCycle.prefab";
        public const string DirectorPrefabPath = "Assets/_Chopper/Prefabs/World/EnemyDirector.prefab";
        private const string PalettePath = "Assets/_Chopper/Config/SO_ElementPalette.asset";
        private const string SpawnTablePathFormat = "Assets/_Chopper/Config/SO_SpawnTable_{0}.asset";
        // Models used for the treant body per element, in Element value order (Neutral, Water, Fire, Earth, Wind).
        // The pack ships its pine in six leaf colors, so the element is carried by the model itself.
        private static readonly string[] TreantModels =
        {
            "pixel_tree_lowpoly_dark", "pixel_tree_lowpoly_blue", "pixel_tree_lowpoly_red", "pixel_tree_lowpoly_yellow", "pixel_tree_lowpoly_lightgreen"
        };
        public const string TreesPrefabPath = "Assets/_Chopper/Prefabs/World/TreeManager.prefab";
        private const string WorldAssetsPath = "Assets/_Chopper/Config/SO_WorldAssets.asset";
        private const string MaterialsFolder = "Assets/_Chopper/Materials/World";
        private const string PixelMeshes = PixelPackSetup.Root + "/Essential/Meshes/";
        private const string PixelMaterials = PixelPackSetup.Root + "/Essential/Materials/";

        private const string NatureMeshes = "Assets/ThirdParty/Quaternius_StylizedNatureMegaKit/FBX/";

        // A model by name: the pixel pack first, then the Quaternius nature kit (grass, ferns, flowers, bushes).
        internal static GameObject PixelModel(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PixelMeshes + name + ".fbx");
            if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(NatureMeshes + name + ".fbx");
            if (model == null) model = ChopperMeadowSwap.MeadowModel(name);
            return model;
        }

        internal static Material PixelMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(PixelMaterials + name + ".mat");
        }

        // The hut stands on a low hill: flat on top (the house and its yard), sloping gently down to the meadow.
        public const float MoundHeight = WorldGen.MoundHeight;

        // Species per zone: the pack's pine in the element's color (Water blue, Fire red and orange, Earth dark and
        // yellow, Wind light green), and the natural green pine plus round oaks in the hub.
        private static string[] SpeciesFor(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return new[] { "pixel_tree_lowpoly_blue" };
                case Element.Fire: return new[] { "pixel_tree_lowpoly_red", "pixel_tree_lowpoly_orange" };
                case Element.Wind: return new[] { "pixel_tree_lowpoly_lightgreen" };
                case Element.Earth: return new[] { "pixel_tree_lowpoly_dark", "pixel_tree_lowpoly_yellow" };
                default: return new[] { "pixel_tree_lowpoly", "pixel_tree2_lowpoly", "pixel_tree2_lowpoly", "pixel_tree2_pink_lowpoly" };
            }
        }

        // Hut layout (meters). Door faces +Z, toward the spawn points. The yard torches stand on a ring round the house
        // (where the palisade used to be, WorldGen.WallRadius).
        private const float WallRadius = WorldGen.WallRadius;

        // Where players come back after going down once the hut is repaired: beside the bed inside the house.
        public static readonly Vector3[] SpawnPositions =
        {
            new Vector3(-1.9f, MoundHeight + 0.2f, -4.6f),
            new Vector3(-1.9f, MoundHeight + 0.2f, -3.4f),
            new Vector3(-0.7f, MoundHeight + 0.2f, -4.6f),
            new Vector3(-0.7f, MoundHeight + 0.2f, -3.4f)
        };

        // The treant prefab is spawned on demand by EnemyDirector; the scene builder registers it with FishNet.
        public static NetworkObject TreantPrefab { get; private set; }
        // The wood pile a fallen player leaves behind, also spawned on demand by EnemyDirector.
        public static NetworkObject WoodPilePrefab { get; private set; }
        public const string WoodPilePrefabPath = "Assets/_Chopper/Prefabs/World/WoodPile.prefab";
        // Something lying on the ground (a picked herb, fern fiber, a branch), taken by walking over it.
        public static NetworkObject GroundItemPrefab { get; private set; }
        public const string GroundItemPrefabPath = "Assets/_Chopper/Prefabs/World/GroundItem.prefab";

        private static NetworkObject BuildGroundItemPrefab(GameConfig config)
        {
            var root = new GameObject("GroundItem");
            root.AddComponent<NetworkObject>();
            AddDistanceObserver(root, "DC_WoodPile", Chopper.Networking.NearbyClients.WoodPileRange);
            GroundItem item = root.AddComponent<GroundItem>();
            Material bark = PixelMaterial("tree_oak_log_psx2");
            if (bark == null) bark = CreateColorMaterial("M_Branch", new Color(0.36f, 0.24f, 0.13f));
            var so = new SerializedObject(item);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_woodMaterial").objectReferenceValue = bark;
            so.FindProperty("_herbModel").objectReferenceValue = PixelModel("pixel_bush2_lowpoly");
            so.FindProperty("_fiberModel").objectReferenceValue = PixelModel("Fern_1");
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, GroundItemPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        private static NetworkObject BuildWoodPilePrefab()
        {
            var root = new GameObject("WoodPile");
            root.AddComponent<NetworkObject>();
            AddDistanceObserver(root, "DC_WoodPile", Chopper.Networking.NearbyClients.WoodPileRange);
            WoodPile pile = root.AddComponent<WoodPile>();
            Material bark = PixelMaterial("tree_oak_log_psx2");
            if (bark == null) bark = CreateColorMaterial("M_Branch", new Color(0.36f, 0.24f, 0.13f));
            var so = new SerializedObject(pile);
            so.FindProperty("_material").objectReferenceValue = bark;
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, WoodPilePrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        // Builds every asset and prefab. Returns the world prefabs the server spawns once at world start.
        public static NetworkObject[] BuildAssetsAndPrefabs(GameConfig config)
        {
            EnsureFolder(WorldFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder("Assets/_Chopper/Prefabs/World");

            DeleteObsoleteAssets();
            Material stump = PixelMaterial("tree_oak_log_psx2");
            if (stump == null) stump = CreateColorMaterial("M_Stump", new Color(0.36f, 0.24f, 0.13f));
            Material particle = CreateParticleMaterial();

            BuildWorldAssets();

            EnsureFolder("Assets/_Chopper/Prefabs/Enemies");
            TreantPrefab = BuildTreantPrefab(config);
            WoodPilePrefab = BuildWoodPilePrefab();
            GroundItemPrefab = BuildGroundItemPrefab(config);

            NetworkObject treeManager = BuildTreeManagerPrefab(config, stump, particle);
            NetworkObject hut = BuildHutPrefab(config);
            NetworkObject dayNight = BuildDayNightPrefab(config);
            NetworkObject director = BuildDirectorPrefab(config, TreantPrefab);
            NetworkObject building = PieceBuilder.BuildAll(config);
            return new[] { treeManager, hut, dayNight, director, building };
        }

        // The asset list WorldBuilder dresses each generated world with. Tree models are measured here (their natural
        // height), because the generator scales every tree to its size class from that.
        private static WorldAssets BuildWorldAssets()
        {
            var assets = AssetDatabase.LoadAssetAtPath<WorldAssets>(WorldAssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<WorldAssets>();
                AssetDatabase.CreateAsset(assets, WorldAssetsPath);
            }

            assets.TerrainLayers = new[]
            {
                CreateTerrainLayer("Hub"), CreateTerrainLayer("Water"), CreateTerrainLayer("Earth"), CreateTerrainLayer("Fire"),
                CreateTerrainLayer("Wind"), CreateTerrainLayer("Dirt"), CreateTerrainLayer("Rock")
            };
            assets.TerrainMaterial = CreateTerrainMaterial();

            CreateWaterMaterials(out assets.PondWater, out assets.MarshWater, out assets.SeaWater);
            assets.SeaFloor = CreateColorMaterial("M_SeaFloor", new Color(0.12f, 0.13f, 0.10f), 0f);

            assets.RockModels = new[] { PixelModel("pixel_rock_lowpoly"), PixelModel("pixel_rock2_lowpoly"), PixelModel("pixel_rock3_lowpoly") };

            // One shared tree model list; each zone picks its species out of it by index (Element order).
            var models = new List<GameObject>();
            var heights = new List<float>();
            Element[] zones = { Element.Neutral, Element.Water, Element.Fire, Element.Earth, Element.Wind };
            var species = new WorldAssets.IntList[zones.Length];
            foreach (Element zone in zones)
            {
                var indices = new List<int>();
                foreach (string modelName in SpeciesFor(zone))
                {
                    int index = models.FindIndex(m => m.name == modelName);
                    if (index < 0)
                    {
                        GameObject model = PixelModel(modelName);
                        if (model == null) throw new FileNotFoundException($"Missing tree model {modelName}.");
                        models.Add(model);
                        heights.Add(MeasureHeight(model));
                        index = models.Count - 1;
                    }
                    indices.Add(index);
                }
                species[(int)zone] = new WorldAssets.IntList { Values = indices.ToArray() };
            }
            assets.TreeModels = models.ToArray();
            assets.TreeModelHeights = heights.ToArray();
            assets.Species = species;
            if (ChopperMeadowSwap.MeadowModel("prop_pine_voxel") != null)
                ChopperMeadowSwap.Retarget(assets);
            assets.GrassMaterial = CreateGrassMaterial();

            // Crypts: grey brick walls, big flagstones underfoot, dark stone overhead, pixel pack chests and lanterns.
            assets.CryptWall = PixelMaterial("pixel_brick");
            assets.CryptFloor = PixelMaterial("pixel_brick3");
            assets.CryptCeiling = PixelMaterial("pixel_stone");
            assets.CryptVoid = CreateColorMaterial("M_CryptVoid", new Color(0.01f, 0.01f, 0.012f), 0f);
            assets.ChestModel = PixelModel("pixel_chest2_lowpoly");
            assets.GrandChestModel = PixelModel("pixel_chest3_lowpoly");
            assets.LanternModel = PixelModel("pixel_lantern_orange_lowpoly");

            // Mines: rough stone walls and ceiling, a worn stone floor, plank timber, pixel pack props, and the vein rock
            // with emission on so its crack can glow (MineVeinView).
            // Rough rock for the walls and roof (pixel_stone_lowpoly is an atlas for its own mesh, not a tiling texture),
            // packed earth underfoot, weathered planks for the timbers.
            assets.MineWall = PixelMaterial("psx_rock_pixel");
            assets.MineFloor = PixelMaterial("psx_isle_concrete");
            assets.MineCeiling = PixelMaterial("psx_isle_rock");
            assets.MineTimber = PixelMaterial("pixel_wood2");
            assets.MineVein = CreateVeinMaterial();
            assets.VeinModel = PixelModel("pixel_rock2_lowpoly");
            assets.RailModel = PixelModel("pixel_train_tracks_lowpoly");
            assets.CartModel = PixelModel("pixel_train_car_red_lowpoly");
            assets.BarrelModel = PixelModel("pixel_barrel");
            assets.LadderModel = PixelModel("pixel_ladder_lowpoly");
            assets.Campfire = BuildCampfirePrefab();

            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            return assets;
        }

        public const string CampfirePrefabPath = "Assets/_Chopper/Prefabs/World/Campfire.prefab";
        internal const string FireEffectPath = "Assets/ThirdParty/PixelLowpoly_100Assets/_PREFABS/Effects/pixel_fire_effect_orange.prefab";

        // A ring of small stones round three crossed logs, glowing embers, the pack's orange pixel flames shrunk to a
        // campfire (their black smoke left out), a warm flickering light and a solid collider. Placed by WorldBuilder;
        // standing beside one dries a wet player (Campfire, WetRules).
        private const int CampfireStones = 9;
        private const float CampfireRingRadius = 0.62f;
        private const float CampfireFlameScale = 0.32f;

        internal static GameObject BuildCampfirePrefab()
        {
            EnsureFolder("Assets/_Chopper/Prefabs/World");
            var root = new GameObject("Campfire");

            for (int i = 0; i < CampfireStones; i++)
            {
                float angle = i * Mathf.PI * 2f / CampfireStones;
                var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * CampfireRingRadius;
                string rock = ChopperMeadowSwap.MeadowModel("prop_rock_voxel") != null
                    ? "prop_rock_voxel"
                    : (i % 3 == 0 ? "pixel_rock_lowpoly" : i % 3 == 1 ? "pixel_rock2_lowpoly" : "pixel_rock3_lowpoly");
                GameObject stone = PlaceModel(rock, root.transform, at, i * 47f, 0.3f + 0.06f * (i % 2), false, 0.05f);
                if (stone != null) stone.name = "Stone";
            }

            if (ChopperMeadowSwap.MeadowModel("prop_log_voxel") != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    GameObject piece = PlaceModel("prop_log_voxel", root.transform, new Vector3(0f, 0.12f + i * 0.04f, 0f),
                        i * 60f, 0.85f, false, 0f);
                    if (piece == null) continue;
                    piece.name = "Log";
                    // The voxel log lies along X (build_meadow.py), unlike the upright cylinder below: a small tilt
                    // about Z lifts one end so the three cross over the embers. (78 degrees, meant for the cylinder,
                    // stood these nearly upright and half under the ground.) Then sit it back on the ground, centered.
                    piece.transform.localRotation = Quaternion.Euler(0f, i * 60f, 12f);
                    Bounds turned = WorldBounds(piece);
                    piece.transform.position += new Vector3(-turned.center.x, 0.02f + i * 0.05f - turned.min.y, -turned.center.z);
                }
            }
            else
            {
                Material log = PixelMaterial("tree_oak_log_psx2");
                if (log == null) log = CreateColorMaterial("M_Branch", new Color(0.36f, 0.24f, 0.13f));
                for (int i = 0; i < 3; i++)
                {
                    GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    piece.name = "Log";
                    Object.DestroyImmediate(piece.GetComponent<Collider>());
                    piece.transform.SetParent(root.transform, false);
                    piece.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f) * Quaternion.Euler(0f, 0f, 78f);
                    piece.transform.localPosition = new Vector3(0f, 0.12f + i * 0.04f, 0f);
                    piece.transform.localScale = new Vector3(0.12f, 0.42f, 0.12f);
                    piece.GetComponent<MeshRenderer>().sharedMaterial = log;
                }
            }

            GameObject embers = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            embers.name = "Embers";
            Object.DestroyImmediate(embers.GetComponent<Collider>());
            embers.transform.SetParent(root.transform, false);
            embers.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            embers.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f);
            embers.GetComponent<MeshRenderer>().sharedMaterial = CharacterSetup.TorchMaterial("M_TorchFlame", new Color(1f, 0.55f, 0.15f), true);

            var effect = AssetDatabase.LoadAssetAtPath<GameObject>(FireEffectPath);
            if (effect != null)
            {
                var flames = (GameObject)PrefabUtility.InstantiatePrefab(effect, root.transform);
                PrefabUtility.UnpackPrefabInstance(flames, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                flames.name = "Flames";
                flames.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                flames.transform.localScale = Vector3.one * CampfireFlameScale;
                foreach (ParticleSystem system in flames.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (system.name.Contains("smoke"))
                    {
                        Object.DestroyImmediate(system.gameObject);
                        continue;
                    }
                    ParticleSystem.MainModule main = system.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            else Debug.LogWarning($"[MeadowBuilder] Missing {FireEffectPath}.");

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            light.range = 12f;
            light.intensity = 5f;
            light.shadows = LightShadows.None;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = CampfireRingRadius + 0.1f;
            collider.height = 0.8f;
            collider.center = new Vector3(0f, 0.4f, 0f);

            Campfire fire = root.AddComponent<Campfire>();
            var so = new SerializedObject(fire);
            so.FindProperty("_light").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CampfirePrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static Material CreateGrassMaterial()
        {
            string path = MaterialsFolder + "/M_GrassCarpet.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Chopper/Grass");
            if (material == null)
            {
                material = new Material(shader) { name = "M_GrassCarpet" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Materials and textures of the old plain look (colored boxes, generated bark) that nothing uses any more.
        private static void DeleteObsoleteAssets()
        {
            string[] obsolete =
            {
                "M_Bark.mat", "M_HutWall.mat", "M_Palisade.mat", "M_HutRoof.mat", "M_Stump.mat", "M_YardWood.mat",
                "M_Marker_Water.mat", "M_Marker_Fire.mat", "M_Marker_Earth.mat", "M_Marker_Wind.mat",
                "M_Orb_Water.mat", "M_Orb_Fire.mat", "M_Orb_Earth.mat", "M_Orb_Wind.mat",
                "T_Bark.png", "T_Bark_N.png", "T_GrassClump.png", "M_Grass_Neutral.mat", "M_Grass_Water.mat", "M_Grass_Earth.mat", "M_Grass_Wind.mat",
                "T_GrassMap.png"
            };
            foreach (string file in obsolete) AssetDatabase.DeleteAsset($"{MaterialsFolder}/{file}");
            // The land is generated at runtime from each game's seed now, so the baked terrain, sea mesh and tree list go.
            AssetDatabase.DeleteAsset(WorldFolder + "/MeadowTerrain.asset");
            AssetDatabase.DeleteAsset(WorldFolder + "/SeaRing.asset");
            AssetDatabase.DeleteAsset(WorldFolder + "/GrassClump.asset");
            AssetDatabase.DeleteAsset("Assets/_Chopper/Config/SO_TreeDatabase.asset");
        }

        // Fills the currently open scene with what every world shares: sun, fog, sky, the yard, the spawn points inside
        // the house, the camera, audio and HUD, and the WorldBuilder that makes the land from the seed at runtime.
        public static void PopulateScene(GameConfig config)
        {
            var sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            // A little of the shade is filled in, so shadowed ground and walls keep their color instead of going black.
            light.shadowStrength = 0.85f;
            light.color = new Color(1f, 0.96f, 0.86f);
            sun.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            RenderSettings.sun = light;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = ZonePalette.FogDensity(Element.Neutral);
            RenderSettings.fogColor = new Color(0.78f, 0.85f, 0.80f);

            string skyPath = $"{MaterialsFolder}/M_Sky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            RenderSettings.skybox = sky;

            LightingSetup.ConfigurePipeline();
            LightingSetup.ApplyToScene();
            var builderGo = new GameObject("WorldBuilder");
            WorldBuilder builder = builderGo.AddComponent<WorldBuilder>();
            var builderSo = new SerializedObject(builder);
            builderSo.FindProperty("_assets").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldAssets>(WorldAssetsPath);
            builderSo.FindProperty("_config").objectReferenceValue = config;
            builderSo.ApplyModifiedPropertiesWithoutUndo();
            BuildYardFurnishings();

            var spawns = new GameObject("PlayerSpawns");
            for (int i = 0; i < SpawnPositions.Length; i++)
            {
                var point = new GameObject($"PlayerSpawn_{i + 1}");
                point.transform.SetParent(spawns.transform, false);
                point.transform.localPosition = SpawnPositions[i];
                // Face the doorway, so the camera sits behind the player inside the room.
                point.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
                point.AddComponent<PlayerSpawnPoint>();
            }

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = ZonePalette.FarClip;
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 30f, -40f), Quaternion.Euler(35f, 0f, 0f));
            LightingSetup.EnableCameraEffects(camera);

            var audioGo = new GameObject("AudioDirector");
            AudioDirector audioDirector = audioGo.AddComponent<AudioDirector>();
            var audioSo = new SerializedObject(audioDirector);
            audioSo.FindProperty("_config").objectReferenceValue = config;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var atmosphereGo = new GameObject("ZoneAtmosphere");
            ZoneAtmosphere atmosphere = atmosphereGo.AddComponent<ZoneAtmosphere>();
            var atmosphereSo = new SerializedObject(atmosphere);
            atmosphereSo.FindProperty("_material").objectReferenceValue = CreateParticleMaterial();
            atmosphereSo.ApplyModifiedPropertiesWithoutUndo();

            var trackerGo = new GameObject("ZoneTracker");
            ZoneTracker tracker = trackerGo.AddComponent<ZoneTracker>();
            var trackerSo = new SerializedObject(tracker);
            trackerSo.FindProperty("_config").objectReferenceValue = config;
            trackerSo.ApplyModifiedPropertiesWithoutUndo();

            var hudGo = new GameObject("GameUi");
            GameUi gameUi = hudGo.AddComponent<GameUi>();
            var so = new SerializedObject(gameUi);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Ground ----------

        // A terrain material without sky reflections or specular highlights: at grazing angles they painted a pale
        // glossy rim along every slope and made the ground look wet and flat.
        private static Material CreateTerrainMaterial()
        {
            string path = $"{MaterialsFolder}/M_Terrain.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "M_Terrain" };
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private const string GroundFolder = "Assets/ThirdParty/PolyHaven_Ground/";
        private const float GroundTileMeters = 7f;

        // The ground of each layer: a CC0 photographic texture set from Poly Haven (colour and normal map), muted and
        // earthy, the way Valheim's ground reads: olive grass, dark mossy marsh, forest soil, ash, dry golden earth.
        private static string GroundTexture(string layer)
        {
            switch (layer)
            {
                case "Water": return "rocky_terrain_02";
                case "Earth": return "forest_ground_06";
                case "Fire": return "burned_ground_01";
                case "Wind": return "park_dirt";
                case "Dirt": return "forest_ground_04";
                case "Rock": return "rock_ground";
                default: return "grass_ground";
            }
        }

        private static Vector4 GroundTint(string layer)
        {
            switch (layer)
            {
                case "Hub": return new Vector4(0.74f, 0.90f, 0.50f, 1f);
                case "Water": return new Vector4(0.70f, 0.86f, 0.58f, 1f);
                case "Earth": return new Vector4(0.86f, 0.92f, 0.74f, 1f);
                case "Wind": return new Vector4(0.98f, 0.96f, 0.72f, 1f);
                default: return Vector4.one;
            }
        }

        private static TerrainLayer CreateTerrainLayer(string name)
        {
            string id = GroundTexture(name);
            string diffusePath = GroundFolder + id + "_diff.jpg";
            string normalPath = GroundFolder + id + "_nor.jpg";

            var diffuseImporter = AssetImporter.GetAtPath(diffusePath) as TextureImporter;
            if (diffuseImporter != null)
            {
                diffuseImporter.filterMode = FilterMode.Bilinear;
                diffuseImporter.anisoLevel = 8;
                diffuseImporter.mipmapEnabled = true;
                diffuseImporter.wrapMode = TextureWrapMode.Repeat;
                diffuseImporter.SaveAndReimport();
            }
            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.anisoLevel = 8;
                normalImporter.wrapMode = TextureWrapMode.Repeat;
                normalImporter.SaveAndReimport();
            }
            // Earlier builds wrote generated tiles and normal maps for each layer.
            AssetDatabase.DeleteAsset($"{MaterialsFolder}/T_{name}.png");
            AssetDatabase.DeleteAsset($"{MaterialsFolder}/T_{name}_N.png");

            var layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath),
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
                normalScale = 1f,
                tileSize = new Vector2(GroundTileMeters, GroundTileMeters),
                // Matte ground: the default smoothness made it shiny and gave point lights a hot specular spot.
                smoothness = 0f,
                metallic = 0f,
                // Grassy ground is tinted toward the grass, so the soil between clumps reads as more grass, not dirt.
                diffuseRemapMax = GroundTint(name)
            };
            AssetDatabase.DeleteAsset($"{WorldFolder}/TL_{name}.terrainlayer");
            AssetDatabase.CreateAsset(layer, $"{WorldFolder}/TL_{name}.terrainlayer");
            return layer;
        }

        // Integer hash to a float in 0..1.
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        // Smooth value noise that repeats every periodX by periodY lattice cells, so textures tile without seams.
        private static float TileNoise(float fx, float fy, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int xa = ((x0 % periodX) + periodX) % periodX;
            int ya = ((y0 % periodY) + periodY) % periodY;
            int xb = (xa + 1) % periodX;
            int yb = (ya + 1) % periodY;
            float top = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), tx);
            float bottom = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), tx);
            return Mathf.Lerp(top, bottom, ty);
        }

        private static float TileFbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude * TileNoise(u * period, v * period, period, period, seed + o * 17);
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }
            return sum / total;
        }

        // Semi transparent, glossy and rippling: the colors fade from the shallows to the deeps through the alpha,
        // and a scrolling normal map (WaterSurface moves it) makes the sun glint move.
        // Batch entry point: rebuilds only the three water materials (on the Chopper/Water shader), no scenes.
        public static void RebuildWaterMaterials()
        {
            CreateWaterMaterials(out _, out _, out _);
            AssetDatabase.SaveAssets();
        }

        private static void CreateWaterMaterials(out Material pond, out Material marsh, out Material sea)
        {
            Texture2D ripples = CreateRippleNormal();
            // Concept 2.0: muted grey-teal, lighter in the shallows. No ripple, no sun glint.
            pond = CreateWaterMaterial("M_Water", new Color(0.42f, 0.58f, 0.60f), new Color(0.13f, 0.29f, 0.36f), ripples, 1.6f, 0.28f, 3.5f);
            marsh = CreateWaterMaterial("M_MarshWater", new Color(0.42f, 0.58f, 0.42f), new Color(0.16f, 0.28f, 0.18f), ripples, 1.2f, 0.22f, 3f);
            sea = CreateWaterMaterial("M_Sea", new Color(0.34f, 0.52f, 0.58f), new Color(0.10f, 0.25f, 0.36f), ripples, 2.2f, 0.22f, 6f);
        }

        private static Material CreateWaterMaterial(string name, Color shallow, Color deep, Texture2D ripples, float depthDistance, float clarity, float rippleTile)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Shader shader = Shader.Find("Chopper/Water");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(shader) { name = name };
            else if (material.shader != shader) material.shader = shader;
            material.SetColor("_ShallowColor", shallow);
            material.SetColor("_DeepColor", deep);
            material.SetFloat("_DepthDistance", depthDistance);
            material.SetFloat("_Clarity", clarity);
            material.SetFloat("_RippleTile", rippleTile);
            material.SetFloat("_RippleStrength", 0f);
            material.SetFloat("_Swell", 0f);
            material.SetFloat("_Refraction", 0f);
            material.SetFloat("_Reflection", 0.08f);
            material.SetFloat("_Specular", 0f);
            material.SetFloat("_FoamDepth", 0.12f);
            if (ripples != null) material.SetTexture("_BumpMap", ripples);
            material.renderQueue = -1;
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateRippleNormal()
        {
            const int size = 128;
            string path = $"{MaterialsFolder}/T_Ripples_N.png";
            var height = new float[size, size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    height[x, y] = TileFbm(u, v, 4, 4, 301) * 0.7f + TileFbm(u, v, 12, 3, 302) * 0.3f;
                }
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = height[(x + 1) % size, y] - height[(x + size - 1) % size, y];
                    float dy = height[x, (y + 1) % size] - height[x, (y + size - 1) % size];
                    var n = new Vector3(-dx * 6f, -dy * 6f, 1f).normalized;
                    pixels[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Placing models ----------

        // Instantiates a pack model under a pivot at `position`, scaled so its height (or, when heightIsSize is false,
        // its widest horizontal extent) equals `size`, with its footprint centered and its bottom on the ground.
        // Several pack meshes have their origin well off the model, so the pivot is rebuilt from the bounds.
        internal static GameObject PlaceModel(string modelName, Transform parent, Vector3 position, float yaw, float size,
            bool heightIsSize, float sink)
        {
            GameObject model = PixelModel(modelName);
            if (model == null)
            {
                Debug.LogWarning($"[MeadowBuilder] Missing pack model {modelName}.");
                return null;
            }

            var pivot = new GameObject(modelName);
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot.transform);
            Bounds natural = WorldBounds(instance);
            float reference = heightIsSize ? natural.size.y : Mathf.Max(natural.size.x, natural.size.z);
            float scale = size / Mathf.Max(0.01f, reference);
            instance.transform.localScale = Vector3.one * scale;

            Bounds scaled = WorldBounds(instance);
            Vector3 offset = new Vector3(position.x - scaled.center.x, position.y - sink - scaled.min.y, position.z - scaled.center.z);
            instance.transform.position += offset;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            return pivot;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        internal static void SetStatic(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        // ---------- The yard and the workbench ----------

        private const float BoardX = -5.5f;
        private const float YardZ = 9.2f;

        // The workbench (inside the house, by the east wall) and the guide signpost in the yard. Both are plain scene
        // objects; players find them by their components (Workbench, Sign).
        private static void BuildYardFurnishings()
        {
            var root = new GameObject("HutYard");
            Material darkWood = PixelMaterial("pixel_wood_black");
            Material plank = PixelMaterial("pixel_woodplank");
            if (darkWood == null) darkWood = CreateColorMaterial("M_YardDarkWood", new Color(0.26f, 0.17f, 0.10f), 0.1f);
            if (plank == null) plank = CreateColorMaterial("M_YardPlank", new Color(0.78f, 0.62f, 0.40f), 0.1f);

            // The workbench: the pack's table along the east wall of the house (a proper workbench model is wanted, see
            // requirements.md). Every player starts with an axe, so none lies here any more.
            var stand = new GameObject("Workbench");
            stand.transform.SetParent(root.transform, false);
            stand.transform.position = new Vector3(0f, MoundHeight + HouseFloorTop, 0f) + WorkbenchSpot;
            stand.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            stand.AddComponent<Workbench>();
            const float tableHeight = 1.05f;
            GameObject table = PlaceModel("pixel_table_lowpoly", stand.transform, stand.transform.position, stand.transform.eulerAngles.y, tableHeight, true, 0f);
            if (table != null) SetStatic(table);
            // Placed models drop their colliders; the table is solid like the one it replaces.
            var tableCollider = stand.AddComponent<BoxCollider>();
            tableCollider.center = new Vector3(0f, tableHeight * 0.5f, 0f);
            tableCollider.size = new Vector3(1.8f, tableHeight, 1.4f);
            // A signpost with the guide. Nothing is written on it: players walk up and interact to read it.
            var board = new GameObject("NoticeSign");
            board.transform.SetParent(root.transform, false);
            board.transform.position = new Vector3(BoardX, MoundHeight, YardZ);
            board.AddComponent<Sign>().Configure("Notice", string.Empty, true);
            AddBox(board.transform, "Post", new Vector3(0f, 1.0f, -0.08f), new Vector3(0.16f, 2.0f, 0.16f), Quaternion.identity, darkWood);
            AddBox(board.transform, "Board", new Vector3(0f, 1.5f, 0f), new Vector3(1.3f, 0.78f, 0.07f), Quaternion.Euler(-6f, 0f, 0f), plank);
            AddBox(board.transform, "Cap", new Vector3(0f, 1.93f, -0.02f), new Vector3(1.45f, 0.08f, 0.16f), Quaternion.identity, darkWood);
            // A blank page pinned to the board, so it reads as something to read.
            Material page = CreateColorMaterial("M_SignPage", new Color(0.86f, 0.78f, 0.60f), 0.05f);
            AddBox(board.transform, "Page", new Vector3(0.05f, 1.5f, 0.045f), new Vector3(0.52f, 0.6f, 0.012f), Quaternion.Euler(-6f, 0f, 3f), page);
            var postCollider = board.AddComponent<BoxCollider>();
            postCollider.center = new Vector3(0f, 1f, -0.05f);
            postCollider.size = new Vector3(0.3f, 2f, 0.3f);
        }

        // ---------- Trees ----------

        private static float MeasureHeight(GameObject model)
        {
            GameObject temp = Object.Instantiate(model);
            var bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in temp.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Object.DestroyImmediate(temp);
            return Mathf.Max(0.1f, bounds.size.y);
        }

        // ---------- Prefabs ----------

        private static NetworkObject BuildTreeManagerPrefab(GameConfig config, Material stump, Material particle)
        {
            var root = new GameObject("TreeManager");
            root.AddComponent<NetworkObject>();
            TreeManager manager = root.AddComponent<TreeManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_stumpMaterial").objectReferenceValue = stump;
            if (ChopperMeadowSwap.MeadowModel("prop_stump_voxel") != null)
                so.FindProperty("_stumpPrefab").objectReferenceValue = ChopperMeadowSwap.StumpPrefab();
            so.FindProperty("_particleMaterial").objectReferenceValue = particle;

            // Enemies path on a NavMesh baked at runtime on the server, after the tree colliders exist.
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            so.FindProperty("_navSurface").objectReferenceValue = surface;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TreesPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        private static NetworkObject BuildDayNightPrefab(GameConfig config)
        {
            var root = new GameObject("DayNightCycle");
            root.AddComponent<NetworkObject>();
            DayNightCycle cycle = root.AddComponent<DayNightCycle>();
            root.AddComponent<WeatherSystem>();
            var so = new SerializedObject(cycle);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, DayNightPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        private static NetworkObject BuildDirectorPrefab(GameConfig config, NetworkObject treantPrefab)
        {
            var root = new GameObject("EnemyDirector");
            root.AddComponent<NetworkObject>();
            HerbManager herbs = root.AddComponent<HerbManager>();
            var herbSo = new SerializedObject(herbs);
            herbSo.FindProperty("_config").objectReferenceValue = config;
            herbSo.FindProperty("_model").objectReferenceValue = PixelModel("pixel_bush2_lowpoly");
            GameObject bushA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Chopper/Resources/Foliage/prop_bush_voxel_a.prefab");
            GameObject bushB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Chopper/Resources/Foliage/prop_bush_voxel_b.prefab");
            herbSo.FindProperty("_fabricBushModel").objectReferenceValue = bushA != null ? bushA : PixelModel("Fern_1");
            if (bushB != null) herbSo.FindProperty("_foliageBushB").objectReferenceValue = bushB;
            herbSo.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<CryptManager>();
            root.AddComponent<ArenaManager>();
            root.AddComponent<MineManager>();
            FishingHotspots hotspots = root.AddComponent<FishingHotspots>();
            var hotspotSo = new SerializedObject(hotspots);
            hotspotSo.FindProperty("_config").objectReferenceValue = config;
            hotspotSo.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<Chopper.World.Saving.WorldSaver>();
            BranchManager branches = root.AddComponent<BranchManager>();
            var branchSo = new SerializedObject(branches);
            Material bark = PixelMaterial("tree_oak_log_psx2");
            if (bark == null) bark = CreateColorMaterial("M_Branch", new Color(0.36f, 0.24f, 0.13f));
            branchSo.FindProperty("_material").objectReferenceValue = bark;
            branchSo.ApplyModifiedPropertiesWithoutUndo();
            EnemyDirector director = root.AddComponent<EnemyDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_treantPrefab").objectReferenceValue = treantPrefab;
            so.FindProperty("_woodPilePrefab").objectReferenceValue = WoodPilePrefab;
            so.FindProperty("_groundItemPrefab").objectReferenceValue = GroundItemPrefab;
            SerializedProperty tables = so.FindProperty("_spawnTables");
            Element[] forests = { Element.Water, Element.Earth, Element.Fire, Element.Wind };
            tables.arraySize = forests.Length;
            for (int i = 0; i < forests.Length; i++)
            {
                tables.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<SpawnTable>(string.Format(SpawnTablePathFormat, forests[i]));
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, DirectorPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        private static NetworkObject BuildTreantPrefab(GameConfig config)
        {
            var root = new GameObject("Treant");
            root.AddComponent<NetworkObject>();
            // Only players within range are sent an enemy and its effects (hits, swings, slams, roots): a crypt 200 m
            // down or a forest across the map costs nothing.
            AddDistanceObserver(root, "DC_Treant", Chopper.Networking.NearbyClients.CosmeticRange);
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.acceleration = 14f;
            agent.angularSpeed = 300f;
            agent.enabled = false;

            // A trigger-free capsule so the axe query and physics never depend on the visual.
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.isTrigger = true;

            Treant treant = root.AddComponent<Treant>();
            TreantAI ai = root.AddComponent<TreantAI>();

            SetServerAuthoritative(networkTransform);

            var so = new SerializedObject(treant);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_palette").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ElementPalette>(PalettePath);
            so.FindProperty("_draugrPrefab").objectReferenceValue = DraugrSetup.Build();
            so.FindProperty("_golemPrefab").objectReferenceValue = GolemSetup.Build();
            SerializedProperty models = so.FindProperty("_modelsByElement");
            models.arraySize = TreantModels.Length;
            for (int i = 0; i < TreantModels.Length; i++)
            {
                models.GetArrayElementAtIndex(i).objectReferenceValue = PixelModel(TreantModels[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("_config").objectReferenceValue = config;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TreantPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        // Server authoritative transform sync: the server's NavMeshAgent drives it, clients only interpolate.
        private static void SetServerAuthoritative(NetworkTransform networkTransform)
        {
            var so = new SerializedObject(networkTransform);
            SerializedProperty authority = so.FindProperty("_clientAuthoritative");
            if (authority != null) authority.boolValue = false;
            SerializedProperty interpolation = so.FindProperty("_interpolation");
            if (interpolation != null) interpolation.intValue = 2;
            SerializedProperty scale = so.FindProperty("_synchronizeScale");
            if (scale != null) scale.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static NetworkObject BuildHutPrefab(GameConfig config)
        {
            var root = new GameObject("Hut");
            // The hut floor is the top of the mound; the network spawner uses this position.
            root.transform.position = new Vector3(0f, MoundHeight, 0f);
            root.AddComponent<NetworkObject>();
            Hut hut = root.AddComponent<Hut>();
            var so = new SerializedObject(hut);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            SharedChest sharedChest = root.AddComponent<SharedChest>();
            var chestSo = new SerializedObject(sharedChest);
            chestSo.FindProperty("_config").objectReferenceValue = config;
            chestSo.ApplyModifiedPropertiesWithoutUndo();

            BuildHouse(root.transform);
            so = new SerializedObject(hut);
            so.FindProperty("_houseHalfExtents").vector2Value = new Vector2(HouseHalfWidth, HouseHalfDepth);
            so.ApplyModifiedPropertiesWithoutUndo();
            // No palisade (removed 2026-10-05): the hut itself shows its state (a ruin until repaired) and enemies
            // reach its walls. The shared storage chest lives on the hut (SharedChest).
            BuildNightTorches(root.transform);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, HutPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        // Torch posts round the house that light up at night, so the yard and the enemies coming at it can be seen.
        // Two flank the path to the door (north), the rest stand around the ring.
        private static readonly float[] TorchAngles = { -22f, 22f, 90f, 150f, 210f, 270f };
        private const float TorchInset = 1.4f;
        private const float TorchPostHeight = 2.2f;


        // The pack's orange pixel fire, without its black smoke, scaled with its parent. Null when the pack is missing.
        internal static GameObject PixelFlames(Transform parent, Vector3 localPosition, float scale)
        {
            var effect = AssetDatabase.LoadAssetAtPath<GameObject>(FireEffectPath);
            if (effect == null) return null;
            var flames = (GameObject)PrefabUtility.InstantiatePrefab(effect, parent);
            PrefabUtility.UnpackPrefabInstance(flames, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            flames.name = "Flame";
            flames.transform.localPosition = localPosition;
            flames.transform.localScale = Vector3.one * scale;
            foreach (ParticleSystem system in flames.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system.name.Contains("smoke"))
                {
                    Object.DestroyImmediate(system.gameObject);
                    continue;
                }
                ParticleSystem.MainModule main = system.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            return flames;
        }

        private static void BuildNightTorches(Transform root)
        {
            Material flame = CharacterSetup.TorchMaterial("M_TorchFlame", new Color(1f, 0.55f, 0.15f), true);
            foreach (float angle in TorchAngles)
            {
                float radians = angle * Mathf.Deg2Rad;
                var torch = new GameObject("NightTorch");
                torch.transform.SetParent(root, false);
                torch.transform.localPosition = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * (WallRadius - TorchInset);

                // The player's own torch, scaled up and planted in the ground, so the yard torches match the one in
                // the hand (bark handle, rope bands, charred head) and still read as torches by day.
                var model = new GameObject("TorchModel");
                model.transform.SetParent(torch.transform, false);
                model.transform.localPosition = Vector3.down * 0.15f;
                model.transform.localRotation = Quaternion.Euler(0f, angle * 1.7f, 0f);
                float headTop = CharacterSetup.BuildTorchModel(model.transform);
                float scale = (TorchPostHeight + 0.15f) / headTop;
                model.transform.localScale = Vector3.one * scale;

                // The same pixel flames as the held torch, scaled with it.
                GameObject fire = PixelFlames(torch.transform, new Vector3(0f, TorchPostHeight - 0.08f, 0f), CharacterSetup.HeldTorchFlameScale * scale);
                if (fire == null)
                {
                    fire = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    fire.name = "Flame";
                    Object.DestroyImmediate(fire.GetComponent<Collider>());
                    fire.transform.SetParent(torch.transform, false);
                    fire.transform.localPosition = new Vector3(0f, TorchPostHeight + 0.18f, 0f);
                    fire.transform.localScale = new Vector3(0.26f, 0.4f, 0.26f);
                    fire.GetComponent<MeshRenderer>().sharedMaterial = flame;
                }

                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(torch.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, TorchPostHeight + 0.4f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.64f, 0.3f);
                light.range = 20f;
                light.intensity = 9f;
                light.shadows = LightShadows.None;

                NightTorch night = torch.AddComponent<NightTorch>();
                var so = new SerializedObject(night);
                so.FindProperty("_light").objectReferenceValue = light;
                so.FindProperty("_flame").objectReferenceValue = fire;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // A walk-in timber house built from the pack's wall pieces (stone capped timber, stretched taller):
        // 9 m wide and 12 m deep, its open doorway facing the yard (north, +Z), windows in the sides and the back, a plank
        // floor and a red tiled gable roof. Inside: the bed (the respawn point, slept in from dusk on), the storage chest
        // (the shared store), a fireplace, a rug and a hanging lantern. The workbench is built with the yard
        // (BuildYardFurnishings).
        // Enemies cannot walk in (the floor is carved out of the NavMesh); they hit the walls from outside (Hut).
        public const float HouseHalfWidth = WorldGen.HutHalfWidth;
        public const float HouseHalfDepth = WorldGen.HutHalfDepth;
        private const float HouseWallHeight = 2.8f;
        private const float HouseFloorTop = 0.1f;
        private const float RoofRise = 2.6f;
        private const float RoofOverhang = 0.6f;
        private const float DoorGap = 1.5f;
        private const float DoorHeight = 2.25f;
        private const string HouseRoofMeshPath = "Assets/_Chopper/Prefabs/World/HutRoof.asset";
        private const string HouseRoofBrokenMeshPath = "Assets/_Chopper/Prefabs/World/HutRoofBroken.asset";
        public const string IntactGroup = "IntactOnly";
        public const string RuinGroup = "RuinOnly";
        // The wall pieces that are knocked down while the hut is broken (east side and back right), and how much of
        // them is left standing.
        private static readonly Vector3[] BrokenWallSpots =
        {
            new Vector3(HouseHalfWidth, 0f, -4.5f), new Vector3(3f, 0f, -HouseHalfDepth), new Vector3(-3f, 0f, HouseHalfDepth)
        };
        private const float BrokenWallHeight = 1.1f;
        // How much of the east roof slope is left above the eave on a broken hut.
        private const float BrokenRoofShare = 0.3f;
        // Where the furniture stands, in the house's own space (door at +Z).
        public static readonly Vector3 BedSpot = new Vector3(-3.1f, 0f, -4.3f);
        private static readonly Vector3 ChestSpot = new Vector3(-3.7f, 0f, -1.2f);
        public static readonly Vector3 WorkbenchSpot = new Vector3(3.55f, 0f, 0.6f);
        private static readonly Vector3 FireplaceSpot = new Vector3(0f, 0f, -5.45f);

        private static void BuildHouse(Transform root)
        {
            var house = new GameObject("House");
            house.transform.SetParent(root, false);
            // Two versions of the parts that change: IntactOnly shows once the hut is repaired, RuinOnly while it is
            // broken (Hut.ApplyRuinLook switches every object with these names).
            var intact = new GameObject(IntactGroup);
            intact.transform.SetParent(house.transform, false);
            var ruin = new GameObject(RuinGroup);
            ruin.transform.SetParent(house.transform, false);
            Material tiles = PixelMaterial("pixel_roof");
            Material timber = PixelMaterial("psx_wall_wood");
            if (timber == null) timber = PixelMaterial("pixel_wood");

            // Walls: (model, center, runs along X). Each pack piece is 3 m long, 2 m tall and 0.5 m thick.
            float w = HouseHalfWidth, d = HouseHalfDepth;
            (string model, Vector3 at, bool alongX)[] pieces =
            {
                (WallModel, new Vector3(-3f, 0f, d), true), (WallModel, new Vector3(3f, 0f, d), true),
                (WallModel, new Vector3(-3f, 0f, -d), true), ("pixel_wall_window_lowpoly", new Vector3(0f, 0f, -d), true), (WallModel, new Vector3(3f, 0f, -d), true),
                (WallModel, new Vector3(w, 0f, -4.5f), false), ("pixel_wall_window_lowpoly", new Vector3(w, 0f, -1.5f), false),
                ("pixel_wall_window_lowpoly", new Vector3(w, 0f, 1.5f), false), (WallModel, new Vector3(w, 0f, 4.5f), false),
                (WallModel, new Vector3(-w, 0f, -4.5f), false), ("pixel_wall_window_lowpoly", new Vector3(-w, 0f, -1.5f), false),
                ("pixel_wall_window_lowpoly", new Vector3(-w, 0f, 1.5f), false), (WallModel, new Vector3(-w, 0f, 4.5f), false),
            };
            foreach (var piece in pieces)
            {
                bool breaks = System.Array.IndexOf(BrokenWallSpots, piece.at) >= 0;
                HouseWall(breaks ? intact.transform : house.transform, piece.model, piece.at, piece.alongX, PieceLength, HouseWallHeight, 0f);
                // Broken down to a low stub while the hut is ruined.
                if (breaks) HouseWall(ruin.transform, WallModel, piece.at, piece.alongX, PieceLength, BrokenWallHeight, 0f);
            }

            // The doorway: two short pieces as stone jambs and a low piece above the gap as the lintel.
            float jamb = (PieceLength - DoorGap) * 0.5f;
            HouseWall(house.transform, WallModel, new Vector3(-(DoorGap + jamb) * 0.5f, 0f, d), true, jamb, HouseWallHeight, 0f);
            HouseWall(house.transform, WallModel, new Vector3((DoorGap + jamb) * 0.5f, 0f, d), true, jamb, HouseWallHeight, 0f);
            HouseWall(house.transform, WallModel, new Vector3(0f, 0f, d), true, DoorGap, HouseWallHeight - DoorHeight, DoorHeight);

            // Stone pillars on the corners hide where the walls meet.
            foreach (Vector3 corner in new[] { new Vector3(w, 0f, d), new Vector3(-w, 0f, d), new Vector3(w, 0f, -d), new Vector3(-w, 0f, -d) })
            {
                GameObject pillar = PlaceModel("pixel_wall_corner_lowpoly", house.transform, root.position + corner, 0f, HouseWallHeight + 0.15f, true, 0f);
                if (pillar != null) pillar.transform.localPosition = corner;
            }

            BuildHouseFloor(house.transform, root.position);
            BuildHouseRoof(intact.transform, tiles, timber, false);
            BuildHouseRoof(ruin.transform, tiles, timber, true);
            BuildHouseRubble(ruin.transform, timber);
            BuildHouseFurniture(house.transform, root.position);

            // No enemy walks inside: the whole floor is carved out of the NavMesh, so they stop at the walls.
            var keepOut = new GameObject("KeepOut");
            keepOut.transform.SetParent(house.transform, false);
            keepOut.transform.localPosition = new Vector3(0f, 1f, 0f);
            var modifier = keepOut.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            var obstacle = keepOut.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(w * 2f + 0.6f, 2f, d * 2f + 0.6f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        // One stretched wall piece with its own box collider. lift raises it (the lintel above the doorway).
        internal static void HouseWall(Transform parent, string modelName, Vector3 at, bool alongX, float length, float height, float lift)
        {
            GameObject model = PixelModel(modelName);
            if (model == null) throw new FileNotFoundException($"Missing {modelName} in the pixel pack.");
            var pivot = new GameObject("Wall");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = at + Vector3.up * lift;
            // A pack piece runs along its own Z: a quarter turn lays it along X.
            pivot.transform.localRotation = Quaternion.Euler(0f, alongX ? 90f : 0f, 0f);
            var piece = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot.transform);
            piece.transform.localPosition = Vector3.zero;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = new Vector3(1f, height / PieceHeight, length / PieceLength);
            foreach (Collider collider in piece.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var box = pivot.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(PieceThickness, height, length);
            SetStatic(pivot);
        }

        // Plank floor tiles, 3 m square, over one walkable box a little above the ground.
        private static void BuildHouseFloor(Transform house, Vector3 origin)
        {
            // The pack's "ceiling" piece is the wooden plank slab; its "floor" piece has no material.
            GameObject tile = PixelModel("pixel_ceiling_lowpoly");
            if (tile != null)
            {
                for (float x = -HouseHalfWidth + 1.5f; x < HouseHalfWidth; x += 3f)
                {
                    for (float z = -HouseHalfDepth + 1.5f; z < HouseHalfDepth; z += 3f)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(tile, house);
                        instance.name = "Floor";
                        instance.transform.localPosition = Vector3.zero;
                        Bounds natural = WorldBounds(instance);
                        instance.transform.localScale = new Vector3(3f / Mathf.Max(0.01f, natural.size.x), 1f, 3f / Mathf.Max(0.01f, natural.size.z));
                        Bounds scaled = WorldBounds(instance);
                        instance.transform.position += new Vector3(origin.x + x - scaled.center.x, origin.y + HouseFloorTop - scaled.max.y, origin.z + z - scaled.center.z);
                        foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                        SetStatic(instance);
                    }
                }
            }
            var floor = new GameObject("FloorCollider");
            floor.transform.SetParent(house, false);
            var box = floor.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, HouseFloorTop - 0.25f, 0f);
            box.size = new Vector3(HouseHalfWidth * 2f, 0.5f, HouseHalfDepth * 2f);
            floor.isStatic = true;
        }

        // A gable roof with its ridge along the house's length: two tiled slopes (seen from inside too) and two timber
        // gable ends. UVs are in meters, so the pixel tiles keep their size. The collider keeps the camera inside.
        private static void BuildHouseRoof(Transform house, Material tiles, Material timber, bool broken)
        {
            float hw = HouseHalfWidth, hd = HouseHalfDepth;
            float a = hw + RoofOverhang, dd = hd + RoofOverhang;
            float top = HouseWallHeight + RoofRise;
            float eave = top - RoofRise / hw * a;
            float slope = Mathf.Sqrt(a * a + (top - eave) * (top - eave));
            const float tileMeters = 2f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var roofTris = new List<int>();
            var gableTris = new List<int>();

            // Each face is added twice with its own vertices, once per side: shared vertices would average the two
            // opposite normals to nothing and break the lighting.
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float uLength, float vLength, List<int> tris)
            {
                var corners = new[] { p0, p1, p2, p3 };
                var cornerUvs = new[] { new Vector2(0f, vLength), new Vector2(uLength, vLength), new Vector2(uLength, 0f), new Vector2(0f, 0f) };
                int i = vertices.Count;
                vertices.AddRange(corners);
                uvs.AddRange(cornerUvs);
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                int j = vertices.Count;
                vertices.AddRange(corners);
                uvs.AddRange(cornerUvs);
                tris.AddRange(new[] { j, j + 2, j + 1, j, j + 3, j + 2 });
            }

            // East and west slopes, from the ridge down to the eaves.
            // A broken roof keeps only a strip of the east slope above the eave; the rest has caved in.
            float east = broken ? BrokenRoofShare : 1f;
            Vector3 eastTop = Vector3.Lerp(new Vector3(a, eave, 0f), new Vector3(0f, top, 0f), east);
            Quad(new Vector3(eastTop.x, eastTop.y, -dd), new Vector3(eastTop.x, eastTop.y, dd), new Vector3(a, eave, dd), new Vector3(a, eave, -dd), dd * 2f / tileMeters, slope * east / tileMeters, roofTris);
            Quad(new Vector3(0f, top, dd), new Vector3(0f, top, -dd), new Vector3(-a, eave, -dd), new Vector3(-a, eave, dd), dd * 2f / tileMeters, slope / tileMeters, roofTris);
            // Gable ends: a triangle of timber above each end wall, both faces.
            foreach (float z in new[] { hd, -hd })
            {
                // A broken roof keeps only the west half of each gable, so the hole shows from the yard too.
                var corners = broken
                    ? new[] { new Vector3(-hw, HouseWallHeight, z), new Vector3(0f, HouseWallHeight, z), new Vector3(0f, top, z) }
                    : new[] { new Vector3(-hw, HouseWallHeight, z), new Vector3(hw, HouseWallHeight, z), new Vector3(0f, top, z) };
                var cornerUvs = broken
                    ? new[] { new Vector2(0f, 0f), new Vector2(hw / tileMeters, 0f), new Vector2(hw / tileMeters, RoofRise / tileMeters) }
                    : new[] { new Vector2(0f, 0f), new Vector2(hw * 2f / tileMeters, 0f), new Vector2(hw / tileMeters, RoofRise / tileMeters) };
                for (int side = 0; side < 2; side++)
                {
                    int i = vertices.Count;
                    vertices.AddRange(corners);
                    uvs.AddRange(cornerUvs);
                    gableTris.AddRange(side == 0 ? new[] { i, i + 1, i + 2 } : new[] { i, i + 2, i + 1 });
                }
            }

            var mesh = new Mesh { name = broken ? "HutRoofBroken" : "HutRoof" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(roofTris, 0);
            mesh.SetTriangles(gableTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = broken ? HouseRoofBrokenMeshPath : HouseRoofMeshPath;
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var roof = new GameObject("Roof");
            roof.transform.SetParent(house, false);
            roof.AddComponent<MeshFilter>().sharedMesh = mesh;
            roof.AddComponent<MeshRenderer>().sharedMaterials = new[] { tiles, timber };
            roof.AddComponent<MeshCollider>().sharedMesh = mesh;
            roof.isStatic = true;

            if (!broken) return;
            // Bare rafters across the hole, a sagging ridge beam, and one rafter fallen in.
            float angle = Mathf.Atan2(top - eave, a) * Mathf.Rad2Deg;
            Vector3 mid = new Vector3(a * 0.5f, (top + eave) * 0.5f, 0f);
            for (float z = -hd + 0.6f; z <= hd - 0.5f; z += 2.2f)
            {
                if (Mathf.Abs(z - 1.6f) < 0.6f) continue;
                AddBox(house, "Rafter", mid + Vector3.forward * z, new Vector3(slope, 0.14f, 0.16f), Quaternion.Euler(0f, 0f, -angle), timber);
            }
            AddBox(house, "Ridge", new Vector3(0f, top - 0.12f, 0f), new Vector3(0.18f, 0.18f, dd * 2f), Quaternion.Euler(2.5f, 0f, 0f), timber);
            AddBox(house, "FallenRafter", new Vector3(2.4f, 1.2f, 1.6f), new Vector3(slope * 0.9f, 0.14f, 0.16f), Quaternion.Euler(8f, 20f, -38f), timber);
        }

        // Loose planks round a broken hut and on its floor, and boards nailed across the doorway (no colliders, so
        // nobody trips on them). Fixed positions, so every peer sees the same mess.
        private static void BuildHouseRubble(Transform ruin, Material timber)
        {
            var random = new System.Random(4711);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            Vector3[] heaps =
            {
                new Vector3(HouseHalfWidth + 1.2f, 0f, -4.4f), new Vector3(HouseHalfWidth + 0.8f, 0f, -2.6f),
                new Vector3(3f, 0f, -HouseHalfDepth - 1.1f), new Vector3(1.4f, 0f, -HouseHalfDepth - 0.9f),
                new Vector3(2.6f, 0f, 1.6f), new Vector3(1.2f, 0f, -3.2f), new Vector3(-2.5f, 0f, 3.6f),
                new Vector3(-HouseHalfWidth - 1f, 0f, 2.2f)
            };
            foreach (Vector3 heap in heaps)
            {
                int planks = 2 + random.Next(3);
                for (int i = 0; i < planks; i++)
                {
                    Vector3 at = heap + new Vector3(Range(-0.6f, 0.6f), HouseFloorTop + 0.05f + i * 0.07f, Range(-0.6f, 0.6f));
                    Quaternion turn = Quaternion.Euler(Range(-8f, 8f), Range(0f, 180f), Range(-12f, 12f));
                    AddBox(ruin, "Plank", at, new Vector3(Range(1.2f, 2.2f), 0.06f, Range(0.18f, 0.28f)), turn, timber);
                }
            }
            // Boards across the doorway, crossed.
            AddBox(ruin, "DoorBoard", new Vector3(0f, 1.0f, HouseHalfDepth + 0.3f), new Vector3(2.0f, 0.2f, 0.06f), Quaternion.Euler(0f, 0f, 24f), timber);
            AddBox(ruin, "DoorBoard", new Vector3(0f, 1.3f, HouseHalfDepth + 0.33f), new Vector3(2.0f, 0.2f, 0.06f), Quaternion.Euler(0f, 0f, -20f), timber);
            foreach (Transform t in ruin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Plank" && t.name != "DoorBoard" && t.name != "FallenRafter" && t.name != "Rafter" && t.name != "Ridge") continue;
                foreach (Collider collider in t.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            }
        }

        // Bed, storage chest, fireplace with a fire, rug and a lantern under the ridge. Furniture has simple box
        // colliders so players walk round it.
        private static void BuildHouseFurniture(Transform house, Vector3 origin)
        {
            GameObject Furnish(string model, Vector3 spot, float yaw, float size, bool heightIsSize, Vector3 collider)
            {
                GameObject placed = PlaceModel(model, house, origin + spot + Vector3.up * HouseFloorTop, yaw, size, heightIsSize, 0f);
                if (placed == null) return null;
                placed.transform.localPosition = spot + Vector3.up * HouseFloorTop;
                if (collider != Vector3.zero)
                {
                    var box = placed.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, collider.y * 0.5f, 0f);
                    box.size = collider;
                }
                SetStatic(placed);
                return placed;
            }

            // The bed lies along the west wall, its head in the south west corner.
            GameObject bed = Furnish("pixel_bed_lowpoly", BedSpot, 0f, 2.3f, false, Vector3.zero);
            if (bed != null)
            {
                Bounds b = WorldBounds(bed);
                if (b.size.x > b.size.z) bed.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                b = WorldBounds(bed);
                var box = bed.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.3f, 0f);
                box.size = Quaternion.Inverse(bed.transform.localRotation) * new Vector3(b.size.x, 0.6f, b.size.z);
                box.size = new Vector3(Mathf.Abs(box.size.x), 0.6f, Mathf.Abs(box.size.z));
                bed.name = "Bed";
                bed.isStatic = false;
                bed.AddComponent<Bed>();
            }
            GameObject chest = Furnish("pixel_chest_lowpoly", ChestSpot, 90f, 0.85f, true, new Vector3(1.1f, 0.85f, 0.8f));
            if (chest != null)
            {
                chest.name = "StorageChest";
                chest.AddComponent<StorageChest>();
            }
            GameObject fireplace = Furnish("pixel_fireplace_lowpoly", FireplaceSpot, 0f, 3.2f, true, new Vector3(1.6f, 3.2f, 0.9f));
            if (fireplace != null)
            {
                fireplace.name = "Fireplace";
                // The fire only burns in a repaired hut.
                var lit = new GameObject(IntactGroup);
                lit.transform.SetParent(fireplace.transform, false);
                PixelFlames(lit.transform, new Vector3(0f, 0.35f, 0.15f), 0.22f);
                var glow = new GameObject("FireLight").AddComponent<Light>();
                glow.transform.SetParent(lit.transform, false);
                glow.transform.localPosition = new Vector3(0f, 0.8f, 0.9f);
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.62f, 0.32f);
                glow.range = 8f;
                glow.intensity = 3f;
                glow.shadows = LightShadows.None;
            }
            GameObject rug = Furnish("pixel_rug_lowpoly", new Vector3(0f, 0f, -0.5f), 0f, 3.2f, false, Vector3.zero);
            if (rug != null) rug.name = "Rug";
            GameObject lantern = Furnish("pixel_lantern_orange_lowpoly", new Vector3(0f, HouseWallHeight + 0.4f, 0f), 0f, 0.6f, true, Vector3.zero);
            if (lantern != null)
            {
                lantern.name = "Lantern";
                // A ruin has no lantern hanging in it.
                Transform intactGroup = house.Find(IntactGroup);
                if (intactGroup != null) lantern.transform.SetParent(intactGroup, true);
                var lamp = new GameObject("LanternLight").AddComponent<Light>();
                var lampLit = new GameObject(IntactGroup);
                lampLit.transform.SetParent(lantern.transform, false);
                lamp.transform.SetParent(lampLit.transform, false);
                lamp.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                lamp.type = LightType.Point;
                lamp.color = new Color(1f, 0.78f, 0.5f);
                lamp.range = 11f;
                lamp.intensity = 2.6f;
                lamp.shadows = LightShadows.None;
            }
        }

        // ---------- Pack wall pieces ----------

        // The pack's stone capped timber wall pieces, used for the house walls.
        internal const string WallModel = "pixel_wall_lowpoly";
        // Natural size of the pack wall pieces, centered on their origin with the bottom on the ground: they run along
        // their own Z axis, 3 m long, 2 m tall and 0.5 m thick.
        internal const float PieceLength = 3f;
        internal const float PieceHeight = 2f;
        internal const float PieceThickness = 0.5f;

        internal static void AddBox(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localRotation = rotation;
            box.transform.localScale = scale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            // The roof is visual only so players never snag on it.
            if (name.StartsWith("Roof")) Object.DestroyImmediate(box.GetComponent<Collider>());
        }

        // ---------- Helpers ----------

        // A saved copy of the pack's rock material with emission on (black), for the mine veins.
        private static Material CreateVeinMaterial()
        {
            string path = $"{MaterialsFolder}/M_MineVein.mat";
            Material source = PixelMaterial("psx_rock_pixel");
            AssetDatabase.DeleteAsset(path);
            Material material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "M_MineVein";
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Material CreateColorMaterial(string name, Color color, float smoothness = 0.15f)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateParticleMaterial()
        {
            string path = $"{MaterialsFolder}/M_TreeParticle.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            // Transparent, so particles can fade and have soft round edges instead of hard squares.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(material);
            return material;
        }

        private const string ObserversFolder = "Assets/_Chopper/Config/Observers";

        // Interest management: a NetworkObserver whose one condition is a distance (with FishNet's default 10 percent
        // more before it hides again, so an enemy at the edge does not flicker in and out).
        private static void AddDistanceObserver(GameObject root, string assetName, float maximumDistance)
        {
            EnsureFolder(ObserversFolder);
            string path = $"{ObserversFolder}/{assetName}.asset";
            DistanceCondition condition = AssetDatabase.LoadAssetAtPath<DistanceCondition>(path);
            if (condition == null)
            {
                condition = ScriptableObject.CreateInstance<DistanceCondition>();
                AssetDatabase.CreateAsset(condition, path);
            }
            var conditionSo = new SerializedObject(condition);
            conditionSo.FindProperty("_maximumDistance").floatValue = maximumDistance;
            conditionSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(condition);

            NetworkObserver observer = root.AddComponent<NetworkObserver>();
            var observerSo = new SerializedObject(observer);
            SerializedProperty conditions = observerSo.FindProperty("_observerConditions");
            conditions.arraySize = 1;
            conditions.GetArrayElementAtIndex(0).objectReferenceValue = condition;
            observerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
