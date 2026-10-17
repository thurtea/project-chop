using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests
{
    public class HomeRulesTests
    {
        private static int FireDone()
        {
            int mask = 0;
            mask = ZoneGoalRules.Mark(mask, Element.Fire, ZoneGoal.Fish);
            mask = ZoneGoalRules.Mark(mask, Element.Fire, ZoneGoal.Gem);
            return ZoneGoalRules.Mark(mask, Element.Fire, ZoneGoal.Warden);
        }

        [Test]
        public void AHomeNeedsAnUnlockedForestOpenGroundAndTheCost()
        {
            int mask = FireDone();
            Assert.AreEqual(HomeRaiseResult.Ok, HomeRules.CanRaise(mask, Element.Fire, false, false, 30, 5, 30, 5));
            Assert.AreEqual(HomeRaiseResult.InHub, HomeRules.CanRaise(mask, Element.Neutral, false, false, 99, 99, 30, 5));
            Assert.AreEqual(HomeRaiseResult.Locked, HomeRules.CanRaise(mask, Element.Water, false, false, 99, 99, 30, 5));
            Assert.AreEqual(HomeRaiseResult.Underground, HomeRules.CanRaise(mask, Element.Fire, false, true, 99, 99, 30, 5));
            Assert.AreEqual(HomeRaiseResult.CannotAfford, HomeRules.CanRaise(mask, Element.Fire, false, false, 29, 5, 30, 5));
            Assert.AreEqual(HomeRaiseResult.CannotAfford, HomeRules.CanRaise(mask, Element.Fire, false, false, 30, 4, 30, 5));
        }

        [Test]
        public void OneHomePerForest()
        {
            Assert.AreEqual(HomeRaiseResult.AlreadyHome, HomeRules.CanRaise(FireDone(), Element.Fire, true, false, 99, 99, 30, 5));
        }

        [Test]
        public void DamageWearsTheHeartDownUntilItBreaks()
        {
            float health = HomeRules.AfterDamage(600f, 250f);
            Assert.AreEqual(350f, health, 0.001f);
            Assert.IsFalse(HomeRules.Destroyed(health));
            Assert.AreEqual(350f, HomeRules.AfterDamage(health, -10f), 0.001f);
            Assert.IsTrue(HomeRules.Destroyed(HomeRules.AfterDamage(health, 350f)));
        }
    }
}
