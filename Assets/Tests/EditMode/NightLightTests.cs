using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class NightLightTests
    {
        [Test]
        public void MoonRisesPeaksAndSets()
        {
            Assert.AreEqual(LightGrade.MoonLow, LightGrade.MoonIntensity(0f), 1e-4f);
            Assert.AreEqual(LightGrade.MoonHigh, LightGrade.MoonIntensity(0.5f), 1e-4f);
            Assert.Less(LightGrade.MoonIntensity(0.95f), LightGrade.MoonIntensity(0.5f));
            Assert.Greater(LightGrade.MoonPitch(0.5f), LightGrade.MoonPitch(0f));
        }

        [Test]
        public void NightAmbientNeverDropsBelowTheFloor()
        {
            Color dark = new Color(0.01f, 0.01f, 0.02f);
            Color lifted = LightGrade.AtLeast(dark, LightGrade.NightAmbientFloor, 1f);
            Assert.GreaterOrEqual(lifted.b, LightGrade.NightAmbientFloor.b);
            Assert.AreEqual(dark, LightGrade.AtLeast(dark, LightGrade.NightAmbientFloor, 0f));
        }

        [Test]
        public void FlickerStaysNearTheBase()
        {
            for (float a = 0f; a <= 1f; a += 0.25f)
                for (float b = 0f; b <= 1f; b += 0.25f)
                    Assert.That(LightGrade.Flicker(5f, a, b), Is.InRange(5f * 0.8f, 5f * 1.12f));
        }

        [Test]
        public void OnlyWarmPointLightsCountAsFire()
        {
            var go = new GameObject("l");
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            Assert.IsTrue(Chopper.World.FireLights.IsFire(light));
            light.color = new Color(0.5f, 0.62f, 1f);
            Assert.IsFalse(Chopper.World.FireLights.IsFire(light));
            Object.DestroyImmediate(go);
        }
    }
}