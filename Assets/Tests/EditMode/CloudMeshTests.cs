using Chopper.World;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class CloudMeshTests
    {
        [Test]
        public void CloudsArePuffyLightTopAndCheap()
        {
            for (int i = 0; i < 8; i++)
            {
                Mesh mesh = BlockSky.CloudMesh(i);
                Assert.Greater(mesh.vertexCount, 200);
                Assert.Less(mesh.triangles.Length / 3, 20000);
                Vector3[] v = mesh.vertices;
                Color[] c = mesh.colors;
                float topSum = 0f, lowSum = 0f; int topN = 0, lowN = 0;
                float mid = mesh.bounds.center.y;
                for (int k = 0; k < v.Length; k++)
                {
                    if (v[k].y > mid + 2f) { topSum += c[k].grayscale; topN++; }
                    else if (v[k].y < mid - 2f) { lowSum += c[k].grayscale; lowN++; }
                }
                Assert.Greater(topSum / Mathf.Max(1, topN), lowSum / Mathf.Max(1, lowN));
                Assert.Greater(mesh.bounds.size.x, mesh.bounds.size.y);
            }
        }
    }
}