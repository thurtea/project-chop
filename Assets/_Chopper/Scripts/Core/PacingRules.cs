using UnityEngine;

namespace Chopper.Core
{
    // Pure pacing estimates used by tests to keep the day and night lengths honest. The day must be long enough
    // to visit all four forests; the night must be long enough for the farthest enemy to reach the hut.
    public static class PacingRules
    {
        // Walk from the hut to the middle of each forest in turn (north, east, south, west) and back home.
        // The four forest centers sit on a circle of radius forestCenterRadius, so the legs between neighbors are
        // chords of length radius times root two.
        public static float CircuitWalkSeconds(float forestCenterRadius, float moveSpeed)
        {
            float distance = 2f * forestCenterRadius + 3f * Mathf.Sqrt(2f) * forestCenterRadius;
            return distance / Mathf.Max(0.1f, moveSpeed);
        }

        // Time spent chopping: each tree costs its swings plus a short walk to the next tree.
        public static float ChopSeconds(int treesPerForest, int forests, float averageHits, float swingCooldown, float repositionSeconds)
        {
            return treesPerForest * forests * (averageHits * swingCooldown + repositionSeconds);
        }

        // Seconds for an enemy to cover a distance when it runs at multiplier times its speed while farther than
        // chargeDistance from the target.
        public static float ChargeWalkSeconds(float distance, float speed, float multiplier, float chargeDistance)
        {
            float far = Mathf.Max(0f, distance - chargeDistance);
            float near = Mathf.Min(distance, chargeDistance);
            return far / Mathf.Max(0.1f, speed * multiplier) + near / Mathf.Max(0.1f, speed);
        }

        // Seconds for an enemy to walk the given distance at the given speed.
        public static float WalkSeconds(float distance, float speed)
        {
            return distance / Mathf.Max(0.1f, speed);
        }
    }
}
