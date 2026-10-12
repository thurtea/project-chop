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
        }

        // The hub house (user, 2026-10-12, matched to chopper-3D-concept-2.0.jpg): a wide, low log house for two under
        // a big thatch roof, built from the hammer pieces. Laid out 12 x 8 piece-metres and scaled by HouseScale to
        // 18 m wide and 12 m deep, the long side to the front (+Z) with the door and the log steps. It stands on its
        // basement (stone-tinted walls, the raised step the concept house stands on); the main floor is one storey of
        // log walls; the roof ridge runs the length of the house, two roof pieces down each side, with a log gable and
        // crossed beams at each end, and the upstairs is a loft inside the roof. Stairs go up from the basement at the
        // -X end and from the main floor into the loft. Two workbenches (main floor and basement), two beds (loft), the
        // chest and the fireplace on the main floor.
        public const float HouseScale = 1.5f;
        public const float StoreyHeight = 2f * HouseScale;
        // Half the house in piece-metres (the layout) and in metres (the walls enemies hit).
        static readonly Vector2 LayoutHalf = new Vector2(6f, 4f);
        public static readonly Vector2 HouseHalfSize = LayoutHalf * HouseScale;
        static readonly float[] TilesX = { -5f, -3f, -1f, 1f, 3f, 5f };
        static readonly float[] TilesZ = { -3f, -1f, 1f, 3f };
        static readonly Color BasementStone = new Color(0.62f, 0.60f, 0.58f);
        static readonly int TintId = Shader.PropertyToID("_BaseColor");

        void DressCabin()
        {
            Transform house = null;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == "House") { house = child; break; }

            var root = new GameObject("Cabin").transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * HouseScale;
            // Piece-metres under the scaled root: basement floor, main floor, loft floor (= the eaves).
            const float main = 2f, loft = 4f;
            const float doorX = 1f;
            var basementHole = new Vector2(-5f, -1f);   // basement stairs, under the main floor
            var loftHole = new Vector2(3f, 1f);         // main floor stairs, under the loft

            foreach (float x in TilesX)
            {
                foreach (float z in TilesZ)
                {
                    Put(root, "Wood floor", new Vector3(x, 0f, z), 0f);
                    if (new Vector2(x, z) != basementHole) Put(root, "Wood floor", new Vector3(x, main, z), 0f);
                    // The loft covers the middle of the house, where the roof is high enough to stand.
                    if (Mathf.Abs(z) < 2f && new Vector2(x, z) != loftHole) Put(root, "Wood floor", new Vector3(x, loft, z), 0f);
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

            // Stairs: from the basement up toward the back at the -X end, and from the main floor up into the loft
            // (rising toward +X).
            Put(root, "Stairs", new Vector3(basementHole.x, 0f, basementHole.y), 180f);
            Put(root, "Stairs", new Vector3(loftHole.x, main, loftHole.y), 90f);
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
                    // The basement's stone-tinted walls too: the terrace buries their outside, and without them the
                    // cellar showed the terrace's dirt.
                    bool basementWall = piece.name == "Wood wall" && p.y < 1f;
                    bool keep = (inside && (piece.name == "Wood floor" || piece.name == "Stairs")) || basementWall;
                    if (keep) continue;
                    foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = false;
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
                PlaceAt(house.Find("Fireplace"), Spot(0f, main, -3.6f), 0f);
                PlaceAt(house.Find("StorageChest"), Spot(-2.6f, main, -3.6f), 0f);
                Transform bed = house.Find("Bed");
                if (bed != null)
                {
                    PlaceAt(bed, Spot(-3.6f, loft, 0f), 90f);
                    Transform second = Instantiate(bed.gameObject, bed.parent).transform;
                    second.name = "Bed";
                    PlaceAt(second, Spot(-1.6f, loft, 0f), 90f);
                }
            }
            GameObject yardBench = GameObject.Find("HutYard/Workbench");
            if (yardBench != null)
            {
                PlaceAt(yardBench.transform, Spot(3.2f, main, -3.4f), 0f);
                Transform second = Instantiate(yardBench, yardBench.transform.parent).transform;
                second.name = "Workbench";
                PlaceAt(second, Spot(-1f, 0f, 2.8f), 0f);
            }
            OpenEnemyPaths(root);
            _houseHalfExtents = HouseHalfSize;
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
            float terrace = Chopper.World.Generation.WorldGen.TerraceHeight / HouseScale;
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
        static Vector3 Spot(float x, float floor, float z) => new Vector3(x, floor, z) * HouseScale + Vector3.up * 0.12f;

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
