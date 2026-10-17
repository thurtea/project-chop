using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests
{
    public class CombatRulesTests
    {
        private const float Reduction = 0.75f;
        private const float Window = 0.25f;

        [Test]
        public void BlockCutsDamageButNeverToZero()
        {
            Assert.AreEqual(3, CombatRules.BlockedDamage(12, Reduction));
            Assert.AreEqual(1, CombatRules.BlockedDamage(1, Reduction));
            Assert.AreEqual(0, CombatRules.BlockedDamage(0, Reduction));
        }

        [Test]
        public void StaggeredEnemiesTakeDoubleAndOthersNormal()
        {
            Assert.AreEqual(8f, CombatRules.StaggeredDamage(4f, true, 2f), 0.0001f);
            Assert.AreEqual(4f, CombatRules.StaggeredDamage(4f, false, 2f), 0.0001f);
            Assert.AreEqual(4f, CombatRules.StaggeredDamage(4f, true, 0.5f), 0.0001f);
        }

        [Test]
        public void ShieldInHandBlocksMoreThanTheWeaponAlone()
        {
            Assert.AreEqual(0.9f, CombatRules.BlockReductionFor(true, Reduction, 0.9f), 0.0001f);
            Assert.AreEqual(Reduction, CombatRules.BlockReductionFor(false, Reduction, 0.9f), 0.0001f);
            Assert.Less(CombatRules.BlockedDamage(20, CombatRules.BlockReductionFor(true, Reduction, 0.9f)), CombatRules.BlockedDamage(20, Reduction));
        }

        [Test]
        public void UnguardedHitLandsInFull()
        {
            int dealt = CombatRules.Resolve(10, false, 0f, 0f, 5f, Reduction, Window, out HitOutcome outcome);
            Assert.AreEqual(10, dealt);
            Assert.AreEqual(HitOutcome.Hit, outcome);
        }

        [Test]
        public void LateBlockReducesDamage()
        {
            int dealt = CombatRules.Resolve(12, true, 1f, 0f, 2f, Reduction, Window, out HitOutcome outcome);
            Assert.AreEqual(3, dealt);
            Assert.AreEqual(HitOutcome.Blocked, outcome);
        }

        [Test]
        public void FreshBlockParriesTheHit()
        {
            int dealt = CombatRules.Resolve(12, true, 4.9f, 0f, 5f, Reduction, Window, out HitOutcome outcome);
            Assert.AreEqual(0, dealt);
            Assert.AreEqual(HitOutcome.Parried, outcome);
        }

        [Test]
        public void RollingMakesYouUntouchableEvenWhileBlocking()
        {
            int dealt = CombatRules.Resolve(12, true, 0f, 5.3f, 5f, Reduction, Window, out HitOutcome outcome);
            Assert.AreEqual(0, dealt);
            Assert.AreEqual(HitOutcome.Dodged, outcome);
            dealt = CombatRules.Resolve(12, false, 0f, 5.3f, 5.4f, Reduction, Window, out outcome);
            Assert.AreEqual(12, dealt, "the window has ended");
        }

        [Test]
        public void BigEnemiesAndBossesResistStagger()
        {
            float small = CombatRules.StaggerSeconds(0.5f, 0, false);
            float large = CombatRules.StaggerSeconds(0.5f, 2, false);
            float boss = CombatRules.StaggerSeconds(0.5f, 2, true);
            Assert.Greater(small, large);
            Assert.Greater(large, boss);
            Assert.Greater(boss, 0f);
        }

        [Test]
        public void EnchantedAxeHitsHarderThanPlain()
        {
            Assert.AreEqual(1f, CombatRules.EnchantMultiplier(false, 0.5f), 0.0001f);
            Assert.AreEqual(1.5f, CombatRules.EnchantMultiplier(true, 0.5f), 0.0001f);
        }

        [Test]
        public void ComboAdvancesWithinTheWindowAndResetsAfterwards()
        {
            Assert.AreEqual(0, ComboRules.NextStep(-1, 0.1f, 0.6f), "no previous swing starts the combo");
            Assert.AreEqual(1, ComboRules.NextStep(0, 0.7f, 0.6f));
            Assert.AreEqual(2, ComboRules.NextStep(1, 0.7f, 0.6f));
            Assert.AreEqual(0, ComboRules.NextStep(2, 0.7f, 0.6f), "the finisher wraps to the first strike");
            Assert.AreEqual(0, ComboRules.NextStep(1, 0.6f + ComboRules.WindowSeconds + 0.1f, 0.6f), "too slow resets the combo");
        }

        [Test]
        public void TheFinisherHitsHarderAndRecoversSlower()
        {
            Assert.AreEqual(1f, ComboRules.Damage(0));
            Assert.Greater(ComboRules.Damage(1), 1f);
            Assert.Greater(ComboRules.Damage(2), ComboRules.Damage(1));
            Assert.Greater(ComboRules.CooldownScale(2), ComboRules.CooldownScale(0));
        }
    }
}
