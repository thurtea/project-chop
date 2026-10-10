using Chopper.Core;
using Chopper.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class SpeedCheckTests
    {
        private const string GameConfigPath = "Assets/_Chopper/Config/SO_GameConfig.asset";
        private const float MaxSpeed = 9f;
        private const float WindowSeconds = 1f / 3f;

        [Test]
        public void NormalRunIsAllowed()
        {
            Vector3 end = new Vector3(6f * WindowSeconds, 0f, 0f);
            Assert.IsFalse(SpeedCheck.IsTooFast(Vector3.zero, end, WindowSeconds, MaxSpeed));
        }

        [Test]
        public void JustUnderLimitIsAllowedAndJustOverIsNot()
        {
            float limit = MaxSpeed * WindowSeconds;
            Assert.IsFalse(SpeedCheck.IsTooFast(Vector3.zero, new Vector3(0f, 0f, limit * 0.99f), WindowSeconds, MaxSpeed));
            Assert.IsTrue(SpeedCheck.IsTooFast(Vector3.zero, new Vector3(0f, 0f, limit * 1.01f), WindowSeconds, MaxSpeed));
        }

        [Test]
        public void FiftyMeterTeleportIsTooFast()
        {
            Assert.IsTrue(SpeedCheck.IsTooFast(Vector3.zero, new Vector3(0f, 0f, 50f), WindowSeconds, MaxSpeed));
        }

        [Test]
        public void VerticalMovementIsIgnored()
        {
            Assert.IsFalse(SpeedCheck.IsTooFast(Vector3.zero, new Vector3(0f, -50f, 0f), WindowSeconds, MaxSpeed));
            Assert.AreEqual(5f, SpeedCheck.HorizontalDistance(new Vector3(0f, 10f, 0f), new Vector3(3f, -4f, 4f)), 0.0001f);
        }

        [Test]
        public void ZeroDurationNeverFlags()
        {
            Assert.IsFalse(SpeedCheck.IsTooFast(Vector3.zero, new Vector3(100f, 0f, 0f), 0f, MaxSpeed));
        }

        [Test]
        public void GameConfigLimitCoversSprintingDownASteepSlope()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, $"Missing {GameConfigPath}");
            Assert.AreEqual(1.5f, config.SpeedCheckTolerance, 0.0001f);
            // Sprinting while sliding off a mountainside is honest; the old limit (sprint only) snapped it back.
            Assert.AreEqual((config.PlayerSprintSpeed + GameConfig.SteepSlideSpeed) * 1.5f, config.MaxAllowedPlayerSpeed, 0.0001f);
            Assert.Greater(config.MaxAllowedPlayerSpeed, config.PlayerSprintSpeed + GameConfig.SteepSlideSpeed + 0.5f, "an honest sprint down a slope passes");
            Assert.AreEqual(50f, config.DebugTeleportDistance, 0.0001f);
        }

        [Test]
        public void FallingIsNeverARise()
        {
            Assert.AreEqual(0f, SpeedCheck.Rise(new Vector3(0f, 10f, 0f), Vector3.zero), 0.0001f);
            Assert.IsFalse(SpeedCheck.IsRisingTooFast(new Vector3(0f, 80f, 0f), Vector3.zero, WindowSeconds, 1f));
        }

        [Test]
        public void RisingJustOverTheLimitIsFlagged()
        {
            float limit = MaxSpeed * WindowSeconds;
            Assert.IsFalse(SpeedCheck.IsRisingTooFast(Vector3.zero, new Vector3(0f, limit * 0.99f, 0f), WindowSeconds, MaxSpeed));
            Assert.IsTrue(SpeedCheck.IsRisingTooFast(Vector3.zero, new Vector3(0f, limit * 1.01f, 0f), WindowSeconds, MaxSpeed));
            Assert.IsFalse(SpeedCheck.IsRisingTooFast(Vector3.zero, new Vector3(0f, 50f, 0f), 0f, MaxSpeed));
        }

        [Test]
        public void GameConfigRiseLimitCoversJumpingAndClimbingButNotFlying()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, $"Missing {GameConfigPath}");
            // A jump's launch speed and sprinting up the steepest walkable slope (45 degrees) both pass.
            Assert.Greater(config.MaxAllowedRiseSpeed, config.JumpLaunchSpeed);
            Assert.Greater(config.MaxAllowedRiseSpeed, config.PlayerSprintSpeed);
            // Rising 25 m in a third of a second (the dev fly test's jump) does not.
            Assert.IsTrue(SpeedCheck.IsRisingTooFast(Vector3.zero, new Vector3(0f, 25f, 0f), WindowSeconds, config.MaxAllowedRiseSpeed));
        }

        [Test]
        public void FinisherLungeOnTopOfASprintFitsTheBurstAllowance()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            float withBurst = config.MaxAllowedPlayerSpeed + ComboRules.FinisherLunge * config.SpeedCheckTolerance;
            Assert.Greater(withBurst, config.PlayerSprintSpeed + ComboRules.FinisherLunge + GameConfig.SteepSlideSpeed);
        }
    }
}
