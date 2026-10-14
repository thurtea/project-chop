using Chopper.Core;
using UnityEngine;

namespace Chopper.World
{
    // Visual identity of each zone (a cross between Valheim's moody biomes and Minecraft's instantly readable ones):
    // natural ground colors (not literal element colors), a fog color and density that set the mood, and a leaf tint.
    //   Hub   bright yellow green meadow, light haze
    //   Water marsh: muddy dark teal ground, thick cool mist
    //   Earth dark deep woods: brown green floor, heavy green gloom
    //   Fire  ashlands: charcoal ground, warm smoky haze
    //   Wind  open highlands: pale golden grass, clear air
    public static class ZonePalette
    {
        public static string Name(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return "Water Forest";
                case Element.Fire: return "Fire Forest";
                case Element.Earth: return "Earth Forest";
                case Element.Wind: return "Wind Forest";
                default: return "Hub";
            }
        }

        public static Color Ground(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return new Color(0.26f, 0.37f, 0.30f);
                case Element.Fire: return new Color(0.36f, 0.28f, 0.24f);
                case Element.Earth: return new Color(0.34f, 0.31f, 0.17f);
                case Element.Wind: return new Color(0.62f, 0.58f, 0.34f);
                default: return new Color(0.42f, 0.53f, 0.23f);
            }
        }

        public static Color Fog(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return new Color(0.40f, 0.52f, 0.62f);
                case Element.Fire: return new Color(0.46f, 0.32f, 0.30f);
                case Element.Earth: return new Color(0.24f, 0.32f, 0.30f);
                case Element.Wind: return new Color(0.58f, 0.66f, 0.74f);
                // W11-08: warm meadow haze, so the distance reads pale gold instead of cool green.
                default: return new Color(0.80f, 0.76f, 0.66f);
            }
        }

        // Exponential squared fog density: thick mist in the marsh, gloom in the deep woods, clear air on the highlands.
        // Thick enough that nobody sees across the island. In the hub the air is half fog at about 88 m (the mountain
        // ring still shows through it) and solid well before the far coast; the camera stops drawing at FarClip.
        public static float FogDensity(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return 0.0120f;
                case Element.Fire: return 0.0110f;
                case Element.Earth: return 0.0130f;
                case Element.Wind: return 0.0090f;
                // W11-08 thicker hub mist (was 0.0060). Half fog near 88 m.
                default: return 0.0095f;
            }
        }

        // Past this the fog is solid in every zone, so nothing further away is drawn.
        public const float FarClip = 600f;

        // Label color for the HUD; brighter than the ground so it reads on any background.
        public static Color Label(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return new Color(0.4f, 0.75f, 1f);
                case Element.Fire: return new Color(1f, 0.5f, 0.25f);
                case Element.Earth: return new Color(0.75f, 0.65f, 0.35f);
                case Element.Wind: return new Color(0.8f, 1f, 0.95f);
                default: return new Color(0.7f, 1f, 0.6f);
            }
        }
    }
}
