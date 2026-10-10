using System;
using System.Collections.Generic;
using Chopper.Core;
using Chopper.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chopper.Tests.EditMode
{
    public class SpawnTableTests
    {
        private const int SampleCount = 100000;
        private const int Seed = 12345;
        private const float DistributionTolerance = 0.01f;

        private static readonly Element[] AllElements = (Element[])Enum.GetValues(typeof(Element));

        private static readonly object[] AssetCases =
        {
            new object[] { "Assets/_Chopper/Config/SO_SpawnTable_Water.asset", Element.Water },
            new object[] { "Assets/_Chopper/Config/SO_SpawnTable_Earth.asset", Element.Earth },
            new object[] { "Assets/_Chopper/Config/SO_SpawnTable_Fire.asset", Element.Fire },
            new object[] { "Assets/_Chopper/Config/SO_SpawnTable_Wind.asset", Element.Wind }
        };

        private SpawnTable _table;

        [SetUp]
        public void SetUp()
        {
            // Plan section 6 odds for the Fire forest.
            _table = ScriptableObject.CreateInstance<SpawnTable>();
            _table.Configure(Element.Fire, new List<SpawnTable.Entry>
            {
                new SpawnTable.Entry(Element.Fire, 55f),
                new SpawnTable.Entry(Element.Neutral, 25f),
                new SpawnTable.Entry(Element.Water, 20f / 3f),
                new SpawnTable.Entry(Element.Earth, 20f / 3f),
                new SpawnTable.Entry(Element.Wind, 20f / 3f)
            });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_table);
        }

        [Test]
        public void SeededDistributionMatchesConfiguredOdds()
        {
            AssertDistribution(_table);
        }

        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new System.Random(Seed);
            var b = new System.Random(Seed);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(_table.Roll(a), _table.Roll(b));
            }
        }

        [Test]
        public void RollBoundariesPickFirstAndLastEntries()
        {
            Assert.AreEqual(Element.Fire, _table.Roll(0f));
            Assert.AreEqual(Element.Wind, _table.Roll(1f));
        }

        [Test]
        public void EmptyTableRollsNeutral()
        {
            SpawnTable empty = ScriptableObject.CreateInstance<SpawnTable>();
            try
            {
                Assert.AreEqual(Element.Neutral, empty.Roll(new System.Random(Seed)));
                Assert.AreEqual(0f, empty.GetProbability(Element.Fire));
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [TestCaseSource(nameof(AssetCases))]
        public void ForestAssetHasPlanOddsAndSeededDistribution(string path, Element zone)
        {
            SpawnTable table = AssetDatabase.LoadAssetAtPath<SpawnTable>(path);
            Assert.IsNotNull(table, $"Missing {path}");

            Assert.AreEqual(zone, table.ZoneElement);
            // Each forest plants its own element or a plain sapling, half and half; no other elements.
            Assert.AreEqual(0.5f, table.GetProbability(zone), 0.001f);
            Assert.AreEqual(0.5f, table.GetProbability(Element.Neutral), 0.001f);
            foreach (Element other in AllElements)
            {
                if (other == zone || other == Element.Neutral) continue;
                Assert.AreEqual(0f, table.GetProbability(other), 0.001f);
            }

            AssertDistribution(table);
        }

        private static void AssertDistribution(SpawnTable table)
        {
            var counts = new Dictionary<Element, int>();
            var rng = new System.Random(Seed);
            for (int i = 0; i < SampleCount; i++)
            {
                Element rolled = table.Roll(rng);
                counts.TryGetValue(rolled, out int count);
                counts[rolled] = count + 1;
            }

            foreach (Element element in AllElements)
            {
                counts.TryGetValue(element, out int count);
                float observed = (float)count / SampleCount;
                Assert.AreEqual(table.GetProbability(element), observed, DistributionTolerance, $"{element} frequency");
            }
        }
    }
}
