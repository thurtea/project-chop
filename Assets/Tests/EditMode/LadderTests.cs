using Chopper.Player;
using Chopper.World;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class LadderTests
    {
        [Test]
        public void MountsTowardTheWallAndClimbs()
        {
            Vector3 inward = Vector3.left;
            Assert.IsTrue(Ladder.Mounts(false, Vector3.left, inward));
            Assert.IsFalse(Ladder.Mounts(false, Vector3.forward, inward));
            Assert.IsFalse(Ladder.Mounts(false, Vector3.zero, inward));
            Assert.IsTrue(Ladder.Mounts(true, Vector3.zero, inward));
            Assert.Greater(Ladder.Climb(Vector3.left, inward), 0f);
            Assert.Less(Ladder.Climb(Vector3.right, inward), 0f);
            Assert.AreEqual(0f, Ladder.Climb(Vector3.zero, inward));
        }

        [Test]
        public void DismountsOnlyOnTheGroundAwayFromTheWall()
        {
            Vector3 inward = Vector3.left;
            Assert.IsTrue(Ladder.Dismounts(true, Vector3.right, inward));
            Assert.IsFalse(Ladder.Dismounts(false, Vector3.right, inward));
            Assert.IsFalse(Ladder.Dismounts(true, Vector3.left, inward));
        }

        [Test]
        public void HandsAlternate()
        {
            for (float p = 0f; p < 6f; p += 0.7f)
                Assert.AreEqual(-ClimbPose.HandLift(p, true), ClimbPose.HandLift(p, false), 1e-4f);
        }
    }
}
