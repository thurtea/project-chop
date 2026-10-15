using Chopper.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class SkillRulesTests
    {
        private const string GameConfigPath = "Assets/_Chopper/Config/SO_GameConfig.asset";

        private GameConfig _config;
        private int _large;
        private float _topGear;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            _large = _config != null ? _config.GetHitsToFell(TreeSize.Large) : 9;
            float[] gears = _config != null ? _config.AxeDamageMultipliers : new[] { 1f, 1.25f, 1.5f, 2f };
            _topGear = gears[gears.Length - 1];
        }

        [Test]
        public void BalanceTableMatchesSkillDesign()
        {
            Assert.AreEqual(9, _large, "the logging curve was tuned for a nine hit large tree");
            Assert.AreEqual(2f, _topGear, 0.0001f, "the logging cap was tuned for a top axe multiplier of two");
        }

        [Test]
        public void LogMultiplierSpansLessAndMoreThanPlain()
        {
            Assert.AreEqual(SkillRules.LogChopMin, SkillRules.LogChopMultiplier(0), 0.0001f);
            Assert.AreEqual(SkillRules.LogChopMax, SkillRules.LogChopMultiplier(SkillRules.MaxSkill), 0.0001f);
            Assert.Less(SkillRules.LogChopMultiplier(0), 1f);
            Assert.Greater(SkillRules.LogChopMultiplier(SkillRules.MaxSkill), 1f);
            Assert.Greater(SkillRules.LogChopMultiplier(60), SkillRules.LogChopMultiplier(40));
            Assert.AreEqual(1f, SkillRules.LogSwingMultiplier(0), 0.0001f);
            Assert.AreEqual(SkillRules.LogSwingMin, SkillRules.LogSwingMultiplier(SkillRules.MaxSkill), 0.0001f);
            Assert.Less(SkillRules.LogSwingMultiplier(SkillRules.MaxSkill), 1f);
        }

        [Test]
        public void NewLoggerIsSlowerThanAPlainSwing()
        {
            int newSwings = ChopRules.SwingsToFell(_large, SkillRules.TreeChopPower(1f, 0));
            int plainSwings = ChopRules.SwingsToFell(_large, 1f);
            Assert.Greater(newSwings, plainSwings);
            Assert.Greater(newSwings, _large, "skill 0 must take more than one plain hit per hit to fell");
        }

        [Test]
        public void MaxLoggerWithTopGearStillNeedsTwoSwings()
        {
            float power = SkillRules.TreeChopPower(_topGear, SkillRules.MaxSkill);
            int points = ChopRules.SwingPoints(power);
            Assert.LessOrEqual(points, ChopRules.TreePoints(_large) / 2, "a swing may not take half the tree or more");
            Assert.GreaterOrEqual(ChopRules.SwingsToFell(_large, power), 2);
        }

        [Test]
        public void RunawayGearIsCappedSoLargeTreeNeverFallsInOneSwing()
        {
            float power = SkillRules.TreeChopPower(_topGear * 10f, SkillRules.MaxSkill);
            Assert.AreEqual(SkillRules.MaxTreeChopPower, power, 0.0001f);
            Assert.GreaterOrEqual(ChopRules.SwingsToFell(_large, power), 2);
        }

        [Test]
        public void MiningSkillSlowsThenSpeedsFreeing()
        {
            int low = StrikesToFree(0);
            int high = StrikesToFree(SkillRules.MaxSkill);
            Assert.Greater(low, MiningRules.FreeAt, "a new miner frees slower than the old threshold");
            Assert.Greater(high, 1, "even a maxed miner needs more than one strike");
            Assert.Less(high, low, "a maxed miner frees faster than a new one");
        }

        [Test]
        public void MiningNeverFreesInOneStrike()
        {
            Assert.IsFalse(SkillRules.MineFrees(1000f, 1));
            Assert.IsTrue(SkillRules.MineFrees(MiningRules.FreeAt, SkillRules.MineMinStrikes));
        }

        [Test]
        public void FishingSkillEasesTheFight()
        {
            Assert.AreEqual(1f, SkillRules.FishFightEase(0), 0.0001f, "skill 0 gives no help");
            Assert.Greater(SkillRules.FishFightEase(SkillRules.MaxSkill), 1f);

            FishSpecies pike = FishSpecies.Defaults()[1];
            Assert.Less(Tension(SkillRules.MaxSkill, pike), Tension(0, pike), "a maxed angler keeps the line calmer");
        }

        [Test]
        public void GainClampsBetweenZeroAndMax()
        {
            Assert.AreEqual(0, SkillRules.Gain(0, 0));
            Assert.AreEqual(5, SkillRules.Gain(0, 5));
            Assert.AreEqual(SkillRules.MaxSkill, SkillRules.Gain(SkillRules.MaxSkill, 10));
            Assert.AreEqual(SkillRules.MaxSkill, SkillRules.Clamp(1000));
            Assert.AreEqual(0, SkillRules.Clamp(-5));
        }

        private static int StrikesToFree(int skill)
        {
            float mult = SkillRules.MineProgressMultiplier(skill);
            float progress = 0f;
            for (int strikes = 1; strikes <= 100; strikes++)
            {
                progress += MiningRules.RhythmProgress(StrikeQuality.Clean, false) * mult;
                if (SkillRules.MineFrees(progress, strikes)) return strikes;
            }
            return -1;
        }

        private static float Tension(int skill, FishSpecies species)
        {
            Fight fight = FishingRules.Begin(FishKind.Pike, species, 10f);
            float ease = SkillRules.FishFightEase(skill);
            for (int i = 0; i < 20; i++) FishingRules.Step(ref fight, species, true, 1, 0f, 0.15f, 0.1f, ease);
            return fight.Tension;
        }
    }
}
