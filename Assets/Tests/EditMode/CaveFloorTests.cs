using System.Collections.Generic;
using Chopper.World.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class CaveFloorTests
    {
        private static List<CryptBox> Room()
        {
            return new List<CryptBox>
            {
                new CryptBox { Center = new Vector3(0f, -320.5f, 0f), Size = new Vector3(16f, 1f, 16f), Rotation = Quaternion.identity, Material = CryptMaterial.Floor },
                new CryptBox { Center = new Vector3(6f, -319.8f, 6f), Size = new Vector3(1.1f, 0.4f, 0.8f), Rotation = Quaternion.identity, Material = CryptMaterial.Floor },
            };
        }

        [Test]
        public void TheVoxelGridFollowsTheMainWalkingFloorNotTheHighestBump()
        {
            Assert.AreEqual(-320f, CaveVoxel.MainFloorTop(Room()), 0.001f);
        }

        [Test]
        public void TheVisibleCaveFloorIsLevelWithTheColliderFloor()
        {
            Mesh mesh = CaveVoxel.Build(Room(), out Mesh crystals, false);
            Assert.IsNotNull(mesh);
            float top = float.NegativeInfinity;
            foreach (Vector3 v in mesh.vertices)
                if (Mathf.Abs(v.x) < 1f && Mathf.Abs(v.z) < 1f && v.y < -319f) top = Mathf.Max(top, v.y);
            Assert.AreEqual(-320f, top, 0.01f);
            Object.DestroyImmediate(mesh);
            if (crystals != null) Object.DestroyImmediate(crystals);
        }

        [Test]
        public void MineReliefSitsOnTheHalfMetreGrid()
        {
            var boxes = new List<CryptBox>();
            CaveShell.FloorRelief(boxes, 0, Vector3.zero, 8f, 7, 2f, 2f, null);
            Assert.Greater(boxes.Count, 0);
            foreach (CryptBox b in boxes)
            {
                float top = b.Center.y + b.Size.y * 0.5f;
                Assert.AreEqual(0f, Mathf.Repeat(top + 0.001f, 0.5f), 0.01f);
                Assert.AreEqual(0f, Mathf.Repeat(b.Center.x - b.Size.x * 0.5f + 0.001f, 0.5f), 0.01f);
                Assert.AreEqual(0f, Mathf.Repeat(b.Center.z - b.Size.z * 0.5f + 0.001f, 0.5f), 0.01f);
            }
        }

        [Test]
        public void VisibleReliefEdgesMatchTheColliderInAnOffGridRoom()
        {
            var c = new Vector3(87.75f, -320f, 133.3f);
            var boxes = new List<CryptBox>
            {
                new CryptBox { Center = c + new Vector3(0f, -0.5f, 0f), Size = new Vector3(17.6f, 1f, 17.6f), Rotation = Quaternion.identity, Material = CryptMaterial.Floor },
                new CryptBox { Center = c + new Vector3(5.5f, 0.5f, 5.5f), Size = new Vector3(1f, 1f, 1f), Rotation = Quaternion.identity, Material = CryptMaterial.Floor },
            };
            Mesh mesh = CaveVoxel.Build(boxes, out Mesh crystals, false);
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (Vector3 v in mesh.vertices)
            {
                if (v.y < c.y + 0.9f || Mathf.Abs(v.x - c.x) > 7f || Mathf.Abs(v.z - c.z) > 7f) continue;
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z);
            }
            Assert.AreEqual(c.x + 5f, minX, 0.01f);
            Assert.AreEqual(c.x + 6f, maxX, 0.01f);
            Assert.AreEqual(c.z + 5f, minZ, 0.01f);
            Assert.AreEqual(c.z + 6f, maxZ, 0.01f);
            Object.DestroyImmediate(mesh);
            if (crystals != null) Object.DestroyImmediate(crystals);
        }

        [Test]
        public void CrystalsNeverGrowOnTheWalkingFloor()
        {
            var boxes = new List<CryptBox>
            {
                new CryptBox { Center = new Vector3(0f, -320.5f, 0f), Size = new Vector3(30f, 1f, 30f), Rotation = Quaternion.identity, Material = CryptMaterial.Floor },
                new CryptBox { Center = new Vector3(15.5f, -317f, 0f), Size = new Vector3(1f, 6f, 30f), Rotation = Quaternion.identity, Material = CryptMaterial.Wall },
                new CryptBox { Center = new Vector3(-15.5f, -317f, 0f), Size = new Vector3(1f, 6f, 30f), Rotation = Quaternion.identity, Material = CryptMaterial.Wall },
            };
            Mesh mesh = CaveVoxel.Build(boxes, out Mesh crystals, true);
            if (crystals != null)
                foreach (Vector3 v in crystals.vertices)
                    Assert.GreaterOrEqual(v.y, -319.51f);
            Object.DestroyImmediate(mesh);
            if (crystals != null) Object.DestroyImmediate(crystals);
        }
    }
}
