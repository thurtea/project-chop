using UnityEngine;

namespace Chopper.Core
{
    // Pure wood arithmetic (plan D15, D16). Carried wood is capped; the hut store has no cap.
    public static class WoodRules
    {
        // Returns the new carried amount after picking up gain wood. Anything over the cap is lost.
        public static int AddCarried(int carried, int gain, int cap)
        {
            return Mathf.Clamp(carried + Mathf.Max(0, gain), 0, cap);
        }

        // Spends store wood to restore health. Never spends more than needed to reach max health.
        public static void Repair(int store, int health, int maxHealth, int healthPerWood, out int woodSpent, out int healthGained)
        {
            int missing = Mathf.Max(0, maxHealth - health);
            int woodNeeded = (missing + healthPerWood - 1) / healthPerWood;
            woodSpent = Mathf.Min(store, woodNeeded);
            healthGained = Mathf.Min(missing, woodSpent * healthPerWood);
        }
    }
}
