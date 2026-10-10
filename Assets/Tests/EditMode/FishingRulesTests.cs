using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class FishingRulesTests
    {
        private static readonly FishSpecies[] Table = FishSpecies.Defaults();
        private const float MinCast = 4f;
        private const float MaxCast = 14f;
        private const float LongCastModifier = 0.15f;

        private static FishSpecies S(FishKind kind) => FishingRules.Species(Table, kind);

        // Plays a fight with a simple policy: "good" reels when it is safe and lets go for thrashes and their warning,
        // "tension" only watches the bar, "hold" never lets go. Returns the result and the seconds it took.
        private static FightResult Play(FishKind kind, float cast, int rod, string policy, out float seconds)
        {
            FishSpecies s = S(kind);
            Fight fight = FishingRules.Begin(kind, s, cast);
            float factor = FishingRules.CastFactor(cast, MinCast, MaxCast);
            bool reeling = true;
            FightResult result = FightResult.Ongoing;
            seconds = 0f;
            while (result == FightResult.Ongoing && seconds < 120f)
            {
                FishPhase phase = FishingRules.PhaseAt(s, fight.Elapsed, out bool warning);
                if (policy == "good")
                {
                    if (phase == FishPhase.Thrashing || warning || fight.Tension > 0.78f) reeling = false;
                    else if (fight.Tension < 0.4f) reeling = true;
                }
                else if (policy == "tension")
                {
                    if (fight.Tension > 0.8f) reeling = false;
                    else if (fight.Tension < 0.35f) reeling = true;
                }
                else reeling = true;
                result = FishingRules.Step(ref fight, s, reeling, rod, factor, LongCastModifier, 0.02f);
                seconds += 0.02f;
            }
            return result;
        }

        [TestCase(FishKind.Perch, 5f, 13.5f)]
        [TestCase(FishKind.Pike, 10f, 21f)]
        [TestCase(FishKind.GoldenCarp, 15f, 30f)]
        public void APlayerWhoWatchesTheFishLandsItInTime(FishKind kind, float min, float max)
        {
            foreach (float cast in new[] { MinCast + 1f, MaxCast - 1f })
            {
                Assert.AreEqual(FightResult.Landed, Play(kind, cast, 1, "good", out float seconds), $"{kind} from a {cast} m cast");
                Assert.That(seconds, Is.InRange(min, max), $"{kind} from a {cast} m cast took {seconds:F1} s");
            }
        }

        [Test]
        public void TheLegendaryFishIsLongButFair()
        {
            Assert.AreEqual(FightResult.Landed, Play(FishKind.OldMossback, 9f, 1, "good", out float seconds));
            Assert.Less(seconds, 60f);
            Assert.AreEqual(FightResult.Landed, Play(FishKind.OldMossback, 9f, 1, "tension", out _), "watching only the bar still works");
        }

        [Test]
        public void HoldingTheReelNonStopSnapsTheLine()
        {
            for (int k = 0; k < FishingRules.Kinds; k++) Assert.AreEqual(FightResult.Snapped, Play((FishKind)k, 9f, 1, "hold", out _), ((FishKind)k).ToString());
        }

        [Test]
        public void WatchingOnlyTheTensionIsForgiving()
        {
            Assert.AreEqual(FightResult.Landed, Play(FishKind.Perch, 9f, 1, "tension", out _));
            Assert.AreEqual(FightResult.Landed, Play(FishKind.GoldenCarp, 9f, 1, "tension", out _));
        }

        [Test]
        public void LettingAFishRunForeverLosesIt()
        {
            FishSpecies s = S(FishKind.Pike);
            Fight fight = FishingRules.Begin(FishKind.Pike, s, 10f);
            FightResult result = FightResult.Ongoing;
            for (int i = 0; i < 20000 && result == FightResult.Ongoing; i++) result = FishingRules.Step(ref fight, s, false, 1, 0.5f, LongCastModifier, 0.02f);
            Assert.AreEqual(FightResult.Escaped, result);
        }

        [Test]
        public void ReelingReducesDistanceAndRaisesTensionWhileLettingGoEasesIt()
        {
            FishSpecies s = S(FishKind.Perch);
            Fight fight = FishingRules.Begin(FishKind.Perch, s, 10f);
            float distance = fight.Distance;
            FishingRules.Step(ref fight, s, true, 1, 0f, LongCastModifier, 0.5f);
            Assert.Less(fight.Distance, distance);
            Assert.Greater(fight.Tension, FishingRules.StartTension);
            float tension = fight.Tension;
            FishingRules.Step(ref fight, s, false, 1, 0f, LongCastModifier, 0.5f);
            Assert.Less(fight.Tension, tension);
        }

        [Test]
        public void ReelingIntoAThrashIsFarRiskierThanWhileCalm()
        {
            FishSpecies s = S(FishKind.Pike);
            Fight calm = FishingRules.Begin(FishKind.Pike, s, 9f);
            Fight thrash = calm;
            thrash.Elapsed = s.CalmSeconds + s.PullSeconds + 0.1f;
            Assert.AreEqual(FishPhase.Thrashing, FishingRules.PhaseAt(s, thrash.Elapsed, out _));
            FishingRules.Step(ref calm, s, true, 1, 0f, LongCastModifier, 0.1f);
            FishingRules.Step(ref thrash, s, true, 1, 0f, LongCastModifier, 0.1f);
            Assert.Greater(thrash.Tension - FishingRules.StartTension, 2.5f * (calm.Tension - FishingRules.StartTension));
        }

        [Test]
        public void PhasesComeInAFixedOrderAndThrashesAreWarned()
        {
            FishSpecies s = S(FishKind.Perch);
            Assert.AreEqual(FishPhase.Calm, FishingRules.PhaseAt(s, 0.1f, out bool warned));
            Assert.IsFalse(warned);
            Assert.AreEqual(FishPhase.Pulling, FishingRules.PhaseAt(s, s.CalmSeconds + 0.1f, out warned));
            Assert.IsFalse(warned);
            Assert.AreEqual(FishPhase.Pulling, FishingRules.PhaseAt(s, s.CalmSeconds + s.PullSeconds - 0.1f, out warned));
            Assert.IsTrue(warned, "the end of a pull warns of the thrash");
            Assert.AreEqual(FishPhase.Thrashing, FishingRules.PhaseAt(s, s.CalmSeconds + s.PullSeconds + 0.1f, out _));
            Assert.AreEqual(FishPhase.Tired, FishingRules.PhaseAt(s, s.CalmSeconds + s.PullSeconds + s.ThrashSeconds + 0.1f, out _));
            float cycle = s.CalmSeconds + s.PullSeconds + s.ThrashSeconds + s.TiredSeconds;
            Assert.AreEqual(FishPhase.Calm, FishingRules.PhaseAt(s, cycle + 0.1f, out _));
        }

        [Test]
        public void TensionZonesHaveFixedEdges()
        {
            Assert.AreEqual(TensionZone.Safe, FishingRules.Zone(0.54f));
            Assert.AreEqual(TensionZone.Risky, FishingRules.Zone(0.55f));
            Assert.AreEqual(TensionZone.Risky, FishingRules.Zone(0.79f));
            Assert.AreEqual(TensionZone.Critical, FishingRules.Zone(0.8f));
        }

        [Test]
        public void ALongCastEasesTheStrain()
        {
            FishSpecies s = S(FishKind.Pike);
            Fight shortCast = FishingRules.Begin(FishKind.Pike, s, MinCast);
            Fight longCast = FishingRules.Begin(FishKind.Pike, s, MaxCast);
            FishingRules.Step(ref shortCast, s, true, 1, 0f, LongCastModifier, 0.5f);
            FishingRules.Step(ref longCast, s, true, 1, 1f, LongCastModifier, 0.5f);
            Assert.Less(longCast.Tension, shortCast.Tension);
        }

        [Test]
        public void ABetterRodReelsFasterAndStrainsTheLineLess()
        {
            FishSpecies s = S(FishKind.Pike);
            Fight weak = FishingRules.Begin(FishKind.Pike, s, 10f);
            Fight strong = weak;
            FishingRules.Step(ref weak, s, true, 1, 0f, LongCastModifier, 0.5f);
            FishingRules.Step(ref strong, s, true, 3, 0f, LongCastModifier, 0.5f);
            Assert.Less(strong.Distance, weak.Distance);
            Assert.Less(strong.Tension, weak.Tension);
        }

        [Test]
        public void TheRollRespectsLevelLuresAndHotspots()
        {
            int Count(FishKind kind, int lure, int level, bool hotspot)
            {
                int n = 0;
                for (int i = 0; i < 1000; i++)
                    if (FishingRules.Roll(Table, (i + 0.5) / 1000.0, lure, level, hotspot, 2.5f, 0.5f, FishWater.Pond, WeatherKind.Sunny, DayPhase.Day) == kind) n++;
                return n;
            }
            Assert.AreEqual(0, Count(FishKind.OldMossback, 3, 0, true), "the legendary fish needs fishing level 3");
            Assert.AreEqual(0, Count(FishKind.GoldenCarp, 0, 0, false), "golden carp need level 1");
            Assert.Greater(Count(FishKind.OldMossback, 0, 3, false), 0);
            Assert.Greater(Count(FishKind.Pike, 3, 1, false), Count(FishKind.Pike, 0, 1, false), "lures find rarer fish");
            Assert.Greater(Count(FishKind.GoldenCarp, 0, 1, true), Count(FishKind.GoldenCarp, 0, 1, false), "hotspots find rarer fish");
            Assert.Greater(Count(FishKind.Perch, 0, 0, false), 500, "perch are the common catch");
        }

        [Test]
        public void HotspotsLuresAndLevelBringBitesSooner()
        {
            FishSpecies s = S(FishKind.Pike);
            float plain = FishingRules.BiteDelay(s, 0.5, 0, false, 0, 0.6f);
            Assert.Less(FishingRules.BiteDelay(s, 0.5, 0, true, 0, 0.6f), plain);
            Assert.Less(FishingRules.BiteDelay(s, 0.5, 3, false, 0, 0.6f), plain);
            Assert.Less(FishingRules.BiteDelay(s, 0.5, 0, false, 4, 0.6f), plain);
            Assert.That(plain, Is.InRange(s.BiteDelayMin, s.BiteDelayMax));
            Assert.Greater(FishingRules.HookWindow(s, 3), FishingRules.HookWindow(s, 0));
            Assert.Less(FishingRules.HookWindow(S(FishKind.OldMossback), 0), FishingRules.HookWindow(S(FishKind.Perch), 0), "harder fish give a shorter window");
        }

        [Test]
        public void FishCountsKeepEveryKindAndTheSmallestIsEatenFirst()
        {
            int packed = 0;
            packed = FishingRules.Add(packed, FishKind.Pike, 2);
            packed = FishingRules.Add(packed, FishKind.OldMossback, 1);
            int[] bag = FishingRules.UnpackLegacy(packed);
            Assert.AreEqual(0, bag[(int)FishKind.Perch]);
            Assert.AreEqual(2, bag[(int)FishKind.Pike]);
            Assert.AreEqual(1, bag[(int)FishKind.OldMossback]);
            Assert.AreEqual(packed, FishingRules.PackLegacy(bag), "the first four kinds still pack the old way");
            FishingRules.AddTo(bag, FishKind.Herring, 1);
            Assert.AreEqual(4, FishingRules.TotalOf(bag));
            Assert.AreEqual(packed, FishingRules.PackLegacy(bag), "a kind past the first four stays out of the old packed int");
            Assert.IsTrue(FishingRules.NextToEat(bag, Table, out FishKind next));
            Assert.AreEqual(FishKind.Herring, next, "the fish that heals the least is eaten first");
            FishingRules.AddTo(bag, FishKind.Herring, -1);
            Assert.IsTrue(FishingRules.NextToEat(bag, Table, out next));
            Assert.AreEqual(FishKind.Pike, next);
            FishingRules.AddTo(bag, FishKind.Perch, -3);
            Assert.AreEqual(0, bag[(int)FishKind.Perch], "never below zero");
            Assert.IsFalse(FishingRules.NextToEat(new int[FishingRules.Kinds], Table, out _));
        }

        [Test]
        public void EachWaterHasItsOwnFishAndWeatherAndHourChangeTheCatch()
        {
            Assert.AreEqual(FishingRules.Kinds, Table.Length);
            int Count(FishKind kind, FishWater water, WeatherKind weather, DayPhase phase, int level)
            {
                int n = 0;
                for (int i = 0; i < 400; i++)
                    if (FishingRules.Roll(Table, (i + 0.5) / 400.0, 0, level, false, 2.5f, 0.5f, water, weather, phase) == kind) n++;
                return n;
            }
            Assert.AreEqual(0, Count(FishKind.RainTrout, FishWater.Pond, WeatherKind.Sunny, DayPhase.Day, 3));
            Assert.AreEqual(0, Count(FishKind.MoonEel, FishWater.Pond, WeatherKind.Rain, DayPhase.Day, 3));
            Assert.Greater(Count(FishKind.RainTrout, FishWater.Pond, WeatherKind.Rain, DayPhase.Day, 0), 40, "rain brings the rain trout");
            Assert.Greater(Count(FishKind.MoonEel, FishWater.Pond, WeatherKind.Sunny, DayPhase.Night, 2), 20, "night brings the moon eel");
            Assert.AreEqual(0, Count(FishKind.MoonEel, FishWater.Pond, WeatherKind.Sunny, DayPhase.Night, 0), "the moon eel needs fishing level 2");
            Assert.Greater(Count(FishKind.SilverShiner, FishWater.Pond, WeatherKind.Sunny, DayPhase.Dawn, 0), 20);
            Assert.AreEqual(0, Count(FishKind.Perch, FishWater.Fire, WeatherKind.Sunny, DayPhase.Day, 3), "pond fish stay in the pond");
            Assert.Greater(Count(FishKind.CinderChar, FishWater.Fire, WeatherKind.Sunny, DayPhase.Day, 0), 40);
            Assert.Greater(Count(FishKind.SteamSalmon, FishWater.Fire, WeatherKind.Rain, DayPhase.Day, 1), 20);
            Assert.Greater(Count(FishKind.Herring, FishWater.Sea, WeatherKind.Sunny, DayPhase.Day, 0), 40);
            Assert.Greater(Count(FishKind.BreakerCod, FishWater.Sea, WeatherKind.Rain, DayPhase.Night, 1), 10, "rain and night fish can bite together");
            Assert.Greater(Count(FishKind.Lanternfish, FishWater.Sea, WeatherKind.Rain, DayPhase.Night, 2), 10);
        }

        [Test]
        public void FishingLevelsFollowTheThresholds()
        {
            int[] thresholds = { 0, 15, 40, 80, 140 };
            Assert.AreEqual(0, FishingRules.Level(14, thresholds));
            Assert.AreEqual(1, FishingRules.Level(15, thresholds));
            Assert.AreEqual(3, FishingRules.Level(100, thresholds));
            Assert.AreEqual(4, FishingRules.Level(9999, thresholds));
        }

        [Test]
        public void TheServerAcceptsAnyFightAGoodPlayerCanWin()
        {
            for (int k = 0; k < FishingRules.Kinds; k++)
            {
                var kind = (FishKind)k;
                Play(kind, MinCast, FishingRules.MaxRodLevel, "good", out float seconds);
                Assert.Less(FishingRules.MinFightSeconds(S(kind), MinCast), seconds, kind.ToString());
            }
        }
    }
}
