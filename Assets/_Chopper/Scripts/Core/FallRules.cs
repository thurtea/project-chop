using UnityEngine;

namespace Chopper.Core
{
    // Falling hurts past a few meters, Minecraft style: nothing up to SafeHeight, then a share of the maximum health that
    // grows with the drop until DeadlyHeight kills outright. Landing in water deep enough to swim breaks any fall. Armor
    // does not help.
    public static class FallRules
    {
        public const float SafeHeight = 4f;
        public const float DeadlyHeight = 20f;
        // A client never reports more than this (a teleport or a bad value).
        public const float MaxReportedHeight = 200f;

        public static int Damage(float height, int maxHealth)
        {
            if (height <= SafeHeight || maxHealth <= 0) return 0;
            float share = (Mathf.Min(height, MaxReportedHeight) - SafeHeight) / (DeadlyHeight - SafeHeight);
            return Mathf.CeilToInt(maxHealth * share);
        }
    }
}
