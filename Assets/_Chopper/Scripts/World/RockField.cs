using System.Collections.Generic;
using Chopper.Core;
using UnityEngine;

namespace Chopper.World
{
    public static class RockField
    {
        private static readonly List<Vector3> Positions = new List<Vector3>();
        private static readonly List<float> Sizes = new List<float>();
        private static readonly Dictionary<int, int> TakenToday = new Dictionary<int, int>();

        public static int Count => Positions.Count;

        public static void Clear()
        {
            Positions.Clear();
            Sizes.Clear();
            TakenToday.Clear();
        }

        public static void Add(Vector3 position, float size)
        {
            Positions.Add(position);
            Sizes.Add(size);
        }

        public static Vector3 PositionOf(int rock) => Positions[rock];

        public static float SizeOf(int rock) => Sizes[rock];

        public static int InReach(Vector3 player, Vector3 aim)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Positions.Count; i++)
            {
                if (!StoneRules.InReach(player, aim, Positions[i], Sizes[i])) continue;
                Vector3 to = Positions[i] - player;
                to.y = 0f;
                if (to.sqrMagnitude >= bestSqr) continue;
                bestSqr = to.sqrMagnitude;
                best = i;
            }
            return best;
        }

        public static int ServerTake(int rock)
        {
            if (rock < 0 || rock >= Positions.Count) return 0;
            TakenToday.TryGetValue(rock, out int taken);
            int amount = StoneRules.Take(taken, Sizes[rock]);
            TakenToday[rock] = taken + amount;
            return amount;
        }

        public static void ServerNewDay() => TakenToday.Clear();
    }
}
