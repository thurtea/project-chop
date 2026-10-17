using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class AtmosphereFogTests
    {
        [Test]
        public void FogIsCappedAndOffIndoorsAndUnderground()
        {
            Assert.LessOrEqual(AtmosphereRules.FogDensity(1f, 0f, false, false, 0f), AtmosphereRules.DensityCap + 1e-4f);
            Assert.Greater(AtmosphereRules.FogDensity(1f, 0f, false, false, 0f), 0.2f);
            Assert.AreEqual(0f, AtmosphereRules.FogDensity(1f, 0f, false, true, 0f));
            Assert.AreEqual(0f, AtmosphereRules.FogDensity(1f, 0f, false, false, 1f));
            Assert.AreEqual(0f, AtmosphereRules.FogDensity(0f, 0f, false, false, 0f));
            Assert.Less(AtmosphereRules.FogDensity(1f, 1f, false, false, 0f), AtmosphereRules.FogDensity(1f, 0f, false, false, 0f));
        }

        [Test]
        public void DefaultIsPointSixAndCaptureOverrideWins()
        {
            Assert.AreEqual(0.6f, AtmosphereRules.DefaultStrength, 1e-5f);
            AtmosphereRules.CaptureOverride = 0.25f;
            try { Assert.AreEqual(0.25f, AtmosphereRules.Strength, 1e-5f); }
            finally { AtmosphereRules.CaptureOverride = -1f; }
        }

        [Test]
        public void ShaftsScaleWithSliderAndStopIndoors()
        {
            Assert.AreEqual(0.45f, AtmosphereRules.ShaftStrength(0.5f, 0.9f, false), 1e-4f);
            Assert.AreEqual(0f, AtmosphereRules.ShaftStrength(1f, 0.9f, true));
        }
    }
}