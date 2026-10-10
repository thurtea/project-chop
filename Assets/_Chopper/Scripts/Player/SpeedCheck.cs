using UnityEngine;

namespace Chopper.Player
{
    // Pure math for the server movement sanity check. Horizontal and rising speed are checked separately: falling
    // under gravity is legitimately faster than running, so only going up is limited on the vertical axis.
    public static class SpeedCheck
    {
        public static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static bool IsTooFast(Vector3 from, Vector3 to, float seconds, float maxSpeed)
        {
            if (seconds <= 0f) return false;
            return HorizontalDistance(from, to) > maxSpeed * seconds;
        }

        // How far up the player went (0 when level or falling).
        public static float Rise(Vector3 from, Vector3 to) => Mathf.Max(0f, to.y - from.y);

        public static bool IsRisingTooFast(Vector3 from, Vector3 to, float seconds, float maxRiseSpeed)
        {
            if (seconds <= 0f) return false;
            return Rise(from, to) > maxRiseSpeed * seconds;
        }
    }
}
