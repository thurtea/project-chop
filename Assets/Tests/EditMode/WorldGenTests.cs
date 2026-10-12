using System.Collections.Generic;
using Chopper.Core;
using Chopper.Trees;
using Chopper.World;
using Chopper.World.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class WorldGenTests
    {
        private const float HubRadius = 110f;
        // Fake model setup: one model per zone, each 10 m tall.
        private static readonly int[][] Species = { new[] { 0 }, new[] { 1 }, new[] { 2 }, new[] { 3 }, new[] { 4 } };
        private static readonly float[] ModelHeights = { 10f, 10f, 10f, 10f, 10f };

        [Test]
        public void SameSeedMakesTheSameWorld()
        {
            var a = new WorldGen(4242, HubRadius);
            var b = new WorldGen(4242, HubRadius);
            for (int i = -300; i <= 300; i += 37)
            {
                Assert.AreEqual(a.HeightAt(i, -i * 0.7f), b.HeightAt(i, -i * 0.7f), 1e-5f);
            }
            List<TreeEntry> treesA = a.GenerateTrees(Species, ModelHeights);
            List<TreeEntry> treesB = b.GenerateTrees(Species, ModelHeights);
            Assert.AreEqual(treesA.Count, treesB.Count);
            Assert.AreEqual(WorldGen.Checksum(treesA), WorldGen.Checksum(treesB));
        }

        [Test]
        public void DifferentSeedsMakeDifferentLand()
        {
            var a = new WorldGen(1, HubRadius);
            var b = new WorldGen(2, HubRadius);
            float difference = 0f;
            for (int i = -200; i <= 200; i += 20) difference += Mathf.Abs(a.HeightAt(i, 170f) - b.HeightAt(i, 170f));
            Assert.Greater(difference, 5f, "two seeds gave nearly the same hills");
            Assert.AreNotEqual(WorldGen.Checksum(a.GenerateTrees(Species, ModelHeights)), WorldGen.Checksum(b.GenerateTrees(Species, ModelHeights)));
        }

        [Test]
        public void TheHutMoundIsFlatInEveryWorld()
        {
            foreach (int seed in new[] { 1, 99, 1337, 777777 })
            {
                var gen = new WorldGen(seed, HubRadius);
                // The house terrace (W8-02) sits on the mound: spots outside the house stand on it.
                Assert.AreEqual(WorldGen.MoundHeight, gen.HeightAt(0f, 0f), 0.01f);
                Assert.AreEqual(WorldGen.MoundHeight + WorldGen.TerraceRaise(4.6f, 6.6f), gen.HeightAt(4.6f, 6.6f), 0.01f, "the axe table spot");
                Assert.AreEqual(WorldGen.MoundHeight + WorldGen.TerraceRaise(-4.6f, 6.6f), gen.HeightAt(-4.6f, 6.6f), 0.01f, "the notice board spot");
            }
        }

        [Test]
        public void ForestsHaveWideDeepLakesAndEveryShoreCanBeFished()
        {
            foreach (int seed in new[] { 1, 2024, 5150, 777777 })
            {
                var gen = new WorldGen(seed, HubRadius);
                var lakes = new Dictionary<Element, int>();
                foreach (Element forest in new[] { Element.Fire, Element.Earth, Element.Wind }) lakes[forest] = 0;
                foreach (WaterBody body in gen.WaterBodies)
                {
                    if (!body.Deep) continue;
                    Element zone = ZoneMap.ElementAt(body.Center.x, body.Center.y, HubRadius);
                    Assert.IsTrue(lakes.ContainsKey(zone), $"seed {seed}: a deep lake outside the dry forests");
                    lakes[zone]++;
                    Assert.GreaterOrEqual(body.Radius, 32f, $"seed {seed}: {zone} lake is too small");
                    Assert.GreaterOrEqual(body.Depth, 4f, $"seed {seed}: {zone} lake is too shallow");
                    Assert.Greater(body.SurfaceRadius, 14f);
                    Assert.Less(body.SurfaceRadius, body.Radius, "the water sits inside the bowl");
                    Assert.Less(gen.HeightAt(body.Center.x, body.Center.y), WorldGen.WaterHeight - 3f, $"seed {seed}: {zone} lake floor is not under the water");
                    Vector2 outward = body.Center.sqrMagnitude > 1f ? body.Center.normalized : Vector2.right;
                    Vector2 bank = body.Center + outward * (body.SurfaceRadius + 2f);
                    Assert.Greater(gen.HeightAt(bank.x, bank.y), WorldGen.WaterHeight + 0.25f, $"seed {seed}: {zone} bank is under water");
                    Assert.IsTrue(gen.TryFishingSpot(bank, out FishWater water, out _, out _), $"seed {seed}: {zone} bank is not a fishing spot");
                    Assert.AreEqual(FishingRules.FromElement(zone), water);
                    Vector2 ramp = body.Center + outward * (body.Radius * 0.75f);
                    Assert.Less(gen.SlopeAt(ramp.x, ramp.y, 1f), 1f, $"seed {seed}: {zone} shore is steeper than 45 degrees");
                }
                foreach (Element forest in lakes.Keys)
                    Assert.AreEqual(1, lakes[forest], $"seed {seed}: {forest} should have a deep lake, has {lakes[forest]}");
            }

            var pondWorld = new WorldGen(4242, HubRadius);
            Vector2 pondBank = pondWorld.PondCenter + Vector2.right * (WorldGen.PondOuterRadius + 1.5f);
            Assert.IsTrue(pondWorld.TryFishingSpot(pondBank, out FishWater pond, out _, out _));
            Assert.AreEqual(FishWater.Pond, pond);
            Vector2 beach = Vector2.up * (WorldGen.SeaInnerRadius - 6f);
            Assert.IsTrue(pondWorld.TryFishingSpot(beach, out FishWater sea, out _, out _), "the north beach is a fishing spot");
            Assert.AreEqual(FishWater.Sea, sea);
        }

        [Test]
        public void BackdropPeaksStandInTheSea()
        {
            var gen = new WorldGen(31, HubRadius);
            for (int i = 0; i < WorldGen.BackdropPeaks; i++)
            {
                WorldGen.BackdropCenter(i, out float x, out float z);
                float radius = Mathf.Sqrt(x * x + z * z);
                Assert.Greater(radius, WorldGen.BeachEndRadius, $"backdrop peak {i} is still on the island");
                Assert.Greater(gen.HeightAt(x, z), WorldGen.SnowLine, $"backdrop peak {i} has no snow cap");
                Assert.Greater(gen.BackdropWeight(x, z), 0.9f, $"backdrop peak {i} is not at its own center");
            }
        }

        [Test]
        public void TheEdgeOfTheWorldIsUnderTheSea()
        {
            var gen = new WorldGen(31, HubRadius);
            for (int angle = 0; angle < 360; angle += 30)
            {
                float radians = angle * Mathf.Deg2Rad;
                float height = gen.HeightAt(Mathf.Cos(radians) * 305f, Mathf.Sin(radians) * 305f);
                Assert.Less(height, WorldGen.WaterHeight, $"land above the sea at {angle} degrees");
            }
        }

        [Test]
        public void TreesFillTheForestsAndStayOutOfWaterAndOffPaths()
        {
            var gen = new WorldGen(2024, HubRadius);
            List<TreeEntry> trees = gen.GenerateTrees(Species, ModelHeights);
            Assert.Greater(trees.Count, 1300, "far fewer trees than the zone quotas");
            int[] perZone = new int[5];
            foreach (TreeEntry tree in trees)
            {
                var p = new Vector2(tree.Position.x, tree.Position.z);
                Assert.IsFalse(gen.InWater(p, 0.85f), "a tree stands in water");
                Assert.IsFalse(gen.OnPath(p.x, p.y, 4f), "a tree stands on a path");
                Assert.AreEqual(ZoneMap.ElementAt(p.x, p.y, HubRadius), tree.Zone);
                perZone[(int)tree.Zone]++;
            }
            foreach (Element zone in new[] { Element.Neutral, Element.Water, Element.Fire, Element.Earth, Element.Wind })
            {
                Assert.Greater(perZone[(int)zone], 50, $"{zone} has hardly any trees");
            }
        }

        [Test]
        public void PlayersWakeUpOnDryGround()
        {
            var gen = new WorldGen(5150, HubRadius);
            List<SpawnSpot> spots = gen.GenerateSpawns();
            Assert.GreaterOrEqual(spots.Count, 8);
            foreach (SpawnSpot spot in spots)
            {
                Assert.Greater(spot.Position.y, WorldGen.WaterHeight);
                Assert.IsFalse(gen.InWater(new Vector2(spot.Position.x, spot.Position.z), 1f));
            }
        }

        [Test]
        public void ZoneGatewaysStayOnTheirPathsAndClearOfTheHub()
        {
            foreach (int seed in new[] { 1, 5150, 777777 })
            {
                var gen = new WorldGen(seed, HubRadius);
                foreach (Element forest in WorldGen.Forests)
                {
                    Vector2 gate = gen.ZoneGatePosition(forest);
                    Assert.AreEqual(forest, ZoneMap.ElementAt(gate.x, gate.y, HubRadius), $"{forest} gate left its forest");
                    Assert.Less(gen.DistanceToPath(gate.x, gate.y), 0.01f, $"{forest} gate left its path");
                    Assert.IsTrue(gen.NearZoneGate(gate, 0f), $"{forest} gate did not clear itself");
                    Assert.Greater(gate.magnitude, HubRadius, $"{forest} gate is still in the hub");
                }
            }
        }

        // Meters of ground steeper than 45 degrees met while walking from start along direction for length meters.
        // Block world (W5-08 step 9): walking from the foot (start + direction * length) up to start one 1 m column at a
        // time, the tallest single rise between neighbouring block tops. Above 3.05 m (double jump plus the step) the
        // line cannot be climbed.
        private static float TallestBlockRise(WorldGen gen, Vector2 start, Vector2 direction, float length)
        {
            float Top(Vector2 p) => Mathf.Round(gen.HeightAt(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f) / 0.5f) * 0.5f;
            float tallest = 0f;
            float previous = Top(start + direction * length);
            for (float t = length - 1f; t >= 0f; t -= 1f)
            {
                float top = Top(start + direction * t);
                tallest = Mathf.Max(tallest, top - previous);
                previous = top;
            }
            return tallest;
        }

        [Test]
        public void MountainsRingTheHubWithAPassOnEveryPath()
        {
            foreach (int seed in new[] { 1, 5150, 777777 })
            {
                var gen = new WorldGen(seed, HubRadius);
                foreach (Element forest in WorldGen.Forests)
                {
                    Vector2 pass = gen.ZoneGatePosition(forest);
                    float floor = gen.HeightAt(pass.x, pass.y);
                    Assert.Less(gen.SlopeAt(pass.x, pass.y, 1f), 0.6f, $"{forest} pass floor is too steep to walk");
                    foreach (float turn in new[] { -20f, 20f })
                    {
                        Vector2 crest = (Vector2)(Quaternion.Euler(0f, 0f, turn) * (Vector3)ZoneMap.Axis(forest)) * gen.RingRadius;
                        // The ring is 20 m since W5-08 step 9; beside a pass it stands at least two 4.5 m risers over the
                        // floor (12.8 m on one seed). That it cannot be climbed is the block rise below and ZoneSeal.
                        Assert.Greater(gen.HeightAt(crest.x, crest.y) - floor, 2f * WorldGen.MountainStep, $"{forest} ring is too low beside its pass");
                        Vector2 inward = -crest.normalized;
                        Assert.Greater(TallestBlockRise(gen, crest, inward, WorldGen.RingFootHalfWidth + 4f), 3.05f, $"{forest} ring can be climbed from the hub");
                    }
                }
            }
        }

        [Test]
        public void HighRidgesSplitTheForests()
        {
            foreach (int seed in new[] { 1, 5150, 777777 })
            {
                var gen = new WorldGen(seed, HubRadius);
                foreach (Vector2 diagonal in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) })
                {
                    Vector2 along = diagonal.normalized;
                    Vector2 across = new Vector2(along.y, -along.x);
                    foreach (float r in new[] { 170f, 230f, 300f })
                    {
                        Vector2 crest = along * r;
                        float top = gen.HeightAt(crest.x, crest.y);
                        Assert.Greater(top, WorldGen.WaterHeight + 15f, $"ridge {diagonal} at {r} m does not stand out of the sea");
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Vector2 below = crest + across * side * (WorldGen.RidgeFootHalfWidth + 15f);
                            // Ridges are 28 m since W5-08 step 9 (concept 2.0 stacked blocks, not 42 m walls).
                            Assert.Greater(top - gen.HeightAt(below.x, below.y), 15f, $"ridge {diagonal} at {r} m is too low");
                            Assert.Greater(TallestBlockRise(gen, crest, across * side, WorldGen.RidgeFootHalfWidth + 4f), 3.05f, $"ridge {diagonal} at {r} m can be climbed");
                        }
                    }
                }
            }
        }

        // Walking out from the hub, the ring wall should climb in cliffs and ledges, not as one smooth bank.
        [Test]
        public void TheRingClimbsInCliffsAndLedges()
        {
            int stepped = 0;
            int walks = 0;
            foreach (int seed in new[] { 1, 5150, 777777 })
            {
                var gen = new WorldGen(seed, HubRadius);
                foreach (float degrees in new[] { 25f, 65f, 115f, 155f, 205f, 245f, 295f, 335f })
                {
                    Vector2 outward = (Vector2)(Quaternion.Euler(0f, 0f, degrees) * Vector3.right);
                    float start = gen.RingRadius - WorldGen.RingFootHalfWidth;
                    const float step = 0.5f;
                    int phase = 0;
                    float previous = gen.HeightAt(outward.x * start, outward.y * start);
                    for (float d = start + step; d <= gen.RingRadius; d += step)
                    {
                        float height = gen.HeightAt(outward.x * d, outward.y * d);
                        float slope = (height - previous) / step;
                        previous = height;
                        if (phase % 2 == 0 && slope > 1.4f) phase++;
                        else if (phase % 2 == 1 && slope < 0.9f) phase++;
                    }
                    walks++;
                    // Cliff, ledge, cliff: at least three changes of steepness on the way up.
                    if (phase >= 3) stepped++;
                }
            }
            Assert.GreaterOrEqual(stepped, walks * 3 / 4, $"only {stepped} of {walks} walks up the ring met a ledge between cliffs");
        }

        [Test]
        public void TypedSeedsAreStable()
        {
            Assert.AreEqual(12345, WorldSession.ParseSeed("12345"));
            Assert.AreEqual(WorldSession.ParseSeed("chopper"), WorldSession.ParseSeed(" chopper "));
            Assert.AreNotEqual(WorldSession.ParseSeed("chopper"), WorldSession.ParseSeed("choppers"));
        }

        [Test]
        public void EveryWorldCutsOneMineIntoEachForest()
        {
            int[] seeds = { 1, 7, 2026, 1337, 4242, 99991 };
            foreach (int seed in seeds)
            {
                var gen = new WorldGen(seed, HubRadius);
                Assert.AreEqual(4, gen.Mines.Length, "seed " + seed);
                var seen = new bool[5];
                foreach (MineSite site in gen.Mines)
                {
                    Assert.IsFalse(seen[(int)site.Forest], seed + " " + site.Forest);
                    seen[(int)site.Forest] = true;
                    Assert.AreNotEqual(Element.Neutral, site.Forest);
                }
            }
        }
    }
}
