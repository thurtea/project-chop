using UnityEngine;

namespace Chopper.Core
{
    // Pure random rules for sapling drops (plan D17) and upgrade tables.
    public static class DropRules
    {
        // Rolls min..max saplings for a felled tree, multiplies by the player count multiplier, and rounds
        // stochastically so that the average matches the multiplier exactly.
        public static int RollSaplings(System.Random rng, int minCount, int maxCount, float multiplier)
        {
            int low = Mathf.Max(0, Mathf.Min(minCount, maxCount));
            int high = Mathf.Max(low, maxCount);
            return Scale(rng, rng.Next(low, high + 1), multiplier);
        }

        // Rolls 0..maxPerHit, multiplies by the player count multiplier, and rounds stochastically so that
        // the average matches the multiplier exactly.
        public static int RollSaplings(System.Random rng, int maxPerHit, float multiplier)
        {
            return Scale(rng, rng.Next(0, Mathf.Max(0, maxPerHit) + 1), multiplier);
        }

        private static int Scale(System.Random rng, int baseCount, float multiplier)
        {
            float scaled = baseCount * Mathf.Max(0f, multiplier);
            int whole = Mathf.FloorToInt(scaled);
            float fraction = scaled - whole;
            return whole + (rng.NextDouble() < fraction ? 1 : 0);
        }

        // Cost of the next upgrade from the given level, or false when the item is at its maximum level.
        public static bool TryGetNextCost(int[] costs, int level, out int cost)
        {
            if (level >= 0 && level < costs.Length)
            {
                cost = costs[level];
                return true;
            }
            cost = 0;
            return false;
        }

        // Table lookup clamped to the last entry, so a level past the table keeps the best value.
        public static float Lookup(float[] table, int level)
        {
            if (table.Length == 0) return 1f;
            return table[Mathf.Clamp(level, 0, table.Length - 1)];
        }
    }
}
