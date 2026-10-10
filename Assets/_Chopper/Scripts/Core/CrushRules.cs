using UnityEngine;

namespace Chopper.Core
{
    // How much a large falling tree hurts. Without protection it kills outright; every level of Sturdy gear (crafted
    // at the workbench from iron found in the crypts) leaves more of the player standing. Blocking, rolling and wood
    // armor do not help against a falling trunk.
    public static class CrushRules
    {
        public const int MaxSturdyLevel = 3;

        // Share of the maximum health dealt, by Sturdy level 0 to 3.
        private static readonly float[] DamageFractions = { 1.5f, 0.8f, 0.55f, 0.3f };

        public static int Damage(int maxHealth, int sturdyLevel)
        {
            int level = Mathf.Clamp(sturdyLevel, 0, MaxSturdyLevel);
            return Mathf.CeilToInt(maxHealth * DamageFractions[level]);
        }

        // True when a player at full health lives through one falling tree.
        public static bool SurvivesFromFull(int maxHealth, int sturdyLevel) => Damage(maxHealth, sturdyLevel) < maxHealth;

        // "kills you", "takes 80% of your health": for the workbench and the death screen.
        public static string Describe(int sturdyLevel)
        {
            int level = Mathf.Clamp(sturdyLevel, 0, MaxSturdyLevel);
            float fraction = DamageFractions[level];
            return fraction >= 1f ? "kills you outright" : $"takes {Mathf.RoundToInt(fraction * 100f)}% of your health";
        }
    }
}
