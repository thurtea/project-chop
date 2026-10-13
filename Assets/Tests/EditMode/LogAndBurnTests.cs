using System.Collections.Generic;
using Chopper.Core;
using Chopper.Trees;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class LogRulesTests
    {
        [Test]
        public void FellingPaysAThirdAndTheLogPaysTheRestPlusSize()
        {
            Assert.AreEqual(0, LogRules.FellWood(0));
            Assert.AreEqual(0, LogRules.FellWood(2));
            Assert.AreEqual(1, LogRules.FellWood(3));
            Assert.AreEqual(4, LogRules.FellWood(12));
            Assert.AreEqual(0, LogRules.FellWood(-6));

            Assert.AreEqual(3 - 1 + (int)TreeSize.Small + 1, LogRules.LogWood(3, TreeSize.Small));
            Assert.AreEqual(6 - 2 + (int)TreeSize.Medium + 1, LogRules.LogWood(6, TreeSize.Medium));
            Assert.AreEqual(12 - 4 + (int)TreeSize.Large + 1, LogRules.LogWood(12, TreeSize.Large));
            Assert.AreEqual(2, LogRules.LogHits(TreeSize.Small));
            Assert.AreEqual(4, LogRules.LogHits(TreeSize.Large));
        }

        [Test]
        public void ChoppingTheWholeLogAddsUpToItsWood()
        {
            int wood = LogRules.LogWood(12, TreeSize.Medium);
            int points = LogRules.LogPoints(TreeSize.Medium);
            int sum = 0;
            int taken = 0;
            while (taken < points)
            {
                int next = System.Math.Min(points, taken + ChopRules.PointsPerHit);
                sum += LogRules.WoodBetween(wood, points, taken, next);
                taken = next;
            }
            Assert.AreEqual(wood, sum);
            Assert.AreEqual(0, LogRules.WoodBetween(wood, points, points, points));
        }
    }

    public class TreeRemainsTests
    {
        [Test]
        public void AStumpRegrowsAtDawnUnlessItWasCleared()
        {
            var remains = new TreeRemains();
            remains.AddLog(1, 20f);
            remains.AddLog(2, 40f);
            Assert.AreEqual(RemainsHit.Finished, remains.HitStump(2, LogRules.StumpPoints));
            Assert.AreEqual(LogRules.ClearedRegrowDawns, remains.Cleared[2]);
            Assert.IsTrue(remains.HasLog(2), "clearing the stump leaves the log");

            var regrow = new List<int>();
            remains.Dawn(new List<int> { 1, 2 }, regrow);

            CollectionAssert.AreEqual(new[] { 1 }, regrow);
            Assert.IsFalse(remains.HasLog(1));
            Assert.IsFalse(remains.IsCleared(1));
            Assert.IsTrue(remains.IsCleared(2));
            Assert.IsTrue(remains.HasLog(2));
        }

        [Test]
        public void AClearedSpotStaysBareForItsDawnsThenRegrows()
        {
            var remains = new TreeRemains();
            Assert.AreEqual(RemainsHit.Damaged, remains.HitStump(4, ChopRules.PointsPerHit));
            Assert.AreEqual(RemainsHit.Damaged, remains.HitStump(4, ChopRules.PointsPerHit));
            Assert.IsFalse(remains.IsCleared(4));
            Assert.AreEqual(RemainsHit.Finished, remains.HitStump(4, ChopRules.PointsPerHit));
            Assert.AreEqual(RemainsHit.Ignored, remains.HitStump(4, ChopRules.PointsPerHit));

            var felled = new List<int> { 4 };
            for (int dawn = 1; dawn < LogRules.ClearedRegrowDawns; dawn++)
            {
                var early = new List<int>();
                remains.Dawn(felled, early);
                Assert.AreEqual(0, early.Count, "dawn " + dawn);
                Assert.IsTrue(remains.IsCleared(4));
                Assert.AreEqual(LogRules.ClearedRegrowDawns - dawn, remains.Cleared[4]);
            }

            var last = new List<int>();
            remains.Dawn(felled, last);
            CollectionAssert.AreEqual(new[] { 4 }, last);
            Assert.IsFalse(remains.IsCleared(4));
        }

        [Test]
        public void ALogFinishesAfterItsHitsAndPacksForTheSave()
        {
            var remains = new TreeRemains();
            remains.AddLog(3, 90f);
            int hits = LogRules.LogHits(TreeSize.Small);
            RemainsHit last = RemainsHit.Ignored;
            for (int i = 0; i < hits; i++)
                last = remains.HitLog(3, TreeSize.Small, ChopRules.PointsPerHit, out _, out _);
            Assert.AreEqual(RemainsHit.Finished, last);
            Assert.IsFalse(remains.HasLog(3));

            int packed = TreeRemains.Pack(12, true, 90f, true, LogRules.ClearedRegrowDawns);
            Assert.Less(packed, 0);
            Assert.IsFalse(TreeRemains.TryUnpack(12, out _, out _, out _, out _, out _));
            Assert.IsTrue(TreeRemains.TryUnpack(packed, out int id, out bool hasLog, out float yaw, out bool cleared, out int dawns));
            Assert.AreEqual(12, id);
            Assert.IsTrue(hasLog);
            Assert.IsTrue(cleared);
            Assert.AreEqual(90f, yaw, 1f);
            Assert.AreEqual(LogRules.ClearedRegrowDawns, dawns);
        }
    }

    public class BurnRulesTests
    {
        [Test]
        public void BurnStartsOnTheTickWhileYouStandInTheFlame()
        {
            float elapsed = 0f;
            Assert.AreEqual(0, BurnRules.Tick(ref elapsed, BurnRules.TickSeconds * 0.5f, true));
            Assert.Greater(elapsed, 0f);
            Assert.AreEqual(BurnRules.DamagePerTick, BurnRules.Tick(ref elapsed, BurnRules.TickSeconds * 0.5f, true));
            Assert.Less(BurnRules.DamagePerTick, 40, "one tick is not a killing blow");
        }

        [Test]
        public void BurnStopsWhenYouLeaveAndDoesNotCarryAPartialTick()
        {
            float elapsed = 0f;
            BurnRules.Tick(ref elapsed, BurnRules.TickSeconds * 0.8f, true);
            Assert.AreEqual(0, BurnRules.Tick(ref elapsed, BurnRules.TickSeconds, false));
            Assert.AreEqual(0f, elapsed);
            Assert.AreEqual(0, BurnRules.Tick(ref elapsed, BurnRules.TickSeconds * 0.4f, true));
        }
    }
}
