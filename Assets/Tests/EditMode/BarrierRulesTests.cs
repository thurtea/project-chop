using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests
{
    public class BarrierRulesTests
    {
        [Test]
        public void FeedingFillsUpToTheMaximum()
        {
            Assert.AreEqual(15f, BarrierRules.Feed(0f, 15f, 60f), 0.001f);
            Assert.AreEqual(60f, BarrierRules.Feed(50f, 15f, 60f), 0.001f);
            Assert.AreEqual(20f, BarrierRules.Feed(20f, -5f, 60f), 0.001f);
        }

        [Test]
        public void EachNightBurnsSomeAndItNeverGoesBelowZero()
        {
            Assert.AreEqual(20f, BarrierRules.Burn(30f, 10f), 0.001f);
            Assert.AreEqual(0f, BarrierRules.Burn(5f, 10f), 0.001f);
            Assert.AreEqual(3, BarrierRules.Nights(30f, 10f));
        }

        [Test]
        public void AnUpBarrierAbsorbsHitsUntilItIsSpent()
        {
            float charge = 4f;
            Assert.IsTrue(BarrierRules.Absorb(ref charge, 3f));
            Assert.AreEqual(1f, charge, 0.001f);
            Assert.IsTrue(BarrierRules.Absorb(ref charge, 3f));
            Assert.AreEqual(0f, charge, 0.001f);
            Assert.IsFalse(BarrierRules.Absorb(ref charge, 3f));
            Assert.IsFalse(BarrierRules.IsUp(charge));
        }

        [Test]
        public void ItCoversTheGroundAroundTheHeart()
        {
            Assert.IsTrue(BarrierRules.Covers(Vector3.zero, new Vector3(10f, 2f, 0f), 14f));
            Assert.IsFalse(BarrierRules.Covers(Vector3.zero, new Vector3(15f, 0f, 0f), 14f));
            Assert.IsFalse(BarrierRules.Covers(Vector3.zero, new Vector3(1f, -20f, 0f), 14f));
        }
    }
}
