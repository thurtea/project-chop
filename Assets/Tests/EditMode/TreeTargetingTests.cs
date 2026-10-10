using Chopper.Core;
using Chopper.Trees;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class TreeTargetingTests
    {
        private const float Range = 2.2f;
        private const float Radius = 0.8f;
        private const float Trunk = 0.35f;

        private static float Hit(Vector3 origin, Vector3 aim, Vector3 tree)
        {
            return TreeTargeting.HitDistance(origin, aim, Range, Radius, tree, Trunk);
        }

        [Test]
        public void TreeInFrontIsHit()
        {
            Assert.GreaterOrEqual(Hit(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2f)), 0f);
        }

        [Test]
        public void TreeBehindIsMissed()
        {
            Assert.Less(Hit(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f)), 0f);
        }

        [Test]
        public void TreeBeyondRangeIsMissed()
        {
            Assert.Less(Hit(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, Range + Trunk + 0.5f)), 0f);
        }

        [Test]
        public void TreeFarToTheSideIsMissed()
        {
            Assert.Less(Hit(Vector3.zero, Vector3.forward, new Vector3(3f, 0f, 1f)), 0f);
        }

        [Test]
        public void HeightDifferenceDoesNotMatter()
        {
            Assert.GreaterOrEqual(Hit(new Vector3(0f, 5f, 0f), Vector3.forward, new Vector3(0f, 0f, 1.5f)), 0f);
        }

        [Test]
        public void HitsFromEveryApproachAngle()
        {
            Vector3 tree = new Vector3(10f, 0f, -4f);
            for (int degrees = 0; degrees < 360; degrees += 15)
            {
                Vector3 aim = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
                Vector3 origin = tree - aim * 1.6f;
                Assert.GreaterOrEqual(Hit(origin, aim, tree), 0f, $"Missed when approaching along {degrees} degrees.");
            }
        }

        [Test]
        public void ZeroAimIsMissed()
        {
            Assert.Less(Hit(Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 1f)), 0f);
            Assert.Less(Hit(Vector3.zero, Vector3.up, new Vector3(0f, 0f, 1f)), 0f);
        }

        [Test]
        public void NearerTreeHasSmallerDistance()
        {
            float near = Hit(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 1f));
            float far = Hit(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2f));
            Assert.Less(near, far);
        }
    }

    public class WoodRulesTests
    {
        [Test]
        public void CarryingIsCapped()
        {
            Assert.AreEqual(10, WoodRules.AddCarried(4, 6, 40));
            Assert.AreEqual(40, WoodRules.AddCarried(38, 8, 40));
            Assert.AreEqual(40, WoodRules.AddCarried(40, 2, 40));
        }

        [Test]
        public void NegativeGainDoesNotRemoveWood()
        {
            Assert.AreEqual(5, WoodRules.AddCarried(5, -3, 40));
        }

        [Test]
        public void RepairSpendsOnlyWhatIsNeeded()
        {
            WoodRules.Repair(store: 100, health: 480, maxHealth: 500, healthPerWood: 10, out int spent, out int gained);
            Assert.AreEqual(2, spent);
            Assert.AreEqual(20, gained);
        }

        [Test]
        public void RepairIsLimitedByStore()
        {
            WoodRules.Repair(store: 3, health: 100, maxHealth: 500, healthPerWood: 10, out int spent, out int gained);
            Assert.AreEqual(3, spent);
            Assert.AreEqual(30, gained);
        }

        [Test]
        public void RepairRoundsUpButNeverExceedsMax()
        {
            WoodRules.Repair(store: 10, health: 495, maxHealth: 500, healthPerWood: 10, out int spent, out int gained);
            Assert.AreEqual(1, spent);
            Assert.AreEqual(5, gained);
        }

        [Test]
        public void FullHealthSpendsNothing()
        {
            WoodRules.Repair(store: 10, health: 500, maxHealth: 500, healthPerWood: 10, out int spent, out int gained);
            Assert.AreEqual(0, spent);
            Assert.AreEqual(0, gained);
        }
    }
}
