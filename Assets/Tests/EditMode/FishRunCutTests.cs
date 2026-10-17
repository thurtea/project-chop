using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class FishRunCutTests
    {
        private const float Water = 0f;

        [Test]
        public void RunCutEyeStaysUnderwaterInOpenWater()
        {
            Vector3 eye = FishShow.RunCutEye(new Vector3(0f, -0.6f, 0f), Vector3.forward, 0.5f, Water, p => -3f);
            Assert.Less(eye.y, Water - 0.3f);
        }

        [Test]
        public void RunCutEyeNeverSitsOverTheBankWhereTreesStand()
        {
            System.Func<Vector3, float> ground = p => p.x > 0.5f ? 1.5f : -3f;
            for (float k = 0f; k <= 1f; k += 0.25f)
            {
                Vector3 eye = FishShow.RunCutEye(new Vector3(0f, -0.6f, 0f), Vector3.forward, k, Water, ground);
                Assert.Less(eye.y, Water - 0.3f);
                Assert.LessOrEqual(ground(eye), eye.y - 0.3f + 0.0001f);
            }
        }

        [Test]
        public void RunCutLooksLevelOrDownSoTheSurfaceStaysOutOfFrame()
        {
            Vector3 eye = new Vector3(2f, -0.45f, 0f);
            Vector3 look = FishShow.RunCutLook(new Vector3(0f, 0.2f, 0f), eye);
            Assert.Less(look.y, eye.y);
            Assert.LessOrEqual(FishShow.RunCutFar, 20f);
        }
    }
}
