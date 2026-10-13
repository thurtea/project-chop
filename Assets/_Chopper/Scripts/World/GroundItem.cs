using System.Collections.Generic;
using Chopper.Core;
using Chopper.Player;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.World
{
    public enum GroundItemKind
    {
        Wood = 0,
        Herb = 1,
        Fiber = 2,
        // W9-01: a player's axe (amount = its durability) and outfit, on the pond bank at the start or where they fell.
        Axe = 3,
        Outfit = 4
    }

    // Something lying on the ground, picked up by walking over it, the way Valheim does it: pressing interact on a
    // plant or a fallen branch knocks it loose, it pops out onto the ground, and whoever walks within AutoPickupRadius
    // with room for it takes it (the pickup card says what). It stays put while nobody has room, and rots away after
    // a while. The server decides who gets it. Spawned by EnemyDirector.ServerDropItem.
    public class GroundItem : NetworkBehaviour
    {
        public const float AutoPickupRadius = 1.6f;
        private const float MaxHeightGap = 2.5f;
        // A moment to see it land before it can be taken.
        private const float ReadySeconds = 0.45f;
        private const float LifetimeSeconds = 600f;
        private const float PopSeconds = 0.35f;
        private const float PopHeight = 0.6f;
        // A dropped sprig is this tall, whatever the size of the plant model.
        private const float SprigHeight = 0.28f;

        [SerializeField] private GameConfig _config;
        [SerializeField] private Material _woodMaterial;
        [SerializeField] private GameObject _herbModel;
        [SerializeField] private GameObject _fiberModel;

        private static readonly List<GroundItem> _all = new List<GroundItem>();

        private readonly SyncVar<int> _kind = new SyncVar<int>();
        private readonly SyncVar<int> _amount = new SyncVar<int>();
        // Where it popped out from, so every client can play the same little hop onto the ground.
        private readonly SyncVar<Vector3> _from = new SyncVar<Vector3>();

        private float _readyAt;
        private float _expiresAt;
        private Transform _view;
        private Mesh _mesh;
        private float _shownAt;

        public static IReadOnlyList<GroundItem> All => _all;
        public GroundItemKind Kind => (GroundItemKind)_kind.Value;
        public int Amount => _amount.Value;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _all.Remove(this);
        }

        // Server only, right after spawning.
        public void ServerSetup(GroundItemKind kind, int amount, Vector3 from)
        {
            _kind.Value = (int)kind;
            _amount.Value = Mathf.Max(1, amount);
            _from.Value = from;
            _readyAt = Time.time + ReadySeconds;
            // A player's own gear never rots away: it waits on the bank or where they fell.
            _expiresAt = kind == GroundItemKind.Axe || kind == GroundItemKind.Outfit ? float.MaxValue : Time.time + LifetimeSeconds;
        }

        // Server only. Gone at once (the world load replaces the fresh start kits with the saved gear).
        public void ServerRemove() => ServerManager.Despawn(gameObject);

        private void Update()
        {
            if (IsServerInitialized) ServerTick();
            if (_view != null) AnimateView();
        }

        private void ServerTick()
        {
            if (Time.time >= _expiresAt)
            {
                ServerManager.Despawn(gameObject);
                return;
            }
            if (Time.time < _readyAt) return;
            // The axe and the outfit wait for interact. Everything else is taken by walking over it.
            if (Kind == GroundItemKind.Axe || Kind == GroundItemKind.Outfit) return;
            Vector3 at = transform.position;
            foreach (PlayerWood player in PlayerWood.Active)
            {
                if (player == null) continue;
                Vector3 offset = player.transform.position - at;
                if (Mathf.Abs(offset.y) > MaxHeightGap) continue;
                offset.y = 0f;
                if (offset.sqrMagnitude > AutoPickupRadius * AutoPickupRadius) continue;
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null && health.IsDown) continue;
                int taken = ServerGive(player);
                if (taken <= 0) continue;
                _amount.Value -= taken;
                if (_amount.Value <= 0)
                {
                    ServerManager.Despawn(gameObject);
                    return;
                }
            }
        }

        public const float GearPickupRadius = 2.2f;

        // The nearest axe or outfit the player could take, within reach (flat). Null if none.
        // With a taker, gear it cannot take (a second axe, a second outfit) is skipped.
        public static GroundItem GearInReach(Vector3 position, GameObject taker = null)
        {
            bool hasAxe = false, dressed = false;
            if (taker != null)
            {
                PlayerAxe axe = taker.GetComponent<PlayerAxe>();
                PlayerUpgrades upgrades = taker.GetComponent<PlayerUpgrades>();
                hasAxe = axe != null && axe.HasAxe;
                dressed = upgrades != null && upgrades.Dressed;
            }
            GroundItem best = null;
            float bestSqr = GearPickupRadius * GearPickupRadius;
            foreach (GroundItem item in _all)
            {
                if (item == null) continue;
                if (item.Kind != GroundItemKind.Axe && item.Kind != GroundItemKind.Outfit) continue;
                if ((item.Kind == GroundItemKind.Axe && hasAxe) || (item.Kind == GroundItemKind.Outfit && dressed)) continue;
                if (Time.time < item._readyAt) continue;
                Vector3 offset = item.transform.position - position;
                if (Mathf.Abs(offset.y) > MaxHeightGap) continue;
                offset.y = 0f;
                if (offset.sqrMagnitude > bestSqr) continue;
                bestSqr = offset.sqrMagnitude;
                best = item;
            }
            return best;
        }

        // Server only. Interact on the shore kit (or gear left where someone fell).
        public bool ServerTryTake(PlayerWood player)
        {
            if (!IsServerInitialized || player == null || Time.time < _readyAt) return false;
            if (Kind != GroundItemKind.Axe && Kind != GroundItemKind.Outfit) return false;
            Vector3 offset = player.transform.position - transform.position;
            if (Mathf.Abs(offset.y) > MaxHeightGap) return false;
            offset.y = 0f;
            if (offset.sqrMagnitude > GearPickupRadius * GearPickupRadius) return false;
            int taken = ServerGive(player);
            if (taken <= 0) return false;
            _amount.Value -= taken;
            if (_amount.Value <= 0) ServerManager.Despawn(gameObject);
            return true;
        }

        // Hands as much as the player has room for; returns how much was taken.
        private int ServerGive(PlayerWood player)
        {
            int amount = _amount.Value;
            switch (Kind)
            {
                case GroundItemKind.Wood:
                {
                    int room = Mathf.Max(0, player.Cap - player.Carried);
                    int moved = Mathf.Min(room, amount);
                    if (moved > 0) player.ServerAddWood(moved);
                    return moved;
                }
                case GroundItemKind.Axe:
                {
                    // Only into empty hands; the axe comes back as worn as it was.
                    PlayerAxe axe = player.GetComponent<PlayerAxe>();
                    if (axe == null || axe.HasAxe) return 0;
                    axe.ServerPickUp(amount);
                    return amount;
                }
                case GroundItemKind.Outfit:
                {
                    PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
                    if (upgrades == null || upgrades.Dressed) return 0;
                    upgrades.ServerSetDressed(true);
                    return amount;
                }
                case GroundItemKind.Herb:
                {
                    PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                    if (inventory == null || _config == null) return 0;
                    int moved = Mathf.Min(Mathf.Max(0, _config.HerbCarryCap - inventory.Herbs), amount);
                    if (moved > 0) inventory.ServerAddHerbs(moved);
                    return moved;
                }
                default:
                {
                    PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                    if (inventory == null || _config == null) return 0;
                    int moved = Mathf.Min(Mathf.Max(0, _config.FabricCarryCap - inventory.Fabric), amount);
                    if (moved > 0) inventory.ServerAddFabric(moved);
                    return moved;
                }
            }
        }

        // ---------- Client visuals ----------

        public override void OnStartClient()
        {
            base.OnStartClient();
            BuildView();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_view != null) Destroy(_view.gameObject);
            if (_mesh != null) Destroy(_mesh);
        }

        private void BuildView()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || _view != null) return;
            GameObject view;
            if (Kind == GroundItemKind.Wood)
            {
                view = new GameObject("Branch");
                _mesh = BranchManager.BuildBranchMesh(ObjectId * 7919 + 13);
                view.AddComponent<MeshFilter>().sharedMesh = _mesh;
                view.AddComponent<MeshRenderer>().sharedMaterial = _woodMaterial;
                view.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            else if (Kind == GroundItemKind.Axe || Kind == GroundItemKind.Outfit)
            {
                view = BuildGearView(Kind);
                if (view == null) return;
            }
            else
            {
                GameObject model = Kind == GroundItemKind.Herb ? _herbModel : _fiberModel;
                if (model == null) return;
                view = Instantiate(model);
                foreach (Collider collider in view.GetComponentsInChildren<Collider>()) Destroy(collider);
                float height = 0f;
                foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>()) height = Mathf.Max(height, renderer.bounds.size.y);
                view.transform.localScale = model.transform.localScale * (SprigHeight / Mathf.Max(0.05f, height));
                // A small sprig of the plant, standing on the ground (lying on its side it read as a dark smudge).
                view.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), 0f);
            }
            view.transform.SetParent(transform, false);
            _view = view.transform;
            _shownAt = Time.time;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Block models from primitives and the character's gear material (in every build, takes a tint): the axe lying
        // flat (haft and head), or the outfit as a folded bundle (plaid shirt, vest on top, belt across).
        private static GameObject BuildGearView(GroundItemKind kind)
        {
            CharacterCatalog catalog = Resources.Load<CharacterCatalog>("CharacterCatalog");
            Material material = catalog != null ? catalog.GearMaterial : null;
            if (material == null) return null;
            var root = new GameObject(kind == GroundItemKind.Axe ? "AxeOnGround" : "OutfitOnGround");
            void Block(Vector3 position, Vector3 size, Color tint)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = position;
                go.transform.localScale = size;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, tint);
                renderer.SetPropertyBlock(block);
            }
            if (kind == GroundItemKind.Axe)
            {
                Block(new Vector3(0f, 0.04f, 0f), new Vector3(0.07f, 0.07f, 0.85f), new Color(0.55f, 0.34f, 0.17f));
                Block(new Vector3(0.09f, 0.05f, 0.36f), new Vector3(0.22f, 0.05f, 0.16f), new Color(0.32f, 0.33f, 0.36f));
                Block(new Vector3(0.21f, 0.05f, 0.36f), new Vector3(0.04f, 0.055f, 0.2f), new Color(0.72f, 0.74f, 0.77f));
            }
            else
            {
                Block(new Vector3(0f, 0.06f, 0f), new Vector3(0.5f, 0.12f, 0.36f), new Color(0.7f, 0.12f, 0.1f));
                Block(new Vector3(0f, 0.15f, 0f), new Vector3(0.44f, 0.07f, 0.3f), new Color(0.52f, 0.33f, 0.18f));
                Block(new Vector3(0f, 0.2f, 0f), new Vector3(0.48f, 0.03f, 0.07f), new Color(0.33f, 0.2f, 0.11f));
            }
            root.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            return root;
        }

        // The little hop from where it was knocked loose to where it lies.
        private void AnimateView()
        {
            float t = Mathf.Clamp01((Time.time - _shownAt) / PopSeconds);
            Vector3 from = _from.Value == Vector3.zero ? transform.position : _from.Value;
            Vector3 position = Vector3.Lerp(from, transform.position, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * PopHeight);
            _view.position = position + Vector3.up * 0.04f;
        }
    }
}
