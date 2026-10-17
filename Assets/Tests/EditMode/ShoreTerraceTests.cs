using Chopper.World.Generation;
using Chopper.World.Voxel;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class ShoreTerraceTests
    {
        [Test]
        public void FarFromWaterLevelIsUntouched()
        {
            Assert.AreEqual(5f, VoxelColumns.TerraceShape(5f, 0.2f));
            Assert.AreEqual(-4f, VoxelColumns.TerraceShape(-4f, 0.2f));
        }

        [Test]
        public void DryBankStaysDryAndWetStaysWet()
        {
            for (float h = WorldGen.WaterHeight; h < WorldGen.WaterHeight + 3f; h += 0.05f)
                for (float w = -0.4f; w <= 0.4f; w += 0.1f)
                    Assert.GreaterOrEqual(VoxelColumns.TerraceShape(h, w), WorldGen.WaterHeight);
            for (float h = WorldGen.WaterHeight - 1f; h < WorldGen.WaterHeight - 0.01f; h += 0.05f)
                Assert.Less(VoxelColumns.TerraceShape(h, 0.1f), WorldGen.WaterHeight);
        }

        [Test]
        public void BankFormsFlatLedges()
        {
            float a = VoxelColumns.TerraceShape(WorldGen.WaterHeight + 1.1f, 0f);
            float b = VoxelColumns.TerraceShape(WorldGen.WaterHeight + 1.4f, 0f);
            Assert.Less(b - a, 0.1f);
        }
    }
}