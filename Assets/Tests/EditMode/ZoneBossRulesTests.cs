using System.Collections.Generic;
using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests
{
    public class ZoneBossRulesTests
    {
        private static List<Vector3> Grove(Vector3 center, int count, float spread)
        {
            var list = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.4f;
                list.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spread * (i % 3) / 2f);
            }
            return list;
        }

        [Test]
        public void AGroveNeedsEnoughTreesCloseTogether()
        {
            Vector3 center = new Vector3(0f, 0f, -200f);
            List<Vector3> felled = Grove(center, 17, 20f);
            Assert.IsFalse(ZoneBossRules.TryFindGrove(felled, center, 35f, 18, out _), "one tree short");
            felled.Add(center);
            Assert.IsTrue(ZoneBossRules.TryFindGrove(felled, center, 35f, 18, out Vector3 found));
            Assert.Less(Vector3.Distance(found, center), 6f, "the warden rises in the middle of the felled trees");
        }

        [Test]
        public void TreesFarApartAreNotOneGrove()
        {
            var felled = new List<Vector3>();
            // The same number of trees, but spread over the whole forest.
            for (int i = 0; i < 30; i++) felled.Add(new Vector3(i * 40f, 0f, 150f));
            Assert.IsFalse(ZoneBossRules.TryFindGrove(felled, felled[15], 35f, 18, out _));
            Assert.AreEqual(3, ZoneBossRules.CountNear(felled, felled[15], 41f));
        }

        [Test]
        public void HeightDoesNotSplitAGrove()
        {
            var felled = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(1f, 30f, 0f) };
            Assert.AreEqual(2, ZoneBossRules.CountNear(felled, Vector3.zero, 2f));
        }

        [Test]
        public void ARisenGroveIsForgottenButOthersStay()
        {
            Vector3 a = new Vector3(0f, 0f, 200f);
            Vector3 b = new Vector3(200f, 0f, 0f);
            List<Vector3> felled = Grove(a, 10, 20f);
            felled.AddRange(Grove(b, 5, 20f));
            ZoneBossRules.ForgetGrove(felled, a, 35f);
            Assert.AreEqual(5, felled.Count);
            Assert.AreEqual(5, ZoneBossRules.CountNear(felled, b, 35f));
        }

        [Test]
        public void TheMemoryIsCapped()
        {
            var felled = new List<Vector3>();
            for (int i = 0; i < ZoneBossRules.MaxRemembered + 50; i++) ZoneBossRules.Remember(felled, new Vector3(i, 0f, 0f));
            Assert.AreEqual(ZoneBossRules.MaxRemembered, felled.Count);
            Assert.AreEqual(50f, felled[0].x, "the oldest felled trees are dropped first");
        }

        [Test]
        public void TheWardenWaitsForTheBossDayAndRisesAlone()
        {
            Assert.IsFalse(ZoneBossRules.ShouldRise(2, 3, false, true), "too early in the game");
            Assert.IsFalse(ZoneBossRules.ShouldRise(5, 3, false, false), "no grove felled yet");
            Assert.IsTrue(ZoneBossRules.ShouldRise(3, 3, false, true));
            Assert.IsFalse(ZoneBossRules.ShouldRise(9, 3, true, true), "only one warden at a time");
        }

        [Test]
        public void ThreePhasesByHealth()
        {
            Assert.AreEqual(1, ZoneBossRules.PhaseFor(1f));
            Assert.AreEqual(1, ZoneBossRules.PhaseFor(0.7f));
            Assert.AreEqual(2, ZoneBossRules.PhaseFor(0.66f));
            Assert.AreEqual(2, ZoneBossRules.PhaseFor(0.34f));
            Assert.AreEqual(3, ZoneBossRules.PhaseFor(0.33f));
            Assert.AreEqual(3, ZoneBossRules.PhaseFor(0f));
        }

        [Test]
        public void LaterPhasesAreHarder()
        {
            Assert.IsFalse(ZoneBossRules.RootsActive(1));
            Assert.IsTrue(ZoneBossRules.RootsActive(2));
            Assert.IsTrue(ZoneBossRules.RootsActive(3));
            Assert.Greater(ZoneBossRules.SpeedMultiplier(3), ZoneBossRules.SpeedMultiplier(2));
            Assert.Greater(ZoneBossRules.SpeedMultiplier(2), ZoneBossRules.SpeedMultiplier(1));
            Assert.Less(ZoneBossRules.IntervalMultiplier(3), ZoneBossRules.IntervalMultiplier(1));
            Assert.AreEqual(1f, ZoneBossRules.SpeedMultiplier(0), "other enemies are not changed");
        }

        [Test]
        public void GuardsShieldTheBarkAndScaleWithPlayers()
        {
            Assert.AreEqual(0, ZoneBossRules.GuardsFor(1, 4));
            Assert.AreEqual(3, ZoneBossRules.GuardsFor(2, 1));
            Assert.AreEqual(6, ZoneBossRules.GuardsFor(2, 4));
            Assert.AreEqual(ZoneBossRules.MaxGuards, ZoneBossRules.GuardsFor(2, 20));
            Assert.AreEqual(2, ZoneBossRules.GuardsFor(3, 1));
            Assert.AreEqual(4f, ZoneBossRules.DamageTaken(4f, false));
            Assert.AreEqual(1f, ZoneBossRules.DamageTaken(4f, true));
            Assert.AreEqual(0f, ZoneBossRules.DamageTaken(-3f, false));
        }

        [Test]
        public void MorePlayersMoreHealth()
        {
            Assert.AreEqual(90f, ZoneBossRules.Health(90f, 0.6f, 1), 0.001f);
            Assert.AreEqual(144f, ZoneBossRules.Health(90f, 0.6f, 2), 0.001f);
            Assert.AreEqual(90f, ZoneBossRules.Health(90f, 0.6f, 0), 0.001f);
        }
    }
}
