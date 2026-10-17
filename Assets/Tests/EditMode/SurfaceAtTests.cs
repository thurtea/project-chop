using Chopper.World.Generation;
using Chopper.World.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class SurfaceAtTests
    {
        [Test]
        public void SurfaceMatchesBlockTopOnTerracedBanks()
        {
            var gen = new WorldGen(12345, 90f);
            VoxelColumns columns = VoxelColumns.Build(gen);
            int checkedBanks = 0;
            for (int i = 0; i < gen.WaterBodies.Length; i++)
            {
                Vector2 c = gen.WaterBodies[i].Center;
                float edge = WorldGen.ShoreEdge(gen.WaterBodies[i]);
                for (int a = 0; a < 36; a++)
                {
                    float ang = a * 10f * Mathf.Deg2Rad;
                    for (float d = edge - 2f; d < edge + 8f; d += 1f)
                    {
                        float x = c.x + Mathf.Cos(ang) * d, z = c.y + Mathf.Sin(ang) * d;
                        x = Mathf.Floor(x) + 0.5f; z = Mathf.Floor(z) + 0.5f;
                        if (!columns.TryTopAt(x, z, out float top)) continue;
                        float raw = gen.HeightAt(x, z);
                        float surface = gen.SurfaceAt(x, z);
                        if (VoxelColumns.TerraceAt(gen, x, z, raw) == raw) continue;
                        checkedBanks++;
                        Assert.AreEqual(top, surface, 0.01f, $"bank at {x:0.0},{z:0.0}");
                    }
                }
            }
            Assert.Greater(checkedBanks, 0);
        }
    }
}