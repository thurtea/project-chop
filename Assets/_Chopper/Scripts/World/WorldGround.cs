using UnityEngine;

namespace Chopper.World
{
    // The one place gameplay asks "where is the ground here?" (W5-08 step 1). Every height, normal and steepness query
    // goes through this facade instead of Terrain.activeTerrain, so the land underneath can change from the Unity
    // Terrain to the block world (W5-08 step 5) without touching the callers. For now it reads the Terrain exactly the
    // way the callers did (SampleHeight plus the terrain's base, the terrain's own smoothed normal and steepness).
    public static class WorldGround
    {
        // True once the world's land has been built.
        public static bool Exists => Terrain.activeTerrain != null;

        // Height of the ground (world Y) under a position, or the position's own Y if there is no land yet.
        public static float HeightAt(Vector3 position)
        {
            Terrain terrain = Terrain.activeTerrain;
            return terrain != null ? terrain.SampleHeight(position) + terrain.transform.position.y : position.y;
        }

        public static float HeightAt(float x, float z) => HeightAt(new Vector3(x, 0f, z));

        // True when a position lies over the land (inside its square), not out past its edge.
        public static bool Covers(Vector3 position)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return false;
            Vector2 uv = UV(terrain, position);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        // The ground's smoothed up direction under a position (straight up off the land or before it exists).
        public static Vector3 NormalAt(Vector3 position)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return Vector3.up;
            Vector2 uv = UV(terrain, position);
            return terrain.terrainData.GetInterpolatedNormal(uv.x, uv.y);
        }

        // The ground's smoothed steepness under a position, in degrees (0 off the land).
        public static float SteepnessAt(Vector3 position)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return 0f;
            Vector2 uv = UV(terrain, position);
            return terrain.terrainData.GetSteepness(uv.x, uv.y);
        }

        // A copy of the land's heights that worker threads may read (the minimap paints its rows in parallel).
        public static HeightSnapshot Snapshot()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return null;
            TerrainData data = terrain.terrainData;
            int res = data.heightmapResolution;
            return new HeightSnapshot(data.GetHeights(0, 0, res, res), res,
                new Vector2(terrain.transform.position.x, terrain.transform.position.z), data.size.x,
                terrain.transform.position.y, data.size.y);
        }

        private static Vector2 UV(Terrain terrain, Vector3 position)
        {
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return new Vector2(local.x / size.x, local.z / size.z);
        }
    }

    // Bilinear heights over a square of land, safe to read from any thread.
    public sealed class HeightSnapshot
    {
        private readonly float[,] _heights;
        private readonly int _res;
        private readonly float _baseY;
        private readonly float _heightScale;

        public Vector2 Min { get; }
        public float Size { get; }

        public HeightSnapshot(float[,] heights, int resolution, Vector2 min, float size, float baseY, float heightScale)
        {
            _heights = heights;
            _res = resolution;
            Min = min;
            Size = size;
            _baseY = baseY;
            _heightScale = heightScale;
        }

        public float HeightAt(float wx, float wz)
        {
            float u = Mathf.Clamp01((wx - Min.x) / Size) * (_res - 1);
            float v = Mathf.Clamp01((wz - Min.y) / Size) * (_res - 1);
            int x0 = Mathf.Min((int)u, _res - 2);
            int z0 = Mathf.Min((int)v, _res - 2);
            float fx = u - x0;
            float fz = v - z0;
            float a = Mathf.Lerp(_heights[z0, x0], _heights[z0, x0 + 1], fx);
            float b = Mathf.Lerp(_heights[z0 + 1, x0], _heights[z0 + 1, x0 + 1], fx);
            return Mathf.Lerp(a, b, fz) * _heightScale + _baseY;
        }
    }
}
