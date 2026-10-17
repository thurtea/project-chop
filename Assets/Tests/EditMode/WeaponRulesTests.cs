using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class WeaponRulesTests
    {
        [Test]
        public void EachWeaponPlaysItsOwnArchetype()
        {
            Assert.AreEqual(SwingTool.AxeFight, WeaponRules.Tool(PlayerWeapon.Axe));
            Assert.AreEqual(SwingTool.Sword, WeaponRules.Tool(PlayerWeapon.Sword));
            Assert.AreEqual(SwingTool.Mace, WeaponRules.Tool(PlayerWeapon.Mace));
            Assert.AreEqual(SwingTool.Spear, WeaponRules.Tool(PlayerWeapon.Spear));
            Assert.Greater(WeaponRules.Stats(PlayerWeapon.Sword).Targets, 1);
            Assert.Greater(WeaponRules.Stats(PlayerWeapon.Spear).RangeScale, 1.3f);
            Assert.Greater(WeaponRules.Stats(PlayerWeapon.Mace).Damage, WeaponRules.Stats(PlayerWeapon.Axe).Damage);
            Assert.Greater(WeaponRules.Stats(PlayerWeapon.Mace).CooldownScale, 1.2f);
            Assert.Greater(WeaponRules.Stats(PlayerWeapon.Mace).SplashRadius, 0f);
        }

        [Test]
        public void OnlyTheAxeChopsTrees()
        {
            Assert.IsTrue(WeaponRules.ChopsTrees(PlayerWeapon.Axe));
            Assert.IsFalse(WeaponRules.ChopsTrees(PlayerWeapon.Sword));
            Assert.IsFalse(WeaponRules.ChopsTrees(PlayerWeapon.Mace));
            Assert.IsFalse(WeaponRules.ChopsTrees(PlayerWeapon.Spear));
        }

        [Test]
        public void NextCyclesOwnedWeaponsAndAlwaysHasTheAxe()
        {
            int mask = WeaponRules.Grant(0, PlayerWeapon.Mace);
            Assert.AreEqual(PlayerWeapon.Mace, WeaponRules.Next(mask, PlayerWeapon.Axe));
            Assert.AreEqual(PlayerWeapon.Axe, WeaponRules.Next(mask, PlayerWeapon.Mace));
            Assert.AreEqual(PlayerWeapon.Axe, WeaponRules.Next(0, PlayerWeapon.Axe));
            Assert.IsTrue(WeaponRules.Owns(0, PlayerWeapon.Axe));
            Assert.IsFalse(WeaponRules.Owns(WeaponRules.Take(mask, PlayerWeapon.Mace), PlayerWeapon.Mace));
        }

        [Test]
        public void WeaponItemsRoundTrip()
        {
            foreach (PlayerWeapon w in new[] { PlayerWeapon.Sword, PlayerWeapon.Mace, PlayerWeapon.Spear })
            {
                int item = WeaponRules.ItemOf(w);
                Assert.IsTrue(ItemRules.IsWeapon(item));
                Assert.IsTrue(ItemRules.Valid(item));
                Assert.AreEqual(w, WeaponRules.FromItem(item));
            }
            Assert.IsFalse(ItemRules.IsWeapon(ItemRules.SharkRod));
            Assert.IsFalse(ItemRules.IsFind(ItemRules.Sword));
        }

        [Test]
        public void RecipesComeFromConfigAtTheWorkbench()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            Recipe sword = RecipeBook.Get(RecipeBook.Sword, config);
            Recipe mace = RecipeBook.Get(RecipeBook.Mace, config);
            Recipe spear = RecipeBook.Get(RecipeBook.Spear, config);
            Assert.AreEqual(CraftStation.Workbench, sword.Station);
            Assert.AreEqual(ItemRules.Sword, (int)sword.Output);
            Assert.AreEqual(ItemRules.Mace, (int)mace.Output);
            Assert.AreEqual(ItemRules.Spear, (int)spear.Output);
            Assert.AreEqual(config.SwordWoodCost, sword.Inputs[0].Amount);
            Assert.AreEqual(config.SwordIronCost, sword.Inputs[1].Amount);
            Assert.AreEqual(8, mace.Inputs[1].Amount);
            Assert.AreEqual(10, spear.Inputs[0].Amount);
            Assert.AreEqual(7, RecipeBook.Count);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void MaceSplashReachesNeighboursOnly()
        {
            Assert.Greater(WeaponRules.SplashUnits(PlayerWeapon.Mace, 10f, 1f), 0f);
            Assert.AreEqual(0f, WeaponRules.SplashUnits(PlayerWeapon.Mace, 10f, 3f));
            Assert.AreEqual(0f, WeaponRules.SplashUnits(PlayerWeapon.Sword, 10f, 1f));
        }
    }
}
