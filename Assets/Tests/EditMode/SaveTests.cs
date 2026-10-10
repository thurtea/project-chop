using System.IO;
using Chopper.World.Saving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chopper.Tests.EditMode
{
    public class SaveTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "edit_test" + SaveFiles.Extension);
            SaveFiles.Delete(_path);
        }

        [TearDown]
        public void TearDown() => SaveFiles.Delete(_path);

        [Test]
        public void AWorldSaveReadsBackTheSame()
        {
            var save = new WorldSave { Name = "Test world", Seed = 4242, Day = 3, Phase = 2, Elapsed = 12.5f };
            save.Hut = new HutRecord { Repaired = true, Level = 2, Health = 420, WoodStore = 77 };
            save.Chest = new[] { 100, 50, 0 };
            save.Pieces.Add(new PieceRecord { Kind = 2, Position = new Vector3(6f, 3f, -8f), Yaw = 90f, Health = 120 });
            save.FelledTrees = new[] { 4, 8, 15 };
            save.FirstLargeTreeFelled = true;
            save.GroveFelled.Add(new Vector3(30f, 4f, -150f));
            save.WardenAwake = true;
            save.WardenHome = new Vector3(-120f, 6f, 10f);
            save.WardenElement = 4;
            save.Saplings.Add(new SaplingRecord { Position = new Vector3(1f, 2f, 3f), Element = 2, Size = 1, Boss = true, BornDays = 1.5f });
            save.Guardians.Add(new GuardianRecord { Position = new Vector3(5f, -200f, 6f), Element = 3, Size = 2 });
            save.Looted.Add(new LootRecord { Chest = 7, Player = "abc" });
            save.Players.Add(new PlayerRecord { Id = "abc", Iron = 5, Shards = 3, SturdyLevel = 2, AxeWear = 12, RodWear = 3, Position = new Vector3(9f, 1f, 9f), FishRecords = new[] { 0.42f, 0f, 3.5f } });

            Assert.IsTrue(SaveFiles.Write(save, _path));
            WorldSave read = SaveFiles.Read(_path);
            Assert.IsNotNull(read);
            Assert.AreEqual("Test world", read.Name);
            Assert.AreEqual(4242, read.Seed);
            Assert.AreEqual(3, read.Day);
            Assert.AreEqual(2, read.Phase);
            Assert.AreEqual(77, read.Hut.WoodStore);
            CollectionAssert.AreEqual(new[] { 100, 50, 0 }, read.Chest);
            Assert.AreEqual(1, read.Pieces.Count);
            Assert.AreEqual(2, read.Pieces[0].Kind);
            Assert.AreEqual(90f, read.Pieces[0].Yaw, 0.001f);
            Assert.AreEqual(120, read.Pieces[0].Health);
            CollectionAssert.AreEqual(new[] { 4, 8, 15 }, read.FelledTrees);
            Assert.IsTrue(read.FirstLargeTreeFelled);
            Assert.AreEqual(1, read.GroveFelled.Count);
            Assert.AreEqual(-150f, read.GroveFelled[0].z, 0.001f);
            Assert.IsTrue(read.WardenAwake);
            Assert.AreEqual(-120f, read.WardenHome.x, 0.001f);
            Assert.AreEqual(4, read.WardenElement);
            Assert.AreEqual(1, read.Saplings.Count);
            Assert.IsTrue(read.Saplings[0].Boss);
            Assert.AreEqual(-200f, read.Guardians[0].Position.y, 0.001f);
            Assert.AreEqual("abc", read.Looted[0].Player);
            PlayerRecord player = read.FindPlayer("abc");
            Assert.IsNotNull(player);
            Assert.AreEqual(5, player.Iron);
            Assert.AreEqual(2, player.SturdyLevel);
            Assert.AreEqual(12, player.AxeWear);
            Assert.AreEqual(3, player.RodWear);
            Assert.AreEqual(3, player.FishRecords.Length);
            Assert.AreEqual(3.5f, player.FishRecords[2], 0.001f);
        }

        [Test]
        public void SavingAgainReplacesTheFile()
        {
            Assert.IsTrue(SaveFiles.Write(new WorldSave { Name = "One", Day = 1 }, _path));
            Assert.IsTrue(SaveFiles.Write(new WorldSave { Name = "One", Day = 5 }, _path));
            Assert.AreEqual(5, SaveFiles.Read(_path).Day);
            Assert.IsFalse(File.Exists(_path + ".tmp"), "no temporary file is left behind");
        }

        [Test]
        public void ABrokenFileIsIgnoredNotFatal()
        {
            File.WriteAllText(_path, "{ this is not json");
            LogAssert.ignoreFailingMessages = true;
            Assert.IsNull(SaveFiles.Read(_path));
            LogAssert.ignoreFailingMessages = false;
            Assert.IsNull(SaveFiles.Read(_path + ".missing"));
        }

        [Test]
        public void WorldNamesBecomeSafeFileNames()
        {
            string path = SaveFiles.NewPath("My <World>: 2!");
            string file = Path.GetFileName(path);
            StringAssert.EndsWith(SaveFiles.Extension, file);
            foreach (char c in Path.GetInvalidFileNameChars()) Assert.IsFalse(file.Contains(c.ToString()), $"'{c}' in {file}");
            StringAssert.StartsWith("My_World_2", file);
            StringAssert.StartsWith("World", Path.GetFileName(SaveFiles.NewPath("???")));
        }
    }
}
