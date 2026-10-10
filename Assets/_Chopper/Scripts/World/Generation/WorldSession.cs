using System;
using System.Collections.Generic;
using Chopper.Trees;
using UnityEngine;

namespace Chopper.World.Generation
{
    // The world of the current game: its seed, and once WorldBuilder has made it, the generator and the tree list.
    // The host picks the seed (New Game, or -chopperSeed on the command line); joining players receive it from the
    // server before the World scene loads (WorldSeedBroadcast). Reset when the game ends.
    public static class WorldSession
    {
        // Used by command line hosting without -chopperSeed, so test runs and screenshot tours see the same world.
        public const int DefaultSeed = 1337;

        private static Action _ready;

        public static int Seed { get; private set; } = DefaultSeed;
        public static bool HasSeed { get; private set; }
        public static bool Ready { get; private set; }
        public static WorldGen Gen { get; private set; }
        public static TreeDatabase Trees { get; private set; }
        public static int TreeChecksum { get; private set; }

        // Raised once the world is built. Subscribing after that calls the handler at once.
        public static event Action OnReady
        {
            add
            {
                if (Ready) value?.Invoke();
                else _ready += value;
            }
            remove => _ready -= value;
        }

        public static void SetSeed(int seed)
        {
            Seed = seed;
            HasSeed = true;
        }

        // A seed typed by a player: a number is used as it is, any other text is hashed, so "chopper" is a valid seed.
        public static int ParseSeed(string text)
        {
            text = text == null ? string.Empty : text.Trim();
            if (text.Length == 0) return RandomSeed();
            if (int.TryParse(text, out int number)) return number;
            unchecked
            {
                int hash = 23;
                foreach (char c in text) hash = hash * 31 + c;
                return hash;
            }
        }

        public static int RandomSeed() => UnityEngine.Random.Range(1, 1000000);

        public static void MarkReady(WorldGen gen, TreeDatabase trees)
        {
            Gen = gen;
            Trees = trees;
            TreeChecksum = trees != null ? WorldGen.Checksum(ToList(trees)) : 0;
            Ready = true;
            Action handlers = _ready;
            _ready = null;
            handlers?.Invoke();
        }

        // Called when the game ends (back to the start screen), so the next game builds a fresh world.
        public static void Reset()
        {
            Ready = false;
            HasSeed = false;
            Gen = null;
            Trees = null;
            _ready = null;
            CryptWorld.Reset();
        }

        private static List<TreeEntry> ToList(TreeDatabase trees)
        {
            var list = new List<TreeEntry>(trees.Count);
            for (int i = 0; i < trees.Count; i++) list.Add(trees.Get(i));
            return list;
        }
    }
}
