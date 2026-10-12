using Chopper.Core;
using Chopper.World;
using UnityEngine;

namespace Chopper.UI
{
    // The world map texture (plan: Valheim style exploration). The terrain is painted once, then only the parts the
    // local player has walked near are shown; the rest stays dark, with a soft edge between the two.
    // The painting reads like Valheim's map: ground in each zone's color under hill shading lit from the north west,
    // darker tree canopies, paths in trodden dirt, grey stone with contour lines on the mountains, and water that
    // deepens away from a dark shoreline with a pale sandy rim. One pixel is about 0.8 m, sharp enough for the minimap.
    public class MapService
    {
        public const int Resolution = 768;
        private const float RevealRadiusMeters = 45f;
        private const float StartRevealRadiusMeters = 70f;
        // The outer part of a reveal fades in, so explored ground has a soft edge.
        private const float RevealFeatherMeters = 10f;
        private const float RevealStepMeters = 2f;
        private const float ApplyInterval = 0.25f;
        private const float MountainHeight = 7f;
        private const float ContourMeters = 6f;
        private const float WaterMinDepth = 0.05f;
        private static readonly Color32 Unexplored = new Color32(34, 32, 27, 255);
        private static readonly Color Parchment = new Color(0.78f, 0.72f, 0.55f);
        private static readonly Color Stone = new Color(0.55f, 0.54f, 0.52f);
        private static readonly Color Trodden = new Color(0.62f, 0.5f, 0.34f);
        private static readonly Color Shallow = new Color(0.33f, 0.58f, 0.68f);
        private static readonly Color Deep = new Color(0.12f, 0.3f, 0.46f);
        private static readonly Color Shore = new Color(0.1f, 0.2f, 0.27f);
        private static readonly Color Sand = new Color(0.8f, 0.74f, 0.55f);
        // North west sun, fairly high, for the hill shading.
        private static readonly Vector3 Sun = new Vector3(-0.6f, 1.1f, 0.6f).normalized;

        private Color32[] _base;
        private Color32[] _pixels;
        private byte[] _explored;
        private float _cell;
        // The trees come from the world's tree list, which can arrive after the map is first painted.
        private bool _treesStamped;
        private bool _dirty;
        private float _nextApply;
        private Vector3 _lastRevealPosition = new Vector3(float.MaxValue, 0f, 0f);

        public Texture2D Texture { get; private set; }
        public Vector2 Min { get; private set; }
        public float Size { get; private set; }
        public bool Ready => Texture != null;

        // Paints the base map. Returns false if there is no terrain yet.
        public bool Build(GameConfig config)
        {
            HeightSnapshot snap = WorldGround.Snapshot();
            if (snap == null) return false;

            Vector2 min = snap.Min;
            float size = snap.Size;
            Min = min;
            Size = size;
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            float hubRadius = config.HubRadius;
            float cell = size / Resolution;

            // Water discs, copied so the rows can be painted on worker threads.
            int waterCount = WaterSurface.All.Count;
            var waterCenter = new Vector2[waterCount];
            var waterRadius = new float[waterCount];
            var waterInner = new float[waterCount];
            var waterY = new float[waterCount];
            for (int i = 0; i < waterCount; i++)
            {
                WaterSurface water = WaterSurface.All[i];
                waterCenter[i] = new Vector2(water.transform.position.x, water.transform.position.z);
                waterRadius[i] = water.Radius;
                waterInner[i] = water.InnerRadius;
                waterY[i] = water.SurfaceY;
            }

            float HeightAt(float wx, float wz) => snap.HeightAt(wx, wz);

            float DepthAt(float wx, float wz, float ground)
            {
                float best = 0f;
                for (int i = 0; i < waterCount; i++)
                {
                    float dx = wx - waterCenter[i].x;
                    float dz = wz - waterCenter[i].y;
                    float d = dx * dx + dz * dz;
                    if (d > waterRadius[i] * waterRadius[i] || d < waterInner[i] * waterInner[i]) continue;
                    best = Mathf.Max(best, waterY[i] - ground);
                }
                return best;
            }

            int count = Resolution * Resolution;
            var heights = new float[count];
            var depths = new float[count];
            var stones = new float[count];
            var land = new Color[count];
            System.Threading.Tasks.Parallel.For(0, Resolution, y =>
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float wx = min.x + (x + 0.5f) * cell;
                    float wz = min.y + (y + 0.5f) * cell;
                    float h = HeightAt(wx, wz);
                    int i = y * Resolution + x;
                    heights[i] = h;
                    depths[i] = DepthAt(wx, wz, h);

                    Element zone = ZoneMap.ElementAt(wx, wz, hubRadius);
                    Color color = Color.Lerp(ZonePalette.Ground(zone) * 1.15f, Parchment, 0.25f);
                    if (gen != null) color = Color.Lerp(color, Trodden, gen.PathWeight(wx, wz) * 0.85f);
                    // The mountains read as grey stone; without the generator, any high ground does.
                    float stone = gen != null ? Mathf.Clamp01(gen.MountainWeight(wx, wz) * 2.5f) : Mathf.Clamp01((h - MountainHeight) / 10f);
                    stones[i] = stone;
                    land[i] = Color.Lerp(color, Stone, stone);
                }
            });

            // Hill shading, contour lines on stone, then water and its edges.
            var painted = new Color32[count];
            System.Threading.Tasks.Parallel.For(0, Resolution, y =>
            {
                for (int x = 0; x < Resolution; x++)
                {
                    int i = y * Resolution + x;
                    int left = y * Resolution + Mathf.Max(0, x - 1);
                    int right = y * Resolution + Mathf.Min(Resolution - 1, x + 1);
                    int down = Mathf.Max(0, y - 1) * Resolution + x;
                    int up = Mathf.Min(Resolution - 1, y + 1) * Resolution + x;
                    bool besideWater = depths[left] > WaterMinDepth || depths[right] > WaterMinDepth || depths[down] > WaterMinDepth || depths[up] > WaterMinDepth;
                    bool besideLand = depths[left] <= WaterMinDepth || depths[right] <= WaterMinDepth || depths[down] <= WaterMinDepth || depths[up] <= WaterMinDepth;
                    Color color;
                    if (depths[i] > WaterMinDepth)
                    {
                        color = besideLand ? Shore : Color.Lerp(Shallow, Deep, Mathf.Clamp01(depths[i] / 3f));
                    }
                    else
                    {
                        float dx = (heights[right] - heights[left]) / (2f * cell);
                        float dz = (heights[up] - heights[down]) / (2f * cell);
                        Vector3 normal = new Vector3(-dx * 1.6f, 1f, -dz * 1.6f).normalized;
                        float shade = 0.5f + 0.55f * Mathf.Max(0f, Vector3.Dot(normal, Sun));
                        color = land[i] * Mathf.Clamp(shade, 0.5f, 1.15f);
                        float band = Mathf.Floor(heights[i] / ContourMeters);
                        bool contour = band != Mathf.Floor(heights[right] / ContourMeters) || band != Mathf.Floor(heights[up] / ContourMeters);
                        if (contour) color = Color.Lerp(color, color * 0.8f, stones[i]);
                        if (besideWater) color = Color.Lerp(color, Sand, 0.6f);
                    }
                    color.a = 1f;
                    painted[i] = color;
                }
            });
            _base = painted;

            _cell = cell;
            _treesStamped = StampTrees(cell);

            Texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _pixels = new Color32[count];
            _explored = new byte[count];
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = Unexplored;
            Reveal(Vector3.zero, StartRevealRadiusMeters);
            Apply();
            return true;
        }

        // Tree canopies as soft darker blots, bigger for bigger trees, lit on their north west side.
        private bool StampTrees(float cell)
        {
            Chopper.Trees.TreeManager manager = Chopper.Trees.TreeManager.Instance;
            Chopper.Trees.TreeDatabase trees = manager != null ? manager.Database : null;
            if (trees == null || trees.Count == 0) return false;
            for (int id = 0; id < trees.Count; id++)
            {
                Chopper.Trees.TreeEntry tree = trees.Get(id);
                float radius = tree.Size == TreeSize.Large ? 3.8f : tree.Size == TreeSize.Medium ? 3f : 2.2f;
                Vector2 uv = WorldToMap(tree.Position);
                float cx = uv.x * Resolution;
                float cy = uv.y * Resolution;
                float r = radius / cell;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r));
                int x1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt(cx + r));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r));
                int y1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt(cy + r));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = (x + 0.5f - cx) / r;
                        float dy = (y + 0.5f - cy) / r;
                        float d = dx * dx + dy * dy;
                        if (d > 1f) continue;
                        // Darker toward the south east, lighter on the sunny north west rim.
                        float dark = 0.58f + 0.3f * Mathf.Clamp01(0.5f - dx * 0.5f + dy * 0.5f);
                        Color c = _base[y * Resolution + x];
                        Color canopy = new Color(c.r * dark * 0.85f, c.g * dark, c.b * dark * 0.8f, 1f);
                        c = Color.Lerp(c, canopy, Mathf.Clamp01((1f - d) * 3f));
                        c.a = 1f;
                        _base[y * Resolution + x] = c;
                    }
                }
            }
            return true;
        }

        // True once the local player has been near this spot (crypt markers only show on explored ground).
        public bool IsExplored(Vector3 world)
        {
            if (!Ready) return false;
            Vector2 uv = WorldToMap(world);
            int x = Mathf.FloorToInt(uv.x * Resolution);
            int y = Mathf.FloorToInt(uv.y * Resolution);
            return x >= 0 && y >= 0 && x < Resolution && y < Resolution && _explored[y * Resolution + x] > 127;
        }

        public Vector2 WorldToMap(Vector3 world)
        {
            return new Vector2((world.x - Min.x) / Size, (world.z - Min.y) / Size);
        }

        // Shows the part of the map within radius of a position.
        public void Reveal(Vector3 position, float radius)
        {
            float cell = Size / Resolution;
            int span = Mathf.CeilToInt(radius / cell);
            Vector2 uv = WorldToMap(position);
            int cx = Mathf.FloorToInt(uv.x * Resolution);
            int cy = Mathf.FloorToInt(uv.y * Resolution);
            float radiusSqr = radius * radius;

            for (int y = Mathf.Max(0, cy - span); y <= Mathf.Min(Resolution - 1, cy + span); y++)
            {
                for (int x = Mathf.Max(0, cx - span); x <= Mathf.Min(Resolution - 1, cx + span); x++)
                {
                    int i = y * Resolution + x;
                    if (_explored[i] == 255) continue;
                    float dx = (x + 0.5f) * cell - (position.x - Min.x);
                    float dy = (y + 0.5f) * cell - (position.z - Min.y);
                    float distanceSqr = dx * dx + dy * dy;
                    if (distanceSqr > radiusSqr) continue;
                    float amount = Mathf.Clamp01((radius - Mathf.Sqrt(distanceSqr)) / RevealFeatherMeters);
                    byte level = (byte)Mathf.RoundToInt(amount * 255f);
                    if (level <= _explored[i]) continue;
                    _explored[i] = level;
                    _pixels[i] = Color32.Lerp(Unexplored, _base[i], level / 255f);
                    _dirty = true;
                }
            }
        }

        // Shows the whole map at once (the console's explore command, for screenshots and testing).
        public void RevealAll()
        {
            if (!Ready) return;
            for (int i = 0; i < _pixels.Length; i++)
            {
                _explored[i] = 255;
                _pixels[i] = _base[i];
            }
            Apply();
        }

        public void Tick(Vector3 playerPosition)
        {
            if (!Ready) return;
            if (!_treesStamped && StampTrees(_cell))
            {
                _treesStamped = true;
                for (int i = 0; i < _pixels.Length; i++)
                    if (_explored[i] > 0) _pixels[i] = Color32.Lerp(Unexplored, _base[i], _explored[i] / 255f);
                _dirty = true;
            }
            if ((playerPosition - _lastRevealPosition).sqrMagnitude > RevealStepMeters * RevealStepMeters)
            {
                _lastRevealPosition = playerPosition;
                Reveal(playerPosition, RevealRadiusMeters);
            }
            if (_dirty && Time.unscaledTime >= _nextApply) Apply();
        }

        private void Apply()
        {
            Texture.SetPixels32(_pixels);
            Texture.Apply(false);
            _dirty = false;
            _nextApply = Time.unscaledTime + ApplyInterval;
        }
    }
}
