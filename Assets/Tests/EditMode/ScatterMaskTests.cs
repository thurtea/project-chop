using System.Collections.Generic;
using Chopper.World;
using Chopper.World.Generation;
using Chopper.World.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class ScatterMaskTests
    {
        [TearDown]
        public void Reset() => ScatterMask.ClearAll();

        [Test]
        public void PlacedPieceBlocksScatterInsideAndAtMargin()
        {
            var floor = new Bounds(new Vector3(40f, 5.1f, 40f), new Vector3(4f, 0.2f, 4f));
            ScatterMask.Set("floor", floor);
            Assert.IsTrue(ScatterMask.Blocked(new Vector3(40f, 4.96f, 40f)));
            Assert.IsTrue(ScatterMask.Blocked(new Vector3(42.3f, 4.96f, 40f)));
            Assert.IsFalse(ScatterMask.Blocked(new Vector3(43f, 4.96f, 40f)));
            Assert.IsFalse(ScatterMask.Blocked(new Vector3(40f, 1f, 40f)));
            ScatterMask.Clear("floor");
            Assert.IsFalse(ScatterMask.Blocked(new Vector3(40f, 4.96f, 40f)));
        }

        [Test]
        public void RegrowingTreeNearPieceIsHeldBack()
        {
            ScatterMask.Set("wall", new Bounds(new Vector3(60f, 6f, 60f), new Vector3(4f, 2f, 0.3f)));
            Assert.IsTrue(ScatterMask.PieceNear(new Vector3(60f, 5.1f, 60.6f), 0.5f));
            Assert.IsFalse(ScatterMask.PieceNear(new Vector3(60f, 5.1f, 62f), 0.5f));
            Assert.IsFalse(ScatterMask.PieceNear(new Vector3(0f, 3f, 0f), 0.5f));
        }

        [Test]
        public void CullerHidesRocksUnderAPieceAndShowsThemAfter()
        {
            var root = new GameObject("Rocks");
            var rock = new GameObject("Rock");
            rock.transform.SetParent(root.transform);
            rock.transform.position = new Vector3(80f, 4f, 80f);
            var far = new GameObject("Far");
            far.transform.SetParent(root.transform);
            far.transform.position = new Vector3(90f, 4f, 80f);
            var culler = root.AddComponent<ScatterCuller>();
            ScatterMask.Changed += culler.OnMaskChanged;
            try
            {
                ScatterMask.Set("floor", new Bounds(new Vector3(80f, 4.2f, 80f), new Vector3(3f, 0.3f, 3f)));
                Assert.IsFalse(rock.activeSelf);
                Assert.IsTrue(far.activeSelf);
                ScatterMask.Clear("floor");
                Assert.IsTrue(rock.activeSelf);
            }
            finally
            {
                ScatterMask.Changed -= culler.OnMaskChanged;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ChangedFiresOnPlaceAndRemove()
        {
            int fired = 0;
            System.Action<Bounds> h = _ => fired++;
            ScatterMask.Changed += h;
            ScatterMask.Set("p", new Bounds(new Vector3(30f, 2f, 30f), Vector3.one));
            ScatterMask.Clear("p");
            ScatterMask.Changed -= h;
            Assert.AreEqual(2, fired);
        }

        [Test]
        public void HutFootprintIsAlwaysClear()
        {
            Assert.IsTrue(ScatterMask.Blocked(new Vector3(0f, 3f, 0f)));
            Assert.IsTrue(ScatterMask.Blocked(new Vector3(WorldGen.HutHalfWidth + 0.3f, 3f, 0f)));
            Assert.IsTrue(ScatterMask.Blocked(new Vector3(0f, 3f, -WorldGen.HutHalfDepth - 0.3f)));
            Assert.IsFalse(ScatterMask.Blocked(new Vector3(WorldGen.HutHalfWidth + 2f, 3f, 0f)));
        }

        [Test]
        public void GrassOnlyOnSoilBlocks()
        {
            Assert.IsTrue(ScatterMask.GrowsOn(BlockType.Grass));
            Assert.IsTrue(ScatterMask.GrowsOn(BlockType.Dirt));
            Assert.IsFalse(ScatterMask.GrowsOn(BlockType.Stone));
            Assert.IsFalse(ScatterMask.GrowsOn(BlockType.Path));
            Assert.IsFalse(ScatterMask.GrowsOn(BlockType.Sand));
            Assert.IsFalse(ScatterMask.GrowsOn(BlockType.Snow));
        }

        [Test]
        public void GeneratedScatterStaysOutOfStructures()
        {
            var gen = new WorldGen(12345, 90f);
            VoxelColumns previous = VoxelColumns.Current;
            VoxelColumns.Current = VoxelColumns.Build(gen);
            try
            {
                var spots = new List<Vector2>();
                spots.AddRange(gen.PlantSpots(WorldGen.PlantKind.Herb));
                spots.AddRange(gen.PlantSpots(WorldGen.PlantKind.Fern));
                spots.AddRange(gen.PlantSpots(WorldGen.PlantKind.Bush));
                Assert.Greater(spots.Count, 100);
                foreach (Vector2 p in spots)
                {
                    Assert.IsFalse(ScatterMask.InHutFootprint(p.x, p.y), $"scatter at {p} inside the hut footprint");
                    Assert.IsFalse(gen.OnPath(p.x, p.y, 0f), $"scatter at {p} on a path");
                }
            }
            finally { VoxelColumns.Current = previous; }
        }
    }
}
