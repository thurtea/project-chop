using UnityEngine;

namespace Chopper.Core
{
    // How a swing wears a tree down. A tree holds chop points (its hits to fell from GameConfig, with a plain axe);
    // a swing takes off points by the axe's power, so a better axe (upgrades, boons, runes) fells in fewer swings.
    // A fist's punches add up to one plain hit (FistRules). Later skills multiply the power here too.
    public static class ChopRules
    {
        public const int PointsPerHit = 10;

        public static int TreePoints(int hitsToFell) => Mathf.Max(1, hitsToFell) * PointsPerHit;

        // Points one swing takes off: a plain axe (power 1) takes one hit's worth; never less than a point.
        public static int SwingPoints(float power) => Mathf.Max(1, Mathf.RoundToInt(PointsPerHit * Mathf.Max(0f, power)));

        public static int SwingsToFell(int hitsToFell, float power)
        {
            int swing = SwingPoints(power);
            return (TreePoints(hitsToFell) + swing - 1) / swing;
        }
    }
}
