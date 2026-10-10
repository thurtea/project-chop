using System.Collections.Generic;
using Chopper.Core;
using Chopper.Player;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Chopper.World
{
    // Wild plants: healing herbs (small red flowers) and ferns (they give fiber; the code still calls them fabric bushes, and saves and the config keep that name). A player picks one by
    // holding interact next to it; bandages are made from herbs and fabric at the workbench. Positions are generated
    // the same way on every peer (fixed seed over the terrain), so only picked plant numbers are networked: the server
    // marks one picked and tells everyone, late joiners get the picked list in one message, and dawn makes them all
    // grow back. Herbs come first in the list, then the fabric bushes.
    public class HerbManager : NetworkBehaviour
    {
        private const int Seed = 4711;
        private const int FabricBushSeed = 90210;
        private const float MinRadius = 30f;
        private const float MaxRadius = 238f;
        private const float PickReach = 2.4f;
        // Herbs are scaled to this height whatever the model's own size.
        private const float ViewHeight = 0.7f;
        // Fabric bushes stand a little taller, above the grass.
        private const float FabricBushHeight = 1f;
        private const float MaxSlopeDegrees = 30f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // Healing herbs are the pack's bush tinted red (also used for the dropped herb and its icon).
        public static readonly Color HerbTint = new Color(1.7f, 0.42f, 0.36f);

        [SerializeField] private GameConfig _config;
        [SerializeField] private GameObject _model;
        [SerializeField] private GameObject _fabricBushModel;

        private Vector3[] _positions;
        private int _herbCount;
        private GameObject[] _views;
        private bool[] _picked;
        private Transform _root;

        public static HerbManager Instance { get; private set; }

        public int Count => _positions != null ? _positions.Length : 0;

        public Vector3 PositionOf(int id) => _positions[id];

        // Fabric bushes follow the herbs in the list.
        public bool IsFabricBush(int id) => id >= _herbCount;

        public int FirstFabricBush => _herbCount < Count ? _herbCount : -1;

        private void Awake()
        {
            Instance = this;
            _positions = new Vector3[0];
            _picked = new bool[0];
            _views = new GameObject[0];
            // Placed on the generated terrain, so wait for the world to be built.
            Chopper.World.Generation.WorldSession.OnReady += OnWorldReady;
        }

        private void OnWorldReady()
        {
            BuildPositions();
            BuildViews();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Chopper.World.Generation.WorldSession.OnReady -= OnWorldReady;
        }

        private void BuildPositions()
        {
            var list = new List<Vector3>();
            // Each world grows its plants in its own places.
            AddPositions(list, Seed, _config != null ? _config.HerbCount : 240);
            _herbCount = list.Count;
            AddPositions(list, FabricBushSeed, _config != null ? _config.FabricBushCount : 160);
            _positions = list.ToArray();
            _picked = new bool[_positions.Length];
        }

        private static void AddPositions(List<Vector3> list, int seed, int count)
        {
            var rng = new System.Random(seed ^ Chopper.World.Generation.WorldSession.Seed);
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                int target = list.Count + count;
                for (int tries = 0; tries < count * 40 && list.Count < target; tries++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float radius = Mathf.Lerp(MinRadius, MaxRadius, Mathf.Sqrt((float)rng.NextDouble()));
                    var p = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                    if (WaterSystem.DepthAt(p, out _) > 0.05f) continue;
                    // Not on the mountains between the zones: their tops cannot be reached.
                    if (Chopper.World.Generation.WorldSession.Gen != null && Chopper.World.Generation.WorldSession.Gen.MountainWeight(p.x, p.z) > 0.2f) continue;

                    float dx = terrain.SampleHeight(p + Vector3.right * 2f) - terrain.SampleHeight(p - Vector3.right * 2f);
                    float dz = terrain.SampleHeight(p + Vector3.forward * 2f) - terrain.SampleHeight(p - Vector3.forward * 2f);
                    if (Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) / 4f) * Mathf.Rad2Deg > MaxSlopeDegrees) continue;
                    list.Add(p);
                }
            }
        }

        private void BuildViews()
        {
            _root = new GameObject("Herbs").transform;
            _root.SetParent(transform, false);
            _views = new GameObject[_positions.Length];

            var herbBlock = new MaterialPropertyBlock();
            herbBlock.SetColor(BaseColorId, HerbTint);
            float herbScale = 0f;
            float bushScale = 0f;
            for (int i = 0; i < _positions.Length; i++)
            {
                bool bush = IsFabricBush(i);
                GameObject model = bush ? _fabricBushModel : _model;
                if (model == null) continue;
                GameObject view = Instantiate(model, _positions[i], Quaternion.Euler(0f, (i * 53) % 360, 0f), _root);
                view.name = (bush ? "Fern_" : "Herb_") + i;
                // The first plant of each kind is measured at its natural size; the pack models are not all the same size.
                if (bush && bushScale <= 0f) bushScale = FabricBushHeight / MeasuredHeight(view);
                if (!bush && herbScale <= 0f) herbScale = ViewHeight / MeasuredHeight(view);
                view.transform.localScale = Vector3.one * (bush ? bushScale : herbScale);
                // Fabric bushes keep the pack's own leafy look; herbs are tinted red.
                if (!bush) foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(herbBlock);
                foreach (Collider collider in view.GetComponentsInChildren<Collider>()) Destroy(collider);
                _views[i] = view;
            }
        }

        private static float MeasuredHeight(GameObject view)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return Mathf.Max(0.1f, bounds.size.y);
        }

        // ---------- Client ----------

        // True when an unpicked herb or fabric bush is within reach (for the "pick" prompt).
        public bool HerbInReach(Vector3 position) => FindNearest(position) >= 0;

        // True when the plant in reach is a fabric bush rather than a healing herb (for the prompt).
        public bool FabricInReach(Vector3 position) => FindNearest(position) >= _herbCount;

        private int FindNearest(Vector3 position)
        {
            int best = -1;
            float bestSqr = PickReach * PickReach;
            for (int i = 0; i < _positions.Length; i++)
            {
                if (_picked[i]) continue;
                Vector3 offset = _positions[i] - position;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        // ---------- Server ----------

        public override void OnStartServer()
        {
            base.OnStartServer();
            DayNightCycle.ServerPhaseChanged += OnPhaseChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            DayNightCycle.ServerPhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(DayPhase phase)
        {
            if (phase != DayPhase.Dawn || !IsServerInitialized) return;
            ServerRegrowAll();
        }

        // Server only. Picks the nearest plant in reach: it drops a herb or a frond of fiber on the ground, taken by
        // walking over it (even with a full pack, the plant can be picked; the item just waits). False when none is near.
        public bool ServerTryPick(Vector3 position, PlayerInventory inventory)
        {
            int id = FindNearest(position);
            if (id < 0) return false;
            bool bush = IsFabricBush(id);
            _picked[id] = true;
            if (Chopper.Enemies.EnemyDirector.Instance != null)
            {
                Chopper.Enemies.EnemyDirector.Instance.ServerDropItem(bush ? GroundItemKind.Fiber : GroundItemKind.Herb, 1, _positions[id], position);
            }
            PickedObserversRpc(id);
            return true;
        }

        public void ServerRegrowAll()
        {
            bool any = false;
            for (int i = 0; i < _picked.Length; i++)
            {
                if (!_picked[i]) continue;
                _picked[i] = false;
                any = true;
            }
            if (any) RegrowObserversRpc();
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            var ids = new List<int>();
            for (int i = 0; i < _picked.Length; i++) if (_picked[i]) ids.Add(i);
            if (ids.Count > 0) SyncPickedTargetRpc(connection, ids.ToArray());
        }

        [ObserversRpc]
        private void PickedObserversRpc(int id)
        {
            ApplyPicked(id, true);
        }

        [ObserversRpc]
        private void RegrowObserversRpc()
        {
            for (int i = 0; i < _picked.Length; i++)
            {
                _picked[i] = false;
                if (_views[i] != null) _views[i].SetActive(true);
            }
        }

        [TargetRpc]
        private void SyncPickedTargetRpc(NetworkConnection connection, int[] ids)
        {
            foreach (int id in ids) ApplyPicked(id, false);
        }

        private void ApplyPicked(int id, bool effects)
        {
            if (id < 0 || id >= _picked.Length) return;
            _picked[id] = true;
            if (_views[id] != null) _views[id].SetActive(false);
            Color burst = IsFabricBush(id) ? new Color(0.45f, 0.75f, 0.35f, 0.9f) : new Color(1.2f, 0.35f, 0.3f, 0.9f);
            if (effects) CombatFx.Burst(_positions[id] + Vector3.up * 0.3f, burst, 8, 1.4f, 0.1f, 0.7f, 0.3f);
        }
    }
}
