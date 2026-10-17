using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class BoatSharkTests
    {
        [Test]
        public void BoatIsAWorkbenchRecipeAfterTheFinds()
        {
            Recipe r = RecipeBook.Get(RecipeBook.Boat, null);
            Assert.AreEqual(CraftStation.Workbench, r.Station);
            Assert.AreEqual(ItemRules.Boat, (int)r.Output);
            Assert.AreEqual(ItemRules.Sword - 2, ItemRules.Boat);
            Assert.IsFalse(ItemRules.IsFind(ItemRules.Boat));
            Assert.IsTrue(ItemRules.IsBoat(ItemRules.Boat));
            Assert.IsFalse(RecipeBook.StationAllows(RecipeBook.Boat, false));
            Assert.IsTrue(RecipeBook.StationAllows(RecipeBook.Boat, true));
        }

        [Test]
        public void BoatDeploysOnlyOnOceanAndPicksUpFromShore()
        {
            Assert.IsTrue(BoatRules.CanDeploy(true, true, false));
            Assert.IsFalse(BoatRules.CanDeploy(true, false, false));
            Assert.IsFalse(BoatRules.CanDeploy(true, true, true));
            Assert.IsTrue(BoatRules.CanPickUp(true, false, 2f, 3f));
            Assert.IsFalse(BoatRules.CanPickUp(true, true, 2f, 3f));
        }

        [Test]
        public void HoldingTheArcCentredTiresTheSharkThenReels()
        {
            SharkTuning t = SharkTuning.Default;
            SharkFight f = SharkFight.Start();
            for (int i = 0; i < 2000 && f.Phase == SharkPhase.Towing; i++)
            {
                float pull = SharkRules.SharkPull(f.Time, 3);
                SharkRules.Step(ref f, -f.Offset * 4f - pull * t.DriftSpeed / t.SteerSpeed, pull, 0.02f, t);
            }
            Assert.AreEqual(SharkPhase.Reeling, f.Phase);
        }

        [Test]
        public void DriftingTooFarLosesTheShark()
        {
            SharkTuning t = SharkTuning.Default;
            SharkFight f = SharkFight.Start();
            for (int i = 0; i < 500 && f.Phase == SharkPhase.Towing; i++) SharkRules.Step(ref f, 1f, 1f, 0.02f, t);
            Assert.AreEqual(SharkPhase.Lost, f.Phase);
            Assert.Greater(f.Stamina, 0.5f);
        }

        [Test]
        public void SharksNeedTheRodAndTheBoatAndUnlockIsUndecided()
        {
            Assert.IsFalse(SharkRules.CanHook(false, true));
            Assert.IsFalse(SharkRules.CanHook(true, false));
            Assert.IsTrue(SharkRules.CanHook(true, true));
            var config = ScriptableObject.CreateInstance<GameConfig>();
            Assert.AreEqual(SharkRodUnlock.Craft, config.SharkRodUnlock);
            Assert.AreEqual(10, config.SharkRodFishToUnlock);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void SharkRodRecipeIsLockedBelowTenFishAndOpenAtTen()
        {
            Assert.IsFalse(RecipeBook.Unlocked(RecipeBook.SharkRod, 9, null));
            Assert.AreEqual("Land 10 fish", RecipeBook.LockHint(RecipeBook.SharkRod, 9, null));
            Assert.IsTrue(RecipeBook.Unlocked(RecipeBook.SharkRod, 10, null));
            Assert.AreEqual(string.Empty, RecipeBook.LockHint(RecipeBook.SharkRod, 10, null));
            Assert.IsTrue(RecipeBook.Unlocked(RecipeBook.Boat, 0, null));
            Recipe r = RecipeBook.Get(RecipeBook.SharkRod, null);
            Assert.AreEqual(ItemRules.SharkRod, (int)r.Output);
            Assert.AreEqual(CraftStation.Workbench, r.Station);
            Assert.AreEqual(ItemRules.Sword - 1, ItemRules.SharkRod);
            Assert.IsFalse(ItemRules.IsFind(ItemRules.SharkRod));
        }

        [Test]
        public void SharkRodUnlockFollowsConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var so = new UnityEditor.SerializedObject(config);
            so.FindProperty("_sharkRodFishToUnlock").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(RecipeBook.Unlocked(RecipeBook.SharkRod, 2, config));
            Assert.IsTrue(RecipeBook.Unlocked(RecipeBook.SharkRod, 3, config));
            Assert.AreEqual("Land 3 fish", RecipeBook.LockHint(RecipeBook.SharkRod, 0, config));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void FishLandedAndSharkRodSurviveSaveAndLoad()
        {
            var record = new Chopper.World.Saving.PlayerRecord { FishLanded = 12, SharkRod = true, Boats = 1 };
            string json = JsonUtility.ToJson(record);
            var back = JsonUtility.FromJson<Chopper.World.Saving.PlayerRecord>(json);
            Assert.AreEqual(12, back.FishLanded);
            Assert.IsTrue(back.SharkRod);
            Assert.AreEqual(1, back.Boats);
            Assert.IsTrue(RecipeBook.Unlocked(RecipeBook.SharkRod, back.FishLanded, null));
            var old = JsonUtility.FromJson<Chopper.World.Saving.PlayerRecord>("{\"FishXp\":5}");
            Assert.AreEqual(0, old.FishLanded);
            Assert.IsFalse(RecipeBook.Unlocked(RecipeBook.SharkRod, old.FishLanded, null));
        }

        [Test]
        public void SharedArcMapsCentreToZero()
        {
            Assert.AreEqual(0f, ArcRules.Angle(0.5f), 1e-4f);
            Assert.AreEqual(0.5f, ArcRules.FromOffset(0f), 1e-4f);
            Assert.AreEqual(-70f, ArcRules.Angle(0f), 1e-4f);
            Assert.IsTrue(ArcRules.Within(0.2f, 0.25f));
        }
    }
}