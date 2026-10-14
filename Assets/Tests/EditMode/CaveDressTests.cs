using Chopper.World.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class CaveDressTests
    {
        private const float HubRadius = 110f;

        [Test]
        public void DressingIsSeededAndLeavesTheWalkingCrossClear()
        {
            foreach (int seed in new[] { 1337, 4242, 7 })
            {
                var gen = new WorldGen(seed, HubRadius);
                for (int i = 0; i < gen.Crypts.Length; i++)
                {
                    var a = new CryptPlan(gen, i);
                    var b = new CryptPlan(new WorldGen(seed, HubRadius), i);
                    Assert.AreEqual(a.Dressing.Count, b.Dressing.Count, "crypt dressing is deterministic");
                    Assert.Greater(a.Dressing.Count, 0);
                    AssertCryptClear(a);
                    Assert.AreEqual(a.Chests.Count, b.Chests.Count);
                    for (int c = 0; c < a.Chests.Count; c++) Assert.AreEqual(a.Chests[c].Position, b.Chests[c].Position);
                }
                for (int i = 0; i < gen.Mines.Length; i++)
                {
                    var a = new MinePlan(gen, i);
                    var b = new MinePlan(new WorldGen(seed, HubRadius), i);
                    Assert.AreEqual(a.Dressing.Count, b.Dressing.Count, "mine dressing is deterministic");
                    Assert.AreEqual(a.Veins.Count, b.Veins.Count);
                    for (int v = 0; v < a.Veins.Count; v++) Assert.AreEqual(a.Veins[v].Position, b.Veins[v].Position);
                    Assert.AreEqual(a.Links.Count, a.Rails.Count);
                    foreach (MineProp rail in a.Rails) Assert.AreEqual(6.4f, rail.Length);
                    Assert.AreEqual(0, a.Carts.Count, "the cart is a corner mesh, not the old centre prop");
                    AssertMineClear(a);
                }
            }
        }

        private static void AssertCryptClear(CryptPlan plan)
        {
            int arches = 0;
            int panels = 0;
            int floors = 0;
            foreach (CavePiece piece in plan.Dressing)
            {
                if (piece.Name == CaveDress.CryptArch) arches++;
                if (piece.Name.StartsWith("prop_crypt_wall_") && piece.Name != CaveDress.CryptTrim) panels++;
                if (piece.Name.StartsWith("prop_crypt_floor_")) floors++;
                if (!piece.Solid) continue;
                for (int room = 0; room < plan.Rooms.Count; room++)
                {
                    Vector3 local = piece.Position - plan.RoomCenter(room);
                    if (Mathf.Abs(local.y) > 3f) continue;
                    if (Mathf.Abs(local.x) > CryptPlan.RoomSize || Mathf.Abs(local.z) > CryptPlan.RoomSize) continue;
                    Assert.IsFalse(CaveDress.BlocksWalkingCross(local, 2.5f), piece.Name + " in crypt room " + room);
                }
                Vector3 arena = piece.Position - plan.ArenaCenter;
                if (Mathf.Abs(arena.y) > 3f) continue;
                Assert.IsFalse(CaveDress.BlocksWalkingCross(arena, 2.5f), piece.Name + " in the arena");
                if (piece.Name == CaveDress.ArenaPillar || piece.Name == CaveDress.CryptRubble)
                    Assert.Greater(new Vector2(arena.x, arena.z).magnitude, 9f, piece.Name);
            }
            Assert.Greater(arches, 0);
            Assert.Greater(panels, plan.Rooms.Count * 8, "wall spans are covered, not one decal");
            Assert.Greater(floors, plan.Rooms.Count * 16, "the floor is tiled, not one slab");
        }

        private static void AssertMineClear(MinePlan plan)
        {
            int rails = 0;
            int carts = 0;
            foreach (CavePiece piece in plan.Dressing)
            {
                if (piece.Name == CaveDress.Rail) rails++;
                if (piece.Name == CaveDress.MineCart) carts++;
                if (!piece.Solid) continue;
                for (int room = 0; room < plan.Rooms.Count; room++)
                {
                    Vector3 local = piece.Position - plan.RoomCenter(room);
                    if (Mathf.Abs(local.y) > 3f) continue;
                    if (Mathf.Abs(local.x) > MinePlan.RoomSize || Mathf.Abs(local.z) > MinePlan.RoomSize) continue;
                    Assert.IsFalse(CaveDress.BlocksWalkingCross(local, 2.75f), piece.Name + " in mine room " + room);
                    if (piece.Name == CaveDress.MineCart)
                        Assert.Greater(Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.z)), 5f, "cart stays off the gallery line");
                }
            }
            Assert.AreEqual(plan.Links.Count, rails);
            Assert.AreEqual(1, carts);
        }
    }
}
