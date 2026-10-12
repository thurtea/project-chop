using System.Collections.Generic;
using Chopper.Audio;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using FishNet.Connection;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEngine;

namespace Chopper.Trees
{
    // Owns every standing tree (plan D4, D14, D18). Trees are plain local GameObjects built from the baked
    // TreeDatabase on every peer; they are not networked. The server counts hits by tree id in TreeHitTracker.
    // A felled tree is sent to everyone (it changes the world); a hit only to the players near it. Late joiners get
    // the felled ids in one TargetRpc, and every peer keeps its own felled record (_clientFelled).
    // The server finds targets by looping the database, which is fine for a few hundred trees; T12 replaces
    // the loop with a spatial grid when the tree count grows.
    public class TreeManager : NetworkBehaviour
    {
        // Tree colliders only need to stop the player, so a fixed trunk height is enough.
        private const float ColliderHeight = 4f;
        private const float StumpHeight = 0.5f;
        // Trees farther than this from the swing origin are skipped before the exact test.
        private const float BroadPhaseMargin = 3f;

        public static TreeManager Instance { get; private set; }

        [SerializeField] private GameConfig _config;
        [SerializeField] private Material _stumpMaterial;
        [SerializeField] private GameObject _stumpPrefab;
        [SerializeField] private Material _particleMaterial;
        [Tooltip("Baked on the server at start, after the tree colliders exist, so enemies path around trees.")]
        [SerializeField] private NavMeshSurface _navSurface;

        private TreeHitTracker _tracker;
        // Every peer's own record of which trees are down, filled from the felled, load and late join RPCs. The
        // server's _tracker is the truth; on a joined client it stays empty, so client side checks read this instead.
        private readonly HashSet<int> _clientFelled = new HashSet<int>();
        // Server: the random turn on each falling tree (TreeFallRules).
        private readonly System.Random _fallRandom = new System.Random();
        private bool _firstLargeTreeFelled;
        private TreeView[] _views;
        private GameObject[] _stumps;
        private Transform _root;
        private ParticleSystem _particles;

        // The trees of this world, generated from its seed by WorldBuilder (the same list on every peer).
        public TreeDatabase Database => _database;
        private TreeDatabase _database;
        public GameConfig Config => _config;

        // Client side view state, used by the development smoke test.
        public int StumpCount
        {
            get
            {
                int count = 0;
                foreach (GameObject stump in _stumps) if (stump != null) count++;
                return count;
            }
        }

        public bool IsFelledOnClient(int treeId) => _clientFelled.Contains(treeId);
        public bool FirstLargeTreeFelled => _firstLargeTreeFelled;

        private void Awake()
        {
            Instance = this;
            _tracker = new TreeHitTracker(_config);
            BuildParticles();
            Chopper.World.Generation.WorldSession.OnReady += OnWorldReady;
        }

        private void OnWorldReady()
        {
            _database = Chopper.World.Generation.WorldSession.Trees;
            BuildViews();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Chopper.World.Generation.WorldSession.OnReady -= OnWorldReady;
        }

        private void BuildViews()
        {
            _root = new GameObject("Trees").transform;
            _root.SetParent(transform, false);
            _views = new TreeView[_database.Count];
            _stumps = new GameObject[_database.Count];

            for (int i = 0; i < _database.Count; i++) CreateView(i);
            // Felled ids that arrived before the world was built become stumps now.
            foreach (int id in _clientFelled) ShowFelled(id, Vector3.zero, animate: false);
        }

        private void CreateView(int i)
        {
            TreeEntry entry = _database.Get(i);
            Quaternion rotation = Quaternion.Euler(0f, entry.Yaw, 0f);
            GameObject go = Instantiate(_database.GetModel(entry.ModelIndex), Planted(entry.Position, entry.Scale), rotation, _root);
            go.name = $"Tree_{i}_{entry.Size}";
            go.transform.localScale = Vector3.one * entry.Scale;
            TreeTint.Apply(go, entry.Zone);

            float localRadius = _config.GetTrunkRadius(entry.Size) / entry.Scale;
            var collider = go.AddComponent<CapsuleCollider>();
            collider.radius = localRadius;
            collider.height = ColliderHeight / entry.Scale;
            collider.center = new Vector3(0f, ColliderHeight * 0.5f / entry.Scale, 0f);

            var view = go.AddComponent<TreeView>();
            view.TreeId = i;
            _views[i] = view;
        }

        private static readonly Color ChipColor = new Color(0.62f, 0.42f, 0.22f);
        private static readonly Color LeafColor = new Color(0.34f, 0.62f, 0.22f);
        private const float ChipHeight = 1.2f;
        private const float FellFeelHeight = 3f;

        private void BuildParticles()
        {
            var go = new GameObject("TreeParticles");
            go.transform.SetParent(transform, false);
            _particles = go.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = _particleMaterial;
        }

        private void EmitBurst(Vector3 position, Color color, int count, float spread)
        {
            if (_particles == null || count <= 0) return;
            var shape = _particles.shape;
            shape.radius = spread;
            var emit = new ParticleSystem.EmitParams { startColor = color, position = position };
            _particles.Emit(emit, count);
        }

        // Camera shake and sound fade with distance so a tree falling across the meadow is felt only faintly.
        private float FeelStrength(Vector3 position)
        {
            PlayerCamera cam = PlayerCamera.Local;
            if (cam == null) return 0f;
            float distance = Vector3.Distance(cam.transform.position, position);
            return Mathf.Clamp01(1f - distance / _config.FeelFalloffDistance);
        }

        private void PlayFeedback(TreeEntry entry, bool felled)
        {
            Vector3 position = entry.Position + Vector3.up * (felled ? FellFeelHeight : ChipHeight);
            float strength = FeelStrength(position);
            if (strength <= 0f) return;

            float shake = felled ? _config.ShakeOnFell : _config.ShakeOnHit;
            PlayerCamera.Local.AddShake(shake * strength);
            Sfx.Play(ProceduralSfx.Chop, position, strength, felled ? 0.75f : 1f, 0.07f);
            if (felled) Sfx.Play(ProceduralSfx.Fell, position, strength, 1f, 0.05f);
        }

        // Server only. Called by PlayerAxe after a swing is accepted.
        public void ServerChop(PlayerWood swinger, Vector3 origin, Vector3 aim)
        {
            if (!ServerFindTree(origin, aim, out int id, out _)) return;
            PlayerUpgrades upgrades = swinger != null ? swinger.GetComponent<PlayerUpgrades>() : null;
            ServerHitTree(id, swinger, aim, true, upgrades != null ? upgrades.AxeDamageMultiplier : 1f);
        }

        // Server only. Picks the nearest tree in the swing strip.
        public bool ServerFindTree(Vector3 origin, Vector3 aim, out int treeId, out float distance)
        {
            return FindTree(origin, aim, _tracker.IsFelled, out treeId, out distance);
        }

        // Any peer. The same pick against this peer's view of which trees are down (the owner's swing prediction).
        public bool ClientFindTree(Vector3 origin, Vector3 aim, out int treeId, out float distance)
        {
            return FindTree(origin, aim, _clientFelled.Contains, out treeId, out distance);
        }

        private bool FindTree(Vector3 origin, Vector3 aim, System.Func<int, bool> isFelled, out int treeId, out float distance)
        {
            treeId = -1;
            distance = float.MaxValue;
            float broadRange = _config.SwingRange + _config.SwingRadius + BroadPhaseMargin;
            float broadSqr = broadRange * broadRange;

            if (_database == null) return false;
            for (int i = 0; i < _database.Count; i++)
            {
                if (isFelled(i)) continue;
                TreeEntry entry = _database.Get(i);
                Vector3 delta = entry.Position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > broadSqr) continue;

                float d = TreeTargeting.HitDistance(origin, aim, _config.SwingRange, _config.SwingRadius,
                    entry.Position, _config.GetTrunkRadius(entry.Size));
                if (d >= 0f && d < distance)
                {
                    treeId = i;
                    distance = d;
                }
            }
            return treeId >= 0;
        }

        // Server only. Takes the swing's chop points off the tree (power 1 is a plain axe hit, more for a better axe),
        // grants wood on the felling hit, and rolls sapling drops.
        public void ServerHitTree(int treeId, PlayerWood swinger, Vector3 aim, bool countsAsHit = true, float power = 1f)
        {
            if (!countsAsHit)
            {
                // A fist punch that has not added up to a whole hit yet: the tree shakes, nothing else happens.
                if (!_tracker.IsFelled(treeId)) ServerShowHit(treeId, aim);
                return;
            }
            TreeEntry entry = _database.Get(treeId);
            TreeHitResult result = _tracker.ApplyHit(treeId, entry.Size, ChopRules.SwingPoints(power));
            if (result == TreeHitResult.Ignored) return;

            if (result == TreeHitResult.Damaged)
            {
                ServerShowHit(treeId, aim);
            }
            else
            {
                if (swinger != null) swinger.ServerAddWood(_config.GetWoodForSize(entry.Size));
                bool teachTreeDanger = entry.Size == TreeSize.Large && swinger != null && !_firstLargeTreeFelled;
                if (teachTreeDanger) _firstLargeTreeFelled = true;
                Vector3 fall = teachTreeDanger
                    ? TreeFallRules.TowardPlayer(entry.Position, swinger.transform.position, -aim)
                    : ServerFallDirection(treeId, entry, aim);
                TreeFelledObserversRpc(treeId, fall);
                // Only the big trees are heavy enough to hurt: anyone where the trunk lands is crushed.
                if (entry.Size == TreeSize.Large) StartCoroutine(ServerCrushWhenLanded(entry, fall));
                if (EnemyDirector.Instance != null)
                {
                    EnemyDirector.Instance.ServerOnTreeHit(entry.Position, entry.Zone, entry.Size, swinger);
                    EnemyDirector.Instance.ServerOnTreeFelled(entry.Position, entry.Zone);
                }
            }
        }

        // Server only. Where a felled tree comes down: away from the blow, pulled down the slope under it (TreeFallRules).
        public Vector3 ServerFallDirection(int treeId, TreeEntry entry, Vector3 aim)
        {
            Vector3 downhill = Vector3.zero;
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            if (gen != null)
            {
                const float step = 2f;
                float x = entry.Position.x;
                float z = entry.Position.z;
                float dx = gen.HeightAt(x + step, z) - gen.HeightAt(x - step, z);
                float dz = gen.HeightAt(x, z + step) - gen.HeightAt(x, z - step);
                downhill = new Vector3(-dx, 0f, -dz) / (2f * step);
            }
            return TreeFallRules.Choose(downhill, aim, _fallRandom);
        }

        // A large tree's trunk: how far along the ground it reaches (most of its height) and how wide the strip it
        // flattens is. Without Sturdy gear it kills (CrushRules), and blocking does not stop it.
        private const float CrushHalfWidth = 1.3f;
        private const float CrushStart = 1.5f;
        private const float CrushReachFraction = 0.9f;

        // Server only. Waits for the falling tree to hit the ground, then crushes every player under it.
        private System.Collections.IEnumerator ServerCrushWhenLanded(TreeEntry entry, Vector3 aim)
        {
            yield return new WaitForSeconds(FallingTree.FallDuration * 0.92f);
            foreach (PlayerHealth health in ServerPlayersUnderFall(entry, aim)) health.ServerCrush();
        }

        // Every player standing in the strip where a felled tree with this aim comes down.
        public List<PlayerHealth> ServerPlayersUnderFall(TreeEntry entry, Vector3 aim)
        {
            var hit = new List<PlayerHealth>();
            Vector3 along = FallingTree.GroundDirection(aim);
            float reach = TreeSizeHeight(entry.Size) * CrushReachFraction;
            foreach (PlayerHealth health in PlayerHealth.All)
            {
                if (health == null || health.IsDown) continue;
                Vector3 offset = health.transform.position - entry.Position;
                if (Mathf.Abs(offset.y) > 4f) continue;
                offset.y = 0f;
                float forward = Vector3.Dot(offset, along);
                float side = Vector3.Cross(along, offset).magnitude;
                if (forward >= CrushStart && forward <= reach && side <= CrushHalfWidth) hit.Add(health);
            }
            return hit;
        }

        private static float TreeSizeHeight(TreeSize size) => Chopper.World.Generation.WorldGen.TreeHeights[(int)size];

        // Server only. Saving: the ids of every tree that is down right now.
        public int[] ServerFelledIds()
        {
            var felled = new List<int>();
            _tracker.CopyFelledTo(felled);
            return felled.ToArray();
        }

        // Server only. Loading a saved world: these trees are already down (stumps, no falling, no wood).
        public void ServerLoadFelled(int[] treeIds, bool firstLargeTreeFelled = false)
        {
            _firstLargeTreeFelled = firstLargeTreeFelled;
            if (treeIds == null || treeIds.Length == 0 || _database == null) return;
            var valid = new List<int>();
            foreach (int id in treeIds)
            {
                if (id < 0 || id >= _database.Count) continue;
                _tracker.MarkFelled(id);
                valid.Add(id);
            }
            LoadFelledObserversRpc(valid.ToArray());
        }

        [ObserversRpc]
        private void LoadFelledObserversRpc(int[] treeIds)
        {
            foreach (int id in treeIds) ClientFell(id, Vector3.zero, animate: false);
        }

        // Server only. Dawn regrowth (plan D6) and restarts: every felled tree grows back.
        public void ServerRegrowAll()
        {
            var felled = new List<int>();
            _tracker.CopyFelledTo(felled);
            if (felled.Count == 0) return;

            foreach (int id in felled) _tracker.Regrow(id);
            TreeRegrowObserversRpc(felled.ToArray());
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_navSurface != null)
            {
                // Baked from every collider, so since W5-08 step 5 from the block chunks (plus crypts, rocks, the house).
                var bake = System.Diagnostics.Stopwatch.StartNew();
                _navSurface.BuildNavMesh();
                Debug.Log($"[NavMesh] baked in {bake.ElapsedMilliseconds} ms");
            }
        }

        [ObserversRpc]
        private void TreeRegrowObserversRpc(int[] treeIds)
        {
            foreach (int id in treeIds)
            {
                _clientFelled.Remove(id);
                if (_views == null || id < 0 || id >= _views.Length || _views[id] != null) continue;
                if (_stumps[id] != null) Destroy(_stumps[id]);
                _stumps[id] = null;
                CreateView(id);
            }
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            var felled = new List<int>();
            _tracker.CopyFelledTo(felled);
            if (felled.Count > 0) SyncFelledTargetRpc(connection, felled.ToArray());
        }

        // A tree shaking is only worth sending to the players who could see it (NearbyClients).
        private void ServerShowHit(int treeId, Vector3 direction)
        {
            Vector3 position = _database.Get(treeId).Position;
            foreach (NetworkConnection connection in Chopper.Networking.NearbyClients.Within(ServerManager, position))
            {
                TreeHitTargetRpc(connection, treeId, direction);
            }
        }

        [TargetRpc]
        private void TreeHitTargetRpc(NetworkConnection connection, int treeId, Vector3 direction)
        {
            if (_views == null || treeId < 0 || treeId >= _views.Length || _views[treeId] == null) return;
            _views[treeId].Shake(direction);
            TreeEntry entry = _database.Get(treeId);
            EmitBurst(entry.Position + Vector3.up * ChipHeight, ChipColor, _config.ChipsPerHit, 0.15f);
            PlayFeedback(entry, felled: false);
        }

        [ObserversRpc]
        private void TreeFelledObserversRpc(int treeId, Vector3 direction)
        {
            ClientFell(treeId, direction, animate: true);
        }

        [TargetRpc]
        private void SyncFelledTargetRpc(NetworkConnection connection, int[] felledIds)
        {
            foreach (int id in felledIds) ClientFell(id, Vector3.zero, animate: false);
        }

        // Every felled tree on this peer comes through here (live, loading, late join), so the client record and the
        // stumps can never disagree.
        private void ClientFell(int treeId, Vector3 direction, bool animate)
        {
            if (treeId < 0) return;
            _clientFelled.Add(treeId);
            ShowFelled(treeId, direction, animate);
        }

        private void ShowFelled(int treeId, Vector3 direction, bool animate)
        {
            if (_views == null || treeId >= _views.Length || _views[treeId] == null) return;

            TreeEntry entry = _database.Get(treeId);
            GameObject view = _views[treeId].gameObject;
            if (animate)
            {
                EmitBurst(entry.Position + Vector3.up * ChipHeight, ChipColor, _config.ChipsPerHit * 2, 0.2f);
                EmitBurst(entry.Position + Vector3.up * (FellFeelHeight + 1f), LeafColor, _config.LeavesOnFell, 1.5f);
                PlayFeedback(entry, felled: true);
                FallingTree.Spawn(_database.GetModel(entry.ModelIndex), Planted(entry.Position, entry.Scale),
                    view.transform.rotation, entry.Scale, direction, entry.Zone, entry.Position);
            }

            Destroy(view);
            _views[treeId] = null;
            _stumps[treeId] = CreateStump(entry);
        }

        // The pine is authored on a 0.25 m dirt pad (build_pine.py, cells at z = 0). Concept 2.0 has the trunk
        // coming out of the grass, so the pad and a short bite of trunk sit below the ground. Stumps stay on top.
        internal const float PineBuryMeters = 0.40f;

        internal static Vector3 Planted(Vector3 ground, float scale) => ground + Vector3.down * (PineBuryMeters * scale);

        // Authored prop_stump_voxel is 0.75 m wide and 0.75 m tall, base on Y = 0.
        private const float StumpModelSize = 0.75f;

        private GameObject CreateStump(TreeEntry entry)
        {
            float diameter = _config.GetTrunkRadius(entry.Size) * 2f;
            if (_stumpPrefab != null)
            {
                GameObject stump = Instantiate(_stumpPrefab, _root);
                stump.name = "Stump";
                float scale = diameter / StumpModelSize;
                stump.transform.localScale = Vector3.one * scale;
                stump.transform.position = entry.Position;
                return stump;
            }

            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Stump";
            cylinder.transform.SetParent(_root, false);
            // A Unity cylinder is 2 m tall at scale 1, so half the desired height is the Y scale.
            cylinder.transform.localScale = new Vector3(diameter, StumpHeight * 0.5f, diameter);
            cylinder.transform.position = entry.Position + Vector3.up * (StumpHeight * 0.5f);
            if (_stumpMaterial != null) cylinder.GetComponent<MeshRenderer>().sharedMaterial = _stumpMaterial;
            return cylinder;
        }
    }
}
