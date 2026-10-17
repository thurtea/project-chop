using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests
{
    public class StoneRulesTests
    {
        [Test]
        public void BiggerRocksGiveMorePerDay()
        {
            Assert.AreEqual(2, StoneRules.DailyYield(0.5f));
            Assert.AreEqual(4, StoneRules.DailyYield(2f));
            Assert.AreEqual(8, StoneRules.DailyYield(6f));
            Assert.AreEqual(1, StoneRules.PerStrike(1.5f));
            Assert.AreEqual(2, StoneRules.PerStrike(3f));
        }

        [Test]
        public void ARockRunsOutForTheDay()
        {
            Assert.AreEqual(2, StoneRules.Take(0, 3f));
            Assert.AreEqual(1, StoneRules.Take(5, 3f));
            Assert.AreEqual(0, StoneRules.Take(6, 3f));
        }

        [Test]
        public void YouMustStandCloseAndFaceTheRock()
        {
            Vector3 rock = new Vector3(0f, 0f, 2f);
            Assert.IsTrue(StoneRules.InReach(Vector3.zero, Vector3.forward, rock, 2f));
            Assert.IsFalse(StoneRules.InReach(Vector3.zero, Vector3.back, rock, 2f));
            Assert.IsFalse(StoneRules.InReach(new Vector3(0f, 0f, -4f), Vector3.forward, rock, 2f));
        }
    }
}
