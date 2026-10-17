using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests
{
    public class ZoneGoalRulesTests
    {
        [Test]
        public void EachZoneHasItsOwnRareFishAndGem()
        {
            Assert.AreEqual(FishKind.StormEel, ZoneGoalRules.RareFish(Element.Water));
            Assert.AreEqual(FishKind.SteamSalmon, ZoneGoalRules.RareFish(Element.Fire));
            Assert.AreEqual(FishKind.FloodCarp, ZoneGoalRules.RareFish(Element.Earth));
            Assert.AreEqual(FishKind.SquallTuna, ZoneGoalRules.RareFish(Element.Wind));
            Assert.AreEqual(MineFindKind.Aquamarine, ZoneGoalRules.Gem(Element.Water));
            Assert.AreEqual(MineFindKind.FireOpal, ZoneGoalRules.Gem(Element.Fire));
            Assert.AreEqual(MineFindKind.Amber, ZoneGoalRules.Gem(Element.Earth));
            Assert.AreEqual(MineFindKind.StormCrystal, ZoneGoalRules.Gem(Element.Wind));
        }

        [Test]
        public void AllThreeGoalsUnlockOnlyThatZonesHome()
        {
            int mask = 0;
            mask = ZoneGoalRules.Mark(mask, Element.Fire, ZoneGoal.Fish);
            mask = ZoneGoalRules.Mark(mask, Element.Fire, ZoneGoal.Gem);
            Assert.IsFalse(ZoneGoalRules.HomeUnlocked(mask, Element.Fire));
            Assert.AreEqual(2, ZoneGoalRules.Count(mask, Element.Fire));
            mask = ZoneGoalRules.Mark(mask, Element.Fire, ZoneGoal.Warden);
            Assert.IsTrue(ZoneGoalRules.HomeUnlocked(mask, Element.Fire));
            Assert.IsFalse(ZoneGoalRules.HomeUnlocked(mask, Element.Water));
            Assert.AreEqual(0, ZoneGoalRules.Count(mask, Element.Wind));
        }

        [Test]
        public void TheHubHasNoGoalsAndOtherCatchesCountForNothing()
        {
            Assert.AreEqual(0, ZoneGoalRules.Mark(0, Element.Neutral, ZoneGoal.Fish));
            Assert.IsFalse(ZoneGoalRules.HomeUnlocked(~0, Element.Neutral));
            Assert.IsFalse(ZoneGoalRules.ZoneOfFish(FishKind.Perch, out _));
            Assert.IsTrue(ZoneGoalRules.ZoneOfFish(FishKind.SquallTuna, out Element zone));
            Assert.AreEqual(Element.Wind, zone);
            Assert.IsTrue(ZoneGoalRules.ZoneOfGem(MineFindKind.Amber, out Element gemZone));
            Assert.AreEqual(Element.Earth, gemZone);
            Assert.IsFalse(ZoneGoalRules.ZoneOfGem(MineFindKind.Quartz, out _));
        }
    }
}
