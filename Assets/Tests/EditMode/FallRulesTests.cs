using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class FallRulesTests
    {
        [Test]
        public void ShortDropsDoNotHurt()
        {
            Assert.AreEqual(0, FallRules.Damage(1f, 100));
            Assert.AreEqual(0, FallRules.Damage(FallRules.SafeHeight, 100));
        }

        [Test]
        public void DamageGrowsWithTheDropAndTheDeadlyHeightKills()
        {
            int low = FallRules.Damage(6f, 100);
            int high = FallRules.Damage(12f, 100);
            Assert.Greater(low, 0);
            Assert.Greater(high, low);
            Assert.GreaterOrEqual(FallRules.Damage(FallRules.DeadlyHeight, 100), 100);
            Assert.GreaterOrEqual(FallRules.Damage(1000f, 100), 100);
        }
    }
}
