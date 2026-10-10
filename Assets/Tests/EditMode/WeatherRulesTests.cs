using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class WeatherRulesTests
    {
        [Test]
        public void RainAlwaysComesAndGoesThroughCloud()
        {
            for (int i = 0; i < 100; i++)
            {
                double roll = (i + 0.5) / 100.0;
                Assert.AreNotEqual(WeatherKind.Rain, WeatherRules.Next(WeatherKind.Sunny, roll), "sun never turns straight to rain");
                Assert.AreNotEqual(WeatherKind.Sunny, WeatherRules.Next(WeatherKind.Rain, roll), "rain never turns straight to sun");
            }
        }

        [Test]
        public void EveryWeatherCanFollowCloud()
        {
            Assert.AreEqual(WeatherKind.Sunny, WeatherRules.Next(WeatherKind.Cloudy, 0.1));
            Assert.AreEqual(WeatherKind.Cloudy, WeatherRules.Next(WeatherKind.Cloudy, 0.5));
            Assert.AreEqual(WeatherKind.Rain, WeatherRules.Next(WeatherKind.Cloudy, 0.9));
        }

        [Test]
        public void SpellsLastMinutesAndRainIsShorter()
        {
            Assert.That(WeatherRules.Duration(WeatherKind.Sunny, 0.0), Is.EqualTo(WeatherRules.MinSpellSeconds).Within(0.01f));
            Assert.That(WeatherRules.Duration(WeatherKind.Sunny, 1.0), Is.EqualTo(WeatherRules.MaxSpellSeconds).Within(0.01f));
            Assert.Less(WeatherRules.Duration(WeatherKind.Rain, 0.5), WeatherRules.Duration(WeatherKind.Sunny, 0.5));
        }

        [Test]
        public void TheWindPicksUpWithCloudAndGustsInTheRain()
        {
            float sun = WeatherRules.Look(WeatherKind.Sunny).Wind;
            float cloud = WeatherRules.Look(WeatherKind.Cloudy).Wind;
            float rain = WeatherRules.Look(WeatherKind.Rain).Wind;
            Assert.AreEqual(1f, sun, 0.0001f);
            Assert.Greater(cloud, sun);
            Assert.Greater(rain, cloud);
            Assert.AreEqual((sun + rain) * 0.5f, WeatherLook.Lerp(WeatherRules.Look(WeatherKind.Sunny), WeatherRules.Look(WeatherKind.Rain), 0.5f).Wind, 0.0001f);
            Assert.Greater(WeatherRules.WindDirection.magnitude, 0.99f);
        }

        [Test]
        public void WorseWeatherIsDarkerFoggierAndOnlyRainRains()
        {
            WeatherLook sun = WeatherRules.Look(WeatherKind.Sunny);
            WeatherLook cloud = WeatherRules.Look(WeatherKind.Cloudy);
            WeatherLook rain = WeatherRules.Look(WeatherKind.Rain);
            Assert.AreEqual(1f, sun.Sun);
            Assert.Less(cloud.Sun, sun.Sun);
            Assert.Less(rain.Sun, cloud.Sun);
            Assert.Greater(rain.FogDensity, cloud.FogDensity);
            Assert.AreEqual(0f, sun.Rain);
            Assert.AreEqual(0f, cloud.Rain);
            Assert.AreEqual(1f, rain.Rain);
            WeatherLook half = WeatherLook.Lerp(sun, rain, 0.5f);
            Assert.AreEqual(0.5f, half.Rain, 0.001f);
        }

        [Test]
        public void FishBiteSoonerInTheRain()
        {
            Assert.Less(WeatherRules.BiteMultiplier(WeatherKind.Rain), 1f);
            Assert.AreEqual(1f, WeatherRules.BiteMultiplier(WeatherKind.Sunny));
        }
    }
}
