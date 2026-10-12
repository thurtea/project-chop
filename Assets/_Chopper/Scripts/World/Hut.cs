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

        // Concept 2.0's home is a small log cabin: door on the gable (+Z), one window, two thatch slopes.
        // The bed, chest, and fireplace move inside. The old pixel house and the longhouse stay hidden.
        void DressCabin()
        {
            Transform house = null;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == "House") { house = child; break; }

            if (house != null)
            {
                HideOldHouse();
                MoveInside(house, "Fireplace", new Vector3(0f, 0.1f, -1.55f), Quaternion.identity);
                MoveInside(house, "Bed", new Vector3(0f, 0.1f, 0.15f), Quaternion.Euler(0f, 90f, 0f));
                MoveInside(house, "StorageChest", new Vector3(0f, 0.1f, 1.7f), Quaternion.identity);
            }

            var root = new GameObject("Cabin").transform;
            root.SetParent(transform, false);
            // 2 m wide, 4 m deep, centered on the hut. Door and gable face +Z. Roofs meet on the ridge at z = 0.
            Put(root, "Wood floor", new Vector3(0f, 0f, -1f), 0f);
            Put(root, "Wood floor", new Vector3(0f, 0f, 1f), 0f);
            Put(root, "Doorway", new Vector3(0f, 0f, 2f), 0f);
            Put(root, "Wood wall", new Vector3(0f, 0f, -2f), 0f);
            Put(root, "Wood wall", new Vector3(-1f, 0f, -1f), 90f);
            Put(root, "Wood wall", new Vector3(-1f, 0f, 1f), 90f);
            Put(root, "Log window", new Vector3(1f, 0f, -1f), 90f);
            Put(root, "Wood wall", new Vector3(1f, 0f, 1f), 90f);
            Put(root, "Log gable", new Vector3(0f, 2f, 2f), 0f);
            Put(root, "Roof", new Vector3(0f, 2f, -1f), 0f);
            Put(root, "Roof", new Vector3(0f, 2f, 1f), 180f);
            _houseHalfExtents = new Vector2(1f, 2f);
        }

        static void MoveInside(Transform house, string name, Vector3 localPosition, Quaternion localRotation)
        {
            Transform prop = house.Find(name);
            if (prop == null) return;
            prop.localPosition = localPosition;
            prop.localRotation = localRotation;
        }

        void Put(Transform root, string displayName, Vector3 localPosition, float yaw)
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
                return;
            }
            GameObject copy = Instantiate(prefab, root);
            copy.name = displayName;
            copy.transform.localPosition = localPosition;
            copy.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            copy.transform.localScale = Vector3.one;
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
