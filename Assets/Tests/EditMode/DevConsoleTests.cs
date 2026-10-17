using System.Linq;
using Chopper.Core;
using Chopper.UI;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class DevConsoleTests
    {
        [Test]
        public void SplitsIntoLowerCaseWords()
        {
            CollectionAssert.AreEqual(new[] { "tp", "crypt", "2" }, DevConsole.Split("  TP  Crypt 2 "));
            Assert.AreEqual(0, DevConsole.Split("   ").Length);
            Assert.AreEqual(0, DevConsole.Split(null).Length);
        }

        [Test]
        public void CountFallsBackAndNeverGoesBelowOne()
        {
            Assert.AreEqual(100, DevConsole.Count(new[] { "wood" }, 100));
            Assert.AreEqual(25, DevConsole.Count(new[] { "wood", "25" }, 100));
            Assert.AreEqual(100, DevConsole.Count(new[] { "wood", "lots" }, 100));
            Assert.AreEqual(1, DevConsole.Count(new[] { "wood", "-4" }, 100));
        }

        [Test]
        public void HelpListsEveryCommand()
        {
            var console = new DevConsole(null);
            console.Run("help");
            string output = string.Join("\n", console.Output);
            foreach (string name in console.Names) StringAssert.Contains(name, output);
            Assert.IsTrue(console.Names.Contains("fly"));
            Assert.IsTrue(console.Names.Contains("fiber"));
        }

        [Test]
        public void UnknownCommandsAndCheatsWithoutAWorldExplainThemselves()
        {
            var console = new DevConsole(null);
            console.Run("teleportme");
            StringAssert.Contains("Unknown command", console.Output.Last());
            console.Run("fly");
            StringAssert.Contains("Start or join a world", console.Output.Last());
            int version = console.Version;
            console.Run("clear");
            Assert.AreEqual(0, console.Output.Count);
            Assert.Greater(console.Version, version);
        }
    
        [Test]
        public void ParsesSpawnElementsAndCounts()
        {
            Assert.IsTrue(DevConsole.TryParseElement("Earth", out Element e)); Assert.AreEqual(Element.Earth, e);
            Assert.IsTrue(DevConsole.TryParseElement("plain", out e)); Assert.AreEqual(Element.Neutral, e);
            Assert.IsTrue(DevConsole.TryParseElement("water", out e)); Assert.AreEqual(Element.Water, e);
            Assert.IsFalse(DevConsole.TryParseElement("lava", out e));
            DevConsole.DecodeSpawn(DevConsole.EncodeSpawn(Element.Wind, 7), out Element back, out int count);
            Assert.AreEqual(Element.Wind, back); Assert.AreEqual(7, count);
            DevConsole.DecodeSpawn(DevConsole.EncodeSpawn(Element.Neutral, 0), out back, out count);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void ParsesHoursIntoTheClock()
        {
            Assert.IsTrue(DevConsole.TryParseHour("12", out float h)); Assert.AreEqual(12f, h);
            Assert.IsFalse(DevConsole.TryParseHour("25", out h));
            Assert.IsFalse(DevConsole.TryParseHour("noon", out h));
            DevConsole.HourToClock(12f, out DayPhase p, out float f); Assert.AreEqual(DayPhase.Day, p); Assert.AreEqual(0.5f, f, 1e-4f);
            DevConsole.HourToClock(19f, out p, out f); Assert.AreEqual(DayPhase.Dusk, p);
            DevConsole.HourToClock(0f, out p, out f); Assert.AreEqual(DayPhase.Night, p); Assert.AreEqual(0.5f, f, 1e-4f);
            DevConsole.HourToClock(5f, out p, out f); Assert.AreEqual(DayPhase.Dawn, p);
        }

        [Test]
        public void NewCommandsAreListed()
        {
            var console = new DevConsole(null);
            foreach (string n in new[] { "night", "day", "time", "spawn", "killall", "wave", "god", "heal", "help" })
                Assert.IsTrue(console.Names.Contains(n), n);
        }
}
}
