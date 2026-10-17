using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chopper.World
{
    // The meadow's grass carpet, Valheim style: thousands of small blade clumps drawn with GPU instancing around each
    // camera instead of one GameObject per plant. Where grass grows, how dense, how dry and how tall comes from a map
    // baked by MeadowBuilder (R density, G dryness, B lushness, A height). Clumps are generated per terrain cell on
    // demand from a fixed seed, so every peer sees the same carpet, and forgotten when the camera moves far away.
    // Wind, leaning away from players and enemies (GrassPushers) and the color all happen in the Chopper/Grass shader.
    [ExecuteAlways]
    public class GrassCarpet : MonoBehaviour
    {
        [SerializeField] private Material _material;
        [SerializeField] private Texture2D _map;
        [Tooltip("World x and z of the map's corner, then its width and depth in meters.")]
        [SerializeField] private Vector4 _mapRect = new Vector4(-320f, -320f, 640f, 640f);
        [SerializeField, Min(2f)] private float _cellSize = 10f;
        [SerializeField, Min(10f)] private float _drawDistance = 70f;
        [Tooltip("Clumps per square meter where the map's density is full.")]
        // Concept 2.0: separate clumps on textured grass blocks, not a wheat carpet (was 4).
        [SerializeField, Min(0.1f)] private float _clumpsPerSquareMeter = 1.6f;
        [Tooltip("Beyond this share of the draw distance fewer clumps are drawn, down to Far Fraction at the edge.")]
        [SerializeField, Range(0f, 1f)] private float _thinStart = 0.45f;
        [SerializeField, Range(0.05f, 1f)] private float _farFraction = 0.55f;
        [SerializeField] private int _seed = 4242;

        public static float FarThin = 0.15f;
        private const int MaxPushers = 16;
        // New cells built in one frame. Running at full speed needs about one row of cells a second, so a small budget
        // keeps up and spreads the work instead of building a burst in a single frame.
        private const int MaxCellsBuiltPerCamera = 10;
        // Past this share of the draw distance a clump is drawn with the cheap mesh (half the blades, two segments,
        // wider blades), about a third of the triangles. Far away the difference cannot be seen.
        private const float FarMeshFraction = 0.4f;

        private sealed class Cell
        {
            public Matrix4x4[] Matrices;
            public Bounds Bounds;
            public int LastUsedFrame;
        }

        private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();
        private readonly List<long> _evict = new List<long>();
        private readonly Vector4[] _pushers = new Vector4[MaxPushers];
        private readonly Plane[] _planes = new Plane[6];
        private Mesh _mesh;
        private Mesh _farMesh;
        private Color32[] _pixels;
        private int _mapWidth;
        private int _mapHeight;
        private static readonly int PushersId = Shader.PropertyToID("_GrassPushers");
        private static readonly int PusherCountId = Shader.PropertyToID("_GrassPusherCount");
        private static readonly int MapId = Shader.PropertyToID("_GrassMap");
        private static readonly int MapRectId = Shader.PropertyToID("_GrassMapRect");

        public int CachedCellCount => _cells.Count;

        public static GrassCarpet Instance { get; private set; }

        // How thick the grass is at a spot, 0 to 1 (used for footstep sounds). 0 if the map is not loaded.
        public float DensityAt(float x, float z) => Ready() ? Sample(x, z).r : 0f;

        public void Configure(Material material, Texture2D map, Vector4 mapRect)
        {
            _material = material;
            _map = map;
            _mapRect = mapRect;
        }

        private void OnEnable()
        {
            Instance = this;
            ScatterMask.Changed += OnMaskChanged;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            ScatterMask.Changed -= OnMaskChanged;
            if (Instance == this) Instance = null;
            _cells.Clear();
            _pixels = null;
            Release(ref _farMesh);
            Release(ref _mesh);
        }

        private void OnMaskChanged(Bounds zone)
        {
            zone.Expand(new Vector3(ScatterMask.Margin * 2f, ScatterMask.BelowReach * 2f, ScatterMask.Margin * 2f));
            _evict.Clear();
            foreach (KeyValuePair<long, Cell> pair in _cells)
            {
                Bounds b = pair.Value.Bounds;
                if (b.min.x <= zone.max.x && b.max.x >= zone.min.x && b.min.z <= zone.max.z && b.max.z >= zone.min.z) _evict.Add(pair.Key);
            }
            foreach (long key in _evict) _cells.Remove(key);
            _evict.Clear();
        }

        public int ClumpsIn(Bounds zone)
        {
            int count = 0;
            foreach (Cell cell in _cells.Values)
            {
                if (!cell.Bounds.Intersects(zone)) continue;
                foreach (Matrix4x4 m in cell.Matrices) if (zone.Contains(m.GetColumn(3))) count++;
            }
            return count;
        }

        static void Release(ref Mesh mesh)
        {
            if (mesh == null) return;
            if (mesh.hideFlags == HideFlags.DontSave)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            mesh = null;
        }

        private bool Ready()
        {
            if (_material == null || _map == null) return false;
            if (!WorldGround.Exists) return false;
            if (_mesh == null) _mesh = LoadTuft("prop_grass_tuft_voxel_a_carpet") ?? BuildClumpMesh(_seed);
            if (_farMesh == null) _farMesh = LoadTuft("prop_grass_tuft_voxel_c_carpet") ?? BuildClumpMesh(_seed, 10, 2, 1.7f);
            if (_pixels == null)
            {
                if (!_map.isReadable)
                {
                    Debug.LogWarning("[GrassCarpet] The grass map is not readable; rerun Rebuild Network Scenes.");
                    return false;
                }
                _pixels = _map.GetPixels32();
                _mapWidth = _map.width;
                _mapHeight = _map.height;
            }
            return true;
        }

        // Voxel tufts from the world kit, vertex colour already in the Chopper/Grass channels (red = height).
        // The smooth blade clump stays as the fallback when those meshes are not in the build.
        static Mesh LoadTuft(string name)
        {
            var prefab = Resources.Load<GameObject>("Foliage/" + name);
            if (prefab == null) return null;
            var filter = prefab.GetComponentInChildren<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.vertexCount == 0) return null;
            Debug.Log($"[GrassCarpet] clump {name} verts {mesh.vertexCount} size {mesh.bounds.size.x:F2} x {mesh.bounds.size.y:F2} x {mesh.bounds.size.z:F2}");
            return mesh;
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            // The game camera and the editor's scene view; not previews, reflections or the map cameras.
            bool sceneView = camera.cameraType == CameraType.SceneView;
            if (!sceneView && (camera.cameraType != CameraType.Game || camera != Camera.main)) return;
            if (!Ready()) return;
            // Nothing to draw from inside a crypt, far under the meadow.
            if (Chopper.World.Generation.CryptWorld.IsUnderground(camera.transform.position)) return;

            Shader.SetGlobalTexture(MapId, _map);
            Shader.SetGlobalVector(MapRectId, new Vector4(_mapRect.x, _mapRect.y, 1f / _mapRect.z, 1f / _mapRect.w));
            UploadPushers(camera.transform.position);

            Vector3 eye = camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            int minX = Mathf.FloorToInt((eye.x - _drawDistance) / _cellSize);
            int maxX = Mathf.FloorToInt((eye.x + _drawDistance) / _cellSize);
            int minZ = Mathf.FloorToInt((eye.z - _drawDistance) / _cellSize);
            int maxZ = Mathf.FloorToInt((eye.z + _drawDistance) / _cellSize);
            float reach = _drawDistance + _cellSize * 0.75f;
            int built = 0;

            var renderParams = new RenderParams(_material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                camera = camera,
                layer = gameObject.layer
            };

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    float centerX = (cx + 0.5f) * _cellSize;
                    float centerZ = (cz + 0.5f) * _cellSize;
                    float dx = centerX - eye.x;
                    float dz = centerZ - eye.z;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance > reach) continue;

                    long key = ((long)cx << 32) ^ (uint)cz;
                    if (!_cells.TryGetValue(key, out Cell cell))
                    {
                        if (built >= MaxCellsBuiltPerCamera) continue;
                        cell = BuildCell(cx, cz);
                        _cells[key] = cell;
                        built++;
                    }
                    cell.LastUsedFrame = Time.frameCount;
                    if (cell.Matrices.Length == 0) continue;
                    if (!GeometryUtility.TestPlanesAABB(_planes, cell.Bounds)) continue;

                    // Thin the carpet with distance; clumps are stored in random order, so any prefix is an even spread.
                    float t = Mathf.InverseLerp(_drawDistance * _thinStart, _drawDistance, distance);
                    int count = Mathf.CeilToInt(cell.Matrices.Length * Mathf.Lerp(1f, Mathf.Min(_farFraction, FarThin), t));
                    if (count <= 0) continue;
                    renderParams.worldBounds = cell.Bounds;
                    Mesh clump = distance > _drawDistance * FarMeshFraction && _farMesh != null ? _farMesh : _mesh;
                    Graphics.RenderMeshInstanced(renderParams, clump, 0, cell.Matrices, count);
                }
            }

            ForgetFarCells();
        }

        // Cells unused for a while (the camera moved on) are dropped to keep memory flat on a long walk.
        private void ForgetFarCells()
        {
            if (_cells.Count < 600 || Time.frameCount % 120 != 0) return;
            _evict.Clear();
            foreach (KeyValuePair<long, Cell> pair in _cells)
            {
                if (Time.frameCount - pair.Value.LastUsedFrame > 300) _evict.Add(pair.Key);
            }
            foreach (long key in _evict) _cells.Remove(key);
        }

        private void UploadPushers(Vector3 eye)
        {
            int count = 0;
            IReadOnlyList<GrassPushers.Pusher> all = GrassPushers.All;
            for (int i = 0; i < all.Count && count < MaxPushers; i++)
            {
                Transform pusher = all[i].Transform;
                if (pusher == null) continue;
                Vector3 p = pusher.position;
                if ((p - eye).sqrMagnitude > _drawDistance * _drawDistance) continue;
                _pushers[count++] = new Vector4(p.x, p.y, p.z, all[i].Radius + 0.5f);
            }
            for (int i = count; i < MaxPushers; i++) _pushers[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(PushersId, _pushers);
            Shader.SetGlobalFloat(PusherCountId, count);
        }

        // Reads the map with bilinear filtering at a world position. Outside the map there is no grass.
        public Color Sample(float x, float z)
        {
            float u = (x - _mapRect.x) / _mapRect.z * _mapWidth - 0.5f;
            float v = (z - _mapRect.y) / _mapRect.w * _mapHeight - 0.5f;
            if (u < 0f || v < 0f || u >= _mapWidth - 1 || v >= _mapHeight - 1) return Color.clear;
            int x0 = (int)u;
            int y0 = (int)v;
            float fx = u - x0;
            float fy = v - y0;
            Color a = Color.Lerp(_pixels[y0 * _mapWidth + x0], _pixels[y0 * _mapWidth + x0 + 1], fx);
            Color b = Color.Lerp(_pixels[(y0 + 1) * _mapWidth + x0], _pixels[(y0 + 1) * _mapWidth + x0 + 1], fx);
            return Color.Lerp(a, b, fy);
        }

        private Cell BuildCell(int cx, int cz)
        {
            var random = new System.Random(unchecked(_seed ^ (cx * 73856093) ^ (cz * 19349663)));
            int candidates = Mathf.RoundToInt(_cellSize * _cellSize * _clumpsPerSquareMeter);
            var matrices = new List<Matrix4x4>(candidates);
            float originX = cx * _cellSize;
            float originZ = cz * _cellSize;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < candidates; i++)
            {
                float x = originX + (float)random.NextDouble() * _cellSize;
                float z = originZ + (float)random.NextDouble() * _cellSize;
                float roll = (float)random.NextDouble();
                float yaw = (float)random.NextDouble() * 360f;
                float size = 0.8f + (float)random.NextDouble() * 0.45f;
                float tiltX = ((float)random.NextDouble() - 0.5f) * 14f;
                float tiltZ = ((float)random.NextDouble() - 0.5f) * 14f;
                Color map = Sample(x, z);
                if (roll >= map.r) continue;

                float y = WorldGround.HeightAt(x, z) - 0.04f;
                if (!ScatterMask.Allows(new Vector3(x, y, z), true)) continue;
                // A: height, 0 is half the base height and 1 is one and a half times it.
                float tall = size * (0.5f + map.a);
                Quaternion rotation = Quaternion.Euler(tiltX, yaw, tiltZ);
                matrices.Add(Matrix4x4.TRS(new Vector3(x, y, z), rotation, new Vector3(size, tall, size)));
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            // Random order already (positions were drawn at random), so drawing a prefix thins evenly.
            var cell = new Cell { Matrices = matrices.ToArray() };
            if (matrices.Count == 0) minY = maxY = 0f;
            var center = new Vector3(originX + _cellSize * 0.5f, (minY + maxY) * 0.5f + 0.5f, originZ + _cellSize * 0.5f);
            cell.Bounds = new Bounds(center, new Vector3(_cellSize + 2f, maxY - minY + 3f, _cellSize + 2f));
            return cell;
        }

        // One clump: a tuft of thin tapered blades leaning outward from a small circle. Vertex color red is the height
        // along the blade (0 root, 1 tip), green a random number per blade for the shader's variation.
        public static Mesh BuildClumpMesh(int seed) => BuildClumpMesh(seed, 20, 3, 1f);

        // The same tuft with fewer blades and segments; widthScale widens the blades so it covers as much ground.
        public static Mesh BuildClumpMesh(int seed, int blades, int segments, float widthScale)
        {
            var random = new System.Random(seed);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (int b = 0; b < blades; b++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * 0.42f;
                var root = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                float height = 0.26f + (float)random.NextDouble() * 0.30f;
                float width = (0.026f + (float)random.NextDouble() * 0.022f) * widthScale;
                float facing = (float)random.NextDouble() * Mathf.PI * 2f;
                var across = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));
                // Blades lean outward from the clump center and curve over toward the tip.
                Vector3 outward = radius > 0.01f ? root.normalized : across;
                float lean = 0.15f + (float)random.NextDouble() * 0.35f;
                float bladeRandom = (float)random.NextDouble();
                int start = vertices.Count;

                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    float halfWidth = width * 0.5f * (1f - t * 0.92f);
                    Vector3 spine = root + Vector3.up * (height * t) + outward * (lean * height * t * t);
                    vertices.Add(spine - across * halfWidth);
                    vertices.Add(spine + across * halfWidth);
                    normals.Add(Vector3.up);
                    normals.Add(Vector3.up);
                    colors.Add(new Color(t, bladeRandom, 0f, 1f));
                    colors.Add(new Color(t, bladeRandom, 0f, 1f));
                }
                for (int s = 0; s < segments; s++)
                {
                    int i = start + s * 2;
                    triangles.Add(i);
                    triangles.Add(i + 2);
                    triangles.Add(i + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + 2);
                    triangles.Add(i + 3);
                }
            }

            var mesh = new Mesh { name = "GrassClump", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
