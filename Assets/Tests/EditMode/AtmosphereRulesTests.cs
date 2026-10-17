using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class AtmosphereRulesTests
    {
        [Test]
        public void HazeIsClearNearAndCappedFar()
        {
            Assert.AreEqual(0f, AtmosphereRules.HazeAmount(30f, 1000f));
            Assert.AreEqual(AtmosphereRules.HazeMax, AtmosphereRules.HazeAmount(1000f, 1000f), 1e-4f);
            Assert.LessOrEqual(AtmosphereRules.HazeMax, 0.35f);
        }

        [Test]
        public void GradeIsSubtleWarmHighCoolLow()
        {
            Assert.LessOrEqual(AtmosphereRules.GradeWeight, 0.6f);
            Assert.Greater(AtmosphereRules.HighlightTone.r, AtmosphereRules.HighlightTone.b);
            Assert.Greater(AtmosphereRules.ShadowTone.b, AtmosphereRules.ShadowTone.r);
        }

        [Test]
        public void HazeDarkensAtNight()
        {
            Color day = AtmosphereRules.HazeTint(Color.white, 0f);
            Color night = AtmosphereRules.HazeTint(Color.white, 1f);
            Assert.Less(night.grayscale, day.grayscale * 0.2f);
            Assert.AreEqual(1f, night.a);
        }
    }
}