using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class WetRulesTests
    {
        [Test]
        public void WaterSoaksYouFully()
        {
            Assert.AreEqual(WetRules.SoakSeconds, WetRules.Step(0f, true, false, false, 0.1f));
            // Even beside a fire, standing in the water keeps you soaked.
            Assert.AreEqual(WetRules.SoakSeconds, WetRules.Step(10f, true, false, true, 0.1f));
        }

        [Test]
        public void WetWearsOffInTheOpenAndFastByAFire()
        {
            float open = WetRules.Step(WetRules.SoakSeconds, false, false, false, 10f);
            float fire = WetRules.Step(WetRules.SoakSeconds, false, false, true, 10f);
            Assert.AreEqual(WetRules.SoakSeconds - 10f, open, 1e-4f);
            Assert.AreEqual(0f, fire, 1e-4f, "ten seconds by a fire dries a full soaking");
            Assert.IsFalse(WetRules.IsWet(fire));
        }

        [Test]
        public void RainKeepsYouWetButAFireStillDries()
        {
            Assert.AreEqual(WetRules.RainSoakSeconds, WetRules.Step(0f, false, true, false, 1f));
            Assert.AreEqual(50f, WetRules.Step(51f, false, true, false, 1f), 1e-4f);
            Assert.Less(WetRules.Step(20f, false, true, true, 1f), 20f);
        }

        [Test]
        public void SwimmingIsSlowerThanWalkingAndSlowerStillWhenExhausted()
        {
            float walk = 6f;
            Assert.Less(SwimRules.Speed(walk, false), walk * 0.6f);
            Assert.Less(SwimRules.Speed(walk, true), SwimRules.Speed(walk, false));
        }

        [Test]
        public void SprintToggleMakesSwimmingFaster()
        {
            float walk = 6f;
            Assert.IsTrue(SwimRules.KeepsSprint(true, false));
            Assert.Greater(SwimRules.Speed(walk, false, true), SwimRules.Speed(walk, false, false));
            Assert.Less(SwimRules.Speed(walk, false, true), walk);
            Assert.AreEqual(SwimRules.Speed(walk, true, true), SwimRules.Speed(walk, true, false), 1e-4f);
            Assert.IsFalse(SwimRules.KeepsSprint(true, true));
        }

        [Test]
        public void CrouchIsBlockedInWater()
        {
            Assert.IsTrue(SwimRules.NextCrouch(false, true, false, false, false));
            Assert.IsFalse(SwimRules.NextCrouch(false, true, false, false, true));
            Assert.IsFalse(SwimRules.NextCrouch(true, false, false, false, true));
            Assert.IsTrue(SwimRules.NextCrouch(true, false, false, false, false));
            Assert.IsFalse(SwimRules.NextCrouch(true, true, false, false, false));
        }

        [Test]
        public void WetStaminaRegenIsSlower()
        {
            var model = new StaminaModel(100f, 10f, 0f, 20f);
            model.Drain(50f);
            model.Tick(0.01f);
            float start = model.Current;
            model.Tick(1f, WetRules.StaminaRegenMultiplier);
            Assert.AreEqual(start + 10f * WetRules.StaminaRegenMultiplier, model.Current, 1e-3f);
        }
    }
}
