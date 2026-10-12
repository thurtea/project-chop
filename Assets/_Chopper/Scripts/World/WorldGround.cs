using UnityEngine;

namespace Chopper.World
{
    // The one place gameplay asks "where is the ground here?" (W5-08). Heights, normals and the map square come
    // from the block columns. The Unity Terrain object is gone (step 13).
    public static class WorldGround
    {
        // True once the world's land has been built.
        public static bool Exists => Voxel.VoxelColumns.Current != null;

        // Height of the ground (world Y) under a position, or the position's own Y if there is no land yet.
        public static float HeightAt(Vector3 position)
        {
            Voxel.VoxelColumns columns = Voxel.VoxelColumns.Current;
            if (columns != null && columns.TryTopAt(position.x, position.z, out float top)) return top;
            return position.y;
        }

        public static float HeightAt(float x, float z) => HeightAt(new Vector3(x, 0f, z));

        // Lowest and highest block top under a footprint (the box's x/z extent, 5 cm in from each edge so a piece that
        // only touches the next column does not count it). False without blocks.
        public static bool FootprintTops(Bounds box, out float lowest, out float highest)
        {
            lowest = float.MaxValue;
            highest = float.MinValue;
            Voxel.VoxelColumns columns = Voxel.VoxelColumns.Current;
            if (columns == null) return false;
            float x0 = box.min.x + 0.05f, x1 = box.max.x - 0.05f, z0 = box.min.z + 0.05f, z1 = box.max.z - 0.05f;
            for (float x = Mathf.Floor(x0); x <= x1; x += 1f)
            {
                for (float z = Mathf.Floor(z0); z <= z1; z += 1f)
                {
                    if (!columns.TryTopAt(Mathf.Clamp(x + 0.5f, x0, x1), Mathf.Clamp(z + 0.5f, z0, z1), out float top)) continue;
                    lowest = Mathf.Min(lowest, top);
                    highest = Mathf.Max(highest, top);
                }
            }
            return highest >= lowest;
        }

        // True when a position lies over the block table, not out past its edge.
        public static bool Covers(Vector3 position)
        {
            Voxel.VoxelColumns columns = Voxel.VoxelColumns.Current;
            return columns != null && columns.TryTopAt(position.x, position.z, out _);
        }

        // Up direction from the block tops one metre either side (straight up off the land or on a flat top).
        public static Vector3 NormalAt(Vector3 position)
        {
            if (Voxel.VoxelColumns.Current == null) return Vector3.up;
            float h = HeightAt(position);
            float dx = HeightAt(position + Vector3.right) - h;
            float dz = HeightAt(position + Vector3.forward) - h;
            Vector3 normal = new Vector3(-dx, 1f, -dz);
            return normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
        }

        // Steepness under a position, in degrees (0 off the land).
        public static float SteepnessAt(Vector3 position)
        {
            if (Voxel.VoxelColumns.Current == null) return 0f;
            return Mathf.Acos(Mathf.Clamp(NormalAt(position).y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        // The map's square. Heights are read from the columns, not from a stored heightmap.
        public static HeightSnapshot Snapshot()
        {
            if (Voxel.VoxelColumns.Current == null) return null;
            return new HeightSnapshot(null, 2,
                new Vector2(-Generation.WorldGen.HalfSize, -Generation.WorldGen.HalfSize), Generation.WorldGen.TerrainSize,
                0f, 1f);
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
            if (_heights == null) return 0f;
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
