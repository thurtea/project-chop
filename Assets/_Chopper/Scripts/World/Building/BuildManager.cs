using System.Collections.Generic;
using Chopper.Audio;
using Chopper.Core;
using Chopper.Player;
using Chopper.World.Generation;
using Chopper.World.Saving;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.World.Building
{
    // One placed piece, as the server keeps and sends it.
    public struct PieceState
    {
        public int Id;
        public int Kind;
        public Vector3 Position;
        public float Yaw;
        public int Health;
    }

    public enum PlaceResult { Ok, BadKind, TooFar, NoWorkbench, CannotAfford, Overlaps, Floating, TooSteep, Full, Unstable }

    // Every building piece in the world, server owned (one entry per piece in a SyncList, so a piece costs a few bytes
    // and no network object). Every peer, the server included, keeps a copy of each piece's model in the scene from
    // that list, so players walk on floors, enemies path round walls and the hammer can aim at them. The server checks
    // every placement (range, a workbench nearby, cost, overlap and slope), takes the cost, and gives it back
    // when a piece is taken down (by how whole it is). Enemies break pieces (ServerDamage); a broken piece is gone.
    // No support rules (Minecraft style, 2026-10-08): pieces may float, and taking one down never brings others with it.
    public class BuildManager : NetworkBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private PieceCatalog _catalog;

        private readonly SyncList<PieceState> _pieces = new SyncList<PieceState>();
        private readonly Dictionary<int, PieceView> _views = new Dictionary<int, PieceView>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private Transform _root;
        private bool _dirty = true;
        private int _nextId = 1;

        public static BuildManager Instance { get; private set; }

        public PieceCatalog Catalog => _catalog;
        public int Count => _pieces.Count;
        public IReadOnlyList<PieceState> Pieces => _pieces;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_root != null) Destroy(_root.gameObject);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _pieces.OnChange += OnPiecesChanged;
            _dirty = true;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _pieces.OnChange -= OnPiecesChanged;
        }

        private void OnPiecesChanged(SyncListOperation op, int index, PieceState previous, PieceState next, bool asServer)
        {
            _dirty = true;
            // A piece broken by an enemy or taken down: a puff of splinters where it stood (once per machine).
            if (op == SyncListOperation.RemoveAt && asServer == IsServerInitialized && previous.Id != 0)
                CombatFx.Burst(previous.Position + Vector3.up, new Color(0.55f, 0.4f, 0.24f, 0.9f), 16, 2.2f, 0.18f, 0.9f, 0.4f);
        }

        // Every peer: the scene copies follow the list.
        private void LateUpdate()
        {
            if (!_dirty || !WorldSession.Ready) return;
            _dirty = false;
            Reconcile();
        }

        private void Reconcile()
        {
            if (_root == null) _root = new GameObject("BuildingPieces").transform;
            _seen.Clear();
            for (int i = 0; i < _pieces.Count; i++)
            {
                PieceState state = _pieces[i];
                _seen.Add(state.Id);
                if (!_views.TryGetValue(state.Id, out PieceView view) || view == null)
                {
                    view = Spawn(state);
                    if (view == null) continue;
                    _views[state.Id] = view;
                }
                PieceDefinition definition = _catalog != null ? _catalog.Get(state.Kind) : null;
                if (definition != null) view.ShowHealth(state.Health / (float)Mathf.Max(1, definition.Health));
            }
            _gone.Clear();
            foreach (KeyValuePair<int, PieceView> pair in _views) if (!_seen.Contains(pair.Key)) _gone.Add(pair.Key);
            foreach (int id in _gone)
            {
                if (_views[id] != null) Destroy(_views[id].gameObject);
                _views.Remove(id);
            }
        }

        private PieceView Spawn(PieceState state)
        {
            PieceDefinition definition = _catalog != null ? _catalog.Get(state.Kind) : null;
            if (definition == null || definition.Prefab == null) return null;
            GameObject copy = Instantiate(definition.Prefab, state.Position, Quaternion.Euler(0f, state.Yaw, 0f), _root);
            copy.name = definition.DisplayName;
            PieceView view = copy.GetComponent<PieceView>();
            if (view == null) view = copy.AddComponent<PieceView>();
            view.Bind(state.Id, state.Kind);
            return view;
        }

        public PieceView ViewOf(int id) => _views.TryGetValue(id, out PieceView view) ? view : null;

        public int IndexOf(int id)
        {
            for (int i = 0; i < _pieces.Count; i++) if (_pieces[i].Id == id) return i;
            return -1;
        }

        // ---------- Checking a placement (the ghost on the client, the request on the server) ----------

        public PlaceResult Check(GameObject player, int kind, Vector3 position, float yaw)
        {
            PieceDefinition definition = _catalog != null ? _catalog.Get(kind) : null;
            if (definition == null || definition.Prefab == null) return PlaceResult.BadKind;
            if (_pieces.Count >= _config.MaxPieces) return PlaceResult.Full;
            // Not underground (the crypts) and within reach.
            if (position.y < -50f || player == null || !BuildRules.InRange(player.transform.position, position, _config.BuildRange)) return PlaceResult.TooFar;
            if (!definition.IsWorkbench && !Workbench.AnyWithin(position, _config.BuildWorkbenchRadius) && !BuiltWorkbenchWithin(position))
                return PlaceResult.NoWorkbench;
            if (PlayerItems.Get(player, (int)ItemKind.Wood) < definition.WoodCost || PlayerItems.Get(player, (int)ItemKind.Fiber) < definition.FiberCost)
                return PlaceResult.CannotAfford;

            Bounds box = BuildRules.BoundsOf(position, definition.Size, yaw);
            bool touchesPiece = false;
            for (int i = 0; i < _pieces.Count; i++)
            {
                PieceDefinition other = _catalog.Get(_pieces[i].Kind);
                if (other == null) continue;
                Bounds otherBox = BuildRules.BoundsOf(_pieces[i].Position, other.Size, _pieces[i].Yaw);
                if (BuildRules.Overlaps(box, otherBox)) return PlaceResult.Overlaps;
                if (BuildRules.Touches(box, otherBox)) touchesPiece = true;
            }
            if (BlockedByWorld(box)) return PlaceResult.Overlaps;

            float ground = GroundAt(position, out Vector3 normal);
            bool onGround = Mathf.Abs(position.y - ground) <= BuildRules.GroundTolerance;
            // Minecraft, not Valheim: a piece may hang in the air, so there is no support check.
            if (onGround && !touchesPiece && BuildRules.TooSteep(normal.y)) return PlaceResult.TooSteep;
            return PlaceResult.Ok;
        }

        // Pieces within this of the aim point offer their snap points; at most this many joins are tried.
        private const float SnapSearch = 6f;
        private const int MaxSnapTries = 12;
        private readonly List<Vector3> _snapTargets = new List<Vector3>();

        // Valheim's snapping: where a piece aimed at `aim` goes when it joins a nearby placed piece corner to corner (the
        // nearest join that does not overlap anything), or gridPosition when no placed piece is near enough.
        public Vector3 SnapToPieces(GameObject player, int kind, Vector3 aim, float yaw, Vector3 gridPosition)
        {
            PieceDefinition definition = _catalog != null ? _catalog.Get(kind) : null;
            if (definition == null || definition.SnapPoints == null || definition.SnapPoints.Length == 0) return gridPosition;
            _snapTargets.Clear();
            for (int i = 0; i < _pieces.Count; i++)
            {
                if ((_pieces[i].Position - aim).sqrMagnitude > SnapSearch * SnapSearch) continue;
                PieceDefinition other = _catalog.Get(_pieces[i].Kind);
                if (other == null || other.SnapPoints == null) continue;
                foreach (Vector3 local in other.SnapPoints) _snapTargets.Add(BuildRules.WorldPoint(_pieces[i].Position, BuildRules.SnapYaw(_pieces[i].Yaw), local));
            }
            if (_snapTargets.Count == 0) return gridPosition;
            List<Vector3> moves = BuildRules.SnapMoves(aim, BuildRules.SnapYaw(yaw), definition.SnapPoints, _snapTargets);
            int tried = 0;
            foreach (Vector3 move in moves)
            {
                if (tried++ >= MaxSnapTries) break;
                Vector3 snapped = aim + move;
                if (Check(player, kind, snapped, yaw) == PlaceResult.Overlaps) continue;
                return snapped;
            }
            return gridPosition;
        }

        // A workbench piece counts from the moment it is in the list (its scene copy appears a frame later).
        private bool BuiltWorkbenchWithin(Vector3 position)
        {
            float radius = _config.BuildWorkbenchRadius;
            for (int i = 0; i < _pieces.Count; i++)
            {
                PieceDefinition definition = _catalog.Get(_pieces[i].Kind);
                if (definition == null || !definition.IsWorkbench) continue;
                Vector3 offset = _pieces[i].Position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        private static readonly Collider[] Hits = new Collider[32];

        // Anything solid in the way: the house, rocks, trunks, the campfires. The ground, players, enemies, pieces (the
        // rules above handle those) and triggers do not count.
        private static bool BlockedByWorld(Bounds box)
        {
            Vector3 half = box.extents - Vector3.one * 0.05f;
            if (half.x <= 0f || half.y <= 0f || half.z <= 0f) return false;
            int count = Physics.OverlapBoxNonAlloc(box.center, half, Hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i];
                if (hit == null || hit is TerrainCollider || hit is CharacterController) continue;
                if (hit.GetComponentInParent<PieceView>() != null) continue;
                if (hit.GetComponentInParent<Chopper.Enemies.Treant>() != null) continue;
                if (hit.GetComponentInParent<WaterSurface>() != null) continue;
                return true;
            }
            return false;
        }

        public static float GroundAt(Vector3 position, out Vector3 normal)
        {
            normal = Vector3.up;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return position.y;
            Vector3 local = position - terrain.transform.position;
            TerrainData data = terrain.terrainData;
            if (data != null && data.size.x > 0f && data.size.z > 0f)
                normal = data.GetInterpolatedNormal(local.x / data.size.x, local.z / data.size.z);
            return terrain.SampleHeight(position) + terrain.transform.position.y;
        }

        // ---------- Server ----------

        // Server only. Places a piece for a player if the rules allow it, paying its cost from the bag.
        public PlaceResult ServerPlace(GameObject player, int kind, Vector3 position, float yaw)
        {
            yaw = BuildRules.SnapYaw(yaw);
            PlaceResult result = Check(player, kind, position, yaw);
            if (result != PlaceResult.Ok) return result;
            PieceDefinition definition = _catalog.Get(kind);
            PlayerItems.ServerChange(player, (int)ItemKind.Wood, -definition.WoodCost);
            PlayerItems.ServerChange(player, (int)ItemKind.Fiber, -definition.FiberCost);
            ServerAdd(kind, position, yaw, definition.Health);
            PlacedObserversRpc(position);
            return PlaceResult.Ok;
        }

        private void ServerAdd(int kind, Vector3 position, float yaw, int health)
        {
            _pieces.Add(new PieceState { Id = _nextId++, Kind = kind, Position = position, Yaw = BuildRules.SnapYaw(yaw), Health = health });
        }

        [ObserversRpc]
        private void PlacedObserversRpc(Vector3 position)
        {
            Sfx.Play(ProceduralSfx.Deposit, position + Vector3.up * 0.5f, 0.8f, 1.2f, 0.08f);
        }

        // Server only. Takes a piece down and gives its whole cost back to the player who did it.
        public bool ServerRemove(GameObject player, int id)
        {
            int index = IndexOf(id);
            if (index < 0 || player == null) return false;
            PieceState state = _pieces[index];
            if (!BuildRules.InRange(player.transform.position, state.Position, _config.BuildRange)) return false;
            PieceDefinition definition = _catalog.Get(state.Kind);
            _pieces.RemoveAt(index);
            if (definition != null)
            {
                PlayerItems.ServerChange(player, (int)ItemKind.Wood, BuildRules.Refund(definition.WoodCost, state.Health, definition.Health));
                PlayerItems.ServerChange(player, (int)ItemKind.Fiber, BuildRules.Refund(definition.FiberCost, state.Health, definition.Health));
            }
            return true;
        }

        // Server only. Mends every damaged piece near a workbench the player stands at, paying with carried wood.
        // Returns the health restored.
        public int ServerRepairNear(GameObject player)
        {
            if (player == null || !Workbench.AnyInReach(player.transform.position)) return 0;
            int restored = 0;
            for (int i = 0; i < _pieces.Count; i++)
            {
                PieceState state = _pieces[i];
                PieceDefinition definition = _catalog.Get(state.Kind);
                if (definition == null || state.Health >= definition.Health) continue;
                if (!Workbench.AnyWithin(state.Position, _config.BuildWorkbenchRadius)) continue;
                int cost = BuildRules.RepairCost(state.Health, definition.Health, definition.WoodCost);
                if (PlayerItems.Get(player, (int)ItemKind.Wood) < cost) break;
                PlayerItems.ServerChange(player, (int)ItemKind.Wood, -cost);
                restored += definition.Health - state.Health;
                state.Health = definition.Health;
                _pieces[i] = state;
            }
            return restored;
        }

        // Server only. An enemy's blow; at zero the piece breaks and is gone.
        public void ServerDamage(int id, int amount)
        {
            int index = IndexOf(id);
            if (index < 0 || amount <= 0) return;
            PieceState state = _pieces[index];
            state.Health -= amount;
            if (state.Health <= 0)
            {
                _pieces.RemoveAt(index);
            }
            else _pieces[index] = state;
            HitObserversRpc(state.Position);
        }

        [ObserversRpc]
        private void HitObserversRpc(Vector3 position)
        {
            Sfx.Play(ProceduralSfx.HutHit, position + Vector3.up, 0.8f, 1.1f, 0.1f);
        }

        // Server only. The piece nearest to a position within reach (flat distance to its box), for enemies.
        public bool ServerNearest(Vector3 position, float reach, out int id)
        {
            id = 0;
            float best = reach;
            for (int i = 0; i < _pieces.Count; i++)
            {
                PieceDefinition definition = _catalog.Get(_pieces[i].Kind);
                if (definition == null) continue;
                Bounds box = BuildRules.BoundsOf(_pieces[i].Position, definition.Size, _pieces[i].Yaw);
                if (position.y < box.min.y - 2f || position.y > box.max.y + 1f) continue;
                Vector3 flat = new Vector3(position.x, box.center.y, position.z);
                float distance = Vector3.Distance(flat, box.ClosestPoint(flat));
                if (distance < best)
                {
                    best = distance;
                    id = _pieces[i].Id;
                }
            }
            return id != 0;
        }

        public List<PieceRecord> ServerCapture()
        {
            var records = new List<PieceRecord>(_pieces.Count);
            foreach (PieceState state in _pieces)
                records.Add(new PieceRecord { Kind = state.Kind, Position = state.Position, Yaw = state.Yaw, Health = state.Health });
            return records;
        }

        // Server only. Loading a saved world: the saved pieces replace whatever stands.
        public void ServerLoad(List<PieceRecord> records)
        {
            _pieces.Clear();
            if (records == null) return;
            foreach (PieceRecord record in records)
            {
                PieceDefinition definition = _catalog != null ? _catalog.Get(record.Kind) : null;
                if (definition == null) continue;
                ServerAdd(record.Kind, record.Position, record.Yaw, Mathf.Clamp(record.Health, 1, definition.Health));
            }
        }

        // ---------- Requests from players ----------

        public void RequestPlace(int kind, Vector3 position, float yaw) => PlaceServerRpc(kind, position, yaw);

        public void RequestRemove(int id) => RemoveServerRpc(id);

        [ServerRpc(RequireOwnership = false)]
        private void PlaceServerRpc(int kind, Vector3 position, float yaw, FishNet.Connection.NetworkConnection sender = null)
        {
            NetworkObject player = sender != null ? sender.FirstObject : null;
            if (player != null) ServerPlace(player.gameObject, kind, position, yaw);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RemoveServerRpc(int id, FishNet.Connection.NetworkConnection sender = null)
        {
            NetworkObject player = sender != null ? sender.FirstObject : null;
            if (player != null) ServerRemove(player.gameObject, id);
        }
    }
}
