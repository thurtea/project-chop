using System.Threading.Tasks;
using Chopper.World.Generation;
using UnityEngine;

namespace Chopper.World.Voxel
{
    // One height and one top type per 1 m column, quantized to 0.5 m steps from WorldGen.
    // The same seed produces the same table. The Unity Terrain is still the collider.
    public sealed class VoxelColumns
    {
        public const int Resolution = 640;
        public const float Step = 0.5f;

        readonly short[] _top;
        readonly byte[] _type;

        public uint Crc { get; }
        public int BuildMilliseconds { get; }

        VoxelColumns(short[] top, byte[] type, uint crc, int milliseconds)
        {
            _top = top;
            _type = type;
            Crc = crc;
            BuildMilliseconds = milliseconds;
        }

        public short TopUnits(int x, int z) => _top[z * Resolution + x];
        public BlockType TypeAt(int x, int z) => (BlockType)_type[z * Resolution + x];
        public bool Inside(int x, int z) => (uint)x < Resolution && (uint)z < Resolution;

        public float TopY(int x, int z) => TopUnits(x, z) * Step;

        public static float WorldX(int x) => -WorldGen.HalfSize + x;
        public static float WorldZ(int z) => -WorldGen.HalfSize + z;

        public static VoxelColumns Build(WorldGen gen)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var top = new short[Resolution * Resolution];
            var type = new byte[Resolution * Resolution];
            Parallel.For(0, Resolution, z =>
            {
                float wz = -WorldGen.HalfSize + z + 0.5f;
                int row = z * Resolution;
                for (int x = 0; x < Resolution; x++)
                {
                    float wx = -WorldGen.HalfSize + x + 0.5f;
                    float height = gen.HeightAt(wx, wz);
                    top[row + x] = (short)Mathf.Clamp(Mathf.RoundToInt(height / Step), -400, 400);
                    type[row + x] = (byte)Classify(gen, wx, wz, height);
                }
            });
            return new VoxelColumns(top, type, Hash(top, type), (int)watch.ElapsedMilliseconds);
        }

        static BlockType Classify(WorldGen gen, float x, float z, float height)
        {
            float radius = Mathf.Sqrt(x * x + z * z);
            if (height < WorldGen.WaterHeight + 0.25f) return BlockType.Sand;
            if (radius > WorldGen.BeachStartRadius && radius < WorldGen.CoastEndRadius && height < 1.2f) return BlockType.Sand;
            if (gen.MountainWeight(x, z) > 0.35f) return height > 30f ? BlockType.Snow : BlockType.Stone;
            if (gen.PathWeight(x, z) > 0.6f || WorldGen.TerraceStairs(x, z)) return BlockType.Path;
            return BlockType.Grass;
        }

        static uint Hash(short[] top, byte[] type)
        {
            uint crc = 2166136261;
            for (int i = 0; i < top.Length; i++)
            {
                crc ^= (ushort)top[i];
                crc *= 16777619;
                crc ^= type[i];
                crc *= 16777619;
            }
            return crc;
        }
    }
}
