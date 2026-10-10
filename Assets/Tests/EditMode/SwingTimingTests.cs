using Chopper.Core;
using Chopper.Player;
using NUnit.Framework;
using UnityEditor;

namespace Chopper.Tests.EditMode
{
    public class SwingTimingTests
    {
        private const string GameConfigPath = "Assets/_Chopper/Config/SO_GameConfig.asset";
        private const float Tolerance = 0.1f;

        [Test]
        public void FirstSwingIsAllowed()
        {
            Assert.IsTrue(SwingTiming.IsServerSwingAllowed(0f, 0f, Tolerance));
        }

        [Test]
        public void SwingAtOrAfterCooldownIsAllowed()
        {
            Assert.IsTrue(SwingTiming.IsServerSwingAllowed(10.6f, 10.6f, Tolerance));
            Assert.IsTrue(SwingTiming.IsServerSwingAllowed(11f, 10.6f, Tolerance));
        }

        [Test]
        public void SlightlyEarlySwingWithinToleranceIsAllowed()
        {
            Assert.IsTrue(SwingTiming.IsServerSwingAllowed(10.55f, 10.6f, Tolerance));
        }

        [Test]
        public void EarlySwingBeyondToleranceIsRejected()
        {
            Assert.IsFalse(SwingTiming.IsServerSwingAllowed(10.4f, 10.6f, Tolerance));
            Assert.IsFalse(SwingTiming.IsServerSwingAllowed(10.0f, 10.6f, Tolerance));
        }

        [Test]
        public void GameConfigSwingValuesMatchPlan()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, $"Missing {GameConfigPath}");
            Assert.AreEqual(0.6f, config.SwingCooldown, 0.0001f);
            Assert.AreEqual(0.1f, config.SwingLatencyTolerance, 0.0001f);
            Assert.LessOrEqual(config.SwingAnimationDuration, config.SwingCooldown,
                "The chop animation should finish before the next swing is allowed.");
        }
    }
}
