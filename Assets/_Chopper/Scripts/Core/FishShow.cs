using UnityEngine;

namespace Chopper.Core
{
    public static class FishShow
    {
        public const float LeapSeconds = 0.8f;
        public const float LeapPeak = 1.1f;
        public const float LeapMinGap = 3.5f;
        public const float LeapMaxGap = 7f;
        public const float RunCutSeconds = 1.4f;
        public const float LineNearWidth = 0.035f;
        public const float LineFarWidth = 0.024f;
        public const float LineGrowFrom = 6f;
        public const float LineMaxScale = 1.6f;
        public const float ArcSizeFactor = 0.32f;
        public const float ArcGrowFrom = 10f;
        public const float ArcMaxScale = 1.4f;

        public static float LeapHeight(float elapsed)
        {
            if (elapsed <= 0f || elapsed >= LeapSeconds) return 0f;
            float t = elapsed / LeapSeconds;
            return LeapPeak * 4f * t * (1f - t);
        }

        public static float NextLeapGap(float random01) => Mathf.Lerp(LeapMinGap, LeapMaxGap, Mathf.Clamp01(random01));

        public static bool LeapsIn(FishPhase phase) => phase == FishPhase.Pulling || phase == FishPhase.Thrashing;

        public static bool StartsRunCut(FishPhase before, FishPhase now) => now == FishPhase.Pulling && before != FishPhase.Pulling;

        public static float ArcScale(float cameraDistance) => Mathf.Clamp(cameraDistance / ArcGrowFrom, 1f, ArcMaxScale);

        public static float LineScale(float cameraDistance) => Mathf.Clamp(cameraDistance / LineGrowFrom, 1f, LineMaxScale);
    }
}
