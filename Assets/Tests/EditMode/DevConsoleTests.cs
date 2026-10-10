using System.Linq;
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
    }
}
