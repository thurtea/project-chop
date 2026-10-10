using Chopper.Core;
using Chopper.Trees;
using NUnit.Framework;
using UnityEditor;

namespace Chopper.Tests.EditMode
{
    public class TreeHitTrackerTests
    {
        private const string GameConfigPath = "Assets/_Chopper/Config/SO_GameConfig.asset";
        // One plain axe hit.
        private const int Hit = ChopRules.PointsPerHit;

        private GameConfig _config;
        private TreeHitTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(_config, $"Missing {GameConfigPath}");
            _tracker = new TreeHitTracker(_config);
        }

        // Plain axe hits until the tree falls; returns how many it took.
        private int HitsUntilFelled(int id, TreeSize size, int points)
        {
            for (int swing = 1; swing <= 100; swing++)
            {
                if (_tracker.ApplyHit(id, size, points) == TreeHitResult.Felled) return swing;
            }
            return -1;
        }

        [Test]
        public void EachSizeFallsAfterItsHitsToFell()
        {
            Assert.AreEqual(_config.GetHitsToFell(TreeSize.Small), HitsUntilFelled(1, TreeSize.Small, Hit));
            Assert.AreEqual(_config.GetHitsToFell(TreeSize.Medium), HitsUntilFelled(2, TreeSize.Medium, Hit));
            Assert.AreEqual(_config.GetHitsToFell(TreeSize.Large), HitsUntilFelled(3, TreeSize.Large, Hit));
            Assert.IsTrue(_tracker.IsFelled(1) && _tracker.IsFelled(2) && _tracker.IsFelled(3));
        }

        [Test]
        public void TreesTakeSeveralSwingsAndBiggerTakesMore()
        {
            int small = _config.GetHitsToFell(TreeSize.Small);
            int medium = _config.GetHitsToFell(TreeSize.Medium);
            int large = _config.GetHitsToFell(TreeSize.Large);
            Assert.GreaterOrEqual(small, 3, "a small tree should not fall in one or two swings");
            Assert.Greater(medium, small);
            Assert.Greater(large, medium);
        }

        [Test]
        public void ABetterAxeFellsInFewerSwings()
        {
            int plain = HitsUntilFelled(10, TreeSize.Large, ChopRules.SwingPoints(1f));
            int best = HitsUntilFelled(11, TreeSize.Large, ChopRules.SwingPoints(2f));
            Assert.AreEqual(ChopRules.SwingsToFell(_config.GetHitsToFell(TreeSize.Large), 2f), best);
            Assert.Less(best, plain);
            Assert.AreEqual(1, ChopRules.SwingPoints(0f), "even the weakest swing takes off a point");
        }

        [Test]
        public void DamageIsTrackedPerTree()
        {
            _tracker.ApplyHit(4, TreeSize.Large, Hit);
            Assert.AreEqual(Hit, _tracker.HitsTaken(4));
            Assert.AreEqual(0, _tracker.HitsTaken(5));
            Assert.AreEqual(TreeHitResult.Damaged, _tracker.ApplyHit(5, TreeSize.Large, Hit));
        }

        [Test]
        public void HitOnFelledTreeIsIgnored()
        {
            HitsUntilFelled(6, TreeSize.Small, Hit);
            Assert.AreEqual(TreeHitResult.Ignored, _tracker.ApplyHit(6, TreeSize.Small, Hit));
            Assert.AreEqual(1, _tracker.FelledCount);
        }

        [Test]
        public void NonPositiveHitsAreIgnored()
        {
            Assert.AreEqual(TreeHitResult.Ignored, _tracker.ApplyHit(7, TreeSize.Small, 0));
            Assert.AreEqual(TreeHitResult.Ignored, _tracker.ApplyHit(7, TreeSize.Small, -1));
            Assert.IsFalse(_tracker.IsFelled(7));
        }

        [Test]
        public void OversizedHitStillFellsOnce()
        {
            Assert.AreEqual(TreeHitResult.Felled, _tracker.ApplyHit(8, TreeSize.Large, 1000));
            Assert.AreEqual(TreeHitResult.Ignored, _tracker.ApplyHit(8, TreeSize.Large, Hit));
        }

        [Test]
        public void RegrowRestoresTreeWithFullHealth()
        {
            _tracker.ApplyHit(9, TreeSize.Medium, Hit);
            _tracker.ApplyHit(9, TreeSize.Medium, Hit);
            _tracker.Regrow(9);
            Assert.IsFalse(_tracker.IsFelled(9));
            Assert.AreEqual(0, _tracker.HitsTaken(9));
            Assert.AreEqual(TreeHitResult.Damaged, _tracker.ApplyHit(9, TreeSize.Medium, Hit));
        }
    }
}
