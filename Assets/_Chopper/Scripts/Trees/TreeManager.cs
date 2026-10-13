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
        // Logs, stump chops and cleared spots. The server's copy is the truth; peers only keep enough to show and aim.
        private readonly TreeRemains _remains = new TreeRemains();
        // Every peer's own record of which trees are down, filled from the felled, load and late join RPCs. The
        // server's _tracker is the truth; on a joined client it stays empty, so client side checks read this instead.
        private readonly HashSet<int> _clientFelled = new HashSet<int>();
        private readonly Dictionary<int, float> _clientLogYaw = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _clientLogReady = new Dictionary<int, float>();
        private readonly HashSet<int> _clientCleared = new HashSet<int>();
        // Server: Time.time when a freshly felled log can be chopped. Loaded logs are already down and stay out of here.
        private readonly Dictionary<int, float> _logReadyAt = new Dictionary<int, float>();
        // Server: the random turn on each falling tree (TreeFallRules).
        private readonly System.Random _fallRandom = new System.Random();
        private bool _firstLargeTreeFelled;
        private TreeView[] _views;
        private GameObject[] _stumps;
        private FallingTree[] _fallen;
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
            _fallen = new FallingTree[_database.Count];

            for (int i = 0; i < _database.Count; i++) CreateView(i);
            // Felled ids that arrived before the world was built become stumps (and landed logs) now.
            foreach (int id in _clientFelled) Present(id, Vector3.zero, animate: false);
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
            ServerHitTree(id, swinger, origin, aim, true, upgrades != null ? upgrades.AxeDamageMultiplier : 1f);
        }

        // Server only. Picks the nearest tree in the swing strip.
        public bool ServerFindTree(Vector3 origin, Vector3 aim, out int treeId, out float distance)
        {
            return FindTree(origin, aim, server: true, out treeId, out distance);
        }

        // Any peer. The same pick against this peer's view of which trees are down (the owner's swing prediction).
        public bool ClientFindTree(Vector3 origin, Vector3 aim, out int treeId, out float distance)
        {
            return FindTree(origin, aim, server: false, out treeId, out distance);
        }

        private bool FindTree(Vector3 origin, Vector3 aim, bool server, out int treeId, out float distance)
        {
            treeId = -1;
            distance = float.MaxValue;
            float broadRange = _config.SwingRange + _config.SwingRadius + BroadPhaseMargin;
            float broadSqr = broadRange * broadRange;

            if (_database == null) return false;
            for (int i = 0; i < _database.Count; i++)
            {
                TreeEntry entry = _database.Get(i);
                Vector3 delta = entry.Position - origin;
                delta.y = 0f;
                bool felled = server ? _tracker.IsFelled(i) : _clientFelled.Contains(i);
                if (!felled)
                {
                    if (delta.sqrMagnitude > broadSqr) continue;
                    float d = TreeTargeting.HitDistance(origin, aim, _config.SwingRange, _config.SwingRadius,
                        entry.Position, _config.GetTrunkRadius(entry.Size));
                    if (d >= 0f && d < distance)
                    {
                        treeId = i;
                        distance = d;
                    }
                    continue;
                }

                float yaw = 0f;
                bool cleared = server ? _remains.IsCleared(i) : _clientCleared.Contains(i);
                bool hasLog = server ? _remains.TryGetLogYaw(i, out yaw) : _clientLogYaw.TryGetValue(i, out yaw);
                if (hasLog && LogStillFalling(i, server)) hasLog = false;
                if (cleared && !hasLog) continue;
                float reach = LogLength(entry.Size) + broadRange;
                if (delta.sqrMagnitude > reach * reach) continue;
                float remains = RemainsDistance(entry, origin, aim, !cleared, hasLog, yaw);
                if (remains >= 0f && remains < distance)
                {
                    treeId = i;
                    distance = remains;
                }
            }
            return treeId >= 0;
        }

        // Server only. Older callers have no swing origin; the swinger's feet stand in for it.
        public void ServerHitTree(int treeId, PlayerWood swinger, Vector3 aim, bool countsAsHit = true, float power = 1f)
        {
            Vector3 origin = swinger != null ? swinger.transform.position : new Vector3(float.NaN, 0f, 0f);
            ServerHitTree(treeId, swinger, origin, aim, countsAsHit, power);
        }

        // Server only. Takes the swing's chop points off the tree (power 1 is a plain axe hit, more for a better axe).
        // Felling pays a third of the wood (LogRules.FellWood). The log and the stump take the swings after that.
        public void ServerHitTree(int treeId, PlayerWood swinger, Vector3 origin, Vector3 aim, bool countsAsHit, float power)
        {
            if (_database == null || treeId < 0 || treeId >= _database.Count) return;
            if (_tracker.IsFelled(treeId))
            {
                if (countsAsHit) ServerHitRemains(treeId, swinger, origin, aim, power);
                return;
            }
            if (!countsAsHit)
            {
                // A fist punch that has not added up to a whole hit yet: the tree shakes, nothing else happens.
                ServerShowHit(treeId, aim);
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
                int fellWood = LogRules.FellWood(_config.GetWoodForSize(entry.Size));
                if (swinger != null && fellWood > 0) swinger.ServerAddWood(fellWood);
                bool teachTreeDanger = entry.Size == TreeSize.Large && swinger != null && !_firstLargeTreeFelled;
                if (teachTreeDanger) _firstLargeTreeFelled = true;
                Vector3 fall = teachTreeDanger
                    ? TreeFallRules.TowardPlayer(entry.Position, swinger.transform.position, -aim)
                    : ServerFallDirection(treeId, entry, aim);
                _remains.AddLog(treeId, FlatYaw(fall));
                _logReadyAt[treeId] = Time.time + FallingTree.FallDuration;
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

        // Server only. Saving: each felled id, plus a packed tail when that tree still has a log or a cleared wait
        // (TreeRemains.Pack). WorldSaver stores the array unchanged. A plain id still means "this tree is down".
        public int[] ServerFelledIds()
        {
            var felled = new List<int>();
            _tracker.CopyFelledTo(felled);
            var saved = new List<int>(felled.Count * 2);
            foreach (int id in felled)
            {
                saved.Add(id);
                bool hasLog = _remains.TryGetLogYaw(id, out float yaw);
                bool cleared = _remains.Cleared.TryGetValue(id, out int dawns);
                if (!hasLog && !cleared) continue;
                if (id >= 1 << 14) continue;
                saved.Add(TreeRemains.Pack(id, hasLog, yaw, cleared, cleared ? dawns : 0));
            }
            return saved.ToArray();
        }

        // Server only. Loading a saved world: stumps, and any log or cleared wait packed beside the id.
        public void ServerLoadFelled(int[] treeIds, bool firstLargeTreeFelled = false)
        {
            _firstLargeTreeFelled = firstLargeTreeFelled;
            _remains.Clear();
            _logReadyAt.Clear();
            if (treeIds == null || treeIds.Length == 0 || _database == null) return;
            foreach (int raw in treeIds)
            {
                if (raw >= 0)
                {
                    if (raw < _database.Count) _tracker.MarkFelled(raw);
                    continue;
                }
                if (!TreeRemains.TryUnpack(raw, out int id, out bool hasLog, out float yaw, out bool cleared, out int dawns)) continue;
                if (id < 0 || id >= _database.Count) continue;
                _tracker.MarkFelled(id);
                if (hasLog) _remains.AddLog(id, yaw);
                if (cleared) _remains.MarkCleared(id, dawns);
            }
            BroadcastFelled();
        }

        private void BroadcastFelled()
        {
            var felled = new List<int>();
            _tracker.CopyFelledTo(felled);
            if (felled.Count == 0) return;
            FillRemains(out int[] logIds, out float[] yaws, out int[] cleared);
            LoadFelledObserversRpc(felled.ToArray(), logIds, yaws, cleared);
        }

        private void FillRemains(out int[] logIds, out float[] yaws, out int[] cleared)
        {
            logIds = new int[_remains.Logs.Count];
            yaws = new float[_remains.Logs.Count];
            int n = 0;
            foreach (KeyValuePair<int, TreeRemains.Log> pair in _remains.Logs)
            {
                logIds[n] = pair.Key;
                yaws[n] = pair.Value.Yaw;
                n++;
            }
            cleared = new int[_remains.Cleared.Count];
            n = 0;
            foreach (KeyValuePair<int, int> pair in _remains.Cleared) cleared[n++] = pair.Key;
        }

        [ObserversRpc]
        private void LoadFelledObserversRpc(int[] treeIds, int[] logIds, float[] logYaws, int[] clearedIds)
        {
            ApplySnapshot(treeIds, logIds, logYaws, clearedIds);
        }

        // Server only. Dawn keeps a cleared spot bare (TreeRemains.Dawn). A restart, any other phase, grows every tree.
        public void ServerRegrowAll()
        {
            var felled = new List<int>();
            _tracker.CopyFelledTo(felled);
            if (felled.Count == 0) return;

            var regrow = new List<int>();
            bool dawn = Chopper.World.DayNightCycle.Instance != null
                && Chopper.World.DayNightCycle.Instance.Phase == DayPhase.Dawn;
            if (dawn) _remains.Dawn(felled, regrow);
            else
            {
                regrow.AddRange(felled);
                _remains.Clear();
                _logReadyAt.Clear();
            }
            foreach (int id in regrow) _logReadyAt.Remove(id);
            if (regrow.Count == 0) return;
            foreach (int id in regrow) _tracker.Regrow(id);
            TreeRegrowObserversRpc(regrow.ToArray());
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
                _clientLogYaw.Remove(id);
                _clientLogReady.Remove(id);
                _clientCleared.Remove(id);
                if (_views == null || id < 0 || id >= _views.Length) continue;
                if (_fallen != null && id < _fallen.Length && _fallen[id] != null) Destroy(_fallen[id].gameObject);
                if (_fallen != null && id < _fallen.Length) _fallen[id] = null;
                if (_stumps[id] != null) Destroy(_stumps[id]);
                _stumps[id] = null;
                if (_views[id] == null) CreateView(id);
            }
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            var felled = new List<int>();
            _tracker.CopyFelledTo(felled);
            if (felled.Count == 0) return;
            FillRemains(out int[] logIds, out float[] yaws, out int[] cleared);
            SyncFelledTargetRpc(connection, felled.ToArray(), logIds, yaws, cleared);
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
        private void SyncFelledTargetRpc(NetworkConnection connection, int[] felledIds, int[] logIds, float[] logYaws, int[] clearedIds)
        {
            ApplySnapshot(felledIds, logIds, logYaws, clearedIds);
        }

        private void ApplySnapshot(int[] felledIds, int[] logIds, float[] logYaws, int[] clearedIds)
        {
            _clientLogYaw.Clear();
            _clientLogReady.Clear();
            _clientCleared.Clear();
            if (logIds != null && logYaws != null)
            {
                int count = Mathf.Min(logIds.Length, logYaws.Length);
                for (int i = 0; i < count; i++) _clientLogYaw[logIds[i]] = logYaws[i];
            }
            if (clearedIds != null)
            {
                foreach (int id in clearedIds) _clientCleared.Add(id);
            }
            if (felledIds == null) return;
            foreach (int id in felledIds) ClientFell(id, Vector3.zero, animate: false);
        }

        // Every felled tree on this peer comes through here (live, loading, late join), so the client record and the
        // stumps can never disagree.
        private void ClientFell(int treeId, Vector3 direction, bool animate)
        {
            if (treeId < 0) return;
            _clientFelled.Add(treeId);
            if (direction.sqrMagnitude > 0.0001f)
            {
                _clientLogYaw[treeId] = FlatYaw(direction);
                if (animate) _clientLogReady[treeId] = Time.time + FallingTree.FallDuration;
                else _clientLogReady.Remove(treeId);
            }
            Present(treeId, direction, animate);
        }

        private void Present(int treeId, Vector3 direction, bool animate)
        {
            if (_views == null || treeId < 0 || treeId >= _views.Length) return;

            TreeEntry entry = _database.Get(treeId);
            Quaternion rotation = Quaternion.Euler(0f, entry.Yaw, 0f);
            if (_views[treeId] != null) rotation = _views[treeId].transform.rotation;
            if (animate)
            {
                EmitBurst(entry.Position + Vector3.up * ChipHeight, ChipColor, _config.ChipsPerHit * 2, 0.2f);
                EmitBurst(entry.Position + Vector3.up * (FellFeelHeight + 1f), LeafColor, _config.LeavesOnFell, 1.5f);
                PlayFeedback(entry, felled: true);
            }

            if (_views[treeId] != null)
            {
                Destroy(_views[treeId].gameObject);
                _views[treeId] = null;
            }

            if (_clientCleared.Contains(treeId))
            {
                if (_stumps[treeId] != null) Destroy(_stumps[treeId]);
                _stumps[treeId] = null;
            }
            else if (_stumps[treeId] == null)
            {
                _stumps[treeId] = CreateStump(entry);
            }

            if (!_clientLogYaw.TryGetValue(treeId, out float yaw) || _fallen == null || _fallen[treeId] != null) return;
            Vector3 fall = direction.sqrMagnitude > 0.0001f ? direction : YawDirection(yaw);
            Vector3 stumpTop = entry.Position + Vector3.up * StumpTop(entry);
            _fallen[treeId] = FallingTree.Spawn(_database.GetModel(entry.ModelIndex), Planted(entry.Position, entry.Scale),
                rotation, entry.Scale, fall, entry.Zone, stumpTop,
                at => EmitBurst(at, ChipColor, _config.ChipsPerHit * 3, 1.2f), landed: !animate, groundY: entry.Position.y);
        }

        // Server only. The nearer of the log and the stump takes the swing. Clearing the stump does not eat the log.
        private void ServerHitRemains(int treeId, PlayerWood swinger, Vector3 origin, Vector3 aim, float power)
        {
            TreeEntry entry = _database.Get(treeId);
            if (float.IsNaN(origin.x)) origin = entry.Position;
            bool cleared = _remains.IsCleared(treeId);
            bool hasLog = _remains.TryGetLogYaw(treeId, out float yaw);
            if (hasLog && LogStillFalling(treeId, server: true)) hasLog = false;
            if (cleared && !hasLog) return;

            int points = ChopRules.SwingPoints(power);
            float stumpDistance = -1f;
            if (!cleared)
            {
                stumpDistance = TreeTargeting.HitDistance(origin, aim, _config.SwingRange, _config.SwingRadius,
                    entry.Position, _config.GetTrunkRadius(entry.Size));
            }
            float logDistance = -1f;
            if (hasLog)
            {
                LogEnds(entry, yaw, out Vector3 a, out Vector3 b);
                logDistance = TreeTargeting.SegmentHitDistance(origin, aim, _config.SwingRange, _config.SwingRadius,
                    a, b, _config.GetTrunkRadius(entry.Size));
            }

            bool hitLog = logDistance >= 0f && (stumpDistance < 0f || logDistance <= stumpDistance);
            if (hitLog)
            {
                RemainsHit hit = _remains.HitLog(treeId, entry.Size, points, out int before, out int after);
                if (hit == RemainsHit.Ignored) return;
                int wood = LogRules.WoodBetween(LogRules.LogWood(_config.GetWoodForSize(entry.Size), entry.Size),
                    LogRules.LogPoints(entry.Size), before, after);
                if (swinger != null && wood > 0) swinger.ServerAddWood(wood);
                if (hit == RemainsHit.Finished)
                {
                    _logReadyAt.Remove(treeId);
                    LogFinishedObserversRpc(treeId);
                }
                else ServerShowLogHit(treeId);
                return;
            }
            if (stumpDistance < 0f) return;

            RemainsHit stump = _remains.HitStump(treeId, points);
            if (stump == RemainsHit.Ignored) return;
            if (stump == RemainsHit.Finished)
            {
                if (swinger != null && LogRules.StumpWood > 0) swinger.ServerAddWood(LogRules.StumpWood);
                StumpClearedObserversRpc(treeId);
            }
            else ServerShowStumpHit(treeId);
        }

        private float RemainsDistance(TreeEntry entry, Vector3 origin, Vector3 aim, bool stump, bool hasLog, float yaw)
        {
            float best = -1f;
            float radius = _config.GetTrunkRadius(entry.Size);
            if (stump)
            {
                float d = TreeTargeting.HitDistance(origin, aim, _config.SwingRange, _config.SwingRadius, entry.Position, radius);
                if (d >= 0f) best = d;
            }
            if (hasLog)
            {
                LogEnds(entry, yaw, out Vector3 a, out Vector3 b);
                float d = TreeTargeting.SegmentHitDistance(origin, aim, _config.SwingRange, _config.SwingRadius, a, b, radius);
                if (d >= 0f && (best < 0f || d < best)) best = d;
            }
            return best;
        }

        private void LogEnds(TreeEntry entry, float fallYaw, out Vector3 a, out Vector3 b)
        {
            Vector3 along = FallingTree.GroundDirection(YawDirection(fallYaw));
            a = entry.Position;
            b = entry.Position + along * LogLength(entry.Size);
        }

        private static float LogLength(TreeSize size) => TreeSizeHeight(size) * CrushReachFraction;

        private bool LogStillFalling(int treeId, bool server)
        {
            Dictionary<int, float> ready = server ? _logReadyAt : _clientLogReady;
            return ready.TryGetValue(treeId, out float at) && Time.time < at;
        }

        private static float FlatYaw(Vector3 direction)
        {
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            return Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        private static Vector3 YawDirection(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        private void ServerShowLogHit(int treeId)
        {
            Vector3 position = _database.Get(treeId).Position;
            foreach (NetworkConnection connection in Chopper.Networking.NearbyClients.Within(ServerManager, position))
                LogHitTargetRpc(connection, treeId);
        }

        private void ServerShowStumpHit(int treeId)
        {
            Vector3 position = _database.Get(treeId).Position;
            foreach (NetworkConnection connection in Chopper.Networking.NearbyClients.Within(ServerManager, position))
                StumpHitTargetRpc(connection, treeId);
        }

        [TargetRpc]
        private void LogHitTargetRpc(NetworkConnection connection, int treeId)
        {
            if (_fallen != null && treeId >= 0 && treeId < _fallen.Length && _fallen[treeId] != null) _fallen[treeId].Jolt();
            if (_database == null || treeId < 0 || treeId >= _database.Count) return;
            TreeEntry entry = _database.Get(treeId);
            EmitBurst(entry.Position + Vector3.up * 0.4f, ChipColor, _config.ChipsPerHit, 0.2f);
            PlayFeedback(entry, felled: false);
        }

        [TargetRpc]
        private void StumpHitTargetRpc(NetworkConnection connection, int treeId)
        {
            if (_database == null || treeId < 0 || treeId >= _database.Count) return;
            TreeEntry entry = _database.Get(treeId);
            EmitBurst(entry.Position + Vector3.up * 0.25f, ChipColor, _config.ChipsPerHit, 0.12f);
            PlayFeedback(entry, felled: false);
        }

        [ObserversRpc]
        private void LogFinishedObserversRpc(int treeId)
        {
            _clientLogYaw.Remove(treeId);
            _clientLogReady.Remove(treeId);
            if (_fallen == null || treeId < 0 || treeId >= _fallen.Length) return;
            if (_fallen[treeId] != null) _fallen[treeId].Break();
            _fallen[treeId] = null;
        }

        [ObserversRpc]
        private void StumpClearedObserversRpc(int treeId)
        {
            _clientCleared.Add(treeId);
            if (_stumps == null || treeId < 0 || treeId >= _stumps.Length) return;
            if (_stumps[treeId] != null) Destroy(_stumps[treeId]);
            _stumps[treeId] = null;
            if (_database == null || treeId >= _database.Count) return;
            EmitBurst(_database.Get(treeId).Position + Vector3.up * 0.2f, ChipColor, _config.ChipsPerHit * 2, 0.25f);
        }

        // The pine is authored on a 0.25 m dirt pad (build_pine.py, cells at z = 0). Concept 2.0 has the trunk
        // coming out of the grass, so the pad and a short bite of trunk sit below the ground. Stumps stay on top.
        internal const float PineBuryMeters = 0.40f;

        internal static Vector3 Planted(Vector3 ground, float scale) => ground + Vector3.down * (PineBuryMeters * scale);

        // Authored prop_stump_voxel is 0.75 m wide and 0.75 m tall, base on Y = 0.
        private const float StumpModelSize = 0.75f;

        // Height of the stump CreateStump leaves; the falling trunk is cut there.
        private float StumpTop(TreeEntry entry)
        {
            float diameter = _config.GetTrunkRadius(entry.Size) * 2f;
            return _stumpPrefab != null ? diameter : StumpHeight;
        }

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
