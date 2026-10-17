using UnityEngine;

namespace Chopper.Core
{
    public static class ArcRules
    {
        public const float DefaultSpanDegrees = 140f;

        public static float Angle(float value01, float spanDegrees = DefaultSpanDegrees) =>
            (Mathf.Clamp01(value01) - 0.5f) * spanDegrees;

        public static float Offset(float value01) => Mathf.Clamp01(value01) * 2f - 1f;

        public static float FromOffset(float offset) => Mathf.Clamp01((Mathf.Clamp(offset, -1f, 1f) + 1f) * 0.5f);

        public static bool Within(float offset, float band) => Mathf.Abs(offset) <= band;

        public static Vector2 Point(float value01, float radius, float spanDegrees = DefaultSpanDegrees)
        {
            float a = Angle(value01, spanDegrees) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius);
        }
    }
}