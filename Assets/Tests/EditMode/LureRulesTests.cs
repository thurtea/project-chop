using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests
{
    public class LureRulesTests
    {
        private static readonly FishSpecies[] Table = FishSpecies.Defaults();

        private static SchoolFish Fish(Vector2 at, int rarity = 0)
        {
            return new SchoolFish { Kind = FishKind.Perch, Kg = 0.5f, Position = at, Home = at, Shyness = LureRules.Shyness(rarity) };
        }

        // Runs the school with the lure worked (a gentle twitch every second) and returns the step a fish bit, or -1.
        private static int WorkUntilBite(SchoolFish[] school, ref Vector2 lure, Vector2 shore, float twitchEvery, int seconds)
        {
            var rng = new System.Random(3);
            const float dt = 0.05f;
            float sinceTwitch = 10f;
            float sinceAction = 10f;
            for (int step = 0; step < seconds / dt; step++)
            {
                sinceTwitch += dt;
                sinceAction += dt;
                if (sinceTwitch >= twitchEvery)
                {
                    LureRules.OnTwitch(school, lure, sinceTwitch, LureRules.NoticeRadius(0, false));
                    lure = LureRules.Twitch(lure, shore);
                    sinceTwitch = 0f;
                    sinceAction = 0f;
                }
                if (LureRules.Step(school, lure, sinceAction, 0, false, dt, null, rng) >= 0) return step;
            }
            return -1;
        }

        [Test]
        public void AWorkedLureDrawsAFishThatBites()
        {
            var school = new[] { Fish(new Vector2(2f, 0f)) };
            Vector2 lure = Vector2.zero;
            Vector2 shore = new Vector2(-20f, 0f);
            int bite = WorkUntilBite(school, ref lure, shore, 1f, 20);
            Assert.GreaterOrEqual(bite, 0, "a gently worked lure gets a bite");
            Assert.AreEqual(SchoolFishState.Biting, school[0].State);
            Assert.Less(Vector2.Distance(school[0].Position, lure), 0.01f, "the biter is at the lure");
        }

        [Test]
        public void TwitchingTooFastSpooksTheFish()
        {
            var school = new[] { Fish(new Vector2(1f, 0f)) };
            Vector2 lure = Vector2.zero;
            for (int i = 0; i < 4; i++) LureRules.OnTwitch(school, lure, 0.1f, LureRules.NoticeRadius(0, false));
            Assert.AreEqual(SchoolFishState.Fled, school[0].State);
            Assert.AreEqual(-1, LureRules.LazyBiter(school), "a fled fish never takes the lure");
        }

        [Test]
        public void FishOutOfSightIgnoreTheLure()
        {
            var school = new[] { Fish(new Vector2(30f, 0f)) };
            Vector2 lure = Vector2.zero;
            Assert.AreEqual(-1, WorkUntilBite(school, ref lure, new Vector2(-20f, 0f), 1f, 10));
            Assert.AreEqual(SchoolFishState.Idle, school[0].State);
        }

        [Test]
        public void AStillLureLosesTheirInterest()
        {
            var school = new[] { Fish(new Vector2(1f, 0f)) };
            school[0].Interest = 0.5f;
            var rng = new System.Random(1);
            for (int i = 0; i < 200; i++) LureRules.Step(school, Vector2.zero, 10f, 0, false, 0.05f, null, rng);
            Assert.Less(school[0].Interest, 0.5f);
        }

        [Test]
        public void RareFishAreHarderToTempt()
        {
            var common = new[] { Fish(new Vector2(2f, 0f), 0) };
            var legend = new[] { Fish(new Vector2(2f, 0f), 3) };
            Vector2 a = Vector2.zero;
            Vector2 b = Vector2.zero;
            Vector2 shore = new Vector2(-40f, 0f);
            int commonBite = WorkUntilBite(common, ref a, shore, 1f, 30);
            int legendBite = WorkUntilBite(legend, ref b, shore, 1f, 30);
            Assert.GreaterOrEqual(commonBite, 0);
            Assert.True(legendBite < 0 || legendBite > commonBite, "the legendary fish takes longer");
        }

        [Test]
        public void TheLureComesHomeToTheShore()
        {
            Vector2 lure = new Vector2(10f, 0f);
            Vector2 shore = Vector2.zero;
            Vector2 next = LureRules.Twitch(lure, shore);
            Assert.AreEqual(10f - LureRules.TwitchStep, next.x, 0.001f);
            for (int i = 0; i < 100 && !LureRules.Home(lure, shore); i++) lure = LureRules.Reel(lure, shore, 0.5f);
            Assert.IsTrue(LureRules.Home(lure, shore));
            Assert.AreEqual(shore, LureRules.Reel(shore, shore, 1f), "it never overshoots");
        }

        [Test]
        public void WeightsStayInRangeAndBigOnesAreRare()
        {
            FishSpecies perch = Table[(int)FishKind.Perch];
            FishSpecies moss = Table[(int)FishKind.OldMossback];
            Assert.Less(LureRules.MaxKg(perch), LureRules.MinKg(moss) * 2f, "a perch is always small next to Old Mossback");
            Assert.AreEqual(LureRules.MinKg(perch), LureRules.Weigh(perch, 0.0), 0.01f);
            Assert.AreEqual(LureRules.MaxKg(perch), LureRules.Weigh(perch, 1.0), 0.01f);
            float middle = Mathf.Lerp(LureRules.MinKg(perch), LureRules.MaxKg(perch), 0.5f);
            Assert.Less(LureRules.Weigh(perch, 0.5), middle, "half the rolls are in the lighter quarter");
        }

        [Test]
        public void HeavierFishFightHarder()
        {
            FishSpecies pike = Table[(int)FishKind.Pike];
            float light = LureRules.Strength(pike, LureRules.MinKg(pike));
            float heavy = LureRules.Strength(pike, LureRules.MaxKg(pike));
            Assert.Greater(heavy, light);

            Fight small = FishingRules.Begin(FishKind.Pike, pike, 10f, LureRules.MinKg(pike));
            Fight big = FishingRules.Begin(FishKind.Pike, pike, 10f, LureRules.MaxKg(pike));
            for (int i = 0; i < 10; i++)
            {
                FishingRules.Step(ref small, pike, true, 1, 0f, 0f, 0.1f);
                FishingRules.Step(ref big, pike, true, 1, 0f, 0f, 0.1f);
            }
            Assert.Greater(big.Tension, small.Tension, "reeling a big one strains the line more");
            Fight plain = FishingRules.Begin(FishKind.Pike, pike, 10f);
            Assert.AreEqual(1f, plain.Strength, "a fight without a weight plays as before");
        }

        [Test]
        public void RecordsOnlyGoUp()
        {
            Assert.IsTrue(LureRules.IsRecord(1.2f, 0f));
            Assert.IsTrue(LureRules.IsRecord(1.21f, 1.2f));
            Assert.IsFalse(LureRules.IsRecord(1.2f, 1.2f));
            Assert.IsFalse(LureRules.IsRecord(0.9f, 1.2f));
        }

        [Test]
        public void SchoolsAreSmallAndHotspotsHoldMore()
        {
            Assert.AreEqual(LureRules.MinSchool, LureRules.SchoolSize(0.1, false));
            Assert.AreEqual(LureRules.MinSchool + 1, LureRules.SchoolSize(0.9, false));
            Assert.AreEqual(LureRules.MaxSchool, LureRules.SchoolSize(0.9, true));
            Assert.Greater(LureRules.NoticeRadius(3, false), LureRules.NoticeRadius(0, false), "a better lure is seen from farther");
        }
    }
}
