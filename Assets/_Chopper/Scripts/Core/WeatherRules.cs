using UnityEngine;

namespace Chopper.Core
{
    public enum WeatherKind
    {
        Sunny = 0,
        Cloudy = 1,
        Rain = 2
    }

    // How the weather changes the world's look: multipliers on the sun, the ambient light and the sky's brightness,
    // how grey the sky turns, a saturation shift, fog density and tint, how hard it rains (0 to 1), and how hard the
    // wind blows (1 is a sunny day's breeze; the grass sways and the rain slants by it).
    public struct WeatherLook
    {
        public float Sun;
        public float Ambient;
        public float Sky;
        public float Overcast;
        public float Saturation;
        public float FogDensity;
        public Color FogTint;
        public float Rain;
        public float Wind;

        public static WeatherLook Lerp(WeatherLook a, WeatherLook b, float t)
        {
            t = Mathf.Clamp01(t);
            return new WeatherLook
            {
                Sun = Mathf.Lerp(a.Sun, b.Sun, t),
                Ambient = Mathf.Lerp(a.Ambient, b.Ambient, t),
                Sky = Mathf.Lerp(a.Sky, b.Sky, t),
                Overcast = Mathf.Lerp(a.Overcast, b.Overcast, t),
                Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t),
                FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t),
                FogTint = Color.Lerp(a.FogTint, b.FogTint, t),
                Rain = Mathf.Lerp(a.Rain, b.Rain, t),
                Wind = Mathf.Lerp(a.Wind, b.Wind, t)
            };
        }
    }

    // Pure weather rules. The weather drifts between sun, cloud and rain: rain always comes and goes through cloud,
    // and each spell lasts a few minutes. Fish bite sooner in the rain.
    public static class WeatherRules
    {
        public const float MinSpellSeconds = 100f;
        public const float MaxSpellSeconds = 260f;
        public const float BlendSeconds = 20f;

        // The one way the wind blows over the whole world (x and z): the grass leans and the rain slants along it.
        // The grass shader (ChopperGrass) uses the same direction.
        public static readonly Vector2 WindDirection = new Vector2(0.8f, 0.6f);

        public static string Name(WeatherKind kind) => kind == WeatherKind.Rain ? "Rain" : kind == WeatherKind.Cloudy ? "Cloudy" : "Sunny";

        // The weather after this one, from a roll of 0 to 1.
        public static WeatherKind Next(WeatherKind current, double roll)
        {
            switch (current)
            {
                case WeatherKind.Sunny: return roll < 0.5 ? WeatherKind.Sunny : WeatherKind.Cloudy;
                case WeatherKind.Cloudy: return roll < 0.35 ? WeatherKind.Sunny : roll < 0.65 ? WeatherKind.Cloudy : WeatherKind.Rain;
                default: return roll < 0.6 ? WeatherKind.Cloudy : WeatherKind.Rain;
            }
        }

        // Seconds a spell of weather lasts, from a roll of 0 to 1. Rain is a little shorter.
        public static float Duration(WeatherKind kind, double roll)
        {
            float seconds = Mathf.Lerp(MinSpellSeconds, MaxSpellSeconds, (float)roll);
            return kind == WeatherKind.Rain ? seconds * 0.75f : seconds;
        }

        public static WeatherLook Look(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Cloudy:
                    return new WeatherLook { Sun = 0.55f, Ambient = 0.85f, Sky = 0.6f, Overcast = 0.65f, Saturation = -12f, FogDensity = 1.5f, FogTint = new Color(0.86f, 0.87f, 0.88f), Rain = 0f, Wind = 1.4f };
                case WeatherKind.Rain:
                    return new WeatherLook { Sun = 0.3f, Ambient = 0.66f, Sky = 0.38f, Overcast = 1f, Saturation = -22f, FogDensity = 2.6f, FogTint = new Color(0.68f, 0.72f, 0.76f), Rain = 1f, Wind = 2f };
                default:
                    return new WeatherLook { Sun = 1f, Ambient = 1f, Sky = 1f, Overcast = 0f, Saturation = 0f, FogDensity = 1f, FogTint = Color.white, Rain = 0f, Wind = 1f };
            }
        }

        // Fish bite sooner when it rains.
        public static float BiteMultiplier(WeatherKind kind) => kind == WeatherKind.Rain ? 0.8f : 1f;
    }
}
