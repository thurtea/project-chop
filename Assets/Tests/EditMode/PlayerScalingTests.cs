using Chopper.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class PlayerScalingTests
    {
        // Plan section 5: multiplier(n) = 1 + 0.4 * log2(min(n, 5)).
        private const float PlanCoefficient = 0.4f;
        private const int PlanCap = 5;
        private const float Tolerance = 0.01f;
        private const string GameConfigPath = "Assets/_Chopper/Config/SO_GameConfig.asset";

        [TestCase(1, 1.00f)]
        [TestCase(2, 1.40f)]
        [TestCase(3, 1.63f)]
        [TestCase(4, 1.80f)]
        [TestCase(5, 1.93f)]
        [TestCase(6, 1.93f)]
        [TestCase(8, 1.93f)]
        public void MatchesPlanTable(int players, float expected)
        {
            Assert.AreEqual(expected, PlayerScaling.Multiplier(players, PlanCoefficient, PlanCap), Tolerance);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void BelowOnePlayerClampsToOne(int players)
        {
            Assert.AreEqual(1f, PlayerScaling.Multiplier(players, PlanCoefficient, PlanCap), Tolerance);
        }

        [TestCase(1, 1.00f)]
        [TestCase(2, 1.40f)]
        [TestCase(3, 1.63f)]
        [TestCase(4, 1.80f)]
        [TestCase(5, 1.93f)]
        public void GameConfigAssetMatchesPlanTable(int players, float expected)
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, $"Missing {GameConfigPath}");

            Assert.AreEqual(expected, PlayerScaling.EnemyMultiplier(config, players), Tolerance);
            Assert.AreEqual(expected, PlayerScaling.TreeDensityMultiplier(config, players), Tolerance);
            Assert.AreEqual(expected, PlayerScaling.SaplingMultiplier(config, players), Tolerance);
        }

        [Test]
        public void GameConfigDefaultsMatchPlanTreeRules()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(4, config.GetHitsToFell(TreeSize.Small));
                Assert.AreEqual(6, config.GetHitsToFell(TreeSize.Medium));
                Assert.AreEqual(9, config.GetHitsToFell(TreeSize.Large));
                // Enemies keep their old health: tougher trees do not make tougher enemies.
                Assert.AreEqual(1, config.GetTreantHealth(TreeSize.Small));
                Assert.AreEqual(2, config.GetTreantHealth(TreeSize.Medium));
                Assert.AreEqual(3, config.GetTreantHealth(TreeSize.Large));
                Assert.AreEqual(1, config.GetBaseSaplings(TreeSize.Small));
                Assert.AreEqual(2, config.GetBaseSaplings(TreeSize.Medium));
                Assert.AreEqual(3, config.GetBaseSaplings(TreeSize.Large));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
