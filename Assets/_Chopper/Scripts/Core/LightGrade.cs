using UnityEngine;

namespace Chopper.Core
{
    public struct GradeLook
    {
        public Color Shadows;
        public Color Highlights;
        public float Balance;
        public float Contrast;
        public float Saturation;
        public float Exposure;
        public Color SunColor;
        public float SunScale;
    }

    public static class LightGrade
    {
        public const float BloomThreshold = 1.1f;
        public const float AcesLift = 0.6f;

        public static readonly GradeLook Dawn = new GradeLook
        {
            Shadows = new Color(0.40f, 0.48f, 0.60f), Highlights = new Color(0.74f, 0.56f, 0.52f),
            Balance = 10f, Contrast = 6f, Saturation = -22f, Exposure = 0.25f,
            SunColor = new Color(1f, 0.70f, 0.55f), SunScale = 0.85f
        };

        public static readonly GradeLook Noon = new GradeLook
        {
            Shadows = new Color(0.36f, 0.54f, 0.58f), Highlights = new Color(0.66f, 0.57f, 0.44f),
            Balance = 0f, Contrast = 10f, Saturation = -24f, Exposure = 0.15f,
            SunColor = new Color(1f, 0.90f, 0.74f), SunScale = 1.05f
        };

        public static readonly GradeLook Dusk = new GradeLook
        {
            Shadows = new Color(0.34f, 0.46f, 0.56f), Highlights = new Color(0.84f, 0.52f, 0.30f),
            Balance = 18f, Contrast = 14f, Saturation = -12f, Exposure = 0.2f,
            SunColor = new Color(1f, 0.50f, 0.24f), SunScale = 0.9f
        };

        public static readonly GradeLook Night = new GradeLook
        {
            Shadows = new Color(0.30f, 0.40f, 0.62f), Highlights = new Color(0.48f, 0.52f, 0.64f),
            Balance = -15f, Contrast = 4f, Saturation = -18f, Exposure = 0f,
            SunColor = new Color(0.50f, 0.62f, 1f), SunScale = 1f
        };

        public static Color ZoneHighlight(Element zone)
        {
            switch (zone)
            {
                case Element.Fire: return new Color(0.86f, 0.52f, 0.30f);
                case Element.Water: return new Color(0.48f, 0.62f, 0.62f);
                case Element.Earth: return new Color(0.62f, 0.62f, 0.38f);
                case Element.Wind: return new Color(0.62f, 0.64f, 0.66f);
                default: return new Color(0.66f, 0.57f, 0.44f);
            }
        }

        public const float MoonLow = 0.22f;
        public const float MoonHigh = 0.40f;
        public static readonly Color NightAmbientFloor = new Color(0.11f, 0.14f, 0.24f);
        public static readonly Color NightEquatorFloor = new Color(0.07f, 0.09f, 0.15f);
        public static readonly Color NightGroundFloor = new Color(0.035f, 0.04f, 0.06f);

        public static float MoonIntensity(float nightT) => Mathf.Lerp(MoonLow, MoonHigh, Mathf.Sin(Mathf.Clamp01(nightT) * Mathf.PI));

        public static float MoonPitch(float nightT) => Mathf.Lerp(18f, 52f, Mathf.Sin(Mathf.Clamp01(nightT) * Mathf.PI));

        public static Color AtLeast(Color c, Color floor, float weight) =>
            Color.Lerp(c, new Color(Mathf.Max(c.r, floor.r), Mathf.Max(c.g, floor.g), Mathf.Max(c.b, floor.b), c.a), Mathf.Clamp01(weight));

        public static float Flicker(float baseIntensity, float noiseA, float noiseB) =>
            baseIntensity * (0.82f + 0.22f * noiseA + 0.06f * noiseB);

        public static float ZoneWeight(Element zone) => zone == Element.Neutral ? 0f : 0.3f;

        public static GradeLook Lerp(GradeLook a, GradeLook b, float t)
        {
            t = Mathf.Clamp01(t);
            return new GradeLook
            {
                Shadows = Color.Lerp(a.Shadows, b.Shadows, t), Highlights = Color.Lerp(a.Highlights, b.Highlights, t),
                Balance = Mathf.Lerp(a.Balance, b.Balance, t), Contrast = Mathf.Lerp(a.Contrast, b.Contrast, t),
                Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t), Exposure = Mathf.Lerp(a.Exposure, b.Exposure, t),
                SunColor = Color.Lerp(a.SunColor, b.SunColor, t), SunScale = Mathf.Lerp(a.SunScale, b.SunScale, t)
            };
        }

        public static GradeLook For(Element zone, float dayT, float lowSun, float darkness)
        {
            GradeLook low = dayT < 0.5f ? Dawn : Dusk;
            GradeLook look = Lerp(Noon, low, lowSun);
            look = Lerp(look, Night, darkness);
            look.Highlights = Color.Lerp(look.Highlights, ZoneHighlight(zone), ZoneWeight(zone) * (1f - darkness));
            return look;
        }
    }
}