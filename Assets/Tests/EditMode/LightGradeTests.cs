using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class LightGradeTests
    {
        [Test]
        public void DawnNoonDuskAndNightDiffer()
        {
            GradeLook dawn = LightGrade.For(Element.Neutral, 0f, 1f, 0f);
            GradeLook noon = LightGrade.For(Element.Neutral, 0.5f, 0f, 0f);
            GradeLook dusk = LightGrade.For(Element.Neutral, 1f, 1f, 0f);
            GradeLook night = LightGrade.For(Element.Neutral, 1f, 1f, 1f);
            Assert.Greater(dusk.Highlights.r - dusk.Highlights.b, noon.Highlights.r - noon.Highlights.b);
            Assert.Greater(dawn.Highlights.b, dusk.Highlights.b);
            Assert.Greater(night.Shadows.b, night.Shadows.r);
            Assert.AreNotEqual(dawn.SunColor, dusk.SunColor);
        }

        [Test]
        public void HighlightsWarmShadowsTeal()
        {
            foreach (GradeLook g in new[] { LightGrade.Dawn, LightGrade.Noon, LightGrade.Dusk })
            {
                Assert.Greater(g.Highlights.r, g.Highlights.b);
                Assert.Greater(g.Shadows.b, g.Shadows.r);
                Assert.Greater(g.Shadows.g, g.Shadows.r);
            }
        }

        [Test]
        public void ZonesTintHighlightsByDayOnly()
        {
            GradeLook fire = LightGrade.For(Element.Fire, 0.5f, 0f, 0f);
            GradeLook water = LightGrade.For(Element.Water, 0.5f, 0f, 0f);
            Assert.Greater(fire.Highlights.r, water.Highlights.r);
            GradeLook fireNight = LightGrade.For(Element.Fire, 0.5f, 0f, 1f);
            Assert.AreEqual(LightGrade.Night.Highlights, fireNight.Highlights);
            Assert.That(LightGrade.BloomThreshold, Is.InRange(1.05f, 1.15f));
        }
    }
}