using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests
{
    public class IntroRulesTests
    {
        [Test]
        public void ThreePunchesMakeOneHit()
        {
            float progress = 0f;
            int hits = 0;
            for (int i = 0; i < 9; i++)
            {
                if (FistRules.Accumulate(ref progress, 0.34f)) hits++;
            }
            Assert.AreEqual(3, hits);
            Assert.AreEqual(3, FistRules.PunchesPerHit(0.34f));
        }

        [Test]
        public void StrongerFistsNeedFewerPunches()
        {
            Assert.AreEqual(2, FistRules.PunchesPerHit(0.5f));
            Assert.AreEqual(1, FistRules.PunchesPerHit(1f));
        }

        [Test]
        public void FirstPunchesDoNotCount()
        {
            float progress = 0f;
            Assert.IsFalse(FistRules.Accumulate(ref progress, 0.34f));
            Assert.IsFalse(FistRules.Accumulate(ref progress, 0.34f));
            Assert.IsTrue(FistRules.Accumulate(ref progress, 0.34f));
        }

        [Test]
        public void ElementalShareZeroMakesEverythingPlain()
        {
            var rng = new System.Random(1);
            for (int i = 0; i < 100; i++) Assert.AreEqual(Element.Neutral, NightRules.RollElement(rng, 0f, Element.Fire));
        }

        [Test]
        public void ElementalShareOneKeepsAndGivesElements()
        {
            var rng = new System.Random(2);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(Element.Fire, NightRules.RollElement(rng, 1f, Element.Fire));
                Assert.AreNotEqual(Element.Neutral, NightRules.RollElement(rng, 1f, Element.Neutral));
            }
        }

        [Test]
        public void ElementalShareSetsTheMix()
        {
            var rng = new System.Random(3);
            int elemental = 0;
            const int samples = 4000;
            for (int i = 0; i < samples; i++)
            {
                if (NightRules.RollElement(rng, 0.7f, Element.Water) != Element.Neutral) elemental++;
            }
            Assert.That(elemental / (float)samples, Is.EqualTo(0.7f).Within(0.04f));
        }

        [Test]
        public void NightSharesStayInsideTheConfiguredRange()
        {
            var rng = new System.Random(4);
            for (int i = 0; i < 200; i++)
            {
                float share = NightRules.RollShare(rng, 0.15f, 0.85f);
                Assert.That(share, Is.InRange(0.15f, 0.85f));
            }
        }

        [Test]
        public void EachBossRaisesTheTreesNeededForTheNext()
        {
            Assert.AreEqual(40, BossRules.RequiredTrees(40, 0.5f, 0));
            Assert.AreEqual(60, BossRules.RequiredTrees(40, 0.5f, 1));
            Assert.AreEqual(80, BossRules.RequiredTrees(40, 0.5f, 2));
        }

        [Test]
        public void BoonLevelsPackAndCap()
        {
            int packed = 0;
            packed = BoonRules.Grant(packed, BoonKind.Axe);
            packed = BoonRules.Grant(packed, BoonKind.Axe);
            packed = BoonRules.Grant(packed, BoonKind.Stamina);
            Assert.AreEqual(2, BoonRules.Level(packed, BoonKind.Axe));
            Assert.AreEqual(1, BoonRules.Level(packed, BoonKind.Stamina));
            Assert.AreEqual(0, BoonRules.Level(packed, BoonKind.Torch));

            for (int i = 0; i < 20; i++) packed = BoonRules.Grant(packed, BoonKind.Axe);
            Assert.AreEqual(BoonRules.MaxLevel, BoonRules.Level(packed, BoonKind.Axe));
            Assert.AreEqual(1, BoonRules.Level(packed, BoonKind.Stamina), "other boons are untouched");
        }

        [Test]
        public void EveryBoonKindCanBePickedAndNamed()
        {
            var rng = new System.Random(5);
            var seen = new System.Collections.Generic.HashSet<BoonKind>();
            for (int i = 0; i < 200; i++) seen.Add(BoonRules.Pick(rng));
            Assert.AreEqual(BoonRules.KindCount, seen.Count);
            foreach (BoonKind kind in seen) Assert.IsNotEmpty(BoonRules.Name(kind));
        }

        [Test]
        public void FelledTreesPlantTheRightNumberOfSaplings()
        {
            var rng = new System.Random(11);
            int[,] ranges = { { 1, 2 }, { 2, 3 }, { 3, 4 } };
            for (int r = 0; r < 3; r++)
            {
                int low = int.MaxValue, high = 0;
                for (int i = 0; i < 400; i++)
                {
                    int n = DropRules.RollSaplings(rng, ranges[r, 0], ranges[r, 1], 1f);
                    low = System.Math.Min(low, n);
                    high = System.Math.Max(high, n);
                }
                Assert.AreEqual(ranges[r, 0], low, "smallest drop for range " + r);
                Assert.AreEqual(ranges[r, 1], high, "largest drop for range " + r);
            }
        }

        [Test]
        public void ARestlessNightWakesElementalSaplingsFirst()
        {
            var rng = new System.Random(5);
            int elemental = 0;
            for (int i = 0; i < 2000; i++) if (NightRules.WakeElementalNext(rng, 0.85f, true, true)) elemental++;
            Assert.That(elemental / 2000f, Is.EqualTo(0.85f).Within(0.04f));
            Assert.IsFalse(NightRules.WakeElementalNext(rng, 1f, false, true), "no elemental ones left");
            Assert.IsTrue(NightRules.WakeElementalNext(rng, 0f, true, false), "only elemental ones left");
        }

        [Test]
        public void CounterOfIsTheElementThatBeatsIt()
        {
            Assert.AreEqual(Element.Water, NightRules.CounterOf(Element.Fire));
            Assert.AreEqual(Element.Fire, NightRules.CounterOf(Element.Wind));
            Assert.AreEqual(Element.Wind, NightRules.CounterOf(Element.Earth));
            Assert.AreEqual(Element.Earth, NightRules.CounterOf(Element.Water));
            Assert.AreEqual(Element.Neutral, NightRules.CounterOf(Element.Neutral));
        }

        [Test]
        public void ThemeIsPickedOnlyAmongElementsWithSaplings()
        {
            var rng = new System.Random(3);
            Assert.AreEqual(Element.Neutral, NightRules.RollDominant(rng, new[] { 9, 0, 0, 0, 0 }));
            for (int i = 0; i < 200; i++) Assert.AreEqual(Element.Fire, NightRules.RollDominant(rng, new[] { 5, 0, 7, 0, 0 }));
        }

        [Test]
        public void ADominantNightIsMostlyThatElementWithSomeCounterAndPlain()
        {
            var rng = new System.Random(8);
            int[] available = { 50, 50, 50, 50, 50 };
            var counts = new int[5];
            for (int i = 0; i < 4000; i++) counts[(int)NightRules.PickWake(rng, 0.8f, Element.Fire, available)]++;
            Assert.Greater(counts[(int)Element.Fire], counts[(int)Element.Earth] * 3, "Fire swarms");
            Assert.Greater(counts[(int)Element.Water], counts[(int)Element.Earth], "the counter shows up more than the rest");
            Assert.Greater(counts[(int)Element.Water], 300, "enough counter trees to charge the axe");
            Assert.That(counts[0] / 4000f, Is.EqualTo(0.2f).Within(0.04f), "plain share");
        }

        [Test]
        public void ThemeFallsBackWhenTheWantedKindIsGone()
        {
            var rng = new System.Random(2);
            Assert.AreEqual(Element.Earth, NightRules.PickWake(rng, 1f, Element.Fire, new[] { 0, 0, 0, 4, 0 }));
            Assert.AreEqual(Element.Neutral, NightRules.PickWake(rng, 1f, Element.Fire, new[] { 3, 0, 0, 0, 0 }));
            StringAssert.Contains("Water", NightRules.DescribeTheme(Element.Fire, 0.8f));
        }
    }
}
