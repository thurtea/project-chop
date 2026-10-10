using Chopper.Core;
using Chopper.World;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class ZoneMapTests
    {
        private const float Hub = 45f;

        [Test]
        public void HutAndHubAreNeutral()
        {
            Assert.AreEqual(Element.Neutral, ZoneMap.ElementAt(0f, 0f, Hub));
            Assert.AreEqual(Element.Neutral, ZoneMap.ElementAt(44f, 0f, Hub));
            Assert.AreEqual(Element.Neutral, ZoneMap.ElementAt(0f, -44f, Hub));
        }

        [Test]
        public void ForestsMatchTheirCompassDirection()
        {
            Assert.AreEqual(Element.Water, ZoneMap.ElementAt(0f, 80f, Hub));
            Assert.AreEqual(Element.Fire, ZoneMap.ElementAt(0f, -80f, Hub));
            Assert.AreEqual(Element.Earth, ZoneMap.ElementAt(80f, 0f, Hub));
            Assert.AreEqual(Element.Wind, ZoneMap.ElementAt(-80f, 0f, Hub));
        }

        [Test]
        public void QuadrantsSplitOnTheDiagonals()
        {
            Assert.AreEqual(Element.Water, ZoneMap.ElementAt(30f, 60f, Hub));
            Assert.AreEqual(Element.Earth, ZoneMap.ElementAt(60f, 30f, Hub));
            Assert.AreEqual(Element.Fire, ZoneMap.ElementAt(-30f, -60f, Hub));
            Assert.AreEqual(Element.Wind, ZoneMap.ElementAt(-60f, -30f, Hub));
        }

        [Test]
        public void AxisPointsIntoEachForest()
        {
            foreach (Element zone in new[] { Element.Water, Element.Fire, Element.Earth, Element.Wind })
            {
                Vector2 axis = ZoneMap.Axis(zone) * 80f;
                Assert.AreEqual(zone, ZoneMap.ElementAt(axis.x, axis.y, Hub));
            }
        }
    }
}
