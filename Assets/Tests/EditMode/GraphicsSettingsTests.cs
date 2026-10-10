using Chopper.UI;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    // The graphics preferences: crisp native resolution by default, values are clamped, and every named option maps
    // to a real entry.
    public class GraphicsSettingsTests
    {
        private static readonly string[] Keys =
        {
            "Chopper.RenderScale", "Chopper.AntiAliasing", "Chopper.ShadowQuality", "Chopper.VSync", "Chopper.PixelLook"
        };

        [SetUp]
        public void ClearPreferences()
        {
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
        }

        [TearDown]
        public void RestorePreferences()
        {
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
        }

        [Test]
        public void DefaultsAreNativeResolutionWithBestAntiAliasing()
        {
            Assert.AreEqual(1f, UserSettings.RenderScale, 0.0001f);
            Assert.AreEqual(UserSettings.DefaultAntiAliasing, UserSettings.AntiAliasing);
            Assert.AreEqual("MSAA 4x + SMAA", UserSettings.AntiAliasingNames[UserSettings.AntiAliasing]);
            Assert.AreEqual("High", UserSettings.ShadowQualityNames[UserSettings.ShadowQuality]);
            Assert.IsTrue(UserSettings.VSync);
        }

        [Test]
        public void PixelLookIsOffByDefaultSoThePictureStaysCrisp()
        {
            Assert.IsFalse(UserSettings.PixelLook);
        }

        [Test]
        public void RenderScaleIsClamped()
        {
            UserSettings.RenderScale = 9f;
            Assert.AreEqual(UserSettings.MaxRenderScale, UserSettings.RenderScale, 0.0001f);
            UserSettings.RenderScale = 0.01f;
            Assert.AreEqual(UserSettings.MinRenderScale, UserSettings.RenderScale, 0.0001f);
        }

        [Test]
        public void AntiAliasingAndShadowQualityStayInTheirLists()
        {
            UserSettings.AntiAliasing = 99;
            Assert.AreEqual(UserSettings.AntiAliasingNames.Length - 1, UserSettings.AntiAliasing);
            UserSettings.AntiAliasing = -3;
            Assert.AreEqual(0, UserSettings.AntiAliasing);
            UserSettings.ShadowQuality = 99;
            Assert.AreEqual(UserSettings.ShadowQualityNames.Length - 1, UserSettings.ShadowQuality);
        }

        [Test]
        public void RenderScaleReachesAboveNativeForSupersampling()
        {
            Assert.Greater(UserSettings.MaxRenderScale, 1f);
            Assert.Less(UserSettings.MinRenderScale, 1f);
        }
    }
}
