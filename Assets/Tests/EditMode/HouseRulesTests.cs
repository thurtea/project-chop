using System.Collections.Generic;
using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    // The house: crafting happens at the workbench (bandages anywhere), the shared chest store, tools that wear out,
    // fish as food, sleeping through the night and the quiet hub.
    public class HouseRulesTests
    {
        [Test]
        public void BandagesAreMadeAnywhere()
        {
            Assert.AreEqual(CraftStation.Anywhere, RecipeBook.StationFor(RecipeBook.Bandage));
            Assert.IsTrue(RecipeBook.StationAllows(RecipeBook.Bandage, false));
            Assert.IsTrue(RecipeBook.StationAllows(RecipeBook.Bandage, true));
        }

        [Test]
        public void EverythingElseNeedsTheWorkbench()
        {
            for (int id = 0; id < RecipeBook.Count; id++)
            {
                if (id == RecipeBook.Bandage) continue;
                Assert.AreEqual(CraftStation.Workbench, RecipeBook.StationFor(id), RecipeBook.Get(id, null).Name);
                Assert.IsFalse(RecipeBook.StationAllows(id, false), RecipeBook.Get(id, null).Name + " away from a bench");
                Assert.IsTrue(RecipeBook.StationAllows(id, true), RecipeBook.Get(id, null).Name + " at a bench");
            }
            Assert.IsFalse(RecipeBook.UpgradeAllowed(false), "upgrades, the forge and repairs need a bench");
            Assert.IsTrue(RecipeBook.UpgradeAllowed(true));
            Assert.IsFalse(RecipeBook.StationAllows(RecipeBook.Count, true), "an unknown recipe is never allowed");
        }

        [Test]
        public void RecipesNeedEveryIngredientAndRoom()
        {
            Recipe bandage = RecipeBook.Get(RecipeBook.Bandage, null);
            Assert.AreEqual(ItemKind.Bandage, bandage.Output);
            Assert.AreEqual(2, bandage.Inputs.Length);
            int herbs = 2, fiber = 0;
            System.Func<ItemKind, int> have = kind => kind == ItemKind.Herb ? herbs : kind == ItemKind.Fiber ? fiber : 0;
            Assert.IsFalse(RecipeBook.CanAfford(bandage, have, 0, 9), "herbs alone are not enough");
            fiber = 1;
            Assert.IsTrue(RecipeBook.CanAfford(bandage, have, 0, 9));
            Assert.IsFalse(RecipeBook.CanAfford(bandage, have, 9, 9), "a full stack has no room");
            Recipe torch = RecipeBook.Get(RecipeBook.Torch, null);
            Assert.AreEqual(ItemKind.Wood, torch.Inputs[0].Kind);
        }

        [Test]
        public void TheChestMovesOnlyWhatThereIsAndWhatFits()
        {
            int bag = 12, chest = 0;
            Assert.AreEqual(12, ItemRules.Transfer(ref bag, ref chest, ItemRules.ChestCap, 999));
            Assert.AreEqual(0, bag);
            Assert.AreEqual(12, chest);

            // Taking out into a bag that can hold 20 and has 15 already: 5 fit.
            bag = 15;
            Assert.AreEqual(5, ItemRules.Transfer(ref chest, ref bag, 20, 999));
            Assert.AreEqual(20, bag);
            Assert.AreEqual(7, chest);

            Assert.AreEqual(0, ItemRules.Movable(0, 0, 10, 5), "nothing moves from an empty stack");
            Assert.AreEqual(0, ItemRules.Movable(5, 0, 10, 0), "asking for none moves none");
            Assert.AreEqual(0, ItemRules.Movable(5, 0, 10, -3), "a negative ask moves none");
            Assert.AreEqual(3, ItemRules.Movable(3, 0, 10, 5), "never more than the giver has");
        }

        [Test]
        public void TheChestHoldsEveryCarriedKind()
        {
            Assert.AreEqual(ItemRules.FirstFind, (int)ItemKind.FirstFish + FishingRules.Kinds);
            Assert.AreEqual(ItemRules.FirstFind + MiningRules.Kinds + 2 + WeaponRules.Count - 1, ItemRules.Count);
            Assert.IsTrue(ItemRules.IsFish(ItemRules.Fish(FishKind.Pike)));
            Assert.AreEqual(FishKind.Pike, ItemRules.FishOf(ItemRules.Fish(FishKind.Pike)));
            Assert.IsTrue(ItemRules.IsFind(ItemRules.Find(MineFindKind.IronOre)));
            Assert.AreEqual(MineFindKind.OldPickHead, ItemRules.FindOf(ItemRules.Find(MineFindKind.OldPickHead)));
            Assert.IsFalse(ItemRules.IsFish(ItemRules.Find(MineFindKind.Quartz)));
            Assert.IsFalse(ItemRules.IsFind(ItemRules.Fish(FishKind.Pike)));
            foreach (Element element in EssenceRules.Elements)
            {
                int kind = ItemRules.Essence(element);
                Assert.IsTrue(ItemRules.IsEssence(kind));
                Assert.AreEqual(element, ItemRules.ElementOf(kind));
            }
            Assert.IsFalse(ItemRules.IsFish((int)ItemKind.Wood));
            Assert.IsFalse(ItemRules.Valid(ItemRules.Count));
            Assert.IsFalse(ItemRules.Valid(-1));
        }

        [Test]
        public void ToolsWearBreakAndAreSavedAsWear()
        {
            int axe = 3;
            axe = ToolRules.Wear(axe, 1);
            Assert.AreEqual(2, axe);
            axe = ToolRules.Wear(axe, 5);
            Assert.AreEqual(0, axe);
            Assert.IsTrue(ToolRules.Broken(axe));
            Assert.AreEqual(0, ToolRules.Wear(0, 1), "never below zero");
            Assert.IsTrue(ToolRules.Worn(399, 400));
            Assert.IsFalse(ToolRules.Worn(400, 400));
            Assert.AreEqual(0.5f, ToolRules.Fraction(200, 400), 0.0001f);
            // Saves keep the wear: an older save without the field (0) loads a whole tool.
            Assert.AreEqual(400, ToolRules.FromWear(0, 400));
            Assert.AreEqual(380, ToolRules.FromWear(ToolRules.ToWear(380, 400), 400));
            Assert.AreEqual(0, ToolRules.FromWear(999, 400));
        }

        [Test]
        public void FishAreAMealForStamina()
        {
            Assert.AreEqual(10, FoodRules.StaminaBonus(20, 0.5f));
            Assert.AreEqual(50, FoodRules.StaminaBonus(100, 0.5f));
            int bonus = 0;
            float left = 0f;
            FoodRules.Eat(ref bonus, ref left, 10, 300f);
            Assert.AreEqual(10, bonus);
            // A smaller fish while fed keeps the better boost and restarts the timer.
            FoodRules.Tick(ref bonus, ref left, 100f);
            FoodRules.Eat(ref bonus, ref left, 5, 300f);
            Assert.AreEqual(10, bonus);
            Assert.AreEqual(300f, left, 0.001f);
            FoodRules.Tick(ref bonus, ref left, 301f);
            Assert.AreEqual(0, bonus, "the meal wears off");
            FoodRules.Eat(ref bonus, ref left, 5, 300f);
            Assert.AreEqual(5, bonus, "after it wore off the new fish counts on its own");
        }

        [Test]
        public void SleepingIsForDuskAndNightAndNeedsEveryone()
        {
            Assert.IsFalse(SleepRules.CanSleep(DayPhase.Day));
            Assert.IsTrue(SleepRules.CanSleep(DayPhase.Dusk));
            Assert.IsTrue(SleepRules.CanSleep(DayPhase.Night));
            Assert.IsFalse(SleepRules.CanSleep(DayPhase.Dawn));
            Assert.IsTrue(SleepRules.Everyone(2, 2));
            Assert.IsFalse(SleepRules.Everyone(1, 2));
            Assert.IsFalse(SleepRules.Everyone(0, 0), "an empty world never skips the night");
        }

        [Test]
        public void OnlyForestTreesPlantSaplings()
        {
            Assert.IsFalse(NightRules.PlantsSaplings(Element.Neutral), "the hub stays quiet");
            foreach (Element element in EssenceRules.Elements) Assert.IsTrue(NightRules.PlantsSaplings(element));
        }
    }

    // Building pieces on the 2 m grid (Valheim style).
    public class BuildRulesTests
    {
        [Test]
        public void YawSnapsToQuarterTurns()
        {
            Assert.AreEqual(0f, BuildRules.SnapYaw(10f));
            Assert.AreEqual(90f, BuildRules.SnapYaw(80f));
            Assert.AreEqual(270f, BuildRules.SnapYaw(-90f));
            Assert.AreEqual(0f, BuildRules.SnapYaw(360f));
            Assert.IsTrue(BuildRules.Turned(90f));
            Assert.IsTrue(BuildRules.Turned(270f));
            Assert.IsFalse(BuildRules.Turned(180f));
        }

        [Test]
        public void AWallSnapsOntoAFloorEdge()
        {
            // A floor at the origin: its top corners at y 0.2.
            Vector3[] floor = { new Vector3(-1f, 0.2f, -1f), new Vector3(1f, 0.2f, -1f), new Vector3(-1f, 0.2f, 1f), new Vector3(1f, 0.2f, 1f) };
            var targets = new List<Vector3>();
            foreach (Vector3 p in floor) targets.Add(BuildRules.WorldPoint(Vector3.zero, 0f, p));
            Vector3[] wall = { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(-1f, 2f, 0f), new Vector3(1f, 2f, 0f) };
            // Aimed a little off the floor's front edge and a little low: the nearest join puts the wall on that edge,
            // sharing the floor's center so the corners meet. This floor sits at the origin, not on a cell center.
            List<Vector3> moves = BuildRules.SnapMoves(new Vector3(0.3f, 0.05f, -0.8f), 0f, wall, targets);
            Assert.IsTrue(moves.Count > 0);
            Vector3 placed = new Vector3(0.3f, 0.05f, -0.8f) + moves[0];
            Assert.AreEqual(0.2f, placed.y, 0.0001f);
            Assert.AreEqual(-1f, placed.z, 0.0001f);
            Assert.AreEqual(0f, placed.x, 0.0001f);
        }

        [Test]
        public void NothingSnapsFromTooFarAway()
        {
            var targets = new List<Vector3> { Vector3.zero };
            Assert.AreEqual(0, BuildRules.SnapMoves(new Vector3(5f, 0f, 5f), 0f, new[] { Vector3.zero }, targets).Count);
            // A turned piece's points turn with it.
            Vector3 turned = BuildRules.WorldPoint(Vector3.zero, 90f, new Vector3(1f, 0f, 0f));
            Assert.AreEqual(-1f, turned.z, 0.0001f);
        }

        [Test]
        public void CellsAndEdgesSnapToTheGrid()
        {
            Vector3 floor = BuildRules.Snap(new Vector3(0.3f, 1.02f, 3.9f), PieceSnap.Cell, 0f);
            Assert.AreEqual(1f, floor.x, 0.0001f);
            Assert.AreEqual(3f, floor.z, 0.0001f);
            Assert.AreEqual(1f, floor.y, 0.0001f);
            // A wall along X sits on a grid line in z, centered on a cell in x.
            Vector3 wall = BuildRules.Snap(new Vector3(0.3f, 0f, 3.9f), PieceSnap.Edge, 0f);
            Assert.AreEqual(1f, wall.x, 0.0001f);
            Assert.AreEqual(4f, wall.z, 0.0001f);
            // Turned, the other way round.
            Vector3 turned = BuildRules.Snap(new Vector3(0.3f, 0f, 3.9f), PieceSnap.Edge, 90f);
            Assert.AreEqual(0f, turned.x, 0.0001f);
            Assert.AreEqual(3f, turned.z, 0.0001f);
            Vector3 free = BuildRules.Snap(new Vector3(0.3f, 0f, 3.9f), PieceSnap.Free, 0f);
            Assert.AreEqual(0.5f, free.x, 0.0001f);
            Assert.AreEqual(4f, free.z, 0.0001f);
        }

        [Test]
        public void PiecesThatTouchDoNotOverlap()
        {
            var floorSize = new Vector3(2f, 0.2f, 2f);
            Bounds a = BuildRules.BoundsOf(new Vector3(1f, 0f, 1f), floorSize, 0f);
            Bounds b = BuildRules.BoundsOf(new Vector3(3f, 0f, 1f), floorSize, 0f);
            Assert.IsFalse(BuildRules.Overlaps(a, b), "side by side floors touch");
            Assert.IsTrue(BuildRules.Touches(a, b));
            Bounds same = BuildRules.BoundsOf(new Vector3(1f, 0f, 1f), floorSize, 0f);
            Assert.IsTrue(BuildRules.Overlaps(a, same), "two floors in one cell overlap");
            // A wall standing on the floor's edge touches it without overlapping.
            Bounds wall = BuildRules.BoundsOf(new Vector3(1f, 0.2f, 2f), new Vector3(2f, 2f, 0.3f), 0f);
            Assert.IsFalse(BuildRules.Overlaps(a, wall));
            Assert.IsTrue(BuildRules.Touches(a, wall));
            // A turned wall swaps its width and depth.
            Bounds turned = BuildRules.BoundsOf(Vector3.zero, new Vector3(2f, 2f, 0.3f), 90f);
            Assert.AreEqual(0.3f, turned.size.x, 0.0001f);
            Assert.AreEqual(2f, turned.size.z, 0.0001f);
        }

        [Test]
        public void NothingFloats()
        {
            Assert.IsTrue(BuildRules.Supported(10.2f, 10f, false), "on the ground");
            Assert.IsFalse(BuildRules.Supported(14f, 10f, false), "in the air");
            Assert.IsTrue(BuildRules.Supported(14f, 10f, true), "held up by another piece");
            Assert.IsFalse(BuildRules.TooSteep(1f));
            Assert.IsTrue(BuildRules.TooSteep(Mathf.Cos(50f * Mathf.Deg2Rad)));
        }

        [Test]
        public void CostsComeBackAndRepairsCostTheirShare()
        {
            Assert.AreEqual(4, BuildRules.Refund(4, 200, 200), "a whole piece gives all of its cost back");
            Assert.AreEqual(2, BuildRules.Refund(4, 100, 200), "a half broken piece gives half");
            Assert.AreEqual(0, BuildRules.Refund(4, 0, 200), "a broken piece gives nothing");
            for (int health = 1; health <= 200; health += 7)
                Assert.AreEqual(4, BuildRules.Refund(4, health, 200) + BuildRules.RepairCost(health, 200, 4), "taking down plus mending is always the full cost");
            Assert.AreEqual(0, BuildRules.RepairCost(200, 200, 4), "a whole piece costs nothing");
            Assert.AreEqual(2, BuildRules.RepairCost(100, 200, 4));
            Assert.AreEqual(1, BuildRules.RepairCost(199, 200, 4), "any damage costs at least one");
            Assert.AreEqual(4, BuildRules.RepairCost(0, 200, 4));
            Assert.IsTrue(BuildRules.InRange(Vector3.zero, new Vector3(3f, 0f, 4f), 5f));
            Assert.IsFalse(BuildRules.InRange(Vector3.zero, new Vector3(3f, 0f, 4.1f), 5f));
        }
    
        // A column of pieces, each touching the one below, the first on the ground.
        private static Bounds[] Column(int count)
        {
            var boxes = new Bounds[count];
            for (int i = 0; i < count; i++) boxes[i] = BuildRules.BoundsOf(new Vector3(0f, i * 2f, 0f), new Vector3(2f, 2f, 0.3f), 0f);
            return boxes;
        }

        [Test]
        public void PiecesCountTheirStepsToTheGround()
        {
            Bounds[] boxes = Column(4);
            int[] steps = BuildRules.SupportSteps(boxes, new[] { true, false, false, false });
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, steps);
            Assert.IsTrue(BuildRules.Stands(steps[3]));
        }

        [Test]
        public void APieceWithNothingUnderItDoesNotStand()
        {
            Bounds[] boxes = Column(3);
            // The bottom one is gone: what was on it has no way to the ground.
            boxes[0] = new Bounds(new Vector3(50f, 0f, 50f), Vector3.one);
            int[] steps = BuildRules.SupportSteps(boxes, new[] { false, false, false });
            Assert.IsFalse(BuildRules.Stands(steps[1]));
            Assert.IsFalse(BuildRules.Stands(steps[2]));
        }

        [Test]
        public void TowersStopAtTheSupportLimit()
        {
            int count = BuildRules.MaxSupportSteps + 3;
            var grounded = new bool[count];
            grounded[0] = true;
            int[] steps = BuildRules.SupportSteps(Column(count), grounded);
            Assert.IsTrue(BuildRules.Stands(steps[BuildRules.MaxSupportSteps]));
            Assert.IsFalse(BuildRules.Stands(steps[BuildRules.MaxSupportSteps + 1]));
        }

        [Test]
        public void ThePieceTakesTheShortestWayDown()
        {
            Bounds[] boxes = Column(4);
            // The top one also touches a second grounded column right beside it.
            var withSide = new Bounds[5];
            System.Array.Copy(boxes, withSide, 4);
            withSide[4] = BuildRules.BoundsOf(new Vector3(0f, 6f, 0.3f), new Vector3(2f, 2f, 0.3f), 0f);
            int[] steps = BuildRules.SupportSteps(withSide, new[] { true, false, false, false, true });
            Assert.AreEqual(1, steps[3]);
        }

        [Test]
        public void ABlockFootprintMaySpanOneHalfBlockStep()
        {
            Assert.IsFalse(BuildRules.TooUneven(2f, 2f));
            Assert.IsFalse(BuildRules.TooUneven(2f, 2.5f));
            Assert.IsTrue(BuildRules.TooUneven(2f, 3f));
        }
    }
}
