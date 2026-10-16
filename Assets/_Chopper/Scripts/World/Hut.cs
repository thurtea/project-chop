using Chopper.Audio;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.World.Building;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.World
{
    // The hut (plan D16): health, a wood store, and an upgrade level, all server owned SyncVars. Enemies call
    // ServerDamage. At zero health the run is over and EnemyDirector restarts it.
    public class Hut : NetworkBehaviour
    {
        public static Hut Instance { get; private set; }

        [SerializeField] private GameConfig _config;
        [Tooltip("Half the house's width (X) and depth (Z) in meters. Enemies hit it from outside these walls.")]
        [SerializeField] private Vector2 _houseHalfExtents = new Vector2(4.5f, 6f);

        private readonly SyncVar<int> _health = new SyncVar<int>();
        private readonly SyncVar<int> _woodStore = new SyncVar<int>();
        private readonly SyncVar<int> _level = new SyncVar<int>();
        private readonly SyncVar<bool> _repaired = new SyncVar<bool>();
        private readonly SyncVar<int> _repairWood = new SyncVar<int>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float RuinDarkening = 0.7f;
        private MaterialPropertyBlock _block;

        public int Health => _health.Value;
        public int Level => _level.Value;
        public int MaxHealth => _config.HutMaxHealth + _level.Value * _config.HutHealthPerLevel;
        public int WoodStore => _woodStore.Value;

        // The hut starts broken (a ruin): enough deposited wood rebuilds it.
        public bool IsRepaired => _repaired.Value;
        public int RepairWoodHave => _repairWood.Value;
        public int RepairWoodNeeded => _config.HutRepairWood;

        // The nearest point on the house's walls to a position (ground height of the hut), so enemies walk up to a wall
        // instead of to the middle of a room they cannot enter.
        public Vector3 ClosestWallPoint(Vector3 from)
        {
            Vector3 local = from - transform.position;
            float x = Mathf.Clamp(local.x, -_houseHalfExtents.x, _houseHalfExtents.x);
            float z = Mathf.Clamp(local.z, -_houseHalfExtents.y, _houseHalfExtents.y);
            // From inside (should not happen to enemies), push out to the nearest wall.
            if (Mathf.Abs(local.x) < _houseHalfExtents.x && Mathf.Abs(local.z) < _houseHalfExtents.y)
            {
                if (_houseHalfExtents.x - Mathf.Abs(local.x) < _houseHalfExtents.y - Mathf.Abs(local.z)) x = Mathf.Sign(local.x) * _houseHalfExtents.x;
                else z = Mathf.Sign(local.z) * _houseHalfExtents.y;
            }
            return transform.position + new Vector3(x, 0f, z);
        }

        // Flat distance from a position to the house's walls (0 inside).
        public bool IsIndoors(Vector3 at)
        {
            Vector3 local = transform.InverseTransformPoint(at);
            return Mathf.Abs(local.x) < HouseHalfSize.x && Mathf.Abs(local.z) < HouseHalfSize.y && local.y > -3.5f && local.y < 4f * StoreyHeight;
        }

        public float DistanceToWalls(Vector3 from)
        {
            Vector3 local = from - transform.position;
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - _houseHalfExtents.x);
            float dz = Mathf.Max(0f, Mathf.Abs(local.z) - _houseHalfExtents.y);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public bool NextUpgradeCost(out int cost) => DropRules.TryGetNextCost(_config.HutUpgradeCosts, _level.Value, out cost);

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            DressCabin();
            DressYard();
            // The NavMesh was baked before the house existed.
            var surface = FindAnyObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (surface != null && surface.navMeshData != null) surface.UpdateNavMesh(surface.navMeshData);
        }

        // The hub house (user, 2026-10-12, matched to chopper-3D-concept-2.0.jpg): a wide, low log house for two under
        // a big thatch roof, built from the hammer pieces. Laid out 12 x 8 piece-metres and scaled by HouseScale to
        // 18 m wide and 12 m deep, the long side to the front (+Z) with the door and the log steps. It stands on its
        // basement (stone-tinted walls, the raised step the concept house stands on); the main floor is one storey of
        // log walls; the roof ridge runs the length of the house, two roof pieces down each side, with a log gable and
        // crossed beams at each end, and the upstairs is a loft inside the roof. Stairs go up from the basement at the
        // -X end and from the main floor into the loft. Two workbenches (main floor and basement), two beds (loft), the
        // chest and the fireplace on the main floor.
        public static Bounds LadderZone;
        public const float HouseScale = 1.8f;
        public const float StoreyHeight = 2f * HouseScale;
        // Half the house in piece-metres (the layout) and in metres (the walls enemies hit).
        static readonly Vector2 LayoutHalf = new Vector2(6f, 4f);
        public static readonly Vector2 HouseHalfSize = LayoutHalf * HouseScale;
        static readonly float[] TilesX = { -5f, -3f, -1f, 1f, 3f, 5f };
        static readonly float[] TilesZ = { -3f, -1f, 1f, 3f };
        static readonly Color BasementStone = new Color(0.62f, 0.60f, 0.58f);
        static readonly int TintId = Shader.PropertyToID("_BaseColor");

        void DressYard()
        {
            var pine = Resources.Load<GameObject>("Props/yard_pine");
            var yard = new GameObject("Yard").transform;
            yard.SetParent(transform, false);
            Vector2[] pines =
            {
                new Vector2(28.5f, 20.5f), new Vector2(30f, 15f), new Vector2(26f, 25.5f), new Vector2(4.5f, 26f),
                new Vector2(-1.5f, 28.5f), new Vector2(-19f, -13f), new Vector2(-25f, -3f), new Vector2(-21f, 8f),
                new Vector2(14f, -16f), new Vector2(23f, -9f), new Vector2(-8f, -18f)
            };
            if (pine != null)
            {
                for (int i = 0; i < pines.Length; i++)
                {
                    Vector3 at = Ground(pines[i]);
                    GameObject tree = Instantiate(pine, at, Quaternion.Euler(0f, i * 47f, 0f), yard);
                    tree.transform.localScale = Vector3.one * (1.1f + (i % 3) * 0.18f);
                }
            }
            Vector3 pondAt = Ground(new Vector2(17f, 22f));
            var pond = Resources.Load<GameObject>("Props/prop_yard_pond_voxel");
            if (pond != null) PlaceProp(pond, yard, pondAt, pondAt.y - 0.25f, 15f, 1f, 0f);
            var waterPrefab = Resources.Load<GameObject>("Props/prop_yard_water_voxel");
            if (waterPrefab != null)
            {
                GameObject w = PlaceProp(waterPrefab, yard, pondAt, pondAt.y + 0.2f, 15f, 1f, 0f);
                Material real = null;
                foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name == "Chopper/Water" && !r.sharedMaterial.name.Contains("Marsh")) { real = r.sharedMaterial; break; }
                if (real != null) foreach (Renderer r in w.GetComponentsInChildren<Renderer>()) r.sharedMaterial = real;
                foreach (Collider c in w.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            var stone = Resources.Load<GameObject>("Props/prop_path_stone_voxel");
            if (stone != null)
            {
                int k = 0;
                for (float z = HouseHalfSize.y + 2.8f; z < HouseHalfSize.y + 15f; z += 1.6f, k++)
                {
                    Vector3 at = Ground(new Vector2(1.8f + Mathf.Sin(z * 0.4f) * 0.5f, z));
                    GameObject s = PlaceProp(stone, yard, at, at.y - 0.12f, (k % 2) * 90f + k * 7f, 0.8f, 0f);
                    foreach (Collider c in s.GetComponentsInChildren<Collider>()) Destroy(c);
                }
            }
            var mountain = Resources.Load<GameObject>("Props/prop_mountain_backdrop_voxel");
            if (mountain != null)
            {
                Vector2 dir = new Vector2(-0.59f, -0.8f);
                for (int m = -1; m <= 1; m++)
                {
                    Vector2 side = new Vector2(-dir.y, dir.x) * (m * 95f);
                    Vector2 at2 = dir * (125f + Mathf.Abs(m) * 20f) + side * 0.8f;
                    Vector3 at = transform.TransformPoint(new Vector3(at2.x, 0f, at2.y));
                    float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + m * 12f;
                    GameObject g = PlaceProp(mountain, yard, at, transform.position.y - 8f, yaw, 3.4f - Mathf.Abs(m) * 0.5f, 0f);
                    foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);
                }
            }
        }

        static GameObject PlaceProp(GameObject prefab, Transform parent, Vector3 center, float bottom, float yaw, float scale, float fitWidth)
        {
            GameObject g = Instantiate(prefab, parent);
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
            g.transform.localScale = Vector3.one;
            Bounds b = WorldBounds(g);
            float s = scale;
            if (fitWidth > 0f && b.size.x > 0.001f) s = fitWidth / b.size.x;
            g.transform.localScale = g.transform.localScale * s;
            b = WorldBounds(g);
            g.transform.position += new Vector3(center.x - b.center.x, bottom - b.min.y, center.z - b.center.z);
            return g;
        }

        static Bounds WorldBounds(GameObject g)
        {
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
        static readonly System.Collections.Generic.Dictionary<Color, Material> PlainCache = new System.Collections.Generic.Dictionary<Color, Material>();

        static Material Plain(Material source, Color color)
        {
            if (PlainCache.TryGetValue(color, out Material m) && m != null) return m;
            m = new Material(source);
            foreach (string prop in m.GetTexturePropertyNames()) m.SetTexture(prop, null);
            m.SetColor(TintId, color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            PlainCache[color] = m;
            return m;
        }

        Vector3 Ground(Vector2 local)
        {
            Vector3 p = transform.TransformPoint(new Vector3(local.x, 0f, local.y));
            if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            if (Chopper.World.WorldGround.Exists) p.y = Chopper.World.WorldGround.HeightAt(p);
            return p;
        }

        void DressCabin()
        {
            Transform house = null;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == "House") { house = child; break; }

            var root = new GameObject("Cabin").transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * HouseScale;
            root.localPosition = Vector3.down * Chopper.World.Generation.WorldGen.HutSink;
            foreach (PlayerSpawnPoint point in new System.Collections.Generic.List<PlayerSpawnPoint>(PlayerSpawnPoint.Active))
            {
                Vector3 local = transform.InverseTransformPoint(point.transform.position);
                if (Mathf.Abs(local.x) < HouseHalfSize.x && Mathf.Abs(local.z) < HouseHalfSize.y && local.y > -1f && local.y < 1.5f)
                    point.transform.position -= Vector3.up * Chopper.World.Generation.WorldGen.HutSink;
            }
            // Piece-metres under the scaled root: basement floor, main floor, loft floor (= the eaves).
            const float main = 2f, second = 4f, third = 6f, loft = 8f;
            const float doorX = 1f;
            // The basement stairwell is two tiles long at the -X end, so the flight rises at half the pitch (W11-14).
            var basementHoles = new[] { new Vector2(-5f, -1f), new Vector2(-5f, 1f) };
            var secondHoles = new[] { new Vector2(5f, -1f), new Vector2(5f, 1f) };
            var thirdHoles = basementHoles;

            foreach (float x in TilesX)
            {
                foreach (float z in TilesZ)
                {
                    Put(root, "Wood floor", new Vector3(x, 0f, z), 0f);
                    if (System.Array.IndexOf(basementHoles, new Vector2(x, z)) < 0) Put(root, "Wood floor", new Vector3(x, main, z), 0f);
                    if (System.Array.IndexOf(secondHoles, new Vector2(x, z)) < 0) Put(root, "Wood floor", new Vector3(x, second, z), 0f);
                    if (System.Array.IndexOf(thirdHoles, new Vector2(x, z)) < 0) Put(root, "Wood floor", new Vector3(x, third, z), 0f);
                }
            }

            // Basement walls, stone tinted and without windows: the terrace (WorldGen.TerraceRaise) buries them, leaving
            // a stone foundation course showing; the main floor walls with windows and the door.
            Walls(root, 0f,
                new[] { "Wood wall", "Wood wall", "Wood wall", "Wood wall", "Wood wall", "Wood wall" },
                new[] { "Wood wall", "Wood wall", "Wood wall", "Wood wall", "Wood wall", "Wood wall" }, 99f, BasementStone);
            Walls(root, main,
                new[] { "Wood wall", "Log window", "Wood wall", "Wood wall", "Log window", "Wood wall" },
                new[] { "Log window", "Wood wall", "Log window", "Doorway", "Log window", "Wood wall" }, 1f, Color.white);
            Walls(root, second,
                new[] { "Wood wall", "Log window", "Wood wall", "Wood wall", "Log window", "Wood wall" },
                new[] { "Log window", "Wood wall", "Wood wall", "Log window", "Wood wall", "Log window" }, -1f, Color.white);
            Walls(root, third,
                new[] { "Wood wall", "Wood wall", "Log window", "Wood wall", "Log window", "Wood wall" },
                new[] { "Log window", "Wood wall", "Wood wall", "Log window", "Wood wall", "Log window" }, -1f, Color.white);

            // Stairs: from the basement up toward the back at the -X end, and from the main floor up into the loft
            // (rising toward +X).
            foreach (Transform piece in root)
            {
                Vector3 lp = piece.localPosition;
                if (piece.name == "Wood wall" && Mathf.Approximately(lp.x, LayoutHalf.x) && Mathf.Approximately(lp.y, third) && Mathf.Approximately(lp.z, 3f)) { Destroy(piece.gameObject); break; }
            }
            Put(root, "Doorway", new Vector3(LayoutHalf.x, third, 3f), 90f);
            Chopper.World.Ladder.Clear();
            {
                Vector3 lo = root.TransformPoint(new Vector3(LayoutHalf.x + 0.15f, -0.2f, 2.15f));
                Vector3 hi = root.TransformPoint(new Vector3(LayoutHalf.x + 1.3f, third, 3.45f)) + Vector3.up * 0.75f;
                var zone = new Bounds((lo + hi) * 0.5f, Vector3.zero);
                zone.Encapsulate(lo); zone.Encapsulate(hi);
                Chopper.World.Ladder.Add(zone, -root.right);
                LadderZone = zone;
            }
            Put(root, "Stairs", new Vector3(-5f, 0f, 0f), 180f, new Vector3(1f, 1f, 2f));
            Put(root, "Stairs", new Vector3(5f, main, 0f), 180f, new Vector3(1f, 1f, 2f));
            Put(root, "Stairs", new Vector3(-5f, second, 0f), 180f, new Vector3(1f, 1f, 2f));
            BuildPorch(root, doorX, main);

            // Thatch: ridge along X at z = 0, two roof pieces (4 m rise) down the front and the back; log gables at both
            // ends, stretched to the 8 m depth and 4 m height.
            foreach (float x in TilesX)
            {
                Put(root, "Roof", new Vector3(x, loft, 3f), 180f);
                Put(root, "Roof", new Vector3(x, loft + 2f, 1f), 180f);
                Put(root, "Roof", new Vector3(x, loft, -3f), 0f);
                Put(root, "Roof", new Vector3(x, loft + 2f, -1f), 0f);
            }
            // The gable triangle is 2 wide and 1 high per unit scale: 8 wide and 4 high fills the 45 degree roof end.
            Put(root, "Log gable", new Vector3(LayoutHalf.x, loft, 0f), 90f, new Vector3(4f, 4f, 1f));
            Put(root, "Log gable", new Vector3(-LayoutHalf.x, loft, 0f), -90f, new Vector3(4f, 4f, 1f));

            // The piece meshes are the collision. The look is one hall (stone, logs, a closed roof, a porch).
            GameObject hallPrefab = Resources.Load<GameObject>("Props/hall_hub");
            if (hallPrefab != null)
            {
                // The hall is the shell only (stone, log walls, roof, porch). Floors and stairs inside the house keep
                // their own look, or the basement, main floor and loft were invisible (user, 2026-10-12: "floors were
                // missing from the hut"). The porch deck and its steps are drawn by the hall.
                foreach (Transform piece in root)
                {
                    Vector3 p = piece.localPosition;
                    bool inside = Mathf.Abs(p.x) < LayoutHalf.x && Mathf.Abs(p.z) < LayoutHalf.y;
                    bool basementWall = piece.name == "Wood wall" && p.y < 1f;
                    bool keep = (inside && (piece.name == "Wood floor" || piece.name == "Stairs")) || basementWall;
                    if (basementWall)
                    {
                        Vector3 inset = Mathf.Abs(Mathf.Abs(p.z) - LayoutHalf.y) < 0.2f
                            ? new Vector3(0f, 0f, -Mathf.Sign(p.z) * 0.12f)
                            : new Vector3(-Mathf.Sign(p.x) * 0.12f, 0f, 0f);
                        piece.localPosition = p + inset;
                    }
                    if (keep) continue;
                    foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = false;
                }
                foreach (Transform piece in root)
                {
                    if (piece.name != "Wood floor") continue;
                    Vector3 fp = piece.localPosition;
                    if (Mathf.Abs(fp.x) >= LayoutHalf.x || Mathf.Abs(fp.z) >= LayoutHalf.y) continue;
                    Renderer look = piece.GetComponentInChildren<Renderer>(true);
                    if (look == null) continue;
                    Material wood = look.sharedMaterial;
                    Bounds b = look.localBounds;
                    foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    Bounds old = look.bounds;
                    var boardPrefab = Resources.Load<GameObject>("Props/prop_floor_boards_voxel");
                    if (boardPrefab == null) { foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true; continue; }
                    PlaceProp(boardPrefab, piece, old.center, old.max.y - 0.1f, 0f, 1f, old.size.x);
                }
                foreach (Transform piece in root)
                {
                    if (piece.name != "Stairs") continue;
                    Transform treads = piece.Find("Treads");
                    Renderer look = piece.GetComponentInChildren<Renderer>(true);
                    if (treads == null || look == null) continue;
                    Material wood = look.sharedMaterial;
                    foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    int step = 0;
                    foreach (BoxCollider box in treads.GetComponents<BoxCollider>())
                    {
                        GameObject tread = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        tread.name = "TreadLook";
                        Destroy(tread.GetComponent<Collider>());
                        tread.transform.SetParent(treads, false);
                        tread.transform.localPosition = box.center;
                        tread.transform.localScale = box.size;
                        var mr = tread.GetComponent<MeshRenderer>();
                        mr.sharedMaterial = wood;
                        var block = new MaterialPropertyBlock();
                        block.SetColor(TintId, step % 2 == 0 ? new Color(0.78f, 0.58f, 0.40f) : new Color(0.66f, 0.47f, 0.31f));
                        mr.SetPropertyBlock(block);
                        step++;
                    }
                }
                Transform hall = Instantiate(hallPrefab, root).transform;
                hall.name = "Hall";
                hall.localPosition = Vector3.zero;
                hall.localRotation = Quaternion.identity;
                hall.localScale = Vector3.one;
            }

            // Furniture at its own size, placed in piece-metres (scaled to metres by Spot). The bed and the workbench are
            // copied for the second player.
            if (house != null)
            {
                HideOldHouse();
                PlaceAt(house.Find("Fireplace"), Spot(-2.9f, main, -3.6f), 0f);
                PlaceAt(house.Find("StorageChest"), Spot(0.6f, main, -3.5f), 0f);
                Transform bed = house.Find("Bed");
                if (bed != null)
                {
                    PlaceAt(bed, Spot(-2.6f, second, -2.9f), 90f);
                    Transform bed2 = Instantiate(bed.gameObject, bed.parent).transform;
                    bed2.name = "Bed";
                    PlaceAt(bed2, Spot(-0.6f, second, -2.9f), 90f);
                }
            }
            GameObject yardBench = GameObject.Find("HutYard/Workbench");
            if (yardBench != null)
            {
                PlaceAt(yardBench.transform, Spot(1.6f, third, -3.3f), 0f);
                Transform bench2 = Instantiate(yardBench, yardBench.transform.parent).transform;
                bench2.name = "Workbench";
                PlaceAt(bench2, Spot(-1f, 0f, 2.8f), 0f);
            }
            Furnish(main, second, third);
            OpenEnemyPaths(root);
            _houseHalfExtents = HouseHalfSize;
        }

        void Furnish(float main, float second, float third)
        {
            var spots = new (string prop, float x, float y, float z, float yaw)[]
            {
                ("prop_mine_barrel_voxel", -2.2f, 0f, -3.2f, 0f), ("prop_mine_barrel_voxel", -1.2f, 0f, -3.3f, 30f), ("prop_mine_barrel_voxel", 2.2f, 0f, -3.2f, 10f),
                ("prop_mine_crate_voxel", 3.2f, 0f, -3.1f, 0f), ("prop_mine_crate_voxel", 4.3f, 0f, -3.1f, 15f), ("prop_mine_crate_voxel", 2.6f, 0f, 2.9f, 0f),
                ("prop_hut_sack_voxel", 4.2f, 0f, 2.6f, 0f), ("prop_hut_sack_voxel", 4.8f, 0f, 3.1f, 40f), ("prop_hut_sack_voxel", 0.4f, 0f, -3.3f, 70f),
                ("prop_hut_notice_voxel", 3.2f, main, -3.75f, 0f),
                ("prop_hut_table_voxel", -2.2f, main, 0.8f, 0f), ("prop_hut_table_voxel", 2.4f, main, 0.6f, 90f),
                ("prop_hut_stool_voxel", -3.2f, main, 0.8f, 0f), ("prop_hut_stool_voxel", -1.2f, main, 0.8f, 0f), ("prop_hut_stool_voxel", 2.4f, main, -0.5f, 0f), ("prop_hut_stool_voxel", 2.4f, main, 1.7f, 0f),
                ("prop_hut_bunk_voxel", 0.6f, second, 3.2f, 180f), ("prop_hut_bunk_voxel", 2.6f, second, 3.2f, 180f),
                ("prop_mine_crate_voxel", -2.6f, second, -1.5f, 0f), ("prop_mine_crate_voxel", 4.4f, second, 3.2f, 0f),
                ("prop_mine_barrel_voxel", -4.0f, main, -3.3f, 0f), ("prop_mine_crate_voxel", -4.0f, main, -2.3f, 20f), ("prop_hut_sack_voxel", 0.4f, main, -2.6f, 0f),
                ("prop_hut_table_voxel", -0.2f, main, 2.4f, 0f), ("prop_hut_stool_voxel", -1.2f, main, 2.4f, 0f), ("prop_hut_stool_voxel", 0.8f, main, 2.4f, 0f),
                ("prop_hut_bunk_voxel", -2.8f, second, 3.2f, 180f), ("prop_hut_bunk_voxel", 4.4f, second, -3.2f, 0f), ("prop_hut_stool_voxel", 1.6f, second, 0.4f, 0f), ("prop_hut_table_voxel", 1.6f, second, -0.6f, 0f),
                ("prop_mine_crate_voxel", 0.9f, second, -3.3f, 0f), ("prop_mine_lantern_voxel", 1.6f, second, -0.6f, 0f),
                ("prop_hut_toolwall_voxel", -1.2f, third, -3.8f, 0f), ("prop_hut_table_voxel", 3.8f, third, -3.2f, 0f), ("prop_hut_vise_voxel", 3.8f, third, -3.2f, 0f),
                ("prop_mine_crate_voxel", 4.6f, third, 1.0f, 0f), ("prop_mine_barrel_voxel", 4.6f, third, 0.0f, 0f), ("prop_hut_sack_voxel", -0.4f, third, 3.2f, 0f), ("prop_hut_stool_voxel", -1.2f, third, 2.6f, 0f),
                ("prop_mine_lantern_voxel", 1.6f, third, -3.0f, 0f), ("prop_mine_lantern_voxel", -0.2f, main, 2.4f, 0f),
                ("prop_hut_toolwall_voxel", 1.6f, third, -3.8f, 0f), ("prop_hut_vise_voxel", -0.4f, third, -3.3f, 0f),
                ("prop_hut_wheel_voxel", -2.4f, third, 2.6f, 200f), ("prop_hut_stool_voxel", 1.6f, third, -2.3f, 0f), ("prop_hut_table_voxel", 3.6f, third, 2.4f, 0f),
            };
            Transform holder = new GameObject("HutFurniture").transform;
            holder.SetParent(transform, false);
            int placed = 0;
            foreach (var s in spots)
            {
                GameObject prefab = Resources.Load<GameObject>("Props/" + s.prop);
                if (prefab == null) { Debug.LogWarning("[Hut] missing prop " + s.prop); continue; }
                Vector3 at = transform.TransformPoint(Spot(s.x, s.y, s.z));
                GameObject g = PlaceProp(prefab, holder, at, at.y - 0.1f, transform.eulerAngles.y + s.yaw, 1f, 0f);
                g.name = s.prop;
                Bounds gb = WorldBounds(g);
                if (placed < 3 || s.y > 5f) Debug.Log($"[Hut] prop {s.prop} at {gb.center:F2} size {gb.size:F2}");
                if (g.GetComponentInChildren<Collider>() == null)
                {
                    Bounds b = WorldBounds(g);
                    var box = g.AddComponent<BoxCollider>();
                    box.center = g.transform.InverseTransformPoint(b.center);
                    Vector3 ls = g.transform.lossyScale;
                    box.size = new Vector3(b.size.x / Mathf.Max(0.001f, ls.x), b.size.y / Mathf.Max(0.001f, ls.y), b.size.z / Mathf.Max(0.001f, ls.z));
                    if (s.prop.Contains("notice") || s.prop.Contains("toolwall")) box.enabled = false;
                }
                placed++;
            }
            foreach (var (fy, lx, lz) in new[] { (0f, 0f, 0f), (main, -1f, 0f), (main, 3f, 0f), (second, -2f, 0f), (second, 2.5f, 0f), (third, -2f, 0f), (third, 2.5f, -1f) })
            {
                var lamp = new GameObject("HutLamp").AddComponent<Light>();
                lamp.transform.SetParent(holder, false);
                lamp.transform.position = transform.TransformPoint(Spot(lx, fy, lz) + Vector3.up * 2.6f);
                lamp.type = LightType.Point;
                lamp.color = new Color(1f, 0.74f, 0.45f);
                lamp.range = 11f;
                lamp.intensity = fy > 5f ? 3.2f : 2.4f;
                lamp.shadows = LightShadows.None;
            }
            Debug.Log($"[Hut] furniture {placed}/{spots.Length}");
        }

        // The old house carved its whole floor out of the NavMesh (KeepOut), and that box still crossed the new
        // doorway, so an enemy inside the hall had no complete path to the yard. The stair pieces also carved away
        // their own treads. Walls and doorway jambs still block; the opening and a link up each flight stay open.
        void OpenEnemyPaths(Transform cabin)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == "KeepOut") child.gameObject.SetActive(false);
            foreach (Transform piece in cabin)
            {
                if (piece.name != "Stairs") continue;
                foreach (UnityEngine.AI.NavMeshObstacle obstacle in piece.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
                    obstacle.enabled = false;
                var link = piece.gameObject.AddComponent<Unity.AI.Navigation.NavMeshLink>();
                // The link ignores the object's scale, so the piece-metre ends are scaled here. Stairs rise from
                // local -Z on the lower floor to local +Z, 2 m up.
                Vector3 scale = piece.lossyScale;
                link.startPoint = Vector3.Scale(scale, new Vector3(0f, 0.2f, -1.4f));
                link.endPoint = Vector3.Scale(scale, new Vector3(0f, 2.15f, 1.4f));
                link.width = 1.2f;
                link.bidirectional = true;
            }
        }

        // The porch of concept 2.0: a plank deck at the main floor in front of the door, a short flight up from the
        // terrace, and a small cross gable (two roof pieces with the ridge running out from the wall, a log gable with
        // the crossed beams at the front) on two log posts. Piece-metres under the scaled root.
        void BuildPorch(Transform root, float doorX, float main)
        {
            float terrace = (Chopper.World.Generation.WorldGen.TerraceHeight + Chopper.World.Generation.WorldGen.HutSink) / HouseScale;
            float z0 = LayoutHalf.y;                       // the front wall
            Put(root, "Wood floor", new Vector3(doorX - 1f, main, z0 + 1f), 0f);
            Put(root, "Wood floor", new Vector3(doorX + 1f, main, z0 + 1f), 0f);
            // Steps from the terrace up to the deck (rising toward the house).
            float rise = main + 0.25f - terrace;
            Put(root, "Stairs", new Vector3(doorX, terrace, z0 + 2.5f), 180f, new Vector3(1f, rise / 2f, 0.5f));
            // Roof: eaves `eave` above the deck, ridge along Z over the door, out past the deck by a little.
            // The eaves clear the door (1.75 high); a flatter pitch (rise 1.2 over 2) keeps the ridge near the main eave.
            const float eave = 1.75f, depth = 2.6f, pitch = 0.6f;
            float zc = z0 + depth * 0.5f;
            Put(root, "Roof", new Vector3(doorX - 1f, main + eave, zc), 90f, new Vector3(depth / 2f, pitch, 1f));
            Put(root, "Roof", new Vector3(doorX + 1f, main + eave, zc), -90f, new Vector3(depth / 2f, pitch, 1f));
            Put(root, "Log gable", new Vector3(doorX, main + eave, z0 + depth - 0.1f), 0f, new Vector3(2f, 2f * pitch, 1f));
            // The back of the cross gable stands above the main eave, against the main roof; close it with logs.
            Put(root, "Log gable", new Vector3(doorX, main + eave, z0 + 0.1f), 180f, new Vector3(2f, 2f * pitch, 1f));
            // Two log posts at the deck's front corners, from the planks to the eaves.
            foreach (float side in new[] { -1.8f, 1.8f })
                Put(root, "Wood wall", new Vector3(doorX + side, main + 0.25f, z0 + 1.85f), 0f, new Vector3(0.125f, (eave - 0.25f) / 2f, 1f));
        }

        // One storey's walls at height y: six pieces along the back (-Z) and the front (+Z), from -X to +X, and four along
        // each end, with a window instead of a wall at endWindowZ. tint colours every piece (white leaves them as they are).
        void Walls(Transform root, float y, string[] back, string[] front, float endWindowZ, Color tint)
        {
            float d = LayoutHalf.y, w = LayoutHalf.x;
            for (int i = 0; i < TilesX.Length; i++)
            {
                Tint(Put(root, back[i], new Vector3(TilesX[i], y, -d), 0f), tint);
                Tint(Put(root, front[i], new Vector3(TilesX[i], y, d), 0f), tint);
            }
            foreach (float z in TilesZ)
            {
                string end = Mathf.Approximately(z, endWindowZ) ? "Log window" : "Wood wall";
                Tint(Put(root, end, new Vector3(-w, y, z), 90f), tint);
                Tint(Put(root, end, new Vector3(w, y, z), 90f), tint);
            }
        }

        static void Tint(GameObject piece, Color tint)
        {
            if (piece == null || tint == Color.white) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(TintId, tint);
            foreach (Renderer r in piece.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
        }

        // A point on a floor of the house, from piece-metres to the hut's local metres (just above the planks).
        static Vector3 Spot(float x, float floor, float z) => new Vector3(x, floor, z) * HouseScale + Vector3.up * (0.12f - Chopper.World.Generation.WorldGen.HutSink);

        void PlaceAt(Transform prop, Vector3 local, float yaw)
        {
            if (prop == null) return;
            prop.position = transform.TransformPoint(local);
            prop.rotation = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
        }

        GameObject Put(Transform root, string displayName, Vector3 localPosition, float yaw) => Put(root, displayName, localPosition, yaw, Vector3.one);

        GameObject Put(Transform root, string displayName, Vector3 localPosition, float yaw, Vector3 scale)
        {
            PieceCatalog catalog = BuildManager.Instance != null ? BuildManager.Instance.Catalog : null;
            GameObject prefab = null;
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    PieceDefinition piece = catalog.Get(i);
                    if (piece != null && piece.DisplayName == displayName) { prefab = piece.Prefab; break; }
                }
            }
            if (prefab == null)
            {
                Debug.LogWarning("[Hut] missing piece " + displayName);
                return null;
            }
            GameObject copy = Instantiate(prefab, root);
            copy.name = displayName;
            copy.transform.localPosition = localPosition;
            copy.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            copy.transform.localScale = scale;
            return copy;
        }

        void HideOldHouse()
        {
            Transform house = null;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == "House") { house = child; break; }
            if (house == null) return;
            foreach (Renderer renderer in house.GetComponentsInChildren<Renderer>(true))
            {
                if (Kept(renderer.transform)) continue;
                renderer.enabled = false;
            }
            foreach (Collider collider in house.GetComponentsInChildren<Collider>(true))
            {
                if (Kept(collider.transform)) continue;
                collider.enabled = false;
            }
        }

        static bool Kept(Transform t)
        {
            while (t != null)
            {
                if (t.name is "Bed" or "StorageChest" or "Fireplace" or "Workbench") return true;
                t = t.parent;
            }
            return false;
        }

        static bool UnderCabin(Transform t)
        {
            while (t != null)
            {
                if (t.name == "Cabin") return true;
                t = t.parent;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _level.OnChange += OnLevelChanged;
            _health.OnChange += OnHealthChanged;
            _repaired.OnChange += OnRepairedChanged;
            ApplyRuinLook();
            HideOldHouse();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _level.OnChange -= OnLevelChanged;
            _health.OnChange -= OnHealthChanged;
            _repaired.OnChange -= OnRepairedChanged;
        }

        private void OnRepairedChanged(bool previous, bool next, bool asServer)
        {
            // Host: the server side call, whose arguments are trustworthy (see PlayerWood).
            if (asServer != IsServerInitialized) return;
            ApplyRuinLook();
            if (next && !previous) Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up * 2f, 1f);
        }

        // A broken hut is a ruin: caved in roof, knocked down walls, planks everywhere, a cold fireplace and darker
        // timber (the "RuinOnly" parts). Repairing it swaps in the whole house ("IntactOnly").
        private void ApplyRuinLook()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            bool ruined = !_repaired.Value;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "IntactOnly" && t.gameObject.activeSelf == ruined) t.gameObject.SetActive(!ruined);
                else if (t.name == "RuinOnly" && t.gameObject.activeSelf != ruined) t.gameObject.SetActive(ruined);
            }
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (UnderCabin(renderer.transform)) continue;
                if (!ruined)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(BaseColorId)) continue;
                Color color = material.GetColor(BaseColorId);
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, new Color(color.r * RuinDarkening, color.g * RuinDarkening, color.b * RuinDarkening, color.a));
                renderer.SetPropertyBlock(_block);
            }
        }

        private void OnLevelChanged(int previous, int next, bool asServer)
        {
            if (asServer == IsServerInitialized && next > previous) Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up * 2f, 0.9f);
        }

        // Hits on the hut thump; repairs (health going up) stay quiet because the deposit sound covers them.
        private void OnHealthChanged(int previous, int next, bool asServer)
        {
            // Host: the server side call, as in PlayerWood (the host's client side call sees previous equal to next).
            if (asServer == IsServerInitialized && next < previous) Sfx.Play(ProceduralSfx.HutHit, transform.position + Vector3.up, 1f, 1f, 0.1f);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerReset();
        }

        // Server only. Also used by the game over restart.
        public void ServerReset()
        {
            _level.Value = 0;
            _health.Value = _config.HutStartHealth;
            _woodStore.Value = 0;
            _repaired.Value = !_config.HutStartsRuined;
            _repairWood.Value = 0;
            SharedChest chest = GetComponent<SharedChest>();
            if (chest != null && chest.IsServerInitialized) chest.ServerReset();
        }

        // Server only. Loading a saved world.
        public void ServerLoad(bool repaired, int repairWood, int level, int health, int woodStore)
        {
            _repaired.Value = repaired;
            _repairWood.Value = Mathf.Max(0, repairWood);
            _level.Value = Mathf.Max(0, level);
            _health.Value = Mathf.Clamp(health, 1, MaxHealth);
            _woodStore.Value = Mathf.Max(0, woodStore);
        }

        // Server only. Moves the player's carried wood into the shared store. While the hut is still broken, the wood
        // goes into repairing it first, and the rest (if any) into the store.
        public void ServerDeposit(PlayerWood player)
        {
            int wood = player.ServerTakeAll();
            if (!_repaired.Value && wood > 0)
            {
                int missing = Mathf.Max(0, _config.HutRepairWood - _repairWood.Value);
                int used = Mathf.Min(wood, missing);
                _repairWood.Value += used;
                wood -= used;
                if (_repairWood.Value >= _config.HutRepairWood) ServerSetRepaired(true);
            }
            _woodStore.Value += wood;
        }

        // Server only. The hut is fixed: the whole house swaps in (Hut.ApplyRuinLook on every client).
        public void ServerSetRepaired(bool repaired)
        {
            bool was = _repaired.Value;
            _repaired.Value = repaired;
            if (repaired) _repairWood.Value = _config.HutRepairWood;
            if (repaired && !was && EnemyDirector.Instance != null)
            {
                EnemyDirector.Instance.ServerAnnounce("The hut is repaired!");
            }
        }

        // Server only. Repairs the hut, paying with store wood first and then the wood the player carries. Returns the
        // health restored and the wood spent.
        public int ServerRepair(PlayerWood player, out int woodSpent)
        {
            int perWood = Mathf.Max(1, _config.HutHealthPerWood);
            int budget = _woodStore.Value + player.Carried;
            int startBudget = budget;
            int restored = 0;

            int hutMissing = Mathf.Max(0, MaxHealth - _health.Value);
            if (budget > 0 && hutMissing > 0)
            {
                int use = Mathf.Min(budget, Mathf.CeilToInt(hutMissing / (float)perWood));
                int gain = Mathf.Min(hutMissing, use * perWood);
                _health.Value += gain;
                budget -= use;
                restored += gain;
            }

            woodSpent = startBudget - budget;
            int fromStore = Mathf.Min(_woodStore.Value, woodSpent);
            _woodStore.Value -= fromStore;
            if (woodSpent > fromStore) player.ServerTrySpend(woodSpent - fromStore);
            return restored;
        }

        // Server only. Sets the hut back to full health (developer tools).
        public void ServerHealToFull() => _health.Value = MaxHealth;

        // Server only. Adds wood straight to the store (boss rewards).
        public void ServerAddStoreWood(int amount)
        {
            _woodStore.Value += Mathf.Max(0, amount);
        }

        // Server only. Spends store wood on an upgrade. Returns false when there is not enough.
        public bool ServerTrySpend(int cost)
        {
            if (cost > _woodStore.Value) return false;
            _woodStore.Value -= cost;
            return true;
        }

        // Server only. Raises the hut level and its max health, healing by the added amount.
        public bool ServerTryUpgrade()
        {
            if (!DropRules.TryGetNextCost(_config.HutUpgradeCosts, _level.Value, out int cost)) return false;
            if (!ServerTrySpend(cost)) return false;

            _level.Value++;
            _health.Value = Mathf.Min(MaxHealth, _health.Value + _config.HutHealthPerLevel);
            return true;
        }

        // Server only.
        public void ServerDamage(int amount)
        {
            if (_health.Value <= 0) return;
            _health.Value = Mathf.Max(0, _health.Value - Mathf.Max(0, amount));
            if (_health.Value == 0 && EnemyDirector.Instance != null) EnemyDirector.Instance.ServerGameOver();
        }
    }
}
