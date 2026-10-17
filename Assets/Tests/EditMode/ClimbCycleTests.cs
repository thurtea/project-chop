using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class ClimbCycleTests
    {
        [Test]
        public void AHandStaysOnItsRungWhileTheBodyRises()
        {
            float a = ClimbCycle.Grip(0.05f, 1.8f, 0f, out float la);
            float b = ClimbCycle.Grip(0.15f, 1.8f, 0f, out float lb);
            Assert.AreEqual(a, b, 1e-4f);
            Assert.AreEqual(0f, la);
            Assert.AreEqual(0f, lb);
        }

        [Test]
        public void HandsAlternateAndMoveOneCycleUp()
        {
            int moving = 0;
            for (float y = 0f; y < ClimbCycle.Cycle; y += 0.01f)
            {
                ClimbCycle.Grip(y, 1.8f, 0f, out float l);
                ClimbCycle.Grip(y, 1.8f, 0.5f, out float r);
                Assert.IsFalse(l > 0.001f && r > 0.001f, "both hands off the ladder at " + y);
                if (l > 0.001f) moving++;
            }
            Assert.Greater(moving, 0);
            float low = ClimbCycle.Grip(0.05f, 1.8f, 0f, out _);
            float high = ClimbCycle.Grip(0.05f + ClimbCycle.Cycle, 1.8f, 0f, out _);
            Assert.AreEqual(ClimbCycle.Cycle, high - low, 1e-3f);
        }

        [Test]
        public void GripsLandOnRungs()
        {
            for (float y = 0f; y < 2f; y += 0.07f)
            {
                float g = ClimbCycle.Grip(y, 1.8f, 0.5f, out float lift);
                if (lift != 0f) continue;
                float m = Mathf.Repeat(g + 1000f * ClimbCycle.Rung, ClimbCycle.Rung);
                Assert.Less(Mathf.Min(m, ClimbCycle.Rung - m), 1e-3f, "grip off a rung at " + y);
            }
        }

        [Test]
        public void MountEasesInAndOut()
        {
            Assert.AreEqual(0f, ClimbCycle.Ease(0f));
            Assert.AreEqual(1f, ClimbCycle.Ease(1f));
            Assert.Less(ClimbCycle.Ease(0.1f), 0.1f);
        }
    }
}
