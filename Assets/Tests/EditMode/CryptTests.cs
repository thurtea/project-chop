using System.Collections.Generic;
using Chopper.Core;
using Chopper.Trees;
using Chopper.World;
using Chopper.World.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class CryptTests
    {
        private const float HubRadius = 110f;
        private static readonly int[] Seeds = { 1, 42, 1337, 777777, -5, 90210 };
        private static readonly int[][] Species = { new[] { 0 }, new[] { 1 }, new[] { 2 }, new[] { 3 }, new[] { 4 } };
        private static readonly float[] ModelHeights = { 10f, 10f, 10f, 10f, 10f };

        // ---------- Falling trees ----------

        [Test]
        public void AFallingTreeKillsWithoutSturdyGear()
        {
            Assert.GreaterOrEqual(CrushRules.Damage(100, 0), 100);
            Assert.IsFalse(CrushRules.SurvivesFromFull(100, 0));
        }

        [Test]
        public void EverySturdyLevelLetsThePlayerSurviveAndHurtsLess()
        {
            int previous = CrushRules.Damage(100, 0);
            for (int level = 1; level <= CrushRules.MaxSturdyLevel; level++)
            {
                int damage = CrushRules.Damage(100, level);
                Assert.IsTrue(CrushRules.SurvivesFromFull(100, level), $"level {level}");
                Assert.Less(damage, previous, $"level {level} should hurt less than level {level - 1}");
                previous = damage;
            }
            Assert.AreEqual(CrushRules.Damage(100, CrushRules.MaxSturdyLevel), CrushRules.Damage(100, 99), "levels past the table keep the best value");
        }

        // ---------- Where the crypts are ----------

        [Test]
        public void EveryWorldHasThreeCryptsInDifferentForests()
        {
            foreach (int seed in Seeds)
            {
                var gen = new WorldGen(seed, HubRadius);
                Assert.AreEqual(WorldGen.CryptCount, gen.Crypts.Length, $"seed {seed}");
                var zones = new HashSet<Element>();
                var tiers = new HashSet<int>();
                foreach (CryptSite site in gen.Crypts)
                {
                    Assert.AreNotEqual(Element.Neutral, site.Zone);
                    Assert.AreEqual(site.Zone, ZoneMap.ElementAt(site.Position.x, site.Position.y, HubRadius), $"seed {seed}: the crypt stands in its own forest");
                    Assert.IsFalse(gen.InWater(site.Position, 1.2f), $"seed {seed}: a crypt in a pool");
                    Assert.Greater(gen.HeightAt(site.Position.x, site.Position.y), WorldGen.WaterHeight + 0.9f, $"seed {seed}: a crypt below the water line");
                    Assert.Less(site.Position.magnitude, WorldGen.BeachStartRadius, $"seed {seed}: a crypt on the beach");
                    zones.Add(site.Zone);
                    tiers.Add(site.Tier);
                }
                Assert.AreEqual(WorldGen.CryptCount, zones.Count, $"seed {seed}: two crypts share a forest");
                CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, tiers, $"seed {seed}");
            }
        }

        [Test]
        public void NothingGrowsOnACryptEntrance()
        {
            foreach (int seed in Seeds)
            {
                var gen = new WorldGen(seed, HubRadius);
                foreach (TreeEntry tree in gen.GenerateTrees(Species, ModelHeights))
                {
                    Assert.IsFalse(gen.NearCrypt(new Vector2(tree.Position.x, tree.Position.z), 0f), $"seed {seed}: a tree on a crypt");
                }
                foreach (RockSpot rock in gen.GenerateRocks(3)) Assert.IsFalse(gen.NearCrypt(rock.Position, 0f), $"seed {seed}: a rock on a crypt");
                foreach (SpawnSpot spot in gen.GenerateSpawns())
                {
                    Assert.IsFalse(gen.NearCrypt(new Vector2(spot.Position.x, spot.Position.z), 0f), $"seed {seed}: a wake up spot on a crypt");
                }
            }
        }

        // ---------- Inside a crypt ----------

        private static bool InsideAnyBox(CryptPlan plan, Vector3 point)
        {
            foreach (CryptBox box in plan.Boxes)
            {
                Vector3 d = point - box.Center;
                Vector3 h = box.Size * 0.5f;
                if (Mathf.Abs(d.x) < h.x && Mathf.Abs(d.y) < h.y && Mathf.Abs(d.z) < h.z) return true;
            }
            return false;
        }

        private static bool InsideARoom(CryptPlan plan, Vector3 point)
        {
            for (int room = 0; room < plan.Rooms.Count; room++)
            {
                Vector3 d = point - plan.RoomCenter(room);
                float half = CryptPlan.RoomSize * 0.5f;
                if (Mathf.Abs(d.x) < half && Mathf.Abs(d.z) < half && d.y > -0.1f && d.y < CryptPlan.RoomHeight) return true;
            }
            return false;
        }

        [Test]
        public void TheSameSeedBuildsTheSameCrypt()
        {
            var a = new CryptPlan(new WorldGen(321, HubRadius), 1);
            var b = new CryptPlan(new WorldGen(321, HubRadius), 1);
            Assert.AreEqual(a.Rooms, b.Rooms);
            Assert.AreEqual(a.Boxes.Count, b.Boxes.Count);
            Assert.AreEqual(a.Chests.Count, b.Chests.Count);
            for (int i = 0; i < a.Chests.Count; i++)
            {
                Assert.AreEqual(a.Chests[i].Position, b.Chests[i].Position);
                Assert.AreEqual(a.Chests[i].Iron, b.Chests[i].Iron);
            }
            Assert.AreEqual(a.Guardians.Count, b.Guardians.Count);
        }

        [Test]
        public void EveryCryptIsOneConnectedSetOfRoomsWithAVaultAtTheEnd()
        {
            foreach (int seed in Seeds)
            {
                var gen = new WorldGen(seed, HubRadius);
                for (int i = 0; i < gen.Crypts.Length; i++)
                {
                    var plan = new CryptPlan(gen, i);
                    Assert.AreEqual(3 + plan.Site.Tier, plan.Rooms.Count, $"seed {seed} crypt {i}: room count follows the tier");
                    Assert.AreEqual(plan.Rooms.Count - 1, plan.Links.Count, "the rooms form a tree");
                    foreach ((int a, int b) in plan.Links)
                    {
                        Vector2Int step = plan.Rooms[b] - plan.Rooms[a];
                        Assert.AreEqual(1, Mathf.Abs(step.x) + Mathf.Abs(step.y), "corridors join neighbouring rooms");
                    }
                    for (int room = 0; room < plan.Rooms.Count; room++) Assert.LessOrEqual(plan.Depths[room], plan.Depths[plan.Vault]);
                    Assert.AreNotEqual(0, plan.Vault, "the vault is not the room with the stairs");
                    Assert.IsTrue(plan.Chests.Exists(c => c.Grand), "there is a grand chest");
                    Assert.GreaterOrEqual(plan.Chests.Count, 2, "a grand chest and at least one more");
                    Assert.Greater(plan.Guardians.Count, 0);
                }
            }
        }

        [Test]
        public void PlayersAndChestsNeverStandInsideTheStone()
        {
            foreach (int seed in Seeds)
            {
                var gen = new WorldGen(seed, HubRadius);
                for (int i = 0; i < gen.Crypts.Length; i++)
                {
                    var plan = new CryptPlan(gen, i);
                    Vector3 body = Vector3.up * 1f;
                    Assert.IsFalse(InsideAnyBox(plan, plan.ArrivalSpot + body), $"seed {seed} crypt {i}: arrival inside stone");
                    Assert.IsFalse(InsideAnyBox(plan, plan.ExitSpot + body), $"seed {seed} crypt {i}: the way out is inside stone");
                    Assert.IsTrue(InsideARoom(plan, plan.ArrivalSpot), "the player arrives inside the first room");
                    Assert.IsTrue(CryptWorld.IsUnderground(plan.ArrivalSpot), "the rooms count as underground");
                    Assert.IsFalse(CryptWorld.IsUnderground(plan.ReturnSpot), "climbing out returns to the surface");
                    foreach (CryptChest chest in plan.Chests)
                    {
                        Assert.IsFalse(InsideAnyBox(plan, chest.Position + Vector3.up * 0.4f), $"seed {seed} crypt {i}: a chest inside stone");
                        Assert.IsTrue(InsideARoom(plan, chest.Position), "chests stand in rooms");
                    }
                    foreach (CryptGuardian guardian in plan.Guardians)
                    {
                        Assert.IsFalse(InsideAnyBox(plan, guardian.Position + body), $"seed {seed} crypt {i}: a guardian inside stone");
                        Assert.IsTrue(InsideARoom(plan, guardian.Position), "guardians stand in rooms");
                    }
                }
            }
        }

        [Test]
        public void TheArenaHasRoomForTheFightAndAWayBack()
        {
            foreach (int seed in Seeds)
            {
                var gen = new WorldGen(seed, HubRadius);
                for (int i = 0; i < gen.Crypts.Length; i++)
                {
                    var plan = new CryptPlan(gen, i);
                    Vector3 body = Vector3.up * 1f;
                    Assert.IsFalse(InsideAnyBox(plan, plan.ArenaDoorSpot + body), $"seed {seed} crypt {i}: the vault archway spot is inside stone");
                    Assert.IsTrue(InsideARoom(plan, plan.ArenaDoorSpot), "the archway is reached from inside the vault");
                    Assert.IsFalse(InsideAnyBox(plan, plan.VaultReturnSpot + body), "coming back up lands inside stone");
                    foreach (Vector3 spot in new[] { plan.ArenaEntry, plan.ArenaExitSpot, plan.BossSpot })
                    {
                        Assert.IsFalse(InsideAnyBox(plan, spot + body), $"seed {seed} crypt {i}: an arena spot is inside stone");
                        Assert.IsTrue(plan.InArena(spot), "arena spots are in the arena");
                    }
                    Assert.AreEqual(8, plan.ArenaSpawns.Count);
                    foreach (Vector3 spawn in plan.ArenaSpawns)
                    {
                        Assert.IsFalse(InsideAnyBox(plan, spawn + body), $"seed {seed} crypt {i}: a wave spawns inside a pillar");
                        Assert.IsTrue(plan.InArena(spawn));
                    }
                    Assert.IsTrue(CryptWorld.IsUnderground(plan.ArenaCenter));
                    Assert.IsFalse(plan.InArena(plan.ArrivalSpot), "the crypt rooms are not the arena");
                }
            }
        }

        [Test]
        public void EssenceForEveryElementPacksIntoOneNumber()
        {
            int packed = 0;
            packed = EssenceRules.Set(packed, Element.Water, 3);
            packed = EssenceRules.Set(packed, Element.Fire, 7);
            packed = EssenceRules.Set(packed, Element.Earth, 1);
            packed = EssenceRules.Set(packed, Element.Wind, 200);
            Assert.AreEqual(3, EssenceRules.Get(packed, Element.Water));
            Assert.AreEqual(7, EssenceRules.Get(packed, Element.Fire));
            Assert.AreEqual(1, EssenceRules.Get(packed, Element.Earth));
            Assert.AreEqual(200, EssenceRules.Get(packed, Element.Wind));
            Assert.AreEqual(0, EssenceRules.Get(packed, Element.Neutral));
            packed = EssenceRules.Set(packed, Element.Fire, 0);
            Assert.AreEqual(0, EssenceRules.Get(packed, Element.Fire));
            Assert.AreEqual(200, EssenceRules.Get(packed, Element.Wind), "changing one element leaves the others alone");
            Assert.AreEqual(255, EssenceRules.Get(EssenceRules.Set(0, Element.Water, 999), Element.Water), "counts cap at 255");
        }

        [Test]
        public void TheCryptsHoldEnoughForTheirUpgrades()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            int ironNeeded = 0;
            foreach (int cost in config.SturdyIronCosts) ironNeeded += cost;
            int shardsNeeded = 0;
            foreach (int cost in config.RuneShardCosts) shardsNeeded += cost;
            foreach (int seed in Seeds)
            {
                var gen = new WorldGen(seed, HubRadius);
                int iron = 0;
                int shards = 0;
                for (int i = 0; i < gen.Crypts.Length; i++)
                {
                    foreach (CryptChest chest in new CryptPlan(gen, i).Chests)
                    {
                        iron += chest.Iron;
                        shards += chest.Shards;
                    }
                }
                Assert.GreaterOrEqual(iron, ironNeeded, $"seed {seed}: not enough crypt iron for full Sturdy gear");
                Assert.GreaterOrEqual(shards, shardsNeeded, $"seed {seed}: not enough rune shards for the Rune axe");
            }
            Object.DestroyImmediate(config);
        }
    }
}
