using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class EnemyWeaponRulesTests
    {
        [Test]
        public void FinalWeaponSetPerElement()
        {
            Assert.AreEqual(EnemyWeapon.Mace, EnemyWeaponRules.For(Element.Earth));
            Assert.AreEqual(EnemyWeapon.Axe, EnemyWeaponRules.For(Element.Wind));
            Assert.AreEqual(EnemyWeapon.Sword, EnemyWeaponRules.For(Element.Fire));
            Assert.AreEqual(EnemyWeapon.Spear, EnemyWeaponRules.For(Element.Water));
        }

        [Test]
        public void OnlyElementalEnemiesAreEnchanted()
        {
            Assert.IsFalse(EnemyWeaponRules.Enchanted(Element.Neutral));
            Assert.IsFalse(ElementAuraRules.Style(Element.Neutral).On);
            foreach (Element e in new[] { Element.Earth, Element.Wind, Element.Fire, Element.Water })
            {
                Assert.IsTrue(EnemyWeaponRules.Enchanted(e));
                Assert.IsTrue(ElementAuraRules.Style(e).On);
            }
        }

        [Test]
        public void ArchetypesFeelDifferent()
        {
            WeaponProfile mace = EnemyWeaponRules.Profile(EnemyWeapon.Mace);
            WeaponProfile axe = EnemyWeaponRules.Profile(EnemyWeapon.Axe);
            WeaponProfile sword = EnemyWeaponRules.Profile(EnemyWeapon.Sword);
            WeaponProfile spear = EnemyWeaponRules.Profile(EnemyWeapon.Spear);
            Assert.Greater(mace.Windup, spear.Windup);
            Assert.Less(axe.Windup, sword.Windup);
            Assert.Greater(mace.Damage, sword.Damage);
            Assert.Greater(spear.Reach, sword.Reach);
            Assert.Greater(sword.Arc, axe.Arc);
            Assert.Less(spear.Arc, axe.Arc);
            Assert.Greater(mace.Recover, axe.Recover);
            Assert.AreEqual(2, mace.Take);
            Assert.AreEqual(1, spear.Take);
        }

        [Test]
        public void SpearThrustMissesToTheSideSwordSweepDoesNot()
        {
            Vector3 side = Quaternion.Euler(0f, 55f, 0f) * Vector3.forward * 1.5f;
            Assert.IsFalse(EnemyWeaponRules.Hits(EnemyWeaponRules.Profile(EnemyWeapon.Spear), 2f, Vector3.forward, side, 1f));
            Assert.IsTrue(EnemyWeaponRules.Hits(EnemyWeaponRules.Profile(EnemyWeapon.Sword), 2f, Vector3.forward, side, 1f));
            Assert.IsTrue(EnemyWeaponRules.Hits(EnemyWeaponRules.Profile(EnemyWeapon.Spear), 2f, Vector3.forward, Vector3.forward * 2.9f, 1f));
            Assert.IsFalse(EnemyWeaponRules.Hits(EnemyWeaponRules.Profile(EnemyWeapon.Sword), 2f, Vector3.forward, Vector3.forward * 2.9f, 1f));
        }

        [Test]
        public void NeutralTreesRollAnyOfTheFourPlainWeapons()
        {
            var seen = new System.Collections.Generic.HashSet<EnemyWeapon>();
            for (int roll = -4; roll < 8; roll++) seen.Add(EnemyWeaponRules.Pick(Element.Neutral, roll));
            Assert.AreEqual(4, seen.Count);
            Assert.IsFalse(EnemyWeaponRules.Enchanted(Element.Neutral));
            Assert.AreEqual("Weapon0Spear", EnemyWeaponRules.GroupName(EnemyWeapon.Spear));
        }

        [Test]
        public void ElementalsKeepTheirWeaponWhateverTheRoll()
        {
            for (int roll = 0; roll < 4; roll++)
            {
                Assert.AreEqual(EnemyWeapon.Mace, EnemyWeaponRules.Pick(Element.Earth, roll));
                Assert.AreEqual(EnemyWeapon.Axe, EnemyWeaponRules.Pick(Element.Wind, roll));
                Assert.AreEqual(EnemyWeapon.Sword, EnemyWeaponRules.Pick(Element.Fire, roll));
                Assert.AreEqual(EnemyWeapon.Spear, EnemyWeaponRules.Pick(Element.Water, roll));
                Assert.IsTrue(EnemyWeaponRules.Enchanted(Element.Fire));
            }
        }

        [Test]
        public void MaceSmashHitsAllAroundUpClose()
        {
            Assert.IsTrue(EnemyWeaponRules.Hits(EnemyWeaponRules.Profile(EnemyWeapon.Mace), 2f, Vector3.forward, Vector3.back * 1.1f, 1f));
            Assert.IsFalse(EnemyWeaponRules.Hits(EnemyWeaponRules.Profile(EnemyWeapon.Axe), 2f, Vector3.forward, Vector3.back * 1.1f, 1f));
        }
    }
}
