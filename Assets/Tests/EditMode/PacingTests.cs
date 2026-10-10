using Chopper.Core;
using NUnit.Framework;
using UnityEditor;

namespace Chopper.Tests.EditMode
{
    // Design guards: the day is long enough to visit all four forests, the night is long enough for the farthest
    // enemy to arrive, and the whole cycle stays short.
    public class PacingTests
    {
        private const string GameConfigPath = "Assets/_Chopper/Config/SO_GameConfig.asset";

        // Forest centers sit about here from the hut: the hub edge plus half the forest depth (45 to 122 m).
        private const float ForestDepthFromHub = 75f;
        private const float FarthestTreeRadius = 260f;
        // A "cleared" forest means about this many trees felled at an average of two hits each.
        private const int TreesPerForest = 8;
        private const float AverageHits = 2f;
        private const float RepositionSeconds = 2.5f;
        // Slack for deposits at the hut, waking enemies, and mistakes.
        private const float NightSlackSeconds = 30f;
        private const float MaxCycleSeconds = 600f;

        private static GameConfig Load()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, $"Missing {GameConfigPath}");
            return config;
        }

        [Test]
        public void DayIsLongEnoughToVisitAllFourForests()
        {
            GameConfig config = Load();
            // Players sprint between forests (Shift toggles sprint), so the circuit is walked at sprint speed.
            float walk = PacingRules.CircuitWalkSeconds(config.HubRadius + ForestDepthFromHub, config.PlayerSprintSpeed);
            float chop = PacingRules.ChopSeconds(TreesPerForest, 4, AverageHits, config.SwingCooldown, RepositionSeconds);
            Assert.GreaterOrEqual(config.DayDuration, walk + chop,
                $"Day of {config.DayDuration} s is shorter than the estimated circuit ({walk:F0} s walking + {chop:F0} s chopping).");
        }

        [Test]
        public void NightIsLongEnoughForTheFarthestEnemyToArrive()
        {
            GameConfig config = Load();
            float slowest = config.GetTreantSpeed(TreeSize.Large);
            float arrival = PacingRules.ChargeWalkSeconds(FarthestTreeRadius, slowest, config.TreantChargeMultiplier, config.TreantChargeDistance);
            Assert.GreaterOrEqual(config.NightDuration, arrival + NightSlackSeconds,
                $"Night of {config.NightDuration} s is shorter than the farthest walk ({arrival:F0} s) plus slack.");
        }

        [Test]
        public void WholeCycleStaysShort()
        {
            GameConfig config = Load();
            float total = config.DayDuration + config.DuskDuration + config.NightDuration + config.DawnDuration;
            Assert.LessOrEqual(total, MaxCycleSeconds, $"A full day is {total} s.");
        }

        [Test]
        public void AxeChargeLastsFifteenHits()
        {
            GameConfig config = Load();
            Assert.AreEqual(15, config.ChargesPerKill);
            Assert.GreaterOrEqual(config.ChargeCapPerElement, config.ChargesPerKill);
        }
    }
}
