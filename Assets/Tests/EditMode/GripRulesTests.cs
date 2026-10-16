using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class GripRulesTests
    {
        [Test]
        public void NormalGroundStopsWithinAStride()
        {
            Assert.Less(GripRules.StopDistance(9.5f, 22f, false), 1f);
            Assert.Less(GripRules.StopDistance(6f, 22f, false), 0.4f);
        }

        [Test]
        public void SlipperyGroundStillGlides()
        {
            Assert.Greater(GripRules.StopDistance(9.5f, 22f, true), 4f);
            Assert.Greater(GripRules.StopDistance(6f, 22f, true), GripRules.StopDistance(6f, 22f, false) * 5f);
        }
    }
}