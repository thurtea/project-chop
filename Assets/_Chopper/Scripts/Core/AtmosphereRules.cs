using UnityEngine;

namespace Chopper.Core
{
    public static class AtmosphereRules
    {
        public const float HazeStart = 60f;
        public const float HazeMax = 0.3f;
        public const float GradeWeight = 0.5f;
        public const float Balance = -15f;
        public static readonly Color HazeColor = new Color(0.68f, 0.76f, 0.86f);
        public static readonly Color ShadowTone = new Color(0.42f, 0.50f, 0.64f);
        public static readonly Color HighlightTone = new Color(0.66f, 0.56f, 0.44f);

        public const string StrengthKey = "chopper.atmosphere";
        public const float DensityCap = 0.3f;
        public const float DefaultStrength = 0.6f;
        public static float CaptureOverride = -1f;
        private static float _strength = -1f;

        public static float Strength
        {
            get
            {
                if (CaptureOverride >= 0f) return Mathf.Clamp01(CaptureOverride);
                if (_strength < 0f) _strength = Mathf.Clamp01(PlayerPrefs.GetFloat(StrengthKey, DefaultStrength));
                return _strength;
            }
            set { _strength = Mathf.Clamp01(value); PlayerPrefs.SetFloat(StrengthKey, _strength); }
        }

        public static float FogDensity(float strength, float darkness, bool inForest, bool indoors, float underground)
        {
            if (indoors || strength <= 0f) return 0f;
            float d = Mathf.Lerp(0.28f, 0.10f, Mathf.Clamp01(darkness)) * (inForest ? 0.7f : 1f) * (1f - Mathf.Clamp01(underground));
            return Mathf.Min(DensityCap, d * Mathf.Clamp01(strength) / 0.93f);
        }

        public static float ShaftStrength(float strength, float baseStrength, bool indoors) =>
            indoors ? 0f : baseStrength * Mathf.Clamp01(strength);

        public static float LinearEnd(float far) => HazeStart + Mathf.Max(1f, far - HazeStart) / HazeMax;

        public static float HazeAmount(float distance, float far)
        {
            float end = LinearEnd(far);
            return Mathf.Clamp01((distance - HazeStart) / (end - HazeStart));
        }

        public static Color HazeTint(Color dayTint, float darkness)
        {
            Color day = HazeColor * dayTint;
            Color c = Color.Lerp(day, day * 0.14f, Mathf.Clamp01(darkness));
            c.a = 1f;
            return c;
        }
    }
}