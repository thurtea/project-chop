using UnityEngine;

namespace Chopper.Core
{
    public static class ClimbCycle
    {
        public const float Rung = 0.3f;
        public const float Cycle = Rung * 2f;
        public const float HoldPart = 0.7f;
        public const float ReachOut = 0.08f;

        public static float Grip(float rootY, float reachAbove, float offset, out float lift)
        {
            float k = (rootY + reachAbove) / Cycle + offset;
            float i = Mathf.Floor(k);
            float f = k - i;
            float planted = (i - offset) * Cycle;
            lift = 0f;
            if (f < HoldPart) return planted;
            float t = (f - HoldPart) / (1f - HoldPart);
            t = t * t * (3f - 2f * t);
            lift = Mathf.Sin(t * Mathf.PI) * ReachOut;
            return Mathf.Lerp(planted, planted + Cycle, t);
        }

        public static float Ease(float blend) => blend * blend * (3f - 2f * blend);
    }
}
