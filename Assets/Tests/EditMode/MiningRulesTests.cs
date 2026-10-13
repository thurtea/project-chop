using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class MiningRulesTests
    {
        [Test]
        public void TheGlintPeaksOnceAndIsDarkMostOfTheTime()
        {
            float peak = 0f;
            int bright = 0;
            for (int i = 0; i < 170; i++)
            {
                float glow = MiningRules.Glint(i * 0.01f, 0);
                if (glow > peak) peak = glow;
                if (glow >= MiningRules.GoodAt) bright++;
            }
            Assert.Greater(peak, 0.95f);
            // Good or better about a third of each glint (55 of 170 samples), dark the rest.
            Assert.Less(bright, 170 * 0.4f);
        }

        [Test]
        public void ASwingIsJudgedByTheGlow()
        {
            Assert.AreEqual(StrikeQuality.Clean, MiningRules.Judge(0.9f, false));
            Assert.AreEqual(StrikeQuality.Good, MiningRules.Judge(0.7f, false));
            Assert.AreEqual(StrikeQuality.Clean, MiningRules.Judge(0.7f, true), "the old pick head makes a near miss clean");
            Assert.AreEqual(StrikeQuality.Rough, MiningRules.Judge(0.2f, true));
            Assert.AreEqual(1f, MiningRules.Progress(StrikeQuality.Clean), 0.0001f);
            Assert.AreEqual(0.6f, MiningRules.Progress(StrikeQuality.Good), 0.0001f);
            Assert.AreEqual(0.3f, MiningRules.Progress(StrikeQuality.Rough), 0.0001f);
            Assert.IsTrue(MiningRules.Cracks(StrikeQuality.Rough, 0.1));
            Assert.IsFalse(MiningRules.Cracks(StrikeQuality.Clean, 0.0));
            Assert.IsFalse(MiningRules.Cracks(StrikeQuality.Rough, 0.5));
            Assert.AreEqual("Clean", MiningRules.Word(StrikeQuality.Clean));
            Assert.AreEqual("Rough", MiningRules.Word(StrikeQuality.Rough));
            Assert.AreEqual(1, MiningRules.NextChain(0, 0f, MiningRules.ComboWindow));
            Assert.AreEqual(1, MiningRules.NextChain(2, MiningRules.ComboWindow + 0.1f, MiningRules.ComboWindow));
            Assert.AreEqual(3, MiningRules.NextChain(2, MiningRules.GlintPeriod, MiningRules.ComboWindow));
            // Every strike pays in full; the combo adds damage (+25 % a link, at most double).
            Assert.AreEqual(1f, MiningRules.ComboProgress(StrikeQuality.Clean, 1), 0.001f);
            Assert.AreEqual(1.5f, MiningRules.ComboProgress(StrikeQuality.Clean, 3), 0.001f);
            Assert.AreEqual(2f, MiningRules.ComboProgress(StrikeQuality.Clean, 12), 0.001f);
            Assert.Greater(MiningRules.ComboProgress(StrikeQuality.Good, 4), MiningRules.ComboProgress(StrikeQuality.Good, 1));
            Assert.AreEqual("Clean", MiningRules.ComboWord(StrikeQuality.Clean, 1));
            Assert.AreEqual("Clean x3", MiningRules.ComboWord(StrikeQuality.Clean, 3));
        }

        [Test]
        public void AVeinKeepsToItsMineAndFishingFindsStayOnTheLine()
        {
            for (int i = 0; i < 200; i++)
            {
                double roll = i / 200.0;
                MineFindKind water = MiningRules.Roll(MineZone.Water, roll, 0f);
                Assert.AreNotEqual(MineFindKind.OldPickHead, water);
                MineZone home = MiningRules.Finds[(int)water].Home;
                Assert.IsTrue(home == MineZone.Any || home == MineZone.Water, water.ToString());
                MineFindKind fire = MiningRules.Roll(MineZone.Fire, roll, 1f);
                Assert.AreNotEqual(MineFindKind.Aquamarine, fire);
                Assert.AreNotEqual(MineFindKind.OldPickHead, fire);
            }
            Assert.AreEqual("Drip Mine", MiningRules.MineName(MineZone.Water));
            Assert.AreEqual(MineZone.Fire, MiningRules.ZoneOf(Element.Fire));
        }

        [Test]
        public void CleanerStrikesFreeBiggerFinds()
        {
            float rough = MiningRules.Grams(MineFindKind.IronOre, 0.5, 0f);
            float clean = MiningRules.Grams(MineFindKind.IronOre, 0.5, 1f);
            Assert.Greater(clean, rough);
            MineFind ore = MiningRules.Finds[(int)MineFindKind.IronOre];
            Assert.GreaterOrEqual(rough, ore.MinGrams - 0.05f);
            Assert.LessOrEqual(clean, ore.MaxGrams + 0.05f);
            Assert.IsTrue(MiningRules.IsRecord(clean, rough));
            Assert.IsFalse(MiningRules.IsRecord(rough, clean));
        }
    }
}
