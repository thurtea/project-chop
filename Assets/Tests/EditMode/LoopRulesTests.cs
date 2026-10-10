using System.Collections.Generic;
using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class DayNightClockTests
    {
        private static DayNightClock Make() => new DayNightClock(10f, 2f, 8f, 2f);

        [Test]
        public void StartsAtDayOne()
        {
            DayNightClock clock = Make();
            Assert.AreEqual(DayPhase.Day, clock.Phase);
            Assert.AreEqual(1, clock.Day);
            Assert.AreEqual(0f, clock.Darkness);
        }

        [Test]
        public void PhasesAdvanceInOrderAndDayCountIncrements()
        {
            DayNightClock clock = Make();
            var seen = new List<DayPhase>();
            clock.PhaseChanged += p => seen.Add(p);
            clock.Advance(22f);
            CollectionAssert.AreEqual(new[] { DayPhase.Dusk, DayPhase.Night, DayPhase.Dawn, DayPhase.Day }, seen);
            Assert.AreEqual(2, clock.Day);
        }

        [Test]
        public void OneLargeStepCanCrossSeveralPhases()
        {
            DayNightClock clock = Make();
            clock.Advance(13f);
            Assert.AreEqual(DayPhase.Night, clock.Phase);
            Assert.AreEqual(1f, clock.Elapsed, 0.0001f);
        }

        [Test]
        public void DarknessRampsThroughDuskAndDawn()
        {
            DayNightClock clock = Make();
            clock.Advance(11f);
            Assert.AreEqual(0.5f, clock.Darkness, 0.0001f);
            clock.Advance(1f + 8f);
            Assert.AreEqual(DayPhase.Dawn, clock.Phase);
            Assert.AreEqual(1f, clock.Darkness, 0.0001f);
            clock.Advance(1f);
            Assert.AreEqual(0.5f, clock.Darkness, 0.0001f);
        }

        [Test]
        public void SkipAndRestartWork()
        {
            DayNightClock clock = Make();
            clock.SkipPhase();
            Assert.AreEqual(DayPhase.Dusk, clock.Phase);
            clock.Advance(100f);
            clock.Restart();
            Assert.AreEqual(DayPhase.Day, clock.Phase);
            Assert.AreEqual(1, clock.Day);
            Assert.AreEqual(0f, clock.Elapsed);
        }

        [Test]
        public void WorldDaysGrowsMonotonically()
        {
            DayNightClock clock = Make();
            float last = clock.WorldDays;
            for (int i = 0; i < 100; i++)
            {
                clock.Advance(0.7f);
                Assert.GreaterOrEqual(clock.WorldDays, last);
                last = clock.WorldDays;
            }
            Assert.AreEqual(1f, new DayNightClock(10f, 2f, 8f, 2f).WorldDays + 1f, 0.0001f);
        }
    }

    public class ElementBuffRulesTests
    {
        private const int Cap = 15;

        [Test]
        public void KillLoadsTheAxeWithFifteenHits()
        {
            int charges = ElementBuffRules.LoadCharges(0, Element.Fire, 15, Cap);
            Assert.AreEqual(15, ElementBuffRules.GetCharges(charges, Element.Fire));
            Assert.AreEqual(0, ElementBuffRules.GetCharges(charges, Element.Water));
        }

        [Test]
        public void LoadingNeverExceedsTheCapOrLowersACharge()
        {
            int charges = ElementBuffRules.LoadCharges(0, Element.Water, 40, Cap);
            Assert.AreEqual(Cap, ElementBuffRules.GetCharges(charges, Element.Water));
            charges = ElementBuffRules.ConsumeHit(charges, Element.Fire, false);
            Assert.AreEqual(14, ElementBuffRules.GetCharges(charges, Element.Water));
            charges = ElementBuffRules.LoadCharges(charges, Element.Water, 5, Cap);
            Assert.AreEqual(14, ElementBuffRules.GetCharges(charges, Element.Water));
            charges = ElementBuffRules.LoadCharges(charges, Element.Water, 15, Cap);
            Assert.AreEqual(15, ElementBuffRules.GetCharges(charges, Element.Water));
        }

        [Test]
        public void NeutralKillsLoadNothing()
        {
            Assert.AreEqual(0, ElementBuffRules.LoadCharges(0, Element.Neutral, 15, Cap));
            Assert.AreEqual(0, ElementBuffRules.GetCharges(12345, Element.Neutral));
        }

        [Test]
        public void FifteenHitsEmptyTheAxe()
        {
            int charges = ElementBuffRules.LoadCharges(0, Element.Earth, 15, Cap);
            for (int i = 0; i < 15; i++)
            {
                Assert.IsTrue(ElementBuffRules.HasAny(charges), $"empty after only {i} hits");
                charges = ElementBuffRules.ConsumeHit(charges, Element.Neutral, false);
            }
            Assert.IsFalse(ElementBuffRules.HasAny(charges));
            Assert.AreEqual(charges, ElementBuffRules.ConsumeHit(charges, Element.Neutral, false));
        }

        [Test]
        public void EveryChargedElementLosesOneChargePerHit()
        {
            int charges = ElementBuffRules.LoadCharges(0, Element.Water, 15, Cap);
            charges = ElementBuffRules.LoadCharges(charges, Element.Wind, 10, Cap);
            charges = ElementBuffRules.ConsumeHit(charges, Element.Fire, false);
            Assert.AreEqual(14, ElementBuffRules.GetCharges(charges, Element.Water));
            Assert.AreEqual(9, ElementBuffRules.GetCharges(charges, Element.Wind));
        }

        [Test]
        public void EffectiveOnlyModeSpendsJustTheElementsThatBeatTheEnemy()
        {
            int charges = ElementBuffRules.LoadCharges(0, Element.Water, 15, Cap);
            charges = ElementBuffRules.LoadCharges(charges, Element.Wind, 15, Cap);
            // Water beats Fire, Wind does not.
            charges = ElementBuffRules.ConsumeHit(charges, Element.Fire, true);
            Assert.AreEqual(14, ElementBuffRules.GetCharges(charges, Element.Water));
            Assert.AreEqual(15, ElementBuffRules.GetCharges(charges, Element.Wind));
            // Nothing beats Neutral, so nothing is spent.
            Assert.AreEqual(charges, ElementBuffRules.ConsumeHit(charges, Element.Neutral, true));
        }

        [Test]
        public void ChargeBoostsOnlyEnemiesItBeats()
        {
            int charges = ElementBuffRules.LoadCharges(0, Element.Water, 15, Cap);
            Assert.AreEqual(1.5f, ElementBuffRules.DamageMultiplier(charges, Element.Fire, 0.5f), 0.0001f);
            Assert.AreEqual(1f, ElementBuffRules.DamageMultiplier(charges, Element.Water, 0.5f), 0.0001f);
            Assert.AreEqual(1f, ElementBuffRules.DamageMultiplier(charges, Element.Earth, 0.5f), 0.0001f);
            Assert.AreEqual(1f, ElementBuffRules.DamageMultiplier(charges, Element.Neutral, 0.5f), 0.0001f);
            Assert.AreEqual(1f, ElementBuffRules.DamageMultiplier(0, Element.Fire, 0.5f), 0.0001f);
        }

        [Test]
        public void SeveralChargesDoNotStack()
        {
            int charges = ElementBuffRules.LoadCharges(0, Element.Water, 15, Cap);
            charges = ElementBuffRules.LoadCharges(charges, Element.Earth, 15, Cap);
            Assert.AreEqual(1.5f, ElementBuffRules.DamageMultiplier(charges, Element.Fire, 0.5f), 0.0001f);
            Assert.AreEqual(1.5f, ElementBuffRules.DamageMultiplier(charges, Element.Water, 0.5f), 0.0001f);
        }

        [Test]
        public void StrongestPicksTheMostCharges()
        {
            Assert.AreEqual(Element.Neutral, ElementBuffRules.Strongest(0));
            int charges = ElementBuffRules.LoadCharges(0, Element.Fire, 4, Cap);
            charges = ElementBuffRules.LoadCharges(charges, Element.Wind, 9, Cap);
            Assert.AreEqual(Element.Wind, ElementBuffRules.Strongest(charges));
        }

        [Test]
        public void AStoneCountersItsElementForGoodWithoutCharges()
        {
            int stones = ElementBuffRules.AddStone(0, Element.Water);
            Assert.IsTrue(ElementBuffRules.HasStone(stones, Element.Water));
            Assert.IsFalse(ElementBuffRules.HasStone(stones, Element.Fire));
            // Water beats Fire, and only Fire.
            Assert.AreEqual(1.5f, ElementBuffRules.DamageMultiplier(0, Element.Fire, 0.5f, stones), 0.0001f);
            Assert.AreEqual(1f, ElementBuffRules.DamageMultiplier(0, Element.Wind, 0.5f, stones), 0.0001f);
            // A stone and a charge of the same counter still do not stack.
            int charges = ElementBuffRules.LoadCharges(0, Element.Water, 15, Cap);
            Assert.AreEqual(1.5f, ElementBuffRules.DamageMultiplier(charges, Element.Fire, 0.5f, stones), 0.0001f);
        }

        [Test]
        public void StonesAreOneBitPerElementAndNeutralHasNone()
        {
            int stones = 0;
            foreach (Element element in new[] { Element.Water, Element.Fire, Element.Earth, Element.Wind }) stones = ElementBuffRules.AddStone(stones, element);
            Assert.AreEqual(stones, ElementBuffRules.AddStone(stones, Element.Neutral));
            Assert.IsFalse(ElementBuffRules.HasStone(stones, Element.Neutral));
            Assert.AreEqual(Element.Water, ElementBuffRules.FirstStone(stones));
            Assert.AreEqual(Element.Neutral, ElementBuffRules.FirstStone(0));
            // Every element is countered once all four stones are set.
            foreach (Element enemy in new[] { Element.Water, Element.Fire, Element.Earth, Element.Wind })
            {
                Assert.AreEqual(1.5f, ElementBuffRules.DamageMultiplier(0, enemy, 0.5f, stones), 0.0001f);
            }
        }
    }

    public class DropRulesTests
    {
        [Test]
        public void SaplingRollsStayInRangeAndAverageOut()
        {
            var rng = new System.Random(7);
            int total = 0;
            const int rolls = 20000;
            for (int i = 0; i < rolls; i++)
            {
                int n = DropRules.RollSaplings(rng, 2, 1f);
                Assert.That(n, Is.InRange(0, 2));
                total += n;
            }
            Assert.AreEqual(1.0, total / (double)rolls, 0.05);
        }

        [Test]
        public void MultiplierScalesTheAverage()
        {
            var rng = new System.Random(11);
            int total = 0;
            const int rolls = 20000;
            for (int i = 0; i < rolls; i++) total += DropRules.RollSaplings(rng, 1, 1.4f);
            Assert.AreEqual(0.7, total / (double)rolls, 0.05);
        }

        [Test]
        public void ZeroMaxNeverDrops()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 100; i++) Assert.AreEqual(0, DropRules.RollSaplings(rng, 0, 2f));
        }

        [Test]
        public void UpgradeCostsStopAtMaxLevel()
        {
            int[] costs = { 30, 60, 120 };
            Assert.IsTrue(DropRules.TryGetNextCost(costs, 0, out int first));
            Assert.AreEqual(30, first);
            Assert.IsTrue(DropRules.TryGetNextCost(costs, 2, out int last));
            Assert.AreEqual(120, last);
            Assert.IsFalse(DropRules.TryGetNextCost(costs, 3, out _));
        }

        [Test]
        public void LookupClampsToTable()
        {
            float[] table = { 1f, 1.5f, 2f };
            Assert.AreEqual(1f, DropRules.Lookup(table, -1));
            Assert.AreEqual(1.5f, DropRules.Lookup(table, 1));
            Assert.AreEqual(2f, DropRules.Lookup(table, 9));
        }
    }

    public class LivesRulesTests
    {
        [Test]
        public void InfiniteLivesNeverRunOut()
        {
            int lives = 3;
            for (int i = 0; i < 20; i++) lives = LivesRules.AfterFall(lives, finite: false);
            Assert.AreEqual(3, lives);
            Assert.IsFalse(LivesRules.IsOut(0, finite: false));
        }

        [Test]
        public void FiniteLivesCountDownAndStopAtZero()
        {
            int lives = 3;
            lives = LivesRules.AfterFall(lives, true);
            Assert.AreEqual(2, lives);
            lives = LivesRules.AfterFall(lives, true);
            lives = LivesRules.AfterFall(lives, true);
            Assert.AreEqual(0, lives);
            Assert.IsTrue(LivesRules.IsOut(lives, true));
            Assert.AreEqual(0, LivesRules.AfterFall(lives, true));
            Assert.IsFalse(LivesRules.IsOut(1, true));
        }
    }
}
