using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests
{
    public class HomeRaidRulesTests
    {
        [Test]
        public void RaidsGrowWithPlayersUpToTheCap()
        {
            Assert.AreEqual(3, HomeRaidRules.Count(3, 1, 1, 12));
            Assert.AreEqual(6, HomeRaidRules.Count(3, 1, 4, 12));
            Assert.AreEqual(5, HomeRaidRules.Count(3, 2, 4, 5));
            Assert.AreEqual(3, HomeRaidRules.Count(3, 1, 0, 12));
        }

        [Test]
        public void EveryThirdRaiderIsBigger()
        {
            Assert.AreEqual(TreeSize.Small, HomeRaidRules.Size(0));
            Assert.AreEqual(TreeSize.Small, HomeRaidRules.Size(1));
            Assert.AreEqual(TreeSize.Medium, HomeRaidRules.Size(2));
        }

        [Test]
        public void ReachIsMeasuredOnTheGround()
        {
            Assert.IsTrue(HomeRaidRules.InReach(new Vector3(0f, 5f, 0f), new Vector3(2f, 0f, 0f), 2.2f));
            Assert.IsFalse(HomeRaidRules.InReach(Vector3.zero, new Vector3(2.5f, 0f, 0f), 2.2f));
        }
    }
}
