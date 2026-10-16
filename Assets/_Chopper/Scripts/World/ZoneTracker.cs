using Chopper.Core;
using Chopper.Player;
using UnityEngine;

namespace Chopper.World
{
    // Client side. Tracks which zone the local player stands in and eases the fog toward that zone's color,
    // so each forest feels different even away from the trees.
    public class ZoneTracker : MonoBehaviour
    {
        private const float FogEaseSpeed = 1.2f;
        private const float NightFogBrightness = 0.16f;
        private const float NightFogDensityScale = 1.3f;
        private const float CryptFogDensity = 0.035f;
        private static readonly Color CryptFog = new Color(0.05f, 0.04f, 0.035f);

        [SerializeField] private GameConfig _config;

        public static Element Current { get; private set; } = Element.Neutral;

        private void Update()
        {
            PlayerWood local = PlayerWood.Local;
            if (local == null) return;

            Current = ZoneMap.ElementAt(local.transform.position, _config.HubRadius);
            DayNightCycle cycle = DayNightCycle.Instance;
            float darkness = cycle != null ? cycle.Darkness : 0f;
            Color haze = cycle != null ? cycle.HazeTint : Color.white;
            bool forest = Current != Element.Neutral;
            float nightDim = Mathf.Lerp(1f, forest ? 0.28f : NightFogBrightness, darkness);
            Color target = ZonePalette.Fog(Current) * haze * nightDim;
            if (forest) target *= 0.55f;
            float low = 0f;
            if (forest && WorldGround.Exists)
            {
                float above = local.transform.position.y - WorldGround.HeightAt(local.transform.position);
                low = Mathf.Clamp01(1f - above / 16f);
                target = Color.Lerp(target, target * new Color(0.55f, 0.68f, 0.80f), low * 0.7f);
            }
            // Inside a crypt the far rooms fade into a warm dark.
            float below = DayNightCycle.UndergroundBlend;
            // Cloud and rain grey the air and thicken it (WeatherSystem reads clear weather in a crypt).
            WeatherLook weather = WeatherSystem.Look;
            target *= weather.FogTint;
            target = Color.Lerp(target, CryptFog, below);
            target.a = 1f;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, target, (forest ? 2.8f : FogEaseSpeed) * Time.deltaTime);
            float nightFog = forest ? 1.35f : NightFogDensityScale;
            float density = ZonePalette.FogDensity(Current) * Mathf.Lerp(1f, nightFog, darkness) * weather.FogDensity;
            if (forest) density *= Mathf.Lerp(1f, 1.15f, low);
            density = Mathf.Lerp(density, CryptFogDensity, below);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, density, (forest ? 2.8f : FogEaseSpeed) * Time.deltaTime);
        }
    }
}
