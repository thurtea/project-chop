using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class StaminaTests
    {
        private static StaminaModel Make() => new StaminaModel(100f, 20f, 1f, 20f);

        [Test]
        public void StartsFull()
        {
            StaminaModel s = Make();
            Assert.AreEqual(100f, s.Current);
            Assert.IsTrue(s.CanSprint);
            Assert.IsFalse(s.Exhausted);
        }

        [Test]
        public void TryUseSpendsWhenAffordable()
        {
            StaminaModel s = Make();
            Assert.IsTrue(s.TryUse(30f));
            Assert.AreEqual(70f, s.Current, 0.0001f);
        }

        [Test]
        public void TryUseFailsWithoutSpendingWhenTooLow()
        {
            StaminaModel s = Make();
            s.Drain(95f);
            Assert.IsFalse(s.TryUse(10f));
            Assert.AreEqual(5f, s.Current, 0.0001f);
        }

        [Test]
        public void RegenWaitsForTheDelay()
        {
            StaminaModel s = Make();
            s.TryUse(50f);
            s.Tick(0.9f);
            Assert.AreEqual(50f, s.Current, 0.0001f);
            s.Tick(0.2f);
            s.Tick(1f);
            Assert.Greater(s.Current, 50f);
        }

        [Test]
        public void EmptyMeansExhaustedUntilRecovered()
        {
            StaminaModel s = Make();
            s.Drain(1000f);
            Assert.IsTrue(s.Exhausted);
            Assert.IsFalse(s.CanSprint);
            Assert.IsFalse(s.TryUse(1f));

            s.Tick(1.01f);
            s.Tick(0.5f);
            Assert.IsTrue(s.Exhausted, "Still exhausted below the recovery threshold.");
            s.Tick(1f);
            Assert.IsFalse(s.Exhausted);
            Assert.IsTrue(s.TryUse(5f));
        }

        [Test]
        public void NeverExceedsMax()
        {
            StaminaModel s = Make();
            s.Tick(1000f);
            Assert.AreEqual(100f, s.Current);
        }

        [Test]
        public void RefillClearsExhaustion()
        {
            StaminaModel s = Make();
            s.Drain(1000f);
            s.Refill();
            Assert.AreEqual(100f, s.Current);
            Assert.IsFalse(s.Exhausted);
        }
    }

    public class CraftRulesTests
    {
        [Test]
        public void NeedsEnoughWoodAndRoom()
        {
            Assert.IsTrue(CraftRules.CanCraft(10, 3, 0, 9));
            Assert.IsTrue(CraftRules.CanCraft(3, 3, 8, 9));
            Assert.IsFalse(CraftRules.CanCraft(2, 3, 0, 9));
            Assert.IsFalse(CraftRules.CanCraft(10, 3, 9, 9));
            Assert.IsFalse(CraftRules.CanCraft(10, -1, 0, 9));
        }

        [Test]
        public void BandageNeedsHerbsAndFabric()
        {
            Assert.IsTrue(CraftRules.CanCraft(2, 2, 1, 1, 0, 9));
            Assert.IsFalse(CraftRules.CanCraft(2, 2, 0, 1, 0, 9));
            Assert.IsFalse(CraftRules.CanCraft(1, 2, 5, 1, 0, 9));
            Assert.IsFalse(CraftRules.CanCraft(2, 2, 1, 1, 9, 9));
        }
    }
}
