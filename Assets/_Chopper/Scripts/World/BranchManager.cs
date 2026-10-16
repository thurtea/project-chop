using System.Collections.Generic;
using Chopper.Player;
using Chopper.Trees;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Chopper.World
{
    // Fallen branches lying in the grass, like Valheim's: press interact next to one to pick it up for a piece of wood,
    // so nobody has to punch a tree to get started. Some lie all around the broken hut (knocked off when it was
    // wrecked), a few on the way out of the yard, more in the forests.
    // Like the herbs, positions come from a fixed seed on every peer and only picked numbers are networked; dawn
    // brings them all back.
    public class BranchManager : NetworkBehaviour
    {
        private const int Seed = 9157;
        private const int Count = 330;
        private const int AroundHutCount = 18;
        private const int NearGateCount = 10;
        private const float PickReach = 2.2f;
        private const float MaxSlopeDegrees = 28f;
        public const int WoodPerBranch = 1;

        [SerializeField] private Material _material;

        private Vector3[] _positions;
        private GameObject[] _views;
        private bool[] _picked;
        private Mesh[] _meshes;

        public static BranchManager Instance { get; private set; }

        public int BranchCount => _positions != null ? _positions.Length : 0;

        public Vector3 PositionOf(int id) => _positions[id];

        public bool IsPicked(int id) => _picked[id];

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
            if (_meshes == null) return;
            foreach (Mesh mesh in _meshes) Destroy(mesh);
        }

        private void BuildPositions()
        {
            var list = new List<Vector3>();
            // Each world drops its branches in its own places.
            var rng = new System.Random(Seed ^ Chopper.World.Generation.WorldSession.Seed);
            bool hasGround = Chopper.World.WorldGround.Exists;
            if (hasGround)
            {
                for (int tries = 0; tries < Count * 40 && list.Count < Count; tries++)
                {
                    float angle;
                    float radius;
                    if (list.Count < AroundHutCount)
                    {
                        // Knocked off the broken hut: scattered all around the house.
                        angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        radius = Mathf.Lerp(12.5f, 18f, (float)rng.NextDouble());
                    }
                    else if (list.Count < AroundHutCount + NearGateCount)
                    {
                        // Just north of the yard (+Z), where a new player walks first.
                        angle = Mathf.PI * 0.5f + ((float)rng.NextDouble() - 0.5f) * 1.6f;
                        radius = Mathf.Lerp(16f, 40f, (float)rng.NextDouble());
                    }
                    else break;
                    var p = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    p.y = Chopper.World.WorldGround.HeightAt(p);
                    if (WaterSystem.DepthAt(p, out _) > 0.02f) continue;
                    // Not on the mountains between the zones: their tops cannot be reached.
                    if (Chopper.World.Generation.WorldSession.Gen != null && Chopper.World.Generation.WorldSession.Gen.MountainWeight(p.x, p.z) > 0.2f) continue;
                    float dx = Chopper.World.WorldGround.HeightAt(p + Vector3.right * 1.5f) - Chopper.World.WorldGround.HeightAt(p - Vector3.right * 1.5f);
                    float dz = Chopper.World.WorldGround.HeightAt(p + Vector3.forward * 1.5f) - Chopper.World.WorldGround.HeightAt(p - Vector3.forward * 1.5f);
                    if (Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) / 3f) * Mathf.Rad2Deg > MaxSlopeDegrees) continue;
                    list.Add(p);
                }
            }
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            TreeDatabase trees = Chopper.World.Generation.WorldSession.Trees;
            if (hasGround && gen != null && trees != null)
            {
                var entries = new TreeEntry[trees.Count];
                for (int i = 0; i < entries.Length; i++) entries[i] = trees.Get(i);
                foreach (Vector2 spot in gen.BranchSpots(entries, Count - list.Count))
                {
                    var p = new Vector3(spot.x, 0f, spot.y);
                    p.y = Chopper.World.WorldGround.HeightAt(p);
                    if (WaterSystem.DepthAt(p, out _) > 0.02f) continue;
                    list.Add(p);
                }
            }
            _positions = list.ToArray();
            _picked = new bool[_positions.Length];
        }

        private void BuildViews()
        {
            var root = new GameObject("Branches").transform;
            root.SetParent(transform, false);
            _views = new GameObject[_positions.Length];
            if (_material == null || Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

            _meshes = new Mesh[4];
            for (int m = 0; m < _meshes.Length; m++) _meshes[m] = BuildBranchMesh(Seed + m);
            bool hasGround = Chopper.World.WorldGround.Exists;
            for (int i = 0; i < _positions.Length; i++)
            {
                var view = new GameObject("Branch_" + i);
                view.transform.SetParent(root, false);
                // Lie along the slope so the stick does not float on hillsides.
                Vector3 normal = hasGround ? WorldGround.NormalAt(_positions[i]) : Vector3.up;
                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
                view.transform.SetPositionAndRotation(_positions[i], tilt * Quaternion.Euler(0f, (i * 137) % 360, 0f));
                GameObject voxel = Resources.Load<GameObject>("Props/prop_branch_" + (i % 4) + "_voxel");
                if (voxel != null)
                {
                    GameObject model = Instantiate(voxel, view.transform);
                    model.transform.localPosition = Vector3.zero;
                    foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Destroy(collider);
                    Bounds bounds = default;
                    bool any = false;
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        if (!any) { bounds = r.bounds; any = true; }
                        else bounds.Encapsulate(r.bounds);
                    }
                    if (any)
                    {
                        Vector3 local = view.transform.InverseTransformPoint(bounds.center);
                        model.transform.localPosition = new Vector3(-local.x, 0f, -local.z);
                    }
                    _views[i] = view;
                    continue;
                }
                view.AddComponent<MeshFilter>().sharedMesh = _meshes[i % _meshes.Length];
                var meshRenderer = view.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                _views[i] = view;
            }
        }

        // A crooked stick about a meter long with a side twig: a few bent six sided tubes, built once per variant.
        public static Mesh BuildBranchMesh(int seed)
        {
            var random = new System.Random(seed);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float length = 0.85f + (float)random.NextDouble() * 0.35f;
            float bend = ((float)random.NextDouble() - 0.5f) * 0.25f;
            var spine = new List<Vector3>();
            for (int s = 0; s <= 4; s++)
            {
                float t = s / 4f;
                spine.Add(new Vector3((t - 0.5f) * length, 0.035f, Mathf.Sin(t * Mathf.PI) * bend));
            }
            AddTube(spine, 0.04f, 0.022f, vertices, normals, uvs, triangles);

            // The twig forks off a little past the middle.
            Vector3 fork = spine[2] + (spine[3] - spine[2]) * 0.4f;
            float side = random.NextDouble() < 0.5 ? -1f : 1f;
            var twig = new List<Vector3>
            {
                fork,
                fork + new Vector3(0.12f, 0.03f, 0.10f * side),
                fork + new Vector3(0.26f, 0.05f, 0.17f * side)
            };
            AddTube(twig, 0.018f, 0.008f, vertices, normals, uvs, triangles);

            var mesh = new Mesh { name = "Branch" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTube(List<Vector3> spine, float startRadius, float endRadius, List<Vector3> vertices,
            List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            const int sides = 6;
            int start = vertices.Count;
            for (int s = 0; s < spine.Count; s++)
            {
                float t = s / (float)(spine.Count - 1);
                Vector3 forward = (s < spine.Count - 1 ? spine[s + 1] - spine[s] : spine[s] - spine[s - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                Vector3 up = Vector3.Cross(forward, right);
                float radius = Mathf.Lerp(startRadius, endRadius, t);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    Vector3 n = right * Mathf.Cos(a) + up * Mathf.Sin(a);
                    vertices.Add(spine[s] + n * radius);
                    normals.Add(n);
                    uvs.Add(new Vector2(k / (float)sides, t * 2f));
                }
            }
            int ring = sides + 1;
            for (int s = 0; s < spine.Count - 1; s++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = start + s * ring + k;
                    int b = a + ring;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }
        }

        // ---------- Client ----------

        public bool BranchInReach(Vector3 position) => FindNearest(position) >= 0;

        private int FindNearest(Vector3 position)
        {
            int best = -1;
            float bestSqr = PickReach * PickReach;
            for (int i = 0; i < _positions.Length; i++)
            {
                if (_picked[i]) continue;
                Vector3 offset = _positions[i] - position;
                if (Mathf.Abs(offset.y) > 2.5f) continue;
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

        private void OnPhaseChanged(Chopper.Core.DayPhase phase)
        {
            if (phase != Chopper.Core.DayPhase.Dawn || !IsServerInitialized) return;
            ServerRegrowAll();
        }

        // Server only. Picks up the nearest branch in reach: it drops on the ground as wood, taken by walking over it.
        // Returns false when none is near.
        public bool ServerTryPick(Vector3 position, PlayerWood wood)
        {
            int id = FindNearest(position);
            if (id < 0) return false;

            _picked[id] = true;
            if (Chopper.Enemies.EnemyDirector.Instance != null)
            {
                Chopper.Enemies.EnemyDirector.Instance.ServerDropItem(GroundItemKind.Wood, WoodPerBranch, _positions[id], position);
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
        private void PickedObserversRpc(int id) => ApplyPicked(id);

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
            foreach (int id in ids) ApplyPicked(id);
        }

        private void ApplyPicked(int id)
        {
            if (id < 0 || id >= _picked.Length) return;
            _picked[id] = true;
            if (_views[id] != null) _views[id].SetActive(false);
        }
    }
}
