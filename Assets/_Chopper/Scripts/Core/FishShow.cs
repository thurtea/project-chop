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
        public const float RunCutDepth = 0.45f;
        public const float RunCutFar = 14f;

        public static Vector3 RunCutEye(Vector3 fish, Vector3 heading, float k, float water, System.Func<Vector3, float> ground)
        {
            Vector3 dir = new Vector3(heading.x, 0f, heading.z);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float under = water - RunCutDepth;
            float start = Mathf.Lerp(3.2f, 2.0f, Mathf.Clamp01(k));
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? 1f : -1f;
                for (float d = start; d >= 0.6f; d -= 0.2f)
                {
                    Vector3 eye = fish + side * (sign * d) - dir * 0.6f;
                    if (ground(eye) > under - 0.3f) continue;
                    eye.y = under;
                    return eye;
                }
            }
            Vector3 behind = fish - dir * 0.8f;
            behind.y = Mathf.Min(under, fish.y);
            return behind;
        }

        public static Vector3 RunCutLook(Vector3 fish, Vector3 eye) => new Vector3(fish.x, Mathf.Min(fish.y, eye.y - 0.1f), fish.z);
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
