using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Chopper.Core;
using Chopper.Trees;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Chopper.World.Generation
{
    // Builds the world of this game from its seed when the World scene loads, on the server and on every client:
    // terrain and ground paint, ponds, marsh pools and the sea, the edge walls, rocks, the grass carpet, the places
    // where players wake up, and the tree list TreeManager grows its trees from. The scene itself only holds what never
    // changes (sun, camera, the yard by the hut, the interface). The heavy per-pixel work runs on all CPU cores.
    public class WorldBuilder : MonoBehaviour
    {
        [SerializeField] private WorldAssets _assets;
        [SerializeField] private GameConfig _config;

        private bool _built;

        public static WorldBuilder Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            // A joining player normally has the seed already (it arrives before the scene); if not, Update waits.
            if (WorldSession.HasSeed) Build(WorldSession.Seed);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_built && WorldSession.HasSeed) Build(WorldSession.Seed);
        }

        public void Build(int seed)
        {
            if (_built) return;
            _built = true;
            var total = Stopwatch.StartNew();
            var gen = new WorldGen(seed, _config != null ? _config.HubRadius : 110f);
            bool graphics = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

            var heights = new float[WorldGen.HeightmapResolution, WorldGen.HeightmapResolution];
            Parallel.For(0, WorldGen.HeightmapResolution, zi => gen.FillHeightRow(zi, heights));
            float[,,] alpha = null;
            Color32[] grass = null;
            // A server without a screen (headless smoke tests, a future dedicated server) needs no paint or grass.
            if (graphics)
            {
                alpha = new float[WorldGen.AlphamapResolution, WorldGen.AlphamapResolution, WorldGen.LayerCount];
                Parallel.For(0, WorldGen.AlphamapResolution, zi => gen.FillAlphaRow(zi, alpha));
                grass = new Color32[WorldGen.GrassMapResolution * WorldGen.GrassMapResolution];
                Parallel.For(0, WorldGen.GrassMapResolution, zi => gen.FillGrassRow(zi, grass));
            }
            long fields = total.ElapsedMilliseconds;

            Terrain terrain = BuildTerrain(heights, alpha);
            if (graphics)
            {
                var columns = Chopper.World.Voxel.VoxelColumns.Build(gen);
                int blockTris = Chopper.World.Voxel.VoxelWorld.Present(columns, transform, terrain);
                Debug.Log($"[Voxel] CRC {columns.Crc:X8} triangles {blockTris} columns in {columns.BuildMilliseconds} ms. " +
                          (blockTris > 0 ? "Terrain paint hidden, collider kept." : "Terrain paint left on."));
            }
            BuildWater(gen);
            BuildBoundary();
            List<RockSpot> rocks = gen.GenerateRocks(_assets.RockModels.Length);
            BuildRocks(gen, rocks);
            // The trees come from the seed alone; the campfires need them to find a clear spot, and the grass is cleared
            // under the fires, so both are worked out before the grass is built.
            List<TreeEntry> treeEntries = gen.GenerateTrees(_assets.SpeciesArrays(), _assets.TreeModelHeights);
            gen.PushTreesOffMines(treeEntries);
            List<Vector2> fires = CampfireSpots(gen, treeEntries);
            if (graphics)
            {
                WorldGen.StampRocks(grass, rocks);
                WorldGen.StampRocks(grass, gen.CryptClearings());
                WorldGen.StampRocks(grass, gen.MineClearings());
                WorldGen.StampRocks(grass, FireClearings(fires));
                BuildGrass(grass);
            }
            BuildSpawns(gen);
            // Before the trees, so the crypt colliders are there when the server bakes the enemies' NavMesh.
            BuildCrypts(gen, graphics);
            BuildMines(gen, graphics);
            TreeDatabase trees = BuildTreeDatabase(treeEntries);
            BuildCampfires(fires);

            WorldSession.MarkReady(gen, trees);
            // The practice yard puts up its signs and sets the world up once the land is there.
            if (TutorialSession.Active)
            {
                var yard = new GameObject("TutorialYard");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(yard, gameObject.scene);
                yard.AddComponent<TutorialYard>();
            }
            Debug.Log($"[World] Built seed {seed}: {trees.Count} trees, {rocks.Count} rocks, checksum {WorldSession.TreeChecksum}, " +
                      $"in {total.ElapsedMilliseconds} ms (fields {fields} ms).");
        }

        // ---------- Campfires ----------

        // Two fires in the hut yard (between the torches on the south side) and one on the pond bank facing the hut,
        // where swimmers come out: standing by one dries a wet player (WetRules). Every peer places the same fires.
        private static readonly float[] YardFireAngles = { 120f, 240f };
        private const float YardFireRadius = 6.8f;
        private const float PondFireTurn = 35f;
        private const float FireClearance = 3f;

        private static List<Vector2> CampfireSpots(WorldGen gen, List<TreeEntry> trees)
        {
            var spots = new List<Vector2>();
            foreach (float angle in YardFireAngles)
            {
                float a = angle * Mathf.Deg2Rad;
                spots.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * YardFireRadius);
            }

            // On the bank toward the hut, beside the fishing spot (the fishing sign stands straight toward the hut), on
            // whichever side has no tree in the way.
            Vector2 toHut = gen.PondCenter.sqrMagnitude > 0.01f ? -gen.PondCenter.normalized : Vector2.up;
            float bank = WorldGen.PondOuterRadius + 1.5f;
            Vector2 best = gen.PondCenter + (Vector2)(Quaternion.Euler(0f, 0f, PondFireTurn) * toHut) * bank;
            foreach (float turn in new[] { PondFireTurn, -PondFireTurn, 2f * PondFireTurn, -2f * PondFireTurn })
            {
                Vector2 at = gen.PondCenter + (Vector2)(Quaternion.Euler(0f, 0f, turn) * toHut) * bank;
                if (ClearOfTrees(at, trees)) { best = at; break; }
            }
            spots.Add(best);
            return spots;
        }

        private static List<RockSpot> FireClearings(List<Vector2> fires)
        {
            var clearings = new List<RockSpot>();
            foreach (Vector2 at in fires) clearings.Add(new RockSpot { Position = at, Size = FireClearance });
            return clearings;
        }

        private static bool ClearOfTrees(Vector2 at, List<TreeEntry> trees)
        {
            foreach (TreeEntry tree in trees)
            {
                if ((new Vector2(tree.Position.x, tree.Position.z) - at).sqrMagnitude < FireClearance * FireClearance) return false;
            }
            return true;
        }

        private void BuildCampfires(List<Vector2> fires)
        {
            if (_assets.Campfire == null) return;
            var root = new GameObject("Campfires").transform;
            root.SetParent(transform, false);
            foreach (Vector2 at in fires) PlaceCampfire(root, at);
        }

        private void PlaceCampfire(Transform root, Vector2 at)
        {
            bool hasGround = Chopper.World.WorldGround.Exists;
            float y = hasGround ? Chopper.World.WorldGround.HeightAt(new Vector3(at.x, 0f, at.y)) : 0f;
            Instantiate(_assets.Campfire, new Vector3(at.x, y, at.y), Quaternion.Euler(0f, at.x * 37f % 360f, 0f), root);
        }

        // ---------- Terrain ----------

        private Terrain BuildTerrain(float[,] heights, float[,,] alpha)
        {
            var data = new TerrainData
            {
                heightmapResolution = WorldGen.HeightmapResolution,
                alphamapResolution = WorldGen.AlphamapResolution,
                size = new Vector3(WorldGen.TerrainSize, WorldGen.TerrainMaxHeight, WorldGen.TerrainSize)
            };
            data.SetHeights(0, 0, heights);
            if (alpha != null && _assets.TerrainLayers != null && _assets.TerrainLayers.Length == WorldGen.LayerCount)
            {
                data.terrainLayers = _assets.TerrainLayers;
                data.SetAlphamaps(0, 0, alpha);
            }

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(-WorldGen.HalfSize, WorldGen.TerrainBaseY, -WorldGen.HalfSize);
            var terrain = go.GetComponent<Terrain>();
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (_assets.TerrainMaterial != null) terrain.materialTemplate = _assets.TerrainMaterial;
            // Hills must not shadow the valleys: at a low sun that left whole forests as dark slabs.
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            // A finer level of detail than the default 5, so distant ledges and gullies keep their shape (2 cost too much
            // in the hub, where the whole mountain ring is in view).
            terrain.heightmapPixelError = 3f;
            go.isStatic = true;
            return terrain;
        }

        // ---------- Water ----------

        private void BuildWater(WorldGen gen)
        {
            var root = new GameObject("Water").transform;
            root.SetParent(transform, false);
            int index = 0;
            foreach (WaterBody body in gen.WaterBodies)
            {
                float shore = WorldGen.ShoreEdge(body);
                var water = new GameObject(body.Deep ? $"Lake_{index}" : body.Marsh ? $"MarshPool_{index}" : "PondWater");
                water.transform.SetParent(root, false);
                water.transform.position = new Vector3(body.Center.x, WorldGen.WaterHeight, body.Center.y);
                // A flat disc with its face up only (it was a squashed cylinder, whose underside showed as a grey wall
                // from below). The surface reaches well past the bank: the ground hides it wherever it rises above the
                // water, so the shoreline is wherever the terrain meets the water, never the edge of this disc.
                water.AddComponent<MeshFilter>().sharedMesh = BuildDiscMesh(shore * 1.25f, 64);
                var waterRenderer = water.AddComponent<MeshRenderer>();
                waterRenderer.sharedMaterial = WaterMaterial(body, gen);
                waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
                water.AddComponent<WaterSurface>().Configure(shore * 1.02f);
                index++;
            }

            // The sea: a flat ring from the beach out past the horizon; its WaterSurface skips the island.
            var sea = new GameObject("Sea");
            sea.transform.SetParent(root, false);
            sea.transform.position = new Vector3(0f, WorldGen.WaterHeight, 0f);
            sea.AddComponent<MeshFilter>().sharedMesh = BuildRingMesh(WorldGen.SeaInnerRadius, WorldGen.SeaOuterRadius, 160, 6f);
            var seaRenderer = sea.AddComponent<MeshRenderer>();
            seaRenderer.sharedMaterial = _assets.SeaWater;
            seaRenderer.shadowCastingMode = ShadowCastingMode.Off;
            sea.AddComponent<WaterSurface>().Configure(WorldGen.SeaOuterRadius, WorldGen.SeaInnerRadius);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "SeaFloor";
            Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(root, false);
            floor.transform.position = new Vector3(0f, WorldGen.SeaFloorHeight - 0.4f, 0f);
            floor.transform.localScale = new Vector3(WorldGen.SeaOuterRadius * 0.2f, 1f, WorldGen.SeaOuterRadius * 0.2f);
            var floorRenderer = floor.GetComponent<MeshRenderer>();
            floorRenderer.sharedMaterial = _assets.SeaFloor;
            floorRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static readonly int ShallowColorId = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");

        // Forest lakes take a tint from their zone. The hub pond and the marsh keep their own materials.
        private Material WaterMaterial(WaterBody body, WorldGen gen)
        {
            if (!body.Deep) return body.Marsh ? _assets.MarshWater : _assets.PondWater;
            Material source = _assets.PondWater != null ? _assets.PondWater : _assets.MarshWater;
            var mat = new Material(source);
            Element zone = Chopper.World.ZoneMap.ElementAt(body.Center.x, body.Center.y, gen.HubRadius);
            // The ash lake runs rusty, the deep woods lake peaty green, the highland lake cold and pale.
            Color shallow = zone == Element.Fire ? new Color(0.56f, 0.40f, 0.26f)
                : zone == Element.Earth ? new Color(0.34f, 0.46f, 0.28f)
                : zone == Element.Wind ? new Color(0.48f, 0.66f, 0.68f)
                : new Color(0.36f, 0.58f, 0.56f);
            Color deep = zone == Element.Fire ? new Color(0.24f, 0.10f, 0.06f)
                : zone == Element.Earth ? new Color(0.07f, 0.17f, 0.09f)
                : zone == Element.Wind ? new Color(0.10f, 0.25f, 0.33f)
                : new Color(0.06f, 0.22f, 0.26f);
            if (mat.HasProperty(ShallowColorId)) mat.SetColor(ShallowColorId, shallow);
            if (mat.HasProperty(DeepColorId)) mat.SetColor(DeepColorId, deep);
            return mat;
        }

        // A flat ring in the XZ plane, UVs in world meters divided by the tile size so ripples keep their scale.
        // A flat disc facing up, centered on its object. The water shader maps its textures in world space.
        public static Mesh BuildDiscMesh(float radius, int segments)
        {
            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            normals[0] = Vector3.up;
            for (int s = 0; s < segments; s++)
            {
                float angle = s / (float)segments * Mathf.PI * 2f;
                vertices[s + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                normals[s + 1] = Vector3.up;
                // Clockwise seen from above, so the faces point up.
                triangles[s * 3] = 0;
                triangles[s * 3 + 1] = s + 2 > segments ? 1 : s + 2;
                triangles[s * 3 + 2] = s + 1;
            }
            var mesh = new Mesh { name = "WaterDisc", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildRingMesh(float inner, float outer, int segments, float tileMeters)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float[] radii = { inner, inner + 30f, inner + 120f, outer };
            for (int s = 0; s <= segments; s++)
            {
                float angle = s / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                foreach (float radius in radii)
                {
                    Vector3 v = dir * radius;
                    vertices.Add(v);
                    uvs.Add(new Vector2(v.x, v.z) / tileMeters);
                    normals.Add(Vector3.up);
                }
            }
            int rings = radii.Length;
            for (int s = 0; s < segments; s++)
            {
                for (int k = 0; k < rings - 1; k++)
                {
                    int a = s * rings + k;
                    int b = (s + 1) * rings + k;
                    // Clockwise seen from above, so the faces point up.
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                    triangles.Add(a + 1);
                }
            }
            var mesh = new Mesh { name = "SeaRing" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Invisible walls so nobody walks or swims off the edge of the world.
        private void BuildBoundary()
        {
            var root = new GameObject("Boundary").transform;
            root.SetParent(transform, false);
            const float thickness = 4f;
            const float height = 60f;
            float offset = WorldGen.BoundaryOffset + thickness * 0.5f;
            Vector3[] centers = { new Vector3(0f, 0f, offset), new Vector3(0f, 0f, -offset), new Vector3(offset, 0f, 0f), new Vector3(-offset, 0f, 0f) };
            for (int i = 0; i < centers.Length; i++)
            {
                var wall = new GameObject($"Wall_{i}");
                wall.transform.SetParent(root, false);
                wall.transform.position = centers[i];
                var box = wall.AddComponent<BoxCollider>();
                bool alongX = i < 2;
                box.size = alongX ? new Vector3(WorldGen.TerrainSize + 2f * thickness, height, thickness)
                                  : new Vector3(thickness, height, WorldGen.TerrainSize + 2f * thickness);
            }
        }

        // ---------- Rocks ----------

        private void BuildRocks(WorldGen gen, List<RockSpot> rocks)
        {
            var root = new GameObject("Rocks");
            root.transform.SetParent(transform, false);
            foreach (RockSpot rock in rocks)
            {
                GameObject model = _assets.RockModels[rock.Model];
                if (model == null) continue;
                var position = new Vector3(rock.Position.x, gen.HeightAt(rock.Position.x, rock.Position.y), rock.Position.y);
                // Rocks sit a little in the ground so they look settled.
                GameObject placed = PlaceModel(model, root.transform, position, rock.Yaw, rock.Size, rock.Size * 0.12f);
                AddRockCollider(placed);
            }
        }

        // Instantiates a model under a pivot, scaled so its widest horizontal extent equals size, with its footprint
        // centered on the position and its bottom on the ground (several pack meshes have their origin off the model).
        private static GameObject PlaceModel(GameObject model, Transform parent, Vector3 position, float yaw, float size, float sink)
        {
            var pivot = new GameObject(model.name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            GameObject instance = Instantiate(model, pivot.transform);
            Bounds natural = WorldBounds(instance);
            float scale = size / Mathf.Max(0.01f, Mathf.Max(natural.size.x, natural.size.z));
            instance.transform.localScale = Vector3.one * scale;
            Bounds scaled = WorldBounds(instance);
            instance.transform.position += new Vector3(position.x - scaled.center.x, position.y - sink - scaled.min.y, position.z - scaled.center.z);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);
            return instance;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // Rocks block the player and the enemies' NavMesh like real boulders.
        private static void AddRockCollider(GameObject rock)
        {
            Bounds bounds = WorldBounds(rock);
            var box = rock.AddComponent<BoxCollider>();
            Vector3 scale = rock.transform.lossyScale;
            box.center = rock.transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z) * 0.8f;
        }

        // ---------- Grass ----------

        private void BuildGrass(Color32[] pixels)
        {
            var map = new Texture2D(WorldGen.GrassMapResolution, WorldGen.GrassMapResolution, TextureFormat.RGBA32, false, true)
            {
                name = "GrassMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            map.SetPixels32(pixels);
            map.Apply(false, false);
            var go = new GameObject("GrassCarpet");
            go.transform.SetParent(transform, false);
            go.AddComponent<GrassCarpet>().Configure(_assets.GrassMaterial, map,
                new Vector4(-WorldGen.HalfSize, -WorldGen.HalfSize, WorldGen.TerrainSize, WorldGen.TerrainSize));
        }

        // ---------- Crypts ----------

        private static readonly float[] TileMeters = { 2f, 2.4f, 2f, 1f, 1f };
        private static readonly float[] MineTileMeters = { 4f, 3f, 4f, 1f, 1.2f };
        private static readonly Color LanternColor = new Color(1f, 0.62f, 0.3f);
        private const float LanternRange = 13f;
        private const float LanternIntensity = 4.5f;

        private void BuildCrypts(WorldGen gen, bool graphics)
        {
            var plans = new List<CryptPlan>();
            for (int i = 0; i < gen.Crypts.Length; i++) plans.Add(new CryptPlan(gen, i));
            CryptWorld.Set(plans);

            var root = new GameObject("Crypts").transform;
            root.SetParent(transform, false);
            int chestId = 0;
            foreach (CryptPlan plan in plans)
            {
                var crypt = new GameObject(plan.Name).transform;
                crypt.SetParent(root, false);
                BuildBlocks(crypt, "Entrance", plan.EntranceBoxes, graphics, graphics ? CryptMaterials() : null, TileMeters);
                BuildBlocks(crypt, "Rooms", plan.Boxes, graphics, graphics ? CryptMaterials() : null, TileMeters);
                if (graphics)
                {
                    foreach (CryptLantern lantern in plan.EntranceLanterns) PlaceLantern(crypt, lantern, false);
                    foreach (CryptLantern lantern in plan.Lanterns) PlaceLantern(crypt, lantern, true);
                    foreach (CryptLantern lantern in plan.ArenaLanterns) PlaceLantern(crypt, lantern, true);
                }
                foreach (CryptChest chest in plan.Chests) CryptWorld.SetChestGlint(chestId++, PlaceChest(crypt, chest, graphics));
            }
        }

        // One collider per block, and (with graphics) one mesh per material with the texture tiled in meters.
        private void BuildBlocks(Transform parent, string name, List<CryptBox> boxes, bool graphics, Material[] materials, float[] tiles)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.isStatic = true;
            foreach (CryptBox box in boxes)
            {
                var block = new GameObject("Block");
                block.transform.SetParent(root.transform, false);
                block.transform.SetPositionAndRotation(box.Center, box.Rotation);
                block.AddComponent<BoxCollider>().size = box.Size;
                block.isStatic = true;
            }
            if (!graphics) return;

            // One mesh per room and material, so each room is lit and culled on its own.
            var groups = new SortedSet<int>();
            foreach (CryptBox box in boxes) groups.Add(box.Group);
            foreach (int group in groups)
            {
                List<CryptBox> inGroup = boxes.FindAll(box => box.Group == group);
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null) continue;
                    Mesh mesh = BuildBoxMesh(inGroup, (CryptMaterial)m, tiles[m]);
                    if (mesh == null) continue;
                    var part = new GameObject($"{(CryptMaterial)m}_{group}");
                    part.transform.SetParent(root.transform, false);
                    part.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var meshRenderer = part.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = materials[m];
                    meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    part.isStatic = true;
                }
            }
        }

        private Material[] _cryptMaterials;

        // ---------- Mines ----------

        private static Color MineGlow(Chopper.Core.MineZone zone)
        {
            switch (zone)
            {
                case Chopper.Core.MineZone.Water: return new Color(0.35f, 0.75f, 1f);
                case Chopper.Core.MineZone.Fire: return new Color(1f, 0.45f, 0.15f);
                case Chopper.Core.MineZone.Earth: return new Color(1f, 0.78f, 0.3f);
                case Chopper.Core.MineZone.Wind: return new Color(0.7f, 1f, 0.85f);
                default: return new Color(1f, 0.8f, 0.5f);
            }
        }

        private Material[] MineMaterials()
        {
            Material[] crypt = CryptMaterials();
            return new[]
            {
                _assets.MineWall != null ? _assets.MineWall : crypt[0],
                _assets.MineFloor != null ? _assets.MineFloor : crypt[1],
                _assets.MineCeiling != null ? _assets.MineCeiling : crypt[2],
                crypt[3],
                _assets.MineTimber != null ? _assets.MineTimber : crypt[1]
            };
        }

        // The portals on the surface and, far below, the chambers, with their veins, lanterns, rails and props.
        private void BuildMines(WorldGen gen, bool graphics)
        {
            var plans = new List<MinePlan>();
            for (int i = 0; i < gen.Mines.Length; i++) plans.Add(new MinePlan(gen, i));
            MineWorld.Set(plans);

            var root = new GameObject("Mines").transform;
            root.SetParent(transform, false);
            Material[] materials = graphics ? MineMaterials() : null;
            int veinId = 0;
            foreach (MinePlan plan in plans)
            {
                var mine = new GameObject(plan.Name).transform;
                mine.SetParent(root, false);
                BuildBlocks(mine, "Portal", plan.EntranceBoxes, graphics, materials, MineTileMeters);
                BuildBlocks(mine, "Chambers", plan.Boxes, graphics, materials, MineTileMeters);
                if (!graphics)
                {
                    veinId += plan.Veins.Count;
                    continue;
                }
                foreach (CryptLantern lantern in plan.EntranceLanterns) PlaceLantern(mine, lantern, false);
                foreach (CryptLantern lantern in plan.Lanterns) PlaceLantern(mine, lantern, true);
                Color glow = MineGlow(plan.Zone);
                foreach (MineVein vein in plan.Veins) PlaceVein(mine, vein, veinId++, glow);
                if (_assets.RailModel != null)
                    foreach (MineProp rail in plan.Rails) PlaceStretched(_assets.RailModel, mine, rail.Position, rail.Yaw, rail.Length, 0.02f);
                if (_assets.CartModel != null)
                    foreach (MineProp cart in plan.Carts) PlaceModel(_assets.CartModel, mine, cart.Position, cart.Yaw, cart.Length, 0f);
                if (_assets.BarrelModel != null)
                    foreach (MineProp barrel in plan.Barrels) PlaceModel(_assets.BarrelModel, mine, barrel.Position, barrel.Yaw, barrel.Length, 0f);
                if (_assets.LadderModel != null)
                {
                    var ladder = new GameObject("Ladder");
                    ladder.transform.SetParent(mine, false);
                    ladder.transform.SetPositionAndRotation(plan.LadderSpot, Quaternion.Euler(0f, plan.LadderYaw, 0f));
                    PlaceByHeight(_assets.LadderModel, ladder.transform, MinePlan.RoomHeight + 0.4f);
                }
            }
        }

        // A lump of vein rock half sunk in the wall, in the vein material, with a small light in front of it.
        private void PlaceVein(Transform parent, MineVein vein, int id, Color glow)
        {
            var pivot = new GameObject("Vein" + id);
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(vein.Position, Quaternion.LookRotation(vein.Normal, Vector3.up));
            if (_assets.VeinModel != null)
            {
                GameObject rock = Instantiate(_assets.VeinModel, pivot.transform);
                Bounds natural = WorldBounds(rock);
                rock.transform.localScale = Vector3.one * (vein.Size / Mathf.Max(0.01f, Mathf.Max(natural.size.x, Mathf.Max(natural.size.y, natural.size.z))));
                rock.transform.localRotation = Quaternion.Euler(id * 47f % 360f, id * 83f % 360f, id * 29f % 360f);
                Bounds placed = WorldBounds(rock);
                rock.transform.position += vein.Position - placed.center + vein.Normal * (vein.Size * 0.12f);
                foreach (Collider collider in rock.GetComponentsInChildren<Collider>()) Destroy(collider);
                if (_assets.MineVein != null)
                {
                    foreach (Renderer r in rock.GetComponentsInChildren<Renderer>())
                    {
                        var shared = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < shared.Length; i++) shared[i] = _assets.MineVein;
                        r.sharedMaterials = shared;
                    }
                }
            }
            var lightGo = new GameObject("Glint");
            lightGo.transform.SetParent(pivot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = glow;
            light.range = MineVeinView.Range;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            pivot.AddComponent<MineVeinView>().Init(id, glow, light);
        }

        // A model laid along its longest side to a length, centered on a point, its foot on that point's height.
        private static void PlaceStretched(GameObject model, Transform parent, Vector3 position, float yaw, float length, float sink)
        {
            GameObject instance = PlaceModel(model, parent, position, yaw, 1f, sink);
            Bounds natural = WorldBounds(instance);
            Vector3 local = instance.transform.InverseTransformVector(natural.size);
            bool alongX = Mathf.Abs(local.x) > Mathf.Abs(local.z);
            Vector3 scale = instance.transform.localScale;
            float factor = length / Mathf.Max(0.01f, Mathf.Max(natural.size.x, natural.size.z));
            if (alongX) scale.x *= factor;
            else scale.z *= factor;
            instance.transform.localScale = scale;
        }

        // The crypt materials from WorldAssets, or plain stand ins when the assets have not been rebuilt yet
        // (Rebuild Network Scenes fills them in), so the stone is never invisible.
        private Material[] CryptMaterials()
        {
            if (_cryptMaterials != null) return _cryptMaterials;
            Material[] assigned = { _assets.CryptWall, _assets.CryptFloor, _assets.CryptCeiling, _assets.CryptVoid };
            Color[] stand = { new Color(0.42f, 0.40f, 0.38f), new Color(0.36f, 0.33f, 0.30f), new Color(0.18f, 0.17f, 0.16f), new Color(0.01f, 0.01f, 0.01f) };
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            _cryptMaterials = new Material[assigned.Length];
            for (int m = 0; m < assigned.Length; m++)
            {
                if (assigned[m] != null || lit == null)
                {
                    _cryptMaterials[m] = assigned[m];
                    continue;
                }
                if (m == 0) Debug.LogWarning("[World] Crypt materials are missing; run Chopper > Setup > Rebuild Network Scenes.");
                _cryptMaterials[m] = new Material(lit) { name = "CryptStandIn" + m };
                _cryptMaterials[m].SetColor("_BaseColor", stand[m]);
                _cryptMaterials[m].SetFloat("_Smoothness", 0.1f);
            }
            return _cryptMaterials;
        }

        // Every box of one material as a single mesh in world space. UVs are the face's own coordinates in meters
        // divided by the tile size, so bricks keep their size on long and short walls alike.
        public static Mesh BuildBoxMesh(List<CryptBox> boxes, CryptMaterial material, float tileMeters)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (CryptBox box in boxes)
            {
                if (box.Material != material) continue;
                Vector3 half = box.Size * 0.5f;
                foreach (Vector3 n in faceNormals)
                {
                    // Two axes across the face.
                    Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                    Vector3 v = Vector3.Cross(n, u);
                    float uHalf = Mathf.Abs(Vector3.Dot(half, Abs(u)));
                    float vHalf = Mathf.Abs(Vector3.Dot(half, Abs(v)));
                    Vector3 faceCenter = Vector3.Scale(n, half);
                    int start = vertices.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        float su = k == 1 || k == 2 ? 1f : -1f;
                        float sv = k >= 2 ? 1f : -1f;
                        Vector3 local = faceCenter + u * (su * uHalf) + v * (sv * vHalf);
                        vertices.Add(box.Center + box.Rotation * local);
                        normals.Add(box.Rotation * n);
                        uvs.Add(new Vector2(su * uHalf + uHalf, sv * vHalf + vHalf) / tileMeters);
                    }
                    // Clockwise seen from outside, which is the front face in Unity.
                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + 2);
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 3);
                }
            }
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = "Crypt" + material, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private void PlaceLantern(Transform parent, CryptLantern lantern, bool underground)
        {
            var pivot = new GameObject("Lantern");
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(lantern.Position, Quaternion.Euler(0f, lantern.Yaw, 0f));
            if (_assets.LanternModel != null) PlaceByHeight(_assets.LanternModel, pivot.transform, 0.55f);
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(pivot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.1f, 0.35f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LanternColor;
            light.range = underground ? LanternRange : LanternRange * 0.6f;
            light.intensity = underground ? LanternIntensity : LanternIntensity * 0.6f;
            light.shadows = LightShadows.None;
        }

        // Returns the glint (a small warm light over the chest) that hides once the chest is looted.
        private GameObject PlaceChest(Transform parent, CryptChest chest, bool graphics)
        {
            var pivot = new GameObject(chest.Grand ? "GrandChest" : "Chest");
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(chest.Position, Quaternion.Euler(0f, chest.Yaw, 0f));
            float width = chest.Grand ? 1.5f : 1.1f;
            var box = pivot.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, width * 0.35f, 0f);
            box.size = new Vector3(width, width * 0.7f, width * 0.65f);
            if (!graphics) return null;

            GameObject model = chest.Grand && _assets.GrandChestModel != null ? _assets.GrandChestModel : _assets.ChestModel;
            if (model != null) PlaceModel(model, pivot.transform, chest.Position, chest.Yaw, width, 0f);
            var glint = new GameObject("Glint");
            glint.transform.SetParent(pivot.transform, false);
            glint.transform.localPosition = new Vector3(0f, width * 0.9f, 0.3f);
            Light light = glint.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.4f);
            light.range = chest.Grand ? 4.5f : 3f;
            light.intensity = chest.Grand ? 2.2f : 1.4f;
            light.shadows = LightShadows.None;
            return glint;
        }

        // Instantiates a model under a pivot, scaled to a height, its footprint centered on the pivot.
        private static void PlaceByHeight(GameObject model, Transform pivot, float height)
        {
            GameObject instance = Instantiate(model, pivot);
            Bounds natural = WorldBounds(instance);
            instance.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, natural.size.y));
            Bounds scaled = WorldBounds(instance);
            instance.transform.position += pivot.position - scaled.center;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);
        }


        // ---------- Spawns and trees ----------

        private void BuildSpawns(WorldGen gen)
        {
            var root = new GameObject("RandomSpawns").transform;
            root.SetParent(transform, false);
            List<SpawnSpot> spots = gen.GenerateSpawns();
            for (int i = 0; i < spots.Count; i++)
            {
                var point = new GameObject($"PlayerSpawnRandom_{i + 1:00}");
                point.SetActive(false);
                point.transform.SetParent(root, false);
                point.transform.SetPositionAndRotation(spots[i].Position, Quaternion.Euler(0f, spots[i].Yaw, 0f));
                point.AddComponent<PlayerSpawnPoint>().SetOutside(true);
                point.SetActive(true);
            }
            if (spots.Count < 8) Debug.LogWarning($"[World] Only {spots.Count} wake up spots were found.");
        }

        private TreeDatabase BuildTreeDatabase(List<TreeEntry> entries)
        {
            var database = ScriptableObject.CreateInstance<TreeDatabase>();
            database.name = "GeneratedTrees";
            database.Bake(_assets.TreeModels, entries.ToArray());
            return database;
        }
    }
}
