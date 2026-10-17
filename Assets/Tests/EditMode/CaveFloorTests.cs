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
            }
        }
    }
}
