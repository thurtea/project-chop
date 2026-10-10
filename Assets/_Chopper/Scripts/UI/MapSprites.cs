using UnityEngine;

namespace Chopper.UI
{
    // Small procedural sprites for the maps: a thin ring for the minimap edge, a soft edge shadow, a house for the hut,
    // the player arrow and dots with a dark outline so they read on any ground color.
    public static class MapSprites
    {
        private static Sprite _ring;
        private static Sprite _vignette;
        private static Sprite _arrow;
        private static Sprite _dot;

        // A thin annulus with soft edges (outer radius 1, inner about 0.94).
        public static Sprite Ring => _ring != null ? _ring : (_ring = Draw(256, (u, v) =>
        {
            float d = Distance(u, v);
            float outer = Mathf.Clamp01((1f - d) * 128f);
            float inner = Mathf.Clamp01((d - 0.94f) * 128f);
            float light = Mathf.Lerp(0.75f, 1.1f, v);
            return new Color(light, light, light, outer * inner);
        }));

        // Transparent in the middle, darker toward the rim: gives the map a lens-like depth.
        public static Sprite Vignette => _vignette != null ? _vignette : (_vignette = Draw(128, (u, v) =>
        {
            float d = Distance(u, v);
            float alpha = Mathf.SmoothStep(0.55f, 1f, d) * 0.65f;
            return new Color(0f, 0f, 0f, d > 1f ? 0f : alpha);
        }));

        // The hut: a longhouse from the icon set, white with a dark rim, tinted by the map.
        public static Sprite House => HotbarIcons.Get("map-house");

        // Upward arrowhead with a notch, white with a dark outline.
        public static Sprite Arrow => _arrow != null ? _arrow : (_arrow = Draw(64, (u, v) =>
        {
            bool Inside(float x, float y, float grow)
            {
                float half = (1f - y) * 0.5f + grow;
                float side = Mathf.Abs(x - 0.5f);
                bool body = y > 0.06f - grow && y < 0.98f + grow && side < half;
                bool notch = y < 0.24f - grow && side < (0.24f - y) * 0.5f - grow;
                return body && !notch;
            }
            if (Inside(u, v, 0f)) return Color.white;
            return Inside(u, v, 0.06f) ? new Color(0f, 0f, 0f, 0.9f) : Color.clear;
        }));

        // A crypt entrance from the icon set, white with a dark rim.
        public static Sprite Crypt => HotbarIcons.Get("map-crypt");

        // A filled circle with a dark rim, tinted by the icon color.
        public static Sprite Dot => _dot != null ? _dot : (_dot = Draw(64, (u, v) =>
        {
            float d = Distance(u, v);
            if (d > 1f) return Color.clear;
            float edge = Mathf.Clamp01((1f - d) * 24f);
            return d > 0.72f ? new Color(0.08f, 0.06f, 0.05f, 0.9f * edge) : Color.white;
        }));

        private static float Distance(float u, float v) => Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;

        private static Sprite Draw(int size, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) texture.SetPixel(x, y, pixel((x + 0.5f) / size, (y + 0.5f) / size));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
