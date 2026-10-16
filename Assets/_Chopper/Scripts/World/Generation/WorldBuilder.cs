using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Chopper.Core;
using Chopper.Trees;
using Chopper.World.Voxel;
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
            if (_built) Chopper.World.Voxel.VoxelWorld.Forget();
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

            Color32[] grass = null;
            // A server without a screen (headless smoke tests, a future dedicated server) needs no grass.
            // W5-08 step 13: the Unity Terrain is gone. The block columns are the only ground.
            if (graphics)
            {
                grass = new Color32[WorldGen.GrassMapResolution * WorldGen.GrassMapResolution];
                Parallel.For(0, WorldGen.GrassMapResolution, zi => gen.FillGrassRow(zi, grass));
            }
            long fields = total.ElapsedMilliseconds;

            var columns = Chopper.World.Voxel.VoxelColumns.Build(gen);
            Chopper.World.Voxel.VoxelColumns.Current = columns;
            var chunkWatch = Stopwatch.StartNew();
            int blockTris = Chopper.World.Voxel.VoxelWorld.Present(columns, transform, null, graphics);
            Debug.Log($"[Voxel] CRC {columns.Crc:X8} triangles {blockTris} columns in {columns.BuildMilliseconds} ms, " +
                      $"chunks and colliders in {chunkWatch.ElapsedMilliseconds} ms. No Terrain.");
            BuildWater(gen);
            if (graphics) Chopper.World.BlockSky.Build(transform);
            BuildBoundary();
            List<RockSpot> rocks = gen.GenerateRocks(_assets.RockModels.Length);
            BuildRocks(gen, rocks);
            // The trees come from the seed alone; the campfires need them to find a clear spot, and the grass is cleared
            // under the fires, so both are worked out before the grass is built.
            List<TreeEntry> treeEntries = gen.GenerateTrees(_assets.SpeciesArrays(), _assets.TreeModelHeights);
            gen.PushTreesOffMines(treeEntries);
            // Step 7: every trunk stands on the top of its block, not on the smooth hill between the steps.
            for (int i = 0; i < treeEntries.Count; i++)
            {
                TreeEntry entry = treeEntries[i];
                if (!columns.TryTopAt(entry.Position.x, entry.Position.z, out float top)) continue;
                entry.Position.y = top;
                treeEntries[i] = entry;
            }
            List<Vector2> fires = CampfireSpots(gen, treeEntries);
            if (graphics)
            {
                WorldGen.StampRocks(grass, rocks);
                WorldGen.StampRocks(grass, gen.CryptClearings());
                WorldGen.StampRocks(grass, gen.MineClearings());
                WorldGen.StampRocks(grass, FireClearings(fires));
            WorldGen.StampRocks(grass, new List<RockSpot> { new RockSpot { Position = new Vector2(17f, 22f), Size = 11f } });
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
        // Behind the 18 m x 12 m house (its back wall is 6 m out), inside the yard torches (12.6 m) and clear of the two
        // behind it (150 and 210 degrees).
        private static readonly float[] YardFireAngles = { 165f, 195f };
        private const float YardFireRadius = 10f;
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
                var position = new Vector3(rock.Position.x, Chopper.World.WorldGround.HeightAt(rock.Position.x, rock.Position.y), rock.Position.y);
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

        private static readonly float[] TileMeters = { 1f, 1f, 1f, 1f, 1f };
        private static readonly float[] MineTileMeters = { 1f, 1f, 1f, 1f, 1f };
        private static readonly Color[] CryptBlockColors =
        {
            BlockColor(0.20f, 0.15f, 0.11f, 2), BlockColor(0.15f, 0.11f, 0.08f, 2), BlockColor(0.08f, 0.06f, 0.05f, 2),
            BlockColor(0f, 0f, 0f, 2), BlockColor(0.24f, 0.107f, 0.04f, 1)
        };
        private static readonly Color[] MineBlockColors =
        {
            BlockColor(0.22f, 0.15f, 0.10f, 2), BlockColor(0.17f, 0.10f, 0.055f, 1), BlockColor(0.09f, 0.07f, 0.05f, 2),
            BlockColor(0f, 0f, 0f, 2), BlockColor(0.24f, 0.107f, 0.04f, 1)
        };

        private static Color BlockColor(float r, float g, float b, int type) => new Color(r, g, b, type / 8f + 1f / 16f);

        private static Material _blockMaterial;

        private static Material BlockMaterial()
        {
            if (_blockMaterial != null) return _blockMaterial;
            _blockMaterial = Resources.Load<Material>("M_Blocks");
            if (_blockMaterial == null)
            {
                Shader shader = Shader.Find("Chopper/Block");
                if (shader != null) _blockMaterial = new Material(shader) { name = "M_Blocks" };
            }
            return _blockMaterial;
        }

        private static Material _crystal;

        private static void PlaceThreshold(Transform mine, MinePlan plan)
        {
            GameObject prefab = Resources.Load<GameObject>("Props/prop_mine_crystal_voxel");
            Quaternion turn = Quaternion.Euler(0f, plan.Site.Yaw, 0f);
            Vector3 right = turn * Vector3.right;
            Vector3 forward = turn * Vector3.forward;
            Vector3 door = plan.DoorSpot;
            int n = 0;
            foreach (float s in new[] { 1f, -1f, 0.55f, -0.6f })
            {
                Vector3 at = door + right * s * (MinePlan.PortalWidth * 0.5f + 0.9f + n * 0.25f) + forward * (0.5f + n * 0.45f);
                at.y = Chopper.World.WorldGround.Exists ? Chopper.World.WorldGround.HeightAt(at) : at.y;
                GameObject crystal = prefab != null ? Object.Instantiate(prefab, mine) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                crystal.name = "ThresholdCrystal";
                crystal.transform.SetParent(mine, true);
                crystal.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, plan.Site.Yaw + n * 47f, 0f));
                crystal.transform.localScale = Vector3.one * (prefab != null ? (n < 2 ? 1.1f : 0.7f) : 0.3f);
                foreach (Collider c in crystal.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                foreach (Renderer r in crystal.GetComponentsInChildren<Renderer>()) r.sharedMaterial = CrystalMaterial();
                n++;
            }
            var glow = new GameObject("ThresholdGlow");
            glow.transform.SetParent(mine, false);
            glow.transform.position = door + forward * 1.2f + Vector3.up * 0.7f;
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = CaveVoxel.CrystalColor;
            light.range = 6f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
            PlaceTimberPortal(mine, plan, door, forward, turn);
        }

        private static void PlaceTimberPortal(Transform mine, MinePlan plan, Vector3 door, Vector3 forward, Quaternion turn)
        {
            Vector3 at = door - forward * 0.35f;
            float ground = Chopper.World.WorldGround.Exists ? Chopper.World.WorldGround.HeightAt(at) : at.y;
            GameObject lanternPrefab = Resources.Load<GameObject>("Props/prop_mine_lantern_voxel");
            Vector3 hang = new Vector3(at.x, ground + MinePlan.PortalHeight - 0.4f, at.z) + forward * 1.1f;
            if (lanternPrefab != null)
            {
                GameObject lantern = Object.Instantiate(lanternPrefab, hang, turn, mine);
                lantern.name = "PortalLantern";
                foreach (Collider c in lantern.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
            Vector3 baseAt = new Vector3(at.x, ground, at.z);
            GameObject portalPrefab = Resources.Load<GameObject>("Props/prop_mine_portal_voxel");
            if (portalPrefab != null)
            {
                GameObject frame = Object.Instantiate(portalPrefab, baseAt - forward * 1.3f, turn, mine);
                frame.name = "PortalFrame";
                foreach (Collider c in frame.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
            GameObject railPrefab = Resources.Load<GameObject>("Props/prop_mine_rail_voxel");
            GameObject cartPrefab = Resources.Load<GameObject>("Props/prop_mine_cart_voxel");
            if (railPrefab != null)
            {
                float len = Mathf.Max(0.5f, RendererBounds(railPrefab).size.z);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = baseAt + forward * (0.6f + len * (k + 0.5f));
                    p.y = Chopper.World.WorldGround.Exists ? Chopper.World.WorldGround.HeightAt(p) + 0.02f : p.y;
                    GameObject rail = Object.Instantiate(railPrefab, p, turn, mine);
                    rail.name = "PortalRail";
                }
            }
            if (cartPrefab != null)
            {
                Vector3 p = baseAt + forward * 2.2f + (turn * Vector3.right) * 2.4f;
                p.y = Chopper.World.WorldGround.Exists ? Chopper.World.WorldGround.HeightAt(p) + 0.12f : p.y;
                GameObject cart = Object.Instantiate(cartPrefab, p, turn * Quaternion.Euler(0f, 4f, 0f), mine);
                cart.name = "PortalCart";
                Bounds cb = RendererBounds(cart);
                cart.transform.position += new Vector3(p.x - cb.center.x, p.y + 0.1f - cb.min.y, p.z - cb.center.z);
                var cartLight = new GameObject("PortalCartLight");
                cartLight.transform.SetParent(mine, false);
                cartLight.transform.position = cart.transform.position + forward * 1.6f + Vector3.up * 1.8f;
                var cl = cartLight.AddComponent<Light>();
                cl.type = LightType.Point; cl.color = LanternColor; cl.range = 5f; cl.intensity = 2.5f; cl.shadows = LightShadows.None;
                Debug.Log($"[Mine] portal cart at {cart.transform.position} size {cb.size}");
            }
            var dark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            dark.name = "PortalDark";
            Object.Destroy(dark.GetComponent<Collider>());
            dark.transform.SetParent(mine, false);
            dark.transform.position = new Vector3(at.x, ground + MinePlan.PortalHeight * 0.5f - 0.05f, at.z) + forward * 0.8f;
            dark.transform.rotation = turn * Quaternion.Euler(0f, 180f, 0f);
            dark.transform.localScale = new Vector3(MinePlan.PortalWidth + 0.1f, MinePlan.PortalHeight + 0.2f, 1f);
            {
                var black = new Material(CrystalMaterial()) { name = "M_PortalDark" };
                if (black.HasProperty("_BaseColor")) black.SetColor("_BaseColor", new Color(0.01f, 0.012f, 0.014f));
                dark.GetComponent<MeshRenderer>().sharedMaterial = black;
                var mf = dark.GetComponent<MeshFilter>();
                Mesh quad = Object.Instantiate(mf.sharedMesh);
                var cols = new Color[quad.vertexCount];
                for (int c = 0; c < cols.Length; c++) cols[c] = new Color(0.01f, 0.012f, 0.014f, 1f);
                quad.colors = cols;
                mf.sharedMesh = quad;
            }
            var lampGo = new GameObject("PortalLanternLight");
            lampGo.transform.SetParent(mine, false);
            lampGo.transform.position = hang - Vector3.up * 0.3f + forward * 0.8f;
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = LanternColor;
            lamp.range = 8f;
            lamp.intensity = 4f;
            lamp.shadows = LightShadows.None;
        }

        private static Bounds RendererBounds(GameObject g)
        {
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
        private static Material CrystalMaterial()
        {
            if (_crystal != null) return _crystal;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            _crystal = unlit != null ? new Material(unlit) { name = "M_CaveCrystal" } : new Material(BlockMaterial()) { name = "M_CaveCrystal" };
            if (_crystal.HasProperty("_BaseColor")) _crystal.SetColor("_BaseColor", CaveVoxel.CrystalColor);
            return _crystal;
        }

        private static Material _caveStone;

        private static Material _portalStone;

        private static Material PortalStoneMaterial()
        {
            if (_portalStone != null) return _portalStone;
            Material source = CaveStoneMaterial();
            if (source == null) return null;
            _portalStone = new Material(source) { name = "M_PortalStone" };
            _portalStone.SetFloat("_Fill", 0.3f);
            return _portalStone;
        }

        private static Material CaveStoneMaterial()
        {
            if (_caveStone != null) return _caveStone;
            Material source = BlockMaterial();
            if (source == null) return null;
            _caveStone = new Material(source) { name = "M_CaveStone" };
            _caveStone.SetFloat("_Jitter", 0.62f);
            _caveStone.SetFloat("_Edge", 0.035f);
            _caveStone.SetFloat("_Fill", 0.03f);
            return _caveStone;
        }
        // Warmer and tighter than the old flat pools (was 1, 0.62, 0.3 at range 13, intensity 4.5).
        private static readonly Color LanternColor = new Color(1f, 0.62f, 0.22f);
        private const float LanternRange = 11f;
        private const float LanternIntensity = 4.6f;
        private static readonly Color EmberColor = new Color(1f, 0.32f, 0.06f);

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
                BuildBlocks(crypt, "Entrance", plan.EntranceBoxes, graphics, graphics ? CryptMaterials() : null, TileMeters, CryptBlockColors);
                BuildBlocks(crypt, "Rooms", plan.Boxes, graphics, graphics ? CryptMaterials() : null, TileMeters, CryptBlockColors);
                if (graphics)
                {
                    foreach (CryptLantern lantern in plan.EntranceLanterns) PlaceLantern(crypt, lantern, false, false);
                    foreach (CryptLantern lantern in plan.Lanterns) PlaceLantern(crypt, lantern, true, false);
                    foreach (CryptLantern lantern in plan.ArenaLanterns) PlaceLantern(crypt, lantern, true, false);
                }
                PlaceDressing(crypt, plan.Dressing);
                foreach (CryptChest chest in plan.Chests) CryptWorld.SetChestGlint(chestId++, PlaceChest(crypt, chest, graphics));
            }
        }

        // One collider per block, and (with graphics) one mesh per material with the texture tiled in meters.
        private void BuildBlocks(Transform parent, string name, List<CryptBox> boxes, bool graphics, Material[] materials, float[] tiles,
            Color[] blockColors = null)
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

            var groups = new SortedSet<int>();
            foreach (CryptBox box in boxes) groups.Add(box.Group);
            foreach (int group in groups)
            {
                List<CryptBox> inGroup = boxes.FindAll(box => box.Group == group);
                if (blockColors != null)
                {
                    Mesh cave = CaveVoxel.Build(inGroup, out Mesh crystals, name != "Portal");
                    if (cave != null)
                    {
                        var shell = new GameObject($"Cave_{group}");
                        shell.transform.SetParent(root.transform, false);
                        shell.AddComponent<MeshFilter>().sharedMesh = cave;
                        var shellRenderer = shell.AddComponent<MeshRenderer>();
                        shellRenderer.sharedMaterial = name == "Portal" ? PortalStoneMaterial() : CaveStoneMaterial();
                        shellRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        shell.isStatic = true;
                    }
                    if (crystals != null)
                    {
                        var glint = new GameObject($"Crystals_{group}");
                        glint.transform.SetParent(root.transform, false);
                        glint.AddComponent<MeshFilter>().sharedMesh = crystals;
                        var glintRenderer = glint.AddComponent<MeshRenderer>();
                        glintRenderer.sharedMaterial = CrystalMaterial();
                        glintRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        glint.isStatic = true;
                        Bounds cb = crystals.bounds;
                        for (int g = 0; g < 3; g++)
                        {
                            var glow = new GameObject("CrystalGlow");
                            glow.transform.SetParent(glint.transform, false);
                            glow.transform.localPosition = new Vector3(Mathf.Lerp(cb.min.x, cb.max.x, 0.2f + g * 0.3f), cb.min.y + 0.6f, Mathf.Lerp(cb.min.z, cb.max.z, 0.8f - g * 0.3f));
                            var light = glow.AddComponent<Light>();
                            light.type = LightType.Point;
                            light.color = CaveVoxel.CrystalColor;
                            light.range = 9f;
                            light.intensity = 1.1f;
                            light.shadows = LightShadows.None;
                        }
                    }
                    continue;
                }
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null) continue;
                    bool world = blockColors != null && (CryptMaterial)m != CryptMaterial.Void;
                    Mesh mesh = BuildBoxMesh(inGroup, (CryptMaterial)m, tiles[m], world ? blockColors[m] : (Color?)null, world);
                    if (mesh == null) continue;
                    var part = new GameObject($"{(CryptMaterial)m}_{group}");
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var meshRenderer = part.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = world ? CaveStoneMaterial() : materials[m];
                    meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
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
                BuildBlocks(mine, "Portal", plan.EntranceBoxes, graphics, materials, MineTileMeters, MineBlockColors);
                BuildBlocks(mine, "Chambers", plan.Boxes, graphics, materials, MineTileMeters, MineBlockColors);
                if (graphics)
                {
                    foreach (CryptLantern lantern in plan.EntranceLanterns) PlaceLantern(mine, lantern, true, true);
                    PlaceThreshold(mine, plan);
                    foreach (CryptLantern lantern in plan.Lanterns) PlaceLantern(mine, lantern, true, true);
                    Color glow = MineGlow(plan.Zone);
                    foreach (MineVein vein in plan.Veins) PlaceVein(mine, vein, veinId++, glow);
                    PlaceBlockProp(mine, "Ladder", plan.LadderSpot, plan.LadderYaw, LadderParts(MinePlan.RoomHeight + 0.4f), false);
                }
                else veinId += plan.Veins.Count;
                PlaceDressing(mine, plan.Dressing);
            }
        }

        private static readonly Color PropTimber = BlockColor(0.24f, 0.107f, 0.04f, 1);
        private static readonly Color PropTimberDark = BlockColor(0.14f, 0.062f, 0.024f, 1);
        private static readonly Color PropIron = BlockColor(0.06f, 0.06f, 0.065f, 2);
        private static readonly Color PropSteel = BlockColor(0.2f, 0.2f, 0.21f, 2);

        private static (Vector3 center, Vector3 size, Color color) Part(float x, float y, float z, float sx, float sy, float sz, Color color) =>
            (new Vector3(x, y, z), new Vector3(sx, sy, sz), color);

        private static List<(Vector3, Vector3, Color)> LadderParts(float height)
        {
            var parts = new List<(Vector3, Vector3, Color)>
            {
                Part(-0.3f, height * 0.5f, 0f, 0.12f, height, 0.12f, PropTimber),
                Part(0.3f, height * 0.5f, 0f, 0.12f, height, 0.12f, PropTimber)
            };
            for (float y = 0.3f; y < height - 0.1f; y += 0.35f) parts.Add(Part(0f, y, 0f, 0.6f, 0.08f, 0.1f, PropTimberDark));
            return parts;
        }

        private static readonly Color PropFlame = BlockColor(1f, 0.55f, 0.2f, 2);

        private static List<(Vector3, Vector3, Color)> LanternParts()
        {
            return new List<(Vector3, Vector3, Color)>
            {
                Part(0f, 0.1f, 0.3f, 0.12f, 0.06f, 0.5f, PropIron),
                Part(0f, 0.0f, 0.55f, 0.26f, 0.06f, 0.26f, PropIron),
                Part(0f, 0.2f, 0.55f, 0.18f, 0.3f, 0.18f, PropFlame),
                Part(0f, 0.38f, 0.55f, 0.26f, 0.06f, 0.26f, PropIron)
            };
        }

        private static List<(Vector3, Vector3, Color)> RailParts(float length)
        {
            var parts = new List<(Vector3, Vector3, Color)>
            {
                Part(-0.35f, 0.09f, 0f, 0.06f, 0.06f, length, PropSteel),
                Part(0.35f, 0.09f, 0f, 0.06f, 0.06f, length, PropSteel)
            };
            for (float z = -length * 0.5f + 0.3f; z < length * 0.5f; z += 0.6f) parts.Add(Part(0f, 0.03f, z, 1f, 0.06f, 0.16f, PropTimberDark));
            return parts;
        }

        private static List<(Vector3, Vector3, Color)> CartParts(float length)
        {
            var parts = new List<(Vector3, Vector3, Color)>
            {
                Part(0f, 0.65f, 0f, 0.9f, 0.7f, length, PropTimber),
                Part(0f, 1.02f, 0f, 1f, 0.06f, length + 0.1f, PropIron)
            };
            foreach (float x in new[] { -0.5f, 0.5f })
                foreach (float z in new[] { -length * 0.3f, length * 0.3f })
                    parts.Add(Part(x, 0.18f, z, 0.1f, 0.36f, 0.36f, PropIron));
            return parts;
        }

        private static List<(Vector3, Vector3, Color)> BarrelParts(float size)
        {
            float w = size * 0.85f, h = size * 1.3f;
            return new List<(Vector3, Vector3, Color)>
            {
                Part(0f, h * 0.5f, 0f, w, h, w, PropTimber),
                Part(0f, h * 0.2f, 0f, w + 0.04f, 0.08f, w + 0.04f, PropIron),
                Part(0f, h * 0.8f, 0f, w + 0.04f, 0.08f, w + 0.04f, PropIron)
            };
        }

        private void PlaceBlockProp(Transform parent, string name, Vector3 position, float yaw, List<(Vector3 center, Vector3 size, Color color)> parts, bool solid)
        {
            Material material = BlockMaterial();
            if (material == null) return;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var bounds = new Bounds(parts[0].center, parts[0].size);
            var byColor = new Dictionary<Color, List<CryptBox>>();
            foreach ((Vector3 center, Vector3 size, Color color) in parts)
            {
                bounds.Encapsulate(new Bounds(center, size));
                if (!byColor.TryGetValue(color, out List<CryptBox> boxes)) byColor[color] = boxes = new List<CryptBox>();
                boxes.Add(new CryptBox { Center = center, Size = size, Rotation = Quaternion.identity, Material = CryptMaterial.Timber });
            }
            foreach (KeyValuePair<Color, List<CryptBox>> group in byColor)
            {
                var part = new GameObject("Part");
                part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = BuildBoxMesh(group.Value, CryptMaterial.Timber, 1f, group.Key);
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            if (solid)
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }

        // A lump of vein rock half sunk in the wall, in the vein material, with a small light in front of it.
        private void PlaceVein(Transform parent, MineVein vein, int id, Color glow)
        {
            var pivot = new GameObject("Vein" + id);
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(vein.Position, Quaternion.LookRotation(vein.Normal, Vector3.up));
            Mesh ore = CaveVoxel.BuildOre(Vector3.zero, Vector3.forward, glow, id * 31 + 7);
            if (ore != null)
            {
                var go = new GameObject("Ore");
                go.transform.SetParent(pivot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = ore;
                var meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = BlockMaterial();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            pivot.AddComponent<MineVeinView>().Init(id, glow, null);
        }

        private void PlaceVeinPlate(Transform pivot, MineVein vein, int id)
        {
            BlockPalette.Ensure();
            var boxes = new List<CryptBox>
            {
                new CryptBox
                {
                    Center = Vector3.zero,
                    Size = new Vector3(Mathf.Max(1.2f, vein.Size), Mathf.Max(1.2f, vein.Size), 0.45f),
                    Rotation = Quaternion.identity,
                    Material = CryptMaterial.Wall,
                    Group = id
                }
            };
            Mesh mesh = BuildBoxMesh(boxes, CryptMaterial.Wall, 1f, null, true);
            if (mesh == null) return;
            var go = new GameObject("VeinPlate");
            go.transform.SetParent(pivot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = BlockMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material _oreMaterial;

        private static void BuildOre(Transform pivot, int id, Color ore)
        {
            var go = new GameObject("Ore");
            go.transform.SetParent(pivot, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.28f);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = OreMesh(id, ore);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = OreMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material OreMaterial()
        {
            if (_oreMaterial != null) return _oreMaterial;
            Shader shader = Shader.Find("Chopper/VertexColorLit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            _oreMaterial = new Material(shader) { name = "M_Ore" };
            if (_oreMaterial.HasProperty("_UseAtlas")) _oreMaterial.SetFloat("_UseAtlas", 0f);
            if (_oreMaterial.HasProperty("_BaseColor")) _oreMaterial.SetColor("_BaseColor", Color.white);
            return _oreMaterial;
        }

        private static Mesh OreMesh(int id, Color ore)
        {
            var random = new System.Random(id * 17 + 3);
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            Color rust = new Color(0.62f, 0.28f, 0.16f);
            Color crystal = Color.Lerp(Color.white, ore, 0.35f);
            Color metal = Color.Lerp(rust, ore, 0.72f);
            for (int i = 0; i < 9; i++)
            {
                float x = ((float)random.NextDouble() - 0.5f) * 1.05f;
                float y = ((float)random.NextDouble() - 0.5f) * 0.85f;
                float z = (float)random.NextDouble() * 0.35f;
                float s = 0.28f + (float)random.NextDouble() * 0.22f;
                Color tint = i % 3 == 0 ? crystal : (i % 3 == 1 ? metal : rust);
                OreCube(verts, colors, tris, new Vector3(x, y, z), new Vector3(s, s * 0.85f, s * 0.75f), tint);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = ((float)random.NextDouble() - 0.5f) * 0.7f;
                float y = ((float)random.NextDouble() - 0.5f) * 0.55f;
                OreCube(verts, colors, tris, new Vector3(x, y, 0.32f), new Vector3(0.12f, 0.12f, 0.28f), crystal);
            }
            var mesh = new Mesh { name = "Ore" + id };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void OreCube(List<Vector3> verts, List<Color> colors, List<int> tris, Vector3 center, Vector3 size, Color color)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p =
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, h.y, -h.z), center + new Vector3(-h.x, h.y, -h.z),
                center + new Vector3(-h.x, -h.y, h.z), center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z)
            };
            int[] quads = { 0, 3, 2, 1, 4, 5, 6, 7, 0, 1, 5, 4, 3, 7, 6, 2, 1, 2, 6, 5, 0, 4, 7, 3 };
            for (int q = 0; q < quads.Length; q += 4)
            {
                int i = verts.Count;
                for (int k = 0; k < 4; k++)
                {
                    verts.Add(p[quads[q + k]]);
                    colors.Add(color);
                }
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
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
        public static Mesh BuildBoxMesh(List<CryptBox> boxes, CryptMaterial material, float tileMeters, Color? blockColor = null, bool worldPalette = false)
        {
            if (worldPalette) BlockPalette.Ensure();
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
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
                    Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                    Vector3 v = Vector3.Cross(n, u);
                    float uHalf = Mathf.Abs(Vector3.Dot(half, Abs(u)));
                    float vHalf = Mathf.Abs(Vector3.Dot(half, Abs(v)));
                    Vector3 faceCenter = Vector3.Scale(n, half);
                    Vector3 worldN = box.Rotation * n;
                    float cell = Mathf.Max(uHalf * 2f, vHalf * 2f, 0.25f);
                    int nu = Mathf.Max(1, Mathf.RoundToInt((uHalf * 2f) / cell));
                    int nv = Mathf.Max(1, Mathf.RoundToInt((vHalf * 2f) / cell));
                    float du = (uHalf * 2f) / nu;
                    float dv = (vHalf * 2f) / nv;
                    for (int iu = 0; iu < nu; iu++)
                    {
                        for (int iv = 0; iv < nv; iv++)
                        {
                            float u0 = -uHalf + iu * du;
                            float v0 = -vHalf + iv * dv;
                            Vector3 midLocal = faceCenter + u * (u0 + du * 0.5f) + v * (v0 + dv * 0.5f);
                            Vector3 midWorld = box.Center + box.Rotation * midLocal;
                            if (FaceBuried(boxes, box, midWorld + worldN * 0.04f)) continue;
                            int start = vertices.Count;
                            int h = CaveShell.Hash(
                                Mathf.RoundToInt(box.Center.x * 8f) + iu * 17,
                                Mathf.RoundToInt(box.Center.z * 8f) + iv * 13,
                                Mathf.RoundToInt(n.x * 3f + n.y * 5f + n.z * 7f + box.Center.y * 8f));
                            Color faceColor = FaceColor(material, worldN, h, blockColor, worldPalette);
                            float faceTop = (box.Center + box.Rotation * (faceCenter + v * vHalf)).y;
                            for (int k = 0; k < 4; k++)
                            {
                                float su = k == 1 || k == 2 ? 1f : 0f;
                                float sv = k >= 2 ? 1f : 0f;
                                Vector3 local = faceCenter + u * (u0 + su * du) + v * (v0 + sv * dv);
                                vertices.Add(box.Center + box.Rotation * local);
                                normals.Add(worldN);
                                if (blockColor.HasValue || worldPalette) colors.Add(faceColor);
                                uvs.Add(new Vector2(faceTop, 0f));
                            }
                            triangles.Add(start);
                            triangles.Add(start + 2);
                            triangles.Add(start + 1);
                            triangles.Add(start);
                            triangles.Add(start + 3);
                            triangles.Add(start + 2);
                        }
                    }
                }
            }
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = "Crypt" + material, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            if (colors.Count == vertices.Count) mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static bool FaceBuried(List<CryptBox> boxes, CryptBox self, Vector3 probe)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                CryptBox other = boxes[i];
                if (other.Center == self.Center && other.Size == self.Size && other.Rotation == self.Rotation && other.Material == self.Material)
                    continue;
                Vector3 local = Quaternion.Inverse(other.Rotation) * (probe - other.Center);
                Vector3 h = other.Size * 0.5f;
                if (Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z)
                    return true;
            }
            return false;
        }

        private static Color FaceColor(CryptMaterial material, Vector3 worldN, int hash, Color? fallback, bool worldPalette)
        {
            if (!worldPalette)
                return fallback ?? Color.white;
            BlockType type;
            if (material == CryptMaterial.Timber) type = BlockType.Dirt;
            else if (material == CryptMaterial.Ceiling) type = BlockType.Path;
            else if (material == CryptMaterial.Floor)
                type = Mathf.Abs(worldN.y) > 0.5f ? BlockType.Dirt : BlockType.Stone;
            else type = BlockType.Stone;
            BlockPalette.FaceColors faces = BlockPalette.Of(type);
            Color rgb = worldN.y > 0.5f ? faces.Top : faces.Side;
            rgb.a = (int)type / 8f + 1f / 16f;
            return rgb;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private void PlaceLantern(Transform parent, CryptLantern lantern, bool underground, bool kitMesh)
        {
            var pivot = new GameObject("Lantern");
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(lantern.Position, Quaternion.Euler(0f, lantern.Yaw, 0f));
            bool mesh = false;
            if (kitMesh)
            {
                GameObject prefab = Resources.Load<GameObject>("Props/" + CaveDress.Lantern);
                if (prefab != null)
                {
                    GameObject model = Instantiate(prefab, pivot.transform);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    mesh = true;
                }
            }
            if (!mesh) PlaceBlockProp(pivot.transform, "Model", lantern.Position, lantern.Yaw, LanternParts(), false);
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(pivot.transform, false);
            lightGo.transform.localPosition = mesh ? CaveDress.LanternFlame : new Vector3(0f, 0.2f, 0.8f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LanternColor;
            light.range = underground ? LanternRange : LanternRange * 0.6f;
            light.intensity = underground ? LanternIntensity : LanternIntensity * 0.6f;
            light.shadows = LightShadows.None;
        }

        private void PlaceDressing(Transform parent, List<CavePiece> pieces)
        {
            foreach (CavePiece piece in pieces)
            {
                GameObject prefab = Resources.Load<GameObject>("Props/" + piece.Name);
                if (prefab == null) continue;
                GameObject go = Instantiate(prefab, piece.Position, Quaternion.Euler(0f, piece.Yaw, 0f), parent);
                go.name = piece.Name;
                go.transform.localScale = piece.Scale.sqrMagnitude < 0.01f ? Vector3.one : piece.Scale;
                if (!piece.Ember) continue;
                var ember = new GameObject("Ember");
                ember.transform.SetParent(go.transform, false);
                ember.transform.localPosition = CaveDress.ScorchFlame;
                Light light = ember.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = EmberColor;
                light.range = 2.6f;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
            }
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
